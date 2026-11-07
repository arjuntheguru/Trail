using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Auditing;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Persistence;

public sealed class AuditTrailTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _org;
    private TestUser _user = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync(mysql);
        await using var context = _db.Context(TestUser.Anonymous);
        var organization = new Organization { Name = "Alpha Foods", Slug = $"alpha-{Guid.NewGuid():N}" };
        context.Organizations.Add(organization);
        await context.SaveChangesAsync(Ct);
        _org = organization.Id;
        _user = TestUser.MemberOf(_org);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<AuditEntry>> TrailForAsync(Guid entityId)
    {
        await using var read = _db.ContextFor(_org);
        return await read.AuditEntries.AsNoTracking().Where(a => a.EntityId == entityId).OrderBy(a => a.Id).ToListAsync(Ct);
    }

    [Fact]
    public async Task Create_update_and_soft_delete_are_recorded_with_the_changed_values()
    {
        var site = new Site { Name = "Leeds", TimeZone = "Europe/London" };
        await using (var write = _db.Context(_user))
        {
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
        }

        await using (var edit = _db.Context(_user))
        {
            var loaded = await edit.Sites.SingleAsync(s => s.Id == site.Id, Ct);
            loaded.Name = "Leeds Central";
            loaded.TimeZone = "Europe/London"; // unchanged value: not recorded
            await edit.SaveChangesAsync(Ct);

            edit.Sites.Remove(loaded);
            await edit.SaveChangesAsync(Ct);
        }

        var trail = await TrailForAsync(site.Id);
        trail.Select(a => a.Action).ShouldBe([AuditAction.Created, AuditAction.Updated, AuditAction.Deleted]);
        trail.ShouldAllBe(a => a.OrganizationId == _org && a.SiteId == site.Id && a.ActorId == _user.UserId && a.EntityType == "Site");
        trail[0].OccurredAt.ShouldBe(_db.Clock.GetUtcNow().UtcDateTime);

        trail[0].Changes["Name"].New!.Value.GetString().ShouldBe("Leeds");
        trail[0].Changes.ShouldNotContainKey("CreatedAt");
        trail[0].Changes.ShouldNotContainKey("OrganizationId");

        var update = trail[1].Changes.ShouldHaveSingleItem();
        update.Key.ShouldBe("Name");
        update.Value.Old!.Value.GetString().ShouldBe("Leeds");
        update.Value.New!.Value.GetString().ShouldBe("Leeds Central");
    }

    [Fact]
    public async Task Rows_belonging_to_a_site_carry_its_id_for_scoping()
    {
        var region = new Region { Name = "North" };
        var site = new Site { Name = "York", TimeZone = "Europe/London" };
        await using (var write = _db.Context(_user))
        {
            write.Regions.Add(region);
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
        }

        (await TrailForAsync(region.Id)).ShouldHaveSingleItem().SiteId.ShouldBeNull();
        (await TrailForAsync(site.Id)).ShouldHaveSingleItem().SiteId.ShouldBe(site.Id);
    }

    [Fact]
    public async Task Secrets_and_sign_in_bookkeeping_stay_out_of_the_trail()
    {
        var user = new User { Email = "cook@trail.test", NormalizedEmail = "COOK@TRAIL.TEST", FirstName = "Sam", LastName = "Cook" };
        user.SetPasswordHash("not-a-real-hash");
        await using (var write = _db.Context(TestUser.Anonymous))
        {
            write.Users.Add(user);
            write.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id, FamilyId = Guid.CreateVersion7(), TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(1),
            });
            await write.SaveChangesAsync(Ct);
        }

        await using (var signIn = _db.Context(TestUser.Anonymous))
        {
            var loaded = await signIn.Users.SingleAsync(u => u.Id == user.Id, Ct);
            loaded.RecordFailedSignIn(DateTime.UtcNow);
            await signIn.SaveChangesAsync(Ct);
        }

        await using var read = _db.Context(TestUser.Anonymous);
        var trail = await read.AuditEntries.IgnoreQueryFilters([QueryFilters.Tenant]).AsNoTracking()
            .Where(a => a.EntityId == user.Id || a.EntityType == nameof(RefreshToken)).ToListAsync(Ct);

        var created = trail.ShouldHaveSingleItem(); // no row for the token, none for the failed sign-in
        created.Action.ShouldBe(AuditAction.Created);
        created.OrganizationId.ShouldBeNull();
        created.Changes.ShouldContainKey("Email");
        created.Changes.ShouldNotContainKey("PasswordHash");
        created.Changes.ShouldNotContainKey("SecurityStamp");
    }

    [Fact]
    public async Task A_failed_save_leaves_no_audit_rows()
    {
        var first = new Site { Name = "Hull", TimeZone = "Europe/London" };
        await using (var write = _db.Context(_user))
        {
            write.Sites.Add(first);
            await write.SaveChangesAsync(Ct);
        }

        var duplicateId = first.Id;
        await using (var failing = _db.Context(_user))
        {
            var clash = new Site { Name = "Clash", TimeZone = "UTC" };
            typeof(Trail.Domain.Common.Entity).GetProperty("Id")!.SetValue(clash, duplicateId);
            failing.Sites.Add(clash);
            await Should.ThrowAsync<DbUpdateException>(() => failing.SaveChangesAsync(Ct));
        }

        (await TrailForAsync(first.Id)).ShouldHaveSingleItem().Action.ShouldBe(AuditAction.Created);
    }

    [Fact]
    public async Task Other_organizations_cannot_read_the_trail()
    {
        var site = new Site { Name = "Leeds", TimeZone = "Europe/London" };
        await using (var write = _db.Context(_user))
        {
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
        }

        await using var other = _db.ContextFor(Guid.CreateVersion7());
        (await other.AuditEntries.CountAsync(a => a.EntityId == site.Id, Ct)).ShouldBe(0);
    }
}
