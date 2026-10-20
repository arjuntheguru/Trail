using System.Security.Claims;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using MimeKit;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Domain.Identity;

namespace Trail.Infrastructure.Security;

internal sealed class AccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken Issue(AccessTokenSubject subject)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(TrailClaimTypes.UserId, subject.UserId.ToString()),
            new(TrailClaimTypes.Email, subject.Email),
            new(TrailClaimTypes.Name, subject.Name),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (subject.OrganizationId is { } org) claims.Add(new(TrailClaimTypes.OrganizationId, org.ToString()));
        if (subject.MembershipId is { } mid) claims.Add(new(TrailClaimTypes.MembershipId, mid.ToString()));
        if (subject.IsPlatformAdmin) claims.Add(new(TrailClaimTypes.PlatformAdmin, "true"));
        claims.AddRange(subject.Permissions.Select(p => new Claim(TrailClaimTypes.Permission, p)));

        var token = _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });

        return new AccessToken(token, expires);
    }
}

internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(User user, string password) => _inner.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash)) return PasswordCheck.Failed;

        return _inner.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed,
        };
    }
}

internal sealed class AppLinks(IOptions<AppOptions> options) : IAppLinks
{
    private string Base => options.Value.FrontendBaseUrl.TrimEnd('/');

    public string ResetPassword(string token) => $"{Base}/reset-password?token={Uri.EscapeDataString(token)}";
    public string ConfirmEmail(string token) => $"{Base}/confirm-email?token={Uri.EscapeDataString(token)}";
    public string AcceptInvitation(string token) => $"{Base}/accept-invitation?token={Uri.EscapeDataString(token)}";
}

/// <summary>Development sender: writes the email (including its links) to the log instead of sending it.</summary>
internal sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("Email to {To}: {Subject}\n{Body}", message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}

internal sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var settings = options.Value;
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.TextBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost!, settings.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable, ct);
        if (!string.IsNullOrEmpty(settings.SmtpUser))
            await client.AuthenticateAsync(settings.SmtpUser, settings.SmtpPassword ?? "", ct);
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("Sent email {Subject} to {To}", message.Subject, message.To);
    }
}
