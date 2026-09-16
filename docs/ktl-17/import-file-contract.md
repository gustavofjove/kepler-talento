# Candidate import file contract (KTL-17)

The file an HR administrator uploads on **Admin › Importación** to create candidates. This
contract is separate from the legacy Access export contract that `ktl-migrate` reads
(`docs/ktl-7/access-export-procedure.md`); the two share row semantics, not file formats.

## File

| Property      | Value                                                                                                                                                                   |
| ------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Accepted type | CSV only (`text/csv`), file name ending in `.csv`. Excel is a later follow-up (design D8).                                                                              |
| Encoding      | UTF-8, with or without a byte-order mark. UTF-16 and invalid UTF-8 are refused.                                                                                         |
| Delimiter     | Comma. A header that uses semicolons and no commas selects semicolon, which is what a spreadsheet saved as CSV in a Spanish locale produces.                            |
| Quoting       | RFC 4180: a field may be wrapped in double quotes; a quote inside it is doubled (`""`); a quoted field may contain delimiters and line breaks.                          |
| Header        | Required, first non-blank line. Column names are matched after trimming and lower-casing.                                                                               |
| Blank lines   | Ignored, and not counted as rows.                                                                                                                                       |
| Maximum rows  | **2000** data rows (`Import:MaximumRows`). Enforced by the server while reading; a file over the limit is refused before any row is judged.                             |
| Maximum size  | **5 MiB** (`Import:MaximumBytes`, never above the 20 MiB storage ceiling). Enforced while reading.                                                                      |
| Content       | Must actually be text. A `.csv` that is really a zip or OLE container (xlsx, xls, docx), a PDF or an executable is refused after the antivirus scan and before parsing. |

A **data row number** is the 1-based position of a non-blank record after the header. It is the
identifier every report uses ("row 47"), and it carries no personal data.

## Columns

| Column          | Required | Format                                                                                    | Bound |
| --------------- | -------- | ----------------------------------------------------------------------------------------- | ----- |
| `first_name`    | yes      | Text, non-blank                                                                           | 120   |
| `last_name`     | yes      | Text, non-blank                                                                           | 180   |
| `email`         | yes      | An address shape: one `@`, a dotted domain, no whitespace. Deliverability is not checked. | 255   |
| `phone`         | no       | Text                                                                                      | 40    |
| `location`      | no       | Text                                                                                      | 160   |
| `province`      | no       | Text                                                                                      | 120   |
| `country`       | no       | Text                                                                                      | 120   |
| `availability`  | no       | Text                                                                                      | 120   |
| `status`        | no       | One of `new`, `available`, `in_process`, `hired`, `rejected`. Blank means `new`.          | —     |
| `source`        | no       | Text                                                                                      | 120   |
| `notes`         | no       | Text, kept exactly as written                                                             | 4000  |
| `received_at`   | no       | `yyyy-MM-dd`. Blank means absent — never today.                                           | —     |
| `consent_at`    | no       | `yyyy-MM-dd`. Blank means absent — never today.                                           | —     |
| `review_due_at` | no       | `yyyy-MM-dd`. Blank means absent — nothing is derived.                                    | —     |
| `languages`     | no       | `Idioma:Nivel` pairs separated by `;`, e.g. `Inglés:B2;Francés:A1`                        | 1000  |

Other text columns are trimmed. Any column not in this table is refused as a structural problem
(`import.column.unknown`) rather than silently ignored — a misspelt `emial` would otherwise drop
every address in the file. A duplicated column is refused too.

Dates are stricter than the candidate form on purpose: a locale format such as `17/03/2026`
would otherwise risk swapping day and month.

## Catalog references

`languages` is resolved against the existing **language** and **language level** catalogs using
the same resolver `ktl-migrate` uses, in order:

1. exact name (`Inglés`);
2. the catalog's own normalization — case, accents and spacing (`ingles`, `INGLÉS`);
3. nothing else. There is no fuzzy matching, and no operator mapping file for imports.

Retired (inactive) catalog entries still resolve, as they do for migration. A value that
resolves to nothing rejects the row (`reference.unresolved`) and is listed on the batch with its
catalog family and occurrence count. **No import ever creates, renames or reactivates a catalog
entry**: add the value on **Admin › Catálogos** or correct the file, then upload again.

## Row rules

Each data row ends in exactly one outcome — **loaded**, **rejected** or **skipped** — and the three
counts always add up to the row count. The rules run in a fixed order and the first failure is the
one reported; see [`row-reason-codes.md`](row-reason-codes.md). A row is rejected with a column name
and a code, never with the value it contained.

After the import-specific checks, every row also passes through the same create-candidate
validator a direct write uses. A row the candidate rules refuse is rejected; it is never loaded in
a weakened form.

## Duplicate rule (design D5)

Two rows describe **the same person** when their `email` values are equal after trimming and
lower-casing. Names are not compared.

- The first loadable row for an address loads; every later row for it in the same file is
  **skipped** with `candidate.duplicate`.
- A row whose address already belongs to a candidate — active or deactivated — is **skipped**
  with `candidate.duplicate`.
- A row that is rejected for another reason does not claim its address, so a later correct row
  for the same person still loads.

Skipped means "understood and deliberately not loaded", which is why uploading an already
committed file again produces a batch of skips rather than a wall of failures. Import never
updates an existing candidate.

## Two steps

1. **Upload and validate.** The file is stored under an opaque key in quarantine and scanned.
   Only after a clean verdict is it read. Validation writes the per-row report and changes no
   candidate.
2. **Commit.** Allowed only for a `validated` batch with **zero rejected rows** whose file is still
   retained. Rows are re-read and re-judged, because the catalog or the candidate table may have
   changed since validation; the file's SHA-256 must still match the one recorded at upload.

## Example

```csv
first_name,last_name,email,phone,status,consent_at,languages
Ana,Ruiz,ana.ruiz@example.test,600111222,available,2026-03-18,Inglés:B2;Francés:A1
Luis,"Gil Pérez",luis.gil@example.test,,,,
```
