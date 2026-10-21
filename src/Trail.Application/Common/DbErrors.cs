using Microsoft.EntityFrameworkCore;

namespace Trail.Application.Common;

public static class DbErrors
{
    /// <summary>True when a save failed because a unique index rejected a duplicate (MySQL error 1062).</summary>
    public static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true;
}
