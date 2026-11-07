using System.Reflection;

namespace Trail.Application.Common.Security;

/// <summary>
/// Every permission an organization role can grant. Values are stable strings stored in role definitions and
/// carried as <see cref="TrailClaimTypes.Permission"/> claims, so never rename one; add a new one instead.
/// Each permission is also an authorization policy of the same name.
/// </summary>
public static class Permissions
{
    public static class Organization
    {
        public const string Manage = "organization.manage";
    }

    public static class Members
    {
        public const string View = "members.view";
        public const string Manage = "members.manage";
    }

    public static class Roles
    {
        public const string Manage = "roles.manage";
    }

    public static class Sites
    {
        public const string View = "sites.view";
        public const string Manage = "sites.manage";
    }

    public static class Tags
    {
        public const string Manage = "tags.manage";
    }

    public static class Templates
    {
        public const string View = "templates.view";
        public const string Manage = "templates.manage";
    }

    public static class Schedules
    {
        public const string Manage = "schedules.manage";
    }

    public static class Tasks
    {
        public const string View = "tasks.view";
        public const string Complete = "tasks.complete";
        public const string CreateAdHoc = "tasks.create_ad_hoc";
        public const string Approve = "tasks.approve";
    }

    public static class Issues
    {
        public const string View = "issues.view";
        public const string Raise = "issues.raise";
        public const string Manage = "issues.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Audit
    {
        public const string View = "audit.view";
    }

    public static class Integrations
    {
        public const string Manage = "integrations.manage";
    }

    /// <summary>All permission values, discovered from the constants above.</summary>
    public static IReadOnlyList<string> All { get; } = typeof(Permissions)
        .GetNestedTypes()
        .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .Order(StringComparer.Ordinal)
        .ToList();

    public static bool IsKnown(string permission) => All.Contains(permission, StringComparer.Ordinal);
}

/// <summary>Built-in roles created for every organization. Owners can add custom roles beside them.</summary>
public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string AreaManager = "Area Manager";
    public const string SiteManager = "Site Manager";
    public const string TeamMember = "Team Member";

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Definitions { get; } =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Owner] = Permissions.All,
            [Admin] = Permissions.All.Where(p => p != Permissions.Organization.Manage).ToList(),
            [AreaManager] =
            [
                Permissions.Members.View, Permissions.Sites.View, Permissions.Sites.Manage,
                Permissions.Templates.View, Permissions.Templates.Manage, Permissions.Schedules.Manage,
                Permissions.Tasks.View, Permissions.Tasks.Complete, Permissions.Tasks.CreateAdHoc, Permissions.Tasks.Approve,
                Permissions.Issues.View, Permissions.Issues.Raise, Permissions.Issues.Manage,
                Permissions.Reports.View, Permissions.Reports.Export, Permissions.Audit.View,
            ],
            [SiteManager] =
            [
                Permissions.Members.View, Permissions.Sites.View, Permissions.Templates.View,
                Permissions.Schedules.Manage,
                Permissions.Tasks.View, Permissions.Tasks.Complete, Permissions.Tasks.CreateAdHoc, Permissions.Tasks.Approve,
                Permissions.Issues.View, Permissions.Issues.Raise, Permissions.Issues.Manage,
                Permissions.Reports.View,
            ],
            [TeamMember] =
            [
                Permissions.Sites.View, Permissions.Tasks.View, Permissions.Tasks.Complete,
                Permissions.Tasks.CreateAdHoc, Permissions.Issues.View, Permissions.Issues.Raise,
            ],
        };
}
