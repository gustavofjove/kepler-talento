# KTL-21 — Candidate tags and custom notes

## [original]

Tags and custom notes for candidates

New tags catalog similar to "Sectores" where there is no level or status for the tag. Administrators will be able to add new tags to the catalog that can be manually assigned to candidates by users, for example: "Recontratable", "No contactar", "Referido por plantilla", etc

Also the candidates will include a new "custom notes" (notas personalizadas) section so users can add any comments to candidates.

## [enhanced]

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (catalog write slice), KTL-8 (candidate writes and relations), KTL-10
(paged search), KTL-14 (shared presets), KTL-16 (identity — supplies the internal user id a
note records as its author)

### Summary

Recruiters currently have one free-text box per candidate, `Observaciones internas`, and no
way to classify a candidate with a shared, controlled vocabulary. Facts that the whole team
needs to act on — "Recontratable", "No contactar", "Referido por plantilla" — end up buried
in prose where they cannot be filtered, counted or trusted.

This ticket adds two things:

1. **Tags** — a tenth catalog family, `tag`, administered exactly like `sector` (a value has
   a name and an active flag; it has no level and no status), assigned to candidates as a
   multi-value relation, and searchable as a filter family with `ANY` / `ALL` semantics.
2. **Custom notes** — an append-only, authored, dated thread of notes on the candidate, in
   its own section of the candidate detail page, replacing nothing: the existing single
   `Notes` field stays where it is.

Both are personal data about a candidate, and one of them ("No contactar") is a decision a
candidate may have asked for. They are minimally exposed, never logged, and behind
permission checks that fail closed.

### User stories

- **As an administrator**, I add, rename, reorder and deactivate tags on the Catálogos page,
  so the team classifies candidates with one shared vocabulary instead of free text.
- **As a recruiter**, I assign one or more tags to a candidate while editing them, and I see
  those tags on the candidate detail page, so I can tell at a glance that someone is
  recontratable or must not be contacted.
- **As a recruiter**, I add a dated note to a candidate and see who wrote each earlier note,
  so a conversation with a candidate is not lost when a colleague picks the file up.
- **As a recruiter**, I filter the advanced search by tags — any of them, or all of them —
  and I save that filter as a preset like any other criteria family.

### Context — what exists today

| Concern             | Today                                                                                                              | Evidence                                                                               |
| ------------------- | ------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| Catalog families    | A **closed set of nine**, asserted in the spec and in code                                                         | `backend/Domain/Catalogs/CatalogFamily.cs`, `openspec/specs/business-catalogs/spec.md` |
| A level-free family | `sector` already has no level and no status; it is the shape a tag needs                                           | `CatalogFamilies.Sector`                                                               |
| Catalog admin UI    | One page serves every family generically                                                                           | `src/app/features/catalogs/pages/catalog-management-page.tsx`                          |
| Candidate relations | Five: languages, programs, education, experience, skills — each replaced wholesale under the candidate's `Version` | `backend/Domain/Candidates/Candidate.cs:87-135`                                        |
| Candidate notes     | A single `Notes` string on the aggregate, edited in one textarea                                                   | `Candidate.Notes`, `src/app/features/candidates/components/candidate-form.tsx:114`     |
| Note authorship     | None. Nothing records who wrote the text                                                                           | —                                                                                      |
| Search families     | Text, status, skills, languages, programs, CV presence                                                             | `src/app/features/search/models/search.models.ts`, `CandidateSearchQuery.cs`           |
| Search projection   | Deliberately minimal — no relation collections, no notes                                                           | `openspec/specs/candidate-search/spec.md`                                              |

### Decisions taken (2026-09-16)

These were settled before the change is written; the design records the consequences, not the
options.

1. **Tags are the tenth catalog family**, `tag`, not a separate entity. They reuse `CAT_` storage,
   `/api/catalogs/{family}`, the Catálogos page and `catalogs.manage`. The "nine families"
   statement in the business-catalogs spec becomes ten, in the same change.
2. **Notes are append-only authored entries**, one row per note with a body, an author, a
   creation and update timestamp, and logical retirement. Not a richer textarea.
3. **Tags are a full search filter family** with `ANY` and `ALL` modes, and travel in saved
   presets like the existing families.
4. **No new permissions.** Managing the tag vocabulary is `catalogs.manage`. Assigning tags and
   writing notes are candidate edits: `candidates.update`. Reading them is `candidates.read`.

### Scope

#### A — The `tag` catalog family

- Add `CatalogFamilies.Tag = "tag"` to the closed set and to `All`, so every existing catalog
  endpoint, validator and admin screen accepts it with no further change.
- Seed default tags in `backend/Infrastructure/Persistence/CatalogSeedData.cs` — at least
  `Recontratable`, `No contactar`, `Referido por plantilla` — under the seed's existing
  idempotence and explicit-code rules (a seeded code is never silently uniquified).
- Frontend: extend the `CatalogFamily` union and `CATALOG_FAMILY_LABELS` in
  `src/app/features/catalogs/models/catalog.models.ts` with `tag` → `Etiquetas`. Do **not**
  add a browser-side fallback vocabulary; the spec forbids substituting local defaults when
  the API is unavailable. If `DEFAULT_CATALOGS` is still consumed anywhere, say where and why
  in the design rather than extending it by reflex.
- A tag has no level and no status, exactly like `sector`. Nothing about `CatalogItem` changes.

#### B — Assigning tags to candidates

- `CandidateTag : CandidateRelation` in `backend/Domain/Candidates/`, holding `TagId` and
  `TagFamily = CatalogFamilies.Tag`, following `CandidateSkill` — but with a single catalog
  reference, since there is no level.
- `Candidate.ReplaceTags(...)`, replaced wholesale under the candidate's `Version`, like its
  siblings; removed rows are returned for explicit deletion (no cascade).
- `SetCandidateTagsCommand(Guid Id, IReadOnlyList<CandidateTagInput> Tags, uint Version)` in
  `backend/Application/Features/Candidates/SetCandidateRelations.cs`, with its validator and
  handler, resolving names through `CandidateCatalogLookup` against the `tag` family only.
- `PUT /api/candidates/{id}/tags` in `backend/Web/Features/Candidates/CandidateEndpoints.cs`,
  named `SetCandidateTags`, guarded by `candidates.update` before validation, 409 on version
  mismatch.
- `CandidateTagResponse(Guid Id, string Tag)` in `CandidateContract.cs`, projected by
  `CandidateProjection.cs`, added to the **detail** response only.
- A candidate cannot carry the same tag twice; a deactivated tag stays resolvable on
  candidates that already hold it but is no longer offered for selection.

#### C — Custom notes

- `CandidateNote` in `backend/Domain/Candidates/`: `Id` (`Guid.CreateVersion7()`),
  `CandidateId`, `Body`, `AuthorUserId` (nullable internal user id — never an email, a display
  name or an external subject), `CreatedAtUtc`, `UpdatedAtUtc`, `IsActive`, `DeletedAtUtc`,
  `Version`.
- Notes are **not** a replace-wholesale collection. They are created, edited and retired one at
  a time, so each note carries its own concurrency token and its own endpoints:

  | Verb | Route                                        | Permission          | Notes                               |
  | ---- | -------------------------------------------- | ------------------- | ----------------------------------- |
  | GET  | `/api/candidates/{id}/notes`                 | `candidates.read`   | active notes, newest first          |
  | POST | `/api/candidates/{id}/notes`                 | `candidates.update` | 201, author from `ICurrentActor`    |
  | PUT  | `/api/candidates/{id}/notes/{noteId}`        | `candidates.update` | body + `Version` → 409              |
  | PUT  | `/api/candidates/{id}/notes/{noteId}/active` | `candidates.update` | logical retirement — no DELETE verb |

- Detail reads return the candidate's active notes alongside its relations, so the detail page
  makes one request.
- A note body is required and non-blank, with a documented maximum length, refused with a
  stable validation code and a Spanish message.
- Author display names are resolved by the users endpoint (KTL-16), not stored on the note. A
  note whose author is unknown renders as `Autor desconocido`, never as a blank.

#### D — Tags in advanced search

- Add a `tag` criteria family to `CandidateSearchQuery` and the search contract: `ANY` matches
  a candidate holding at least one of the tags, `ALL` only a candidate holding every distinct
  tag named, across separate relation rows, with no duplicate candidates. An empty tag family
  places no restriction; a tag value matching no known tag matches no candidate.
- Frontend: `tagCriteria: CriteriaFilter[]` and `tagMode: MultiValueMode` in `SearchFilters`
  (`src/app/features/search/models/search.models.ts`), reusing `CriteriaFilter` with an always-
  empty `level` so the generic criteria group, dialog and summary keep one shape; the level
  control is hidden for this family.
- Saved presets must stay readable: a preset stored before this change loads with an empty tag
  family and an `ANY` mode, without a migration failing or the page erroring.

### Data model

New tables, under the already-registered `CND_` prefix — no new prefix, no architecture
decision needed. One EF Core migration, `AddCandidateTagsAndNotes`, carrying its constraints,
indexes **and** runtime grants.

| Table                | Key columns                                                                                                        | Constraints                                                                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `CND_CandidateTags`  | `Id`, `CandidateId`, `TagId`, `TagFamily`                                                                          | composite FK `(TagId, TagFamily)` → `CAT_Items`, check `TagFamily = 'tag'`, unique `(CandidateId, TagId)`, index on `CandidateId`, no cascade delete |
| `CND_CandidateNotes` | `Id`, `CandidateId`, `Body`, `AuthorUserId`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsActive`, `DeletedAtUtc`, `Version` | FK → `CND_Candidates`, index `(CandidateId, CreatedAtUtc DESC)` filtered on `IsActive`, no cascade delete                                            |

Follow `CandidateRelationMapping` for the composite foreign key — the family column is what
stops a sector identifier from being stored as a tag — and
`EnforceCandidateRelationUniqueness` for the uniqueness precedent.

Grants shipped in the same migration: `ktl_runtime` gets `SELECT, INSERT, UPDATE, DELETE` on
`CND_CandidateTags` (wholesale replacement deletes rows, as the other relation tables do) and
`SELECT, INSERT, UPDATE` — **no `DELETE`** — on `CND_CandidateNotes`, because a note is retired
logically. DDL stays with `ktl_migrator`.

### Files to touch

**Backend**

- `Domain/Catalogs/CatalogFamily.cs`, `Domain/Candidates/CandidateTag.cs`,
  `Domain/Candidates/CandidateNote.cs`, `Domain/Candidates/Candidate.cs`,
  `Domain/Candidates/CandidateAuditEvents.cs`
- `Application/Features/Candidates/SetCandidateRelations.cs`, `CandidateContract.cs`,
  `CandidateProjection.cs`, `GetCandidate.cs`, and a new
  `Application/Features/Candidates/Notes/` file per use case (`AddCandidateNote`,
  `UpdateCandidateNote`, `SetCandidateNoteActive`, `ListCandidateNotes`)
- `Application/Features/Search/` — the tag criteria family; `Infrastructure/Persistence/CandidateSearchQuery.cs`
- `Infrastructure/Persistence/Configurations/CandidateTagConfiguration.cs`,
  `CandidateNoteConfiguration.cs`, `ApplicationDbContext.cs`, `CatalogSeedData.cs`,
  `Persistence/Migrations/<timestamp>_AddCandidateTagsAndNotes.cs`
- `Web/Features/Candidates/CandidateEndpoints.cs`

**Frontend**

- `src/app/features/catalogs/models/catalog.models.ts`
- `src/app/features/candidates/models/candidate.models.ts` (`CandidateTag`, `CandidateNote`,
  `Candidate.tags`, `Candidate.notes` — naming the collection so it does not collide with the
  existing `notes` string is a design decision; `customNotes` is the obvious escape)
- `src/app/features/candidates/components/candidate-tags.tsx` + `.css`,
  `candidate-notes.tsx` + `.css`, wired into `candidate-form.tsx` and
  `pages/candidate-detail-page.tsx`
- `src/app/features/candidates/services/candidate.api.ts`,
  `candidate-relations.service.ts`, and a `candidate-notes.service.ts` registered in
  `src/app/core/di/services.ts` with its own subscribing hook if it is read during render
- `src/app/features/search/models/search.models.ts`, `components/criteria-group.tsx`,
  `search-criteria-form.tsx`, `search-criteria-summary.tsx`,
  `services/candidate-search.service.ts`
- `src/assets/i18n/es.json`

### Spanish copy

All new copy goes to `src/assets/i18n/es.json` under flat keys and is rendered with `t()`;
nothing hardcoded in JSX, and `candidate-form.tsx` is not to be added to
`LEGACY_HARDCODED_COPY`. Suggested keys and values:

| Key                                     | Value                          |
| --------------------------------------- | ------------------------------ |
| `catalog.family.tag`                    | Etiquetas                      |
| `candidate.profile.tags.title`          | Etiquetas                      |
| `candidate.profile.tags.add`            | Añadir etiqueta                |
| `candidate.profile.tags.remove`         | Quitar la etiqueta {{name}}    |
| `candidate.profile.tags.empty`          | Sin etiquetas asignadas.       |
| `candidate.profile.notes.title`         | Notas personalizadas           |
| `candidate.profile.notes.add`           | Añadir nota                    |
| `candidate.profile.notes.body`          | Nota                           |
| `candidate.profile.notes.required`      | La nota no puede estar vacía.  |
| `candidate.profile.notes.author`        | {{author}} · {{date}}          |
| `candidate.profile.notes.unknownAuthor` | Autor desconocido              |
| `candidate.profile.notes.retire`        | Retirar nota                   |
| `candidate.profile.notes.empty`         | Este candidato no tiene notas. |
| `search.filters.tags.title`             | Etiquetas                      |
| `search.filters.tags.mode.any`          | Cualquiera                     |
| `search.filters.tags.mode.all`          | Todas                          |

Dates use `formatDate`, counts use `formatNumber`, tag names use `catalogLabel` — never a
hardcoded locale or `nameEs`. Errors surface through `useErrorToast()` and validation throws
`TranslatableError`.

### Acceptance criteria

1. An administrator creates, renames, reorders, deactivates and reactivates a tag on the
   Catálogos page, through the existing catalog endpoints, with no new admin screen.
2. A tag has no level and no status anywhere in the UI or the contract.
3. A recruiter assigns several tags to a candidate and sees them on the detail page; the same
   tag cannot be assigned twice.
4. A deactivated tag stops being offered for new assignments, still resolves and still displays
   on candidates that already hold it.
5. A recruiter adds a note; it appears with its author and date, newest first. Editing it
   requires its current `Version` and a stale edit answers 409.
6. A retired note disappears from the candidate and is still present in the database; the API
   exposes no operation that physically deletes a note.
7. A note written before an actor exists, or by a system process, renders as
   `Autor desconocido`.
8. Advanced search filters by tags with `ANY` and `ALL`; an empty tag family restricts nothing;
   the same candidate never appears twice; presets saved before this change still load.
9. Every new endpoint answers 401 unauthenticated and 403 without its permission, **before**
   validation runs.
10. No response, log or error message contains a note body, a tag assignment, an author's email
    or display name, or a storage path.
11. `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
    `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

### Test coverage

**Backend unit** (`backend/Tests/UnitTests/`, sealed classes, sentence-style names)

- `tag` is a known family; an unknown family is still refused.
- `ReplaceTags` refuses a relation belonging to another candidate and returns removed rows.
- Note creation trims and refuses a blank body; update rejects a stale version; retirement is
  idempotent.
- Handler guards: `candidates.update` for writes, `candidates.read` for the list.

**Backend integration** (Testcontainers `PostgreSqlFixture`)

- The composite foreign key refuses a `sector` identifier stored as a tag.
- The unique index refuses the same tag twice on one candidate, including concurrently.
- Deactivating a tag in use succeeds and the candidate still resolves it.
- The tag filter: `ANY`, `ALL` across separate rows, unknown tag matches nothing, no duplicate
  candidates, deterministic paging.
- Grants: `ktl_runtime` has no `DELETE` on `CND_CandidateNotes`, asserted against the catalog.
- 401/403 per endpoint, refused before validation.
- The seed is idempotent and leaves administrator edits to the `tag` family intact.

**Frontend unit** (`tests/unit/`)

- `candidate-tags.spec.tsx`, `candidate-notes.spec.tsx` — assignment, removal, empty states,
  note validation, unknown author, retirement confirmation.
- Updates to `catalog.service.spec.ts`, `candidate-relations.service.spec.ts`,
  `candidate-search.service.spec.ts`, `search-criteria-form.spec.tsx`,
  `search-criteria-summary.spec.tsx`, `search-presets.service.spec.ts` (old preset shape loads),
  `i18n.spec.ts` (no missing keys).

**E2E** (`tests/e2e/`)

- New `candidate-tags-notes.spec.ts`: create a tag in Catálogos → assign it to a candidate →
  add, edit and retire a note → filter the advanced search by the tag. Selectors by role,
  accessible name or `data-testid`; **no hardcoded Spanish text**. Restore seed data afterwards.
- Extend `catalogs-crud.spec.ts` and `advanced-search.spec.ts` for the new family.

**Security** (`tests/security/`)

- Grants for both new tables, and an assertion that no note body or tag assignment reaches the
  logs through the new paths.

### Non-functional requirements

- **Security and personal data.** Tag assignments and note bodies are personal data. They stay
  out of the search projection, out of logs (`PersonalDataRedactionEnricher` is not to be worked
  around), and behind `candidates.read`. "No contactar" is a candidate's own instruction: it is
  visible to anyone who may read the candidate, and never exported without a decision.
- **Performance.** The tag filter joins the same way the skill filter does and must not degrade
  the paged search; the tag join is indexed on `CandidateId` and on `(TagId, TagFamily)`. The
  notes list is bounded and indexed by `(CandidateId, CreatedAtUtc DESC)`.
- **Accessibility.** Tags render as a list of removable chips, each remove control with an
  accessible name naming the tag; the notes thread is a list with a heading, and adding a note
  moves focus to the new entry. Keyboard-operable throughout.
- **Responsive.** One DOM tree serves both widths. No `window.innerWidth`, no `matchMedia`; any
  breakpoint work belongs in the existing 768px rule in `primary-nav.css`, and layout is covered
  by Playwright, not jsdom.
- **Audit.** Tag assignment and note write, edit and retirement each record an audit event
  through `CandidateAuditEvents`, carrying identifiers and codes only — never the note body.

### Out of scope

- Showing tags in the search results or candidate list. The minimal search projection stands;
  changing it is a separate decision with its own personal-data reasoning.
- Searching the text of notes. The free-text family keeps its current field set.
- Exporting tags or notes (`export.service.ts` is untouched).
- Tag colours, grouping, hierarchies, per-tag permissions or automatic tagging.
- Replacing, migrating or deprecating the existing `Notes` field.
- Retention or anonymization of notes — their own ticket, though the design should say what it
  expects.

### Decisions to make in the design

1. **Sequencing against KTL-16.** A note's value depends on knowing who wrote it. Ship after
   KTL-16 and store the internal user id, or ship now with `AuthorUserId` null and backfill
   nothing. State which; if it ships early, the release notes must say those notes will never
   gain an author.
2. **Does a note move the candidate's `Version`?** Touching it makes every note a source of
   spurious 409s for someone editing the candidate; not touching it means the aggregate's
   token no longer covers everything the candidate owns. Recommended: notes carry their own
   token and do not touch the candidate's.
3. **Note body limits.** Maximum length, whether line breaks are preserved, and whether any
   markup is permitted (plain text is the safe default).
4. **Editing someone else's note.** `candidates.update` lets any editor change any note. Confirm
   that is intended, or restrict editing to the author and leave retirement to any editor.
5. **Naming the detail collection.** `Candidate.notes` is already the legacy string;
   `customNotes` avoids the collision but is a contract name the frontend and e2e specs inherit.
6. **Default tags.** Which values the seed carries, and whether `No contactar` warrants any
   distinct treatment in the UI beyond being a tag.

### Risks

- **Opening a closed set.** The "nine families" statement is asserted in the spec, in code and in
  tests. Missing one of those leaves the family half-supported. Grep for the number and for
  `CatalogFamilies.All` before starting.
- **A tag that reads as a decision.** "No contactar" looks like an instruction the system
  enforces, and it enforces nothing. Say so in the docs, or a recruiter will assume the app is
  blocking contact.
- **Notes as an unstructured personal-data store.** A free-text thread will accumulate opinions
  about people. The retention ticket inherits this; flag it now and keep the bodies out of every
  export, log and search index until then.
- **Preset compatibility.** Saved presets are stored filter payloads. Adding a family without a
  tolerant read breaks every existing preset at once.

### Documentation to update

- `docs/ktl-21/` — the tag family and its seed, the candidate-tag and note contracts, the note
  authorship rule, and what the trail records.
- `openspec/specs/business-catalogs/spec.md` — nine families become ten, and the seeded defaults.
- `openspec/specs/candidate-search/spec.md` — the tag criteria family and its `ANY`/`ALL`
  semantics.
- `openspec/specs/candidate-management/spec.md` — tags and notes as part of the candidate record.
- `README.md` (Spanish) — the Etiquetas family and the Notas personalizadas section.
