using System.ComponentModel.DataAnnotations;

namespace Trail.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = "";

    /// <summary>Apply pending migrations at startup. Convenient locally; production applies them from CI instead.</summary>
    public bool MigrateOnStartup { get; set; }
}
