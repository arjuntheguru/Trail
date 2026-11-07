using System.ComponentModel.DataAnnotations;

namespace Trail.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = "";

    /// <summary>Apply pending migrations at startup. Convenient locally; production applies them from CI instead.</summary>
    public bool MigrateOnStartup { get; set; }

    /// <summary>Per-command timeout in seconds.</summary>
    [Range(5, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>How long a replica waits for another replica's migration run before giving up, in seconds.</summary>
    [Range(1, 3600)]
    public int MigrationLockWaitSeconds { get; set; } = 300;
}
