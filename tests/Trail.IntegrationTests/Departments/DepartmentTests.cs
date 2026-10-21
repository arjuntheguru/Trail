using System.Net;
using Trail.Application.Features.Departments;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Departments;

public sealed class DepartmentTests(MySqlFixture mysql) : IAsyncLifetime
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
    public async Task Departments_can_be_created_listed_updated_and_deleted()
    {
        var created = await _client.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand(" Kitchen "));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var department = await created.ReadAsync<DepartmentResponse>();
        department.Name.ShouldBe("Kitchen");
        await _client.CreateDepartmentAsync("Bar");

        var list = await (await _client.GetAsync("/api/v1/departments", Ct)).ReadAsync<List<DepartmentResponse>>();
        list.Select(d => d.Name).ShouldBe(["Bar", "Kitchen"]);

        var updated = await _client.PutJsonAsync($"/api/v1/departments/{department.Id}", new UpdateDepartmentCommand("Back of House"));
        (await updated.ReadAsync<DepartmentResponse>()).Name.ShouldBe("Back of House");

        (await _client.DeleteAsync($"/api/v1/departments/{department.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/v1/departments/{department.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_names_are_rejected_and_duplicates_conflict()
    {
        (await _client.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand(" ")))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand(new string('x', 101))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await _client.CreateDepartmentAsync("Front of House");
        var duplicate = await _client.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand("front of house"));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ProblemCodeAsync()).ShouldBe("department.name_taken");
    }

    [Fact]
    public async Task Unknown_departments_return_not_found()
    {
        (await _client.PutJsonAsync($"/api/v1/departments/{Guid.NewGuid()}", new UpdateDepartmentCommand("X")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Members_without_permission_cannot_manage_departments()
    {
        var teamMember = _factory.CreateClient(await _factory.AddMemberAsync(_owner.OrganizationId));

        (await teamMember.GetAsync("/api/v1/departments", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await teamMember.PostJsonAsync("/api/v1/departments", new CreateDepartmentCommand("Nope")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_organization_cannot_see_or_change_departments()
    {
        var department = await _client.CreateDepartmentAsync("Private");
        var other = _factory.CreateClient(await _factory.CreateOrganizationAsync("Other Org"));

        (await (await other.GetAsync("/api/v1/departments", Ct)).ReadAsync<List<DepartmentResponse>>()).ShouldBeEmpty();
        (await other.PutJsonAsync($"/api/v1/departments/{department.Id}", new UpdateDepartmentCommand("Hacked")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/v1/departments/{department.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
