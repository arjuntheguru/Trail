using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Trail.Application.Common.Security;
using Trail.Application.Features.Attachments;
using Trail.Domain.Files;
using Trail.Domain.Organizations;
using Trail.IntegrationTests.Infrastructure;
using Trail.IntegrationTests.Tasks;

namespace Trail.IntegrationTests.Attachments;

public sealed class AttachmentTests(MySqlFixture mysql) : IAsyncLifetime
{
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private TrailApiFactory _factory = null!;
    private string _root = null!;
    private TestMember _owner = null!;
    private TaskTestKit _kit = null!;
    private Site _site = null!;

    // Uploads must not land in the repository: run the API against a temporary storage root.
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _web = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _root = Path.Combine(Path.GetTempPath(), $"trail-uploads-{Guid.NewGuid():N}");
        _web = _factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration(c =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:LocalPath"] = _root })));
        _owner = await _factory.CreateOrganizationAsync();
        _kit = new TaskTestKit(_factory, _owner.OrganizationId);
        _site = await _kit.AddSiteAsync("London");
    }

    public async ValueTask DisposeAsync()
    {
        await _web.DisposeAsync();
        await _factory.DisposeAsync();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A client on the temp-storage host carrying the same bearer token the factory would issue.</summary>
    private HttpClient ClientFor(TestMember member)
    {
        var client = _web.CreateClient();
        client.DefaultRequestHeaders.Authorization = _factory.CreateClient(member).DefaultRequestHeaders.Authorization;
        return client;
    }

    private static byte[] Png(int size = 64)
    {
        var bytes = new byte[size];
        PngHeader.CopyTo(bytes, 0);
        Random.Shared.NextBytes(bytes.AsSpan(PngHeader.Length));
        return bytes;
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] content, string fileName, string claimedType, string kind, Guid siteId)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(claimedType);
        var form = new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(kind), "kind" },
            { new StringContent(siteId.ToString()), "siteId" },
        };
        return client.PostAsync("/api/v1/attachments", form, Ct);
    }

    [Fact]
    public async Task An_uploaded_photo_downloads_unchanged_with_the_right_headers()
    {
        var client = ClientFor(_owner);
        var bytes = Png();

        var response = await UploadAsync(client, bytes, "fridge.png", "image/png", "Photo", _site.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var uploaded = await response.ReadAsync<AttachmentResponse>();
        uploaded.Kind.ShouldBe(AttachmentKind.Photo);
        uploaded.ContentType.ShouldBe("image/png");
        uploaded.Size.ShouldBe(bytes.Length);
        uploaded.FileName.ShouldBe("fridge.png");
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/attachments/{uploaded.Id}");

        var stored = Directory.GetFiles(_root, "*.png", SearchOption.AllDirectories).ShouldHaveSingleItem();
        stored.ShouldContain(_owner.OrganizationId.ToString());
        stored.ShouldContain(uploaded.Id.ToString());
        var row = await _kit.InDbAsync(db => db.Attachments.FindAsync([uploaded.Id], Ct).AsTask());
        row!.Sha256.ShouldBe(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)));
        row.SiteId.ShouldBe(_site.Id);

        var meta = await (await client.GetAsync($"/api/v1/attachments/{uploaded.Id}", Ct)).ReadAsync<AttachmentResponse>();
        meta.Id.ShouldBe(uploaded.Id);

        var download = await client.GetAsync($"/api/v1/attachments/{uploaded.Id}/content", Ct);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(bytes);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        download.Content.Headers.ContentDisposition.FileName.ShouldBe("fridge.png");
        download.Headers.CacheControl!.Private.ShouldBeTrue();
        download.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task The_real_type_is_detected_and_the_claimed_name_is_sanitized()
    {
        var client = ClientFor(_owner);

        // Claimed as a JPEG with a path in the name; the content is a PNG.
        var uploaded = await (await UploadAsync(client, Png(), "..\\..\\etc/evil.jpg", "image/jpeg", "Photo", _site.Id))
            .ReadAsync<AttachmentResponse>();

        uploaded.ContentType.ShouldBe("image/png");
        uploaded.FileName.ShouldBe("evil.jpg");
        uploaded.FileName.ShouldNotContain("/");
        uploaded.FileName.ShouldNotContain("\\");
    }

    [Theory]
    [InlineData("  ", "upload.png")]
    [InlineData("..", "upload.png")]
    [InlineData("a\u0001b\u0007.png", "ab.png")]
    public void File_names_are_cleaned(string claimed, string expected) =>
        UploadAttachmentHandler.SanitizeFileName(claimed, ".png").ShouldBe(expected);

    [Fact]
    public void Long_file_names_are_shortened_keeping_the_extension()
    {
        var name = UploadAttachmentHandler.SanitizeFileName(new string('a', 400) + ".png", ".png");

        name.Length.ShouldBe(Attachment.FileNameMaxLength);
        name.ShouldEndWith(".png");
    }

    [Fact]
    public async Task A_text_file_labelled_as_an_image_is_rejected()
    {
        var response = await UploadAsync(ClientFor(_owner), "just some text, not a picture"u8.ToArray(), "x.png", "image/png", "Photo", _site.Id);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, "attachment.unsupported_type");
        Directory.Exists(_root).ShouldBeFalse();
    }

    [Fact]
    public async Task An_oversize_file_is_rejected()
    {
        var response = await UploadAsync(ClientFor(_owner), Png((int)Attachment.MaxBytes + 1), "big.png", "image/png", "Photo", _site.Id);

        await response.ShouldBeAsync(HttpStatusCode.BadRequest, "attachment.too_large");
    }

    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        (await UploadAsync(ClientFor(_owner), [], "none.png", "image/png", "Photo", _site.Id))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_documents_may_be_pdfs_and_they_download_as_attachments()
    {
        var client = ClientFor(_owner);
        var pdf = "%PDF-1.7\n%test"u8.ToArray();

        await (await UploadAsync(client, pdf, "cert.pdf", "application/pdf", "Photo", _site.Id)).ShouldBeAsync(HttpStatusCode.BadRequest, "attachment.unsupported_type");
        await (await UploadAsync(client, pdf, "cert.pdf", "application/pdf", "Signature", _site.Id)).ShouldBeAsync(HttpStatusCode.BadRequest, "attachment.unsupported_type");

        var uploaded = await (await UploadAsync(client, pdf, "cert.pdf", "application/pdf", "Document", _site.Id)).ReadAsync<AttachmentResponse>();
        uploaded.ContentType.ShouldBe("application/pdf");

        var download = await client.GetAsync($"/api/v1/attachments/{uploaded.Id}/content", Ct);
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        (await download.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(pdf);
    }

    [Fact]
    public async Task Uploads_need_a_file_a_kind_and_an_existing_site()
    {
        var client = ClientFor(_owner);

        var noFile = new MultipartFormDataContent { { new StringContent("Photo"), "kind" }, { new StringContent(_site.Id.ToString()), "siteId" } };
        (await client.PostAsync("/api/v1/attachments", noFile, Ct)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await UploadAsync(client, Png(), "a.png", "image/png", "Banana", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await (await UploadAsync(client, Png(), "a.png", "image/png", "Photo", Guid.NewGuid())).ShouldBeAsync(HttpStatusCode.NotFound, "site.not_found");
    }

    [Fact]
    public async Task Scoped_members_only_use_their_own_sites_and_other_organizations_see_nothing()
    {
        var other = await _kit.AddSiteAsync("Elsewhere");
        var owner = ClientFor(_owner);
        var onOther = await (await UploadAsync(owner, Png(), "o.png", "image/png", "Photo", other.Id)).ReadAsync<AttachmentResponse>();
        var onSite = await (await UploadAsync(owner, Png(), "s.png", "image/png", "Photo", _site.Id)).ReadAsync<AttachmentResponse>();

        var member = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.TeamMember, allSites: false);
        await _factory.CreateClient(_owner).SetAccessAsync(member.MembershipId, false, siteIds: [_site.Id]);
        var scoped = ClientFor(member);

        (await UploadAsync(scoped, Png(), "n.png", "image/png", "Photo", other.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await UploadAsync(scoped, Png(), "n.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await scoped.GetAsync($"/api/v1/attachments/{onSite.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await scoped.GetAsync($"/api/v1/attachments/{onOther.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await scoped.GetAsync($"/api/v1/attachments/{onOther.Id}/content", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var intruder = ClientFor(await _factory.CreateOrganizationAsync("Other Org"));
        (await intruder.GetAsync($"/api/v1/attachments/{onSite.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await intruder.GetAsync($"/api/v1/attachments/{onSite.Id}/content", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await UploadAsync(intruder, Png(), "n.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploading_needs_permission_to_complete_tasks_or_raise_issues()
    {
        async Task<HttpClient> ClientWith(string name, params string[] permissions)
        {
            await _kit.InDbAsync(async db =>
            {
                db.Roles.Add(new Trail.Domain.Identity.Role { Name = name, Permissions = [.. permissions] });
                await db.SaveChangesAsync();
                return 0;
            });
            return ClientFor(await _factory.AddMemberAsync(_owner.OrganizationId, name));
        }

        var viewer = await ClientWith("Viewer", Permissions.Tasks.View, Permissions.Sites.View);
        var raiser = await ClientWith("Raiser", Permissions.Issues.Raise);
        var completer = await ClientWith("Completer", Permissions.Tasks.Complete);

        (await UploadAsync(viewer, Png(), "a.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await UploadAsync(raiser, Png(), "a.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UploadAsync(completer, Png(), "a.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await UploadAsync(_web.CreateClient(), Png(), "a.png", "image/png", "Photo", _site.Id)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Viewing needs only membership of the organization.
        var uploaded = await (await UploadAsync(completer, Png(), "a.png", "image/png", "Photo", _site.Id)).ReadAsync<AttachmentResponse>();
        (await viewer.GetAsync($"/api/v1/attachments/{uploaded.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
