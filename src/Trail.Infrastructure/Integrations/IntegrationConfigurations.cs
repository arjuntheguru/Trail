using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trail.Domain.Integrations;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.Infrastructure.Persistence.Conventions;

namespace Trail.Infrastructure.Integrations;

internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.Property(x => x.Url).HasMaxLength(WebhookSubscription.UrlMaxLength);
        builder.Property(x => x.Description).HasMaxLength(WebhookSubscription.DescriptionMaxLength);
        builder.Property(x => x.EventTypes).HasJsonConversion();
        builder.Property(x => x.Secret).HasMaxLength(64);
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.Property(x => x.EventType).HasMaxLength(WebhookDelivery.EventTypeMaxLength);
        // The exact text that is signed and sent; longtext keeps it byte for byte (a json column would reformat it).
        builder.Property(x => x.Payload).HasColumnType("longtext");
        builder.Property(x => x.LastError).HasMaxLength(WebhookDelivery.ErrorMaxLength);
        builder.HasOne<WebhookSubscription>().WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SubscriptionId, x.EventId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => new { x.SubscriptionId, x.CreatedAt });
    }
}

internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("api_keys");
        builder.Property(x => x.Name).HasMaxLength(ApiKey.NameMaxLength);
        builder.Property(x => x.Prefix).HasMaxLength(ApiKey.PrefixLength);
        builder.Property(x => x.KeyHash).HasMaxLength(64).IsFixedLength();
        builder.Property(x => x.Scopes).HasJsonConversion();
        builder.Property(x => x.SiteIds).HasJsonConversion();

        // The hash covers 190 bits of randomness, so it is unique across all organizations. Authentication looks keys
        // up by it without knowing the organization. It does not include DeletedAt, so the soft-delete caveat does not apply.
        builder.HasIndex(x => x.KeyHash).IsUnique();
        builder.HasIndex(x => x.Prefix);
    }
}

internal sealed class SensorDeviceConfiguration : IEntityTypeConfiguration<SensorDevice>
{
    public void Configure(EntityTypeBuilder<SensorDevice> builder)
    {
        builder.ToTable("sensor_devices");
        builder.Property(x => x.ExternalId).HasMaxLength(SensorDevice.ExternalIdMaxLength);
        builder.Property(x => x.Name).HasMaxLength(SensorDevice.NameMaxLength);
        builder.Property(x => x.FieldKey).HasMaxLength(TemplateField.KeyMaxLength);
        builder.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaskTemplate>().WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);

        // Not unique: soft-deleted rows would collide in MySQL; the handler enforces uniqueness among live devices.
        builder.HasIndex(x => new { x.OrganizationId, x.ExternalId });
        builder.HasIndex(x => x.SiteId);
        builder.HasIndex(x => x.TemplateId);
    }
}
