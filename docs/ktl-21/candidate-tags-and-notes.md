# Candidate tags and custom notes (KTL-21)

KTL-21 adds a tenth business-catalog family, `tag`, and two candidate-owned data sets:
catalog-backed tags and individually versioned custom notes.

## Tag catalog and assignment

The deployment seed creates `Recontratable`, `No contactar`, and `Referido por plantilla`.
Administrators may add, rename, reorder, or deactivate tag values through the existing catalog
API. Deactivation preserves existing assignments. `No contactar` is only a business label: the
application does not enforce communication restrictions from it.

`PUT /api/candidates/{candidateId}/tags` replaces the complete assignment set using the
candidate version. Each assignment is constrained to the `tag` catalog family and is unique per
candidate. Candidate detail responses include `tags`; list, search, and export projections do
not. Tag changes are audited with identifiers and codes only.

## Custom-note contract

The existing candidate `notes` field is unchanged. Custom notes use these endpoints:

- `GET /api/candidates/{candidateId}/notes`
- `POST /api/candidates/{candidateId}/notes`
- `PUT /api/candidates/{candidateId}/notes/{noteId}`
- `PUT /api/candidates/{candidateId}/notes/{noteId}/active`

A body is required after trimming and is limited to 4,000 characters. Each note owns its
concurrency token, so adding or editing a note does not advance the candidate version. Retirement
is logical and idempotent; there is no HTTP delete operation and `ktl_runtime` has no `DELETE`
grant on `CND_CandidateNotes`.

API-created notes store `ICurrentActor.UserId`, never an email, display name, or identity-provider
subject. Responses resolve only that internal identifier and its current display name. System or
historical notes without an internal author identifier display `Autor desconocido`.

Note bodies are candidate personal data. They are excluded from request, application, diagnostic,
and audit logs and from list, search, and export projections. Add, edit, and retirement audits
contain only the candidate and note identifiers, the actor, correlation identifier, and event
type.
