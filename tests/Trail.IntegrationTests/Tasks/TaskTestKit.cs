using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trail.Domain.Common;
using Trail.Domain.Files;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Tasks;

/// <summary>Seeding helpers for task execution tests. Goes straight to the database; the behavior under test is driven over HTTP.</summary>
internal sealed class TaskTestKit(TrailApiFactory factory, Guid organizationId)
{
    public static readonly TemplateField Temperature = new()
    {
        Key = "temp", Label = "Fridge temperature", Type = FieldType.Temperature, Min = 0, Max = 5,
    };

    public static readonly TemplateField Clean = new()
    {
        Key = "clean", Label = "Surfaces clean", Type = FieldType.YesNo, ExpectedAnswer = true, CommentRequiredOnFailure = false,
    };

    public static readonly TemplateField Notes = new()
    {
        Key = "notes", Label = "Notes", Type = FieldType.Text, Required = false,
    };

    public Guid OrganizationId { get; } = organizationId;

    public static DateTime Now => DateTime.UtcNow;

    public static DateOnly TodayAt(Site site) => SiteTime.Today(Now, SiteTime.Zone(site.TimeZone));

    public Task<T> InDbAsync<T>(Func<Trail.Infrastructure.Persistence.TrailDbContext, Task<T>> action) =>
        factory.WithDbAsync(OrganizationId, action);

    public Task<Site> AddSiteAsync(string name, string timeZone = "Europe/London", bool isActive = true) =>
        InDbAsync(async db =>
        {
            var site = new Site { Name = name, TimeZone = timeZone, IsActive = isActive };
            db.Sites.Add(site);
            await db.SaveChangesAsync();
            return site;
        });

    public Task<Department> AddDepartmentAsync(string name) =>
        InDbAsync(async db =>
        {
            var department = new Department { Name = name };
            db.Departments.Add(department);
            await db.SaveChangesAsync();
            return department;
        });

    public Task<TaskTemplate> AddTemplateAsync(
        TaskKind kind = TaskKind.Checklist, string name = "Daily checks", Action<TaskTemplate>? configure = null,
        params TemplateField[] fields) =>
        InDbAsync(async db =>
        {
            var template = new TaskTemplate { Name = name, Kind = kind, Fields = [.. fields] };
            configure?.Invoke(template);
            db.TaskTemplates.Add(template);
            await db.SaveChangesAsync();
            return template;
        });

    /// <summary>A task on the site's list for its local today, due 23:59 and available all day unless configured otherwise.</summary>
    public Task<SiteTask> AddTaskAsync(Site site, TaskTemplate template, Action<SiteTask>? configure = null) =>
        InDbAsync(async db =>
        {
            var task = SiteTask.FromTemplate(
                template, site.Id, SiteTime.Zone(site.TimeZone), TodayAt(site), new TimeOnly(23, 59), null, SiteTaskSource.Scheduled);
            configure?.Invoke(task);
            db.SiteTasks.Add(task);
            await db.SaveChangesAsync();
            return task;
        });

    public Task<Attachment> AddAttachmentAsync(Site site, AttachmentKind kind = AttachmentKind.Photo) =>
        InDbAsync(async db =>
        {
            var attachment = new Attachment
            {
                Kind = kind, FileName = "evidence.png", ContentType = "image/png", SizeBytes = 8,
                StorageKey = $"{OrganizationId}/test/{Guid.NewGuid():N}.png", Sha256 = new string('0', 64), SiteId = site.Id,
            };
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync();
            return attachment;
        });

    public Task<SiteTask> ReloadAsync(Guid taskId) =>
        InDbAsync(db => db.SiteTasks.AsNoTracking().Include(t => t.Entries).SingleAsync(t => t.Id == taskId));

    /// <summary>An answer in the request shape: <c>Answer(3.5)</c>, <c>Answer(true)</c>, <c>Answer("text", comment: "why")</c>.</summary>
    public static object Answer(object? value = null, string? comment = null, params Guid[] photoIds) => new
    {
        value = value is null ? (JsonElement?)null : JsonSerializer.SerializeToElement(value),
        comment,
        photoIds,
    };

    public static object Answers(params (string Key, object Answer)[] answers) =>
        new { answers = answers.ToDictionary(a => a.Key, a => a.Answer) };

    public static object Answers(string? note, Guid[]? photoIds, params (string Key, object Answer)[] answers) =>
        new { answers = answers.ToDictionary(a => a.Key, a => a.Answer), note, photoIds };

    /// <summary>The problem keys of a 400 validation response, e.g. <c>answers.temp</c>.</summary>
    public static async Task<string[]> ErrorKeysAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.TryGetProperty("errors", out var errors)
            ? [.. errors.EnumerateObject().Select(e => e.Name)]
            : [];
    }
}
