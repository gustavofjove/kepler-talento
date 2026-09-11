## Why

There is no usable development dataset. The KTL-7 fixtures exist to exercise migration
outcomes, not to populate a product: seven candidates, three deliberately rejected, every
personal field a `SENTINEL…` token. The legacy Access database is not a source either — it holds
two candidate rows. What remained in the shared development database was end-to-end residue:
163 rows named `Test<epoch>`, `Doc<epoch>`, `Filtro<epoch>`, all status `new`, one with an
email address. Search, filters, paging and the saved presets delivered by KTL-10 cannot be
judged against any of that.

The seeded catalog vocabulary is the same problem from the other side. `CatalogSeedData` held
five programs and five education types while the Access `Lista_*` tables — the vocabulary the
business actually recruits against — hold 49 and 22, including welding certifications, CNC
controls, PLC and CAD. A filter exercised over six sectors and five programs demonstrates very
little about a filter.

## What Changes

- Add `ktl-testdata`, an operator/developer tool that writes the KTL-7 export contract — the
  seven CSV files plus a `files/` directory of CV stand-ins with matching SHA-256 hashes — so
  that `ktl-migrate` consumes it unchanged and no second ingestion path exists.
- Draw every generated catalog reference from the seed itself, so a run reconciles with no
  unresolved values and no rejected rows; a rejection then means a real defect rather than
  fixture drift.
- Make generation deterministic: one seed reproduces one set byte for byte, so a dataset that
  exposes a defect travels as a number.
- Widen `CatalogSeedData` to the Access `Lista_*` vocabulary, appending rather than reordering
  so existing values keep their documented positions.
- Allow a seeded value to carry an explicit code where derivation cannot distinguish two names
  in one family, and detect such collisions before deployment rather than as a failed insert.
- Assert catalog behaviour against the seed rather than against a pinned vocabulary size, so
  widening a family does not fail tests that are not about vocabulary size.
- Actors: developers and testers populating a disposable database; no end user is involved and
  no production path changes.
- Assumptions and edges: the generator writes fabricated personal data that is deliberately
  plausible, and must never be loaded into a database holding real candidates; addresses use
  the reserved `example.invalid` domain; a widened vocabulary does not reach an
  already-seeded database, which must be reset.
- Measurable success: a generated set passes row validation with no problems, every reference
  resolves, every document hash matches, the same seed reproduces the same set, and
  `ktl-migrate load` reconciles with every source row loaded and none rejected or skipped.

## Capabilities

### New Capabilities

None. The generator produces an artifact an existing capability already defines; it introduces
no runtime behaviour and is not reachable over HTTP.

### Modified Capabilities

- `business-catalogs`: the deployment seed gains an explicit rule for codes — derived by
  default, explicit where derivation cannot distinguish two names, unique within a family, and
  never silently uniquified as an API-created value is.

## Impact

- Backend: `CatalogSeedData` and `DatabaseInitializer`; a new `Tools/TestDataGenerator` project
  referencing `Infrastructure` and `DataMigration`, added to the solution and to the approved
  project-dependency graph. No production project references it, so the fabrication path stays
  as unreachable from the API as the migration tool is.
- Tests: seed collision guards, generated-set validation and resolution guards, and catalog
  tests unpinned from the seeded vocabulary size.
- Personal data: principle 1 is unaffected in production — nothing here changes what the
  application exposes. The obligation this change adds is directional: generated data is
  fabricated and must not be mixed with, or presented as, real candidate data.
- Dependencies: none added. `legacy-data-migration`'s existing synthetic-fixtures requirement
  is satisfied before and after; the KTL-7 fixtures remain the migration suite's fixtures.
