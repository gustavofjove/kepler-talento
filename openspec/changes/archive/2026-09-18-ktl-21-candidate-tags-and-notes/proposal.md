# KTL-21 — Candidate tags and custom notes

## Why

Recruiters have one free-text box per candidate (`Observaciones internas`) and no shared,
controlled vocabulary for classifying people. Facts the whole team must act on —
"Recontratable", "No contactar", "Referido por plantilla" — end up buried in prose where they
cannot be filtered, counted or trusted, and a note written by one recruiter carries no author
and no date, so a candidate's history is lost the moment a colleague picks the file up.

This change gives the team a governed tag vocabulary that search can filter on, and an
authored, dated, non-destructive note thread per candidate.

Source brief: [openspec/KTL-21.md](../../KTL-21.md).

## What Changes

**Tags — a tenth catalog family**

- Add `tag` to the closed catalog family set, administered exactly like `sector`: a value has a
  code, a Spanish name, a position and an active flag, and **no level and no status**.
- Seed default tags (`Recontratable`, `No contactar`, `Referido por plantilla`) through the
  existing idempotent deployment seed.
- No new administration screen: the generic Catálogos page and the existing
  `/api/catalogs/{family}` endpoints serve the new family unchanged.

**Assigning tags to candidates**

- A sixth candidate relation collection, replaced wholesale under the candidate's version like
  its five siblings, written through a new `PUT /api/candidates/{id}/tags`.
- A candidate cannot carry the same tag twice. A deactivated tag stops being offered but still
  resolves and still displays on candidates that already hold it.
- Tags appear on the candidate detail page and the candidate form. They deliberately do **not**
  enter the search-result or candidate-list projection, which stay minimal.

**Custom notes**

- A new per-candidate note: a body, a stored internal author id with a server-resolved display
  name on reads, creation and update timestamps, its own concurrency token, and logical
  retirement.
- Notes are created, edited and retired individually — not replaced wholesale — through
  `GET`/`POST /api/candidates/{id}/notes`, `PUT /api/candidates/{id}/notes/{noteId}` and
  `PUT /api/candidates/{id}/notes/{noteId}/active`. There is no `DELETE` verb.
- The existing single `Notes` field is untouched; this is an addition, not a replacement.

**Tags in advanced search**

- A tag criteria family with `ANY` and `ALL` semantics, combining with the other families by
  AND, ignoring an empty selection, and never producing a duplicate candidate.
- Tag criteria travel in saved presets. Presets stored before this change must still load, with
  an empty tag family.

**No new permissions.** Managing the vocabulary is `catalogs.manage`; assigning tags and writing
notes are candidate edits (`candidates.update`); reading them is `candidates.read`.

## Capabilities

### New Capabilities

- `candidate-notes`: the authored, non-destructive note thread on a candidate — the note's field set
  and lifecycle, per-note optimistic concurrency, logical retirement with no physical delete,
  authorship recording and the unknown-author case, authorization, audit, and the
  personal-data rules that keep note bodies out of responses, exports, search and logs.

### Modified Capabilities

- `business-catalogs`: the closed family set becomes **ten** — `tag` joins the nine — and the
  deployment seed gains that family's default Spanish values. No change to the catalog item
  shape, lifecycle, uniqueness, authorization or audit rules.
- `candidate-management`: tags become a candidate relation collection, with the same wholesale
  replacement, catalog resolution, uniqueness and optimistic-concurrency rules the existing
  collections have; the candidate detail response carries tags and active notes.
- `candidate-persistence`: two new `CND_` tables with their composite catalog foreign key,
  family check constraint, uniqueness and access-path indexes, and least-privilege runtime
  grants — notably **no `DELETE`** for `ktl_runtime` on the notes table.
- `candidate-search`: a tag criteria family added to the filter contract with `ANY`/`ALL`
  semantics; the minimal result projection is explicitly unchanged.
- `saved-search-presets`: a preset stored before the tag family existed stays readable and
  applies with an empty tag family, rather than failing validation or erroring the page.

## Impact

**Personal data, authorization, storage and roles.** This change touches personal data and does
not touch storage access or role definitions. It adds no permission.

- _Principle 1 (personal data by design)._ A tag assignment and a note body are personal data
  about a candidate, and one tag ("No contactar") may record the candidate's own instruction.
  They are exposed only on the permission-checked candidate detail path, excluded from the
  minimal search and list projections, excluded from exports, and never written to logs —
  `PersonalDataRedactionEnricher` is not worked around. A note stores the **internal user id**
  of its author, never an email, display name or external subject. Audit rows carry identifiers
  and codes only, never a note body.
- _Principle 3 (fail closed, least privilege)._ Every new endpoint checks `ICurrentActor`
  authentication and its specific permission **before** validating or dispatching. The migration
  ships its grants in the same slice: `ktl_runtime` gets no `DELETE` on the notes table, because
  a note is retired logically, and DDL stays with `ktl_migrator`.
- _Principle 5 (no physical deletes)._ No `DELETE` endpoint is added for tags or notes.

**Code**

- Backend: `Domain/Catalogs/CatalogFamily.cs`; new `Domain/Candidates/CandidateTag.cs` and
  `CandidateNote.cs`; `Domain/Candidates/Candidate.cs`; `Application/Features/Candidates/`
  (relations, contract, projection, and a new notes feature folder);
  `Infrastructure/Persistence/` (two EF configurations, `ApplicationDbContext`,
  `CatalogSeedData`, `CandidateSearchQuery`, one migration);
  `Web/Features/Candidates/CandidateEndpoints.cs`.
- Frontend: catalog and candidate models; new `candidate-tags.tsx` and `candidate-notes.tsx`
  with co-located CSS, wired into the candidate form and detail page; candidate API and
  services with DI registration; search models and criteria components;
  `src/assets/i18n/es.json`.
- Tests: new and updated Vitest unit specs, xUnit unit and Testcontainers integration tests, a
  new Playwright journey plus extensions to the catalogs and advanced-search specs, and grant
  assertions in `tests/security/`.

**API** — additive only. One new relation endpoint, four note endpoints, one new search filter
family, one new catalog family value. No existing response shape loses a field.

**Dependencies** — no new npm or NuGet package.

**Sequencing** — KTL-16 is already implemented and supplies `ICurrentActor.UserId`. A note
created through the candidate API records that internal id. The nullable author column remains
only for system or historical rows, which render with an explicit unknown author.
