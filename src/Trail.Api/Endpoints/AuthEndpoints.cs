using Trail.Api.Infrastructure;
using Trail.Application.Common.Results;
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
            .Validate<RegisterOrganizationCommand>()
            .WithRefreshCookie()
            .Creates<AuthSessionResponse>()
            .ReturnsConflict();

        auth.MapPost("login", async (LoginCommand command, LoginHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<LoginCommand>()
            .WithRefreshCookie()
            .Returns<AuthSessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // Cookie-transport clients may send an empty body; the token then comes from the httpOnly cookie.
        auth.MapPost("refresh", async (HttpRequest request, RefreshTokenRequest? body, RefreshSessionHandler handler, CancellationToken ct) =>
            {
                var token = RefreshCookie.Read(request, body?.RefreshToken);
                if (token is null || token.Length > 128)
                    return Error.Unauthorized("auth.invalid_refresh_token", "The session has expired. Sign in again.").ToProblem();
                return (await handler.Handle(new RefreshSessionCommand(token), ct)).ToHttp();
            })
            .WithRefreshCookie()
            .Returns<AuthSessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        auth.MapPost("logout", async (HttpRequest request, HttpResponse response, RefreshTokenRequest? body, LogoutHandler handler, CancellationToken ct) =>
            {
                var token = RefreshCookie.Read(request, body?.RefreshToken);
                if (RefreshCookie.Requested(request)) RefreshCookie.Clear(response);
                if (token is null || token.Length > 128) return TypedResults.NoContent();
                return (await handler.Handle(new LogoutCommand(token), ct)).ToHttp();
            })
            .ReturnsNoContent();

        auth.MapPost("confirm-email", async (ConfirmEmailCommand command, ConfirmEmailHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<ConfirmEmailCommand>()
            .ReturnsNoContent();

        auth.MapPost("forgot-password", async (ForgotPasswordCommand command, ForgotPasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(() => TypedResults.Accepted((string?)null)))
            .Validate<ForgotPasswordCommand>()
            .Produces(StatusCodes.Status202Accepted);

        auth.MapPost("reset-password", async (ResetPasswordCommand command, ResetPasswordHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<ResetPasswordCommand>()
            .ReturnsNoContent();

        auth.MapPost("accept-invitation", async (AcceptInvitationCommand command, AcceptInvitationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .Validate<AcceptInvitationCommand>()
            .WithRefreshCookie()
            .Returns<AuthSessionResponse>();

        // Separate group: AllowAnonymous on the group above would override RequireAuthorization here.
        app.MapGroup("auth").WithTags("Auth").RequireAuthorization()
            .MapPost("switch-organization", async (SwitchOrganizationCommand command, SwitchOrganizationHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp())
            .WithRefreshCookie()
            .Returns<AuthSessionResponse>();

        var me = app.MapGroup("me").WithTags("Me").RequireAuthorization();

        me.MapGet("", async (MeHandler handler, CancellationToken ct) => (await handler.Get(ct)).ToHttp())
            .Returns<MeResponse>();

        me.MapPut("", async (UpdateProfileCommand command, MeHandler handler, CancellationToken ct) =>
                (await handler.UpdateProfile(command, ct)).ToHttp())
            .Validate<UpdateProfileCommand>()
            .Returns<UserSummary>();

        me.MapPost("change-password", async (ChangePasswordCommand command, MeHandler handler, CancellationToken ct) =>
                (await handler.ChangePassword(command, ct)).ToHttp())
            .Validate<ChangePasswordCommand>()
            .WithRefreshCookie()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .Returns<AuthSessionResponse>();

        me.MapPost("sessions/revoke-all", async (MeHandler handler, CancellationToken ct) =>
            (await handler.RevokeAllSessions(ct)).ToHttp())
            .ReturnsNoContent();

        me.MapPost("resend-email-confirmation", async (MeHandler handler, CancellationToken ct) =>
                (await handler.ResendEmailConfirmation(ct)).ToHttp())
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ReturnsNoContent()
            .ReturnsConflict();
    }
}