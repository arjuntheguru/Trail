using System.Net;
using Trail.Application.Common.Security;
using Trail.Application.Features.Tags;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Tags;

public sealed class TagTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _client = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Tags_can_be_created_listed_updated_and_deleted()
    {
        var created = await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Opening", "#1F8A70"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tag = await created.ReadAsync<TagResponse>();
        tag.Color.ShouldBe("#1F8A70");
        (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Allergens", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var list = await (await _client.GetAsync("/api/v1/tags", Ct)).ReadAsync<List<TagResponse>>();
        list.Select(t => t.Name).ShouldBe(["Allergens", "Opening"]);

        var updated = await _client.PutJsonAsync($"/api/v1/tags/{tag.Id}", new UpdateTagCommand("Closing", "#ff00AA"));
        var body = await updated.ReadAsync<TagResponse>();
        (body.Name, body.Color).ShouldBe(("Closing", "#ff00AA"));

        (await _client.DeleteAsync($"/api/v1/tags/{tag.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/v1/tags/{tag.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    [InlineData("123456")]
    public async Task Invalid_colors_are_rejected(string color)
    {
        (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Tag", color)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_names_are_rejected()
    {
        (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("", null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand(new string('x', 65), null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_names_conflict()
    {
        await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Dup", null));
        var other = await (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Other", null))).ReadAsync<TagResponse>();

        var duplicate = await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("dup", null));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("tag.name_taken");

        var rename = await _client.PutJsonAsync($"/api/v1/tags/{other.Id}", new UpdateTagCommand("Dup", null));
        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await rename.ProblemCodeAsync()).ShouldBe("tag.name_taken");
    }

    [Fact]
    public async Task Concurrent_creates_of_the_same_name_yield_one_tag_and_conflicts()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Race", null))));

        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
    }

    [Fact]
    public async Task Unknown_tags_return_not_found()
    {
        (await _client.PutJsonAsync($"/api/v1/tags/{Guid.NewGuid()}", new UpdateTagCommand("X", null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Permissions_are_enforced()
    {
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId));
        // Team Members have neither Templates.View nor Tags.Manage.
        (await teamMember.GetAsync("/api/v1/tags", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Nope", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Area Managers can view tags (Templates.View) but not manage them.
        var areaManager = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.AreaManager));
        (await areaManager.GetAsync("/api/v1/tags", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await areaManager.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Nope", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_change_tags()
    {
        var tag = await (await _client.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Private", null))).ReadAsync<TagResponse>();
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await (await other.GetAsync("/api/v1/tags", Ct)).ReadAsync<List<TagResponse>>()).ShouldBeEmpty();
        (await other.PutJsonAsync($"/api/v1/tags/{tag.Id}", new UpdateTagCommand("Hacked", null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/v1/tags/{tag.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostJsonAsync("/api/v1/tags", new CreateTagCommand("Private", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
