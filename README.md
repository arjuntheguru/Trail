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

## Deployment
The API ships as a container (`Dockerfile`, runs as non-root on port 8080, `Production` environment).
```bash
docker build -t trail-api .                          # API image
docker build --target migrator -t trail-migrator .   # EF Core migrations bundle
docker run --rm -e Database__ConnectionString="..." trail-migrator
```
Run the migrator once before rolling out new replicas, and leave `Database__MigrateOnStartup` off in production.
Probe `/health/live` (liveness) and `/health/ready` (readiness); the image has no curl/wget, so use an orchestrator probe.

Required settings (environment variables):

| Key | Purpose |
|---|---|
| `Database__ConnectionString` | MySQL connection string |
| `Jwt__SigningKey` | HMAC key, at least 32 characters, from a secret store |
| `App__FrontendBaseUrl` | Public frontend URL used in email links |
| `Email__Provider` | `Smtp` to send mail (also `Email__SmtpHost`, `Email__SmtpPort`, `Email__SmtpUser`, `Email__SmtpPassword`, `Email__FromAddress`) |
| `Cors__AllowedOrigins__0` | Browser origin allowed to call the API |
| `Storage__LocalPath` | Upload directory (default in image `/app/App_Data/uploads`); must be a shared volume when running several replicas |
| `Bootstrap__PlatformAdminEmail`, `Bootstrap__PlatformAdminPassword` | Optional: create the first platform admin at startup (password at least 12 characters) |

Outside Development/Testing the API refuses to start when `Email__Provider` is not `Smtp` or `App__FrontendBaseUrl` is
not a public https URL. SMTP with credentials always uses TLS (`Email__SmtpSecurity`: `StartTls` or `SslOnConnect`).

Optional settings:

| Key | Default | Purpose |
|---|---|---|
| `Http__HttpsRedirection` | `true` | Set `false` when TLS terminates at the proxy |
| `Http__KnownProxies__0`, `Http__KnownNetworks__0` | loopback | Trusted reverse proxies (IPs / CIDRs) for `X-Forwarded-*` |
| `Http__ForwardLimit` | `1` | Proxies in the chain to trust |
| `Http__MaxRequestBodyBytes` | `1048576` | Global request body cap (attachment uploads allow 11 MB) |
| `Database__CommandTimeoutSeconds` | `30` | EF Core command timeout |
| `Database__MigrationLockWaitSeconds` | `300` | How long a replica waits for another one's migration |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | unset | OTLP gRPC endpoint; traces and metrics are exported only when set |

Rate limits are counted per instance. Background jobs are safe to run on every replica: task generation and the
daily digest take a MySQL `GET_LOCK` per tick, the others claim rows.

For a local containerised run: `docker compose --profile app up -d --build` (API on :8080, Mailpit UI on :8025).

See [CLAUDE.md](CLAUDE.md) for architecture and coding conventions.
