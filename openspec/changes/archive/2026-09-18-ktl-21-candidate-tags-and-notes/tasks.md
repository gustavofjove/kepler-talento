# Tasks — KTL-21 candidate tags and custom notes

Requirements are in [specs/](./specs/); the approach and the decisions behind it are in
[design.md](./design.md). Every task below names the capability it serves.

## 0. Create Feature Branch

- [x] 0.1 Create and check out `feat/KTL-21` from an up-to-date `main`. Never commit to `main`.

## 1. Tag catalog family (business-catalogs)

- [x] 1.1 Add `CatalogFamilies.Tag = "tag"` to `backend/Domain/Catalogs/CatalogFamily.cs` and to `All`.
- [x] 1.2 Grep for the closed set before going further — `CatalogFamilies.All`, the literal "nine", the frontend `CatalogFamily` union — and list every place that asserts the count, so none is left half-updated.
- [x] 1.3 Add the `tag` family's default values to `backend/Infrastructure/Persistence/CatalogSeedData.cs` (`Recontratable`, `No contactar`, `Referido por plantilla`), with explicit codes where derivation would collide, honouring the seed's idempotence and no-silent-uniquification rules.
- [x] 1.4 Extend `src/app/features/catalogs/models/catalog.models.ts` with `tag` in the `CatalogFamily` union and `Etiquetas` in `CATALOG_FAMILY_LABELS`. Do **not** add a browser-side default vocabulary for it (design decision 1).
- [x] 1.5 Backend unit tests: `tag` is a known family; an unknown family is still refused; the seed is idempotent and leaves other families untouched.
- [x] 1.6 Integration test: the seed run against a database holding the other nine families creates the tags and alters nothing else.

## 2. Tag persistence (candidate-persistence)

- [x] 2.1 Add `backend/Domain/Candidates/CandidateTag.cs` (`CandidateRelation` with `TagId` and `TagFamily`), and `Candidate.ReplaceTags` following the existing `Replace*` members.
- [x] 2.2 Add `Configurations/CandidateTagConfiguration.cs`: `CND_CandidateTags`, composite FK `(TagId, TagFamily)` → `CAT_Items` via `CandidateRelationMapping`, family check constraint, unique `(CandidateId, TagId)`, indexes on `CandidateId` and `(TagId, TagFamily)`, no cascade delete. Register the set on `ApplicationDbContext`.
- [x] 2.3 Integration tests: the composite FK refuses a `sector` id stored as a tag; the unique index refuses the same tag twice, including under concurrent writes; a logically deleted candidate keeps its tags.

## 3. Note persistence (candidate-notes, candidate-persistence)

- [x] 3.1 Add `backend/Domain/Candidates/CandidateNote.cs`: body, nullable `AuthorUserId`, created/updated timestamps, `IsActive`, `DeletedAtUtc`, own `Version`; behaviour for edit and retire, with retirement idempotent.
- [x] 3.2 Add `Configurations/CandidateNoteConfiguration.cs`: `CND_CandidateNotes`, FK to the candidate with no cascade, nullable `AuthorUserId` FK to `ADM_Users` with no cascade, non-empty body check, 4000-character limit, index `(CandidateId, CreatedAtUtc DESC)` filtered on `IsActive`. Register the set on `ApplicationDbContext`.
- [x] 3.3 Generate the migration with `dotnet ef migrations add AddCandidateTagsAndNotes --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`, then hand-add the check constraints and the `ktl_runtime` grants the generator does not emit: full DML on `CND_CandidateTags`, and `SELECT, INSERT, UPDATE` with **no `DELETE`** on `CND_CandidateNotes`.
- [x] 3.4 Apply the migration through the `migrator` container and confirm the schema, constraints and indexes landed as written.
- [x] 3.5 Integration test asserting against the database catalog that `ktl_runtime` holds no `DELETE` on `CND_CandidateNotes` and no schema-modification privilege on either new table.

## 4. Tag assignment API (candidate-management)

- [x] 4.1 Add `CandidateTagInput` and `SetCandidateTagsCommand` to `Application/Features/Candidates/SetCandidateRelations.cs` with its validator and handler, resolving names through `CandidateCatalogLookup` against the `tag` family only, repeating the `candidates.update` guard.
- [x] 4.2 Add `CandidateTagResponse` to `CandidateContract.cs` and project tags in `CandidateProjection.cs` and `GetCandidate.cs`, so the detail response carries them and the list response does not.
- [x] 4.3 Add `PUT /api/candidates/{id}/tags` to `Web/Features/Candidates/CandidateEndpoints.cs` as `SetCandidateTags`, guard before validation, explicit `.Produces*` metadata including 403, 404 and 409.
- [x] 4.4 Record a tag-assignment audit event through `CandidateAuditEvents`, carrying identifiers and codes only.
- [x] 4.5 Backend unit tests: wholesale replacement, a tag from another family refused, a duplicate tag refused, a write to a removed candidate refused, a version mismatch as 409, and the guard refusing before validation.
- [x] 4.6 Integration test: resubmitting a candidate's tags succeeds when one of them has since been deactivated (candidate-management, "Candidate with a deactivated tag is saved again").

## 5. Notes API (candidate-notes)

- [x] 5.1 Create `Application/Features/Candidates/Notes/` with one file per use case — `ListCandidateNotes`, `AddCandidateNote`, `UpdateCandidateNote`, `SetCandidateNoteActive` — each a sealed record request, its validator, and a sealed handler repeating its permission guard.
- [x] 5.2 Write `ICurrentActor.UserId` as the author. Keep null support for system or historical rows, and never fall back to `ExternalKey` (design decision 4).
- [x] 5.3 Add the four endpoints to `CandidateEndpoints.cs` with guards before validation and explicit metadata; there is deliberately no `DELETE` verb.
- [x] 5.4 Expose the active notes as `customNotes` on the candidate detail response (design decision 8), including only the author's internal id and server-resolved display name; leave the existing `notes` string untouched. Return the same projection from the notes list endpoint so it can refresh after mutations without calling the admin-only users endpoint.
- [x] 5.5 Record add, edit and retire audit events carrying identifiers and codes only — never a body.
- [x] 5.6 Backend unit tests: blank and over-length bodies refused with a stable code; edit preserves identifier, candidate, author and creation time; stale version as 409; retirement idempotent; a note addressed through the wrong candidate is not found; writes to a removed candidate refused.
- [x] 5.7 Integration test: adding a note does not advance the candidate's version, so a concurrent candidate edit is not refused (candidate-notes, "Adding a note does not conflict with a candidate edit").

## 6. Tag search filter (candidate-search)

- [x] 6.1 Add the tag criteria family to the search request contract and its validator, rejecting a tag criterion that carries a level with a stable validation problem.
- [x] 6.2 Implement the tag join in `Infrastructure/Persistence/CandidateSearchQuery.cs` with `ANY` and `ALL` semantics, mirroring the skill join, producing no duplicate candidates.
- [x] 6.3 Confirm the result projection is unchanged — a candidate matched by a tag carries no tag in its item.
- [x] 6.4 Integration tests: `ANY`, `ALL` across separate rows, an empty family restricting nothing, an unknown tag matching nothing, no duplicates, deterministic paging, and a level on a tag criterion refused.

## 7. Security evidence (all capabilities)

- [x] 7.1 Integration tests per new endpoint for an unauthenticated caller and for an authenticated caller lacking the permission, asserting the refusal happens **before** validation and stores nothing.
- [x] 7.2 Test proving an API-created note stores the acting internal user identifier and no email, display name or identity-provider subject; test that the response resolves the display name without exposing user-administration fields.
- [x] 7.3 Log assertions: no note body, no tag assignment and no author personal data reaches the logs through the new paths.
- [x] 7.4 Add the grant assertions for both new tables to `tests/security/`, and run `npm run security:rls` and `npm run security:storage`.

## 8. Frontend — tags

- [x] 8.1 Add `CandidateTag` and `Candidate.tags` to `src/app/features/candidates/models/candidate.models.ts`; add the tags call to `services/candidate.api.ts` and `candidate-relations.service.ts`.
- [x] 8.2 Build `components/candidate-tags.tsx` with co-located plain `.css`: removable chips plus a select of active tags, reading the catalog through `useCatalogs()`, with `usePermission()` hoisted to a const at the top. Keep `name=` and `data-testid` on every control.
- [x] 8.3 Wire it into `candidate-form.tsx` and `pages/candidate-detail-page.tsx`; use `.span-all` rather than inline grid styles.
- [x] 8.4 Unit spec `tests/unit/candidate-tags.spec.tsx`: assignment, removal, the empty state, a deactivated tag still displayed but not offered, and the permission-gated control.

## 9. Frontend — notes

- [x] 9.1 Add `CandidateNote` and `Candidate.customNotes` to the models; add the four calls to `candidate.api.ts`.
- [x] 9.2 Add `candidate-notes.service.ts` as a singleton holding a signal from `core/state/signal.ts`, register it in `core/di/services.ts`, and give it a `useCandidateNotes()` subscribing hook because it is read during render. Validation throws `TranslatableError`.
- [x] 9.3 Build `components/candidate-notes.tsx` with co-located `.css`: a list with a heading, newest first, each entry showing author and date (`Autor desconocido` when null), plus a controlled `useState` add form and edit and retire actions. Report errors with `useErrorToast()`; render bodies as text, never as markup.
- [x] 9.4 Wire it into `pages/candidate-detail-page.tsx` as its own section.
- [x] 9.5 Unit spec `tests/unit/candidate-notes.spec.tsx`: add, edit, retire, blank-body validation, unknown author, and a 409 surfaced to the user.

## 10. Frontend — search and presets (candidate-search, saved-search-presets)

- [x] 10.1 Add `tagCriteria` and `tagMode` to `SearchFilters` and `EMPTY_SEARCH_FILTERS` in `src/app/features/search/models/search.models.ts`.
- [x] 10.2 Render the tag family through the shared criteria group, dialog and summary with the level control suppressed; send it from `services/candidate-search.service.ts`.
- [x] 10.3 Make the preset reader default an absent tag family to empty with `ANY`, so presets stored before this change still load and apply (design decision 7).
- [x] 10.4 Unit specs: update `search-criteria-form.spec.tsx`, `search-criteria-summary.spec.tsx`, `candidate-search.service.spec.ts`, and `search-presets.service.spec.ts` with a payload that has no tag family.

## 11. Spanish copy

- [x] 11.1 Add every new key to `src/assets/i18n/es.json` under flat `feature.section.element` keys — the tag family label, the tags section, the notes section, the unknown author, and the search tag family — with correct accents. Use whole sentences with interpolation, never concatenated fragments.
- [x] 11.2 Confirm no new JSX copy is hardcoded and that no file is added to `LEGACY_HARDCODED_COPY`. If the tags or notes editor lands in a file already on that list, move that file's copy to keys and remove it from the list in this change.
- [x] 11.3 Run `tests/unit/i18n.spec.ts` and confirm no key is missing.

## 12. Review and update existing tests (MANDATORY)

- [x] 12.1 Review and update every existing unit test the change affects — `catalog.service.spec.ts`, `catalog-loading-states.spec.tsx`, `candidate-relations.service.spec.ts`, `candidate-form.spec.tsx`, `candidate-profile-sections.spec.tsx`, `advanced-search-page.spec.tsx`, and the backend catalog and candidate suites — and fix what the tenth family or the new collections break.

## 13. Run the suites (MANDATORY)

- [x] 13.1 Run `npm test` and inspect the output; fix failures rather than adjusting expectations to match a defect.
- [x] 13.2 Run `npm run test:backend` with Docker running, and inspect the resulting PostgreSQL state for the new tables, constraints, indexes and grants.
- [x] 13.3 Run `npm run build:all` — warnings are errors.

## 14. Exercise it end to end (MANDATORY)

- [x] 14.1 Write `tests/e2e/candidate-tags-notes.spec.ts`: create a tag in Catálogos → assign it to a candidate → add, edit and retire a note → filter the advanced search by that tag. Select by role, accessible name or `data-testid`; no hardcoded Spanish text.
- [x] 14.2 Extend `tests/e2e/catalogs-crud.spec.ts` and `tests/e2e/advanced-search.spec.ts` for the new family.
- [x] 14.3 Run `npx playwright test tests/e2e/candidate-tags-notes.spec.ts` against the dev server on :4300 and inspect the result.
- [x] 14.4 Run the full `npm run e2e` suite and inspect the result.
- [x] 14.5 Restore the seed data afterwards.

## 15. Documentation (MANDATORY)

- [x] 15.1 Write `docs/ktl-21/` — the tag family and its seed, the candidate-tag and note contracts, the note body limit, the authorship rule, and what the audit trail records. State explicitly that "No contactar" is a label the application does not enforce.
- [x] 15.2 Add a release note recording that system or historical notes without an internal author id display `Autor desconocido` (design decision 4).
- [x] 15.3 Update `README.md` (Spanish) with the Etiquetas family and the Notas personalizadas section, leaving commands, paths and identifiers untranslated.
- [x] 15.4 Update `AGENTS.md` only if a rule it states has changed.

## 16. Final gates (MANDATORY)

- [x] 16.1 Run `npm run lint` and `npm run format:check`; fix what they report.
- [x] 16.2 Re-run `npm run security:rls` and `npm run security:storage`.
- [x] 16.3 Run `openspec validate ktl-21-candidate-tags-and-notes --strict` and confirm the deltas are well formed.
