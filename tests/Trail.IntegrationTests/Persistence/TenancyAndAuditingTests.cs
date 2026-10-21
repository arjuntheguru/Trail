using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;
using Trail.Domain.Organizations;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Persistence;

public sealed class TenancyAndAuditingTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _orgA;
    private Guid _orgB;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync(mysql);
        await using var context = _db.Context(TestUser.Anonymous);
        var a = new Organization { Name = "Alpha Foods", Slug = $"alpha-{Guid.NewGuid():N}" };
        var b = new Organization { Name = "Beta Bakes", Slug = $"beta-{Guid.NewGuid():N}" };
        context.Organizations.AddRange(a, b);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        (_orgA, _orgB) = (a.Id, b.Id);

        await using var asA = _db.ContextFor(_orgA);
        asA.Tags.Add(new Tag { Name = "Kitchen" });
        await asA.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var asB = _db.ContextFor(_orgB);
        asB.Tags.Add(new Tag { Name = "Bar" });
        await asB.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Queries_only_return_the_callers_organization()
    {
        await using var context = _db.Context(TestUser.MemberOf(_orgA));

        var names = await context.Tags.Select(t => t.Name).ToListAsync(Ct);

        names.ShouldBe(["Kitchen"]);
    }

    [Fact]
    public async Task Lookups_by_id_cannot_reach_another_organization()
    {
        Guid betaTagId;
        await using (var asB = _db.ContextFor(_orgB))
            betaTagId = await asB.Tags.Select(t => t.Id).SingleAsync(Ct);

        await using var context = _db.Context(TestUser.MemberOf(_orgA));

        (await context.Tags.FirstOrDefaultAsync(t => t.Id == betaTagId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Without_an_active_organization_tenant_queries_return_nothing()
    {
        await using var context = _db.Context(TestUser.Anonymous);

        (await context.Tags.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task Inserts_are_stamped_with_the_active_organization_and_audit_fields()
    {
        var user = TestUser.MemberOf(_orgA);
        var tag = new Tag { Name = "Cellar" };

        await using (var context = _db.Context(user))
        {
            context.Tags.Add(tag);
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = _db.Context(user);
        var saved = await verify.Tags.SingleAsync(t => t.Id == tag.Id, Ct);
        saved.OrganizationId.ShouldBe(_orgA);
        saved.CreatedById.ShouldBe(user.UserId);
        saved.CreatedAt.ShouldBe(_db.Clock.GetUtcNow().UtcDateTime);
        saved.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        saved.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Inserting_into_another_organization_is_rejected()
    {
        await using var context = _db.Context(TestUser.MemberOf(_orgA));
        context.Tags.Add(new Tag { Name = "Sneaky", OrganizationId = _orgB });

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Writing_without_an_active_organization_is_rejected()
    {
        await using var context = _db.Context(TestUser.Anonymous);
        context.Tags.Add(new Tag { Name = "Orphan" });

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Updating_a_row_from_another_organization_is_rejected_even_when_loaded_unfiltered()
    {
        await using var context = _db.Context(TestUser.MemberOf(_orgA));
        var foreign = await context.Tags.IgnoreQueryFilters([QueryFilters.Tenant]).SingleAsync(t => t.Name == "Bar", Ct);
        foreign.Name = "Hijacked";

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Moving_a_row_to_another_organization_is_rejected()
    {
        await using var context = _db.Context(TestUser.MemberOf(_orgA));
        var tag = await context.Tags.SingleAsync(t => t.Name == "Kitchen", Ct);
        tag.OrganizationId = _orgB;

        await Should.ThrowAsync<TenantIsolationException>(() => context.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Updates_stamp_audit_fields_and_rotate_the_concurrency_stamp()
    {
        var user = TestUser.MemberOf(_orgA);
        await using var context = _db.Context(user);
        var tag = await context.Tags.SingleAsync(t => t.Name == "Kitchen", Ct);
        var created = (tag.CreatedAt, tag.CreatedById, tag.ConcurrencyStamp);
        _db.Clock.Advance(TimeSpan.FromMinutes(5));

        tag.Color = "#1F8A70";
        await context.SaveChangesAsync(Ct);

        tag.UpdatedAt.ShouldBe(_db.Clock.GetUtcNow().UtcDateTime);
        tag.UpdatedById.ShouldBe(user.UserId);
        tag.ConcurrencyStamp.ShouldNotBe(created.ConcurrencyStamp);
        (tag.CreatedAt, tag.CreatedById).ShouldBe((created.CreatedAt, created.CreatedById));
    }

    [Fact]
    public async Task Concurrent_edits_are_detected()
    {
        var user = TestUser.MemberOf(_orgA);
        await using var first = _db.Context(user);
        await using var second = _db.Context(user);
        var a = await first.Tags.SingleAsync(t => t.Name == "Kitchen", Ct);
        var b = await second.Tags.SingleAsync(t => t.Name == "Kitchen", Ct);

        a.Color = "#000000";
        await first.SaveChangesAsync(Ct);
        b.Color = "#FFFFFF";

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Deleting_a_soft_deletable_entity_hides_it_but_keeps_the_row()
    {
        var org = new Organization { Name = "Gamma Grill", Slug = $"gamma-{Guid.NewGuid():N}" };
        await using (var context = _db.Context(TestUser.Anonymous))
        {
            context.Organizations.Add(org);
            await context.SaveChangesAsync(Ct);
            context.Organizations.Remove(org);
            await context.SaveChangesAsync(Ct);
        }

        await using var verify = _db.Context(TestUser.Anonymous);
        (await verify.Organizations.AnyAsync(o => o.Id == org.Id, Ct)).ShouldBeFalse();
        var row = await verify.Organizations.IgnoreQueryFilters([QueryFilters.SoftDelete]).SingleAsync(o => o.Id == org.Id, Ct);
        row.DeletedAt.ShouldBe(_db.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Tag_names_are_unique_per_organization_only()
    {
        await using var context = _db.ContextFor(_orgB);
        context.Tags.Add(new Tag { Name = "Kitchen" });

        await Should.NotThrowAsync(() => context.SaveChangesAsync(Ct));

        context.Tags.Add(new Tag { Name = "Kitchen" });
        await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
    }
}
