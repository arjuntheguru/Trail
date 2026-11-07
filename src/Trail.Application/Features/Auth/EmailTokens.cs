using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;
using Trail.Application.Common.Validation;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Auth;

/// <summary>Creates single-use emailed tokens and the emails that carry them.</summary>
public sealed class EmailConfirmationSender(IAppDbContext db, IEmailSender email, IAppLinks links, TimeProvider clock)
{
    /// <summary>Adds a confirmation token (caller saves) and returns the raw token for <see cref="SendAsync"/>.</summary>
    public string Create(User user)
    {
        var (token, hash) = SecretToken.Create();
        var now = clock.GetUtcNow().UtcDateTime;
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.EmailConfirmation,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now + UserToken.EmailConfirmationLifetime,
        });
        return token;
    }

    public Task SendAsync(User user, string token, CancellationToken ct) =>
        email.SendAsync(new EmailMessage(user.Email, "Confirm your email for Trail",
            $"Hi {user.FirstName},\n\nConfirm your email address by opening this link:\n{links.ConfirmEmail(token)}\n\n" +
            "The link expires in 3 days. If you did not create a Trail account, ignore this email."), ct);
}

/// <summary>Finds a usable single-use token by its raw value and marks it used. The caller saves.</summary>
internal static class UserTokens
{
    public static async Task<UserToken?> RedeemAsync(IAppDbContext db, string rawToken, UserTokenPurpose purpose, DateTime now, CancellationToken ct)
    {
        var hash = SecretToken.Hash(rawToken);
        var token = await db.UserTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.Purpose == purpose, ct);
        if (token is null || !token.IsUsable(now)) return null;
        token.UsedAt = now;
        return token;
    }

    public static Task InvalidateAsync(IAppDbContext db, Guid userId, UserTokenPurpose purpose, DateTime now, CancellationToken ct) =>
        db.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), ct);

    public static readonly Error InvalidToken =
        Error.Validation("auth.invalid_token", "This link is invalid or has expired. Request a new one.");
}

public sealed record ConfirmEmailCommand(string Token);

internal sealed class ConfirmEmailValidator : AbstractValidator<ConfirmEmailCommand>
{
    public ConfirmEmailValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
}

public sealed class ConfirmEmailHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<Result> Handle(ConfirmEmailCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = await UserTokens.RedeemAsync(db, command.Token, UserTokenPurpose.EmailConfirmation, now, ct);
        if (token is null) return UserTokens.InvalidToken;

        var user = await db.Users.SingleAsync(u => u.Id == token.UserId, ct);
        user.EmailConfirmedAt ??= now;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed record ForgotPasswordCommand(string Email);

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(x => x.Email).Email();
}

/// <summary>Emails a reset link if the account exists. Responds the same either way to avoid account discovery.</summary>
public sealed class ForgotPasswordHandler(IAppDbContext db, IEmailSender email, IAppLinks links, TimeProvider clock)
{
    public async Task<Result> Handle(ForgotPasswordCommand command, CancellationToken ct)
    {
        var normalized = User.Normalize(command.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized && u.IsActive, ct);
        if (user is null) return Result.Success();

        var now = clock.GetUtcNow().UtcDateTime;
        await UserTokens.InvalidateAsync(db, user.Id, UserTokenPurpose.PasswordReset, now, ct);

        var (token, hash) = SecretToken.Create();
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.PasswordReset,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now + UserToken.PasswordResetLifetime,
        });
        await db.SaveChangesAsync(ct);

        await email.SendAsync(new EmailMessage(user.Email, "Reset your Trail password",
            $"Hi {user.FirstName},\n\nReset your password by opening this link:\n{links.ResetPassword(token)}\n\n" +
            "The link expires in 1 hour. If you did not ask for this, ignore this email; your password is unchanged."), ct);
        return Result.Success();
    }
}

public sealed record ResetPasswordCommand(string Token, string NewPassword);

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).Password();
    }
}

/// <summary>Sets a new password from a reset link and signs the user out everywhere.</summary>
public sealed class ResetPasswordHandler(IAppDbContext db, IPasswordHasher hasher, SessionIssuer sessions, TimeProvider clock)
{
    public async Task<Result> Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var token = await UserTokens.RedeemAsync(db, command.Token, UserTokenPurpose.PasswordReset, now, ct);
        if (token is null) return UserTokens.InvalidToken;

        var user = await db.Users.SingleAsync(u => u.Id == token.UserId, ct);
        if (!user.IsActive) return UserTokens.InvalidToken;

        user.SetPasswordHash(hasher.Hash(user, command.NewPassword));
        user.EmailConfirmedAt ??= now; // they proved they control the inbox
        await sessions.RevokeAllAsync(user.Id, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
