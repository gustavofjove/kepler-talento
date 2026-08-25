## Context

See [proposal.md](./proposal.md) for motivation and scope. The repository currently has a
React 19 SPA at the root, local singleton services backed mostly by `localStorage`, and
Supabase SQL/functions that are not wired as the operational backend. KTL-5 must add a
new backend/runtime foundation without breaking the existing SPA flows or choosing the
production login mechanism.

The design is constrained by a small initial scale (roughly two concurrent users and
hundreds of documents), intranet-only operation, Docker availability, Nginx in
development, application-owned PostgreSQL, physical uppercase table prefixes, private
candidate documents from untrusted origins, and a final production topology that is not
yet selected.

The behavioral contracts are defined in the six delta specs under [specs/](./specs/).

## Goals / Non-Goals

**Goals:**

- Establish production-quality project boundaries and one repeatable vertical-slice
  template.
- Prove the real HTTP-to-PostgreSQL path with synthetic candidate data.
- Make PostgreSQL schema evolution explicit, testable, and independent of Supabase.
- Provide private storage, quarantine, automatic malware scanning, and durable scan work
  without exposing a production candidate-document endpoint before authentication is
  selected.
- Provide a same-origin React transport seam that later feature migrations can adopt one
  service at a time.
- Make development startup, failure diagnosis, backup, and restore reproducible.
- Replace the standing Supabase/RLS design rules with API/database/filesystem boundaries
  suitable for the approved target architecture.

**Non-Goals:**

- Import Access data or make the reference seed resemble real candidates.
- Migrate full candidate CRUD, relations, search, catalogs, admin, import, or export.
- Expose candidate/document operations in production before identity integration.
- Decide cookies, tokens, Windows authentication, LDAP, Active Directory, MFA, or user
  provisioning.
- Select the final production host, implement multiple API replicas, or introduce SMB.
- Remove existing Supabase or `localStorage` code before later slices replace it.
- Convert or preview Office documents server-side.

## Decisions

### 1. Use four production projects plus two test projects

The backend solution will contain:

```text
backend/
|-- Directory.Packages.props
|-- KeplerTalento.slnx
|-- Domain/
|-- Application/
|-- Infrastructure/
|-- Web/
`-- Tests/
    |-- UnitTests/
    `-- IntegrationTests/
```

References are:

```text
Web --------------------> Application --------------------> Domain
 |                             ^
 `------> Infrastructure ------'

Infrastructure ------------------------------------------> Domain
```

`Domain` has no ASP.NET Core, EF Core, storage, or identity dependency. `Application`
contains features and ports and may use EF Core abstractions required by its persistence
port, but never the Npgsql provider. `Infrastructure` implements persistence, storage,
scanning, operations, and auditing. `Web` is the only composition root.

Architecture tests enumerate project references and selected namespaces. They fail when
Domain references another production project, Application references Infrastructure/Web,
or Infrastructure references Web.

**Alternative considered:** combine Domain and Application into one `Core` project. It
reduces one project but makes it easier for EF/application concerns to leak into domain
invariants. The separate projects are justified because the reference graph is being
established before business migration.

### 2. Pin the .NET toolchain and package families centrally

A repository `global.json` pins the .NET 10 SDK feature band available in the current
development environment. `Directory.Packages.props` enables central package management;
individual project files carry no versions.

Approved package families are ASP.NET Core/EF Core 10, Npgsql, MediatR,
FluentValidation, FastEndpoints/OpenAPI support, Serilog, xUnit, and approved test
assertion/mock and PostgreSQL container libraries. Patch versions are selected during
implementation after compatibility, support, and license checks and are recorded once
centrally.

**Alternative considered:** use only framework primitives and hand-written dispatch.
That reduces dependencies but discards the approved slice/pipeline pattern and creates
custom validation/endpoint infrastructure that later slices would have to maintain.

### 3. One file per application use case, separate endpoint adapters

Application features use `Application/Features/<Group>/<UseCase>.cs`. A use-case file
co-locates its command/query, response, validator, and handler. HTTP endpoints live under
`Web/Features/<Group>/` and only translate HTTP input, resolve the current actor, dispatch,
and map status/headers.

The reference feature is a read-only `GetReferenceCandidate` path backed by synthetic
seed data. It proves endpoint, validation, dispatch, persistence, errors, correlation,
OpenAPI, frontend transport, and browser routing. The reference endpoint and frontend
harness are registered only in Development/Test. There is no candidate API registered
for Production in KTL-5.

**Alternative considered:** implement candidate CRUD as the template. That would mix
platform setup with business migration, require unresolved authorization decisions, and
make rollback much larger.

### 4. Use MediatR and FluentValidation pipeline behavior

Application assembly registration discovers handlers and validators. A single validation
behavior runs all validators before handlers. Validation failures carry stable codes and
field associations. Business/domain exceptions carry a stable code but no HTTP status;
the Web boundary maps them.

The API returns typed success bodies directly. Errors use ASP.NET Core problem details
with extension fields for `code`, `correlationId`, and validation errors. The global
exception handler redacts internal exception details outside Development.

**Alternative considered:** wrap every success in `{ data, message }`. This adds payload
noise and couples clients to a redundant envelope. Pagination/operation metadata will
use dedicated response contracts when later slices need it.

### 5. Generate and propagate correlation at the Web boundary

Web accepts a syntactically valid `X-Correlation-ID` or creates a new identifier. The
value is stored in request context, returned as a response header, included in problem
details, pushed into the Serilog context, passed to audit records, and copied to durable
operations. Invalid/oversized caller values are replaced rather than reflected.

**Alternative considered:** rely only on ASP.NET trace identifiers. They remain useful
internally but do not provide the stable caller-visible identifier required by the SPA
and operational records.

### 6. Keep identity behind a fail-closed current-actor port

Application defines a current-actor contract containing an opaque external actor key and
effective permission codes. No UUID assumption is made for future identity keys. Web has
Development/Test actor adapters only. Startup validation throws if such an adapter is
configured under Production.

Production candidate/reference endpoints are not registered by KTL-5. Health endpoints
return no personal/operational data. Later authentication work supplies the production
adapter and explicit endpoint policies, followed by an endpoint authorization inventory
test.

**Alternative considered:** add temporary JWT/password login. The user explicitly
deferred login decisions, and temporary authentication has a high risk of becoming the
production mechanism accidentally.

### 7. Use one application-owned PostgreSQL database with separate runtime and migration roles

PostgreSQL is reachable only from the API/migrator/test network paths. The SPA never
receives database credentials. Development uses a containerized PostgreSQL service; tests
use a clean disposable PostgreSQL instance.

Two database privilege profiles are used:

- a migration/schema-owner role applies DDL and controlled grants;
- a runtime role receives only required DML privileges on application tables.

The reference schema contains aligned, intentionally minimal records:

| Table              | Foundation fields/purpose                                                          |
| ------------------ | ---------------------------------------------------------------------------------- |
| `"CND_Candidates"` | Synthetic reference candidate (`Id`, names, active flag, audit timestamps)         |
| `"CND_Documents"`  | Synthetic document metadata, opaque key, content metadata, hash, scan state        |
| `"OPS_Operations"` | Durable work type/status, correlation, lease, attempts, idempotency, error summary |
| `"AUD_Events"`     | Append-only non-sensitive infrastructure/audit events                              |

UUIDs are application/database generated consistently. Timestamps are UTC instants; date
only business values use PostgreSQL `date` when later introduced. Every table has an
explicit configuration and quoted physical name. A model/naming test rejects missing or
unapproved prefixes.

**Alternative considered:** use SQLite for development/tests. It would not validate
quoted case-sensitive identifiers, PostgreSQL constraints, transaction behavior, JSON,
or future search semantics.

### 8. Apply migrations through a dedicated deployment action

The API never calls automatic migration during Production startup. A dedicated migrator
command/bundle applies migrations and records them in the normal migration history.
Development Compose runs the migrator as a one-shot dependency before the API; test setup
applies the same migrations to its disposable database.

Migration output can also be generated as a reviewed idempotent SQL script. Each slice
ships schema changes, constraints, indexes, and grants together. Production readiness
reports pending/incompatible migrations without applying them.

**Alternative considered:** `Database.Migrate()` at every startup. It is convenient but
mixes runtime and DDL privileges, obscures release control, complicates rollback, and can
race when multiple instances are introduced later.

### 9. Deliberately replace Supabase RLS rather than reproduce it

This is a documented departure from current standing principles 2 and 3. The simpler
alternative—retain Supabase as the database, RLS, Storage, and function boundary—was
rejected because the approved architecture requires an intranet ASP.NET Core API and
application-owned PostgreSQL/filesystem infrastructure.

KTL-5 does not create PostgreSQL RLS policies or Supabase helper functions. Instead it
ships:

- no direct browser/database connection;
- isolated database networking;
- separate migrator and least-privilege runtime roles with grants in migrations;
- private filesystem permissions and API-only file access;
- explicit permission codes/current-actor context;
- no production business endpoints before authentication is integrated;
- negative integration/security tests and later endpoint-policy inventory.

`openspec/config.yaml` is updated during KTL-5 so future artifacts require these new
controls instead of Supabase-specific commands. Personal-data minimization, logical
deletion, private files, and fail-closed security remain unchanged principles.

### 10. Store document binaries on a bind-mounted host directory

Infrastructure exposes storage operations by opaque relative key. The initial adapter
uses one dedicated host directory mounted into the API container with subtrees for
`quarantine/`, `available/`, and future `exports/`. The original filename is metadata only.
Keys have the form `candidates/{candidateId}/{documentId}/content`; no filename or absolute
path participates in key construction.

Quarantine and available directories live on the same mounted filesystem so promotion
can use an atomic move. File creation uses create-new semantics, restricted permissions,
bounded streams, and cleanup compensation when the database transaction fails.

**Alternative considered:** store binaries in PostgreSQL. It simplifies the number of
backup targets but bloats database backups and couples large-stream behavior to the
relational store. **Alternative considered:** SMB immediately. The expected scale and
single API instance do not justify a network share yet; the storage port preserves that
option.

### 11. Model document safety as explicit metadata states

`"CND_Documents"` records metadata and one of:

```text
pending_scan -> clean
             -> infected
             -> rejected
             -> scan_failed
```

Only `clean` is downloadable. The initial extension/content allowlist is PDF, DOC, DOCX,
ODT, RTF, TXT, JPEG, PNG, TIFF, and BMP. The detector verifies extension/content
agreement; client MIME is not trusted. The common request limit is 20 MB. Encrypted,
password-protected, empty, malformed, macro-enabled, active-content, executable, script,
and arbitrary archive uploads are rejected.

Non-PDF files use attachment download; the server performs no rendering, conversion, or
office parsing beyond bounded detection/scanning. Responses use a sanitized original
filename, safe content type, `Content-Disposition`, `X-Content-Type-Options: nosniff`, and
private/no-store caching.

**Alternative considered:** allow every extension and rely solely on antivirus. Antivirus
is one layer and cannot make arbitrary active/executable content an acceptable CV format.

### 12. Use a private ClamAV sidecar with streamed scanning

The scanner adapter sends content to `clamd` using its current streaming protocol. The
daemon is addressable only on the private Compose network; its port is not published. A
persistent signature volume is updated by the official image's updater. Container health
checks verify daemon availability and signature readiness.

Configuration aligns the 20 MB upload limit with stream size and adds bounded expanded
size, container recursion, contained file count, and scan timeout. The host budget reserves
3 GiB minimum / 4 GiB preferred for the scanner.

If clean, the worker atomically promotes the file and records signature version/time. If
infected or unscannable, it remains unavailable and is audited. A scanner outage blocks
new promotion but does not affect downloads already marked clean. A future corporate
scanner replaces only the adapter.

**Alternative considered:** execute a scanner process per request. A daemon avoids
reloading signatures for every upload and gives clearer health/resource boundaries.

### 13. Persist background work and leases in PostgreSQL

`"OPS_Operations"` is the source of truth. An in-process channel may wake the worker but
is never the only queue. Enqueue persists the operation in the same transaction as the
pending document metadata.

Workers atomically claim queued work, store an owner/lease expiry, increment attempts,
and transition only through `queued -> running -> completed|failed|cancelled`. Startup
requeues or fails stale work according to its retry policy. Idempotency keys and
compare-and-set transitions prevent duplicate clean promotion or artifacts.

The initial worker handles scan operations only. Interfaces and schema allow later import,
export, retention, and reconciliation operation types without implementing their product
behavior now.

**Alternative considered:** use RabbitMQ or another broker. A couple of users and one API
instance do not justify another service; PostgreSQL durability is sufficient and easier
to back up consistently.

### 14. Preserve the existing React architecture and use native browser transport

A singleton HTTP transport is wired in `src/app/core/di/services.ts`. It uses native
`fetch`, not Axios, and centralizes base URL, JSON/problem-details parsing, correlation,
timeouts, cancellation, and safe downloads. Feature services call the transport;
components never call it directly. Errors map to the existing `AppError`/toast boundary
with Spanish fallback messages.

The reference candidate service/harness exists only for Development/Test and uses the
same `ServicesProvider` substitution pattern as current tests. No existing candidate,
catalog, search, role, import, export, auth, or document service is switched in KTL-5.
Local UI preferences may remain in `localStorage`.

**Alternative considered:** introduce React Query, Zustand, Axios, or a schema/form
library during the migration. They are unnecessary for the transport seam and conflict
with established repository conventions.

### 15. Use a same-origin Docker development topology

The new development Compose topology contains:

```text
browser -> Nginx -> React static assets
                 -> /api -> ASP.NET Core API -> PostgreSQL
                                            -> ClamAV
                                            -> document volume
```

Nginx exposes the existing frontend port behavior and routes `/api`. PostgreSQL and ClamAV
are private services. Named/bind volumes persist PostgreSQL data, documents/quarantine,
and ClamAV signatures. Service health/dependency conditions sequence the migrator and API.

The design does not assume this Compose topology for production; all host paths, proxy
networks, and endpoints come from validated configuration.

### 16. Separate liveness, readiness, and dependency detail

- Liveness reports only that the API process can respond.
- Readiness requires PostgreSQL and writable storage; pending migrations make it not
  ready.
- Scanner health is reported separately/degraded: uploads fail closed while existing
  clean downloads remain conceptually available.
- Detailed dependency information is restricted to Development/Test or operator-safe
  output and never includes credentials/paths.

Nginx and Compose use these signals for development startup. Production configuration can
map them to its eventual orchestrator.

### 17. Use structured, redacted logs and explicit HTTP hardening

Serilog request logging enriches events with correlation, environment, route, status,
duration, and non-sensitive operation identifiers. A logging policy forbids request
bodies, document contents, raw candidate values, credentials, storage keys/paths, and
unnecessary original filenames. Scanner/audit failures log stable codes.

The Web pipeline configures trusted forwarded headers, request/upload limits, HTTPS
forwarding behavior, no-sniff, restrictive framing/referrer policy, CSP appropriate to
the existing SPA, and safe exposed download headers. Same-origin development avoids
permissive CORS; any later separate origin must be explicitly configured.

### 18. Back up database and documents as one manifested recovery set

A backup run creates a timestamped recovery directory containing:

- a PostgreSQL dump from an authorized backup identity;
- an archive/snapshot of available and required quarantine metadata/files;
- a manifest with migration version, file counts, hashes, timestamps, and tool outcome;
- no credentials.

Daily scheduling, 30-day retention, 24-hour RPO, and 4-hour RTO are the baseline. Restore
validation targets a clean non-production stack, applies/restores the database and file
set, checks schema state, and reconciles every restored document metadata key/hash (or
records explicit errors). Rollback restores the previous app artifacts and compatible
recovery set after writes are frozen.

**Alternative considered:** back up PostgreSQL and files independently with no manifest.
That can produce metadata/binary mismatches that appear only during an incident.

### 19. Test the actual risk boundaries

Testing is split by purpose:

- unit: domain/application behavior, validators, storage keys, scan mapping, state
  transitions, error/correlation mapping;
- architecture: forbidden references and table-prefix mappings;
- backend integration: real Web host, disposable PostgreSQL, migrations, grants,
  constraints, storage, fake scanner and real ClamAV contract tests, health, restart
  recovery, safe errors/downloads;
- frontend unit/integration: transport parsing/cancellation/download behavior and service
  substitution;
- Playwright: Nginx same-origin reference path;
- operational/security: production actor rejection, unsafe/unscanned denial, path/log
  non-disclosure, backup/restore reconciliation.

The default integration suite uses a deterministic fake scanner for branch coverage; a
separate tagged/container test verifies the ClamAV protocol and EICAR-style test signature
without real candidate files. EF InMemory is not used for persistence integration.

## Risks / Trade-offs

- **[Scope remains large for one change]** → Keep business migration out, organize tasks
  into independently verifiable waves, and make the reference slice the first integration
  checkpoint.
- **[Uppercase PostgreSQL table prefixes require quoted exact-case SQL]** → Map every
  table explicitly, fail naming tests, and require quoted identifiers in migrations and
  operational SQL.
- **[No PostgreSQL RLS means database access outside the API would bypass application
  authorization]** → Isolate database networking, keep browser credentials impossible,
  use least-privilege runtime grants, prohibit production business endpoints until auth
  is integrated, and add negative endpoint/security tests.
- **[Authentication is deferred]** → Register reference/business endpoints only in
  Development/Test, make the development actor fatal in Production, and gate cutover on
  a separate authentication change.
- **[ClamAV signatures can miss new malware or produce false positives]** → Use defense
  in depth (allowlist, content detection, limits, quarantine), expose scan status for
  review, update signatures, and allow a future corporate scanner adapter.
- **[ClamAV consumes significant memory]** → Document and validate the 3–4 GiB budget,
  health-check the daemon, and avoid per-request process startup.
- **[Filesystem and database commits are not one atomic transaction]** → Stage first,
  persist durable state, use atomic same-volume promotion, compensate failures, and run
  reconciliation.
- **[PostgreSQL-backed queue supports the initial single instance but is not a general
  broker]** → Use leases/idempotency now and revisit the adapter only if throughput or
  multi-replica requirements appear.
- **[Daily backups can lose up to 24 hours]** → Record the accepted RPO, validate backup
  completion, and revisit frequency before production if HR changes the tolerance.
- **[Existing specs and tests assume PDF-only/Supabase]** → Update standing principles in
  KTL-5, leave shipped-system docs intact as history, and change affected feature
  requirements only in later migration changes.
- **[Production topology is unknown]** → Keep paths/endpoints/proxy trust configurable;
  do not encode Compose-specific assumptions into application contracts.

## Migration Plan

1. Create branch `feat/KTL-5` and capture baseline frontend tests/status.
2. Update standing project principles and task gates to describe the approved API,
   PostgreSQL, private-storage, and security boundary while preserving personal-data and
   test-evidence requirements.
3. Add the pinned .NET solution, central packages, project graph, and architecture/unit
   test skeletons.
4. Add Web pipeline, reference slice contracts, synthetic model, explicit mappings, and
   PostgreSQL migrations/grants.
5. Add disposable PostgreSQL integration tests and the explicit migrator action.
6. Add the shared frontend transport and Development/Test reference integration.
7. Add filesystem storage/quarantine, document metadata, scanner port/fake, and security
   tests.
8. Add durable operation leasing/recovery and the ClamAV sidecar/protocol tests.
9. Add the unified Docker/Nginx development topology, health checks, persistence volumes,
   logging, and HTTP hardening.
10. Add backup/restore/rollback tooling and run a clean restore reconciliation.
11. Run all frontend/backend/integration/architecture/security/E2E gates, inspect results,
    and retain evidence.

Rollback before any later business cutover is low risk: stop the new stack and redeploy
the existing frontend-only container. Existing `localStorage` behavior and Supabase files
remain untouched. New synthetic PostgreSQL/document volumes can be retained for diagnosis
or removed through the documented development cleanup process. No Access or production
candidate data is modified by KTL-5.

## Open Questions

- Which host, reverse proxy, and container/service topology will production use? This is
  safely deferred because KTL-5 validates a portable development topology and keeps all
  production-specific paths, origins, proxy trust, and service endpoints configurable.
