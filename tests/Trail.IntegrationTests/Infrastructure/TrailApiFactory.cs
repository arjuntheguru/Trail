using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Application.Features.Organizations;
using Trail.Application.Features.Tasks;
using Trail.Domain.Identity;
using Trail.Infrastructure;
using Trail.Infrastructure.Persistence;

namespace Trail.IntegrationTests.Infrastructure;

/// <summary>The real API wired to a fresh, migrated database. Create one per test class.</summary>
public sealed class TrailApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string DefaultPassword = "correct-horse-battery";

    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:ConnectionString"] = connectionString,
            ["Database:MigrateOnStartup"] = "false",
            ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            ["App:FrontendBaseUrl"] = "https://app.trail.test",
            ["Serilog:MinimumLevel:Default"] = "Warning",
            ["RateLimiting:GlobalPermitsPerMinute"] = "100000",
            ["RateLimiting:AuthPermitsPerMinute"] = "100000",
            // Tests drive generation explicitly through RunTaskGenerationAsync against Clock.
            ["TaskGeneration:Enabled"] = "false",
            ["NotificationEmail:Enabled"] = "false",
            ["IssueEscalation:Enabled"] = "false",
            ["DailyDigest:Enabled"] = "false",
        }));
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Emails));
    }

    public static async Task<TrailApiFactory> CreateAsync(MySqlFixture mysql)
    {
        var factory = new TrailApiFactory(await mysql.CreateDatabaseAsync());
        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }

    /// <summary>Creates an organization (with system roles) and returns its Owner.</summary>
    public async Task<TestMember> CreateOrganizationAsync(string name = "Test Kitchen")
    {
        await using var scope = Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<TrailDbContext>();
        var user = NewUser(sp, $"owner-{Guid.NewGuid():N}@trail.test");
        db.Users.Add(user);

        var (organization, membership) = await sp.GetRequiredService<OrganizationProvisioner>()
            .ProvisionAsync(name, user, "Europe/London", CancellationToken.None);
        await db.SaveChangesAsync();

        return new TestMember(user.Id, user.Email, organization.Id, membership.Id, SystemRoles.Owner,
            SystemRoles.Definitions[SystemRoles.Owner]);
    }

    /// <summary>Adds an active member with one of the organization's roles (system or custom) by name.</summary>
    public async Task<TestMember> AddMemberAsync(Guid organizationId, string roleName = SystemRoles.TeamMember, bool allSites = true)
    {
        await using var scope = Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
        var db = sp.GetRequiredService<TrailDbContext>();

        var role = await db.Roles.SingleAsync(r => r.Name == roleName);
        var user = NewUser(sp, $"member-{Guid.NewGuid():N}@trail.test");
        var membership = new Membership
        {
            User = user,
            UserId = user.Id,
            RoleId = role.Id,
            Status = MembershipStatus.Active,
            AllSites = allSites,
            JoinedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        return new TestMember(user.Id, user.Email, organizationId, membership.Id, role.Name, role.Permissions);
    }

    /// <summary>An HTTP client authenticated as <paramref name="member"/> with a freshly issued access token.</summary>
    public HttpClient CreateClient(TestMember member)
    {
        var token = Services.GetRequiredService<IAccessTokenIssuer>().Issue(new AccessTokenSubject(
            member.UserId, member.Email, "Test User", false, member.OrganizationId, member.MembershipId, member.Permissions));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    /// <summary>Runs <paramref name="action"/> against the database scoped to one organization.</summary>
    public async Task<T> WithDbAsync<T>(Guid organizationId, Func<TrailDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
        return await action(scope.ServiceProvider.GetRequiredService<TrailDbContext>());
    }

    /// <summary>
    /// Runs one generation pass (close ended days, then generate) for an organization as if it were
    /// <paramref name="at"/>. The app's own clock is left alone so tokens keep validating.
    /// </summary>
    public async Task<(int Created, int Missed)> RunTaskGenerationAsync(Guid organizationId, DateTimeOffset at)
    {
        await using var scope = Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
        var generator = new TaskGenerator(
            sp.GetRequiredService<IAppDbContext>(), new FakeTimeProvider(at), NullLogger<TaskGenerator>.Instance);
        var missed = await generator.MarkMissedAsync(CancellationToken.None);
        var created = await generator.GenerateAsync(CancellationToken.None);
        return (created, missed);
    }

    private static User NewUser(IServiceProvider sp, string email)
    {
        var user = new User
        {
            Email = email,
            NormalizedEmail = User.Normalize(email),
            FirstName = "Test",
            LastName = "User",
            EmailConfirmedAt = DateTime.UtcNow,
        };
        user.SetPasswordHash(sp.GetRequiredService<IPasswordHasher>().Hash(user, DefaultPassword));
        return user;
    }
}

public sealed record TestMember(
    Guid UserId,
    string Email,
    Guid OrganizationId,
    Guid MembershipId,
    string RoleName,
    IReadOnlyCollection<string> Permissions);

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage LastTo(string email) => Sent.Last(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>Extracts the token query parameter from the link in the last email sent to <paramref name="email"/>.</summary>
    public string TokenFromLastEmailTo(string email)
    {
        var body = LastTo(email).TextBody;
        var start = body.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        var end = body.IndexOfAny([' ', '\n', '\r'], start);
        return Uri.UnescapeDataString(end < 0 ? body[start..] : body[start..end]);
    }
}
