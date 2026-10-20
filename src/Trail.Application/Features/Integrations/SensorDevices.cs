using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Domain.Integrations;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Integrations;

public abstract record SensorDeviceInput(string ExternalId, string Name, Guid SiteId, Guid TemplateId, string FieldKey);

public sealed record CreateSensorDeviceCommand(string ExternalId, string Name, Guid SiteId, Guid TemplateId, string FieldKey)
    : SensorDeviceInput(ExternalId, Name, SiteId, TemplateId, FieldKey);

public sealed record UpdateSensorDeviceCommand(string ExternalId, string Name, Guid SiteId, Guid TemplateId, string FieldKey)
    : SensorDeviceInput(ExternalId, Name, SiteId, TemplateId, FieldKey);

internal static class SensorDeviceRules
{
    public static Error NotFound { get; } = Error.NotFound("sensor_device.not_found", "Sensor device not found.");

    public static void AddCommonRules<T>(AbstractValidator<T> validator) where T : SensorDeviceInput
    {
        validator.RuleFor(x => x.ExternalId).NotEmpty().MaximumLength(SensorDevice.ExternalIdMaxLength);
        validator.RuleFor(x => x.Name).NotEmpty().MaximumLength(SensorDevice.NameMaxLength);
        validator.RuleFor(x => x.SiteId).NotEmpty();
        validator.RuleFor(x => x.TemplateId).NotEmpty();
        validator.RuleFor(x => x.FieldKey).NotEmpty().MaximumLength(TemplateField.KeyMaxLength);
    }

    /// <summary>The site must be one the caller can reach; the template must be a record log with that temperature field.</summary>
    public static async Task<Error?> CheckTargetAsync(
        IAppDbContext db, ISiteAccess access, SensorDeviceInput input, Guid? ignoreDeviceId, CancellationToken ct)
    {
        if (!await access.CanAccessSiteAsync(input.SiteId, ct) || !await db.Sites.AnyAsync(s => s.Id == input.SiteId, ct))
            return Error.NotFound("sensor_device.site_not_found", "Site not found.");

        var template = await db.TaskTemplates.AsNoTracking()
            .Where(t => t.Id == input.TemplateId)
            .Select(t => new { t.Kind, t.Fields })
            .FirstOrDefaultAsync(ct);
        if (template is null) return Error.NotFound("sensor_device.template_not_found", "Template not found.");

        if (template.Kind != TaskKind.RecordLog)
            return Error.Validation("sensor_device.template_not_record_log", "Readings can only feed a record log template.");
        var field = template.Fields.FirstOrDefault(f => f.Key == input.FieldKey);
        if (field is not { Type: FieldType.Temperature })
            return Error.Validation("sensor_device.field_not_temperature", "The template has no temperature field with that key.",
                new Dictionary<string, string[]> { ["fieldKey"] = ["Choose a Temperature field of the template."] });

        // Unique among live devices; the unique-index trick does not work with soft deletes in MySQL.
        var externalId = input.ExternalId.Trim();
        var taken = await db.SensorDevices.AnyAsync(d => d.ExternalId == externalId && d.Id != ignoreDeviceId, ct);
        return taken ? Error.Conflict("sensor_device.external_id_taken", "A device with this id already exists.") : null;
    }
}

internal sealed class CreateSensorDeviceValidator : AbstractValidator<CreateSensorDeviceCommand>
{
    public CreateSensorDeviceValidator() => SensorDeviceRules.AddCommonRules(this);
}

internal sealed class UpdateSensorDeviceValidator : AbstractValidator<UpdateSensorDeviceCommand>
{
    public UpdateSensorDeviceValidator() => SensorDeviceRules.AddCommonRules(this);
}

public sealed class CreateSensorDeviceHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<SensorDeviceResponse>> Handle(CreateSensorDeviceCommand command, CancellationToken ct)
    {
        if (await SensorDeviceRules.CheckTargetAsync(db, access, command, null, ct) is { } problem) return problem;
        if (await db.SensorDevices.CountAsync(ct) >= SensorDevice.MaxPerOrganization)
            return Error.Conflict("sensor_device.limit_reached", $"An organization can have at most {SensorDevice.MaxPerOrganization} devices.");

        var device = new SensorDevice
        {
            ExternalId = command.ExternalId.Trim(),
            Name = command.Name.Trim(),
            SiteId = command.SiteId,
            TemplateId = command.TemplateId,
            FieldKey = command.FieldKey,
        };
        db.SensorDevices.Add(device);
        await db.SaveChangesAsync(ct);
        return device.ToResponse();
    }
}

public sealed class UpdateSensorDeviceHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<SensorDeviceResponse>> Handle(Guid id, UpdateSensorDeviceCommand command, CancellationToken ct)
    {
        var device = await db.SensorDevices.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (device is null || !await access.CanAccessSiteAsync(device.SiteId, ct)) return SensorDeviceRules.NotFound;
        if (await SensorDeviceRules.CheckTargetAsync(db, access, command, id, ct) is { } problem) return problem;

        device.ExternalId = command.ExternalId.Trim();
        device.Name = command.Name.Trim();
        device.SiteId = command.SiteId;
        device.TemplateId = command.TemplateId;
        device.FieldKey = command.FieldKey;
        await db.SaveChangesAsync(ct);
        return device.ToResponse();
    }
}

public sealed class DeleteSensorDeviceHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var device = await db.SensorDevices.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (device is null || !await access.CanAccessSiteAsync(device.SiteId, ct)) return SensorDeviceRules.NotFound;

        db.SensorDevices.Remove(device);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class GetSensorDeviceHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<SensorDeviceResponse>> Handle(Guid id, CancellationToken ct)
    {
        var device = await db.SensorDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return device is null || !await access.CanAccessSiteAsync(device.SiteId, ct) ? SensorDeviceRules.NotFound : device.ToResponse();
    }
}

public sealed class ListSensorDevicesHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<PagedResult<SensorDeviceResponse>> Handle(PageRequest page, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var query = db.SensorDevices.AsNoTracking().AsQueryable();
        if (!scope.AllSites)
        {
            var siteIds = scope.SiteIds.ToList();
            query = query.Where(d => siteIds.Contains(d.SiteId));
        }

        return await query
            .OrderBy(d => d.Name).ThenBy(d => d.Id)
            .Select(IntegrationMapping.DeviceProjection)
            .ToPagedResultAsync(page, ct);
    }
}
