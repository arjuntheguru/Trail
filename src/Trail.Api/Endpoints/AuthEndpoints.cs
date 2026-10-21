using Trail.Api.Infrastructure;
using Trail.Application.Features.Auth;
using Trail.Application.Features.Me;

namespace Trail.Api.Endpoints;

internal sealed class AuthEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        // Sign-in flows are anonymous by nature, so every one of them is rate limited per IP.
        var auth = app.MapGroup("auth").WithTags("Auth").AllowAnonymous().RequireRateLimiting(RateLimitPolicies.Auth);

        auth.MapPost("register", async (RegisterOrganizationCommand command, RegisterOrganizationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(session => TypedResults.Created("/api/v1/me", session)))
            .Validate<RegisterOrganizationCommand>();

        auth.MapPost("login", async (LoginCommand command, LoginHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<LoginCommand>();

        auth.MapPost("refresh", async (RefreshSessionCommand command, RefreshSessionHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<RefreshSessionCommand>();

        auth.MapPost("logout", async (LogoutCommand command, LogoutHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<LogoutCommand>();

        auth.MapPost("confirm-email", async (ConfirmEmailCommand command, ConfirmEmailHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<ConfirmEmailCommand>();

        auth.MapPost("forgot-password", async (ForgotPasswordCommand command, ForgotPasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(() => TypedResults.Accepted((string?)null)))
            .Validate<ForgotPasswordCommand>();

        auth.MapPost("reset-password", async (ResetPasswordCommand command, ResetPasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<ResetPasswordCommand>();

        auth.MapPost("accept-invitation", async (AcceptInvitationCommand command, AcceptInvitationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<AcceptInvitationCommand>();

        // Separate group: AllowAnonymous on the group above would override RequireAuthorization here.
        app.MapGroup("auth").WithTags("Auth").RequireAuthorization()
            .MapPost("switch-organization", async (SwitchOrganizationCommand command, SwitchOrganizationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp());

        var me = app.MapGroup("me").WithTags("Me").RequireAuthorization();

        me.MapGet("", async (MeHandler handler, CancellationToken ct) => (await handler.Get(ct)).ToHttp());

        me.MapPut("", async (UpdateProfileCommand command, MeHandler handler, CancellationToken ct) =>
                (await handler.UpdateProfile(command, ct)).ToHttp())
            .Validate<UpdateProfileCommand>();

        me.MapPost("change-password", async (ChangePasswordCommand command, MeHandler handler, CancellationToken ct) =>
                (await handler.ChangePassword(command, ct)).ToHttp())
            .Validate<ChangePasswordCommand>()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        me.MapPost("sessions/revoke-all", async (MeHandler handler, CancellationToken ct) =>
            (await handler.RevokeAllSessions(ct)).ToHttp());

        me.MapPost("resend-email-confirmation", async (MeHandler handler, CancellationToken ct) =>
                (await handler.ResendEmailConfirmation(ct)).ToHttp())
            .RequireRateLimiting(RateLimitPolicies.Auth);
    }
}
