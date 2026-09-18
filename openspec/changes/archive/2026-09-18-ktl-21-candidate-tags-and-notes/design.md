# Design — KTL-21 candidate tags and custom notes

## Context

See [proposal.md](./proposal.md) — Why. The requirements are in [specs/](./specs/).

What the design has to work with today:

- Catalog families are a **closed set of nine**, declared in `Domain/Catalogs/CatalogFamily.cs`
  and asserted in the business-catalogs spec. Every catalog endpoint, validator and admin screen
  is generic over the family, so a tenth family is a constant plus a seed plus spec wording.
- `sector` is already a level-free family, so a tag needs no new catalog item shape.
- A candidate owns five relation collections, each **replaced wholesale** under the candidate's
  `Version` (`Domain/Candidates/Candidate.cs:87-135`), mapped through
  `Configurations/CandidateRelationMapping.cs`, which carries a family discriminator column so a
  foreign key onto `Id` alone cannot resolve a sector where a tag was meant.
- `Candidate.Notes` is a single string on the aggregate. It stays.
- KTL-16 is implemented: `ICurrentActor` exposes the internal `UserId` alongside
  `ExternalKey`, `IsAuthenticated` and `HasPermission`. Candidate-note writes can therefore
  record a domain-safe author identifier without storing an email or provider subject.
- Search filter families are built in `Infrastructure/Persistence/CandidateSearchQuery.cs`; the
  frontend mirrors them in `src/app/features/search/models/search.models.ts` and renders them
  through one shared criteria editor and summary.

## Goals

- Tags reuse the catalog machinery end to end: no bespoke vocabulary store, no second admin page.
- Notes get the lifecycle they need (individually authored, dated, correctable, retired) without
  bending the aggregate's wholesale-replacement rule into a shape it does not fit.
- The change is additive at every boundary: no response loses a field, no stored preset breaks,
  no existing permission changes meaning.

## Non-Goals

- Changing how the five existing relation collections are written.
- Touching `Candidate.Notes`, the export path, or the search result projection.
- Introducing any identity or permission machinery that belongs to KTL-16.

## Decisions

### 1. Tags are the tenth catalog family, not a new entity

**Chosen:** add `CatalogFamilies.Tag = "tag"` to the closed set.

Everything downstream is already generic over the family string: `ListCatalogFamilyQuery`,
`CreateCatalogItemCommand`, the reorder and activate commands, `CatalogEndpoints`, the Catálogos
page and `catalogs.manage`. The cost is one constant, one seed block, and amending the spec's
"nine families" wording — against a whole slice for a separate entity with its own endpoints,
page and permission.

_Alternative:_ a standalone tag entity. Rejected: it duplicates the lifecycle, uniqueness,
ordering and audit rules the catalog already enforces, for no behaviour the catalog lacks.

_Consequence:_ the closed set is asserted in more than one place. Before implementing, grep for
`CatalogFamilies.All`, for the literal "nine", and for the frontend `CatalogFamily` union, so
none is left half-updated. Do **not** add tags to a browser-side default vocabulary: the spec
forbids falling back to a locally generated vocabulary when the API is unavailable.

### 2. Tags are a sixth relation collection, replaced wholesale

**Chosen:** `CandidateTag : CandidateRelation` with `TagId` + `TagFamily`, written through
`PUT /api/candidates/{id}/tags` as a complete set under the candidate's `Version`, exactly like
skills but with one catalog reference instead of two.

This keeps the aggregate's concurrency story intact — the candidate's version is the token for
everything it owns — and lets the tag editor be a straightforward controlled component that
submits the whole set.

_Alternative:_ per-tag add and remove endpoints. Rejected: a per-item write has no token of its
own, which is precisely the reasoning already recorded on `Candidate.Replace*`.

### 3. Notes are **not** a relation collection

**Chosen:** `CandidateNote` is written individually, carries its **own** `Version`, and does not
advance the candidate's.

Notes differ from relations in the way that matters: they are written one at a time by different
people, often while someone else is editing the candidate. Making them a replaced collection
would mean re-submitting every note to add one, and making them advance the candidate's version
would turn every note into a 409 for whoever is editing the candidate's fields.

_Trade-off:_ the candidate's version no longer covers strictly everything it owns. That is
acceptable because nothing about a note participates in candidate-level invariants — notes have
no uniqueness rule, no ordering constraint, and no cross-field validation with the candidate.
The point is recorded here because `Candidate.TouchUpdated` exists for exactly the opposite
reason (documents), and a future reader will ask why notes differ.

_Consequence:_ `Application/Features/Candidates/Notes/` gets one file per use case
(`AddCandidateNote`, `UpdateCandidateNote`, `SetCandidateNoteActive`, `ListCandidateNotes`),
each with its own validator and handler repeating the permission guard.

### 4. Record the KTL-16 internal user id; keep the author column nullable

**Chosen:** `AuthorUserId` is a nullable `Guid` foreign key to `ADM_Users`. A note created through
the candidate API records `ICurrentActor.UserId`. Null remains valid for a system-created or
historical row, and is presented as `Autor desconocido`.

KTL-16 has already landed, so KTL-21 has no identity sequencing gap. The nullable case is still
needed because the domain permits system-authored data and because absence must be represented
honestly rather than by inventing an author.

_Implementation note:_ the handler reads `ICurrentActor.UserId`. Do **not** fall back to
`ExternalKey` — it may be an email, which is personal data the spec forbids storing on a note.

The note response resolves the stored id to the user's display name on the server and exposes
only that id and display name. Recruiters must not call the admin-only users endpoint to resolve
authors. A missing author or an unexpectedly unresolved user renders as `Autor desconocido`.

### 5. Any holder of `candidates.update` may edit or retire any note

**Chosen (confirmed with the product owner, 2026-09-16):** notes follow the rest of the candidate
record — an editor is an editor. Authorship is a record of who _wrote_ it, not an ownership
claim, and the audit trail carries who changed it afterwards.

This keeps authorization aligned with candidate editing rather than introducing author ownership
semantics that the product did not request.

### 6. Tag criteria reuse `CriteriaFilter` with an always-empty level

**Chosen:** `SearchFilters` gains `tagCriteria: CriteriaFilter[]` and `tagMode: MultiValueMode`,
reusing the existing criterion shape; the editor hides the level control for the tag family and
the API **rejects** a tag criterion that carries a level.

One criterion shape keeps the shared editor, the summary, the preset payload and the server
validator uniform. Hiding the control in the UI is not the control — hence the server-side
rejection, so a hand-built request cannot smuggle a level into a family that has none.

_Alternative:_ a separate `tagValues: string[]`. Rejected: it forks the criteria editor, the
summary and the preset schema for one family.

### 7. Preset payloads are read tolerantly, not migrated

**Chosen:** the preset reader defaults an absent tag family to empty with `ANY`. No data
migration rewrites stored preset payloads.

Migrating them would be a write against every stored preset for a purely additive field, and it
would still need the tolerant read for anything written by an older instance mid-deploy.

### 8. Naming the note collection on the wire

`Candidate.notes` is already the legacy free-text string in both the API contract and
`candidate.models.ts`. The thread is exposed as **`customNotes`** on the candidate detail
response and in the frontend model, matching the Spanish section title (`Notas personalizadas`)
and leaving the existing field untouched.

## Data model

One migration, `AddCandidateTagsAndNotes`, carrying tables, constraints, indexes **and** grants —
DDL as `ktl_migrator`, grants for `ktl_runtime`, per non-negotiable 4.

| Table                | Columns                                                                                                                       | Constraints and indexes                                                                                                                                                                        | `ktl_runtime` grants                       |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| `CND_CandidateTags`  | `Id`, `CandidateId`, `TagId`, `TagFamily`                                                                                     | FK → `CND_Candidates` (no cascade); composite FK `(TagId, TagFamily)` → `CAT_Items`; check `TagFamily = 'tag'`; unique `(CandidateId, TagId)`; index `CandidateId`; index `(TagId, TagFamily)` | `SELECT, INSERT, UPDATE, DELETE`           |
| `CND_CandidateNotes` | `Id`, `CandidateId`, `Body`, `AuthorUserId` (nullable), `CreatedAtUtc`, `UpdatedAtUtc`, `IsActive`, `DeletedAtUtc`, `Version` | FK → `CND_Candidates` (no cascade); nullable FK `AuthorUserId` → `ADM_Users` (no cascade); `Body` non-empty check; index `(CandidateId, CreatedAtUtc DESC)` filtered on `IsActive`             | `SELECT, INSERT, UPDATE` — **no `DELETE`** |

`CND_CandidateTags` keeps `DELETE` because wholesale replacement removes rows, exactly as the
other relation tables do; a tag assignment is not a record of anything, it is current state.
`CND_CandidateNotes` does not, because a note is retired logically — non-negotiable 5.

Ids are `Guid.CreateVersion7()`. Timestamps are `DateTimeOffset` UTC. Physical names are quoted
and prefixed `CND_`, which is already registered — no new prefix, no architecture decision.
Follow `CandidateRelationMapping` for the composite key and `EnforceCandidateRelationUniqueness`
for the uniqueness precedent.

**Note body limit:** 4000 characters, validated in the FluentValidation validator and mirrored by
a database length constraint. Line breaks are preserved; the body is plain text and is rendered
as text, never as markup.

## API surface

Additive only.

| Verb | Route                                        | Permission          | Behaviour                                        |
| ---- | -------------------------------------------- | ------------------- | ------------------------------------------------ |
| PUT  | `/api/candidates/{id}/tags`                  | `candidates.update` | replace the whole set; candidate `Version` → 409 |
| GET  | `/api/candidates/{id}/notes`                 | `candidates.read`   | active notes, newest first                       |
| POST | `/api/candidates/{id}/notes`                 | `candidates.update` | 201; author from the current actor               |
| PUT  | `/api/candidates/{id}/notes/{noteId}`        | `candidates.update` | body + note `Version` → 409                      |
| PUT  | `/api/candidates/{id}/notes/{noteId}/active` | `candidates.update` | logical retirement; no `DELETE` verb             |

Every handler repeats the guard and every endpoint checks `ICurrentActor` **before** validating
or dispatching, so an unauthorized caller cannot probe existence through validation problems —
the pattern `CatalogEndpoints` already uses. Errors are thrown as the types in
`Application/Common/Errors` and mapped by `GlobalExceptionHandler`; none is hand-built. A note
addressed through a candidate it does not belong to is a not-found, never a wrong-candidate
disclosure.

The candidate detail response gains `tags` and `customNotes`; the list and search projections
gain nothing. `GET /api/candidates/{id}/notes` returns the same active-note projection and exists
for refresh after note mutations; the initial detail load does not require a second request.

## Frontend approach

- `candidate-tags.tsx` — a removable-chip list plus a select of active tags, reading the catalog
  through `useCatalogs()` (never `useServices()` during render). `usePermission('candidates.update')`
  is hoisted to a const at the top, never called in a loop or callback.
- `candidate-notes.tsx` — a list with a heading plus a controlled `useState` form. Validation
  stays in the service layer and throws `TranslatableError`; components render it with
  `errorText(err, t)` and report through `useErrorToast()`.
- A `CandidateNotesService` singleton holding a signal from `core/state/signal.ts`, registered in
  `core/di/services.ts`, with its own `useCandidateNotes()` subscribing hook because it is read
  during render.
- Search: `tagCriteria`/`tagMode` in `SearchFilters`, rendered by the existing shared criteria
  group with the level control suppressed for this family.
- Every string goes to `src/assets/i18n/es.json` under flat keys; `candidate-form.tsx` is not
  added to `LEGACY_HARDCODED_COPY`, and if the tags editor lands in a file already on that list,
  that file's copy moves to keys and the file leaves the list in this change. Dates through
  `formatDate`, counts through `formatNumber`, tag names through `catalogLabel`.
- Keep `name=` and `data-testid` on every control. One DOM tree serves both widths; no
  `window.innerWidth`, no `matchMedia`.

## Risks / Trade-offs

- **The closed set is asserted in several places** → grep `CatalogFamilies.All`, the literal
  "nine", and the frontend `CatalogFamily` union before starting; a half-updated set gives a
  family that lists but cannot be created.
- **"No contactar" reads as an enforced rule but enforces nothing** → say so explicitly in
  `docs/ktl-21/` and in the release note; otherwise a recruiter will assume the app blocks
  contact.
- **Notes accumulate unstructured personal data** → bodies stay out of search, exports, list and
  search projections, and logs; the retention ticket inherits this and the design says so now.
- **A system or historical note can have no author** → the nullable foreign key is deliberate
  (decision 4), and the UI presents the case explicitly rather than fabricating identity.
- **Tag joins could slow the paged search** → the tag join mirrors the skill join and is indexed
  on both `CandidateId` and `(TagId, TagFamily)`; the `ALL` mode uses the same distinct-count
  shape the existing families use, so no new query pattern is introduced.
- **Stored presets break on a new family** → tolerant read (decision 7), covered by a test that
  loads a preset payload with no tag family.
- **The notes list could grow unbounded on one candidate** → it is indexed and filtered on
  `IsActive`; if a candidate ever accumulates enough notes to matter, paging the notes endpoint
  is additive and does not change this contract.

## Migration Plan

1. `dotnet ef migrations add AddCandidateTagsAndNotes --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`, then hand-add the grants and the check constraints the generator does not emit.
2. Deploy through the explicit `--migrate` entry point (the `migrator` container). The API never
   migrates on normal startup.
3. The seed adds the `tag` family's default values idempotently; it touches no other family, so
   running it against an existing deployment is safe.
4. **Rollback:** the change is additive. Reverting the application leaves two unused tables and
   one unused catalog family, which are harmless — a deployed-then-rolled-back release does not
   need a down migration executed. If the family must be withdrawn, deactivate its values rather
   than deleting them; the catalog has no physical delete by design.
5. No data is rewritten and no existing row is touched, so there is no restore step beyond the
   standard runbook.
