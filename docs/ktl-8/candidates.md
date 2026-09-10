# KTL-8 candidates

The candidate aggregate is owned by the API. The browser is no longer the system of record
for candidate data, holds no copy of it, and has no local fallback.

## Endpoints

| Method | Route                                    | Capability          |
| ------ | ---------------------------------------- | ------------------- |
| GET    | `/api/candidates?includeInactive={bool}` | `candidates.read`   |
| GET    | `/api/candidates/{id}`                   | `candidates.read`   |
| POST   | `/api/candidates`                        | `candidates.create` |
| PUT    | `/api/candidates/{id}`                   | `candidates.update` |
| PUT    | `/api/candidates/{id}/active`            | `candidates.delete` |
| PUT    | `/api/candidates/{id}/languages`         | `candidates.update` |
| PUT    | `/api/candidates/{id}/programs`          | `candidates.update` |
| PUT    | `/api/candidates/{id}/education`         | `candidates.update` |
| PUT    | `/api/candidates/{id}/experience`        | `candidates.update` |
| PUT    | `/api/candidates/{id}/skills`            | `candidates.update` |
| PUT    | `/api/candidates/{id}/documents`         | `candidates.update` |

There is deliberately **no `DELETE`** anywhere in the group. A candidate is removed
logically through the `active` sub-resource, which clears `IsActive` and records
`DeletedAtUtc`; restoring reverses both. `candidates.delete` governs removal _and_
restoration, because restoring a withdrawn record is as consequential as withdrawing it.

The rule also holds at the database: the runtime role holds no `DELETE` on
`CND_Candidates`, and no `TRUNCATE` on any candidate table. It keeps `DELETE` on the
relation and document tables, where replacing a collection genuinely removes rows.

### Reads

`GET /api/candidates/{id}` returns the complete aggregate — the field set plus the
language, program, education, experience, skill and document collections — and does **not**
filter on the active state: a removed candidate stays retrievable and restorable.

The list returns a summary projection with no collections, plus `documentCount` and
`primaryDocumentId` so the list and search screens can report and filter on "tiene CV"
without loading six collections per row. It excludes removed candidates unless
`includeInactive=true`.

### Collection writes

Each collection is written as a **complete set** against the owning candidate's `version`.
A single relation has no concurrency token of its own, so per-item endpoints would let two
editors interleave into a collection neither intended. The candidate's row version is the
token for everything it owns.

Relation values travel as catalog **names** (`"Inglés"`, `"B2"`), not identifiers, and are
resolved to catalog entries on write. Resolution includes inactive entries, so a value an
administrator retired stays resolvable for the records that already reference it. An
unresolvable name is refused with `candidate.catalog_value.unknown` — never created, since
a typo would otherwise pollute the shared vocabulary permanently.

Writing to a logically removed candidate is refused; restore it first.

### Documents

Document **metadata** only — type, filename, media type, size, primary flag. No file bytes
are accepted, stored or served by this change; upload, scanning, quarantine and secure
download are KTL-9. No response field exposes a storage key, path or filesystem location.
At most one document per candidate is primary, enforced by a partial unique index as well
as by the write.

## Authorization

The four capabilities are enforced by the API on every operation, **before** the request is
dispatched — so an unauthorized caller cannot probe for a candidate's existence through the
shape of the refusal. They map onto the existing frontend `view_candidates`,
`create_candidates`, `edit_candidates` and `delete_candidates` permissions, which gate
navigation and controls only: hiding the UI is not the control. As in KTL-5, production
fails closed without a real actor; authentication remains deferred.

## Error codes

| Code                                   | Status | Spanish message                                                                        |
| -------------------------------------- | ------ | -------------------------------------------------------------------------------------- |
| `candidate.first_name.required`        | 400    | `El nombre es obligatorio.`                                                            |
| `candidate.last_name.required`         | 400    | `Los apellidos son obligatorios.`                                                      |
| `candidate.status.invalid`             | 400    | `El estado del candidato no es válido.`                                                |
| `candidate.date.invalid`               | 400    | `La fecha no es válida.`                                                               |
| `candidate.catalog_value.unknown`      | 400    | `El valor indicado no existe en el catálogo correspondiente.`                          |
| `candidate.relation.duplicate`         | 400    | `El candidato ya tiene este valor registrado.`                                         |
| `candidate.removed`                    | 400    | `No se puede modificar un candidato desactivado. Reactívelo primero.`                  |
| `candidate.document.primary_ambiguous` | 400    | `Solo puede haber un documento principal por candidato.`                               |
| `candidate.constraint.violation`       | 400    | `La solicitud contiene datos no válidos.`                                              |
| `candidate.not_found`                  | 404    | `Candidato no encontrado.`                                                             |
| `candidate.concurrency.conflict`       | 409    | `El candidato ha cambiado desde que se cargó. Vuelva a cargarlo e inténtelo de nuevo.` |

## Status

A candidate's status is one of `new`, `available`, `in_process`, `hired`, `rejected`,
enforced by the validator, by the domain, and by a database check constraint.

## Consent and retention metadata

`receivedAt`, `consentAt` and `reviewDueAt` are stored **exactly as supplied**. Nothing
substitutes today's date, derives one field from another, or drops a supplied value.

On update, an omitted field and an empty one mean different things: a field the caller does
not send leaves the stored value untouched, while an explicitly empty string clears it.
Collapsing the two would let an update that changed only a phone number silently drop a
consent date.

## Concurrency

Every candidate write — field update, removal, restoration, a relation collection, a
document collection — carries the `version` the caller read and is checked against it. A
stale version is refused with `candidate.concurrency.conflict` and applies no part of the
change. Removal advances the version like any other write, so an editor holding a
pre-removal token gets a conflict rather than resurrecting the candidate.

Every write answers with the whole projected aggregate, including its new version, so the
browser cache replaces its entry from the response rather than computing a guess.

## Auditing

`candidate.created`, `candidate.updated`, `candidate.status_changed`, `candidate.removed`,
`candidate.restored`, `candidate.relations_changed` and `candidate.documents_changed` are
written to `AUD_Events` in the same transaction as the change, carrying the acting actor,
the candidate identifier and the request correlation id — and **no field values**, because
every interesting candidate field is personal data and an audit trail that reproduced it
would be a second copy of the record with a longer retention. A refused write records
nothing, and a request that changes nothing (deactivating an already-inactive candidate) is
not audited as a change.

## Logs

Serilog carries a redaction enricher that masks the candidate field names at any depth, so
a value caught inside a destructured request or an exception's structured data is masked as
readily as one logged directly. It is registered for every sink rather than relying on
slices not to log these fields, because that discipline is silent when it fails and logs
are shipped and retained. The redaction is by property name; a value logged under an
unrelated name, or pre-formatted into a message string, is not something it can catch.

## Frontend contract

`CandidateService` is a read-through aggregate cache over the API. `find(id)` and
`list(includeInactive)` stay **synchronous** reads of a loaded state, so the consuming
services and screens keep their shape, but nothing about a candidate is stored on the
device — the cache is in memory and dies with the tab.

`find(id)` returning `undefined` means "not loaded yet" as well as "no such candidate", so
`aggregateStatus(id)` separates the two and the screens branch on it. `ensureAggregate(id)`
is idempotent and shares an in-flight request per identifier. `CandidateRelationsService`
and `DocumentService` call it before they read; their methods are therefore `async`, and
their validation rules and Spanish messages are unchanged.

The `demo-1` seed is gone and is not replaced by anything. With the API, an empty database
produces an empty list and that is the truth; a fabricated row in a table of personal data
is a liability, not a convenience.

### The advanced search

Text, status and CV filters are answered from the list alone. Language, program and skill
criteria read collections the list omits, so those searches load each candidate's aggregate
first — one request per candidate. That cost is paid only by the searches that need it,
never by the default one every visit runs, and it disappears with KTL-10, which moves
search to the server.

The filter panel now starts open and collapses when a search is run. It used to start
collapsed whenever the restored search had results, decided synchronously from browser
storage; now that the restored search awaits the API, that decision would land after the
page was interactive and shut the panel under a user already typing in it.

## Migration note

The `rrhh-candidates` `localStorage` key held the entire candidate table — identity,
contact details, location, consent and retention metadata, status, source and notes — as
one JSON blob, in clear, on every user's device. It is **actively removed** at application
bootstrap, not merely abandoned: ceasing to write a key leaves the exposure sitting in
every browser that ran an earlier build.

The removal runs before and independently of any API call, inside a `try`/`catch`, so the
stale data goes even when the backend is unreachable or the browser denies storage.

**Release note:** on first run of this build, each browser discards its local candidate
copy. Any candidate a user created in an earlier build existed only in that browser and is
not carried over; the database is now the system of record.

## Removed: the KTL-5 reference slice

`/api/reference/candidates/{id}` and its handler, reader, endpoint, development seed and
frontend service are deleted. Its purpose — proving the read path — is served by the real
read slice, which travels the same boundaries under per-operation authorization, and a
second, less-guarded route to candidate personal data has no reason to survive. An
architecture test asserts that none of it comes back.

## Removed: the transitional catalog in-use guard

`CatalogService.isCatalogValueInUse` and `ICatalogRepository.IsValueInUseAsync` are
deleted, along with `CatalogService`'s dependency on `CandidateService`.

This is a deliberate, user-visible behaviour change: **an administrator can now deactivate
a catalog value that candidates reference**, and the Spanish refusal message is gone. What
protects the data is what always protected it — deactivation is not deletion, and the
referencing records keep resolving the value; it simply stops being offered for new
selections.
