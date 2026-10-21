using System.Net;
using Trail.Application.Features.Regions;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Regions;

public sealed class RegionTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync();
        _client = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Regions_can_be_created_listed_updated_and_deleted()
    {
        var created = await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("  North ", "Northern sites"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var region = await created.ReadAsync<RegionResponse>();
        region.Name.ShouldBe("North");
        created.Headers.Location!.ToString().ShouldEndWith($"/api/v1/regions/{region.Id}");
        await _client.CreateRegionAsync("Alpha");

        var list = await (await _client.GetAsync("/api/v1/regions", Ct)).ReadAsync<List<RegionResponse>>();
        list.Select(r => r.Name).ShouldBe(["Alpha", "North"]);

        var updated = await _client.PutJsonAsync($"/api/v1/regions/{region.Id}", new UpdateRegionCommand("North East", null));
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await updated.ReadAsync<RegionResponse>()).Name.ShouldBe("North East");

        (await _client.DeleteAsync($"/api/v1/regions/{region.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/v1/regions/{region.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_regions_are_rejected()
    {
        (await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("", null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand(new string('x', 101), null)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("Ok", new string('x', 501))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_names_conflict_even_in_a_different_case()
    {
        var region = await _client.CreateRegionAsync("South");
        var other = await _client.CreateRegionAsync("West");

        var duplicate = await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("SOUTH", null));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("region.name_taken");

        var rename = await _client.PutJsonAsync($"/api/v1/regions/{other.Id}", new UpdateRegionCommand("South", null));
        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Keeping its own name is fine.
        (await _client.PutJsonAsync($"/api/v1/regions/{region.Id}", new UpdateRegionCommand("South", "same")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_deleted_regions_name_can_be_reused()
    {
        var region = await _client.CreateRegionAsync("Temp");
        (await _client.DeleteAsync($"/api/v1/regions/{region.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("Temp", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_region_with_sites_cannot_be_deleted()
    {
        var region = await _client.CreateRegionAsync("Central");
        var site = await _client.CreateSiteAsync("Central Kitchen", region.Id);

        var blocked = await _client.DeleteAsync($"/api/v1/regions/{region.Id}", Ct);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.ProblemCodeAsync()).ShouldBe("region.has_sites");

        (await _client.DeleteAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/v1/regions/{region.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Unknown_regions_return_not_found()
    {
        var id = Guid.NewGuid();
        (await _client.PutJsonAsync($"/api/v1/regions/{id}", new UpdateRegionCommand("X", null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/v1/regions/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Members_without_permission_cannot_manage_regions_but_can_list_them()
    {
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId));

        (await teamMember.GetAsync("/api/v1/regions", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await teamMember.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("Nope", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/v1/regions", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_change_regions()
    {
        var region = await _client.CreateRegionAsync("Private");
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await (await other.GetAsync("/api/v1/regions", Ct)).ReadAsync<List<RegionResponse>>()).ShouldBeEmpty();
        (await other.PutJsonAsync($"/api/v1/regions/{region.Id}", new UpdateRegionCommand("Hacked", null)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/v1/regions/{region.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The same name is free in another organization, and the original is untouched.
        (await other.PostJsonAsync("/api/v1/regions", new CreateRegionCommand("Private", null)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        var mine = await (await _client.GetAsync("/api/v1/regions", Ct)).ReadAsync<List<RegionResponse>>();
        mine.ShouldHaveSingleItem().Name.ShouldBe("Private");
    }
}
