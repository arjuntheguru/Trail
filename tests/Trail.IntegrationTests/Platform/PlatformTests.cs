using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Features.Auth;
using Trail.Application.Features.Platform;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Platform;

public sealed class PlatformTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;

    public async ValueTask InitializeAsync() => _factory = await TrailApiFactory.CreateAsync(mysql);

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private HttpClient PlatformAdmin()
    {
        var token = _factory.Services.GetRequiredService<IAccessTokenIssuer>()
            .Issue(new AccessTokenSubject(Guid.CreateVersion7(), "staff@trail.test", "Staff", true, null, null, []));
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    [Fact]
    public async Task Customers_cannot_use_platform_endpoints()
    {
        var owner = await _factory.CreateOrganizationAsync();

        (await _factory.CreateClient(owner).GetAsync("/api/v1/platform/organizations", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Platform_admin_onboards_an_organization_whose_owner_accepts_by_email()
    {
        var admin = PlatformAdmin();
        var email = $"founder-{Guid.NewGuid():N}@trail.test";

        var created = await (await admin.PostJsonAsync("/api/v1/platform/organizations", new
        {
            name = "Seaside Group", ownerEmail = email, ownerFirstName = "Sam", ownerLastName = "Sea", timeZone = "Europe/Dublin",
        })).ReadAsync<PlatformOrganizationResponse>();
        created.DefaultTimeZone.ShouldBe("Europe/Dublin");

        var token = _factory.Emails.TokenFromLastEmailTo(email);
        var session = await (await _factory.CreateClient().PostJsonAsync("/api/v1/auth/accept-invitation",
            new { token, firstName = "Sam", lastName = "Sea", password = "seaside-password-1" })).ReadAsync<AuthSessionResponse>();
        session.Organization!.Id.ShouldBe(created.Id);
        session.Organization.Role.ShouldBe("Owner");

        var list = await (await admin.GetAsync("/api/v1/platform/organizations?search=Seaside", Ct))
            .ReadAsync<PagedResult<PlatformOrganizationResponse>>();
        list.Items.ShouldHaveSingleItem().MemberCount.ShouldBe(1);
    }

    [Fact]
    public async Task Deactivating_an_organization_blocks_sign_in_and_refresh()
    {
        var owner = await _factory.CreateOrganizationAsync();
        var anonymous = _factory.CreateClient();
        var login = await (await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { email = owner.Email, password = TrailApiFactory.DefaultPassword })).ReadAsync<AuthSessionResponse>();

        (await PlatformAdmin().PostJsonAsync($"/api/v1/platform/organizations/{owner.OrganizationId}/deactivate"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await anonymous.PostJsonAsync("/api/v1/auth/login", new { email = owner.Email, password = TrailApiFactory.DefaultPassword }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "auth.no_organization");
    }
}
