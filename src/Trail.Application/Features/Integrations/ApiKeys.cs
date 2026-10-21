using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

/// <param name="Scopes">What the key may do; defaults to every scope there is.</param>
/// <param name="SiteIds">Restricts the key to these sites; omit for every site the caller can reach.</param>
public sealed record CreateApiKeyCommand(
    string Name, IReadOnlyList<string>? Scopes = null, IReadOnlyList<Guid>? SiteIds = null, DateTime? ExpiresAt = null);

internal sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(ApiKey.NameMaxLength);
        RuleFor(x => x.Scopes)
            .Must(s => s is null || (s.Count > 0 && s.All(ApiKeyScopes.IsKnown)))
            .WithMessage($"Scopes must be among: {string.Join(", ", ApiKeyScopes.All)}.");
        RuleFor(x => x.SiteIds)
            .Must(s => s is null || s.Count <= ApiKey.MaxSites).WithMessage($"Restrict a key to at most {ApiKey.MaxSites} sites.")
            .Must(s => s is null || s.All(id => id != Guid.Empty)).WithMessage("Site ids must be valid.");
    }
}

public sealed class CreateApiKeyHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<ApiKeyCreatedResponse>> Handle(CreateApiKeyCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (command.ExpiresAt is { } expires && expires.ToUniversalTime() <= now)
            return Error.Validation("api_key.invalid_expiry", "The expiry must be in the future.",
                new Dictionary<string, string[]> { ["expiresAt"] = ["Choose a time in the future."] });

        // A key must not reach further than the person creating it: without a site list it covers every site.
        var scope = await access.GetScopeAsync(ct);
        var siteIds = command.SiteIds?.Distinct().ToList() ?? [];
        if (siteIds.Count == 0 && !scope.AllSites)
            return Error.Forbidden("api_key.sites_required", "Restrict the key to specific sites; you cannot grant access to all sites.");
        if (siteIds.Count > 0)
        {
            var existing = await db.Sites.Where(s => siteIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
            if (existing.Count != siteIds.Count || siteIds.Any(id => !scope.Includes(id)))
                return Error.NotFound("api_key.unknown_sites", "Some sites do not exist.");
        }

        if (await db.ApiKeys.CountAsync(k => k.RevokedAt == null, ct) >= ApiKey.MaxPerOrganization)
            return Error.Conflict("api_key.limit_reached", $"An organization can have at most {ApiKey.MaxPerOrganization} active API keys.");

        var (key, prefix, hash) = ApiKeyFormat.Generate();
        var apiKey = new ApiKey
        {
            Name = command.Name.Trim(),
            Prefix = prefix,
            KeyHash = hash,
            Scopes = [.. command.Scopes ?? ApiKeyScopes.All],
            SiteIds = siteIds.Count == 0 ? null : siteIds,
            ExpiresAt = command.ExpiresAt?.ToUniversalTime(),
        };
        db.ApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);
        return new ApiKeyCreatedResponse(apiKey.ToResponse(), key);
    }
}

public sealed class ListApiKeysHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<IReadOnlyList<ApiKeyResponse>>> Handle(CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        return await db.ApiKeys.AsNoTracking()
            .OrderByDescending(k => k.CreatedAt).ThenBy(k => k.Id)
            .Select(IntegrationMapping.ApiKeyProjection)
            .ToListAsync(ct);
    }
}

public sealed class RevokeApiKeyHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (key is null) return Error.NotFound("api_key.not_found", "API key not found.");

        if (key.RevokedAt is null)
        {
            key.RevokedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }
}
