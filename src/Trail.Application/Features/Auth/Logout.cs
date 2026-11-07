using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;

namespace Trail.Application.Features.Auth;

public sealed record LogoutCommand(string RefreshToken);

internal sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(128);
}

/// <summary>Ends the session the refresh token belongs to. Always succeeds so it reveals nothing about tokens.</summary>
public sealed class LogoutHandler(IAppDbContext db, SessionIssuer sessions)
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken ct)
    {
        var hash = SecretToken.Hash(command.RefreshToken);
        var familyId = await db.RefreshTokens.Where(t => t.TokenHash == hash).Select(t => (Guid?)t.FamilyId).SingleOrDefaultAsync(ct);
        if (familyId is { } family)
            await sessions.RevokeFamilyAsync(family, ct);
        return Result.Success();
    }
}
