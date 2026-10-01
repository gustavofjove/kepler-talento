## Context

See `proposal.md` for motivation and the delta specs for normative behavior. The source brief is
`openspec/KTL-36.md`.

The pieces this change builds on:

- **Candidate aggregate.** `Domain/Candidates/Candidate.cs` holds `Status` (plain text, guarded by
  `CandidateStatuses` and `CK_CND_Candidates_Status`) and `Availability` (free text, encrypted
  since KTL-33). Status changes go through `ChangeStatus` inside `UpdateCandidateHandler`, which
  picks `candidate.status_changed` or `candidate.updated` as its single audit event. The
  candidate's `Version` (`xmin`) protects the whole aggregate. Relation writes use
  `PUT /api/candidates/{id}/<collection>` with that version and return the full `CandidateResponse`.
- **Notes.** `CandidateNote.AuthorUserId` is a nullable FK to `ADM_Users`. `ICurrentActor.UserId`
  is null for the development actor or an unprovisioned subject. The note response exposes
  `AuthorDisplayName` through an `Include`.
- **Search.** `SearchFiltersInput/Value` carry `StatusValues`. `CandidateSearchQuery` applies status
  in SQL and sorts by it in SQL and in the in-memory encrypted stage (text search or last-name
  sort). `SearchFilterDocument` stores filters as camel-case JSON with `version` 1, and refuses any
  other version. Presets (`ADM_SearchPresets.Filters`) and positions (`OPS_Positions.Requirements`,
  `CK_OPS_Positions_RequirementsVersion`) both store this document. Only its `text` member is
  ciphertext (`HasEncryptedFilterText` annotates `<column>.text`).
- **Import and migration.** The KTL-17 import evaluates `status` and `availability` columns and
  refuses unknown columns structurally (`import.column.unknown`). `ktl-migrate` validates the
  legacy `Status` code, writes it and `Availability` through `Candidate.SetDetails`, and recomposes
  every field from the row on each run.
- **Candidate page.** It is a single page of panels with one-at-a-time edit mode (KTL-29).
  `CandidatePositionsPanel` loads `GET /api/candidates/{id}/positions` into local state and acts
  immediately, outside the coordinator (KTL-30). Toasts (`ToastService`) are text-only and dismiss
  themselves after 4.2 s.
- **Branch state.** KTL-35 work is still uncommitted on `feat/KTL-35`, and it touches the same
  search projection and tables. This change starts from `main` once KTL-35 is merged.

## Goals / Non-Goals

**Goals:**

- One availability fact per candidate, with a single write path, so that freshness cannot be faked
  by an unrelated edit.
- Remove every trace of the global status and free-text availability, with no compatibility shims
  in the API or SPA.
- Keep search fully in SQL for the new family and sort, inside the KTL-33 budget.
- Keep runtime grants, tables and dependencies unchanged.

**Non-Goals:**

- Availability history, reminders, bulk or row-level reconfirmation.
- Persisting the pipeline badges, or making them searchable.
- Dated tag assignments (the follow-up "labels" ticket).
- A reader upgrade path for version 1 filter documents (see D6).

## Decisions

### D1. Availability lives on `CND_Candidates`, under the candidate version

Four columns are added to the candidate row:

- `AvailabilityState` (`text`, default `'unknown'`);
- `AvailabilityCheckedOn` (`date`);
- `AvailabilityUntil` (`date`);
- `AvailabilityCheckedByUserId` (`uuid`, FK `ADM_Users` `RESTRICT`, with a `CheckedBy` navigation
  like `CandidateNote.Author`).

Constraint names follow the brief:

- `CK_CND_Candidates_AvailabilityState`;
- `CK_CND_Candidates_AvailabilityCheck`: `unknown` ⇔ no check date, and `unknown` ⇒ no checker;
- `CK_CND_Candidates_AvailabilityUntil`.

_Why:_ only the latest check is kept, so it is a property of the candidate, not a child. Keeping it
under the candidate `Version` reuses the existing 409 path, and with 2–3 users a collision between
a check and a form edit is rare (the SPA adopts the new version after each check, D7). There is no
new table, so `ktl_runtime` needs no new grant.

_Alternatives:_

- A `CND_CandidateAvailability` table with its own version would avoid collisions with form edits,
  but adds a table, grants, a join on every search, and an aggregate boundary that buys nothing at
  this scale.
- A history table was rejected by product decision ("latest only"). The audit trail records that a
  check happened.

The checker is **nullable even for a known check**, because `ICurrentActor.UserId` is null for the
development actor. The SPA then omits «por {name}». The constraint only forbids a checker on
`unknown`.

### D2. One write, `PUT /api/candidates/{id}/availability`; reconfirm and undo are client compositions

`RecordCandidateAvailabilityCommand(Id, State, CheckedOn, Until, Version)` →
`Candidate.RecordAvailability(state, checkedOn, until, checkedBy, now)` → save with
`candidate.availability_checked` → `CandidateResponse`. The SPA builds:

- «Sigue igual» as the stored state and until date with today's date;
- «Deshacer» as the previous state, until date and check date.

_Why:_ one rule ("every write is a check, stamped with the caller") is easy to test and impossible
to bypass. A dedicated `POST …/confirm` would duplicate the invariants and still not cover undo.

_Undo trade-off:_ undo restores the date and value but records the undoer as checker. Storing the
previous check server-side to restore it exactly would bring history back in, so it was rejected.

`unknown` is the same operation with `CheckedOn` and `Until` absent. It clears the checker too.

### D3. Date rules are enforced three times, for three different reasons

1. **The FluentValidation validator** gives stable codes (`candidate.availability.invalid`,
   `.checked_on.invalid`, `.until.invalid`) before any lookup work.
2. **`Candidate.RecordAvailability`** throws on an invariant breach, so a future writer such as
   `ktl-migrate` cannot store an inconsistent check.
3. **The check constraints** guard against any writer that skips the domain.

The "not in the future" rule compares with `DateOnly.FromDateTime(UtcNow).AddDays(1)`. That covers
users ahead of UTC (Spain is UTC+1/+2) without a time-zone setting. An earlier-than-stored date is
deliberately allowed (amend).

### D4. Authorization before validation, as every candidate route

The endpoint calls `Require(actor, Permissions.CandidatesUpdate)` before `sender.Send`, and the
handler repeats `CandidateGuards.RequireUpdate`. The order inside the handler is guard → find →
removed check → `ExpectVersion` → record → save, matching `AddCandidateNote`.

### D5. Search: an availability family and a nullable-date sort, both in SQL

- **Filter contract.** `SearchFiltersInput.StatusValues` becomes `AvailabilityValues` and
  `AvailabilityCheckedFrom` (wire: `availabilityValues`, `availabilityCheckedFrom`).
  - Normalization, like statuses: an empty or complete selection is unrestricted and is stored as
    the complete set.
  - A blank date is unset. A non-date is refused with `search.availability.checked_from.invalid`.
- **Predicates.**
  - `AvailabilityValues` → `WHERE "AvailabilityState" = ANY(@values)`, skipped when unrestricted.
  - `AvailabilityCheckedFrom` → `WHERE "AvailabilityCheckedOn" >= @from`, which excludes nulls by
    SQL semantics.
- **Sort.** `SearchSortField.Status` becomes `AvailabilityCheckedOn`, and the wire value `status`
  is no longer accepted. In SQL it is
  `ORDER BY ("AvailabilityCheckedOn" IS NULL), "AvailabilityCheckedOn" {dir}, "Id"`, so nulls come
  last in both directions. The encrypted stage's `StoredRow` carries `AvailabilityCheckedOn`
  instead of `Status`, and its comparer applies the same nulls-last rule.
- **Index.** A partial index `IX_CND_Candidates_IsActive_AvailabilityCheckedOn` on
  `("AvailabilityCheckedOn" DESC, "Id")` where `"IsActive"` serves the date filter.
  PostgreSQL does not use it to satisfy the complete sort because the explicit `IS NULL` term
  keeps unchecked candidates last in both directions. `SearchQueryPlanTests` asserts index use
  for the checked-from case ordered by date and the latency budget for both sort directions;
  `docs/ktl-10/query-plans.md` records the plans.
- **Projection.** `CandidateSearchItem.Status` becomes `AvailabilityState` and
  `AvailabilityCheckedOn`. The until date and the checker stay out, for minimisation: tables show
  only the value and the age.

_Alternative:_ sorting by value then date. Rejected by product: the useful question is "who did we
confirm recently", and filtering by value is already available.

### D6. Filter schema version 2, rewritten in the migration, not upgraded on read

`FilterSchemaVersion` becomes 2. The EF migration `ReplaceCandidateStatusWithAvailability` runs,
as `ktl_migrator`:

```sql
UPDATE "ADM_SearchPresets"
   SET "Filters" = ("Filters" - 'statusValues')
                   || jsonb_build_object('version', 2,
                                         'availabilityValues', '["unknown","available","unavailable"]'::jsonb,
                                         'availabilityCheckedFrom', ''),
       "FilterSchemaVersion" = 2;
-- the same for "OPS_Positions"."Requirements" / "FilterSchemaVersion"
```

`text` stays untouched, so no ciphertext is decrypted or re-encrypted. `CK_OPS_Positions_RequirementsVersion`
keeps holding, because `version` and the column change together in one statement.
`SearchFilterDocument.Parse` keeps refusing any version other than the current one.

_Why not upgrade on read:_ a reader shim would live forever for data that is all test data, and it
would make "what does a v1 document mean" a runtime question. A one-time rewrite keeps the reader
strict.

_Trade-off:_ a restricted status selection broadens to "any availability". Release notes say so.

The migration's `Down` restores `version` 1 with a complete `statusValues`. The status column
comes back with `'new'`; the dropped data is not recoverable.

### D7. SPA: the availability block is a page-level, immediate-action component

- `candidate-availability.tsx` renders under the contact line in `candidate-detail-page.tsx`. Like
  `CandidatePositionsPanel`, it does not take part in the KTL-29 coordinator and never reports
  dirtiness.
- It calls `candidateService.recordAvailability(id, input)`. The service sends the aggregate's
  current version and **replaces the cached aggregate with the response**. Every panel reads the
  aggregate through `useCandidate`, so a Datos principales editor opened before the check saves with
  the new version.

  _Risk:_ the open editor's draft does not reset. It keeps its own field values and takes the
  version from the service at save time, which `candidateService.update` already does.

- Undo state (the previous check) is held in component state, so it naturally disappears on
  navigation. It is replaced by the next check.
- **Why inline undo and not a toast action:** toasts here are text-only and auto-dismiss at 4.2 s.
  Adding an action to them would be a WCAG 2.2.1 timing failure, and would change a shared service
  for one caller.
- Pure helpers live in `candidate-availability.logic.ts`:
  - `isLapsed(check, today)`;
  - `canReconfirm(check, today)`;
  - `reconfirmInput(check, today)`;
  - `undoInput(previous)`;
  - `toRequest(form)`.

  The service validates the form the same way as the API, throwing `TranslatableError`.

- `formatElapsed(day, now)` in `core/i18n/format.ts` uses `Intl.RelativeTimeFormat` with
  `numeric: 'auto'` over whole days, then months or years («hoy», «ayer», «hace 3 días», «hace 7
  meses»). Calendar days are compared as dates, never through `Date` parsing of `yyyy-MM-dd`, which
  would shift a day in negative UTC offsets (the KTL-34 `formatDay` lesson).

### D8. Badges share the positions panel's list

The links move from `CandidatePositionsPanel` local state to the page:

- the page loads them once, when `positions.read` is held;
- it passes them to the panel and to `candidate-pipeline-badges.tsx`;
- the panel reports changes through `onLinksChange` (stage change, add, remove).

`pipelineBadges(links)` lives in a `.logic.ts`.

_Alternative:_ the badges fetch on their own. Rejected, because it is a second request and could
drift from the panel after a stage change.

### D9. Import and legacy load

- **KTL-17 import.** `status` and `availability` leave `CandidateImportContract.Columns`, so the
  existing unknown-column rule refuses old files with no new code. `ImportReasonCodes.StatusUnknown`
  is no longer produced, but it stays in `RowCodes` as a retired code: deleting it would make the
  migration's rebuilt `CK_ADM_ImportRowOutcomes_ReasonCode` reject stored outcomes that carry it.
  `docs/ktl-17/row-reason-codes.md` marks it as retired.
- **`ktl-migrate`.** The export contract and `RowValidator` are unchanged. `MigrationLoader` calls
  `SetDetails` without status or availability, and composes notes with a pure helper:

  ```
  LegacyNotes.Compose(notes, status, availability) =
      notes + "\n\nEstado en Access: {label}." (status ≠ new) + "\nDisponibilidad en Access: {text}" (non-blank)
  ```

  Exact separators are fixed in unit tests. Because notes are recomposed from the row on every
  run, re-runs are idempotent, and the existing "re-runs do not overwrite application changes"
  guard still applies. The Spanish labels mirror the documented Access mapping (Nuevo, Disponible,
  En proceso, Contratado, Descartado).

### D10. Audit catalogue

`CandidateAuditEvents.AvailabilityChecked = "candidate.availability_checked"` is added to
`AuditEventTypes.All`. `StatusChanged` stays in the catalogue and in `audit.logic.ts`/`es.json`, so
historical rows remain filterable and labelled. `UpdateCandidateHandler` always records
`candidate.updated`.

## Risks / Trade-offs

- **[Risk] Breaking wire change.** `status` and `availability` disappear from requests and
  responses, and `sort=status` is refused. → SPA and API ship together in one image, and there are
  no external clients. The e2e suite and `seed-candidate.ts` are updated in the same change.
- **[Risk] Old bookmarked list URLs** (`?status=…`, `?sort=status`). → The list parser already
  drops an unknown `status` param silently. An unknown `sort` reaches the API and is refused, and
  the list shows its error state. This is acceptable for an internal tool and documented in the
  release notes.
- **[Risk] The undo checker is the undoer.** → Accepted (D2). Audit still shows who did each
  write.
- **[Risk] A check collides with a concurrent form save by another user** (409). → Rare with 2–3
  users. The existing conflict message asks to reload.
- **[Risk] Dropping `Availability` text loses information.** → Test data only. Legacy values are
  preserved as notes by `ktl-migrate` (D9).
- **[Trade-off] Badges are page-only.** Tables would need a per-row join and a `positions.read`
  mask in search. Deferred by product.
- **[Trade-off] The one-day future tolerance** allows a check dated tomorrow from UTC−n zones.
  Harmless, and simpler than a configured office time zone.

## Migration Plan

1. Generate `ReplaceCandidateStatusWithAvailability` with `dotnet ef migrations add`. In order, it:
   - adds the four columns, the FK, the checks and the index;
   - drops `CK_CND_Candidates_Status`, `Status` and `Availability`;
   - rewrites presets and position requirements to version 2 (D6).
2. Remove `Availability` from the cipher column set. The KTL-33 encryption backfill and startup
   checks must not reference the dropped column; `FieldEncryptionStartupTests` covers that.
3. Deploy as usual: the `migrator` container applies the migration, then the API starts. There is
   no runtime grant change.
4. **Rollback:** `Down` re-adds `Status` (default `'new'`, with its check) and an empty `Availability`,
   drops the availability columns, and restores filter documents to version 1 with a complete
   status selection. Availability data recorded after the rollout is lost on rollback.

## Open Questions

None. Every product decision was settled in `openspec/KTL-36.md`.
