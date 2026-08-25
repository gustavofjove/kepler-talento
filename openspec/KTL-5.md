# KTL-5 — Build the .NET infrastructure foundation

**Status:** Ready for OpenSpec planning  
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)

## Summary

Create the reusable backend and runtime foundation for moving Kepler Talento from its
current browser-`localStorage`/unwired-Supabase state to an intranet-hosted ASP.NET Core
API over PostgreSQL.

KTL-5 establishes the platform on which later vertical slices will run. It does not
migrate every business feature or perform the production cutover.

## Why

The current React SPA implements the user experience, but most operational state is
browser-local. The Supabase schema, policies, storage, and Edge Functions are present as
design/implementation evidence but are not the desired target boundary.

The application needs a backend that:

- owns business and persistence operations;
- runs entirely on the company intranet;
- supports Vertical Slice Architecture and explicit dependency boundaries;
- stores relational data in an application-owned PostgreSQL database;
- stores candidate documents privately outside the database;
- scans externally sourced documents before they become available;
- can later integrate the separately selected login mechanism without restructuring
  business handlers.

## Is one OpenSpec change enough?

One change is sufficient for the **infrastructure foundation** described here because the
components form one deployable and testable platform boundary.

One change is not sufficient for the complete backend migration. The following remain
separate follow-up changes so they can be independently specified, tested, and rolled
back:

1. candidate/catalog write slices and logical deletion;
2. advanced PostgreSQL candidate search and saved searches;
3. Access-to-PostgreSQL data migration and reconciliation;
4. React feature-service cutover from `localStorage`/Supabase to the API;
5. authentication/login integration and the completed authorization matrix;
6. CSV export/import product behavior when its requirements are ready;
7. final Supabase runtime retirement and production cutover.

## KTL-5 outcome

At completion, a developer can start the intranet stack, run the frontend and API through
Nginx, reach a healthy ASP.NET Core service, connect to PostgreSQL, exercise the reference
vertical slice, prove architectural dependency rules, persist and retrieve private test
documents through storage/scanner adapters in automated tests, and run backup/restore
validation without using Supabase.

## In scope

### 1. .NET solution and dependency boundaries

- Add `backend/KeplerTalento.slnx` targeting .NET 10.
- Add production projects:
  - `Domain` for framework-independent entities, value objects, invariants, and domain
    events;
  - `Application` for vertical slices, ports, validation, pipeline behaviors, and shared
    application contracts;
  - `Infrastructure` for EF Core/Npgsql, filesystem storage, ClamAV, background
    operations, and auditing adapters;
  - `Web` for FastEndpoints, HTTP middleware, OpenAPI, DI composition, and hosting.
- Add backend unit and integration test projects.
- Enforce inward project references with architecture tests.
- Add `Directory.Packages.props` and a repository `global.json`.

### 2. Vertical-slice and HTTP foundation

- Configure MediatR dispatch and FluentValidation pipeline behavior.
- Configure stable validation codes and RFC 9457-style problem-details errors with a
  correlation identifier.
- Configure FastEndpoints and generated OpenAPI documentation.
- Add one small read-only candidate reference slice to prove the complete Web ->
  Application -> Infrastructure -> PostgreSQL path. This slice is a template, not a full
  candidate-feature migration.
- Keep authentication/login unresolved.
- Define `ICurrentActor` as the application seam, with test/development actors that are
  impossible to enable in a production environment.
- Preserve permission capability codes independently of the eventual identity provider.

### 3. PostgreSQL and EF Core foundation

- Run an application-owned PostgreSQL instance for development and tests.
- Configure EF Core 10 with the Npgsql provider.
- Add explicit entity configurations, migrations, indexes, constraints, and delete
  behavior for the minimal reference slice and infrastructure records.
- Use physical, quoted table names with these uppercase prefixes:

  | Prefix | Purpose                                                   |
  | ------ | --------------------------------------------------------- |
  | `CND_` | Candidate aggregate and related profile/document metadata |
  | `CAT_` | Business-maintained catalogs                              |
  | `OPS_` | Durable import/export/background-operation state          |
  | `AUD_` | Append-only audit events                                  |
  | `ADM_` | Reserved administration/authorization data                |

- Keep C# names free of database prefixes.
- Do not run migrations implicitly during production API startup; expose an explicit
  deployment migration step.
- Treat existing Supabase SQL as schema-intent evidence only.

### 4. Docker and intranet development runtime

- Add a Docker-based development stack for:
  - Nginx;
  - the existing React frontend;
  - the ASP.NET Core API;
  - PostgreSQL;
  - ClamAV `clamd` with a persistent signature database.
- Prefer same-origin routing: `/` for the SPA and `/api` for the backend.
- Persist PostgreSQL data, document data, quarantine data, and ClamAV signatures outside
  disposable container layers.
- Do not publish the ClamAV daemon outside the private Docker network.
- Keep the final production host/topology configurable and undecided.

### 5. Private document-storage foundation

- Define `IFileStorage` and an initial filesystem adapter backed by a dedicated persistent
  host directory mounted into the API container.
- Store only opaque relative keys in PostgreSQL; never expose host paths, drive letters,
  UNC paths, mount points, or permanent URLs.
- Store original filenames as metadata, not physical paths.
- Configure a 20 MB maximum per document at Nginx, API, and scanner boundaries.
- Implement/test an initial allowlist for:
  - PDF, DOC, DOCX, ODT, RTF, and TXT;
  - JPEG, PNG, TIFF, and BMP.
- Reject executables, scripts, HTML/SVG, archives submitted as CVs, encrypted or
  password-protected files that cannot be scanned, and macro-enabled formats such as
  DOCM.
- Validate extension, detected content/signature, non-empty content, safe display name,
  and cryptographic hash without trusting the client MIME type.

### 6. Automated malware-scanning foundation

- Define `IFileScanner` and implement a ClamAV `clamd` adapter using streamed scanning.
- Quarantine every new document before scanning.
- Make only a clean document available; infected, timed-out, errored, or unscannable
  documents fail closed.
- Keep previously clean documents downloadable when the scanner is temporarily
  unavailable.
- Persist and update the ClamAV signature database.
- Bound scan duration, expanded content size, archive/container recursion, and contained
  file count.
- Budget approximately 3 GiB minimum / 4 GiB preferred RAM for the scanner container.
- Record scan state and malware/error audit events without logging document contents.

### 7. Durable operations foundation

- Add PostgreSQL-backed operation state and an ASP.NET Core `BackgroundService` worker.
- Treat database job state as authoritative; an in-memory queue may only signal work.
- Support recoverable and idempotent transitions:

  ```text
  queued -> running -> completed
                    -> failed
                    -> cancelled
  ```

- Recover stale queued/running work after an API restart.
- Use the foundation for scanning now and for future imports/exports and retention jobs.

### 8. Frontend API boundary

- Preserve the existing React function components, plain CSS, singleton services,
  signals/`useSignal()`, controlled forms, and explicit `ServicesProvider` test doubles.
- Add one shared HTTP transport service for API base URL, JSON/problem-details parsing,
  correlation identifiers, cancellation, timeouts, and controlled downloads.
- Keep feature services as the component-facing boundary; components do not call `fetch`
  directly.
- Do not introduce MUI, CSS Modules, Zustand, Redux, React Query, RxJS, or a schema/form
  library as part of KTL-5.
- Do not migrate all feature services in this ticket; only connect what is needed for the
  reference slice and health verification.

### 9. Observability and hardening

- Add structured Serilog request/application logging with personal-data redaction.
- Add correlation across HTTP requests, audit events, and background operations.
- Add liveness/readiness checks for API, PostgreSQL, writable storage, and ClamAV health.
- Add fail-fast validation for database, storage, environment, and development-actor
  configuration.
- Add security headers, request/upload limits, safe download headers, and explicit
  forwarded-proxy configuration.
- Use least-privilege service/database/filesystem identities.

### 10. Backup and recovery foundation

- Back up PostgreSQL and the document directory as one recovery unit.
- Use the initial baseline:
  - daily backup;
  - 24-hour recovery point objective;
  - 4-hour recovery time objective;
  - 30-day backup retention.
- Add a restore-validation procedure that reconciles document metadata, relative keys,
  and hashes.
- Document database migration, backup, restore, rollback, and orphan/stale-quarantine
  recovery procedures.

### 11. Test and delivery evidence

- Backend unit tests cover validators, handlers, storage keys, scan-result handling,
  operation transitions, and domain-independent infrastructure behavior.
- Backend integration tests use a real disposable PostgreSQL instance rather than EF
  InMemory.
- Integration tests exercise the real HTTP pipeline, quoted table mappings, migrations,
  storage/quarantine, ClamAV adapter contract, health checks, and failure modes.
- Architecture tests reject forbidden project references and incorrectly prefixed table
  mappings.
- Frontend tests prove the shared transport and reference service through
  `ServicesProvider` doubles.
- A targeted Playwright smoke flow proves same-origin frontend/API operation.
- Security tests prove that paths are never exposed, unsafe/unscanned files fail closed,
  size/type limits hold, and production cannot enable the development actor.
- Formatting, lint, frontend tests, backend tests, integration tests, and the targeted E2E
  flow are executed and inspected before completion.

## Out of scope

- Selecting or implementing the production login/authentication mechanism.
- Full user, role, and permission administration migration.
- Full candidate CRUD, relations, catalogs, search, or saved-search migration.
- Importing the Access production dataset.
- Product CSV export/import behavior.
- In-browser Office conversion or preview.
- Multiple API replicas, SMB storage, high availability, or cloud deployment.
- Removing current Supabase or `localStorage` code before replacement slices are proven.
- Production cutover.

## Personal-data and security impact

KTL-5 introduces the future storage and processing boundary for candidate personal data
and externally sourced documents. It must:

- minimize copied test data and use synthetic fixtures;
- keep document storage and quarantine private;
- deny access to paths and unscanned content;
- avoid document contents and candidate details in logs;
- preserve logical deletion/retention seams;
- fail closed when production configuration or scanning is unsafe.

The existing OpenSpec principles that mandate Supabase/RLS conflict with this approved
target. The KTL-5 OpenSpec proposal and design must document the departure and include a
task to update `openspec/config.yaml` so the standing principles describe the ASP.NET
Core/PostgreSQL/API authorization boundary before later migration changes are planned.

## Acceptance criteria

1. The .NET solution builds and architecture tests prove the required reference graph.
2. The Docker development stack starts Nginx, frontend, API, PostgreSQL, and ClamAV with
   persistent data/signature volumes.
3. The API reports healthy/readiness status for required dependencies.
4. The reference vertical slice succeeds through the real HTTP and PostgreSQL pipeline.
5. EF migrations create explicitly quoted tables with approved uppercase prefixes.
6. Storage/scanning integration tests prove quarantine, clean promotion, infected-file
   denial, scanner-unavailable behavior, and 20 MB/type controls.
7. No API response exposes an internal storage path or permanent document location.
8. Background operations recover safely after restart and do not duplicate completed
   work.
9. The frontend consumes the reference API through the shared transport/service boundary.
10. Backup/restore validation reconciles database metadata with stored document hashes.
11. Authentication remains deferred and no development actor can run in production.
12. All required frontend, backend, integration, architecture, security, and targeted E2E
    checks pass with inspected evidence.

## Deferred decision

- Final production host and reverse-proxy topology. Docker is permitted and Nginx is used
  for development; the implementation must remain portable until production
  infrastructure is selected.
