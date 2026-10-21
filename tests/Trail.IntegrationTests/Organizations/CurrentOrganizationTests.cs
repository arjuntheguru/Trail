using System.Net;
using Trail.Application.Common.Security;
using Trail.Application.Features.Organizations;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Organizations;

public sealed class CurrentOrganizationTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync("Dailo Foods");
        _client = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_current_organization_is_returned_to_any_member()
    {
        var member = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId));

        var response = await member.GetAsync("/api/v1/organizations/current", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var organization = await response.ReadAsync<OrganizationResponse>();
        organization.ShouldBe(new OrganizationResponse(_owner.OrganizationId, "Dailo Foods", "dailo-foods", "Europe/London"));
    }

    [Fact]
    public async Task The_owner_can_rename_and_change_the_default_time_zone()
    {
        var response = await _client.PutJsonAsync("/api/v1/organizations/current",
            new UpdateCurrentOrganizationCommand("  Dailo Group ", "America/Chicago"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsync<OrganizationResponse>();
        (updated.Name, updated.DefaultTimeZone, updated.Slug).ShouldBe(("Dailo Group", "America/Chicago", "dailo-foods"));
        (await (await _client.GetAsync("/api/v1/organizations/current", Ct)).ReadAsync<OrganizationResponse>()).ShouldBe(updated);
    }

    [Theory]
    [InlineData("", "Europe/London")]
    [InlineData("Name", "")]
    [InlineData("Name", "Mars/Olympus")]
    [InlineData("Name", "Pacific Standard Time")]
    public async Task Invalid_updates_are_rejected(string name, string timeZone)
    {
        var response = await _client.PutJsonAsync("/api/v1/organizations/current", new UpdateCurrentOrganizationCommand(name, timeZone));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_name_over_the_limit_is_rejected()
    {
        (await _client.PutJsonAsync("/api/v1/organizations/current",
                new UpdateCurrentOrganizationCommand(new string('x', 201), "UTC")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Only_members_with_the_organization_permission_can_update()
    {
        var admin = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.Admin));

        (await admin.PutJsonAsync("/api/v1/organizations/current", new UpdateCurrentOrganizationCommand("X", "UTC")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/v1/organizations/current", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Each_organization_only_ever_sees_and_changes_its_own()
    {
        var other = await _factory.CreateOrganizationAsync("Beta Bakes");
        var otherClient = _factory.CreateClient(other);

        (await otherClient.PutJsonAsync("/api/v1/organizations/current", new UpdateCurrentOrganizationCommand("Beta Bread", "UTC")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await (await otherClient.GetAsync("/api/v1/organizations/current", Ct)).ReadAsync<OrganizationResponse>()).Name.ShouldBe("Beta Bread");
        (await (await _client.GetAsync("/api/v1/organizations/current", Ct)).ReadAsync<OrganizationResponse>()).Name.ShouldBe("Dailo Foods");
    }
}
