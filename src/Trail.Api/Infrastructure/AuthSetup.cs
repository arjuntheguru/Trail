using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Trail.Application.Common.Security;
using Trail.Application.Features.Integrations;
using Trail.Domain.Integrations;
using Trail.Infrastructure.Security;

namespace Trail.Api.Infrastructure;

public static class AuthPolicies
{
    public const string PlatformAdmin = "platform_admin";

    /// <summary>Signed in and acting inside an organization (has an org claim).</summary>
    public const string OrganizationMember = "organization_member";
}

internal static class AuthSetup
{
    public static IServiceCollection AddTrailAuth(this IServiceCollection services)
    {
        // JWT stays the default scheme. API keys are a separate scheme that only the policies below name explicitly,
        // so a key never authenticates a normal endpoint and a token never authenticates a sensor endpoint.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuth.Scheme, null);
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = TrailClaimTypes.Name,
                };
            });

        var authorization = services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.PlatformAdmin, p => p.RequireClaim(TrailClaimTypes.PlatformAdmin, "true"))
            .AddPolicy(AuthPolicies.OrganizationMember, p => p.RequireClaim(TrailClaimTypes.OrganizationId))
            .AddPolicy(ApiKeyAuth.SensorsWritePolicy, p => p
                .AddAuthenticationSchemes(ApiKeyAuth.Scheme)
                .RequireClaim(TrailClaimTypes.OrganizationId)
                .RequireClaim(ApiKeyClaims.Scope, ApiKeyScopes.SensorsWrite));

        // Each permission is a policy of the same name: .RequireAuthorization(Permissions.Sites.Manage).
        foreach (var permission in Permissions.All)
            authorization.AddPolicy(permission, p => p
                .RequireClaim(TrailClaimTypes.OrganizationId)
                .RequireClaim(TrailClaimTypes.Permission, permission));

        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
            };
            document.Components.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = ApiKeyAuth.HeaderName,
                In = ParameterLocation.Header,
            };
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
            return Task.CompletedTask;
        }));

        return services;
    }
}
