## 0. Create Feature Branch

- [x] 0.1 Once KTL-35 is merged, create and switch to `feat/KTL-36` from an up-to-date `main`, named after the ticket file `openspec/KTL-36.md`. (Proposal: traceable KTL-36 delivery. Design: Context, branch state.)

## 1. Domain

- [x] 1.1 Add `Domain/Candidates/CandidateAvailabilityStates.cs`: `Unknown`, `Available`, `Unavailable`, with `All` and `IsKnown`. Delete `CandidateStatus.cs` (`CandidateStatuses`). (Spec: candidate availability check.)
- [x] 1.2 In `Candidate.cs`:
  - remove `Status`, `Availability`, `ChangeStatus`, and the status/availability parameters of `UpdateDetails` and `SetDetails`;
  - add `AvailabilityState` (default `unknown`), `AvailabilityCheckedOn`, `AvailabilityUntil`, `AvailabilityCheckedByUserId` and the `CheckedBy` navigation;
  - add `RecordAvailability(state, checkedOn, until, checkedBy, now)`, which throws on an unknown state, a known state without a date, a date or until on `unknown`, an until on a non-`unavailable` state, or an until before the check date, and clears all three for `unknown`.

  (Design D1, D3.)

- [x] 1.3 Add `CandidateAuditEvents.AvailabilityChecked` (`candidate.availability_checked`) and register it in `AuditEventTypes.All`. Keep `StatusChanged` in the catalogue. Remove `CandidateTextLimits.Availability`. (Design D10.)

## 2. Persistence and migration

- [x] 2.1 `CandidateConfiguration.cs`:
  - map the four columns and the `RESTRICT` FK to `ADM_Users`;
  - add the checks `CK_CND_Candidates_AvailabilityState`, `CK_CND_Candidates_AvailabilityCheck` and `CK_CND_Candidates_AvailabilityUntil`;
  - add the partial index `IX_CND_Candidates_IsActive_AvailabilityCheckedOn` on `("AvailabilityCheckedOn" DESC, "Id") WHERE "IsActive"`;
  - remove the `Status` check and the encrypted `Availability` mapping.

  Update `CandidateCiphertext.cs` (cipher column set) and any encryption backfill or startup list that names `Availability`. (Design D1, Migration Plan 2. Spec: constrained availability check; personal-data-encryption.)

- [x] 2.2 Update `ICandidateRepository`'s summary shape and `CandidateRepository.cs`: drop status and availability, add the availability fields, and `Include` `CheckedBy` wherever the aggregate or summary is loaded for a response.
- [x] 2.3 Bump `SearchFilterNormalization.FilterSchemaVersion` to 2 (the rewrite itself is 2.4). (Design D6.)
- [x] 2.4 Generate `ReplaceCandidateStatusWithAvailability` with `dotnet ef migrations add ReplaceCandidateStatusWithAvailability --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`. Then add:
  - the `UPDATE` of `ADM_SearchPresets` and `OPS_Positions` to version 2 (design D6);
  - a `Down` that restores `Status` (default `'new'`, with its check), an empty `Availability`, and version 1 documents with a complete `statusValues`.

  Inspect the generated SQL: column order, constraint names, no grant changes, and the `text` member untouched. (Spec: stored filters move to schema version 2.)

## 3. Application and Web: candidates

- [x] 3.1 Add `Application/Features/Candidates/RecordCandidateAvailability.cs`:
  - `RecordCandidateAvailabilityCommand(Id, State, CheckedOn, Until, Version)`;
  - its validator, with codes `candidate.availability.invalid`, `.checked_on.invalid` (missing for a known state, present for `unknown`, unparsable, or after UTC today + 1) and `.until.invalid`;
  - the sealed handler: `RequireUpdate`, then find, refuse a removed candidate, `ExpectVersion`, `RecordAvailability` with `actor.UserId`, save with `candidate.availability_checked`, and return `CandidateResponse`.

  (Design D2–D4. Spec: availability is recorded through its own write.)

- [x] 3.2 `CandidateContract.cs`:
  - add `CandidateAvailabilityResponse(State, CheckedOn, Until, CheckedByDisplayName)`, with dates as `yyyy-MM-dd` or an empty string;
  - replace `Status`/`Availability` with `Availability` in `CandidateResponse` and `CandidateSummaryResponse`, and in `CandidateProjection.cs`;
  - remove `StatusInvalid` and `MustBeAPermittedStatus`, and add the new error codes and Spanish messages.
- [x] 3.3 In `CreateCandidate.cs` and `UpdateCandidate.cs`, drop status and availability from the commands and validators. `UpdateCandidateHandler` always records `candidate.updated`. (Spec: general update leaves availability alone; availability checks are audited.)
- [x] 3.4 `CandidateEndpoints.cs`:
  - remove `Status`/`Availability` from `CandidateFieldsRequest` and `UpdateCandidateRequest`;
  - map `PUT /{id:guid}/availability` with a nested `RecordAvailabilityRequest(string State, string? CheckedOn, string? Until, uint Version)`, `Require(actor, CandidatesUpdate)` before `Send`, `.WithName("RecordCandidateAvailability")`, `Produces<CandidateResponse>()` and 400/403/404/409 problems.

## 4. Application and Infrastructure: search, presets, positions

- [x] 4.1 `SearchFilterContract.cs`:
  - replace `StatusValues` with `AvailabilityValues` and `AvailabilityCheckedFrom` in `SearchFiltersInput`, `SearchFiltersValue` (`AvailabilityIsUnrestricted`, `CheckedFrom` as `DateOnly?`) and `ToInput`;
  - normalize as in design D5, with codes `search.availability.invalid` and `search.availability.checked_from.invalid`;
  - remove `SearchErrors.StatusInvalid`.

  `SearchFilterDocument.cs` stores and reads the new members. (Spec: candidate availability and primary-CV state.)

- [x] 4.2 `SearchContract.cs`: `CandidateSearchItem` gets `AvailabilityState` and `AvailabilityCheckedOn` instead of `Status`, and `SearchSortField.Status` becomes `AvailabilityCheckedOn` (wire `availabilityCheckedOn`; `status` is no longer parsed). (Spec: bounded deterministic pagination; minimal search result projection.)
- [x] 4.3 `CandidateSearchQuery.cs`:
  - add the availability predicates;
  - add the nulls-last ordering in `Order` and in the encrypted-stage comparer (`StoredRow` carries `AvailabilityCheckedOn`);
  - project the two new item fields;
  - remove every status reference.

  (Design D5.)

- [x] 4.4 Update `ManageSearchPresets.cs`, `ManagePositions.cs`, `PositionContract.cs`, `IPositionRepository`/`PositionRepository.cs` and `ListPositionCandidates.cs` wherever they reference status filters or the search item shape. Confirm that position requirements validate through the new family. (Spec: position requirements use the candidate-search contract.)

## 5. Import and legacy migration

- [x] 5.1 Remove `Status` and `Availability` from `CandidateImportContract.cs` (`Columns`, bounds), and from `CandidateImportRowEvaluator.cs`; no evaluation produces `StatusUnknown` any more, but the code stays in `ImportReasonCodes.RowCodes` as retired so stored import outcomes keep satisfying `CK_ADM_ImportRowOutcomes_ReasonCode`. Old files then fail with the existing `import.column.unknown`. (Design D9. Spec: imported candidates are ordinary candidates.)
- [x] 5.2 In `Tools/DataMigration`, add a pure `LegacyNotes.Compose(notes, status, availability)` and use it in `MigrationLoader.cs`. Call `SetDetails` without status or availability. Keep `RowValidator`'s status check and the export contract unchanged. (Design D9. Spec: legacy status and availability are kept as note text.)

## 6. Backend tests

- [x] 6.1 Unit tests (`backend/Tests/UnitTests`), with hand-written doubles:
  - `Candidate.RecordAvailability` invariants and reset;
  - validator codes, including the UTC + 1 boundary and an earlier date accepted;
  - the handler: refuses unauthenticated and unauthorized callers before validation, refuses a removed candidate, refuses a stale version, records `actor.UserId` and the audit type;
  - `UpdateCandidateHandler` always records `candidate.updated`;
  - `SearchFilterNormalizationTests` for the new family and version 2;
  - `SearchHandlerTests` for the sort parse (including a `status` refusal);
  - `LegacyNotes.Compose` cases;
  - `AuditEventTypes` contains both the new and the retained types.
- [x] 6.2 Integration tests (Testcontainers):
  - `CandidateApiTests`: brief criteria 1–14, including the checker display name, no user id in responses, and 401/403 before validation for existing and missing ids;
  - `CandidateSchemaTests`: each new check refuses a direct write, the default is `unknown`, the `Status` and `Availability` columns are gone, and the FK is `RESTRICT`;
  - `SearchApiTests`: criteria 17–18;
  - `SearchQueryPlanTests`: the new index serves the «comprobado desde» filter (the nulls-last date sort orders on an expression and still sorts, within budget);
  - `SearchSchemaTests`/`PositionSchemaTests`: version 2;
  - `ImportApiTests`: criterion 20;
  - `AuditApiTests`: the new type is listed and filterable, and `candidate.status_changed` is still accepted as a filter.
- [x] 6.3 Migration test: seed a v1 preset and a v1 position requirement with a status selection and a ciphertext `text` member, apply the migration, and assert version 2, no `statusValues`, a complete `availabilityValues`, and the identical `text` ciphertext. Both still parse and apply through the API. (Spec: stored filters move to schema version 2.)
- [x] 6.4 Review and update every existing backend test affected by the removal of status and availability: `CandidateHandlerTests`, `CandidateTextLimitTests`, `ImportRowSemanticsTests`, `PositionHandlerTests`, `PositionApiTests`, `PositionCandidateApiTests`, `PostgreSqlPersistenceTests`, `SearchParityFixture`, `ReferenceSearchEvaluator`, `EncryptedSearchParityTests`, `EncryptedSearchPerformanceTests`, `EncryptionBackfillTests`, `FieldEncryptionStartupTests`, `GeneratedExportSetTests`, and the KTL-7 loader tests (criterion 21, re-run idempotence). Keep `ProjectDependencyTests` green.
- [x] 6.5 Run `npm run test:backend` from `frontend/` (Docker running) and inspect the output. Then connect to the Testcontainers or development database and confirm that `CND_Candidates` has the new columns and constraints, and no `Status`/`Availability`.

## 7. Frontend: models, services, formatting

- [x] 7.1 In `features/candidates/models/candidate.models.ts`:
  - remove `CandidateStatus`, `status` and `availability`;
  - add `CandidateAvailabilityState` and `CandidateAvailability`;
  - change the sort field `'status'` to `'availabilityCheckedOn'` and the list query `status` to `availability`;
  - update `CandidateListItem`, `CandidateDraft` and `EMPTY_CANDIDATE_DRAFT`.

  Update `search.models.ts` (`availabilityValues`, `availabilityCheckedFrom`, defaults, clone) and `ALL_AVAILABILITY_STATES`. (Design D5.)

- [x] 7.2 `candidate.api.ts` and `candidate.service.ts`:
  - `recordAvailability(id, input)` through `ApiTransport`, sending the cached version and replacing the cached aggregate with the response;
  - client-side validation throwing `TranslatableError` with the same rules as the API;
  - list queries send `availabilityValues`.

  (Design D7.)

- [x] 7.3 Add `formatElapsed(day, now)` to `core/i18n/format.ts`, using `Intl.RelativeTimeFormat` with `numeric: 'auto'` and calendar-day arithmetic without `Date` parsing of `yyyy-MM-dd`. (Design D7.)

## 8. Frontend: candidate page

- [x] 8.1 Add `features/candidates/components/candidate-availability.tsx`, `.logic.ts` and `.css`:
  - the lines, with «por {name}» only when a checker name is present;
  - the «(vencido)» mark and its hint;
  - «Sigue igual» (accessible name «Sigue igual: registrar la comprobación con fecha de hoy»);
  - «Cambiar…»/«Registrar comprobación», opening an inline form: a labelled radio group, «Hasta (opcional)» only for «No disponible», and «Comprobado el» defaulting to today and hidden for «Sin comprobar»;
  - inline «Deshacer» holding the previous check in component state;
  - a polite live region, and focus returning to «Cambiar…»;
  - read-only without `candidates.update` (via `usePermission`) or on a removed candidate;
  - `name=` and `data-testid` on every control;
  - Kepler tokens only, and no inline grid styles.

  (Spec: availability block on the candidate page.)

- [x] 8.2 Move the position links to the page, and add `candidate-pipeline-badges.tsx` with a `.logic.ts` (`pipelineBadges(links)`). `CandidatePositionsPanel` takes the links and reports changes through `onLinksChange`. No positions request is sent without `positions.read`. (Design D8. Spec: pipeline badges derived from positions.)
- [x] 8.3 `candidate-detail-page.tsx`: render the badges beside the name and the availability block under the contact line, outside the edit-mode coordinator. Remove the status and availability rows from `candidate-main-panel.tsx` and the fields from `candidate-form.tsx`/`candidate-form.logic.ts`, keeping every other `name=` and `data-testid`. Check `candidate-create-page.tsx`. (Spec: candidate values are shown in readable form.)

## 9. Frontend: list, search, presets, export, audit

- [x] 9.1 Candidate list:
  - in `candidate-table.tsx`, a «Disponibilidad» column showing the value and `formatElapsed`, or «Sin comprobar», sortable as `availabilityCheckedOn`;
  - in `candidate-filters-bar.tsx`, `candidate-list.logic.ts` and `candidate-list-page.tsx`, the «Disponibilidad» select, URL param `availability`, its chip, and removal of `statusLabel`/`STATUS_OPTIONS`.

  (Spec: candidate tables show availability.)

- [x] 9.2 Search editor:
  - `search-criteria-form.tsx`, `search-criteria.logic.ts` and `search-basic-filters.logic.ts`: the status disclosure becomes the availability disclosure, with the same interaction rules, plus a labelled «Comprobado desde» date input;
  - the criteria summary states a set date;
  - `search-presets.service.ts` normalizes the new members.

  (Spec: shared criteria editor with availability.)

- [x] 9.3 `search-results.tsx`: the «Disponibilidad» column. Check `position-detail-page.tsx` matches. `export.service.ts`: `disponibilidad` and `comprobado_el` instead of `estado`. `dashboard-page.tsx`: drop `status`. `audit.logic.ts`: add `candidate.availability_checked`. (Spec: candidate tables show availability.)
- [x] 9.4 `assets/i18n/es.json`:
  - add `candidate.availability.*`, `candidate.pipeline.*`, the filter, chip and summary keys, the `admin.audit.eventType.candidate.availability_checked` label, and the validation messages;
  - remove the status and free-text availability keys;
  - keep `admin.audit.eventType.candidate.status_changed`;
  - update `admin.import.contract` if needed;
  - add `en.json` values where convenient.

  No hardcoded JSX copy, and correct accents.

## 10. Frontend tests

- [x] 10.1 New unit specs (`frontend/tests/unit`):
  - `candidate-availability.spec.tsx`: unknown, known, lapsed, reconfirm, undo, change, reset, read-only, a removed candidate, focus, the live region, and adopting the new version so a following main-panel save sends it;
  - logic specs for the availability and badge helpers;
  - `format` (`formatElapsed`, including a negative-UTC-offset day);
  - the service request contract for `recordAvailability`.
- [x] 10.2 Review and update the existing unit specs affected by the change: candidate detail page (badges with and without `positions.read`, a stage change updating the badges), candidate form, main panel, candidate list page and logic, search results, search criteria form and basic filters, preset service, export service, dashboard, audit labels, and the position detail page. Replace status fixtures in `tests/unit/support/`.
- [x] 10.3 Run `npm test` from `frontend/` and inspect the output: unit, integration and security projects.

## 11. Security evidence

- [x] 11.1 Extend `frontend/tests/security`: `PUT /api/candidates/{id}/availability` refuses unauthenticated callers and a `candidates.read`-only actor with a valid and a malformed body, and identically for existing and missing ids. Responses carry no checker user id.
- [x] 11.2 Run `npm run security:rls` and `npm run security:storage` and inspect the output. Confirm that `ktl_runtime` grants on `CND_Candidates` are unchanged and `DELETE` is still refused.

## 12. End to end

- [x] 12.1 Update `tests/e2e/support/seed-candidate.ts` and the specs that set or read the status or free-text availability: `candidate-profile`, `candidate-list-paging`, `advanced-search`, `audit-trail`, `candidate-documents`, `candidate-cv-row-preview`, `navigation-responsive`, `security-ops` and `secure-access`. Selectors use roles, labels or test ids.
- [x] 12.2 Write `tests/e2e/candidate-availability.spec.ts` with `Date.now()`-marked candidates:
  - record «No disponible hasta», «Sigue igual», «Deshacer»;
  - amend with an earlier date;
  - a lapsed check seeded through the API;
  - the list column, sort by «Disponibilidad», and the list filter;
  - the advanced search «Comprobado desde».
- [x] 12.3 Run `docker compose up --build` so the stack serves the new API and runs the migration. Run `npx playwright test tests/e2e/candidate-availability.spec.ts` and the specs from 12.1, and inspect the results. Then run the full `npm run e2e`.
- [x] 12.4 Check that the global teardown left no marked candidates, and that existing presets and positions in the development database load at version 2.

## 13. Documentation

- [x] 13.1 Update:
  - `docs/ktl-8/candidates.md` (endpoint, payloads, error codes, audit events);
  - `docs/ktl-10/search.md` and `docs/ktl-18/list-contract.md` (family, sort, projection);
  - `docs/ktl-10/query-plans.md` (new plan);
  - `docs/ktl-19/audit-contract.md`;
  - `docs/ktl-17/import-file-contract.md` and `row-reason-codes.md`;
  - `docs/ktl-7/access-export-procedure.md` and `database-notes.md`;
  - `docs/ktl-33/performance.md` if the plan or timings change.
- [x] 13.2 Write `docs/ktl-36/release-notes.md`:
  - the status is gone and availability checks replace it;
  - stored presets and position requirements lost any status selection;
  - old list URLs with `sort=status` fail;
  - CSV import files must drop `status`/`availability`.

  Update `README.md` (in Spanish) if it mentions candidate states.

## 14. Quality gates

- [x] 14.1 From `frontend/`, run `npm run lint` and `npm run format:check`, and fix all findings.
- [x] 14.2 Run `npm run build:all` (warnings are errors) and confirm it succeeds.
- [x] 14.3 Run `openspec validate ktl-36-candidate-availability-checks --strict`.
