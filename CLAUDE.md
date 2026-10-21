# Trail backend

Multi-tenant SaaS for hospitality operations and food-safety compliance, modelled on trailapp.com: scheduled
checklists and record logs per site, issues and corrective actions, audit trail and reporting.

## Stack
- .NET 10, ASP.NET Core minimal APIs, EF Core 10 on MySQL 8.4 via Oracle `MySql.EntityFrameworkCore`.
- FluentValidation, Serilog, built-in OpenAPI + Scalar (Development only).
- Tests: xUnit v3 on Microsoft.Testing.Platform, Shouldly, NSubstitute, Testcontainers (`mysql:8.4`).

## Commands
- Build: `dotnet build Trail.slnx`
- All tests (Docker must be running): `dotnet test --solution Trail.slnx`
- One project: `dotnet test --project tests/Trail.IntegrationTests`
- Filter: `dotnet test --project tests/Trail.UnitTests --filter-method "*PageRequest*"`
- New migration: `dotnet ef migrations add <Name> -p src/Trail.Infrastructure -s src/Trail.Api -o Persistence/Migrations`
- Migration check: `dotnet ef migrations has-pending-model-changes -p src/Trail.Infrastructure -s src/Trail.Api`
- Local DB: `docker compose up -d mysql`; run API: `dotnet run --project src/Trail.Api`

## Layout
- `src/Trail.Domain` – entities, enums, domain rules. No framework dependencies.
- `src/Trail.Application` – use cases (`Features/<Area>/...`), abstractions (`IAppDbContext`, `ICurrentUser`,
  `ITenantContext`), `Result`/`Error`, paging, permissions.
- `src/Trail.Infrastructure` – `TrailDbContext`, entity configurations, migrations, interceptors, external services.
- `src/Trail.Api` – endpoint groups (`Endpoints/<Area>Endpoints.cs`), auth, middleware, `Program.cs`.
- `tests/Trail.UnitTests`, `tests/Trail.IntegrationTests` (real API + real MySQL).

## Conventions (follow these exactly)

### Entities
- Inherit `TenantEntity` for anything owned by an organization, `AuditableEntity` otherwise. Implement
  `ISoftDeletable` only when history must survive deletion (users, sites, templates); otherwise hard delete.
- Ids are `Guid` v7, assigned by the base class. Never set `OrganizationId`, audit fields or `ConcurrencyStamp`
  by hand: the `AuditingInterceptor` does it.
- Add a `DbSet` to both `IAppDbContext` and `TrailDbContext`, an `IEntityTypeConfiguration<T>` in
  `Infrastructure/Persistence/Configurations` (snake_case plural table name, explicit max lengths for every
  string, indexes for every lookup), then a migration.
- Unique indexes on soft-deletable tables do not work in MySQL (NULL `DeletedAt` is not equal to itself);
  enforce those rules in the handler instead.
- Structured values that are never filtered or joined on go in a `json` column with `.HasJsonConversion()`.
  The provider has no `ToJson`/primitive collections. Anything queried gets real columns or a child table.
- Store timestamps as UTC `DateTime` (a converter restores `Kind`). Site-local dates use `DateOnly`/`TimeOnly`
  plus the site's IANA time zone. Use the injected `TimeProvider`, never `DateTime.Now`/`UtcNow`.

### Tenancy (security critical)
- Every query on a tenant entity is filtered to `ITenantContext.OrganizationId` automatically. Never pass an
  organization id from the client; it comes from the token.
- `IgnoreQueryFilters([QueryFilters.SoftDelete])` is fine when needed. Ignoring `QueryFilters.Tenant` is only
  for platform-admin features and needs a comment explaining why.
- Writes outside the active organization throw `TenantIsolationException` (403). Background jobs call
  `ITenantContext.UseOrganization(orgId)` in their own scope per organization.
- An id from the URL that is not visible to the caller returns 404, never 403, so ids cannot be probed.

### Identity and permissions
- Users are global; `Membership` links a user to an organization with a `Role` (a set of permission strings).
  Permission constants live in `Common/Security/Permissions.cs`; each one is also an authorization policy,
  so endpoints use `.RequireAuthorization(Permissions.Sites.Manage)`. Never check role names for access.
- Access tokens (15 min) carry `sub`, `org`, `mid` (membership), `perm` claims; read them via `ICurrentUser`.
  Permission changes apply at the next refresh. Refresh tokens rotate and are single use.
- Anything that grants access (roles, invitations, member changes) must refuse to grant permissions or
  sites the caller does not hold (`MemberRules.CallerHolds`), and members can never change themselves.
- Anonymous endpoints (sign-in flows) go in the `auth` group with the `auth` rate-limit policy. Do not add
  `RequireAuthorization` inside an `AllowAnonymous` group: AllowAnonymous wins.
- Secrets (`Jwt:SigningKey`, SMTP password, bootstrap admin password) come from user secrets or env vars.

### Use cases (Application/Features/<Area>/<UseCase>.cs)
One file per use case containing the request record, its validator and its handler:
```csharp
public sealed record CreateTagCommand(string Name, string? Color);

internal sealed class CreateTagValidator : AbstractValidator<CreateTagCommand> { ... }

public sealed class CreateTagHandler(IAppDbContext db)
{
    public async Task<Result<TagResponse>> Handle(CreateTagCommand command, CancellationToken ct) { ... }
}
```
- Handlers are auto-registered by name (`*Handler`). Return `Result`/`Result<T>`; expected failures are
  `Error.NotFound/Conflict/Validation/Forbidden(code, message)` with codes like `"tag.name_taken"`.
  Throw only for bugs.
- Responses are records named `<Thing>Response` in `Features/<Area>/<Area>Responses.cs`. Never return entities.
- Read queries project with `Select` straight to response records, `AsNoTracking`, and page with
  `ToPagedResultAsync(PageRequest)` after an explicit `OrderBy`.

### Endpoints (Api/Endpoints/<Area>Endpoints.cs)
```csharp
internal sealed class TagEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("tags").WithTags("Tags").RequireAuthorization();
        group.MapPost("", async (CreateTagCommand command, CreateTagHandler handler, CancellationToken ct) =>
                (await handler.Handle(command, ct)).ToHttp(tag => TypedResults.Created($"/api/v1/tags/{tag.Id}", tag)))
            .Validate<CreateTagCommand>()
            .RequireAuthorization(Permissions.Tags.Manage);
    }
}
```
- Routes are plural kebab-case nouns under `/api/v1`. Use `.Validate<T>()` on every body. Every endpoint
  requires authorization unless it is explicitly part of sign-in.
- Lists accept `[AsParameters] PageRequest` and return `PagedResult<T>`.

### Tests
- Integration tests per area in `tests/Trail.IntegrationTests/<Area>/`: create a `TrailApiFactory` per test
  class via `TrailApiFactory.CreateAsync(mysql)` and drive the real HTTP API.
- Helpers: `_factory.CreateOrganizationAsync()` returns an Owner; `AddMemberAsync(orgId, SystemRoles.X, allSites)`
  adds a member; `_factory.CreateClient(member)` gives an authenticated client; `WithDbAsync(orgId, db => ...)`
  for direct setup/asserts; `HttpExtensions` (`PostJsonAsync`, `ReadAsync<T>`, `ShouldBeAsync(status, code)`);
  `_factory.Emails` captures outgoing email (`TokenFromLastEmailTo`).
- Every new tenant-owned endpoint gets a test proving another organization cannot read or change the data.
- Cover validation failures, not-found, permission denied and the happy path. Use
  `TestContext.Current.CancellationToken`.
- Do not weaken or delete existing tests to make a change pass.
