using Trail.Domain.Identity;

namespace Trail.Application.Common.Abstractions;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

/// <summary>Who an access token is for. Permissions are those of the membership's role at issue time.</summary>
public sealed record AccessTokenSubject(
    Guid UserId,
    string Email,
    string Name,
    bool IsPlatformAdmin,
    Guid? OrganizationId,
    Guid? MembershipId,
    IReadOnlyCollection<string> Permissions);

public interface IAccessTokenIssuer
{
    TimeSpan RefreshTokenLifetime { get; }

    AccessToken Issue(AccessTokenSubject subject);
}

/// <summary>Details of the HTTP request a use case runs in, recorded on sessions for the user's review.</summary>
public interface IRequestContext
{
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IPasswordHasher
{
    string Hash(User user, string password);

    PasswordCheck Verify(User user, string password);
}

public enum PasswordCheck
{
    Failed,
    Success,
    SuccessRehashNeeded,
}

public sealed record EmailMessage(string To, string Subject, string TextBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Builds links into the frontend app for emails (password reset, invitations, confirmation).</summary>
public interface IAppLinks
{
    string ResetPassword(string token);
    string ConfirmEmail(string token);
    string AcceptInvitation(string token);
}
