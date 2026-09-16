# Release notes — KTL-17 server-side candidate import

## The previous import created no candidates

Until this release, **Admin › Importación** looked like a working feature and was not one. It read
the CSV in the browser, checked two names and an email shape, and wrote a batch record to the
browser's local storage. **No candidate was ever created.** "Confirmar commit" changed that local
record's status and showed a number of loaded rows without writing anything anywhere.

If you "imported" candidates with an earlier build, they do not exist. Upload the file again.

## What changes for users

- **Committing now really creates candidates**, through the same rules as creating one by hand, with
  the same audit trail. It therefore takes time and **can really fail** — a file refused by the
  antivirus, a column missing, a catalog value that does not exist. The page shows the batch's state
  while the server works and says why a batch did not go through.
- **Two steps, as before.** _Subir y validar_ uploads the file and runs a dry run that changes
  nothing; the row report lists each problem by row number, column and reason. _Confirmar carga_ is
  enabled only when no row is rejected. Fix the file and upload it again if any are.
- **The antivirus checks every file first.** Nothing in the file is read until it is clean. A file
  it rejects, or cannot check, cannot be validated or loaded.
- **People who already exist are skipped, not duplicated.** A row whose email already belongs to a
  candidate, or appears earlier in the same file, is reported as _omitida_. Uploading the same file
  twice is allowed, and the page warns that an identical file was already imported.
- **Languages can be imported** as `Idioma:Nivel` pairs. They must already exist in Catálogos;
  import never adds catalog values.
- **Batch history is shared and kept on the server**, most recent first. You see the file name of the
  batches you uploaded; other people's show as uploaded by someone else.
- **Uploaded files are deleted 30 days after the batch closes.** The counts and the row report stay.
- The downloadable report contains row numbers, columns and reason codes only.

The file format is documented in [`import-file-contract.md`](import-file-contract.md). The required
columns are `first_name`, `last_name` and `email` — **email is now required**, because it is how the
import recognises a person who already exists.

## Local history is discarded

On first start of this build each browser deletes its `rrhh.import.batches.v1` entry. It is not
migrated to the server: those records described imports that never happened, and carrying them over
would give a fabricated history the appearance of a real one. Server history starts empty.

## Access and permissions

Every import endpoint requires `candidates.import` on the server, checked before the request is read.
Only `rrhh_admin` holds it by default. Being able to create candidates by hand does not grant it, and
holding it does not grant reading candidates.

## For operators

- Migration `AddImportBatches` adds `ADM_ImportBatches` and `ADM_ImportRowOutcomes`; it runs through
  the usual `migrator` container.
- New configuration section `Import` (limits, retention, purge interval), new durable operation types
  `import.*`, and a `--purge-imports` entry point. See the [runbook](runbook.md).
- The durable operation worker now renews a claim's lease through its own database scope. Before, a
  handler running longer than half a lease failed at its first renewal; no existing handler ran that
  long, but a large import commit does.
