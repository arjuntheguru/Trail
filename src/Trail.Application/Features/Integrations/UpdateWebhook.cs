using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Integrations;

public sealed record UpdateWebhookCommand(string Url, string? Description, IReadOnlyList<string> EventTypes, bool IsActive);

internal sealed class UpdateWebhookValidator : AbstractValidator<UpdateWebhookCommand>
{
    public UpdateWebhookValidator(IOptions<WebhookOptions> options) =>
        WebhookRules.AddCommonRules(this, x => x.Url, x => x.Description, x => x.EventTypes, options.Value.AllowInsecureTargets);
}

public sealed class UpdateWebhookHandler(IAppDbContext db)
{
    public async Task<Result<WebhookResponse>> Handle(Guid id, UpdateWebhookCommand command, CancellationToken ct)
    {
        var webhook = await db.WebhookSubscriptions.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (webhook is null) return WebhookRules.NotFound;

        webhook.Url = command.Url.Trim();
        webhook.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        webhook.EventTypes = [.. command.EventTypes];

        if (command.IsActive && !webhook.IsActive)
        {
            // Switching back on (by hand or after auto-disable) gives the endpoint a fresh start.
            webhook.IsActive = true;
            webhook.DisabledAt = null;
            webhook.FailureCount = 0;
        }
        else if (!command.IsActive)
        {
            webhook.IsActive = false;
        }

        await db.SaveChangesAsync(ct);
        return webhook.ToResponse();
    }
}
