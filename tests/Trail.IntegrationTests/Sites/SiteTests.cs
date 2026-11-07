using System.Net;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Paging;
using Trail.Application.Features.Sites;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Sites;

public sealed class SiteTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TrailApiFactory _factory = null!;
    private TestMember _owner = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _owner = await _factory.CreateOrganizationAsync(); // default time zone Europe/London
        _client = _factory.CreateClient(_owner);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateSiteCommand NewSite(
        string name = "Soho Kitchen", Guid? regionId = null, string? timeZone = null,
        IReadOnlyList<OpeningHoursDto>? hours = null, AddressDto? address = null, string? email = null) =>
        new(name, "LDN-01", regionId, timeZone, address, "+44 20 7946 0000", email, hours);

    private static UpdateSiteCommand ToUpdate(CreateSiteCommand c) =>
        new(c.Name, c.Code, c.RegionId, c.TimeZone, c.Address, c.Phone, c.Email, c.OpeningHours);

    [Fact]
    public async Task A_site_round_trips_with_address_and_opening_hours()
    {
        var region = await _client.CreateRegionAsync("London");
        var address = new AddressDto("1 Old Compton St", null, "London", null, "W1D 4TB", "gb");
        List<OpeningHoursDto> hours =
        [
            new(DayOfWeek.Friday, new TimeOnly(22, 0), new TimeOnly(2, 30)), // closes after midnight
            new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 45)),
        ];

        var created = await _client.PostJsonAsync("/api/v1/sites",
            NewSite(regionId: region.Id, timeZone: "America/New_York", hours: hours, address: address, email: "soho@example.com"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var site = await created.ReadAsync<SiteResponse>();
        created.Headers.Location!.ToString().ShouldEndWith($"/api/v1/sites/{site.Id}");

        var fetched = await (await _client.GetAsync($"/api/v1/sites/{site.Id}", Ct)).ReadAsync<SiteResponse>();
        fetched.ShouldBeEquivalentTo(site);
        fetched.RegionName.ShouldBe("London");
        fetched.TimeZone.ShouldBe("America/New_York");
        fetched.IsActive.ShouldBeTrue();
        fetched.Address.ShouldBe(address with { CountryCode = "GB" });
        // Stored Monday first, with times intact.
        fetched.OpeningHours.ShouldBe(
        [
            new OpeningHoursDto(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 45)),
            new OpeningHoursDto(DayOfWeek.Friday, new TimeOnly(22, 0), new TimeOnly(2, 30)),
        ]);
    }

    [Fact]
    public async Task Time_zone_defaults_to_the_organizations()
    {
        var site = await _client.CreateSiteAsync("Default Zone");

        site.TimeZone.ShouldBe("Europe/London");
        site.Address.ShouldBeNull();
        site.OpeningHours.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sites_can_be_updated_and_clear_optional_fields()
    {
        var site = await _client.CreateSiteAsync("Original");
        var region = await _client.CreateRegionAsync("East");

        var update = new UpdateSiteCommand("Renamed", null, region.Id, "Asia/Tokyo",
            new AddressDto("2 Street", "Floor 3", "Tokyo", null, null, "JP"), null, null,
            [new OpeningHoursDto(DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(10, 0))]);
        var response = await _client.PutJsonAsync($"/api/v1/sites/{site.Id}", update);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsync<SiteResponse>();
        updated.Name.ShouldBe("Renamed");
        updated.Code.ShouldBeNull();
        updated.RegionName.ShouldBe("East");
        updated.TimeZone.ShouldBe("Asia/Tokyo");
        updated.Address!.Line2.ShouldBe("Floor 3");
        updated.OpeningHours.ShouldHaveSingleItem().Closes.ShouldBe(new TimeOnly(10, 0));

        // Omitting the time zone keeps the current one; omitting address and hours clears them.
        var cleared = await (await _client.PutJsonAsync($"/api/v1/sites/{site.Id}",
            new UpdateSiteCommand("Renamed", null, null, null, null, null, null, null))).ReadAsync<SiteResponse>();
        cleared.TimeZone.ShouldBe("Asia/Tokyo");
        cleared.Address.ShouldBeNull();
        cleared.OpeningHours.ShouldBeEmpty();
        cleared.RegionId.ShouldBeNull();
    }

    [Fact]
    public async Task Sites_can_be_deactivated_activated_and_deleted()
    {
        var site = await _client.CreateSiteAsync("Seasonal");

        (await _client.PostEmptyAsync($"/api/v1/sites/{site.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(site.Id)).IsActive.ShouldBeFalse();

        var inactive = await ListAsync("isActive=false");
        inactive.Items.ShouldHaveSingleItem().Id.ShouldBe(site.Id);
        (await ListAsync("isActive=true")).Items.ShouldBeEmpty();

        (await _client.PostEmptyAsync($"/api/v1/sites/{site.Id}/activate")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync(site.Id)).IsActive.ShouldBeTrue();

        (await _client.DeleteAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.GetAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ListAsync("")).Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_site_keeps_the_row_for_history()
    {
        var site = await _client.CreateSiteAsync("Historic");
        (await _client.DeleteAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var deletedAt = await _factory.WithDbAsync(_owner.OrganizationId, db =>
            db.Sites.IgnoreQueryFilters(["SoftDelete"]).Where(s => s.Id == site.Id).Select(s => s.DeletedAt).SingleAsync(Ct));
        deletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_list_pages_searches_and_filters()
    {
        var north = await _client.CreateRegionAsync("North");
        await _client.CreateSiteAsync("Bravo", north.Id);
        await _client.CreateSiteAsync("Alpha", north.Id);
        await _client.CreateSiteAsync("Charlie");
        await _client.PostJsonAsync("/api/v1/sites", NewSite("Delta") with { Code = "XYZ-9" });

        var all = await ListAsync("pageSize=3");
        all.Items.Select(s => s.Name).ShouldBe(["Alpha", "Bravo", "Charlie"]);
        (all.TotalCount, all.TotalPages, all.HasNextPage).ShouldBe((4, 2, true));
        (await ListAsync("page=2&pageSize=3")).Items.Select(s => s.Name).ShouldBe(["Delta"]);

        (await ListAsync($"regionId={north.Id}")).Items.Select(s => s.Name).ShouldBe(["Alpha", "Bravo"]);
        (await ListAsync("search=ARLI")).Items.Select(s => s.Name).ShouldBe(["Charlie"]);
        (await ListAsync("search=xyz")).Items.Select(s => s.Name).ShouldBe(["Delta"]);
        (await ListAsync("search=nothing-like-this")).Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_name_is_rejected(string name)
    {
        (await _client.PostJsonAsync("/api/v1/sites", NewSite(name))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Invalid_input_is_rejected()
    {
        async Task ShouldReject(CreateSiteCommand command) =>
            (await _client.PostJsonAsync("/api/v1/sites", command)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await ShouldReject(NewSite(new string('x', 201)));
        await ShouldReject(NewSite() with { Code = new string('x', 33) });
        await ShouldReject(NewSite(email: "not-an-email"));
        await ShouldReject(NewSite() with { Phone = new string('1', 33) });
        await ShouldReject(NewSite(address: new AddressDto("", null, "London", null, null, "GB")));
        await ShouldReject(NewSite(address: new AddressDto("1 St", null, "London", null, null, "GBR")));
        await ShouldReject(NewSite(address: new AddressDto("1 St", null, "", null, null, "GB")));
    }

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("Pacific Standard Time")]
    [InlineData("garbage")]
    public async Task An_invalid_time_zone_is_rejected(string timeZone)
    {
        var response = await _client.PostJsonAsync("/api/v1/sites", NewSite(timeZone: timeZone));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var site = await _client.CreateSiteAsync("Valid");
        (await _client.PutJsonAsync($"/api/v1/sites/{site.Id}", ToUpdate(NewSite("Valid", timeZone: timeZone))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Opening_hours_may_list_each_day_only_once()
    {
        var hours = new[]
        {
            new OpeningHoursDto(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            new OpeningHoursDto(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(18, 0)),
        };

        (await _client.PostJsonAsync("/api/v1/sites", NewSite(hours: hours))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_region_is_rejected()
    {
        var response = await _client.PostJsonAsync("/api/v1/sites", NewSite(regionId: Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ProblemCodeAsync()).ShouldBe("site.unknown_region");
    }

    [Fact]
    public async Task A_region_from_another_organization_is_rejected()
    {
        var otherOwner = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));
        var foreignRegion = await otherOwner.CreateRegionAsync("Foreign");

        var response = await _client.PostJsonAsync("/api/v1/sites", NewSite(regionId: foreignRegion.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Duplicate_names_conflict_but_deleted_names_are_free()
    {
        var first = await _client.CreateSiteAsync("Camden");
        var second = await _client.CreateSiteAsync("Islington");

        var duplicate = await _client.PostJsonAsync("/api/v1/sites", NewSite("camden"));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("site.name_taken");

        var rename = await _client.PutJsonAsync($"/api/v1/sites/{second.Id}", ToUpdate(NewSite("Camden")));
        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _client.PutJsonAsync($"/api/v1/sites/{first.Id}", ToUpdate(NewSite("Camden"))))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await _client.DeleteAsync($"/api/v1/sites/{first.Id}", Ct);
        (await _client.PostJsonAsync("/api/v1/sites", NewSite("Camden"))).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Unknown_sites_return_not_found()
    {
        var id = Guid.NewGuid();
        (await _client.GetAsync($"/api/v1/sites/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PutJsonAsync($"/api/v1/sites/{id}", ToUpdate(NewSite()))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostEmptyAsync($"/api/v1/sites/{id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.PostEmptyAsync($"/api/v1/sites/{id}/activate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/v1/sites/{id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var notFound = await _client.GetAsync($"/api/v1/sites/{id}", Ct);
        (await notFound.ProblemCodeAsync()).ShouldBe("site.not_found");
    }

    [Fact]
    public async Task Team_members_can_view_but_not_manage_sites()
    {
        var site = await _client.CreateSiteAsync("Visible");
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId));

        (await teamMember.GetAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await teamMember.GetAsync("/api/v1/sites", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await teamMember.PostJsonAsync("/api/v1/sites", NewSite("Nope"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.PutJsonAsync($"/api/v1/sites/{site.Id}", ToUpdate(NewSite("Nope")))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.PostEmptyAsync($"/api/v1/sites/{site.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamMember.DeleteAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/v1/sites", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_change_sites()
    {
        var site = await _client.CreateSiteAsync("Private Site");
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await other.GetAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PutJsonAsync($"/api/v1/sites/{site.Id}", ToUpdate(NewSite("Hacked")))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostEmptyAsync($"/api/v1/sites/{site.Id}/deactivate")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/v1/sites/{site.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await other.GetAsync("/api/v1/sites", Ct)).ReadAsync<PagedResult<SiteResponse>>()).Items.ShouldBeEmpty();

        var unchanged = await GetAsync(site.Id);
        (unchanged.Name, unchanged.IsActive).ShouldBe(("Private Site", true));
    }

    private async Task<SiteResponse> GetAsync(Guid id) =>
        await (await _client.GetAsync($"/api/v1/sites/{id}", Ct)).ReadAsync<SiteResponse>();

    private async Task<PagedResult<SiteResponse>> ListAsync(string query) =>
        await (await _client.GetAsync($"/api/v1/sites?{query}", Ct)).ReadAsync<PagedResult<SiteResponse>>();
}
