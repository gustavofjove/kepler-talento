# Kepler Talento Stack Blueprint

> Target architecture for the internal Kepler Talento CV-management application:
> a .NET 10 vertical-slice API over PostgreSQL, paired with the existing React 19 SPA.

This document adapts the useful architectural decisions from the supplied stack blueprint
to Kepler Talento. It is a target-state architecture, not a description of the repository
as it works today. The current SPA still persists most operational data in browser
`localStorage`, while Supabase schemas and functions exist but are not wired as the
runtime backend.

The target is a two-application monorepo:

- an ASP.NET Core JSON API owns business rules, persistence, files, import/export,
  auditing, and authorization enforcement;
- the existing React SPA consumes that API and remains independently buildable;
- an application-owned PostgreSQL instance and private file storage run entirely on the
  company intranet;
- authentication/login integration is intentionally deferred and is not selected here.

The backend combines two complementary ideas:

- **Vertical Slice Architecture (VSA)** governs how use cases are organized. A use case
  owns its request, response, validation, handler, and tests.
- **Dependency inversion** governs project references. Domain and application code do not
  depend on PostgreSQL, the filesystem, a reverse proxy, or any future identity provider.

This structure is justified for Kepler Talento because the application has more than
simple CRUD: compound candidate search, private CV handling, logical deletion, catalog
governance, imports, exports, background operations, and personal-data auditing.

---

## 1. Scope and non-goals

### In scope

- .NET 10 / ASP.NET Core API.
- VSA use-case organization with explicit dependency boundaries.
- EF Core code-first persistence against PostgreSQL.
- Semantic database table prefixes tailored to the candidate-management domain.
- Private storage and controlled retrieval of CVs and related candidate documents.
- Controlled CSV export and CSV/Access-derived import.
- Auditing, validation, error contracts, observability, tests, and intranet deployment.
- Incremental replacement of browser persistence and Supabase-specific backend code.

### Deferred or excluded

- The login/authentication mechanism, credential storage, token format, session renewal,
  SSO provider, and identity lifecycle. No JWT, BCrypt, refresh-token, Active Directory,
  or Entra ID choice is made by this document.
- Azure hosting or Azure services. The application has no Azure Blob Storage, Azure
  Communication Services, App Service, or scale-to-zero dependency.
- PDF or office-document generation. CV PDFs are stored and retrieved, not generated.
- A live Microsoft Access dependency. Access remains a migration/reference source only.
- Replacing the existing React design system, CSS, signal services, or form conventions.

Production rollout still requires an approved authentication integration. Deferring that
decision must not result in a production API with anonymous access or a temporary login
scheme that silently becomes permanent.

---

## 2. Repository layout

Keep the React application at the repository root initially to avoid a large, low-value
move. Add the backend as an independently buildable subtree.

```text
/
|-- backend/
|   |-- Directory.Packages.props
|   |-- KeplerTalento.slnx
|   |-- Domain/
|   |   |-- Entities/
|   |   |-- ValueObjects/
|   |   |-- Enums/
|   |   `-- Domain.csproj
|   |-- Application/
|   |   |-- Features/
|   |   |-- Interfaces/
|   |   |-- Behaviors/
|   |   |-- Common/
|   |   |-- DependencyInjection.cs
|   |   `-- Application.csproj
|   |-- Infrastructure/
|   |   |-- Data/
|   |   |   |-- Configurations/
|   |   |   |-- Migrations/
|   |   |   `-- Seed/
|   |   |-- Storage/
|   |   |-- Operations/
|   |   |-- Auditing/
|   |   |-- Identity/             # adapter only after identity is decided
|   |   |-- DependencyInjection.cs
|   |   `-- Infrastructure.csproj
|   |-- Web/
|   |   |-- Features/
|   |   |-- Middleware/
|   |   |-- Security/             # policies/integration after auth decision
|   |   |-- Resources/
|   |   |-- Program.cs
|   |   `-- Web.csproj
|   `-- Tests/
|       |-- UnitTests/
|       `-- IntegrationTests/
|-- src/app/                      # existing React application
|-- tests/                        # existing Vitest/Playwright suites
|-- openspec/
`-- specs/
```

Use NuGet central package management through `Directory.Packages.props`; project files
contain package references without individual versions. Add a `global.json` so local,
CI, and intranet build environments use the same .NET SDK feature band.

The existing `supabase/` directory remains migration evidence until the .NET/PostgreSQL
replacement is verified. Retiring it is a later, explicit migration task, not an
incidental deletion during scaffolding.

---

## 3. Backend dependency boundaries

Production project references point inward:

```text
Web --------------------> Application --------------------> Domain
 |                             ^
 `------> Infrastructure ------'

Infrastructure ------------------------------------------> Domain
```

- **Domain** contains candidate entities, value objects, invariants, and domain events.
  It has no ASP.NET Core, EF Core, storage, or identity-provider dependency.
- **Application** contains vertical slices, ports, validation, and orchestration. It
  references Domain but not Infrastructure or Web.
- **Infrastructure** implements persistence, file storage, background operations,
  auditing, and later identity adapters.
- **Web** is the composition root and HTTP boundary. Endpoints translate HTTP concerns,
  obtain the current actor through an abstraction, dispatch a use case, and map the
  result. They do not contain business rules.

Infrastructure is registered by Web but is never referenced by name from Application.
Tests may reference the projects needed for their scope.

---

## 4. Vertical slice pattern

Organize application code by business use case, not by technical service type.

```text
Application/Features/
|-- Candidates/
|   |-- CreateCandidate.cs
|   |-- UpdateCandidate.cs
|   |-- DeactivateCandidate.cs
|   |-- GetCandidate.cs
|   `-- ListCandidates.cs
|-- CandidateDocuments/
|   |-- UploadCandidateCv.cs
|   |-- SetPrimaryCandidateCv.cs
|   `-- DownloadCandidateCv.cs
|-- CandidateSearch/
|   |-- SearchCandidates.cs
|   `-- SaveSearchPreset.cs
|-- Exports/
|   |-- RequestCandidateExport.cs
|   |-- GetExportStatus.cs
|   `-- DownloadCandidateExport.cs
`-- Imports/
    |-- ValidateCandidateImport.cs
    `-- CommitCandidateImport.cs
```

Each slice normally owns nested or co-located types for:

- `Command` or `Query`;
- `Response`;
- FluentValidation validator;
- MediatR handler;
- stable business/error codes;
- unit tests and relevant integration tests.

DTOs belong to their slice. Similar DTOs may remain duplicated until a stable shared
contract appears in at least three use cases. Domain entities are not returned directly
from API endpoints.

Use EF Core queries directly through an application persistence abstraction; do not add
a generic repository over `DbSet`. Reusable complex search predicates may be expressed
as named query specifications or `IQueryable` extensions owned by the relevant feature.

---

## 5. Request pipeline and API contract

The common pipeline keeps handlers focused on the use case.

| Mechanism           | Target behavior                                                                                                                         |
| ------------------- | --------------------------------------------------------------------------------------------------------------------------------------- |
| Validation behavior | Runs all FluentValidation validators before the handler and returns stable per-rule codes.                                              |
| Problem details     | Uses RFC 9457-style problem details for errors, including a stable `code` and correlation identifier.                                   |
| Success responses   | Returns the slice response directly unless pagination or operation metadata requires a documented envelope.                             |
| Localization        | User-facing API messages are Spanish first. Add `Accept-Language` and resource files only when a second language is a real requirement. |
| Observability       | Correlates HTTP requests, audit events, background jobs, and failures with a request/operation identifier.                              |

Avoid wrapping every successful response in `{ data, message }` without a consumer need;
HTTP status codes and typed response bodies already carry most of that information.
Paged endpoints use one consistent pagination contract.

Validation covers request shape. Domain invariants remain enforced by the domain model,
and database constraints protect invariants that must survive every code path.

---

## 6. PostgreSQL persistence and table prefixes

Use EF Core code-first with the Npgsql provider. Application code depends on an
`IApplicationDbContext`-style port or focused persistence ports; the concrete
`ApplicationDbContext` remains in Infrastructure.

Every entity has an explicit `IEntityTypeConfiguration<T>` with an explicit table name,
keys, constraints, indexes, precision, and delete behavior. Do not rely on convention
names for physical tables.

Database tables use physical uppercase semantic prefixes as requested. Because
PostgreSQL folds unquoted identifiers to lowercase, EF Core mappings and all handwritten
SQL must quote these case-sensitive names exactly.

| Prefix | Holds                                                                    | PostgreSQL examples                                      |
| ------ | ------------------------------------------------------------------------ | -------------------------------------------------------- |
| `CND_` | Candidate aggregate and profile data                                     | `"CND_Candidates"`, `"CND_Languages"`, `"CND_Documents"` |
| `CAT_` | Business-maintained catalogs                                             | `"CAT_CandidateStatuses"`, `"CAT_Languages"`             |
| `OPS_` | Imports, exports, and durable background operations                      | `"OPS_ImportBatches"`, `"OPS_ExportJobs"`                |
| `AUD_` | Append-only audit records                                                | `"AUD_Events"`                                           |
| `ADM_` | Administration/authorization data, reserved until that model is approved | `"ADM_Roles"`, `"ADM_Permissions"`                       |

Prefixes affect physical database objects only. C# entity names, namespaces, and
`DbSet` properties remain clean (`Candidate`, not `CndCandidate`). Join tables stay with
the aggregate they describe, for example `"CND_CandidateSkills"`.

Key persistence rules:

- use UUID identifiers unless a measured database concern requires otherwise;
- use `timestamptz`/UTC instants for timestamps and `date` for date-only business data;
- preserve logical deletion for candidates and governed catalog values;
- enforce the single-primary-CV rule with a partial unique index;
- keep search semantics in PostgreSQL and verify indexes with representative query plans;
- store audit before/after data selectively so sensitive data is not duplicated without
  a support or compliance need;
- make seed operations idempotent.

Do not run production migrations implicitly on every API startup. Development and test
may auto-migrate; intranet releases apply reviewed migrations as an explicit deployment
step with backup, rollback, and compatibility checks.

The existing Supabase SQL is a source for schema intent and migration data, not the
target migration mechanism. New target migrations are EF Core migrations and must not
depend on Supabase schemas, RLS helpers, Edge Functions, or Storage tables.

---

## 7. Authentication and authorization boundary

Authentication/login is deliberately unresolved. This blueprint makes no choice about:

- where users authenticate;
- cookies versus bearer tokens;
- Windows Integrated Authentication, LDAP, Active Directory, or another provider;
- password hashing, MFA, refresh tokens, or logout behavior.

The backend still needs a seam for the eventual decision:

- handlers receive actor identity from a server-owned `ICurrentActor` abstraction, never
  from a request-body `userId`;
- audit fields use the resolved actor identifier and preserve the external identity key
  without assuming its format is a UUID;
- endpoints have an explicit authorization declaration before production rollout;
- application authorization is permission-based independently of the eventual login
  provider and uses capability codes such as `candidates.view`, `candidates.edit`,
  `candidate_documents.download`, and `candidate_exports.create` instead of hard-coded
  role-name checks.

An endpoint-authorization inventory test should be added once the authentication and
authorization integration is designed. Until then, test actors may exercise use cases in
automated tests, but a development actor must be impossible to enable in production.

---

## 8. Private candidate-document storage

Candidate-document binaries do not belong in PostgreSQL and must never be served from
the SPA's static files or a public directory. The database stores document metadata; an
`IFileStorage` port owns binary storage.

With only a few users, hundreds of documents, and one initial API instance, the initial
adapter is a filesystem implementation backed by a dedicated host directory mounted into
the API container. The directory is outside the container's writable layer and outside
the webroot. It is backed up with PostgreSQL. `IFileStorage` preserves the option to move
to SMB or another private storage adapter later without changing use-case handlers.

The storage root is configuration outside the database. Rows store only an opaque,
relative object key such as:

```text
candidates/{candidate-id}/{document-id}/content
```

Never store or return drive letters, UNC paths, mount points, hostnames, or permanent
download URLs. The original filename is metadata only and is not used as a filesystem
path.

### Upload flow

1. Receive a streamed multipart upload with a configurable 20 MB per-document limit.
2. Check the extension against an explicit business allowlist. Do not accept arbitrary
   "other" formats or rely on a denylist.
3. Validate declared type, detected content/signature, non-empty content, and sanitized
   display filename. The client-supplied MIME type is only a hint.
4. Calculate a cryptographic hash and write the file under an application-generated key
   in a non-public quarantine directory.
5. Scan the quarantined file automatically through `IFileScanner`.
6. Atomically promote only a clean file to the final key, then commit metadata and an
   audit event. Infected, unscannable, or scan-timeout files remain unavailable and are
   rejected or retained in quarantine according to the operational policy.
7. Compensate by deleting staged/final objects if the database transaction fails.

Candidate documents may originate outside the company and are therefore untrusted even
when an HR employee performs the upload. The initial broad allowlist covers common CV
formats whose residual risk is acceptable behind quarantine and automated scanning:

- documents: PDF, DOC, DOCX, ODT, RTF, and plain text;
- raster images: JPEG, PNG, TIFF, and BMP.

The allowlist is configuration-backed but may be expanded only through a reviewed change
that defines content detection, scanning behavior, download headers, and resource limits
for the new format. "Allow all files" is not a valid configuration. Executables, scripts,
HTML, SVG, archives submitted as CVs, password-protected/encrypted documents that cannot
be scanned, and macro-enabled Office formats such as DOCM are denied. Legacy DOC and
container formats such as DOCX and ODT remain quarantined until scanning completes.

The API, reverse proxy, and scanner use compatible limits. Scanner configuration bounds
expanded content size, archive/container recursion, file count, and scan duration so a
small compressed document cannot cause disproportionate CPU, memory, or disk use.

The initial `IFileScanner` adapter is a ClamAV `clamd` sidecar on the private Docker
network, using streamed scanning so the scanner does not need the application's storage
path. Its signature database is persisted and updated automatically. The scanner port is
not published outside the private container network. A scanner outage blocks new files
from becoming available but does not block downloads of previously clean files.

The host must budget the scanner's documented memory requirement (approximately 3 GiB
minimum and 4 GiB preferred). If corporate IT later provides a scanner, only the adapter
changes. This defense-in-depth flow follows the
[OWASP File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html)
and the [official ClamAV Docker guidance](https://docs.clamav.net/manual/Installing/Docker.html).

### Download flow

The browser requests an API endpoint. After the future identity/permission boundary
allows access, the API streams a clean file with `Content-Type`, a safe
`Content-Disposition`, `nosniff`, and no-store caching. Direct share access and raw paths
are never exposed to the browser. Non-PDF documents default to controlled download; the
application does not execute Office conversion or server-side previewing unless that is
planned as a separate feature.

Deleting a candidate remains logical. File retention/deletion follows the approved HR
retention policy; a background sweeper may remove only objects whose metadata is in an
explicit purgeable state. Missing objects, orphaned objects, malware detections, scan
errors, and stale quarantined files are detected, audited, and reported without exposing
the file contents in logs.

Database and file backups form one recovery unit. Restore tests must prove that metadata
still resolves to the expected file keys and hashes.

---

## 9. CSV export and controlled import

CSV generation moves from the browser to the API so permissions, field minimization,
auditing, limits, and consistent formatting cannot be bypassed.

Every export creates an `"OPS_ExportJobs"` record containing requester, normalized
search filters, approved field set, format, status, timestamps, row count, expiry, and
failure code. Small exports may complete during the request; larger exports use the same
record and run in a background worker.

The initial default field set remains:

- first name and surname;
- phone and email when permitted;
- candidate status;
- location and province;
- reception and last-update dates;
- a boolean CV-available indicator.

Exports never contain file keys, filesystem paths, raw audit data, credentials, or
unrequested personal data. Values beginning with spreadsheet formula markers (`=`, `+`,
`-`, or `@`) are neutralized according to the agreed Excel interoperability policy.
CSV is generated with a fixed, tested encoding, delimiter, quoting, newline, and date
format. CSV export is a future feature, so the exact corporate convention, fields, limits,
and retention period are deliberately deferred until that feature is planned.

Generated artifacts use private file storage under an `exports/` namespace and are
downloaded only through the API. They expire after a configured retention period and are
removed by a sweeper without deleting the audit/history record. The current 1,000-row
limit is retained as the initial safe default until representative data proves a better
limit.

Imports preserve the existing dry-run-to-commit contract:

- UTF-8 CSV input derived from Access or another approved source;
- structural and row validation before business writes;
- idempotency key plus content hash;
- explicit commit against the validated batch;
- partial success with downloadable row-level errors where permitted;
- durable `"OPS_ImportBatches"` and `"OPS_ImportErrors"` history.

The Access database is the authoritative migration source and PostgreSQL is the target.
Migration runs as a controlled, repeatable, one-time workflow against a read-only copy of
Access, with dry-run validation, source-to-target identifiers, record-count reconciliation,
row-level errors, and an acceptance report reviewed by HR. Browser `localStorage` and the
unwired Supabase schema are not authoritative data sources unless a later inventory finds
real records that exist nowhere in Access. Access is never queried by the production API.

---

## 10. Durable background operations

Imports, large exports, retention checks, orphan reconciliation, and malware scanning may
outlive an HTTP request. Use an ASP.NET Core `BackgroundService` worker, but keep durable
job state in PostgreSQL.

The database record—not an in-memory channel—is the source of truth. A queue may wake the
worker, but the worker must also recover queued/running jobs after restart, use leases or
compare-and-set state transitions, and make handlers idempotent. Initial states are:

```text
queued -> running -> completed
                  -> failed
                  -> cancelled
```

Each operation exposes status and a correlation identifier to the SPA. Sweepers clean up
expired artifacts and stale leases without hiding failures from operational history.

---

## 11. Frontend integration

Preserve the application's established React conventions:

- React 19 function components in `src/app/features/**` and `src/app/core/**`;
- co-located plain CSS and existing Kepler design tokens;
- plain singleton services wired in `src/app/core/di/services.ts`;
- the existing signal/`useSignal()` subscription model;
- controlled forms and service-layer validation errors in Spanish;
- React Router layout guards and data-driven navigation.

Do not introduce MUI, CSS Modules, Zustand, Redux, React Query, RxJS, or a schema/form
library as part of the backend migration. Those are separate product choices and are not
required to consume the API.

Add one shared HTTP transport service responsible for:

- the configured API base URL;
- JSON and problem-details parsing;
- correlation headers;
- cancellation and timeouts;
- the eventual authentication integration, after it is selected;
- safe download filename handling from `Content-Disposition`.

Feature services remain the component-facing boundary and call the transport service.
Migrate them incrementally from `localStorage`/Supabase to API-backed implementations;
components should not call `fetch` directly. Local UI preferences may remain in browser
storage, but candidate, catalog, role, import/export, audit, and document state may not.

There is no cloud cold start on the intranet, so scale-from-zero retry overlays and long
wake-up timeouts are excluded. Retry only idempotent requests and only for demonstrated
transient failures.

---

## 12. Testing strategy

| Suite                     | Covers                                                                                                                                                            |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Backend unit              | Domain invariants, validators, handlers, query composition, CSV escaping, and storage-key rules without network or disk dependencies.                             |
| Backend integration       | Real HTTP pipeline, endpoint contracts, PostgreSQL mappings/constraints, migrations, file adapter behavior, operation recovery, and later authorization policies. |
| Frontend unit/integration | Existing service, hook, component, and API-adapter behavior with test doubles through `ServicesProvider`.                                                         |
| Playwright E2E            | Candidate workflows, search, CV upload/download, export/import history, and permission behavior once identity is integrated.                                      |
| Operational/security      | Backup/restore, forbidden path exposure, upload validation, CSV injection, audit completeness, and fail-closed production configuration.                          |

Prefer a real disposable PostgreSQL instance for persistence integration tests rather
than EF Core InMemory, because constraints, JSON, text search, transactions, indexes, and
case folding are PostgreSQL behavior. The exact test provisioning mechanism (container,
dedicated CI database, or another approved intranet method) follows deployment policy.

Add architecture tests for forbidden project references and naming tests for table
prefixes. A backend change is complete only with relevant unit and integration evidence;
personal-data, file, export, or authorization changes also require negative security
tests.

---

## 13. Intranet runtime and hardening

Logical production topology:

```text
Internal browser
      |
      v
Intranet reverse proxy / TLS
      |-------------------|
      v                   v
React static files    ASP.NET Core API
                           |-----------|
                           v           v
                     PostgreSQL   Private file volume/share
```

Prefer same-origin routing (`/` for the SPA and `/api` for the API) to avoid unnecessary
CORS configuration. If separate origins are required, allow only configured intranet
origins and expose only required download headers.

Runtime requirements:

- TLS at the reverse proxy even on the intranet;
- explicit trusted proxy/network configuration for forwarded headers;
- fail-fast validation of database, storage-root, environment, and any future identity
  configuration;
- structured logs with redaction of candidate data, filenames where necessary, and
  credentials;
- health/readiness checks for API, PostgreSQL, and writable storage;
- security headers including `nosniff`, restrictive framing, and a reviewed CSP;
- request and upload limits at both proxy and API layers;
- least-privilege OS/service accounts and PostgreSQL credentials;
- coordinated PostgreSQL and file backups, with scheduled restore drills;
- controlled deployment migrations and a documented rollback procedure.

Docker is an approved runtime option. The development server uses Nginx, but the
production host and proxy remain open. The initial scale target is one API instance for a
couple of concurrent users and hundreds of candidate documents. Multiple API replicas
and shared filesystem storage are not initial requirements.

The application owns its PostgreSQL instance, including its version upgrades, migration
execution, backup policy, restore drills, monitoring, and credentials. These operational
duties may be automated by the platform team, but they cannot be delegated to a managed
database service assumed by the application architecture.

The initial recovery baseline is a daily coordinated backup of PostgreSQL and the
document directory, a 24-hour recovery point objective, a 4-hour recovery time objective,
and 30-day backup retention. Restore drills validate database records against document
keys and hashes.

---

## 14. Package baseline

Pin compatible versions centrally when the implementation change begins; do not copy
stale patch versions from another application.

| Package family                                      | Role                                       |
| --------------------------------------------------- | ------------------------------------------ |
| .NET / ASP.NET Core 10                              | Runtime and HTTP host                      |
| EF Core 10 + Npgsql provider                        | PostgreSQL persistence                     |
| MediatR                                             | Slice dispatch and pipeline behaviors      |
| FluentValidation                                    | Request validation                         |
| FastEndpoints + OpenAPI support                     | Endpoint definitions and API documentation |
| Serilog.AspNetCore                                  | Structured application/request logging     |
| xUnit plus approved test-double/assertion libraries | Backend tests                              |

MediatR, FastEndpoints, FluentValidation, Serilog, and a PostgreSQL test-container
library are approved dependency families. Pin specific compatible versions after a
support and license check when implementation begins. File storage uses BCL filesystem
APIs for the initial adapter; there is no Azure SDK dependency.

---

## 15. Adoption sequence

This architecture is a migration, not a big-bang rewrite.

| #   | Step                                                                                                                                | Reason                                                                    |
| --- | ----------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| 1   | Resolve the open infrastructure, storage, CSV, and naming questions in this document.                                               | They affect schema and deployment boundaries.                             |
| 2   | Create an OpenSpec change for the backend migration and update standing project principles that currently mandate Supabase/RLS.     | The requested architecture conflicts with the current recorded boundary.  |
| 3   | Scaffold the .NET solution, central packages, architecture tests, problem-details contract, and PostgreSQL test environment.        | Establishes constraints before feature code.                              |
| 4   | Implement one read-only candidate slice end to end through PostgreSQL and the existing React service boundary.                      | Proves the template with limited migration risk.                          |
| 5   | Implement candidate/catalog writes, logical deletion, search, and auditing slice by slice.                                          | Moves core business data before peripheral operations.                    |
| 6   | Add private document storage, quarantine, scanning, and prove backup/restore plus denied path exposure.                             | Files require a secure operational storage and scanning boundary.         |
| 7   | Move CSV export and Access/CSV import to durable server-side operations.                                                            | Builds on search, persistence, storage, and auditing.                     |
| 8   | Integrate the separately approved authentication/authorization mechanism.                                                           | Required before production cutover, without prematurely choosing it here. |
| 9   | Migrate authoritative data, cut the SPA over, verify parity, then retire Supabase runtime artifacts and operational `localStorage`. | Removal follows evidence, not scaffolding.                                |

Every step after discovery should be represented by OpenSpec proposal/design/spec/task
artifacts before implementation.

---

## 16. Decisions intentionally not carried over

The following choices from the source blueprint do not apply to Kepler Talento:

- Azure Blob Storage and Azure Communication Services;
- JWT issuing, BCrypt password storage, and refresh-token design;
- cloud scale-to-zero resilience and wake-up UI;
- MUI, Zustand, React Query, and a frontend reorganization unrelated to the backend;
- PDF/DXF generation, 3D libraries, and offline SQLite distribution;
- Supabase RLS, Storage, Edge Functions, and service-role keys as target architecture.

Useful ideas retained in an adapted form are VSA, inward dependencies, central package
management, explicit table mapping and prefixes, permission-oriented authorization as a
possible domain model, ports/adapters, durable operations, controlled files, PostgreSQL,
structured logging, and layered automated tests.

---

## 17. Confirmed decisions and open questions

### Confirmed

- Docker is permitted; Nginx is used on the development server.
- The application owns its PostgreSQL database.
- The physical PostgreSQL table prefixes are uppercase `CND_`, `CAT_`, `OPS_`, `AUD_`,
  and reserved `ADM_`; identifiers are explicitly mapped and quoted.
- Permission-based authorization remains independent of the deferred login mechanism.
- MediatR, FastEndpoints, FluentValidation, Serilog, and PostgreSQL test containers are
  acceptable dependency families.
- CSV details remain deferred until the export feature is planned.
- Expected scale is a couple of users, hundreds of candidate documents, and one initial
  API instance.
- Initial documents use a dedicated persistent host directory; SMB is not required.
- Access is the authoritative migration source and PostgreSQL is the target.
- Automated malware scanning is required before a new document becomes available.
- The initial CV allowlist covers PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF, and BMP;
  unsafe, encrypted/unscannable, executable, active-content, and archive formats are
  rejected.
- The upload limit is 20 MB per document, with aligned proxy and scanner resource limits.
- The recovery baseline is daily backup, 24-hour RPO, 4-hour RTO, and 30-day retention.

### Deferred infrastructure decision

1. **Production topology:** What will host production, and will it also run as Docker
   containers behind Nginx? This may remain deferred until infrastructure planning.
