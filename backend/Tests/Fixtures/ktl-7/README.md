# KTL-7 synthetic migration fixtures

An export set conforming to `docs/ktl-7/access-export-procedure.md`, built so that one run
over it exercises every outcome the migration can produce. **No production data.** Every
personal-data field carries a `SENTINEL…` token, so the no-leak test can assert that no
value reaches the logs or the reconciliation report by searching for that one prefix.

## Candidates and their intended outcome

| Source key | Intent                                    | Expected outcome                        |
| ---------- | ----------------------------------------- | --------------------------------------- |
| `C-001`    | Clean row, every relation, clean document | Loaded                                  |
| `C-002`    | Logically removed in the source           | Loaded, inactive, `DeletedAt` preserved |
| `C-003`    | No consent date                           | Rejected — consent metadata             |
| `C-004`    | Malformed email                           | Rejected — field validation             |
| `C-005`    | Language `Klingon` resolves to nothing    | Rejected — unresolved reference         |
| `C-006`    | Document hash does not match the file     | Loaded; its document rejected           |
| `C-007`    | Document the scanner refuses              | Loaded; its document rejected           |

Seven source rows: four loaded, three rejected, none skipped — so a run over this set
reconciles, and the arithmetic is easy to assert.

## Reference resolution paths

The language rows deliberately cover all three resolver steps and its failure:

| Row     | Value     | Resolves by                                   |
| ------- | --------- | --------------------------------------------- |
| `L-001` | `Inglés`  | Exact name match                              |
| `L-003` | `ingles`  | `CatalogName.DeriveCode` — casing and accents |
| `L-006` | `Ingl.`   | `mappings.csv` — operator decision            |
| `L-004` | `Klingon` | Nothing. Reported; rejects the owning row     |

`mappings.csv` sits outside `export/` on purpose: it is an operator decision about the
data, not part of what Access produced.

## Documents

`files/` holds three plain-text stand-ins for CVs — `.txt` is on the business allowlist and
is trivially scannable, so the fixtures test the pipeline rather than a parser.

- `cv-001.txt` — hash in `documents.csv` matches; expected to reach available storage.
- `cv-006.txt` — hash in `documents.csv` is deliberately all zeroes; expected to be rejected
  as a mismatch before it can be promoted.
- `cv-007.txt` — contains `SYNTHETIC-MALWARE-MARKER`, which the test scanner treats as
  infected. It is **not** a real EICAR string: a live antivirus on a developer machine
  would quarantine the fixture and break the checkout.

## Regenerating hashes

If a fixture file's content changes, its `Sha256` in `documents.csv` must be recomputed —
except for `cv-006.txt`, whose mismatch is the point.

```bash
sha256sum backend/Tests/Fixtures/ktl-7/export/files/cv-001.txt
```
