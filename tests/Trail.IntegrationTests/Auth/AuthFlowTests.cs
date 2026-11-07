using System.Net;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Security;
using Trail.Application.Features.Auth;
using Trail.Application.Features.Me;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Auth;

public sealed class AuthFlowTests(MySqlFixture mysql) : IAsyncLifetime
{
    private const string Password = "a-long-enough-password";
    private TrailApiFactory _factory = null!;

    public async ValueTask InitializeAsync() => _factory = await TrailApiFactory.CreateAsync(mysql);

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient Anonymous => _factory.CreateClient();

    private async Task<(AuthSessionResponse Session, string Email)> RegisterAsync(string organization = "Riverside Cafe")
    {
        var email = $"owner-{Guid.NewGuid():N}@trail.test";
        var response = await Anonymous.PostJsonAsync("/api/v1/auth/register", new
        {
            organizationName = organization,
            firstName = "Ada",
            lastName = "Lovelace",
            email,
            password = Password,
            timeZone = "Europe/London",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsync<AuthSessionResponse>(), email);
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password, Guid? organizationId = null) =>
        Anonymous.PostJsonAsync("/api/v1/auth/login", new { email, password, organizationId });

    [Fact]
    public async Task Register_creates_the_organization_signs_the_owner_in_and_sends_a_confirmation_email()
    {
        var (session, email) = await RegisterAsync("Riverside Cafe & Bar");

        session.Organization.ShouldNotBeNull();
        session.Organization.Role.ShouldBe(SystemRoles.Owner);
        session.Organization.Slug.ShouldBe("riverside-cafe-bar");
        session.Permissions.ShouldBe(Permissions.All, ignoreOrder: true);
        session.User.EmailConfirmed.ShouldBeFalse();
        _factory.Emails.LastTo(email).TextBody.ShouldContain("https://app.trail.test/confirm-email?token=");

        var me = await Anonymous.WithBearer(session.AccessToken).GetAsync("/api/v1/me", TestContext.Current.CancellationToken);
        (await me.ReadAsync<MeResponse>()).Organization!.Id.ShouldBe(session.Organization.Id);
    }

    [Fact]
    public async Task Register_gives_each_organization_a_unique_slug()
    {
        var (first, _) = await RegisterAsync("Duplicate Diner");
        var (second, _) = await RegisterAsync("Duplicate Diner");

        second.Organization!.Slug.ShouldNotBe(first.Organization!.Slug);
        second.Organization.Slug.ShouldStartWith("duplicate-diner");
    }

    [Fact]
    public async Task Register_rejects_an_existing_email_and_weak_input()
    {
        var (_, email) = await RegisterAsync();

        var duplicate = await Anonymous.PostJsonAsync("/api/v1/auth/register", new
        {
            organizationName = "Other", firstName = "A", lastName = "B", email, password = Password,
        });
        await duplicate.ShouldBeAsync(HttpStatusCode.Conflict, "auth.email_taken");

        var weak = await Anonymous.PostJsonAsync("/api/v1/auth/register", new
        {
            organizationName = "Other", firstName = "A", lastName = "B", email = "new@trail.test", password = "short", timeZone = "Mars/Base",
        });
        await weak.ShouldBeAsync(HttpStatusCode.BadRequest);
        var body = await weak.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Password");
        body.ShouldContain("TimeZone");
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_gives_the_same_error_for_wrong_password_and_unknown_email()
    {
        var (_, email) = await RegisterAsync();

        (await LoginAsync(email.ToUpperInvariant(), Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await LoginAsync(email, "wrong-password-123")).ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        await (await LoginAsync("nobody@trail.test", Password)).ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
    }

    [Fact]
    public async Task Five_failed_logins_lock_the_account_even_for_the_right_password()
    {
        var (_, email) = await RegisterAsync();

        for (var i = 0; i < 5; i++)
            await LoginAsync(email, "wrong-password-123");

        await (await LoginAsync(email, Password)).ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.locked_out");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reusing_an_old_one_revokes_the_whole_session()
    {
        var (session, _) = await RegisterAsync();

        var first = await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .ReadAsync<AuthSessionResponse>();
        first.RefreshToken.ShouldNotBe(session.RefreshToken);
        first.Organization!.Id.ShouldBe(session.Organization!.Id);

        // Replaying the original token looks like theft: it fails and kills the chain.
        await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
        await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken }))
            .ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Concurrent_refreshes_with_one_token_cannot_both_succeed()
    {
        var (session, _) = await RegisterAsync();

        var attempts = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken })));

        attempts.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (session, _) = await RegisterAsync();

        (await Anonymous.PostJsonAsync("/api/v1/auth/logout", new { refreshToken = session.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Password_reset_replaces_the_password_signs_out_everywhere_and_works_once()
    {
        var (session, email) = await RegisterAsync();

        (await Anonymous.PostJsonAsync("/api/v1/auth/forgot-password", new { email })).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var token = _factory.Emails.TokenFromLastEmailTo(email);
        _factory.Emails.LastTo(email).TextBody.ShouldContain("https://app.trail.test/reset-password?token=");

        const string newPassword = "an-even-better-password";
        (await Anonymous.PostJsonAsync("/api/v1/auth/reset-password", new { token, newPassword })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await (await LoginAsync(email, Password)).ShouldBeAsync(HttpStatusCode.Unauthorized, "auth.invalid_credentials");
        (await LoginAsync(email, newPassword)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken }))
            .ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await Anonymous.PostJsonAsync("/api/v1/auth/reset-password", new { token, newPassword = "yet-another-password" }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "auth.invalid_token");
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_email_looks_the_same_and_sends_nothing()
    {
        var before = _factory.Emails.Sent.Count;

        (await Anonymous.PostJsonAsync("/api/v1/auth/forgot-password", new { email = "ghost@trail.test" }))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        _factory.Emails.Sent.Count.ShouldBe(before);
    }

    [Fact]
    public async Task Confirming_the_email_marks_the_user_confirmed()
    {
        var (session, email) = await RegisterAsync();
        var token = _factory.Emails.TokenFromLastEmailTo(email);

        (await Anonymous.PostJsonAsync("/api/v1/auth/confirm-email", new { token })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var me = await (await Anonymous.WithBearer(session.AccessToken).GetAsync("/api/v1/me", TestContext.Current.CancellationToken))
            .ReadAsync<MeResponse>();
        me.User.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task Changing_password_requires_the_current_one_and_ends_other_sessions()
    {
        var (session, email) = await RegisterAsync();
        var client = Anonymous.WithBearer(session.AccessToken);

        await (await client.PostJsonAsync("/api/v1/me/change-password", new { currentPassword = "not-it-at-all", newPassword = "brand-new-password" }))
            .ShouldBeAsync(HttpStatusCode.BadRequest, "auth.wrong_password");

        var fresh = await (await client.PostJsonAsync("/api/v1/me/change-password", new { currentPassword = Password, newPassword = "brand-new-password" }))
            .ReadAsync<AuthSessionResponse>();

        await (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = session.RefreshToken })).ShouldBeAsync(HttpStatusCode.Unauthorized);
        (await Anonymous.PostJsonAsync("/api/v1/auth/refresh", new { refreshToken = fresh.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoginAsync(email, "brand-new-password")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl")]
    public async Task Invalid_access_tokens_are_rejected(string token)
    {
        var response = await Anonymous.WithBearer(token).GetAsync("/api/v1/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_routes_require_a_token()
    {
        (await Anonymous.GetAsync("/api/v1/me", TestContext.Current.CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Anonymous.PostJsonAsync("/api/v1/auth/switch-organization", new { organizationId = Guid.NewGuid() }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_member_of_two_organizations_can_switch_between_them_but_not_into_others()
    {
        var (home, email) = await RegisterAsync("Home Kitchen");
        var (other, _) = await RegisterAsync("Second Site Co");
        var stranger = await _factory.CreateOrganizationAsync("Not Yours Ltd");

        // The second organization's owner invites the first owner, who accepts with their existing account.
        var inviter = Anonymous.WithBearer(other.AccessToken);
        var teamRole = await _factory.WithDbAsync(other.Organization!.Id,
            db => db.Roles.Where(r => r.Name == SystemRoles.TeamMember).Select(r => r.Id).SingleAsync());
        (await inviter.PostJsonAsync("/api/v1/members/invitations", new { email, roleId = teamRole })).StatusCode.ShouldBe(HttpStatusCode.Created);
        var token = _factory.Emails.TokenFromLastEmailTo(email);
        (await Anonymous.PostJsonAsync("/api/v1/auth/accept-invitation", new { token })).StatusCode.ShouldBe(HttpStatusCode.OK);

        var client = Anonymous.WithBearer(home.AccessToken);
        var switched = await (await client.PostJsonAsync("/api/v1/auth/switch-organization", new { organizationId = other.Organization.Id }))
            .ReadAsync<AuthSessionResponse>();
        switched.Organization!.Id.ShouldBe(other.Organization.Id);
        switched.Organization.Role.ShouldBe(SystemRoles.TeamMember);
        switched.Organizations.Count.ShouldBe(2);
        switched.Permissions.ShouldNotContain(Permissions.Members.Manage);

        await (await client.PostJsonAsync("/api/v1/auth/switch-organization", new { organizationId = stranger.OrganizationId }))
            .ShouldBeAsync(HttpStatusCode.Forbidden, "auth.not_a_member");

        // Next login defaults to the organization used last.
        var login = await (await LoginAsync(email, Password)).ReadAsync<AuthSessionResponse>();
        login.Organization!.Id.ShouldBe(other.Organization.Id);
    }
}
