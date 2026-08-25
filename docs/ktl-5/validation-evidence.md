# KTL-5 validation evidence

Validation date: 2026-08-25. All fixtures and runtime records used below are synthetic.

## Builds and automated tests

- `.NET 10.0.102` restore and Release build: passed with 0 warnings and 0 errors.
- backend unit/architecture suite: 24 passed.
- backend integration suite: 12 passed, including disposable PostgreSQL 17.6 and real
  ClamAV 1.4.3 containers.
- frontend/unit/security/integration suite: 149 passed.
- dedicated legacy integration command: 15 passed.
- Playwright through the unified Nginx stack: 49 passed, including the KTL-5 reference,
  problem/correlation, and unavailable-route checks.
- ESLint and repository Prettier check: passed after applying the repository formatter to
  the 110 pre-existing baseline differences.
- OpenSpec strict validation: passed.
- consolidated `npm run release:gate`: passed, including both real-service backend suites
  and the replacement database/storage security gates.

The real PostgreSQL suite inspected exact quoted `CND_`, `OPS_`, and `AUD_` tables,
constraints, seed idempotence, runtime grants, `xmin` concurrency, single-owner claiming,
lease recovery, retry exhaustion, idempotent completion, exact correlation lookup,
missing/orphan/stale storage reconciliation, and scanner-outage behavior. The real ClamAV
contract accepted clean synthetic text and detected the EICAR test signature.

## Empty-stack and boundary inspection

The Compose stack was built with its pinned images and started from newly created empty
volumes. The one-shot migrator completed before the API; PostgreSQL, ClamAV, API readiness,
and Nginx all became healthy. Only Nginx published a host port. PostgreSQL and ClamAV
reported container ports only on the internal network.

Actual `ktl_runtime` grants were inspected: DML only on `"CND_Candidates"`,
`"CND_Documents"`, `"OPS_Operations"`, and `"AUD_Events"`, plus read-only access to
`"__EFMigrationsHistory"`. No truncate or schema-alter privilege was present.

The generated Development OpenAPI document contained only platform, health, and reference
paths. Every operation documented `X-Correlation-ID`; no authentication scheme, password,
storage key, or Compose credential appeared. Production HTTP-pipeline tests separately
proved that reference routes are absent and that the Development actor prevents startup.

## Backup and clean restore drill

The first backup attempt intentionally remained marked `incomplete` when a PowerShell to
psql quoting defect was found. After correcting the command construction, recovery set
`20260825T095149Z` completed with database dump, document archive, migration version,
sizes, and SHA-256 hashes.

The recovery set included a synthetic clean document under an opaque key. The validation
containers and their three synthetic volumes were removed, PostgreSQL and document volumes
were recreated, and the set was restored. The restore command completed in approximately
4.44 seconds; migration validation and metadata/file SHA-256 reconciliation passed. The
restored controlled download returned the 27-byte synthetic body as a sanitized attachment
with `private, no-store`, `nosniff`, and no internal path.

After verification, the rebuilt one-shot reconciliation container was rerun against this
restored set. It checked one document and reported zero missing objects, hash mismatches,
orphans, or stale quarantine objects. The live Development API also returned a redacted
empty result for exact correlation lookup, rejected an unsafe correlation value, and
documented both operation lookup modes in OpenAPI.

This small validation set demonstrates that the documented daily/24-hour RPO procedure and
4-hour RTO are operationally achievable. A production-sized rehearsal remains an operator
responsibility once production topology and data volume are known.
