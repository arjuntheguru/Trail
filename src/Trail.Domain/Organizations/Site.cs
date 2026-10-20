using Trail.Domain.Common;

namespace Trail.Domain.Organizations;

/// <summary>Postal address of a site. Stored as a json column; never queried relationally.</summary>
public sealed record Address(
    string Line1,
    string? Line2,
    string City,
    string? StateOrRegion,
    string? PostalCode,
    string CountryCode)
{
    public const int LineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
}

/// <summary>
/// Opening window on one weekday, in the site's local time. <see cref="Closes"/> at or before <see cref="Opens"/>
/// means the site closes after midnight, on the following day.
/// </summary>
public sealed record OpeningHours(DayOfWeek Day, TimeOnly Opens, TimeOnly Closes);

/// <summary>A physical location where work happens (a restaurant, kitchen, hotel).</summary>
public sealed class Site : TenantEntity, ISoftDeletable
{
    public const int NameMaxLength = 200;
    public const int CodeMaxLength = 32;
    public const int TimeZoneMaxLength = 64;
    public const int PhoneMaxLength = 32;
    public const int EmailMaxLength = 254;

    public required string Name { get; set; }

    /// <summary>Short internal code, e.g. "LDN-01".</summary>
    public string? Code { get; set; }

    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }

    /// <summary>IANA time zone id; all site-local times (opening hours, schedules) are interpreted in it.</summary>
    public required string TimeZone { get; set; }

    public Address? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public List<OpeningHours> OpeningHours { get; set; } = [];

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
