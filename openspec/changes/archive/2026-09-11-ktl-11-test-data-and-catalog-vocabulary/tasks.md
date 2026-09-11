## 1. Catalog Vocabulary

- [x] 1.1 Widen `CatalogSeedData` to the Access `Lista_*` vocabulary — programs, education types including metal-sector certifications, sectors, skills and languages — appending so existing values keep their documented order. (Proposal: vocabulary the business recruits against)
- [x] 1.2 Add an optional explicit code to a seeded value, derived by default, for names the derivation cannot distinguish. (Catalogs: seeded codes; Design decision 3)
- [x] 1.3 Seed from the value's own code rather than re-deriving it in `DatabaseInitializer`. (Catalogs: seeded codes)
- [x] 1.4 Assert seeded codes and accent-folded names are unique per family, and that every known family is seeded. (Catalogs: collision detected before deployment)
- [x] 1.5 Leave per-family seeding semantics unchanged; record the reset consequence rather than weakening the administrator-edit guarantee. (Catalogs: "Seed runs again"; Design decision 5)

## 2. Test Data Generator

- [x] 2.1 Add the `Tools/TestDataGenerator` project (`ktl-testdata`) referencing `Infrastructure` and `DataMigration`; register it in the solution. (Proposal: generate the export contract)
- [x] 2.2 Take column lists from `ExportContract` so generator and loader cannot drift. (Design decision 2)
- [x] 2.3 Draw every catalog reference from `CatalogSeedData` so a run resolves completely. (Design decision 2)
- [x] 2.4 Write RFC 4180 CSV as `CsvDocument` parses it — CRLF, UTF-8 without BOM, quoting only where required. (Migration: documented export contract)
- [x] 2.5 Generate plausible Spanish identity, location and free-text values, with `example.invalid` addresses. (Proposal: personal-data direction)
- [x] 2.6 Satisfy every row rule: consent present, `DeletedAt` consistent with `IsActive`, non-overlapping experience periods, an open period only where current, integer and date encodings, distinct relation values per candidate. (Migration: validate before writing)
- [x] 2.7 Write CV stand-ins and record matching SHA-256 hashes in the manifest. (Migration: document hash verification)
- [x] 2.8 Drive all randomness from one seed so a set is reproducible. (Design decision 4)
- [x] 2.9 Refuse to overwrite a non-empty output directory without `--force`, and state in usage that the output is fabricated personal data. (Proposal: must not mix with real data)

## 3. Architecture and Boundaries

- [x] 3.1 Add the generator to the approved project-dependency graph; confirm no production project references it. (Proposal: unreachable over HTTP)

## 4. Evidence

- [x] 4.1 Assert a generated set produces no row-level problems from `RowValidator`. (Acceptance 1)
- [x] 4.2 Assert every generated reference resolves against the seeded vocabulary. (Acceptance 2)
- [x] 4.3 Assert every document hash matches the file it names. (Acceptance 3)
- [x] 4.4 Assert the same seed reproduces the same set. (Acceptance 4)
- [x] 4.5 Unpin catalog tests from the seeded vocabulary size, deriving expected names, counts and sort order from the seed. (Acceptance 7)
- [x] 4.6 Run `ktl-migrate validate` and `load` over a 500-candidate set against a disposable database and confirm reconciliation with no rejections or skips. (Acceptance 5)
- [x] 4.7 Confirm loaded documents reach `Clean` through the real scanner, and that the API serves candidates with accents intact. (Acceptance 5; principle 4)

## 5. Specification

- [x] 5.1 Sync the `business-catalogs` delta into the main spec and archive this change. (Principle 5)
