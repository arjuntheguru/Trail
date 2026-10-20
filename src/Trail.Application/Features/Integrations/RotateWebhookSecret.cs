using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Integrations;

public sealed class RotateWebhookSecretHandler(IAppDbContext db)
{
    public async Task<Result<WebhookWithSecretResponse>> Handle(Guid id, CancellationToken ct)
    {
        var webhook = await db.WebhookSubscriptions.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (webhook is null) return WebhookRules.NotFound;

        var secret = WebhookSigning.NewSecret();
        webhook.Secret = secret;
        await db.SaveChangesAsync(ct);
        return new WebhookWithSecretResponse(webhook.ToResponse(), secret);
    }
}
