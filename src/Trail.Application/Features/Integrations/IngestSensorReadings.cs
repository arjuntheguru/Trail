using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;
using Trail.Domain.Integrations;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Integrations;

/// <param name="DeviceId">The device's external id as registered under sensor devices.</param>
/// <param name="Value">Temperature in degrees Celsius.</param>
/// <param name="RecordedAt">When the sensor took the reading, in UTC.</param>
public sealed record SensorReadingInput(string DeviceId, decimal Value, DateTimeOffset RecordedAt);

public sealed record SensorReadingsCommand(IReadOnlyList<SensorReadingInput> Readings);

internal sealed class SensorReadingsValidator : AbstractValidator<SensorReadingsCommand>
{
    public SensorReadingsValidator()
    {
        RuleFor(x => x.Readings)
            .NotNull()
            .Must(r => r is { Count: > 0 }).WithMessage("Send at least one reading.")
            .Must(r => r is null || r.Count <= SensorReadingRules.MaxReadingsPerRequest)
            .WithMessage($"Send at most {SensorReadingRules.MaxReadingsPerRequest} readings per request.")
            .Must(r => r is null || r.All(x => x is not null)).WithMessage("A reading cannot be null.")
            .Must(r => r is null || r.All(x => x is null || (!string.IsNullOrWhiteSpace(x.DeviceId) && x.DeviceId.Length <= SensorDevice.ExternalIdMaxLength)))
            .WithMessage("Every reading needs a deviceId of at most 100 characters.")
            .Must(r => r is null || r.All(x => x is null || Math.Abs(x.Value) <= 1000))
            .WithMessage("Temperatures must be within +-1000.");
    }
}

/// <summary>
/// Turns sensor readings into record log entries for the day they were taken on. Results are reported per
/// reading, so one unknown device or missing task does not fail the batch.
/// <para>
/// Retries do not double-log: an entry made by a sensor carries the reading's time (whole seconds) as its
/// submission time and the "Recorded by sensor" note, so a reading is a duplicate when its task already holds a
/// sensor entry with the same time and note. That needs no new table or column and is checked against the
/// entries of the few tasks the batch touches, plus the readings earlier in the same batch.
/// </para>
/// </summary>
public sealed class IngestSensorReadingsHandler(IAppDbContext db, RecordLogWriter writer, TimeProvider clock)
{
    private sealed record TaskKey(Guid SiteId, Guid TemplateId, DateOnly Date);

    public async Task<Result<SensorReadingsResponse>> Handle(Guid apiKeyId, SensorReadingsCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // The caller was authenticated as this key; check again that it can still do this and note its site limit.
        var key = await db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == apiKeyId, ct);
        if (key is null || !key.IsUsable(now) || !key.Scopes.Contains(ApiKeyScopes.SensorsWrite))
            return Error.Unauthorized("sensors.key_unusable", "The API key cannot be used.");

        var externalIds = command.Readings.Select(r => r.DeviceId.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var devices = (await db.SensorDevices.Where(d => externalIds.Contains(d.ExternalId)).ToListAsync(ct))
            .GroupBy(d => d.ExternalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var siteIds = devices.Values.Select(d => d.SiteId).Distinct().ToList();
        var zones = await db.Sites.AsNoTracking().Where(s => siteIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.TimeZone, ct);

        var tasks = new Dictionary<TaskKey, SiteTask?>();
        var seen = new Dictionary<Guid, HashSet<(long Ticks, string? Note)>>();
        var results = new List<SensorReadingResult>(command.Readings.Count);

        for (var index = 0; index < command.Readings.Count; index++)
        {
            var reading = command.Readings[index];
            var deviceId = reading.DeviceId.Trim();
            SensorReadingResult Outcome(string result, string? detail = null) => new(index, deviceId, result, detail);

            var recordedAt = SensorReadingRules.Normalize(reading.RecordedAt.UtcDateTime);
            if (SensorReadingRules.CheckTime(recordedAt, now) is { } badTime)
            {
                results.Add(Outcome(badTime));
                continue;
            }

            if (!devices.TryGetValue(deviceId, out var device))
            {
                results.Add(Outcome(SensorResults.UnknownDevice));
                continue;
            }
            if (!key.AllowsSite(device.SiteId))
            {
                results.Add(Outcome(SensorResults.SiteNotAllowed));
                continue;
            }
            if (!zones.TryGetValue(device.SiteId, out var timeZone))
            {
                results.Add(Outcome(SensorResults.UnknownDevice, "The device's site is no longer available."));
                continue;
            }

            // The device is alive whatever happens to the reading, so note it (not audited, never fails the batch).
            if (device.LastReadingAt is null || recordedAt > device.LastReadingAt)
            {
                device.LastReadingAt = recordedAt;
                device.LastValue = reading.Value;
            }

            var taskKey = new TaskKey(device.SiteId, device.TemplateId, SensorReadingRules.LocalDate(recordedAt, timeZone));
            if (!tasks.TryGetValue(taskKey, out var task))
            {
                task = await db.SiteTasks
                    .Where(t => t.SiteId == taskKey.SiteId && t.TemplateId == taskKey.TemplateId && t.DueDate == taskKey.Date
                                && t.Kind == TaskKind.RecordLog && t.Status == SiteTaskStatus.Open)
                    .OrderBy(t => t.DueAt).ThenBy(t => t.Id)
                    .FirstOrDefaultAsync(ct);
                tasks[taskKey] = task;
            }
            if (task is null)
            {
                results.Add(Outcome(SensorResults.NoOpenTask));
                continue;
            }

            if (!seen.TryGetValue(task.Id, out var known))
            {
                var existing = await db.TaskEntries.AsNoTracking()
                    .Where(e => e.SiteTaskId == task.Id && e.SubmittedById == null)
                    .Select(e => new { e.SubmittedAt, e.Note })
                    .ToListAsync(ct);
                known = existing.Where(e => e.SubmittedAt != null).Select(e => (e.SubmittedAt!.Value.Ticks, e.Note)).ToHashSet();
                seen[task.Id] = known;
            }

            var note = SensorReadingRules.Note(device.Name);
            if (known.Contains((recordedAt.Ticks, note)))
            {
                results.Add(Outcome(SensorResults.Duplicate));
                continue;
            }

            var answers = SensorReadingRules.ToAnswers(device.FieldKey, reading.Value);
            var added = await writer.AddAsync(
                task, new RecordLogReading(answers, note, [], recordedAt, SubmittedById: null, Automated: true), now, ct);
            if (added.IsFailure)
            {
                results.Add(Rejection(added.Error!, Outcome));
                continue;
            }

            known.Add((recordedAt.Ticks, note));
            results.Add(Outcome(SensorResults.Accepted));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the same task meanwhile. Nothing was stored; the sensor may simply resend.
            return Error.Conflict("sensors.concurrent_update", "The readings could not be stored because of a concurrent change. Send them again.");
        }

        return new SensorReadingsResponse(
            results.Count(r => r.Result == SensorResults.Accepted),
            results.Count(r => r.Result.StartsWith("skipped", StringComparison.Ordinal)),
            results.Count(r => r.Result.StartsWith("rejected", StringComparison.Ordinal)),
            results);
    }

    private static SensorReadingResult Rejection(Error error, Func<string, string?, SensorReadingResult> result) => error.Code switch
    {
        "task.not_available_yet" => result(SensorResults.TaskNotAvailable, null),
        "task.not_open" or "task.late_window_closed" or "task.wrong_kind" => result(SensorResults.NoOpenTask, null),
        _ => result(SensorResults.InvalidReading, Describe(error)),
    };

    private static string Describe(Error error) =>
        error.Details is { Count: > 0 } details
            ? string.Join(" ", details.Values.SelectMany(v => v))
            : error.Description;
}
