using Trail.Application.Common;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Sites;

public sealed record AddressDto(
    string Line1,
    string? Line2,
    string City,
    string? StateOrRegion,
    string? PostalCode,
    string CountryCode);

/// <summary>One opening window in site-local time. A <see cref="Closes"/> at or before <see cref="Opens"/> means after midnight.</summary>
public sealed record OpeningHoursDto(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

public sealed record SiteResponse(
    Guid Id,
    string Name,
    string? Code,
    Guid? RegionId,
    string? RegionName,
    string TimeZone,
    AddressDto? Address,
    string? Phone,
    string? Email,
    bool IsActive,
    IReadOnlyList<OpeningHoursDto> OpeningHours);

internal static class SiteMapping
{
    public static AddressDto? ToDto(Address? address) =>
        address is null
            ? null
            : new AddressDto(address.Line1, address.Line2, address.City, address.StateOrRegion, address.PostalCode, address.CountryCode);

    public static Address? ToDomain(AddressDto? dto) =>
        dto is null
            ? null
            : new Address(
                dto.Line1.Trim(),
                dto.Line2?.Trim().NullIfEmpty(),
                dto.City.Trim(),
                dto.StateOrRegion?.Trim().NullIfEmpty(),
                dto.PostalCode?.Trim().NullIfEmpty(),
                dto.CountryCode.Trim().ToUpperInvariant());

    public static IReadOnlyList<OpeningHoursDto> ToDto(IEnumerable<OpeningHours> hours) =>
        hours.Select(h => new OpeningHoursDto(h.Day, h.Opens, h.Closes)).ToList();

    /// <summary>Monday-first, one entry per day, so stored and returned hours have a stable order.</summary>
    public static List<OpeningHours> ToDomain(IEnumerable<OpeningHoursDto>? hours) =>
        (hours ?? [])
            .OrderBy(h => ((int)h.Day + 6) % 7)
            .Select(h => new OpeningHours(h.Day, h.Opens, h.Closes))
            .ToList();

    public static SiteResponse ToResponse(Site site, string? regionName) => new(
        site.Id, site.Name, site.Code, site.RegionId, regionName, site.TimeZone, ToDto(site.Address),
        site.Phone, site.Email, site.IsActive, ToDto(site.OpeningHours));
}
