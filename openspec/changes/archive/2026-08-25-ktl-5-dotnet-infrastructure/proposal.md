## Why

Kepler Talento currently has a functional React user experience but no wired operational
backend: most state remains in browser `localStorage`, while the Supabase artifacts do
not match the newly approved intranet architecture. Establishing a tested ASP.NET Core,
PostgreSQL, private-file, and Docker foundation now gives later candidate slices one
secure and repeatable platform instead of migrating each feature onto ad hoc
infrastructure.

## What Changes

- Add a .NET 10 solution with Domain, Application, Infrastructure, Web, unit-test, and
  integration-test projects, central NuGet versions, a pinned SDK, and enforceable inward
  dependency rules.
- Add the Vertical Slice Architecture and HTTP foundations: MediatR, FluentValidation,
  FastEndpoints/OpenAPI, stable problem details, correlation identifiers, and one
  read-only candidate reference slice through the real PostgreSQL pipeline.
- Add an application-owned PostgreSQL service, EF Core/Npgsql migrations, explicit quoted
  table mappings, and physical `CND_`, `CAT_`, `OPS_`, `AUD_`, and reserved `ADM_`
  prefixes.
- Add a Docker/Nginx development topology for the React SPA, API, PostgreSQL, and private
  ClamAV service with persistent data, document, quarantine, and signature volumes.
- Add private filesystem storage and quarantine/scanning capabilities for candidate
  documents, including a 20 MB limit, a business allowlist, content detection,
  application-generated keys, and fail-closed ClamAV results.
- Add recoverable PostgreSQL-backed background operations for scanning and future batch
  workloads.
- Add a shared React HTTP transport boundary and connect only the reference slice; the
  existing components, CSS, singleton services, signals, and test-swappable DI remain.
- Add structured/redacted logging, health/readiness checks, security headers, fail-fast
  configuration, explicit migration execution, and coordinated database/file recovery
  evidence.
- **BREAKING (architecture):** replace Supabase/RLS/Storage/Edge Functions as the target
  backend boundary with an ASP.NET Core API over PostgreSQL and private filesystem
  storage. Existing Supabase and `localStorage` paths remain temporarily until later
  feature cutovers prove their replacements.
- Update the standing project principles so future OpenSpec changes target the new API,
  persistence, storage, authorization, and security boundaries.
- Keep production authentication/login and hosting topology unresolved. Permission
  capability codes and an `ICurrentActor` seam are retained, but no identity provider,
  credential, cookie, or token decision is introduced.

KTL-5 serves developers and operators directly by providing a reproducible platform and
RRHH users indirectly by creating the protected data/document boundary their workflows
will later use. Success is measurable when the stack starts locally, the reference slice
passes end to end, quoted-prefix migrations and architecture checks pass, unsafe or
unscanned documents remain unavailable, no storage path leaks, restart recovery is
idempotent, and a coordinated backup can be restored and reconciled.

## Capabilities

### New Capabilities

- `backend-platform`: .NET solution boundaries, vertical-slice request pipeline,
  reference candidate slice, API errors, correlation, and production-safe actor seam.
- `postgresql-persistence`: Application-owned PostgreSQL, EF Core migrations, quoted
  uppercase table prefixes, explicit deployment migrations, constraints, and real
  PostgreSQL integration evidence.
- `private-document-storage`: Private filesystem keys, quarantine, broad CV allowlist,
  ClamAV scanning, controlled downloads, size/resource limits, and path non-disclosure.
- `durable-operations`: PostgreSQL-backed, recoverable, idempotent background operation
  states for scanning and future batch work.
- `intranet-runtime`: Docker/Nginx development topology, persistent services,
  observability, health/readiness, hardening, backup, restore, and rollback behavior.
- `frontend-api-transport`: Shared React HTTP/problem-details/download boundary and a
  reference API integration that preserves current frontend state and DI conventions.

### Modified Capabilities

- None. The existing `primary-navigation` capability is unaffected. The legacy feature
  documentation under `specs/001-gestion-cvs-rrhh/` remains the shipped-system record;
  later feature migration changes will alter its business capabilities incrementally.

## Impact

- **Code:** new `backend/` solution and tests; new Docker/Nginx runtime configuration;
  focused additions to `src/app/core` and the reference candidate service only.
- **Data:** new application-owned PostgreSQL schema and persistent volumes. KTL-5 uses
  synthetic fixtures and does not import Access or production candidate data.
- **Documents/personal data:** storage access is affected. Files remain outside the
  webroot, use opaque keys, are quarantined and scanned before availability, never expose
  paths, and do not appear in logs. Tests use synthetic documents.
- **Authorization:** permission-oriented authorization remains the target, but login and
  roles are not changed. Production rejects the development actor and cannot expose the
  reference API anonymously by configuration accident.
- **RLS departure:** PostgreSQL RLS and Supabase Storage policies are not the new boundary.
  The ASP.NET Core API, database/service least privilege, private filesystem permissions,
  explicit endpoint authorization, and negative integration/security tests replace that
  responsibility. Production cutover remains blocked until authentication integration
  proves fail-closed authorization.
- **Dependencies:** .NET/ASP.NET Core 10, EF Core/Npgsql, MediatR, FluentValidation,
  FastEndpoints, Serilog, xUnit, approved assertion/mock libraries, PostgreSQL test
  containers, PostgreSQL, Nginx, and ClamAV.
- **Operations:** the application owns database upgrades, explicit migrations, document
  storage, daily coordinated backups, a 24-hour RPO, a 4-hour RTO, 30-day retention, and
  restore drills. Final production hosting remains configurable.
- **Assumptions/edge cases:** one API instance, a couple of users, hundreds of documents,
  scanner outages, infected/unscannable/encrypted files, stale jobs, orphaned files,
  missing volumes, failed migrations, and partial dependency health are explicitly
  covered. CSV behavior, Access migration, full feature cutover, and authentication are
  follow-up changes.
