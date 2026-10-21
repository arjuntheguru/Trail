using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

public sealed record CreateWebhookCommand(string Url, string? Description, IReadOnlyList<string> EventTypes);

internal sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookValidator(IOptions<WebhookOptions> options) =>
        WebhookRules.AddCommonRules(this, x => x.Url, x => x.Description, x => x.EventTypes, options.Value.AllowInsecureTargets);
}

public sealed class CreateWebhookHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<WebhookWithSecretResponse>> Handle(CreateWebhookCommand command, CancellationToken ct)
    {
        if (await IntegrationAccess.RequireAllSitesAsync(access, ct) is { } denied) return denied;
        if (await db.WebhookSubscriptions.CountAsync(ct) >= WebhookSubscription.MaxPerOrganization)
            return Error.Conflict("webhook.limit_reached", $"An organization can have at most {WebhookSubscription.MaxPerOrganization} webhooks.");

        var secret = WebhookSigning.NewSecret();
        var webhook = new WebhookSubscription
        {
            Url = command.Url.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            EventTypes = [.. command.EventTypes],
            Secret = secret,
        };
        db.WebhookSubscriptions.Add(webhook);
        await db.SaveChangesAsync(ct);
        return new WebhookWithSecretResponse(webhook.ToResponse(), secret);
    }
}
