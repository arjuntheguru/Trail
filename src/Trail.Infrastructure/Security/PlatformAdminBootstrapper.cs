using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Identity;
using Trail.Infrastructure.Persistence;

namespace Trail.Infrastructure.Security;

/// <summary>
/// Creates the first platform admin from configuration (Bootstrap:PlatformAdminEmail/Password) when none exists.
/// Credentials come from secrets or environment variables, never source code.
/// </summary>
public static class PlatformAdminBootstrapper
{
    public static async Task EnsurePlatformAdminAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<BootstrapOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.PlatformAdminEmail)) return;

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PlatformAdminBootstrapper));
        var db = scope.ServiceProvider.GetRequiredService<TrailDbContext>();
        if (await db.Users.AnyAsync(u => u.IsPlatformAdmin, ct)) return;

        if (string.IsNullOrEmpty(options.PlatformAdminPassword) || options.PlatformAdminPassword.Length < 12)
        {
            logger.LogWarning("Bootstrap:PlatformAdminPassword must be at least 12 characters; platform admin not created.");
            return;
        }

        var normalized = User.Normalize(options.PlatformAdminEmail);
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (user is null)
        {
            user = new User
            {
                Email = options.PlatformAdminEmail.Trim(),
                NormalizedEmail = normalized,
                FirstName = "Platform",
                LastName = "Admin",
            };
            db.Users.Add(user);
        }

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        user.SetPasswordHash(hasher.Hash(user, options.PlatformAdminPassword));
        user.IsPlatformAdmin = true;
        user.EmailConfirmedAt ??= scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created platform admin {Email}", user.Email);
    }
}
