# Import reason codes (KTL-17)

Stable strings. The page translates them, operators match on them, and the database holds row
outcomes to the row-level set with a check constraint. The source of truth is
`backend/Domain/Import/ImportReasonCodes.cs`; a frontend test fails if any code there lacks Spanish
copy.

No code, and no report built from them, ever carries a value from a row.

## Row codes

Recorded on `ADM_ImportRowOutcomes` with the column (`Field`) they failed on. Rules run in this
order; a row reports the first one it fails.

| Code                   | Outcome  | Field           | Meaning and what to do                                                                                                   |
| ---------------------- | -------- | --------------- | ------------------------------------------------------------------------------------------------------------------------ |
| `row.shape_invalid`    | rejected | `row`           | The record has a different number of fields than the header — usually an unquoted comma inside a value. Quote the field. |
| `field.required`       | rejected | the column      | `first_name`, `last_name` or `email` is blank.                                                                           |
| `field.too_long`       | rejected | the column      | The value exceeds the column's bound in the [file contract](import-file-contract.md).                                    |
| `email.invalid`        | rejected | `email`         | Not an address shape (missing `@`, two `@`, no dotted domain, whitespace).                                               |
| `status.unknown`       | rejected | `status`        | Not one of `new`, `available`, `in_process`, `hired`, `rejected`.                                                        |
| `date.invalid`         | rejected | the date column | Not `yyyy-MM-dd`.                                                                                                        |
| `reference.malformed`  | rejected | `languages`     | An entry is not `Idioma:Nivel`.                                                                                          |
| `reference.unresolved` | rejected | `languages`     | A language or level matches no catalog entry. The value is listed on the batch; add it in Catálogos or fix the file.     |
| `reference.duplicate`  | rejected | `languages`     | The same language appears twice in the row.                                                                              |
| `candidate.refused`    | rejected | column or `row` | The create-candidate rules, or a database constraint, refused a row the import checks let through. Rare; report it.      |
| `candidate.duplicate`  | skipped  | `email`         | The person already exists, or appears earlier in the file (design D5). Not an error.                                     |

A `loaded` outcome carries no code. In the **validation** report it means "would load"; in the
**commit** report it means the candidate was created, and the outcome row references it.

## Batch codes

Recorded on `ADM_ImportBatches.FailureCode` when the file, not a row, is the problem. The batch
ends `failed`, `infected` or `unscannable`, and no row outcome is written. `FailureDetail` holds a
column name or a limit, never row data.

| Code                           | State         | Detail     | Meaning and what to do                                                                                             |
| ------------------------------ | ------------- | ---------- | ------------------------------------------------------------------------------------------------------------------ |
| `import.scan.infected`         | `infected`    | —          | ClamAV reported malware. Terminal: no retry admits the file. Do not re-upload it.                                  |
| `import.scan.unscannable`      | `unscannable` | —          | The scanner gave no verdict (unavailable, timeout). Check `/api/health/scanner`, then upload again as a new batch. |
| `import.file.content_mismatch` | `failed`      | —          | The content is not CSV text despite the name (zip, OLE, PDF, executable, control bytes).                           |
| `import.file.encoding_invalid` | `failed`      | —          | Not valid UTF-8. Re-save the file as "CSV UTF-8".                                                                  |
| `import.file.malformed`        | `failed`      | —          | Unbalanced quotes or a stray quote inside a field; the file cannot be parsed as CSV.                               |
| `import.file.too_large`        | `failed`      | —          | Over `Import:MaximumBytes` while reading.                                                                          |
| `import.file.empty`            | `failed`      | —          | No bytes at all.                                                                                                   |
| `import.header.missing`        | `failed`      | —          | No header line.                                                                                                    |
| `import.column.missing`        | `failed`      | the column | A required column is absent.                                                                                       |
| `import.column.unknown`        | `failed`      | the column | A column not in the contract (bounded, control characters stripped).                                               |
| `import.column.duplicate`      | `failed`      | the column | A column appears twice.                                                                                            |
| `import.rows.limit_exceeded`   | `failed`      | the limit  | More data rows than `Import:MaximumRows`. Split the file.                                                          |
| `import.file.missing`          | `failed`      | —          | The admitted file was not found when validation or commit ran.                                                     |
| `import.file.changed`          | `failed`      | —          | The file's SHA-256 no longer matches the one recorded at upload.                                                   |
| `import.file.type_not_allowed` | —             | —          | Upload refused before storing: the name does not end in `.csv`. No batch is created.                               |
| `import.run.interrupted`       | reserved      | —          | Reserved for an operator-closed run; not written automatically.                                                    |
| `import.actor.missing`         | `failed`      | —          | Commit refused: the batch has no stored uploader to name on the candidate audit events (KTL-19).                   |

## HTTP refusal codes

Returned as the `code` of a ProblemDetails response. The authorization refusals come first and
are identical for existing and missing batches.

| Status | Code                             | When                                                                                                         |
| ------ | -------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| 401    | —                                | No or invalid token.                                                                                         |
| 403    | `authorization.denied`           | Authenticated without `candidates.import`.                                                                   |
| 400    | `validation.failed`              | Bad paging, missing file part, wrong extension, empty or oversized file (the issue carries `import.file.*`). |
| 404    | `import.batch.not_found`         | No such batch.                                                                                               |
| 409    | `import.batch.not_ready`         | Validate before the scan finished.                                                                           |
| 409    | `import.batch.refused`           | Validate or commit an infected or unscannable batch.                                                         |
| 409    | `import.batch.not_validated`     | Commit before validation completed.                                                                          |
| 409    | `import.batch.has_rejected_rows` | Commit a batch with rejected rows.                                                                           |
| 409    | `import.batch.expired`           | Validate or commit after the file was purged.                                                                |
| 409    | `import.batch.version_conflict`  | The batch changed since the caller read its version.                                                         |
