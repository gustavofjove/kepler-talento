# KTL-13 — Document storage volume left with root-owned candidate folders

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-9 (candidate documents), KTL-5 (runtime and entrypoint)

## Summary

Every CV upload in the development stack fails with HTTP 500 because the API, running as
`app`, cannot create folders in a document storage volume whose candidate folders are
owned by `root`. Repair the volume, make the ownership repair hold for every start instead
of once, and stop root-owned writes from getting back in unnoticed.

This is an operational fix inside existing requirements. It is not expected to need an
OpenSpec change; see [Why no OpenSpec change](#why-no-openspec-change).

## Symptoms

- `POST /api/candidates/{id}/documents` responds 500. The API log shows a
  `System.UnauthorizedAccessException` (inner `IOException: Permission denied`) from
  `FileSystemDocumentStorage.WriteQuarantineAsync`:
  `Access to the path '/var/lib/kepler-talento/documents/quarantine/candidates/<id>' is denied`.
- The candidate detail screen keeps showing "Sin CV adjunto." after "Subir CV", with no
  error message to the user.
- 7 e2e specs fail (observed 2026-09-14): the three upload flows
  (`candidate-api-cutover` documents, `candidate-documents`, `security-ops` upload) and
  four advanced-search/preset specs. The latter fail because the shared "Laura Garcia"
  seed gets its principal CV through the same upload.
- `GET /api/health/ready` still reports healthy, so nothing flags the problem before a
  user tries to upload.

## Root cause

Observed in the running `documents` volume on 2026-09-14:

| Path                                                                   | Owner      | Created                   |
| ---------------------------------------------------------------------- | ---------- | ------------------------- |
| `/var/lib/kepler-talento/documents`                                    | `app`      | 2026-09-11 11:24          |
| `.app-ownership-v1` (marker)                                           | `app`      | 2026-09-11 11:24          |
| `quarantine/`, `available/`                                            | `app`      | re-owned 2026-09-14 08:36 |
| `quarantine/candidates/`, `available/candidates/` and everything below | **`root`** | 2026-09-11 11:27          |

1867 entries in the volume are owned by `root`, all written at 11:27 on 2026-09-11: 375
candidate folders plus their document folders and `content` files. Not a single candidate
folder is owned by `app`.

The API process runs as `app` (uid 1654). `backend/docker-entrypoint.sh` repairs volume
ownership with `chown -R app:app`, but **only once**. After the first start it writes
`.app-ownership-v1` and skips the repair on every later start. The marker was written at
11:24, three minutes before the root-owned tree appeared, so the repair never ran over it.

Every compose service built from `backend/Dockerfile` (`api`, `migrator`) goes through that
entrypoint and drops to `app` with `su-exec`. The root-owned tree was therefore written by
something that bypassed the entrypoint. The timing matches loading document data into the
volume, such as a KTL-7 `ktl-migrate load` or a KTL-11 synthetic set. `docker compose exec`,
`docker exec` and `docker compose run --entrypoint …` all run as `root` by default. The
exact command was not recorded; confirming it is part of this ticket.

The runbooks do not say which user a tool writing into the volume must run as.

## In scope

- **Repair the existing volume.** Re-own the `documents` volume to `app:app` recursively.
  Use a documented command (for example through the `file-tools` operations service) so
  anyone with an affected volume can run it, not a one-off shell session.
- **Make the entrypoint repair hold on every start.** Replace the one-time marker with a
  check that runs on each start and re-owns only entries not already owned by `app` (for
  example `find "$storage_root" ! -user app -exec chown app:app {} +`). It stays cheap on
  a healthy volume. Keep `su-exec` dropping privileges before any application code runs.
  Remove or ignore `.app-ownership-v1`.
- **Make readiness reflect writability.** `GET /api/health/ready` already covers storage.
  It must fail when the API cannot create a directory and write a file under
  `quarantine/candidates/`, not only when the root folder is writable. This tightens the
  existing check without adding a new endpoint.
- **Identify and document the writer.** Confirm which command produced the root-owned
  tree. In `docs/ktl-7/migration-runbook.md`, and any KTL-11 test-data instructions, state
  that tools writing into the `documents` volume run as `app`: through the image
  entrypoint, or `--user app` for `exec`/`run`. Show the exact command.
- **Surface the upload failure to the user.** The candidate documents section must show
  an error message when the upload request fails, instead of silently keeping "Sin CV
  adjunto." Keep this to wiring the existing failure path; no new copy design.

## Out of scope

- Changing the storage layout, storage keys, or the quarantine → available flow.
- Changing the upload API contract or error codes.
- Production or intranet volume provisioning beyond documenting the ownership rule. The
  entrypoint change applies wherever the image runs.
- ClamAV behaviour. The scanner is healthy; failures happen before it is reached.
- The frontend e2e environment. Already fixed: Vite serves the working tree on :5173 with
  an `/api` proxy to the Compose nginx, and Playwright targets :5173, so e2e no longer
  reuses the nginx image's stale bundle on :4200.

## Personal-data impact

The volume holds candidate CVs, which are personal data. The fix changes only file
ownership and the user the process writes as. Documents stay outside the webroot under
opaque keys, quarantine still precedes availability, and nothing becomes readable by
another route. Running tools as `app` instead of `root` also narrows who can write CV
files (principle 3). The repair command must not print or copy file contents.

## Acceptance criteria

- On the affected development volume, after the repair,
  `find /var/lib/kepler-talento/documents ! -user app` returns nothing.
- Given a volume with a root-owned folder created after the first start, restarting the
  `api` container re-owns it without manual steps.
- With a deliberately unwritable `quarantine/candidates/`, `GET /api/health/ready` reports
  unhealthy and names storage.
- `POST /api/candidates/{id}/documents` with a valid PDF returns success, and the API log
  has no `Permission denied`.
- A failed upload shows an error in the candidate documents section.
- The runbook states the user that writing tools must run as, with the exact command.
- `npm run e2e` passes the three upload specs and the four advanced-search/preset specs
  that failed on 2026-09-14, against a freshly built stack.
- `npm run security:storage` and `npm run test:security` still pass.

## Why no OpenSpec change

The existing specs already require what this ticket restores. Documents are stored
privately and uploads succeed (`candidate-management`, "Candidate document metadata";
`private-document-storage`), and readiness reflects storage (`intranet-runtime`,
"Dependency-aware health"). The fix brings the running system back in line with them. It
adds no requirement and changes no contract or observable behaviour of a working
installation.

Open an OpenSpec change instead if any of these turn out to be needed:

- The readiness response shape or status semantics have to change, beyond failing when
  storage is not writable.
- The upload error shown to the user needs new copy or a new error contract.
- The fix requires a storage layout or ownership model change, such as a different uid,
  per-directory permissions, or a separate writer role.
