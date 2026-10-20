using FluentValidation;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;

namespace Trail.Application.Features.Integrations;

internal static class WebhookRules
{
    public const int MaxEventTypes = 20;

    public static Error NotFound { get; } = Error.NotFound("webhook.not_found", "Webhook not found.");

    public static void AddCommonRules<T>(
        AbstractValidator<T> validator,
        Func<T, string?> url, Func<T, string?> description, Func<T, IReadOnlyList<string>?> eventTypes,
        bool allowInsecureTargets)
    {
        validator.RuleFor(x => url(x))
            .NotEmpty().MaximumLength(WebhookSubscription.UrlMaxLength)
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Length > WebhookSubscription.UrlMaxLength
                           || WebhookTargetGuard.Validate(value, allowInsecureTargets) is null)
            .WithMessage((_, value) => WebhookTargetGuard.Validate(value, allowInsecureTargets))
            .OverridePropertyName("url");
        validator.RuleFor(x => description(x)).MaximumLength(WebhookSubscription.DescriptionMaxLength).OverridePropertyName("description");
        validator.RuleFor(x => eventTypes(x))
            .NotNull()
            .Must(t => t is { Count: > 0 }).WithMessage("Choose at least one event type.")
            .Must(t => t is null || t.Count <= MaxEventTypes).WithMessage($"Choose at most {MaxEventTypes} event types.")
            .Must(t => t is null || t.All(WebhookEventTypes.IsKnown))
            .WithMessage($"Event types must be among: {string.Join(", ", WebhookEventTypes.All)}.")
            .OverridePropertyName("eventTypes");
    }
}
