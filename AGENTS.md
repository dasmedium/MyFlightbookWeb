# AGENTS.md — MyFlightbookWeb

The MyFlightbook website, SOAP/OAuth API, and shared domain libraries. The iOS app (`ericberman/MyFlightbookiOS`) is a client of this backend. See `docs/modernization/2026-web-modernization-plan.md` before proposing architectural changes.

## Quick reference

- Solution: `MyFlightbook.sln` (17 projects).
- Web app: `MyFlightbook.Web/` — **.NET Framework 4.8**, old-style `.csproj`, `packages.config`, MVC 5 / Razor.
- Domain libraries: `MyFlightbook.*` — mostly **netstandard2.0**, SDK-style projects.
- Tests: `MyFlightbook.Telemetry.Tests/`, `MyFlightbook.FlightDeckScan.Tests/` — **net8.0 MSTest**.
- API entry points:
  - SOAP: `MyFlightbook.Web/Public/WebService.asmx` → `AppCode/WebService.cs`
  - OAuth2: `AppCode/OAuthWebService.cs`, `AppCode/oAuthServices/OAuth2AuthorizationServer.cs`
  - MVC/AJAX: `MyFlightbook.Web/Areas/mvc/Controllers/` (27 controllers)
- Startup/DI: `MyFlightbook.Web/Global.asax.cs` (static service locator).
- Database: MySQL, raw SQL through `MyFlightbookData/DBHelper.cs`. No ORM.
- Secrets/config: `MyFlightbook.Web/App_Data/connection.config`, `email.config` (gitignored), plus the `localconfig` DB table read via `MyFlightbookData/LocalConfig.cs`.
- Data bootstrap: `MyFlightbook.Web/Support/MinimalDB-*.sql`.

## Build and test

```bash
# Domain/unit tests (works on Linux/macOS)
dotnet test MyFlightbook.Telemetry.Tests/MyFlightbook.Telemetry.Tests.csproj
dotnet test MyFlightbook.FlightDeckScan.Tests/MyFlightbook.FlightDeckScan.Tests.csproj

# The web app itself requires Visual Studio / MSBuild on Windows (.NET Framework 4.8).
# Do not assume `dotnet build` works for MyFlightbook.Web.
```

### Required local setup (not committed)

Create `MyFlightbook.Web/App_Data/connection.config` and `email.config`; import `MyFlightbook.Web/Support/MinimalDB-*.sql`; populate `localconfig`.

## Conventions and guardrails

- **No secrets in source.** Keys belong in `localconfig` or the gitignored config files. Never commit connection strings, API keys, or certificates.
- **Preserve aviation calculations.** Flight time, night/instrument, PIC/SIC, approaches, cross-country, currency, and endorsements are critical. Add/keep characterization tests before refactoring them; do not change formulas without an explicit requirement.
- **No WebForms.** There are no `.aspx`/`.ascx`/`.master` files left; legacy URLs are redirects only. Do not plan a WebForms migration.
- **Mobile compatibility matters.** SOAP and OAuth contracts are consumed by shipped iOS/Android apps and third parties. Avoid breaking changes; prefer additive/versioned changes.
- **Raw SQL + MySQL.** Match the existing `DBHelper` pattern; do not introduce EF Core or a new database as part of routine work.
- **Respect the project split.** Put domain logic in the appropriate `MyFlightbook.*` library (netstandard2.0), not in `MyFlightbook.Web`.
- **Match existing style.** Comments are intentional and sometimes blunt. Follow surrounding code conventions. Do not add comments unless asked.
- **Tests before risky refactors.** There is little existing coverage; add tests when touching controllers, SQL, auth, or calculations.
- **Do not commit unless explicitly asked.**

## Hard modernization requirements

- **Existing database compatibility is mandatory.** The current MySQL schema and data model are the canonical compatibility contract. Existing users must be able to use the modernized application without per-user export, import, conversion, or data transformation.
- **Preserve the current schema for initial cutover.** Preserve existing table names, column names, data types, keys, indexes, foreign keys, identifiers, defaults, nullability, character sets, collations, and relationship semantics. Do not drop, rename, remap, normalize, or destructively alter existing data structures.
- **Schema additions must be additive and backward-compatible.** New tables or nullable/defaulted columns require explicit approval and must not prevent the legacy and modernized applications from using the same database during the strangler migration.
- **Support old-app exports.** Users must be able to import logs produced by an export of the existing application. Preserve the current export format and semantics; add compatibility tests using representative legacy exports. This is separate from, and in addition to, allowing the modernized app to use the existing database directly.
- **Preserve external user data.** Images, telemetry, videos, endorsements, documents, and other files stored outside MySQL must remain readable in their existing local or object-storage locations. No mandatory relocation, re-encoding, or restructuring is allowed.
- **Preserve behavior and calculations.** All current functionality and calculations must remain available and behaviorally equivalent. Do not alter formulas, regulatory interpretations, thresholds, defaults, rounding, timezone behavior, or eligibility rules as part of modernization.
- **Regulatory calculation changes are separate work.** Any future change based on an aviation regulation or other rule must be separately documented, reviewed, tested, and approved. It must not be bundled into framework, database, API, or UI modernization.
- **Modernization requires parity evidence.** Before cutover, restore a production-like database dump without application-level conversion, run the modernized app against it, reconcile representative records and external media, and compare legacy versus modern calculation outputs with golden-master fixtures.

## Current modernization direction (summary)

- Incremental/strangler migration to **ASP.NET Core on .NET 10 LTS**; keep the legacy app running behind a reverse proxy.
- Keep **MySQL**, raw SQL, and the modular monolith.
- Reuse the existing `netstandard2.0` libraries.
- Replace **DotNetOpenAuth** with OpenIddict/JwtBearer — the critical-path blocker.
- Keep **SOAP/OAuth contracts working**; proxy or host via SoapCore before replacing.
- Razor first; React/TypeScript islands later. Do not couple a frontend rewrite to the backend migration.
- Add tests, CI, observability, and docs before large structural changes.

Full details and the phase plan: `docs/modernization/2026-web-modernization-plan.md`.

## When working on a task

1. Identify the layer involved (web host, domain library, API, auth).
2. Read `README.md` and the modernization plan if the task touches architecture, auth, API, or deployment.
3. Check whether a build/test command is available in the environment; if not, state what could not be verified.
4. Prefer small, reversible changes. Do not bundle unrelated refactors.
5. If a change affects SOAP/OAuth contracts or aviation calculations, call it out explicitly and add tests.
