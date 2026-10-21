using Microsoft.Net.Http.Headers;
using Trail.Api.Infrastructure;
using Trail.Application.Common.Security;
using Trail.Application.Features.Reports;

namespace Trail.Api.Endpoints;

internal sealed class ReportEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("reports").WithTags("Reports").RequireAuthorization();

        group.MapGet("compliance", async ([AsParameters] ComplianceReportQuery query, GetComplianceReportHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp())
            .RequireAuthorization(Permissions.Reports.View);

        group.MapGet("dashboard", async ([AsParameters] DashboardQuery query, GetDashboardHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp())
            .RequireAuthorization(Permissions.Reports.View);

        group.MapGet("activity", async ([AsParameters] ActivityFeedQuery query, GetActivityFeedHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp())
            .RequireAuthorization(Permissions.Reports.View);

        group.MapGet("tasks.csv", async ([AsParameters] ExportTasksQuery query, ExportTasksCsvHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp(export => new CsvResult(export)))
            .RequireAuthorization(Permissions.Reports.Export);

        group.MapGet("issues.csv", async ([AsParameters] ExportIssuesQuery query, ExportIssuesCsvHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp(export => new CsvResult(export)))
            .RequireAuthorization(Permissions.Reports.Export);

        // Both policies must pass: the export permission and the right to see the audit trail.
        group.MapGet("audit.csv", async ([AsParameters] ExportAuditQuery query, ExportAuditCsvHandler handler, CancellationToken ct) =>
                (await handler.Handle(query, ct)).ToHttp(export => new CsvResult(export)))
            .RequireAuthorization(Permissions.Audit.View)
            .RequireAuthorization(Permissions.Reports.Export);
    }
}

/// <summary>Streams a prepared CSV export straight into the response body as an attachment.</summary>
internal sealed class CsvResult(CsvExport export) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.ContentType = "text/csv; charset=utf-8";
        response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = export.FileName }.ToString();
        response.Headers.CacheControl = "no-store";
        await export.WriteAsync(response.Body, httpContext.RequestAborted);
    }
}
