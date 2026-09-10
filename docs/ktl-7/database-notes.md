# KTL-7 database notes

Extends [`../ktl-5/database-conventions.md`](../ktl-5/database-conventions.md), which remains
the general rule set. Everything here is what KTL-7 added.

## Tables

| Table                       | Purpose                                                         |
| --------------------------- | --------------------------------------------------------------- |
| `"CND_Candidates"`          | expanded to the full business field set                         |
| `"CND_CandidateLanguages"`  | languages a candidate declares, with a level                    |
| `"CND_CandidatePrograms"`   | programs, with a level and years of experience                  |
| `"CND_CandidateEducation"`  | education records                                               |
| `"CND_CandidateExperience"` | work experience                                                 |
| `"CND_CandidateSkills"`     | skills, with a level                                            |
| `"CND_Documents"`           | existing table; gained `DocumentType`, `IsPrimary`, `SourceKey` |
| `"OPS_MigrationRuns"`       | one row per invocation of the migration tool                    |

`CAT_` in the KTL-5 registry is no longer "reserved for later slices" — KTL-6 filled it with
`"CAT_CatalogItems"`, and KTL-7 references it.

## Catalog references are composite

A relation row referencing a catalog entry does so through a **composite** foreign key onto
`"CAT_CatalogItems" (Id, Family)`, backed by the alternate key
`AK_CAT_CatalogItems_Id_Family`. Each relation table carries a family column pinned to its one
legal value by a check constraint, so a language reference cannot resolve to a sector.

A plain foreign key onto `Id` alone could not express that, and the migration is a bulk writer
that bypasses application validation — so the invariant belongs in the database.

## Invariants worth knowing

| Constraint                             | What it prevents                                                   |
| -------------------------------------- | ------------------------------------------------------------------ |
| `CK_CND_Candidates_Status`             | a status outside `new`/`available`/`in_process`/`hired`/`rejected` |
| `CK_CND_Candidates_Deleted`            | an inactive candidate with no removal timestamp, or the reverse    |
| `CK_CND_Candidates_SourceLoaded`       | a record claiming to be migration-loaded without provenance        |
| `UX_CND_Documents_CandidateId_Primary` | a second primary document for one candidate                        |
| `UX_*_SourceKey` (partial)             | two records claiming the same legacy source row                    |

## Provenance columns

`SourceKey` is the legacy row identifier a record was loaded from; it is null for anything the
application created, which is what keeps application records untouched by a re-run. It is not
personal data.

`SourceLoadedAtUtc` on `"CND_Candidates"` records when the migration last wrote the record. Any
later application write moves `UpdatedAtUtc` past it, which is how a re-run detects records it
must not silently overwrite. The check constraint keeps the pair honest.

## Grants

The migration that creates these tables ships the runtime grants with them:

- relation tables: `SELECT, INSERT, UPDATE, DELETE`, with `TRUNCATE` revoked. `DELETE` is
  needed because relation collections are replaced as whole sets.
- `"OPS_MigrationRuns"`: **no grant at all**, and an explicit `REVOKE ALL`. Migration history
  belongs to the operator running the tool, not to the API.

The runtime role still holds no schema-modification privilege; the initial migration's
`REVOKE CREATE ON SCHEMA public` continues to deny it.

## Staging

A run creates a `migration_staging` schema in the target database, copies the export into it
verbatim as text, and drops it on success. It holds the same personal data the export does, so
a failed run that retains it says so on stderr and the operator must drop it:

```sql
DROP SCHEMA migration_staging CASCADE;
```
