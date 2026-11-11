# MyFlightbook Web Modernization Plan

**Status:** Draft / proposed — Phase 0 in progress (Docker harness and minimal .NET 10 host added)
**Date:** 2026-11-01
**Scope:** `MyFlightbookWeb` (backend + website + mobile API). The iOS app is treated as a compatibility constraint.
**Related repo:** `../MyFlightbookiOS`

This document captures the assessment and proposed direction for modernizing MyFlightbook's web platform. It is intentionally a living document: update the decision log and phase status as work proceeds.

---

## 1. Goals and constraints

### Goals

Modernize the existing platform rather than rewrite it. The project contains years of aviation-specific value that would be expensive and risky to reproduce:

- Flight-time calculations and totals
- Aircraft databases and maintenance tracking
- Endorsements and ratings
- Logbook reporting and currency
- Telemetry/GPS processing
- Mobile and third-party integrations

Five workstreams, in priority order:

1. Modernize the backend and application architecture.
2. Protect data integrity and aviation business logic with tests.
3. Modernize the API and mobile integration architecture.
4. Improve deployment, security, and maintainability.
5. Improve the frontend and add new capabilities (analytics, automation, AI).

### Non-negotiable constraints

- **No simultaneous big-bang rewrite.** Migrate incrementally.
- **Backward compatibility.** Existing iOS/Android clients and third-party OAuth integrations must keep working during and after the migration.
- **Keep the existing MySQL schema initially.** The modernized application must use the existing schema and existing user data without per-user export, import, conversion, or transformation. Do not change database and framework at the same time.
- **Support legacy exports.** Users must also be able to import logbook exports produced by the existing application, preserving the current export format and semantics.
- **Preserve aviation calculations exactly.** Any refactor of time/currency/endorsement logic requires characterization tests first.
- **Preserve all current functionality.** No feature, calculation, regulatory interpretation, threshold, default, rounding behavior, timezone behavior, or eligibility rule may change as part of modernization.
- **Pilot data ownership.** Exports/portability must not regress.

### Guiding principle

> Modernization is not rewriting. A well-executed migration retains practically all existing capabilities while giving the project a maintainable foundation.

---

## 2. Hard compatibility requirements

These are release-blocking requirements, not preferences.

### Existing database and user data

- The current MySQL schema and data model are the canonical compatibility contract.
- The initial modernized application must read existing MyFlightbook databases directly. Existing users must not need to export, import, convert, or transform their data to use it.
- If a database is moved to a new MySQL instance, an unchanged or minimally operational `mysqldump` restore must be sufficient. No application-level data conversion may be required.
- Preserve existing table names, column names, data types, keys, indexes, foreign keys, identifiers, defaults, nullability, character sets, collations, and relationship semantics for the initial cutover.
- No destructive changes are allowed during modernization: no table or column drops, renames, ID remapping, type changes, or normalization of existing data.
- Any new schema elements must be additive, backward-compatible, nullable or safely defaulted, and explicitly approved. The legacy and modernized applications must be able to use the same database during the strangler migration.
- The schema snapshot in `MyFlightbook.Web/Support/MinimalDB-2026-10-28.sql` is a baseline, not a substitute for testing against a sanitized production-like database.

### Legacy logbook exports

- Users must be able to import logs exported by the existing application.
- Preserve the current export format and semantics; do not require users to edit or convert exported files first.
- Add compatibility fixtures covering representative legacy exports, including flights, aircraft, custom properties, endorsements, dates, times, and user-specific settings where those are represented by the export.
- Test both directions independently: direct use of an existing database and import from an old-application export.

### Files and external storage

- Existing images, telemetry, videos, endorsements, documents, and other files stored outside MySQL must remain readable from their current local paths or object-storage locations.
- No mandatory relocation, re-encoding, renaming, or restructuring of external user data is allowed for initial cutover.
- A storage abstraction may be introduced, but it must support the existing storage layout and providers first.

### Functionality and aviation calculations

- All existing functionality must remain available and behaviorally equivalent unless an explicit removal is approved.
- Do not alter formulas, regulatory interpretations, thresholds, defaults, rounding, timezone behavior, or eligibility rules as part of modernization.
- This includes flight time, night, instrument, PIC/SIC, approaches, cross-country, currency, endorsements, ratings, WINGS, BasicMed, and other regulation-dependent behavior.
- Reuse existing domain logic rather than reimplementing it where possible.
- Any future regulatory or formula change must be a separate, documented, reviewed, tested, and approved change. It must not be bundled with framework, database, API, or UI modernization.

### Required parity evidence

Before production cutover, Phase 0-4 must demonstrate:

1. A production-like database dump restores without application-level conversion.
2. The modernized application runs against that restored database.
3. Representative users, flights, aircraft, properties, endorsements, telemetry, images, settings, IDs, and relationships reconcile with the legacy application.
4. Existing external media remains readable in place.
5. Legacy logbook exports import successfully without editing or conversion.
6. Legacy and modern calculation outputs match for the same data and inputs using golden-master fixtures.
7. The legacy and modernized applications can operate against the same database during transition.

---

## 3. Baseline facts (from code inspection)

### Project shape

- **17 projects** in `MyFlightbook.sln`.
- **14 of 16** non-web projects already target **`netstandard2.0`** — roughly **42k LOC** is directly reusable from .NET 10:
  `MyFlightbook.Data`, `CSV`, `SharedUtility`, `Geography`, `Airports`, `Charting`, `Telemetry`, `Image`, `AircraftSupport`, `FlightProperties`, `Payments`, `Currency`, `FlightQueries`, `FlightDeckScan`.
- The web project is old-style **ASP.NET MVC 5 on .NET Framework 4.8** (`MyFlightbook.Web/MyFlightbook.Web.csproj`, `packages.config`).
- Tests are **net8.0 MSTest** (`MyFlightbook.Telemetry.Tests`, `MyFlightbook.FlightDeckScan.Tests`). No CI.

### Web project size

| Area | Size |
|---|---|
| `MyFlightbook.Web` C# | ~174 files / ~116k lines |
| `AppCode/` (domain/business logic) | ~72k lines |
| MVC controllers | ~14.5k lines / 27 controllers |
| Razor views | 295 `.cshtml` |
| Resource files | 25 `.resx` in `App_GlobalResources/` |

### Coupling to legacy ASP.NET

- **66 files** reference `System.Web`.
- **33 files** use `System.Web.Mvc` (only one in `AppCode`: `AppCode/Utility/ValidateHeaderAntiForgeryTokenAttribute.cs`).
- **24** uses of `HttpContext.Current`.
- **0** WebForms files (`runat="server"`); old `.aspx` URLs are redirect rules only.
- **1** ASMX service: `Public/WebService.asmx` → `AppCode/WebService.cs`.
- **403** `Html.RenderAction` calls across **137** views, plus **16** `[ChildActionOnly]` actions.
- **50** uses of `HttpPostedFileBase`/`Request.Files`.
- ~66 path-mapping/`ToAbsolute()` usages.
- Startup is a single static service-locator in `Global.asax.cs` (`DBHelper.Init`, `util.Init`, `Geography.Init`, `MFBImageInfoBase.SetStorage`, etc.).

### Corrected assumptions

- The README says "ASP.NET 4.5"; the project actually targets **4.8**.
- The "remove remaining `.aspx` files" issue is effectively **already solved** — no WebForms remain. Do not plan a WebForms migration.
- The backend is more modern than it appears because the domain libraries are already `netstandard2.0`.

---

## 4. Critical blockers (ordered by risk)

### 3.1 DotNetOpenAuth — the critical path

- Package: `DotNetOpenAuth 4.3.4`, abandoned and **.NET Framework-only**.
- Used in **17 files / ~11.3k lines**, including:
  - `AppCode/oAuthServices/OAuth2AuthorizationServer.cs` (custom authorization server)
  - `AppCode/OAuthWebService.cs` (resource server)
  - OAuth1/OAuth2 clients (`CloudAhoyClient`, `CloudStorage`, `FlightCrewViewClient`, `LeonClient`, `MyTailLogClient`, `RosterBusterClient`, `TachTimeClient`, `ExternalMaintenanceClient`)
  - `AppCode/UserAccounts/Profile.cs`, `AppCode/WebService.cs`
- Replacement: **OpenIddict** for the authorization server + `Microsoft.AspNetCore.Authentication.JwtBearer` for resource validation, or a custom token handler.
- Preserve client IDs, secrets, scopes, endpoints, and grant semantics so mobile apps and third-party integrations do not break.
- **Start a spike in Phase 0.** This is the highest-schedule-risk item.

### 3.2 ASMX SOAP API

- `Public/WebService.asmx` exposes the contract consumed by generated iOS/Android clients (`WebServiceCode/MFBWebServiceSvc.*`).
- ASP.NET Core has **no ASMX server support**.
- Options, in order of preference:
  1. **Proxy** `/public/WebService.asmx` to the legacy app during transition (lowest risk).
  2. **SoapCore** (community) to host the existing contract on Core — validate WSDL against generated clients.
  3. **Versioned REST/JSON API** alongside SOAP; update mobile clients over time.
- Do not remove SOAP until both mobile apps have a tested replacement.

### 3.3 Membership / Roles / Forms auth

- Custom providers: `AppCode/UserAccounts/MySQLMembershipProvider.cs` (1,734 lines), `MySQLRoleProvider.cs` (782 lines), `Profile.cs` (2,610 lines).
- ~207 `FormsAuthentication`/`Membership`/`Roles` references; ~5k lines total.
- No .NET Core equivalents for `System.Web.Security`.
- Approach: ASP.NET Core cookie authentication + an adapter over the existing tables. **Preserve the password hash format** (`UserPasswordHashKey`).
- Forms-auth cookie compatibility across a side-by-side deployment is a known hard point: either establish a single auth authority, or force a one-time re-login at cutover.

### 3.4 Child actions

- 403 `Html.RenderAction` calls in 137 views; 16 `[ChildActionOnly]` actions.
- Removed in ASP.NET Core MVC.
- Convert to **ViewComponents** (or partials where appropriate). This is the largest mechanical view-layer task.

### 3.5 Secondary migration items

- `HttpPostedFileBase`/`Request.Files` → `IFormFile`.
- `HttpContext.Current` → `IHttpContextAccessor`; `VirtualPathUtility`/`Server.MapPath`/`HostingEnvironment.MapPath` → `IWebHostEnvironment`/`PathBase`.
- `App_GlobalResources/` is not a supported special folder in Core → move `.resx` into the project as embedded resources and regenerate designers.
- `SecuritySwitch` IIS module is dead → replace with middleware or reverse-proxy rules.
- Web.config modules/handlers, `<location>` blocks, custom headers, `httpErrors`, and legacy redirect rules → middleware/Core config/proxy.
- `System.Drawing.Common` is Windows-only on modern .NET. It is used in `MyFlightbook.Image` (netstandard) and a few web files → migrate to **Magick.NET** (already referenced) or SkiaSharp before Linux hosting.
- Linux path-case sensitivity and MySQL collation are deployment risks. (`App_Data/connection.config`, case-sensitive file paths, `lower_case_table_names`.)

---

## 5. Target architecture

**Modular monolith, not microservices.** The existing project split already provides module boundaries.

| Layer | Target |
|---|---|
| Web host | ASP.NET Core MVC on **.NET 10 LTS**, Razor views retained initially |
| Domain | Existing netstandard libraries, retargeted `netstandard2.0;net10.0` |
| Architecture | Modular monolith (one deployable, module boundaries per project) |
| API | Versioned REST + OpenAPI (new) **alongside** proxied SOAP initially |
| Auth | ASP.NET Core cookie auth + adapter over existing membership tables |
| OAuth | OpenIddict + JwtBearer |
| Database | Existing MySQL schema retained as the compatibility baseline; raw SQL retained initially; additive migrations only |
| File storage | Storage abstraction: local in dev, S3-compatible in production |
| Background jobs | .NET Worker Services (nightly stats/email, telemetry, media, S3) |
| Frontend | Razor first; React/TypeScript “islands” for app-like workflows later |
| Reverse proxy | Caddy, Traefik, nginx, or IIS ARR |
| Deployment | Docker + Linux |
| CI/CD | GitHub Actions |
| Testing | MSTest (existing) + integration/E2E/contract tests |
| Observability | OpenTelemetry + Sentry (or equivalent) |
| Docs | `docs/` architecture, development, modernization, operations |

### Explicitly avoided for now

- Microservices
- Kubernetes (without demonstrated need)
- Replacing MySQL without a compelling reason
- EF Core as part of the first migration (raw SQL works; introduce patterns gradually)
- Full React SPA rewrite coupled to the backend migration
- Multiple backend languages
- Letting AI-generated code change aviation calculations without regression tests

---

## 6. Phased roadmap

### Phase 0 — Assessment and safety net (2–4 weeks)

**Start here.**

- Inventory all projects, dependencies, and obsolete libraries.
- Catalog the **27 MVC controllers**, ASMX methods, OAuth endpoints/scopes, DB tables, export formats, and `localconfig` keys.
- Capture the existing database schema, indexes, foreign keys, collations, external storage layout, and a sanitized production-like database fixture.
- Define record-level reconciliation checks for users, flights, aircraft, custom properties, endorsements, telemetry, images, settings, IDs, and relationships.
- Stand up the local Docker harness (MySQL plus containerized tests and smoke tests) so changes can be validated as they are made. See `docs/development/local-docker.md`.
- Establish CI (GitHub Actions): test projects on Linux; web project build on a Windows runner.
- Build **characterization tests** for critical aviation logic before touching it:
  - Flight-time totals, night/instrument time, PIC/SIC, approaches, cross-country
  - Currency and WINGS
  - Endorsements and ratings
  - Historical golden datasets for regression
- Add legacy logbook export fixtures and import compatibility tests.
- Baseline performance for large logbooks and telemetry processing.
- Security/dependency scan and key-management review.
- **Spikes:** DotNetOpenAuth → OpenIddict; SoapCore WSDL compatibility; cookie-auth compatibility.
- Write ADRs; create the `docs/` skeleton.
- **Exit criteria:** repeatable build/test, documented inventory, database/storage baseline captured, legacy export fixtures defined, spikes completed, no production behavior changed.

### Phase 1 — Core foundation (4–6 weeks)

- Create the new ASP.NET Core host targeting .NET 10.
- Reference the existing `netstandard2.0` libraries (multi-target where needed).
- Initial `MyFlightbook.Web.Core` scaffold runs in Docker, exposes `/healthz`
  and `/healthz/db`, and validates `DBHelper` connectivity against the existing
  schema. `MyFlightbook.Data.IntegrationTests` covers the same path in CI/local
  test runs.
- Replace `Global.asax.cs` startup with DI/`Program.cs`; add config/env, Serilog, static files, routing, error handling, anti-forgery.
- Implement cookie auth + membership adapter over the existing tables/hash format without changing the existing user data schema.
- Add a reverse proxy in front (YARP or infrastructure layer); move a small set of low-risk public/read-only pages to Core.
- **Exit criteria:** Core serves pilot pages in staging; auth works end-to-end; existing database and external storage work without conversion; legacy app untouched.

### Phase 2 — MVC surface migration (2–4 months)

- Port area-by-area, in this order:
  1. Public/read-only pages
  2. Authenticated read pages
  3. Write-heavy pages (`FlightEdit`, `Flights`, `Aircraft`, `Prefs`, `Image`)
- Convert child actions to ViewComponents/partials.
- Replace `HttpContext.Current`, path utilities, file uploads, and `App_GlobalResources`.
- Replace `System.Drawing.Common` with Magick.NET/SkiaSharp.
- **Exit criteria:** all website pages on Core in staging; legacy serves only SOAP/OAuth; existing database and media remain usable; legacy exports import successfully.

### Phase 3 — OAuth and API (1–3 months)

- Replace DotNetOpenAuth with OpenIddict + JwtBearer, preserving client IDs, scopes, endpoints, and token/grant behavior.
- Contract-test iOS/Android and third-party integrations.
- SOAP: validate SoapCore against generated clients; if incompatible, keep the legacy sidecar behind the proxy and introduce versioned REST v1 alongside SOAP.
- **Exit criteria:** mobile apps and integrations pass contract tests; no DotNetOpenAuth in production.

### Phase 4 — Infrastructure and cutover (1–2 months)

- Docker/Linux, GitHub Actions deploy pipelines, secrets management.
- Health checks, structured logging, OpenTelemetry/Sentry.
- Automated DB backups with tested restores; staged environments; blue-green and rollback.
- Linux audit: path case, MySQL collation, WKHtmlToPdf fonts, ImageMagick native libs, S3.
- **Exit criteria:** production on Linux/Core; legacy decommissioned or reduced to a temporary sidecar; no user data conversion was required.

### Phase 5 — Frontend and new capabilities (ongoing)

- Introduce a design system; migrate high-value workflows (dashboard, flight edit, analytics) to React/TS islands.
- Keep Razor for public/SEO pages unless there is a clear reason to change.
- Add webhooks (flight created/updated, aircraft updated, totals changed, endorsement created).
- Explore AI-assisted entry (pilot verification required), richer telemetry, instructor/student workflows.

---

## 7. Effort and critical path

Single experienced developer, focused:

| Workstream | Estimate |
|---|---|
| Phase 0 — tests/CI/inventory/spikes | 2–4 weeks |
| Phase 1 — Core foundation | 4–6 weeks |
| Phase 2 — Controllers/views/child actions/uploads | 2–4 months |
| Phase 3 — OAuth replacement + API contract | 1–3 months |
| Phase 4 — Infrastructure/cutover | 1–2 months |
| **Total** | **~6–12 months** |

With 2–3 contributors: roughly **3–5 months**.

**Critical path:** Phase 0 characterization tests → Phase 3 OAuth replacement. The view port is large but mechanical; OAuth and SOAP compatibility carry the schedule risk.

---

## 8. Testing, security, and data integrity

### Testing strategy

| Area | Recommendation |
|---|---|
| Unit | Flight calculations, totals, currency, endorsements |
| Regression | Known-good historical cases for night/instrument/PIC/SIC/XC |
| Import compatibility | Legacy logbook export fixtures, including representative user data |
| Data compatibility | Restored database snapshots, record-level reconciliation, schema diff checks |
| Integration | API, database, authentication |
| Contract | Existing SOAP/OAuth mobile compatibility |
| End-to-end | Flight creation, editing, reporting |
| Security | AuthN/AuthZ, file access, rate limiting |
| Performance | Large logbooks, telemetry processing |

Existing tests are MSTest; keep MSTest to avoid churn.

### Security review

- Password hashing and legacy hash compatibility.
- OAuth token storage, refresh/rotation, revocation.
- Encryption key management (`UserPasswordHashKey`, `WebAccessEncryptorKey`, etc.).
- Uploaded document access controls.
- Rate limiting and audit logging.
- Dependency lifecycle and vulnerability scanning.

### Database

- Keep the existing MySQL schema as the compatibility baseline. Introduce migration tooling only for explicitly approved, additive, backward-compatible changes.
- Do not require a per-user database conversion or import during cutover.
- Document relationships/constraints; review indexes and query performance.
- Separate business logic from SQL incrementally.
- Invest in CSV/JSON export, import validation, and documented API access — pilot data ownership is a core product promise.
- Preserve and test the existing logbook export format so users can import data from the old application.
- Treat external media storage as part of the user-data contract, not as disposable deployment state.

---

## 9. Solo OSS vs. platform

Recommended stance: **contributor-ready but solo-operated.**

- Do Phases 0–2 the same way regardless.
- Both paths need: modular monolith, tests, CI, ADRs, `docs/`, coding standards, dev container.
- **Solo:** optimize for low operational overhead (single container, managed MySQL/object storage, minimal infra).
- **Platform:** invest earlier in API-first (REST/OpenAPI + SDKs), webhooks, stable domain contracts, design system, governance.
- Revisit the decision at the **Phase 2/3 boundary**, once the foundation is stable. Do not add platform complexity before external contributors or partners exist.

---

## 10. Decision log

| Date | Decision | Rationale |
|---|---|---|
| 2026-11-01 | Modernize, do not rewrite | Preserve accumulated aviation domain value |
| 2026-11-01 | Incremental/strangler migration | Mobile SOAP/OAuth clients cannot break |
| 2026-11-01 | Modular monolith | Module boundaries exist; avoid distributed complexity |
| 2026-11-01 | Keep MySQL | Avoid simultaneous DB + framework risk |
| 2026-11-01 | ASP.NET Core on .NET 10 LTS | Modern host, long support window |
| 2026-11-01 | Razor first, React later | Avoid coupling backend and UI rewrites |
| 2026-11-01 | OpenIddict replaces DotNetOpenAuth | DotNetOpenAuth is abandoned/.NET Framework-only |
| 2026-11-01 | Proxy SOAP until replaced | Lowest-risk compatibility path |
| 2026-11-07 | Existing MySQL schema/data are a release-blocking compatibility contract | Users must not need to convert or re-import current data to use the modernized application |
| 2026-11-07 | Preserve and import legacy logbook exports | Users must be able to restore logs from old-application exports without editing or conversion |
| 2026-11-07 | Existing calculations and functionality are parity requirements | Modernization must not alter aviation or regulation-dependent behavior |
| 2026-11-09 | Local development uses Docker MySQL plus containerized tests; legacy site runs on Windows/IIS | Enables ongoing validation without changing the legacy runtime or schema |
| 2026-11-09 | Minimal .NET 10 host runs in Docker and validates `DBHelper` against the existing schema | Proves the modern runtime can use the unchanged database before UI migration |

---

## 11. Immediate next steps (Phase 0 task list)

1. Create `docs/` skeleton (architecture, development, modernization, operations).
2. Add GitHub Actions: build/test the two MSTest projects on Linux; build `MyFlightbook.Web` on Windows.
3. Produce an endpoint and export catalog: MVC actions, ASMX methods, OAuth endpoints/scopes, and current logbook export formats.
4. Capture the current schema, indexes, foreign keys, collations, external storage layout, and a sanitized production-like database fixture.
5. Define record-level reconciliation and schema-diff checks for the compatibility baseline.
6. Produce a dependency and risk register (mark DotNetOpenAuth, SecuritySwitch, System.Drawing as blockers).
7. Capture golden datasets for flight-time/currency/endorsement calculations.
8. Capture representative legacy logbook export fixtures and add import compatibility tests.
9. Spike OpenIddict token/client compatibility.
10. Spike SoapCore WSDL compatibility against the generated Objective-C client.
11. Document the `localconfig` key inventory and secret-handling plan.
12. Decide the auth cutover strategy (single authority vs. forced re-login).

---

## 12. References

- Web setup and configuration: `README.md`
- Project and dependency layout: `MyFlightbook.sln`, `MyFlightbook.Web/packages.config`
- OAuth server: `MyFlightbook.Web/AppCode/oAuthServices/OAuth2AuthorizationServer.cs`
- SOAP service: `MyFlightbook.Web/Public/WebService.asmx`, `MyFlightbook.Web/AppCode/WebService.cs`
- Membership providers: `MyFlightbook.Web/AppCode/UserAccounts/MySQLMembershipProvider.cs`, `MySQLRoleProvider.cs`
- Startup/locator: `MyFlightbook.Web/Global.asax.cs`
- Existing tests: `MyFlightbook.Telemetry.Tests`, `MyFlightbook.FlightDeckScan.Tests`
- iOS compatibility constraint: `../MyFlightbookiOS/ReadMe.md`, `../MyFlightbookiOS/WebServiceCode/`
