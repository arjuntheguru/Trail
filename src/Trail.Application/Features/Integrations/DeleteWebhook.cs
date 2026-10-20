using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Integrations;

public sealed class DeleteWebhookHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var webhook = await db.WebhookSubscriptions.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (webhook is null) return WebhookRules.NotFound;

        // Soft delete: queued deliveries are failed by the job, and sent ones keep their history.
        db.WebhookSubscriptions.Remove(webhook);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
