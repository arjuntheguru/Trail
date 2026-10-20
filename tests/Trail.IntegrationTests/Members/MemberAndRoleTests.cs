using System.Net;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Security;
using Trail.Application.Features.Auth;
using Trail.Application.Features.Members;
using Trail.Application.Features.Roles;
using Trail.Domain.Identity;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Members;

public sealed class MemberAndRoleTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _ownerClient = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync("Harbour Hotel");
        _ownerClient = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<Guid> RoleIdAsync(string name, Guid? organizationId = null) =>
        _factory.WithDbAsync(organizationId ?? _owner.OrganizationId,
            db => db.Roles.Where(r => r.Name == name).Select(r => r.Id).SingleAsync());

    private async Task<MemberResponse> InviteAsync(string email, string role = SystemRoles.TeamMember) =>
        await (await _ownerClient.PostJsonAsync("/api/v1/members/invitations", new { email, roleId = await RoleIdAsync(role) }))
            .ReadAsync<MemberResponse>();

    [Fact]
    public async Task Invited_person_accepts_by_choosing_a_password_and_becomes_active()
    {
        var email = $"chef-{Guid.NewGuid():N}@trail.test";
        var invited = await InviteAsync(email);
        invited.Status.ShouldBe(MembershipStatus.Invited);
        _factory.Emails.LastTo(email).Subject.ShouldContain("Harbour Hotel");

        var token = _factory.Emails.TokenFromLastEmailTo(email);
        var anonymous = _factory.CreateClient();
        await (await anonymous.PostJsonAsync("/api/v1/auth/accept-invitation", new { token }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "auth.profile_required");

        var session = await (await anonymous.PostJsonAsync("/api/v1/auth/accept-invitation",
                new { token, firstName = "Gordon", lastName = "Ramsay", password = "kitchen-nightmares-1" }))
            .ReadAsync<AuthSessionResponse>();
        session.Organization!.Id.ShouldBe(_owner.OrganizationId);
        session.User.EmailConfirmed.ShouldBeTrue();

        var member = await (await _ownerClient.GetAsync($"/api/v1/members/{invited.Id}", Ct)).ReadAsync<MemberResponse>();
        member.Status.ShouldBe(MembershipStatus.Active);
        member.FirstName.ShouldBe("Gordon");

        // The link is single use.
        await (await anonymous.PostJsonAsync("/api/v1/auth/accept-invitation", new { token }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "auth.invalid_token");
    }

    [Fact]
    public async Task Resending_an_invitation_invalidates_the_previous_link()
    {
        var email = $"host-{Guid.NewGuid():N}@trail.test";
        var invited = await InviteAsync(email);
        var oldToken = _factory.Emails.TokenFromLastEmailTo(email);

        (await _ownerClient.PostJsonAsync($"/api/v1/members/{invited.Id}/resend-invitation")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var newToken = _factory.Emails.TokenFromLastEmailTo(email);

        newToken.ShouldNotBe(oldToken);
        await (await _factory.CreateClient().PostJsonAsync("/api/v1/auth/accept-invitation",
                new { token = oldToken, firstName = "A", lastName = "B", password = "long-enough-pass" }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "auth.invalid_token");
    }

    [Fact]
    public async Task Inviting_an_existing_member_conflicts()
    {
        var email = $"dup-{Guid.NewGuid():N}@trail.test";
        await InviteAsync(email);

        var again = await _ownerClient.PostJsonAsync("/api/v1/members/invitations", new { email, roleId = await RoleIdAsync(SystemRoles.TeamMember) });

        await again.ShouldBeAsync(HttpStatusCode.Conflict, "member.already_exists");
    }

    [Fact]
    public async Task Team_members_cannot_manage_members()
    {
        var team = await _factory.AddMemberAsync(_owner.OrganizationId);
        var client = _factory.CreateClient(team);

        (await client.GetAsync("/api/v1/members", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostJsonAsync("/api/v1/members/invitations", new { email = "x@trail.test", roleId = Guid.NewGuid() }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admins_cannot_hand_out_roles_more_powerful_than_their_own()
    {
        var admin = await _factory.AddMemberAsync(_owner.OrganizationId, SystemRoles.Admin);
        var client = _factory.CreateClient(admin);
        var ownerRole = await RoleIdAsync(SystemRoles.Owner);

        await (await client.PostJsonAsync("/api/v1/members/invitations", new { email = $"x-{Guid.NewGuid():N}@trail.test", roleId = ownerRole }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.privilege_escalation");

        var team = await _factory.AddMemberAsync(_owner.OrganizationId);
        await (await client.PutJsonAsync($"/api/v1/members/{team.MembershipId}/role", new { roleId = ownerRole }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.privilege_escalation");

        // ...and cannot touch the Owner at all.
        await (await client.PostJsonAsync($"/api/v1/members/{_owner.MembershipId}/suspend"))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.privilege_escalation");

        await (await client.PostJsonAsync("/api/v1/roles", new { name = "Sneaky", permissions = new[] { Permissions.Organization.Manage } }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.privilege_escalation");
    }

    [Fact]
    public async Task Nobody_can_change_their_own_membership()
    {
        await (await _ownerClient.PutJsonAsync($"/api/v1/members/{_owner.MembershipId}/role", new { roleId = await RoleIdAsync(SystemRoles.TeamMember) }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.cannot_manage_self");
        await (await _ownerClient.DeleteAsync($"/api/v1/members/{_owner.MembershipId}", Ct))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "member.cannot_manage_self");
    }

    [Fact]
    public async Task Changing_a_role_updates_permissions_at_the_next_refresh()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId);
        var login = await (await _factory.CreateClient().PostJsonAsync("/api/v1/auth/login",
            new { email = member.Email, password = TrailApiFactory.DefaultPassword })).ReadAsync<AuthSessionResponse>();
        login.Permissions.ShouldNotContain(Permissions.Tasks.Approve);

        (await _ownerClient.PutJsonAsync($"/api/v1/members/{member.MembershipId}/role", new { roleId = await RoleIdAsync(SystemRoles.SiteManager) }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshed = await (await _factory.CreateClient().PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken }))
            .ReadAsync<AuthSessionResponse>();
        refreshed.Organization!.Role.ShouldBe(SystemRoles.SiteManager);
        refreshed.Permissions.ShouldContain(Permissions.Tasks.Approve);
    }

    [Fact]
    public async Task Suspending_a_member_ends_their_sessions_and_blocks_sign_in_until_reactivated()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId);
        var anonymous = _factory.CreateClient();
        var login = await (await anonymous.PostJsonAsync("/api/v1/auth/login",
            new { email = member.Email, password = TrailApiFactory.DefaultPassword })).ReadAsync<AuthSessionResponse>();

        (await _ownerClient.PostJsonAsync($"/api/v1/members/{member.MembershipId}/suspend")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await anonymous.PostJsonAsync("/api/v1/auth/login", new { email = member.Email, password = TrailApiFactory.DefaultPassword }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "auth.no_organization");

        (await _ownerClient.PostJsonAsync($"/api/v1/members/{member.MembershipId}/reactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await anonymous.PostJsonAsync("/api/v1/auth/login", new { email = member.Email, password = TrailApiFactory.DefaultPassword }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Removing_a_member_deletes_the_membership()
    {
        var member = await _factory.AddMemberAsync(_owner.OrganizationId);

        (await _ownerClient.DeleteAsync($"/api/v1/members/{member.MembershipId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _ownerClient.GetAsync($"/api/v1/members/{member.MembershipId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Member_list_is_paged_searchable_and_never_leaks_password_data()
    {
        var target = await _factory.AddMemberAsync(_owner.OrganizationId);
        await _factory.AddMemberAsync(_owner.OrganizationId);

        var response = await _ownerClient.GetAsync($"/api/v1/members?search={Uri.EscapeDataString(target.Email[..12])}&pageSize=10", Ct);
        var raw = await response.Content.ReadAsStringAsync(Ct);
        var page = System.Text.Json.JsonSerializer.Deserialize<PagedResult<MemberResponse>>(raw, HttpExtensions.Json)!;

        page.Items.ShouldHaveSingleItem().Email.ShouldBe(target.Email);
        raw.ShouldNotContain("passwordHash", Case.Insensitive);
        raw.ShouldNotContain("securityStamp", Case.Insensitive);
    }

    [Fact]
    public async Task Members_and_roles_of_another_organization_are_invisible()
    {
        var other = await _factory.CreateOrganizationAsync("Rival Restaurant");
        var otherClient = _factory.CreateClient(other);
        var ourRole = await RoleIdAsync(SystemRoles.TeamMember);

        (await otherClient.GetAsync($"/api/v1/members/{_owner.MembershipId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherClient.PostJsonAsync($"/api/v1/members/{_owner.MembershipId}/suspend")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherClient.PutJsonAsync($"/api/v1/roles/{ourRole}", new { name = "Team Member", permissions = Array.Empty<string>() }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await (await otherClient.PostJsonAsync("/api/v1/members/invitations", new { email = "a@trail.test", roleId = ourRole }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "member.unknown_role");

        var members = await (await otherClient.GetAsync("/api/v1/members", Ct)).ReadAsync<PagedResult<MemberResponse>>();
        members.Items.ShouldAllBe(m => m.Id != _owner.MembershipId);
    }

    [Fact]
    public async Task System_roles_are_listed_and_protected()
    {
        var roles = await (await _ownerClient.GetAsync("/api/v1/roles", Ct)).ReadAsync<List<RoleResponse>>();
        roles.Select(r => r.Name).ShouldBe(SystemRoles.Definitions.Keys, ignoreOrder: true);
        roles.Single(r => r.Name == SystemRoles.Owner).MemberCount.ShouldBe(1);

        var owner = roles.Single(r => r.Name == SystemRoles.Owner);
        await (await _ownerClient.PutJsonAsync($"/api/v1/roles/{owner.Id}", new { name = owner.Name, permissions = Array.Empty<string>() }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "role.owner_immutable");

        var team = roles.Single(r => r.Name == SystemRoles.TeamMember);
        await (await _ownerClient.PutJsonAsync($"/api/v1/roles/{team.Id}", new { name = "Crew", permissions = team.Permissions }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "role.system_rename");
        await (await _ownerClient.DeleteAsync($"/api/v1/roles/{team.Id}", Ct)).ShouldBeAsync(HttpStatusCode.Forbidden, "role.system_delete");

        var catalog = await (await _ownerClient.GetAsync("/api/v1/roles/permissions", Ct)).ReadAsync<List<PermissionResponse>>();
        catalog.Select(p => p.Key).ShouldBe(Permissions.All, ignoreOrder: true);
    }

    [Fact]
    public async Task Custom_roles_can_be_created_used_and_deleted_once_empty()
    {
        var created = await (await _ownerClient.PostJsonAsync("/api/v1/roles", new
        {
            name = "Auditor",
            description = "Reads reports",
            permissions = new[] { Permissions.Reports.View, Permissions.Audit.View, Permissions.Reports.View },
        })).ReadAsync<RoleResponse>();
        created.Permissions.ShouldBe([Permissions.Audit.View, Permissions.Reports.View]);

        await (await _ownerClient.PostJsonAsync("/api/v1/roles", new { name = "Auditor", permissions = Array.Empty<string>() }))
            .ShouldBeAsync(HttpStatusCode.Conflict, "role.name_taken");
        await (await _ownerClient.PostJsonAsync("/api/v1/roles", new { name = "Bogus", permissions = new[] { "everything.ever" } }))
            .ShouldBeAsync(HttpStatusCode.BadRequest);

        var member = await _factory.AddMemberAsync(_owner.OrganizationId, "Auditor");
        member.Permissions.ShouldContain(Permissions.Audit.View);
        await (await _ownerClient.DeleteAsync($"/api/v1/roles/{created.Id}", Ct)).ShouldBeAsync(HttpStatusCode.Conflict, "role.in_use");

        (await _ownerClient.DeleteAsync($"/api/v1/members/{member.MembershipId}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _ownerClient.DeleteAsync($"/api/v1/roles/{created.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
