using Trail.Domain.Common;

namespace Trail.Domain.Integrations;

/// <summary>
/// A temperature sensor that reports through the API. Its readings become entries of the site's record log
/// for <see cref="TemplateId"/>, answering the Temperature field <see cref="FieldKey"/>.
/// </summary>
public sealed class SensorDevice : TenantEntity, ISoftDeletable
{
    public const int ExternalIdMaxLength = 100;
    public const int NameMaxLength = 200;
    public const int MaxPerOrganization = 500;

    /// <summary>The identifier the sensor or its gateway sends, unique among the organization's live devices.</summary>
    public required string ExternalId { get; set; }

    public required string Name { get; set; }
    public Guid SiteId { get; set; }
    public Guid TemplateId { get; set; }
    public required string FieldKey { get; set; }

    [NotAudited] public DateTime? LastReadingAt { get; set; }
    [NotAudited] public decimal? LastValue { get; set; }

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
