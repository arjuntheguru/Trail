# Trail

Backend for Trail, a multi-tenant operations and food-safety compliance platform for hospitality businesses:
scheduled checklists and record logs per site, issues and corrective actions, audit trail and reporting.

## Requirements
- .NET SDK 10.0
- Docker (local MySQL and the integration tests)

## Getting started
```bash
docker compose up -d mysql          # MySQL 8.4 on localhost:3317 (user trail / trail-dev)
dotnet tool restore                 # dotnet-ef
dotnet run --project src/Trail.Api  # applies migrations in Development, docs at /scalar
```

## Tests
```bash
dotnet test --solution Trail.slnx   # integration tests start their own MySQL container
```

## Configuration
Nothing secret is committed. Outside Development, set these through environment variables or user secrets:

| Key | Purpose |
|---|---|
| `Database__ConnectionString` | MySQL connection string |
| `Database__MigrateOnStartup` | `true` to apply migrations on boot (default `false`) |
| `Cors__AllowedOrigins__0` | Browser origin allowed to call the API |

See [CLAUDE.md](CLAUDE.md) for architecture and coding conventions.
