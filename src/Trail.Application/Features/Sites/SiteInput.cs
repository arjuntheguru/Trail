using FluentValidation;
using Trail.Application.Common;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Sites;

/// <summary>The fields shared by creating and updating a site.</summary>
public interface ISiteInput
{
    string Name { get; }
    string? Code { get; }
    string? TimeZone { get; }
    AddressDto? Address { get; }
    string? Phone { get; }
    string? Email { get; }
    IReadOnlyList<OpeningHoursDto>? OpeningHours { get; }
}

internal abstract class SiteInputValidator<T> : AbstractValidator<T> where T : ISiteInput
{
    protected SiteInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Site.NameMaxLength);
        RuleFor(x => x.Code).MaximumLength(Site.CodeMaxLength);
        RuleFor(x => x.TimeZone!).Must(TimeZones.IsValidIana).When(x => x.TimeZone is not null)
            .WithMessage("Time zone must be a valid IANA id such as Europe/London.");
        RuleFor(x => x.Phone).MaximumLength(Site.PhoneMaxLength);
        RuleFor(x => x.Email).MaximumLength(Site.EmailMaxLength).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1).NotEmpty().MaximumLength(Address.LineMaxLength);
            RuleFor(x => x.Address!.Line2).MaximumLength(Address.LineMaxLength);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(Address.CityMaxLength);
            RuleFor(x => x.Address!.StateOrRegion).MaximumLength(Address.StateMaxLength);
            RuleFor(x => x.Address!.PostalCode).MaximumLength(Address.PostalCodeMaxLength);
            RuleFor(x => x.Address!.CountryCode).NotEmpty().Matches("^[A-Za-z]{2}$")
                .WithMessage("Country code must be a two-letter ISO 3166-1 code.");
        });

        When(x => x.OpeningHours is not null, () =>
        {
            RuleFor(x => x.OpeningHours!)
                .Must(hours => hours.Select(h => h.Day).Distinct().Count() == hours.Count)
                .WithMessage("Opening hours may list each day at most once.");
            RuleForEach(x => x.OpeningHours!).Must(h => Enum.IsDefined(h.Day))
                .WithMessage("Day must be a valid day of the week.");
        });
    }
}
