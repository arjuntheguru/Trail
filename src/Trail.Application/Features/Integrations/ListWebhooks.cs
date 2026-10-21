using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

public sealed class ListWebhooksHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<IReadOnlyList<WebhookResponse>>> Handle(CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        return await db.WebhookSubscriptions.AsNoTracking()
            .OrderBy(w => w.CreatedAt).ThenBy(w => w.Id)
            .Select(IntegrationMapping.WebhookProjection)
            .ToListAsync(ct);
    }
}

public sealed class GetWebhookHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<WebhookResponse>> Handle(Guid id, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        var webhook = await db.WebhookSubscriptions.AsNoTracking()
            .Where(w => w.Id == id)
            .Select(IntegrationMapping.WebhookProjection)
            .FirstOrDefaultAsync(ct);
        return webhook is null ? WebhookRules.NotFound : webhook;
    }
}

public sealed class ListWebhookEventTypesHandler
{
    public IReadOnlyList<string> Handle() => WebhookEventTypes.All;
}
