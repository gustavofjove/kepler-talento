## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-5`, then confirm the branch contains only the intended starting changes. [All capabilities]
- [x] 0.2 Record the baseline `git status`, `npm test`, `npm run lint`, and `npm run format:check` results before changing the runtime. [All capabilities]

## 1. Align Project Principles and Toolchain

- [x] 1.1 Update `openspec/config.yaml` so the standing backend boundary is the .NET API, application-owned PostgreSQL, and private document storage, while retaining personal-data, fail-closed authorization, and evidence requirements. [backend-platform, postgresql-persistence, private-document-storage]
- [x] 1.2 Add a pinned .NET 10 SDK configuration and central NuGet package/version management for ASP.NET Core, FastEndpoints, MediatR, FluentValidation, EF Core PostgreSQL, and Serilog. [backend-platform]
- [x] 1.3 Create the solution and `Domain`, `Application`, `Infrastructure`, `Web`, unit-test, and integration-test projects with the approved inward project references. [backend-platform]
- [x] 1.4 Add architecture tests that reject forbidden production-project references and verify they fail against a deliberate invalid fixture. [backend-platform]
- [x] 1.5 Add repository build commands for restoring and building the frontend and backend from a clean checkout. [backend-platform, intranet-runtime]

## 2. Establish the API and Vertical-Slice Boundary

- [x] 2.1 Add the Web composition root, FastEndpoints registration, MediatR pipeline, FluentValidation behavior, and environment-specific OpenAPI generation. [backend-platform]
- [x] 2.2 Define the replaceable current-actor contract plus Development/Test adapter, and make startup fail when that adapter is configured in Production. [backend-platform]
- [x] 2.3 Implement correlation-ID validation/generation and propagate the identifier through responses, logs, MediatR requests, audit data, and queued operations. [backend-platform, durable-operations]
- [x] 2.4 Implement validation and exception mapping to stable problem-details responses that redact internal and personal information. [backend-platform]
- [x] 2.5 Add the minimal synthetic candidate domain model and `GetReferenceCandidate` query, validator, handler, DTO, and endpoint as one vertical slice. [backend-platform]
- [x] 2.6 Register the reference/business endpoint only in Development/Test and add a negative Production registration test. [backend-platform]
- [x] 2.7 Generate and inspect the OpenAPI contract for the reference route, health routes, correlation header, and problem shapes. [backend-platform]
- [x] 2.8 Add unit tests for validation short-circuiting, not-found behavior, correlation handling, actor denial, and exception redaction. [backend-platform]

## 3. Add Application-Owned PostgreSQL Persistence

- [x] 3.1 Define application persistence ports in `Application` and implement the EF Core `DbContext` only in `Infrastructure`. [postgresql-persistence, backend-platform]
- [x] 3.2 Map candidate, document, operation, and audit entities to the exact quoted tables `"CND_Candidates"`, `"CND_Documents"`, `"OPS_Operations"`, and `"AUD_Events"` while keeping C# names idiomatic. [postgresql-persistence]
- [x] 3.3 Add explicit keys, foreign keys, uniqueness, check constraints, concurrency tokens, timestamps, and logical-deletion fields required by the infrastructure models. [postgresql-persistence]
- [x] 3.4 Create the initial EF migration and inspect its SQL for exact uppercase prefixes, quoted identifiers, constraints, indexes, and absence of Supabase assumptions. [postgresql-persistence]
- [x] 3.5 Add separate migration and runtime database roles/grants so the API runtime cannot alter schema or bypass its approved tables. [postgresql-persistence]
- [x] 3.6 Add an explicit migrator command/container path and prove the Web host never applies production migrations automatically. [postgresql-persistence, intranet-runtime]
- [x] 3.7 Add an idempotent Development/Test seeder containing synthetic reference records only. [postgresql-persistence, backend-platform]
- [x] 3.8 Add a disposable real-PostgreSQL integration fixture and tests for migrations, grants, constraints, quoted mappings, seeding, and reference-query behavior. [postgresql-persistence, backend-platform]

## 4. Add the Shared React API Transport

- [x] 4.1 Implement a native-`fetch` transport that centralizes same-origin base paths, JSON/problem parsing, correlation headers, timeouts, cancellation, and safe defaults. [frontend-api-transport]
- [x] 4.2 Map backend validation, not-found, authorization, timeout, and unexpected failures into the existing `AppError` and Spanish toast boundary. [frontend-api-transport, backend-platform]
- [x] 4.3 Add safe attachment-download handling that accepts only sanitized server filenames/content types and never exposes internal storage keys. [frontend-api-transport, private-document-storage]
- [x] 4.4 Register the transport and a Development/Test reference-candidate service in `src/app/core/di/services.ts` without switching existing feature services. [frontend-api-transport]
- [x] 4.5 Add a Development/Test-only reference harness using existing React function-component, signal, hook, `ServicesProvider`, `name`, and `data-testid` conventions. [frontend-api-transport, backend-platform]
- [x] 4.6 Add frontend tests for successful/problem responses, malformed bodies, correlation, cancellation, timeout, safe downloads, subscriptions, and service substitution. [frontend-api-transport]

## 5. Build Private Document Storage and Quarantine

- [x] 5.1 Add validated document-storage options for separate quarantine/available roots, a 20 MB maximum, scan timeout, and startup writability checks. [private-document-storage, intranet-runtime]
- [x] 5.2 Implement opaque generated storage keys and containment-safe path resolution with no candidate data or original filename in physical paths. [private-document-storage]
- [x] 5.3 Implement streamed quarantine writes, bounded hashing, cleanup on failure, and atomic same-volume promotion to available storage. [private-document-storage]
- [x] 5.4 Implement bounded content/extension detection for PDF, DOC, DOCX, ODT, RTF, TXT, JPEG, PNG, TIFF, and BMP. [private-document-storage]
- [x] 5.5 Reject extension/content mismatches, macro-enabled or password-protected office files, executables, scripts, archives, malformed files, and oversized uploads with stable reasons. [private-document-storage]
- [x] 5.6 Persist document metadata and the `pending_scan`, `clean`, `infected`, `rejected`, and `scan_failed` states without storing document bodies or internal paths in responses. [private-document-storage, postgresql-persistence]
- [x] 5.7 Add controlled download behavior that serves only `clean` files with attachment disposition, sanitized names, safe types, `nosniff`, and private/no-store caching. [private-document-storage, frontend-api-transport]
- [x] 5.8 Add storage unit/integration tests for traversal, collision, partial writes, cleanup, size limits, format detection, unsafe formats, state denial, headers, and metadata redaction. [private-document-storage]

## 6. Implement Durable Background Operations

- [x] 6.1 Implement the PostgreSQL operation repository with queued records, correlation/idempotency keys, attempts, owner, lease expiry, status, and non-sensitive outcome fields. [durable-operations, postgresql-persistence]
- [x] 6.2 Implement atomic operation claiming and compare-and-set transitions for `queued -> running -> completed|failed|cancelled`. [durable-operations]
- [x] 6.3 Add the hosted worker wake-up loop while keeping PostgreSQL, rather than process memory, as the queue source of truth. [durable-operations]
- [x] 6.4 Implement startup recovery for queued and stale-running work with bounded retries and lease renewal. [durable-operations]
- [x] 6.5 Make document promotion and operation completion idempotent so retries cannot duplicate files or completed effects. [durable-operations, private-document-storage]
- [x] 6.6 Add non-sensitive status lookup by operation and correlation identifier for Development/Test/operator diagnostics. [durable-operations]
- [x] 6.7 Add concurrent PostgreSQL integration tests for single-owner claims, invalid transitions, restart recovery, retry exhaustion, and idempotent completion. [durable-operations]

## 7. Integrate Automated Malware Scanning

- [x] 7.1 Define the scanner port and deterministic fake implementation for unit and default integration tests. [private-document-storage]
- [x] 7.2 Implement the ClamAV `clamd` streamed-scan adapter with bounded stream size, timeout, cancellation, and explicit clean/infected/error result mapping. [private-document-storage]
- [x] 7.3 Implement the scan operation handler to promote clean files, retain/deny infected or unscannable files, persist signature/time metadata, and emit redacted audit events. [private-document-storage, durable-operations]
- [x] 7.4 Configure ClamAV limits for 20 MB inputs, bounded expansion, recursion, contained-file count, and the documented 3 GiB minimum/4 GiB preferred host budget. [private-document-storage, intranet-runtime]
- [x] 7.5 Add a private ClamAV Compose service with persistent signatures, no published daemon port, updater behavior, and readiness checks. [private-document-storage, intranet-runtime]
- [x] 7.6 Add fake-scanner branch tests and a tagged real-ClamAV contract test using an EICAR-style test signature and no real candidate data. [private-document-storage]
- [x] 7.7 Test scanner outage and timeout behavior to prove new files stay unavailable while an already-clean download remains usable. [private-document-storage, intranet-runtime]

## 8. Assemble the Intranet Docker and Nginx Runtime

- [x] 8.1 Add reproducible multi-stage backend and frontend images with pinned base-image versions and non-root runtime users where supported. [intranet-runtime]
- [x] 8.2 Add the development Compose topology for Nginx, frontend assets, API/worker, migrator, PostgreSQL, and ClamAV with private backend networks. [intranet-runtime]
- [x] 8.3 Add persistent volumes for PostgreSQL data, available/quarantine documents, and ClamAV signatures, and document safe development cleanup. [intranet-runtime, private-document-storage]
- [x] 8.4 Configure Nginx to serve the SPA and proxy `/api` same-origin with correlation/download headers, bounded request bodies, timeouts, and no direct storage route. [intranet-runtime, frontend-api-transport, private-document-storage]
- [x] 8.5 Implement liveness, readiness, migration-state, storage-writability, PostgreSQL, and separately degraded scanner health checks. [intranet-runtime]
- [x] 8.6 Add startup validation for connection strings, roots, scanner endpoint, proxy trust, upload limits, and forbidden Production development-actor settings. [intranet-runtime, backend-platform]
- [x] 8.7 Add structured Serilog request/worker events and automated checks that logs omit bodies, personal values, credentials, storage paths/keys, and unnecessary filenames. [intranet-runtime]
- [x] 8.8 Add HTTP hardening for forwarded headers, `nosniff`, framing/referrer policy, CSP, cache behavior, and non-permissive CORS defaults. [intranet-runtime]
- [x] 8.9 Start the stack from empty volumes and verify migration sequencing, health state, seeded reference access through Nginx, and service isolation. [intranet-runtime, backend-platform, postgresql-persistence]

## 9. Add Backup, Restore, and Reconciliation

- [x] 9.1 Add a backup command that creates one timestamped recovery set containing a PostgreSQL dump, document/quarantine archive, and credential-free manifest. [intranet-runtime, postgresql-persistence, private-document-storage]
- [x] 9.2 Add manifest file counts, hashes, migration version, timestamps, component outcomes, and failure-safe incomplete-backup handling. [intranet-runtime]
- [x] 9.3 Add configurable daily scheduling and 30-day retention without deleting the latest valid recovery set. [intranet-runtime]
- [x] 9.4 Add a non-production restore command/runbook that restores database and files, validates schema state, and reconciles every document key/hash. [intranet-runtime, postgresql-persistence, private-document-storage]
- [x] 9.5 Execute a backup and clean-stack restore, inspect database/file reconciliation, and record whether the 24-hour RPO and 4-hour RTO procedure is achievable. [intranet-runtime]
- [x] 9.6 Document rollback to the existing frontend-only deployment, write freeze, compatible recovery-set selection, and preservation of untouched Access/Supabase sources. [intranet-runtime]

## 10. Update Security Gates and Existing Tests

- [x] 10.1 Review and update all existing unit tests affected by the new transport, configuration, Docker entrypoint, and architectural boundary without weakening their assertions. [All capabilities]
- [x] 10.2 Replace Supabase-only release-gate assumptions with API/PostgreSQL/private-storage checks while retaining legacy checks wherever an unchanged shipped path still uses Supabase. [postgresql-persistence, private-document-storage, intranet-runtime]
- [x] 10.3 Update `npm run security:rls` to verify the replacement database boundary: private networking, least-privilege runtime grants, no browser credentials, and denied unauthenticated/unauthorized production business routes. [backend-platform, postgresql-persistence]
- [x] 10.4 Update `npm run security:storage` to verify opaque private paths, quarantine denial, clean-only controlled downloads, traversal rejection, and absence of direct Nginx storage exposure. [private-document-storage, intranet-runtime]
- [x] 10.5 Update/add specs under `tests/security/` for fail-closed actor behavior, runtime grants, unsafe/unscanned files, redacted errors/logs, and unavailable internal paths. [backend-platform, postgresql-persistence, private-document-storage]
- [x] 10.6 Run `npm run security:rls`, inspect the actual PostgreSQL grants/routes, and resolve every unexpected accessible path. [backend-platform, postgresql-persistence]
- [x] 10.7 Run `npm run security:storage`, inspect quarantine/available state, and resolve every unexpected file exposure. [private-document-storage]
- [x] 10.8 Run the specs under `tests/security/` and confirm unauthenticated and unauthorized cases fail closed. [backend-platform, postgresql-persistence, private-document-storage]

## 11. Execute Unit, Integration, and End-to-End Validation

- [x] 11.1 Run the backend unit and architecture suites and resolve all failures. [backend-platform, durable-operations, private-document-storage]
- [x] 11.2 Run the backend PostgreSQL/storage integration suite against disposable real services and inspect final tables, constraints, grants, operation states, and file reconciliation. [postgresql-persistence, durable-operations, private-document-storage]
- [x] 11.3 Run the tagged real-ClamAV contract suite and verify signature/version evidence and infected-file denial. [private-document-storage]
- [x] 11.4 Run `npm test` and resolve all frontend unit-test regressions. [frontend-api-transport]
- [x] 11.5 Run `npm run test:integration`, then inspect the resulting database and storage state to ensure the new path creates only synthetic data and no unintended Supabase writes. [frontend-api-transport, postgresql-persistence, private-document-storage]
- [x] 11.6 Add and run a targeted Playwright same-origin reference-flow spec through Nginx, including success, not-found/problem mapping, and correlation behavior. [frontend-api-transport, backend-platform, intranet-runtime]
- [x] 11.7 Add and run targeted Playwright/security checks proving `/api` does not expose production business/document routes while authentication is deferred. [backend-platform, private-document-storage, intranet-runtime]
- [x] 11.8 Run the full `npm run e2e` suite against the unified stack and restore/reseed the synthetic development state afterward. [All capabilities]

## 12. Documentation and Final Quality Gates

- [x] 12.1 Update the README with prerequisites, the pinned .NET toolchain, Docker/Nginx startup, migration command, local URLs, volume locations, and troubleshooting. [backend-platform, intranet-runtime]
- [x] 12.2 Add operator documentation for configuration/secrets, health meanings, scanner limits/outages, backup/restore/reconciliation, retention, and rollback. [intranet-runtime, private-document-storage]
- [x] 12.3 Document the table-prefix registry, quoting rule, migration ownership, runtime grants, and explicit departure from Supabase/RLS for the new backend. [postgresql-persistence]
- [x] 12.4 Document that authentication, Access/business-data migration, production topology, SMB, and CSV product behavior are deferred to later changes; only reusable operation/download seams exist here. [All capabilities]
- [x] 12.5 Run backend restore/build, formatter verification, analyzers, unit, architecture, and integration tests from a clean state. [All capabilities]
- [x] 12.6 Run `npm run lint` and resolve all findings. [frontend-api-transport]
- [x] 12.7 Run `npm run format:check` and resolve all formatting differences. [All capabilities]
- [x] 12.8 Run the frontend production build and validate the generated API contract has no secrets or undocumented KTL-5 routes. [backend-platform, frontend-api-transport]
- [x] 12.9 Run `openspec validate ktl-5-dotnet-infrastructure --strict` and retain a final clean `git status` plus test/security/restore evidence for review. [All capabilities]
