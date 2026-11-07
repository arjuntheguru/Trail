using Microsoft.EntityFrameworkCore;
using Trail.Domain.Organizations;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Persistence;

public sealed class SitePersistenceTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _org;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync(mysql);
        await using var context = _db.Context(TestUser.Anonymous);
        var organization = new Organization { Name = "Alpha Foods", Slug = $"alpha-{Guid.NewGuid():N}" };
        context.Organizations.Add(organization);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _org = organization.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Address_and_opening_hours_survive_the_json_columns()
    {
        var address = new Address("1 High St", "Unit 2", "Leeds", "West Yorkshire", "LS1 4AB", "GB");
        List<OpeningHours> hours =
        [
            new(DayOfWeek.Sunday, new TimeOnly(9, 15, 30), new TimeOnly(0, 0)),
            new(DayOfWeek.Friday, new TimeOnly(22, 0), new TimeOnly(2, 30)),
            new(DayOfWeek.Saturday, new TimeOnly(23, 59, 59), new TimeOnly(23, 59, 58)),
        ];
        Guid id;
        await using (var write = _db.ContextFor(_org))
        {
            var site = new Site { Name = "Leeds", TimeZone = "Europe/London", Address = address, OpeningHours = hours };
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
            id = site.Id;
        }

        await using var read = _db.ContextFor(_org);
        var loaded = await read.Sites.AsNoTracking().SingleAsync(s => s.Id == id, Ct);

        loaded.Address.ShouldBe(address);
        loaded.OpeningHours.ShouldBe(hours);
        loaded.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_site_without_address_or_hours_round_trips_as_empty()
    {
        Guid id;
        await using (var write = _db.ContextFor(_org))
        {
            var site = new Site { Name = "Bare", TimeZone = "UTC" };
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
            id = site.Id;
        }

        await using var read = _db.ContextFor(_org);
        var loaded = await read.Sites.AsNoTracking().SingleAsync(s => s.Id == id, Ct);

        loaded.Address.ShouldBeNull();
        loaded.OpeningHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_opening_hours_in_place_is_detected_and_saved()
    {
        Guid id;
        await using (var write = _db.ContextFor(_org))
        {
            var site = new Site { Name = "Editable", TimeZone = "UTC" };
            write.Sites.Add(site);
            await write.SaveChangesAsync(Ct);
            id = site.Id;
        }

        await using (var edit = _db.ContextFor(_org))
        {
            var site = await edit.Sites.SingleAsync(s => s.Id == id, Ct);
            site.OpeningHours.Add(new OpeningHours(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(16, 0)));
            await edit.SaveChangesAsync(Ct);
        }

        await using var read = _db.ContextFor(_org);
        (await read.Sites.AsNoTracking().SingleAsync(s => s.Id == id, Ct)).OpeningHours
            .ShouldHaveSingleItem().Day.ShouldBe(DayOfWeek.Monday);
    }
}
