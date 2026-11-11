# Local Docker development

This harness provides a disposable MySQL instance initialized from the existing
schema, plus containerized test and smoke-test runners. It is the first step of
the modernization work: it lets us validate database and calculation parity
without changing the legacy application.

The legacy ASP.NET MVC site is .NET Framework 4.8 and cannot run in a Linux
container. Run it on Windows with IIS or IIS Express and point it at the MySQL
container. The modernized ASP.NET Core host will run in Docker later.

## Prerequisites

- Docker Engine or Docker Desktop with Compose v2+.
- Windows with Visual Studio / IIS or IIS Express for the legacy site.
- No local .NET SDK is required for the containerized test runner.

## Quick start

```bash
cd MyFlightbookWeb
cp .env.example .env
docker compose up -d mysql

# Confirm the schema loaded and the key tables are present.
docker compose --profile test run --rm db-smoke

# Build and start the modern ASP.NET Core host.
docker compose up -d --build modern-web
curl http://localhost:8080/healthz
curl http://localhost:8080/healthz/db

# Run all containerized test projects, including database integration tests.
docker compose --profile test run --rm test-runner
```

Check MySQL or host logs if startup takes longer than expected:

```bash
docker compose logs -f mysql
docker compose logs -f modern-web
```

## Modern ASP.NET Core host

The first host scaffold is `MyFlightbook.Web.Core`, targeting **.NET 10**:

- `GET /` returns a plain-text host identifier.
- `GET /healthz` returns liveness status.
- `GET /healthz/db` runs `SELECT 1` through the existing `DBHelper`.

It is intentionally minimal. It does not serve the legacy MVC UI yet; that
migration comes in later phases.

- Host port: `8080` by default. Override with `WEB_PORT` in `.env`.
- Media: the `media-data` volume is mounted at `/app/images`. It is a placeholder
  for the storage abstraction that will read existing media in place.
- The host starts only after MySQL reports healthy and uses the same existing
  schema; no conversion or transformation is performed.

## MySQL

- Image: `mysql:8.0.46`, matching the `MinimalDB-2026-10-28.sql` dump header.
  Confirm the production version and update the pin only after compatibility
  testing.
- Database: `logbook` by default.
- Port: `3306` on the host by default. Override with `MYSQL_PORT` in `.env`.
- Credentials: see `.env.example`. These are local-development values only.
- Schema initialization: `docker/mysql/init/00-minimaldb.sh` loads the newest
  `MyFlightbook.Web/Support/MinimalDB-*.sql` into `MYSQL_DATABASE` on first boot.
- Server settings: `docker/mysql/conf.d/myflightbook.cnf` sets
  `lower_case_table_names=1`, `max_allowed_packet=16M`,
  `group_concat_max_len=2048`, and `ALLOW_INVALID_DATES`.
- Reset the local database (destructive):

  ```bash
  docker compose down -v
  ```

  The data volume must be recreated for `lower_case_table_names` changes to take
  effect.
- The seeded `localconfig` rows are empty. Populate the keys the legacy site
  needs before signing in or storing media (for example
  `UserPasswordHashKey`, `WebAccessEncryptorKey`, `SharedDataEncryptorKey`,
  `UserAccessEncryptorKey`, `PeerRequestEncryptorKey`, and the image/telemetry
  directory settings). See `README.md` for the key list.

## Legacy Windows site

Create `MyFlightbook.Web/App_Data/connection.config` with a connection string
that points at the Dockerized MySQL instance:

```xml
<?xml version="1.0"?>
<connectionStrings>
  <add name="logbookConnectionString"
       connectionString="server=127.0.0.1;port=3306;User Id=mfb;password=mfb-local-password;database=logbook;CharSet=utf8mb4;Pooling=false"
       providerName="MySql.Data.MySqlClient" />
</connectionStrings>
```

Then run the legacy site as described in `README.md`: create the
`Images/Aircraft`, `Images/BasicMed`, `Images/Endorsements`,
`Images/OfflineEndorsements`, `Images/Flights`, and `Images/Telemetry`
directories, configure the `/logbook` application, and use IIS or IIS Express.

The legacy site is the behavioral reference. Do not modify the shared database
schema to make the modern host work; fix the modern host instead.

### Optional legacy proxy

`docker/legacy-proxy/nginx.conf.example` shows how to put the legacy Windows site
and the modern host behind one origin. It is an example only and is not wired
into Compose. Replace the legacy host/port placeholders with the IIS or IIS
Express address you use locally.

## Containerized tests

The `test-runner` service uses `mcr.microsoft.com/dotnet/sdk:8.0` and mounts the
repository at `/src`. It runs:

- `MyFlightbook.Telemetry.Tests`
- `MyFlightbook.FlightDeckScan.Tests`
- `MyFlightbook.Data.IntegrationTests` (uses `DBHelper` against the Docker MySQL
  instance; skipped when `ConnectionStrings__logbookConnectionString` is unset)

NuGet packages are cached in the `nuget-cache` volume.

The `db-smoke` service verifies that the schema initialized and that the core
tables exist. It is intentionally read-only: it runs `SELECT` statements only.

## Data and media

- Never commit production dumps or user media. `.env`, `local/`, `*.dump`, and
  `*.sql.gz` are ignored.
- Use sanitized production-like dumps for parity testing. Restore them without
  application-level conversion.
- External media will be mounted through a storage abstraction in a later phase.
  The goal is to read existing files in place, not to relocate or re-encode them.

## Common issues

- **Port 3306 already in use:** set `MYSQL_PORT=3307` in `.env` and update the
  legacy connection string.
- **Port 8080 already in use:** set `WEB_PORT=8081` in `.env`.
- **Schema did not load:** initialization only runs on an empty data volume. Run
  `docker compose down -v` and start again.
- **Case-sensitivity errors:** confirm `lower_case_table_names=1` and recreate
  the volume; this setting cannot be changed on an existing data directory.
- **`lower_case_table_names` or collation mismatch:** verify against the
  production MySQL version and collation before comparing behavior.
