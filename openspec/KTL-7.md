# KTL-7 — Access to PostgreSQL data migration and reconciliation

**Status:** Ready for OpenSpec planning
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (catalog tables must exist to resolve reference values)
**Blocks:** KTL-8 in practice — the candidate cutover has no data without it

## Summary

Migrate the authoritative candidate dataset from the legacy Access database
(`BBDD CVs.accdb`) into PostgreSQL, with a repeatable, re-runnable process and a
reconciliation report that proves nothing was lost or silently altered.

## Why

This ticket is sequenced **early**, ahead of the blueprint's original placement, for a
concrete reason: [candidate.service.ts:173](src/app/features/candidates/services/candidate.service.ts#L173)
seeds demo data whenever `localStorage` is empty, so the app currently always looks
populated. Point the SPA at an empty PostgreSQL and it shows an empty product. Every
per-slice cutover after this one is easier to validate against real data, and parity
checks are only meaningful once real data is in place.

Running it early also surfaces data-quality problems — free-text values that do not
resolve to catalog entries, missing consent dates, malformed contact details — while
there is still time to shape the schema around them, rather than after the schema is
fixed by four shipped slices.

## In scope

- An explicit, idempotent migration procedure (a CLI/one-shot tool or a durable
  operation on the KTL-5 worker — decide in design) that can be re-run without
  duplicating rows.
- Extraction from Access into a staging representation, then load into `CND_` and
  related tables.
- Reference-value resolution: map Access free-text language/program/skill/education/
  sector values onto `CAT_` catalog entries. Unmatched values are reported, not silently
  dropped and not silently auto-created.
- Consent and retention metadata: `receivedAt`, `consentAt`, `reviewDueAt` must be
  carried across, and rows missing them must be reported rather than defaulted.
- Document migration if the Access dataset references CV files: content passes through
  the KTL-5 quarantine and scanning pipeline, and only clean documents become available.
- A reconciliation report: row counts per entity, unresolved reference values, rejected
  rows with reasons, and document hash verification against stored keys.
- A documented rollback: how to return PostgreSQL to its pre-migration state.

## Out of scope

- Changing the candidate schema itself — KTL-8 owns the full field set. If this ticket
  runs first, it targets the KTL-8 schema and the two are sequenced accordingly in
  design.
- Ongoing/incremental synchronisation with Access. This is a one-directional, one-time
  migration, re-runnable only for correction.
- Retiring the Access database.
- Any frontend change.

## Personal-data and security impact

**This is the highest personal-data-exposure ticket in the sequence.** It moves the real
candidate dataset — names, contact details, CV documents, consent and retention metadata
— into the new store.

Required:

- Development and test runs use **synthetic fixtures**, never the production Access
  file. The reconciliation process must be provable without real data.
- No candidate details or document contents in logs or in the reconciliation report
  beyond the minimum needed to identify a rejected row (row identifier and field name,
  not values).
- Consent and retention metadata is preserved exactly; a row whose consent metadata
  cannot be established is rejected and reported, never defaulted to a permissive value.
- Logical-deletion state in the source, if any, is carried across as logical state.
- The staging representation is destroyed after a successful load and is never left on
  disk in an unprotected location.
- Documents fail closed: unscannable or infected content does not become available.

## Acceptance criteria

1. The migration runs end to end against a disposable PostgreSQL instance using
   synthetic fixtures, in automated tests.
2. Re-running the migration produces no duplicate rows.
3. The reconciliation report accounts for every source row as loaded, rejected, or
   skipped, with a reason for each non-loaded row.
4. Unresolved catalog values are reported and require an explicit decision; they are
   never auto-created silently.
5. Consent and retention metadata round-trips; rows lacking it are rejected.
6. Migrated documents exist in private storage under opaque keys, their stored hashes
   match, and no host path appears in any output.
7. No candidate personal data appears in logs.
8. The rollback procedure is documented and exercised at least once.
9. Backup/restore validation from KTL-5 still reconciles after the migration.

## Deferred decisions

- Migration vehicle: standalone CLI tool versus a durable operation on the KTL-5
  background worker. Decide in design based on dataset size and whether HR needs to
  trigger re-runs without a deployment.
- Whether the Access extraction runs on Windows with an ODBC/ACE driver or via a
  pre-exported intermediate format. This affects CI, since the driver is not available
  in a Linux container.

## Next step

```
/enrich-us openspec/KTL-7.md
/opsx:new
```
