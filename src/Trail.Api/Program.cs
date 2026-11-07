using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using Scalar.AspNetCore;

using Serilog;
using Serilog.Formatting.Compact;

using Trail.Api.Infrastructure;
using Trail.Application;
using Trail.Application.Common.Abstractions;
using Trail.Infrastructure;
using Trail.Infrastructure.Security;

using HttpOptions = Trail.Api.Infrastructure.HttpOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) =>
{
    logger
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", Observability.ServiceName);

    // Readable console while developing; one compact JSON document per line (with @tr/@sp trace ids) everywhere else.
    if (builder.Environment.IsDevelopment()) logger.WriteTo.Console();
    else logger.WriteTo.Console(new RenderedCompactJsonFormatter());
});
builder.Services.AddTrailObservability(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IRequestContext, RequestContext>();

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddTrailAuth();

// CORS, rate limits, proxy trust and request size limits, all from validated options (see EdgeSetup).
builder.Services.AddTrailEdge();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
else
{
    app.UseHsts();
    // TLS usually terminates at the proxy in containers: Http:HttpsRedirection=false turns the redirect off.
    if (app.Services.GetRequiredService<IOptions<HttpOptions>>().Value.HttpsRedirection)
        app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = RequestLogging.GetLevel;
    options.EnrichDiagnosticContext = (diagnostics, http) => RequestLogging.Enrich(diagnostics, http);
});
app.UseCors(FrontendCorsOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Probes are anonymous and never rate limited, so an orchestrator cannot lock itself out.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous().DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
})
    .AllowAnonymous().DisableRateLimiting();
app.MapEndpointGroups(typeof(Program).Assembly);

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.MigrateDatabaseAsync();
await app.Services.EnsurePlatformAdminAsync();

await app.RunAsync();

public partial class Program;