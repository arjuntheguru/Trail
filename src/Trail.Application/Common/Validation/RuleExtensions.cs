using FluentValidation;
using Trail.Domain.Identity;

namespace Trail.Application.Common.Validation;

public static class RuleExtensions
{
    public const int PasswordMinLength = 10;
    public const int PasswordMaxLength = 128;

    /// <summary>Length-based policy (NIST 800-63B): no composition rules, long passphrases welcome.</summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MinimumLength(PasswordMinLength)
            .MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(User.EmailMaxLength)
            .EmailAddress();

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(User.NameMaxLength);

    public static IRuleBuilderOptions<T, string?> TimeZone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(tz => tz is null || TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
            .WithMessage("'{PropertyName}' must be an IANA time zone such as 'Europe/London'.");
}
