# KTL-11 — Synthetic test data generation and catalog vocabulary widening

**Status:** Implemented on the working branch, pending spec
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (catalogs), KTL-7 (export contract and migration tool)

## Summary

Give developers a repeatable way to fill a development database with realistic candidate
data, and widen the seeded catalog vocabulary to the one the business actually recruits
for.

## Why

There was no usable test data. The KTL-7 fixtures are built to exercise migration
outcomes, not to populate a product: seven candidates, three of them deliberately
rejected, every personal field a `SENTINEL…` token. The legacy Access database turned out
to be a skeleton — two candidates — so it is not a source either. What was left in the
shared development database was E2E residue: 163 rows named `Test<epoch>`, `Doc<epoch>`,
`Filtro<epoch>`, all status `new`, one with an email.

Widening the catalogs is the same problem seen from the other side. `CatalogSeedData` held
five programs and five education types, while the Access `Lista_*` tables hold 49 and 22 —
welding certifications, CNC controls, PLC and CAD. Search and filters tested against six
sectors and five programs prove very little.

## In scope

- `ktl-testdata`, a developer tool writing the seven-CSV export contract plus hashed CV
  files, consumable by `ktl-migrate` unchanged.
- Catalog references drawn from `CatalogSeedData` itself, so a run reconciles with no
  unresolved values and no rejected rows.
- Deterministic output: one seed reproduces a set byte for byte.
- `CatalogSeedData` widened from the Access `Lista_*` vocabulary.
- An explicit code on a seeded value, for names the derive rule cannot tell apart.

## Out of scope

- Loading anything into a database that also holds real candidate data.
- Replacing the KTL-7 fixtures. They test migration outcomes and stay as they are.
- Correlating position, sector and company within a generated experience row — see
  deferred decisions.
- Any frontend change.

## Personal-data impact

The generator writes fabricated personal data that is deliberately plausible rather than
obviously synthetic, which is the point — accent folding, sort order and free-text search
behave differently on `SENTINELNOMBRE01` than on `Rocío Alonso Díaz`. It follows that it
must never be presented as, or mixed with, real candidate data. Addresses use the reserved
`example.invalid` domain so no generated address can reach a mailbox.

## Constraints discovered

- `(Family, Code)` is unique and `CatalogName.DeriveCode` folds punctuation to separators,
  then drops empty segments — so `C#` and `C++` both derive `C`. Any two names differing
  only in trailing punctuation collide, and the seed fails at deployment against a live
  database rather than in a test.
- `SeedCatalogsAsync` seeds per family and skips a family holding any rows, deliberately,
  so it cannot resurrect values an administrator removed. A widened vocabulary therefore
  does not reach a database that has already been seeded; that database must be reset.

## Acceptance criteria

1. A generated set passes `RowValidator` with no row-level problems.
2. Every generated catalog reference resolves against the seeded vocabulary.
3. Every document hash in the manifest matches the file it names.
4. The same seed reproduces the same set.
5. `ktl-migrate load` over a generated set reconciles: every source row loaded, none
   rejected, none skipped.
6. Seeded codes and normalized names are unique within each family.
7. Catalog tests assert against the seed rather than pinning a vocabulary size, so
   widening a family does not fail them.

## Deferred decisions

- Whether generated experience rows should correlate position, sector and company.
  Independent draws produce combinations like `Desarrollador backend` at a foundry in the
  `Banca y seguros` sector. Adequate for exercising search, filters and paging; wrong for
  demonstrating a single record to the client.
- Whether the generator should be able to target a vocabulary wider than the seed, for
  deliberately exercising the resolver's unresolved-value path.

## Next step

```
/enrich-us openspec/KTL-11.md
/opsx:new
```
