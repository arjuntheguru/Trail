using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using Serilog;
using Trail.Api.Infrastructure;
using Trail.Application;
using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Integrations;
using Trail.Infrastructure;
using Trail.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

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

builder.Services.AddOptions<FrontendCorsOptions>().BindConfiguration(FrontendCorsOptions.SectionName);
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection(FrontendCorsOptions.SectionName).Get<FrontendCorsOptions>()?.AllowedOrigins ?? [];
    options.AddPolicy(FrontendCorsOptions.PolicyName, policy => policy
        .WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var rateLimits = builder.Configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = rateLimits.GlobalPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = rateLimits.AuthPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    // Rate limiting runs before authentication, so the key's database id is not known yet: the partition is the key's
    // public prefix (the identifier part of the key), and requests without a well-formed key share the client IP.
    options.AddPolicy(RateLimitPolicies.Sensors, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            ApiKeyFormat.TryParse(ApiKeyAuth.ReadKey(context.Request), out var prefix)
                ? $"key:{prefix}"
                : $"ip:{context.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = rateLimits.SensorPermitsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Behind a reverse proxy (nginx/Caddy) on the same host. Add KnownProxies/KnownNetworks for anything else.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();
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
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();
app.UseCors(FrontendCorsOptions.PolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapEndpointGroups(typeof(Program).Assembly);

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.MigrateDatabaseAsync();
await app.Services.EnsurePlatformAdminAsync();

await app.RunAsync();

public partial class Program;
