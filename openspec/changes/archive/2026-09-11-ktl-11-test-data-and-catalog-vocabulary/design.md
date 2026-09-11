# Design — KTL-11

## Decision 1: Generate the export contract, not database rows

The generator writes the seven CSV files of `docs/ktl-7/access-export-procedure.md` and lets
`ktl-migrate` load them, rather than inserting candidates directly.

Writing rows directly would have been less work and would have needed no ClamAV, no export
contract and no reconciliation. It was rejected because it creates a second way for candidate
data to enter the database — one that skips validation, reference resolution, per-candidate
transactional load, consent checks and the quarantine-and-scan pipeline. The migration boundary
exists precisely so that there is one such path. A developer tool is not a reason to open a
second one, and a generated set that loads through the real path also exercises that path.

The consequence is that generated data must satisfy every migration rule — which is what makes
the "no rejections" acceptance criterion meaningful rather than decorative.

## Decision 2: Draw references from the seed itself

The generator takes its catalog values from `CatalogSeedData` rather than from its own list.

A private list would drift from the seed, and the failure would surface as rejected rows in a
developer's test data with no obvious cause. Taking both from one declaration makes drift
impossible by construction. The same reasoning applies to the column lists, which come from
`ExportContract` rather than being restated.

The cost is that the generator cannot produce unresolvable values, so it cannot exercise the
resolver's reported-and-rejected path. That path is already covered by the KTL-7 fixture
(`Klingon`), which is why those fixtures stay.

## Decision 3: Explicit codes in the seed, not automatic uniquification

`(Family, Code)` is unique, and `CatalogName.DeriveCode` folds every non-alphanumeric character
to a separator and drops empty segments — so `C#` and `C++` both derive `C`. The widened
program vocabulary contains both.

The API resolves this class of collision by uniquifying the derived code
(`business-catalogs`, "Catalog value creation"). The seed deliberately does not. An
API-created value arrives from an administrator at runtime, where refusing is worse than
adjusting; a seeded value is authored in source, where a collision is an authoring mistake and
an adjusted code would be a silent, permanent oddity nobody chose. `CSHARP` and `CPLUSPLUS` are
chosen and legible; `C` and `C_2` would be neither.

Resolution is unaffected either way: the resolver matches on exact name before code.

Because the collision would otherwise fail at deployment against a live database, it is
asserted in a unit test over the seed instead.

## Decision 4: Determinism over realism where they conflict

All generator randomness comes from one seeded `Random`, so a seed reproduces a set exactly.

Relation values are drawn independently, which produces occasional incoherent rows — a backend
developer at a foundry in the `Banca y seguros` sector. Correlating position, sector and company
was rejected for now: it adds a correlation table to maintain, and the data's purpose is to
exercise search, filters, sorting and accent folding, none of which care. It is recorded as a
deferred decision because it does matter for demonstrating a single record to the client.

## Decision 5: Leave per-family seeding alone

`SeedCatalogsAsync` seeds a family only when it holds no rows, so a widened vocabulary does not
reach an already-seeded database.

Changing this to top up missing values would have made the widening reach existing databases —
and would have broken the guarantee the behaviour exists for, that the seed cannot resurrect
values an administrator deliberately removed. The spec scenario "Seed runs again" is correct as
written. The consequence is operational, not behavioural: a database that predates a vocabulary
change must be reset to pick it up, which is acceptable for development data and is recorded in
the KTL-11 brief rather than hidden.
