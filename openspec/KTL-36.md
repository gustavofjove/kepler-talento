# KTL-36 — Candidate availability checks instead of a global candidate status

## [original]

Having a status for a candidate on a specific position and another status just for the candidate
is confusing. The candidate status (`new`, `available`, `in_process`, `hired`, `rejected`) overlaps
with the position stage (`new`, `shortlisted`, `interview`, `hired`, `rejected`), and there is also
a free-text «Disponibilidad» field.

Agreed direction (explored in conversation):

- Drop the global candidate status. The position stage is the only pipeline state. «En proceso» and
  «Contratado» on a candidate become badges derived from their position links.
- Replace it with an availability check: `unknown` (default), `available` or `unavailable`, an
  optional «a partir de / hasta» date, the date it was checked («comprobado el», defaulting to
  today, never in the future, may be earlier than the stored one so mistakes can be amended) and
  who checked it. Only the latest check is kept.
- Availability leaves the general candidate form. The candidate detail page gets its own widget
  with «Sigue igual» (reconfirm: same value, new date) and «Cambiar…». Right after a check, a toast
  offers «Deshacer», which resubmits the previous check.
- When the «hasta» date has passed, «Sigue igual» is not offered; the user must pick a new value.
- No fixed staleness threshold: show the date and how long ago it was. Search lets the user filter
  by availability and by «comprobado desde».
- The free-text «Disponibilidad» field is dropped.
- Reuse `candidates.update`. Concurrency stays on the candidate version (2–3 users).
- Data migration is not relevant: all existing data is test data.
- Candidate labels (catalog, dated assignments) are a later ticket.

## [enhanced]

**Status:** Ready for an OpenSpec change
**Depends on:** KTL-8 (candidate API), KTL-10/KTL-18 (search and list contract), KTL-15/KTL-30
(positions and position candidates), KTL-17 (CSV import), KTL-7 (Access migration), KTL-33
(encryption)

### Summary

A candidate has two overlapping states:

- a **global status** (`new`, `available`, `in_process`, `hired`, `rejected`);
- a **stage per position** (`new`, `shortlisted`, `interview`, `hired`, `rejected`).

There is also a free-text «Disponibilidad». The spec states that the status and the stages never
affect each other ([position-candidates](specs/position-candidates/spec.md)), so a candidate can be
«Descartado» globally while at «Entrevista» for a position, and nothing explains which is true. Three
of the five global statuses describe a hiring process, and a hiring process only exists per
position.

This ticket:

1. **Removes the global status.** The position stage becomes the only pipeline state. On the
   candidate page, the badges «En proceso» and «Contratado» are derived from the candidate's
   position links.
2. **Removes the free-text «Disponibilidad».**
3. **Adds an availability check:** a value (`unknown`, `available`, `unavailable`), an optional
   «hasta» date, the date it was checked and who checked it. It is recorded from the candidate page
   with its own action, never through the general form, so editing a phone number can never make an
   old check look fresh. «Sigue igual» renews the date without changing the value.

### User story

As an HR user, I want to record whether a candidate is available and when I last confirmed it, so
that I can tell how much to trust the information and find recently confirmed candidates, without a
second pipeline status that contradicts the position stages.

### Decisions

| Topic                        | Decision                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Values                       | `unknown` («Sin comprobar», the default), `available` («Disponible»), `unavailable` («No disponible»).                                                                                                                                                                                                                                                                                                                                                                                                                          |
| «Hasta» date                 | Optional, and only with `unavailable` («No disponible hasta 15/01/2027»). It also covers notice periods. `available` takes no date: "available from a future date" is the same as "unavailable until that date".                                                                                                                                                                                                                                                                                                                |
| Check date («comprobado el») | A calendar date (`DateOnly`, like `ReceivedAt`). The SPA defaults it to today in the browser's time zone. The API refuses a date later than the current UTC date plus one day (tolerance for time zones ahead of UTC). It **may** be earlier than the stored check, which is how a mistake is amended.                                                                                                                                                                                                                          |
| Checked by                   | The server records the current actor's stored user id, as notes record their author. The client never sends it.                                                                                                                                                                                                                                                                                                                                                                                                                 |
| Latest only                  | One check per candidate, stored on the candidate row. No history table. Each check writes an audit event.                                                                                                                                                                                                                                                                                                                                                                                                                       |
| `unknown`                    | Carries no check date, «hasta» date or checker. Setting it through «Cambiar…» clears all three.                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| One action                   | Every availability write is a check: `PUT /api/candidates/{id}/availability`. «Sigue igual» resends the stored value and «hasta» date with today's date. «Cambiar…» sends a new value. There is no reconfirm-specific code path.                                                                                                                                                                                                                                                                                                |
| «Sigue igual» availability   | Offered only when the value is `available` or `unavailable` and the «hasta» date, if any, is today or later. When the «hasta» date has passed («vencido»), the widget asks for a new check instead.                                                                                                                                                                                                                                                                                                                             |
| Undo                         | Right after a successful check, the widget shows «Deshacer» inline, beside the new line. It stays until the page is left or another check is made. Undo resends the previous value, «hasta» date and check date. The checker becomes the person who undid it (accepted). The inline control replaces the toast from the original brief: toasts here disappear after about 4 s, and an action that disappears on a timer fails WCAG 2.2.1.                                                                                       |
| Concurrency                  | The candidate's `Version`, like every other candidate write. After a check, the page adopts the returned aggregate and version, so the user's next edit is not refused because of their own click.                                                                                                                                                                                                                                                                                                                              |
| Removed candidate            | A check on a logically removed candidate is refused with `candidate.removed`, as notes and relations are.                                                                                                                                                                                                                                                                                                                                                                                                                       |
| Permission                   | `candidates.update`, checked before validation. Reading follows `candidates.read`.                                                                                                                                                                                                                                                                                                                                                                                                                                              |
| No staleness threshold       | The UI shows the date and the time elapsed («hace 6 meses»). Freshness is chosen per search with «Comprobado desde», never by a constant.                                                                                                                                                                                                                                                                                                                                                                                       |
| Derived badges               | Shown on the candidate page only, next to the name, and only to actors with `positions.read`. They are derived from `GET /api/candidates/{id}/positions`, which the «Posiciones» panel already loads. «En proceso»: at least one link at `new`, `shortlisted` or `interview` on an **open** position. «Contratado»: at least one link at `hired`, on any position. Both can show at once. Neither is stored, and neither appears in tables or search.                                                                           |
| Tables                       | The «Estado» column becomes «Disponibilidad» in the candidate list, search results and «Candidatos que encajan». It shows the value plus the time elapsed («No disponible · hace 6 meses»), or «Sin comprobar». «Candidatos de la posición» is unchanged: it already shows the stage instead.                                                                                                                                                                                                                                   |
| Sorting                      | The sort field `status` is replaced by `availabilityCheckedOn`. Descending puts the most recent check first, and `unknown` (no date) sorts last in both directions. The candidate id remains the tie-breaker. An old URL with `sort=status` is refused by the API like any unknown sort field.                                                                                                                                                                                                                                  |
| Filtering                    | The status family is replaced by an **availability family**: `availabilityValues` (ANY of the three values; an empty or complete selection restricts nothing) and `availabilityCheckedFrom` (a date; blank restricts nothing; when set, only candidates checked on or after it match, so `unknown` never matches). The two combine with the other families by AND. The list filter bar gets a single-value «Disponibilidad» select; the advanced search, the preset editor and position requirements get the full family.       |
| Stored filter documents      | `FilterSchemaVersion` becomes **2**. A migration rewrites every stored preset (`ADM_SearchPresets.Filters`) and position requirement (`OPS_Positions.Requirements`): it removes `statusValues`, adds empty availability members and sets `version` to 2. The status selection is dropped, which broadens a restricted v1 document; that is acceptable because all current data is test data. Only the `text` member is encrypted, so the rewrite does not touch ciphertext. The reader keeps refusing any version other than 2. |
| CSV import (KTL-17)          | The `status` and `availability` columns are removed from the contract. Imported candidates start as `unknown`. A file that still has either column is refused with `import.column.unknown`, as for any column outside the contract.                                                                                                                                                                                                                                                                                             |
| Access migration (KTL-7)     | The export contract keeps `Status` (still validated against the five codes) and `Availability`. The loader no longer stores them as fields: every candidate loads as `unknown`. When the legacy status is not `new`, or the legacy availability is not blank, the loader appends them to the candidate's `Notes` field as «Estado en Access: Contratado.» and «Disponibilidad en Access: …». It recomposes `Notes` from the row on every run, so re-runs stay idempotent.                                                       |
| Audit                        | New event type `candidate.availability_checked` (candidate id only, never the value or dates, like `position.candidate_stage_changed`). `candidate.status_changed` is no longer emitted. It stays in `AuditEventTypes.All` and the audit UI labels, so existing rows still read.                                                                                                                                                                                                                                                |
| Encryption                   | The new value, dates and user id are not free text, so they stay in clear (per `personal-data-encryption`, "status values, flags, timestamps"). Dropping the encrypted `Availability` column removes it from the cipher column set.                                                                                                                                                                                                                                                                                             |
| CSV export                   | `estado` is replaced by `disponibilidad` (Spanish label) and `comprobado_el` (`yyyy-MM-dd`, blank for `unknown`).                                                                                                                                                                                                                                                                                                                                                                                                               |
| Create form                  | No availability input. A new candidate is `unknown`, and the user records the first check from the candidate page.                                                                                                                                                                                                                                                                                                                                                                                                              |

### Candidate page

```
 Ana Ruiz   [En proceso]                                  [Desactivar]
 ana.ruiz@example.test · 600 111 222

 Disponibilidad: No disponible hasta 15/01/2027
 Comprobado el 12/03/2026 por Marta G. · hace 7 meses
 [Sigue igual]  [Cambiar…]
```

- The badges sit beside the name. The availability block sits under the contact line, outside the
  per-panel edit-mode coordinator, like the «Posiciones» panel: it acts immediately and never counts
  towards unsaved changes.
- «Cambiar…» opens an inline form below the block:
  - radio group «Disponibilidad» (Sin comprobar / Disponible / No disponible);
  - «Hasta (opcional)» date, shown only for «No disponible»;
  - «Comprobado el» date, default today, hidden for «Sin comprobar»;
  - «Guardar» and «Cancelar».
- States:
  - `unknown`: «Sin comprobar» and only «Registrar comprobación» (the same form).
  - **vencido** (the «hasta» date has passed): the line reads «No disponible hasta 15/01/2027
    (vencido)», with a hint «El plazo indicado ya ha pasado. Vuelve a comprobar la
    disponibilidad.» «Sigue igual» is not offered.
- Without `candidates.update`, or on a removed candidate, the block is read-only.
- «Datos principales» loses its «Estado» and «Disponibilidad» rows. Today «Estado» shows the raw
  code; that row goes away rather than being fixed.

### API and data changes

**Domain** (`backend/Domain/Candidates/`):

- `Candidate`:
  - remove `Status`, `Availability`, `ChangeStatus` and the status/availability parameters of
    `UpdateDetails`/`SetDetails`;
  - add `AvailabilityState`, `AvailabilityCheckedOn` (`DateOnly?`), `AvailabilityUntil`
    (`DateOnly?`) and `AvailabilityCheckedByUserId` (`Guid?`, navigation to `User`, as notes have);
  - add `RecordAvailability(state, checkedOn, until, userId, now)`, which enforces the invariants.
- Replace `CandidateStatuses` with `CandidateAvailabilityStates`.
- `CandidateAuditEvents.AvailabilityChecked`, registered in `AuditEventTypes.All`.
- `CandidateTextLimits.Availability` removed.

**Application** (`backend/Application/Features/Candidates/`):

- New `RecordCandidateAvailability.cs`:
  - command `(Id, State, CheckedOn, Until, Version)`;
  - validator: known state; a wire date where present; `CheckedOn` required unless `unknown`, and
    not after UTC today + 1; `Until` only with `unavailable` and not before `CheckedOn`; `CheckedOn`
    and `Until` absent for `unknown`;
  - handler: guard → find → refuse removed → expect version → record → save with
    `candidate.availability_checked` → return `CandidateResponse`.
- `CandidateContract.cs`:
  - `CandidateResponse` and `CandidateSummaryResponse` lose `Status` and `Availability` and gain
    `Availability: CandidateAvailabilityResponse(State, CheckedOn, Until, CheckedByDisplayName)`.
    The display name is exposed the way a note author's is, and the user id is not exposed.
  - `StatusInvalid` and `MustBeAPermittedStatus` are removed. New codes:
    `candidate.availability.invalid`, `candidate.availability.checked_on.invalid`,
    `candidate.availability.until.invalid`.
- `CreateCandidate.cs`, `UpdateCandidate.cs` and `CandidateProjection.cs`: drop status and
  availability. `UpdateCandidate` always emits `candidate.updated`.
- `Import/CandidateImportContract.cs` and `CandidateImportRowEvaluator.cs`: drop both columns and
  `ImportReasonCodes.StatusUnknown`.

**Search** (`backend/Application/Features/Search/`, `backend/Infrastructure/Persistence/CandidateSearchQuery.cs`):

- `SearchFiltersInput`/`SearchFiltersValue`:
  - `StatusValues` becomes `AvailabilityValues` and `AvailabilityCheckedFrom`;
  - normalization gets new error codes `search.availability.invalid` and
    `search.availability.checked_from.invalid`;
  - `FilterSchemaVersion = 2`;
  - `SearchFilterDocument` stores and reads the new members.
- `CandidateSearchItem`: `Status` becomes `AvailabilityState` and `AvailabilityCheckedOn`. The
  checker and the «hasta» date stay out of the minimal projection.
- `SearchSortField.Status` becomes `AvailabilityCheckedOn` (wire value `availabilityCheckedOn`),
  with nulls last in both directions. This covers the SQL path and the in-memory encrypted-stage
  comparer.

**Web**:

- `Web/Features/Candidates/CandidateEndpoints.cs`:
  - add `PUT /{id:guid}/availability` with request
    `RecordAvailabilityRequest(string State, string? CheckedOn, string? Until, uint Version)`, named
    `RecordCandidateAvailability`, `Require(actor, CandidatesUpdate)` before dispatch, and
    `Produces<CandidateResponse>` plus 400/403/404/409;
  - remove `Status` and `Availability` from `CandidateFieldsRequest` and `UpdateCandidateRequest`.
- No new route group, so `Program.cs` is unchanged.

**Persistence** (`CND_Candidates`, one EF Core migration):

- Drop `Status`, `CK_CND_Candidates_Status` and the encrypted `Availability` column.
- Add:
  - `AvailabilityState text NOT NULL DEFAULT 'unknown'`;
  - `AvailabilityCheckedOn date NULL`;
  - `AvailabilityUntil date NULL`;
  - `AvailabilityCheckedByUserId uuid NULL`, FK to `ADM_Users` `ON DELETE RESTRICT`, as
    `CND_CandidateNotes.AuthorUserId`.
- Checks:
  - `CK_CND_Candidates_AvailabilityState`: `IN ('unknown','available','unavailable')`;
  - `CK_CND_Candidates_AvailabilityCheck`: `unknown` ⇔ `AvailabilityCheckedOn IS NULL`, and
    `unknown` has no checker;
  - `CK_CND_Candidates_AvailabilityUntil`: `AvailabilityUntil IS NULL OR (state = 'unavailable' AND AvailabilityUntil >= AvailabilityCheckedOn)`.
- Index `IX_CND_Candidates_IsActive_AvailabilityCheckedOn` for the sort and the «comprobado
  desde» filter.
- The same migration rewrites stored filter documents to version 2 (see Decisions).
- Grants: no new table. `ktl_runtime` already holds `SELECT, INSERT, UPDATE` on `CND_Candidates`,
  so no grant changes.
- Update `CandidateConfiguration.cs`, `CandidateCiphertext.cs` (cipher column set),
  `CandidateRepository.cs` and the `ICandidateRepository` summary shape.

**Access migration tool** (`backend/Tools/DataMigration/Loading/MigrationLoader.cs`): compose
`Notes` as described, and record every candidate as `unknown`. `RowValidator` keeps validating
`Status`. `Tools/TestDataGenerator` is unchanged, because its export columns stay.

### Frontend changes (paths under `frontend/src/app/`)

- **Models and services:**
  - `features/candidates/models/candidate.models.ts`: remove `CandidateStatus`, `status` and
    `availability`; add `CandidateAvailability` (`state`, `checkedOn`, `until`,
    `checkedByDisplayName`); the sort field `'status'` becomes `'availabilityCheckedOn'`; the list
    query `status` becomes `availability`; drop both from `CandidateDraft` and
    `EMPTY_CANDIDATE_DRAFT`.
  - `candidate.api.ts` / `candidate.service.ts`: `recordAvailability(id, input)` through
    `api-transport.ts`. The service replaces the aggregate (and version) with the response. List
    queries send `availabilityValues`.
- **Candidate page:**
  - New `features/candidates/components/candidate-availability.tsx` with `.logic.ts` and `.css`.
    The `.logic.ts` holds the pure helpers: whether the check has lapsed, whether «Sigue igual» is
    offered, the undo payload, and the form-to-request mapping. Validation lives in the service and
    throws `TranslatableError`.
  - New `candidate-pipeline-badges.tsx` with `.logic.ts` (badges from links).
  - `candidate-detail-page.tsx`: badges beside the name, availability block under the contact line.
  - `candidate-positions-panel.tsx`: share its loaded links with the badges so a stage change
    updates them at once. The page owns the list or the panel reports it; no second request.
  - `candidate-main-panel.tsx`, `candidate-form.tsx` and `candidate-form.logic.ts`: remove the
    status and availability fields. Keep every other `name=` and `data-testid`.
- **Candidate list:** `candidate-table.tsx` («Disponibilidad» column, sortable), and
  `candidate-filters-bar.tsx`, `candidate-list.logic.ts` and `candidate-list-page.tsx`
  («Disponibilidad» select, URL param `availability`, chip, sort `availabilityCheckedOn`).
- **Search:**
  - `search.models.ts`: `availabilityValues` and `availabilityCheckedFrom`.
  - `search-criteria-form.tsx`, `search-criteria.logic.ts` and `search-basic-filters.logic.ts`: the
    status disclosure becomes an availability disclosure plus a «Comprobado desde» date. It also
    serves the preset editor and position requirements.
  - `search-results.tsx`: the «Disponibilidad» column.
  - `search-presets.service.ts`: normalize the new members.
  - `export.service.ts`: the `disponibilidad` and `comprobado_el` columns.
- **Other:**
  - `features/dashboard/dashboard-page.tsx`: drop `status`.
  - `features/admin/audit/audit.logic.ts`: add `candidate.availability_checked`.
  - `core/i18n/format.ts`: a `formatElapsed(date)` helper on `Intl.RelativeTimeFormat` («hace 7
    meses»). No new dependency.
- **Copy:** `assets/i18n/es.json`:
  - Add keys under `candidate.availability.*`:
    - values: «Sin comprobar», «Disponible», «No disponible»;
    - lines: «No disponible hasta {{date}}», «Comprobado el {{date}} por {{name}} · {{elapsed}}»,
      «(vencido)» and the lapsed hint;
    - actions: «Sigue igual», «Cambiar…», «Registrar comprobación», «Deshacer»;
    - form labels: «Hasta (opcional)», «Comprobado el»;
    - confirmations: «Disponibilidad registrada.», «Comprobación deshecha.»;
    - validation messages.
  - Add `candidate.pipeline.inProcess` «En proceso» and `candidate.pipeline.hired` «Contratado».
  - Add the «Comprobado desde» filter label and its chip.
  - Remove the `search.criteria.status.*`, `candidate.detail.status`,
    `candidate.detail.availability` and `candidate.form.availability` keys and the status chip.
  - Keep `admin.audit.eventType.candidate.status_changed`.
  - Update `admin.import.contract` if it mentions the removed columns.
- «Sigue igual» has the accessible name «Sigue igual: registrar la comprobación con fecha de hoy».
  The block's result line is a polite live region, so a check and its undo are announced.

### Acceptance criteria

1. **No global status.** Given any candidate, when it is read through `GET /api/candidates/{id}`,
   the list or search, then no response contains `status` or the free-text `availability`, and
   `CND_Candidates` has neither column.
2. **Default.** Given a candidate created through the API or the CSV import, when it is read, then
   its availability is `unknown` with no dates and no checker, and its page shows «Sin comprobar».
3. **Record a check.** Given an actor with `candidates.update`, when they choose «No disponible»,
   «hasta» 15/01/2027 and the default date, then the page shows «No disponible hasta 15/01/2027» and
   «Comprobado el {today} por {their name} · hoy». An audit event
   `candidate.availability_checked` exists that contains no value or date.
4. **Sigue igual.** Given a check stored on 12/03/2026 with no «hasta» date, when the user
   activates «Sigue igual», then the value is unchanged, the check date is today, the checker is
   the user, and the candidate's version advances.
5. **Undo.** Given the check in 4, when the user activates «Deshacer», then the stored check again
   has date 12/03/2026 and the previous value. After leaving the page, no «Deshacer» is offered.
6. **Amend.** Given a stored check dated today, when the user records the same value with
   «Comprobado el» 10/03/2026, then it is accepted and stored with that date.
7. **Future date.** Given any check, when its date is more than one day after the current UTC
   date, then it is refused with `candidate.availability.checked_on.invalid` and nothing changes.
8. **Until rules.** A «hasta» date with `available` or `unknown`, or one earlier than the check
   date, is refused with `candidate.availability.until.invalid`.
9. **Lapsed.** Given «No disponible hasta» a past date, when the page is shown, then the line is
   marked «(vencido)», the hint is shown, and «Sigue igual» is not offered.
10. **Reset.** Given a stored check, when the user records «Sin comprobar», then the dates and the
    checker are cleared.
11. **Fail closed.** Unauthenticated callers get 401 and callers without `candidates.update` get
    403 on `PUT /api/candidates/{id}/availability`, before validation: an invalid body still gets
    403, and a missing candidate still gets 403. Readers see the block without actions.
12. **Removed candidate.** A check on a logically removed candidate is refused with
    `candidate.removed`.
13. **Stale version.** A check carrying an old version gets 409 `candidate.concurrency.conflict`.
    After a successful check, editing «Datos principales» on the same page saves without a 409.
14. **Form independence.** Given a stored check, when «Datos principales» is edited and saved,
    then the availability value, dates and checker are unchanged.
15. **Badges.** Given links at `interview` on an open position and `hired` on a closed one, then
    the page shows «En proceso» and «Contratado». Changing the first link to `rejected` in
    «Posiciones» removes «En proceso» without a reload. An actor without `positions.read` sees no
    badges, and no positions request is sent.
16. **Tables.** The candidate list, search results and «Candidatos que encajan» show a
    «Disponibilidad» column with the value and the time elapsed, or «Sin comprobar». No table has
    an «Estado» column for the candidate.
17. **Sort.** Sorting by «Disponibilidad» descending lists the most recent check first, with
    `unknown` candidates last. Ascending lists the oldest check first, with `unknown` still last.
    Pages do not overlap.
18. **Filter.** Given candidates checked `available` on 01/09/2026 and 01/03/2026 and one
    `unknown`, then:
    - `availabilityValues=[available]` with `availabilityCheckedFrom=2026-08-01` returns only the
      first;
    - `availabilityCheckedFrom` alone never returns the `unknown` candidate;
    - an empty or complete value selection restricts nothing;
    - an unknown value is refused with `search.availability.invalid`.
19. **Stored filters.** After the migration, every preset and position requirement has `version`
    2 and no `statusValues`, and loads and applies without error. A document with `version` 1 is
    refused.
20. **Import.** A CSV with a `status` or `availability` column is refused with
    `import.column.unknown`. A valid CSV creates `unknown` candidates.
21. **Access load.** Given a legacy row with status `hired` and availability «Incorporación en
    enero», when it is loaded, then the candidate is `unknown`, and its notes end with «Estado en
    Access: Contratado.» and «Disponibilidad en Access: Incorporación en enero». A second run
    produces the same notes.
22. **Export.** The CSV export has `disponibilidad` and `comprobado_el` and no `estado`.

### Verification

- **Backend (xUnit):**
  - Domain tests for `RecordAvailability` invariants.
  - Handler unit tests for the validator, the removed-candidate guard and the audit event type.
  - `CandidateApiTests`: criteria 1–14, including 401/403 before validation and the checker's
    display name.
  - `CandidateSchemaTests`: the check constraints refuse rows the application would never write.
  - `SearchApiTests`, `SearchFilterNormalizationTests` and `SearchHandlerTests`: criteria 17–19.
  - `SearchQueryPlanTests`: the new index serves the sort and the date filter.
  - Update `SearchParityFixture`, `EncryptedSearchParityTests` and `EncryptedSearchPerformanceTests`.
  - `ImportApiTests` and `ImportRowSemanticsTests`: criterion 20.
  - DataMigration tests: criterion 21.
  - `AuditApiTests`: the new type is listed and filterable.
  - `PositionApiTests` and `PositionHandlerTests`: version 2 requirements.
  - `ProjectDependencyTests` keeps passing.
- **Frontend unit (Vitest + Testing Library):**
  - `candidate-availability.spec.tsx`: states, «Sigue igual», «Deshacer», lapsed, reset, read-only,
    focus and live region.
  - Logic specs for the availability and badge helpers.
  - Update `candidate-detail-page`, `candidate-form`, `candidate-list-page`, `search-results`,
    `search-criteria-form`, the preset and export specs, and `format` (`formatElapsed`).
- **E2e (Playwright):**
  - New `candidate-availability.spec.ts`: record, «Sigue igual», «Deshacer», amend, lapsed (seeded
    through the API with a past date), the list column, sort and filter.
  - Update `candidate-profile`, `candidate-list-paging`, `advanced-search`, `audit-trail`,
    `candidate-documents`, `candidate-cv-row-preview`, `navigation-responsive`, `security-ops`,
    `secure-access` and `support/seed-candidate.ts` where they set or read the status.
  - Records carry a `Date.now()` marker. Selectors use roles, labels and test ids.
- **Security:** `tests/security` and `npm run security:rls`, confirming that the runtime grants on
  `CND_Candidates` are unchanged and the new endpoint fails closed.
- **Gates:** `npm test`, `npm run test:backend`, `npm run lint`, `npm run format:check`,
  `npm run build:all`.

### Documentation

**Delta specs:**

- `candidate-management`: replace "Constrained candidate status" with "Availability check".
- `candidate-persistence`: replace the status constraint with the availability constraints, and
  drop "availability, status" from the persisted field list.
- `candidate-search`:
  - the availability family replaces "Candidate status and primary-CV state" (the CV part stays);
  - the projection and the sort set change;
  - the empty-filter wording changes.
- `saved-search-presets`: the editor row presents an availability disclosure and «Comprobado desde»
  instead of candidate status; schema version 2.
- `position-management`: requirements use filter schema version 2; "unsupported candidate status"
  becomes "unsupported availability".
- `position-candidates`: remove the sentences tying stages to the candidate status (lines 35–36 and
  the scenario on line 41).
- `candidate-profile-pages`: the availability block and the derived badges; «Datos principales»
  loses status and availability.
- `candidate-import`: the column list.
- `legacy-data-migration`: legacy status and availability load as notes.
- `personal-data-encryption`: remove "availability" from the encrypted list.
- `audit-trail`: the new event type, and `candidate.status_changed` retained for reading only.

**Docs:**

- `docs/ktl-17/import-file-contract.md`, `row-reason-codes.md` (drop `status.unknown`) and the
  example.
- `docs/ktl-7/access-export-procedure.md`: what the loader now does with `Status`/`Availability`.
- `docs/ktl-7/database-notes.md`: the constraints.
- `docs/ktl-8/candidates.md`: the endpoint and audit events.
- `docs/ktl-19/audit-contract.md`.
- `docs/ktl-10/search.md`, `docs/ktl-18/list-contract.md` and `docs/ktl-10/query-plans.md`.
- `docs/ktl-36/` release notes, which state that saved presets and position requirements lose any
  status selection.
- `README.md` only if it mentions candidate states.

### Non-functional requirements

- **Security and personal data:**
  - The endpoint fails closed before validation.
  - The checker is exposed only as a display name to readers who can already read the candidate.
  - Audit events carry no value or date.
  - Nothing new is logged; `PersonalDataRedactionEnricher` is unaffected.
  - No new table, grant or runtime dependency.
- **Performance:** the availability filter and sort run in SQL on clear columns, served by the
  new index. The search p95 budget (≤ 300 ms, KTL-33) still holds, with evidence in
  `docs/ktl-33/performance.md` if the plan changes. The badges reuse the panel's request.
- **Accessibility:**
  - The availability form is a labelled radio group with labelled date inputs.
  - Undo is not on a timer.
  - Results are announced through a polite live region, and focus returns to «Cambiar…» after
    saving or cancelling.
  - Badges are text, not colour alone.
- **Responsive:** the block wraps under the name on narrow screens with the single 768px
  breakpoint unchanged. Its buttons stay reachable at 390px.

### Out of scope

- Candidate labels with dated assignments. Candidate **tags already exist** (catalog family `Tag`,
  `CND_CandidateTags`, ANY/ALL search), so the follow-up ticket adds "added on / by" to those tags
  rather than building a new catalog.
- Availability history, bulk reconfirmation, and reconfirming from table rows.
- A configurable staleness threshold or reminders.
- Showing the derived badges in tables or filtering by them.
- Any change to the position stage vocabulary.
