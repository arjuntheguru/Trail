using Trail.Application.Common.Security;
using Trail.Application.Features.Notifications;
using Trail.Domain.Issues;

namespace Trail.UnitTests.Notifications;

public sealed class DigestTests
{
    // 2026-03-10 09:30 UTC: 09:30 in London (GMT), 20:30 in Sydney (UTC+11), 05:30 in New York (EDT, UTC-4).
    private static readonly DateTime Now = new(2026, 3, 10, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid Me = Guid.CreateVersion7();
    private static readonly Guid Other = Guid.CreateVersion7();

    private static readonly DigestSite Soho = new(Guid.CreateVersion7(), "Soho", "Europe/London");
    private static readonly DigestSite Bondi = new(Guid.CreateVersion7(), "Bondi", "Australia/Sydney");

    private static readonly string[] AllPermissions =
        [Permissions.Tasks.View, Permissions.Tasks.Approve, Permissions.Issues.View];

    private static Dictionary<Guid, DigestSite> Sites(params DigestSite[] sites) => sites.ToDictionary(s => s.Id);

    private static DigestIssueRow Issue(
        DigestSite site, int number, Guid? assignee, DateTime? due, IssuePriority priority = IssuePriority.Medium) =>
        new(Guid.CreateVersion7(), number, $"Issue {number}", site.Id, assignee, priority, due);

    private sealed class Links : INotificationLinks
    {
        public string Issue(Guid issueId) => $"https://app/issues/{issueId}";
        public string Task(Guid taskId) => $"https://app/tasks/{taskId}";
        public string NotificationSettings() => "https://app/settings";
    }

    [Fact]
    public void Only_each_sites_local_yesterday_counts_as_missed()
    {
        // Soho's today is 2026-03-10; Bondi's is 2026-03-10 as well (20:30), New York would be the 10th too.
        var rows = new[]
        {
            new DigestTaskRow(Guid.CreateVersion7(), "Fridge temps", Soho.Id, new DateOnly(2026, 3, 9)),
            new DigestTaskRow(Guid.CreateVersion7(), "Too old", Soho.Id, new DateOnly(2026, 3, 8)),
            new DigestTaskRow(Guid.CreateVersion7(), "Today still open", Soho.Id, new DateOnly(2026, 3, 10)),
            new DigestTaskRow(Guid.CreateVersion7(), "Elsewhere", Guid.CreateVersion7(), new DateOnly(2026, 3, 9)),
        };

        var content = DigestSelector.Select(Me, AllPermissions, Sites(Soho), rows, [], [], Now);

        content.MissedTasks.Select(t => t.Title).ShouldBe(["Fridge temps"]);
        content.MissedTasks[0].SiteName.ShouldBe("Soho");
    }

    [Fact]
    public void Yesterday_is_worked_out_per_site_time_zone()
    {
        var late = new DateTime(2026, 3, 10, 22, 0, 0, DateTimeKind.Utc); // Sydney is already on the 11th
        var rows = new[]
        {
            new DigestTaskRow(Guid.CreateVersion7(), "Bondi 10th", Bondi.Id, new DateOnly(2026, 3, 10)),
            new DigestTaskRow(Guid.CreateVersion7(), "Soho 9th", Soho.Id, new DateOnly(2026, 3, 9)),
            new DigestTaskRow(Guid.CreateVersion7(), "Soho 10th", Soho.Id, new DateOnly(2026, 3, 10)),
        };

        var content = DigestSelector.Select(Me, AllPermissions, Sites(Soho, Bondi), rows, [], [], late);

        content.MissedTasks.Select(t => t.Title).ShouldBe(["Bondi 10th", "Soho 9th"]);
    }

    [Fact]
    public void Issues_are_split_into_mine_and_overdue_at_my_sites()
    {
        var mineOverdue = Issue(Soho, 1, Me, Now.AddHours(-3), IssuePriority.High);
        var mineFuture = Issue(Soho, 2, Me, Now.AddDays(1));
        var theirsOverdue = Issue(Soho, 3, Other, Now.AddHours(-1));
        var theirsFuture = Issue(Soho, 4, Other, Now.AddDays(2));
        var unassignedOverdue = Issue(Soho, 5, null, Now.AddHours(-9));
        var otherSite = Issue(Bondi, 6, Other, Now.AddHours(-9));

        var content = DigestSelector.Select(
            Me, AllPermissions, Sites(Soho), [], [], [mineOverdue, mineFuture, theirsOverdue, theirsFuture, unassignedOverdue, otherSite], Now);

        content.MyOpenIssues.Select(i => i.Number).ShouldBe([1, 2]); // highest priority first
        content.OverdueIssues.Select(i => i.Number).ShouldBe([5, 3]); // most overdue first, mine not repeated
    }

    [Fact]
    public void Approvals_are_listed_only_for_people_who_can_approve_at_those_sites()
    {
        var awaiting = new[]
        {
            new DigestTaskRow(Guid.CreateVersion7(), "Cleaning log", Soho.Id, new DateOnly(2026, 3, 9)),
            new DigestTaskRow(Guid.CreateVersion7(), "Elsewhere", Bondi.Id, new DateOnly(2026, 3, 9)),
        };

        DigestSelector.Select(Me, AllPermissions, Sites(Soho), [], awaiting, [], Now)
            .AwaitingApproval.Select(t => t.Title).ShouldBe(["Cleaning log"]);
        DigestSelector.Select(Me, [Permissions.Tasks.View, Permissions.Issues.View], Sites(Soho), [], awaiting, [], Now)
            .AwaitingApproval.ShouldBeEmpty();
    }

    [Fact]
    public void Without_permissions_nothing_is_selected()
    {
        var rows = new[] { new DigestTaskRow(Guid.CreateVersion7(), "Fridge", Soho.Id, new DateOnly(2026, 3, 9)) };
        var issues = new[] { Issue(Soho, 1, Me, Now.AddHours(-1)) };

        DigestSelector.Select(Me, [], Sites(Soho), rows, rows, issues, Now).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void An_empty_digest_composes_no_email()
    {
        var empty = DigestSelector.Select(Me, AllPermissions, Sites(Soho), [], [], [], Now);

        empty.IsEmpty.ShouldBeTrue();
        DigestComposer.Compose("a@b.c", "Al", "Acme", new DateOnly(2026, 3, 10), empty, new Links()).ShouldBeNull();
    }

    [Fact]
    public void The_digest_groups_missed_tasks_and_overdue_issues_by_site()
    {
        var soho = Soho;
        var rows = new[]
        {
            new DigestTaskRow(Guid.CreateVersion7(), "Fridge temps", soho.Id, new DateOnly(2026, 3, 9)),
            new DigestTaskRow(Guid.CreateVersion7(), "Closing checks", soho.Id, new DateOnly(2026, 3, 9)),
            new DigestTaskRow(Guid.CreateVersion7(), "Deep clean", Bondi.Id, new DateOnly(2026, 3, 9)),
        };
        var overdue = Issue(Soho, 12, Other, Now.AddHours(-5), IssuePriority.Critical);
        var mine = Issue(Bondi, 7, Me, Now.AddDays(1));
        var content = DigestSelector.Select(
            Me, AllPermissions, Sites(Soho, Bondi), rows, [], [overdue, mine], Now);

        var message = DigestComposer.Compose("al@example.com", "Al", "Acme", new DateOnly(2026, 3, 10), content, new Links())!;

        message.To.ShouldBe("al@example.com");
        message.Subject.ShouldContain("2026-03-10");
        var body = message.TextBody;
        body.ShouldContain("Hi Al");
        body.ShouldContain("Open issues assigned to you (1)");
        body.ShouldContain("#7 Issue 7");
        body.ShouldContain($"https://app/issues/{mine.Id}");
        body.ShouldNotContain("Tasks waiting for your approval");
        body.ShouldContain("Overdue issues at your sites (1)");
        body.ShouldContain("Tasks missed yesterday (3)");
        body.ShouldContain("Bondi (1)");
        body.ShouldContain("Soho (2)");
        body.IndexOf("Bondi (1)", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("Soho (2)", StringComparison.Ordinal));
        body.ShouldContain("https://app/settings");
    }

    [Fact]
    public void Overdue_issues_appear_under_their_site()
    {
        var content = new DigestContent(
            [], [], [],
            [new DigestIssueItem(Guid.CreateVersion7(), 12, "Broken oven", "Soho", IssuePriority.High, Now.AddHours(-5)),
             new DigestIssueItem(Guid.CreateVersion7(), 13, "Pest sighting", "Bondi", IssuePriority.Critical, Now.AddHours(-1))]);

        var body = DigestComposer.Compose("a@b.c", "Al", "Acme", new DateOnly(2026, 3, 10), content, new Links())!.TextBody;

        body.ShouldContain("Overdue issues at your sites (2)");
        body.IndexOf("Bondi\n", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("#13", StringComparison.Ordinal));
        body.IndexOf("#13", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("Soho\n", StringComparison.Ordinal));
        body.IndexOf("Soho\n", StringComparison.Ordinal).ShouldBeLessThan(body.IndexOf("#12", StringComparison.Ordinal));
    }

    [Fact]
    public void Long_sections_are_cut_with_a_count_of_the_rest()
    {
        var tasks = Enumerable.Range(1, DigestComposer.MaxItemsPerSection + 5)
            .Select(i => new DigestTaskItem(Guid.CreateVersion7(), $"Task {i:00}", "Soho", new DateOnly(2026, 3, 9))).ToList();
        var content = new DigestContent(tasks, [], [], []);

        var body = DigestComposer.Compose("a@b.c", "Al", "Acme", new DateOnly(2026, 3, 10), content, new Links())!.TextBody;

        body.ShouldContain("Task 15");
        body.ShouldNotContain("Task 16");
        body.ShouldContain("...and 5 more");
    }

    [Fact]
    public void The_digest_zone_is_the_most_common_site_zone_else_the_organization_zone()
    {
        DigestSchedule.ZoneFor(["Europe/London", "Australia/Sydney", "Europe/London"], "UTC").ShouldBe("Europe/London");
        DigestSchedule.ZoneFor(["Europe/London", "Australia/Sydney"], "UTC").ShouldBe("Australia/Sydney"); // tie: alphabetical
        DigestSchedule.ZoneFor([], "America/New_York").ShouldBe("America/New_York");
    }

    [Fact]
    public void A_digest_is_due_from_the_local_hour_and_once_per_local_day()
    {
        // 09:30 UTC: 09:30 in London, 20:30 in Sydney, 05:30 in New York.
        DigestSchedule.IsDue(Now, "Europe/London", 7, null, out var london).ShouldBeTrue();
        london.ShouldBe(new DateOnly(2026, 3, 10));
        DigestSchedule.IsDue(Now, "Europe/London", 10, null, out _).ShouldBeFalse();
        DigestSchedule.IsDue(Now, "America/New_York", 7, null, out _).ShouldBeFalse();
        DigestSchedule.IsDue(Now, "Australia/Sydney", 7, new DateOnly(2026, 3, 10), out _).ShouldBeFalse();
        DigestSchedule.IsDue(Now, "Australia/Sydney", 7, new DateOnly(2026, 3, 9), out var sydney).ShouldBeTrue();
        sydney.ShouldBe(new DateOnly(2026, 3, 10));
    }

    [Fact]
    public void An_unknown_zone_falls_back_to_utc() =>
        DigestSchedule.IsDue(Now, "Not/AZone", 9, null, out _).ShouldBeTrue();
}
