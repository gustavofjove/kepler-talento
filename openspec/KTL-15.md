## [original]

Positions

We are introducing the "Posiciones" (Positions) section into the application as a new main section between "Búsqueda" and "Admin". Similarly to "Candidatos" there will be CRUD operations and a page with the list of positions.

The position details page will have some basic fields: Title (textbox), Description (rich text box), Location (textbox), Status (dropdown menu with Open/Closed for the moment). Apart from that, we will reuse the Search/Preset form (reusing the same component) so that we could edit the parameters as requirements, with the ability of loading a preset or saving them as a preset. This same page will have at the bottom a list of candidates that meet the criteria.

--

## [enhanced]

# KTL-15 — Position management and dynamic candidate matching

## User story

As an **HR user**, I want to create and maintain open or closed positions with reusable candidate-search requirements, so that I can see the current candidates who meet each position's needs without repeating the same search manually.

## Outcome

Add **Posiciones** as a permission-controlled top-level section between **Búsqueda** and **Admin**. The section provides a paginated position list plus create, detail and edit journeys. A position stores a copy of the normalized candidate-search filters as its requirements. Its detail page evaluates those requirements through the existing candidate-search API and shows live, paginated matches to authorized users.

Closing a position is the retirement operation. There is no physical deletion, no `DELETE` endpoint, no candidate assignment model and no frozen candidate snapshot in this ticket.

## Confirmed product decisions

- A position has a required unique title, optional rich-text description, optional location, and status `open` or `closed`.
- New positions start as `open`. Closed positions remain available, editable and reopenable; closing does not freeze their requirements or candidate matches.
- Titles are unique across open and closed positions under case- and accent-insensitive comparison.
- Loading a preset copies its normalized filters into the position. No persistent link to the preset is retained, so later changes in either resource do not affect the other.
- Saving position requirements as a preset creates a new entry in the existing shared preset library. It does not link or update the position automatically.
- Candidate matches are evaluated dynamically through `POST /api/candidates/search`; they are not stored against the position.
- Rich text supports paragraphs, line breaks, bold, italic, ordered lists and unordered lists. Links, images, embedded content, scripts, event attributes, arbitrary styles and all other HTML are rejected or removed by server-side sanitization.
- The list shows title, location, status, last update and available actions. It can be filtered by text and status and is paginated. It shows open positions by default, with explicit options for closed or all positions.

## Functional requirements

### 1. Navigation and routing

- Add **Posiciones** to `TOP_LEVEL_ITEMS` in `src/app/core/layout/nav-items.ts`, after **Búsqueda** and before the **Admin** disclosure.
- The navigation item is visible only with `positions.read`; hiding it is a convenience, while the SPA route guards and API remain the security boundaries.
- Add these routes in `src/app/app.tsx`:
  - `/app/positions` — list, guarded by `positions.read`;
  - `/app/positions/new` — create form, guarded by `positions.manage`;
  - `/app/positions/:id` — detail and live matches, guarded by `positions.read`;
  - `/app/positions/:id/edit` — edit form, guarded by `positions.manage`.
- The existing single responsive navigation DOM and 768 px CSS breakpoint remain unchanged in principle. The new item must work with keyboard navigation and at desktop and mobile widths.

### 2. Position list

- Load a bounded server-side page rather than all positions into the browser.
- Default to `status=open`, page 1, page size 25 and `updatedAt desc`; support page sizes from 1 through 100.
- Allow text filtering across title and location using trimmed, case- and accent-insensitive matching. Blank text is ignored.
- Allow exactly `open`, `closed` or `all` as the status filter. Reject unsupported values rather than broadening the query.
- Support only a closed set of sort fields: `title`, `location`, `status`, and `updatedAt`, in `asc` or `desc` direction. Always append position id ascending as the deterministic tie-breaker.
- Return a minimal list projection: `id`, `title`, `location`, `status`, `updatedAt`, and `version`. Do not return description HTML or requirement filters in list responses.
- Show localized loading, empty, error and pagination states. Rows link to details; create and edit actions appear only with `positions.manage`.

### 3. Create and edit

- Use controlled React form fields with stable `name=` and `data-testid` attributes.
- Title is trimmed, required, at most 200 characters, and globally unique under the same normalization already used for accent-insensitive preset names.
- Location is trimmed, optional, and at most 200 characters.
- Description is optional and accepts at most 20,000 input characters. The API sanitizes it to the confirmed allowlist before persistence and returns only canonical sanitized HTML. An implementation dependency used for editing or sanitization must be justified in the OpenSpec design.
- Status is server-assigned as `open` on create. Edit permits transitions from `open` to `closed` and from `closed` to `open`.
- Requirements use the complete existing `SearchFilters`/`SearchFiltersInput` contract and the shared `SearchCriteriaForm`. Empty filters remain valid and mean that every active candidate is eligible under the existing search semantics.
- Create and update re-normalize and validate the complete filter value on the server. Unknown status values, modes, malformed criteria and unsupported stored schema versions fail closed with stable validation problems.
- Updates send the last-read `version`. A stale write returns `409` and leaves the newer position unchanged.
- A title conflict returns a distinct stable `409` problem and changes nothing.
- Successful create redirects to the new detail page. Successful update returns to that position's detail. Cancel returns without persisting changes.

### 4. Preset reuse

- A user with `candidates.read` can list the existing shared preset library and load one through `POST /api/search-presets/{id}/use`. Applying it copies the returned normalized filters into the position draft and advances the preset's existing `lastUsedAt` value.
- If a selected preset no longer exists or cannot be parsed, the current position draft remains unchanged and a localized error is shown.
- A user who also has `presets.manage` can save the current requirement filters as a new shared preset by supplying a name. Use the existing `POST /api/search-presets` contract and its uniqueness, privacy and validation rules.
- Users without `presets.manage` do not see the save-as-preset action. Users without `candidates.read` cannot list or apply presets, in line with the existing preset contract.
- Loading or saving a preset never records its id on `OPS_Positions` and never causes later synchronization.

### 5. Detail page and candidate matches

- Display title, sanitized rich-text description, location, translated status (`Abierta`/`Cerrada`), timestamps and a read-only `SearchCriteriaSummary`.
- Render only the confirmed rich-text allowlist. The client must not attempt to make unsafe HTML safe; server-side sanitization is authoritative.
- When the actor also has `candidates.read`, run the stored requirements through the existing `CandidateSearchService` and `POST /api/candidates/search`, retaining its debounce/cancellation, default page size 25, deterministic sorting and no-duplicate guarantees.
- Reuse `SearchResults` for the minimal candidate projection and permission-aware CV actions. Links to candidate details remain protected by `candidates.read`; opening a CV still requires `documents.download`.
- A user with `positions.read` but without `candidates.read` may read the position but receives no candidate data, count or search request. Show a localized explanation in place of results.
- Matches remain live for closed positions. Candidate updates, logical removal, tag changes or search-catalog changes can therefore alter later results; no historical membership is implied.
- Candidate names, contacts, filters and match counts are personal or potentially identifying data. Do not place them in URLs, logs, audit payloads or position-list responses.

### 6. Close and reopen semantics

- Closing and reopening are ordinary optimistic-concurrency updates protected by `positions.manage`.
- There is no HTTP `DELETE`, no repository delete operation and no `DELETE` grant on the position table.
- A closed position remains addressable by id and appears when the list filter includes closed positions.
- Closing or reopening a position does not alter candidates, presets or the stored requirements.

## Authorization and seeded roles

Add the following strings to the single API/SPA permission vocabulary and to role-management validation:

| Permission         | Grants                                                                                                               |
| ------------------ | -------------------------------------------------------------------------------------------------------------------- |
| `positions.read`   | List and read positions. It does not grant access to matching candidates.                                            |
| `positions.manage` | Create, edit, close and reopen positions. It does not imply `positions.read`, `candidates.read` or `presets.manage`. |

Every position endpoint and every MediatR handler checks `ICurrentActor` before validation or lookup. An unauthenticated or unauthorized caller receives the same forbidden response for valid, invalid, existing and missing resources.

Seed `positions.read` on `rrhh_admin`, `rrhh_user`, `manager_reader`, `readonly` and `system_admin`. Seed `positions.manage` on `rrhh_admin` and `rrhh_user`, matching the roles that already maintain candidates. Custom roles can be configured independently. Candidate matching additionally requires the existing `candidates.read`; saving a preset additionally requires `presets.manage`.

## API contract

No position endpoint exists today. Add `backend/Web/Features/Positions/PositionEndpoints.cs`, register `MapPositionEndpoints()` in `backend/Web/Program.cs`, and expose only:

| Method and route          | Permission         | Purpose                                                                             |
| ------------------------- | ------------------ | ----------------------------------------------------------------------------------- |
| `GET /api/positions`      | `positions.read`   | Paged list with `text`, `status`, `page`, `pageSize`, `sortField`, `sortDirection`. |
| `GET /api/positions/{id}` | `positions.read`   | Full position including sanitized description and normalized requirements.          |
| `POST /api/positions`     | `positions.manage` | Create an open position.                                                            |
| `PUT /api/positions/{id}` | `positions.manage` | Replace editable fields, requirements and status using `version`.                   |

Responses use the existing Problem Details pipeline and stable codes for invalid input, title conflict, not found and concurrency conflict. New ids use `Guid.CreateVersion7()` and timestamps are UTC `DateTimeOffset` values.

Reuse, without duplicating, these existing contracts:

- `POST /api/candidates/search` for live matches;
- `GET /api/search-presets` and `POST /api/search-presets/{id}/use` to select and copy a preset;
- `POST /api/search-presets` to save the current requirements as a new shared preset.

Suggested full response shape:

```json
{
  "id": "uuid",
  "title": "Desarrollador/a .NET",
  "descriptionHtml": "<p>Experiencia en <strong>ASP.NET Core</strong>.</p>",
  "location": "Madrid",
  "status": "open",
  "requirements": { "text": "", "statusValues": [], "skillCriteria": [] },
  "createdAt": "2026-09-22T10:00:00Z",
  "updatedAt": "2026-09-22T10:00:00Z",
  "version": 1
}
```

The actual `requirements` object is the complete existing search-filter contract, not the abbreviated example above.

## Persistence and backend design

- Add `backend/Domain/Positions/Position.cs` with the invariants and `PositionStatus` closed vocabulary.
- Add one use-case file per operation under `backend/Application/Features/Positions/`: `ListPositions.cs`, `GetPosition.cs`, `CreatePosition.cs`, `UpdatePosition.cs`, plus `PositionContract.cs` and `PositionGuards.cs` where shared behavior is justified.
- Add `IPositionRepository` under `backend/Application/Abstractions/Persistence/` and its EF Core implementation/configuration under `backend/Infrastructure/Persistence/`.
- Extend `ApplicationDbContext` with positions and add an EF Core migration under `backend/Infrastructure/Persistence/Migrations/`; do not hand-write a standalone schema migration.
- Store positions in the quoted table `"OPS_Positions"`. This extends the documented `OPS_` registry from durable import/export operations to recruitment workflow state and must be recorded in `docs/ktl-5/database-conventions.md`; it does not introduce a new prefix.
- Required columns are `Id`, `Title`, `NormalizedTitle`, `DescriptionHtml`, `Location`, `Status`, `Requirements` (`jsonb`), `FilterSchemaVersion`, `CreatedAtUtc`, `UpdatedAtUtc`, and the PostgreSQL `xmin` concurrency token.
- Reuse the normalization, validation and versioned serialization semantics of `SearchFilterDocument` rather than defining a second filter dialect. A stored document that cannot be understood is refused; it is never replaced with empty criteria, which would broaden matching.
- Add database constraints for non-blank/max-length title, max lengths, allowed status, JSON object shape, supported filter schema version and timestamp ordering.
- Add a unique index on `NormalizedTitle`, plus indexes supporting the default `Status + UpdatedAtUtc + Id` listing and normalized title/location text filtering. Verify representative list query plans.
- The migration grants `SELECT`, `INSERT` and `UPDATE` on `"OPS_Positions"` to `ktl_runtime`, and explicitly does not grant `DELETE`. It updates seeded role permission JSON idempotently and preserves custom roles.
- Position data is API-owned PostgreSQL data. Do not add Supabase or `localStorage` persistence. The existing last-used search preference remains the only local search convenience.

## Frontend impact

Create `src/app/features/positions/` with:

- `models/position.models.ts`;
- `services/position.service.ts` using `ApiTransport`;
- `use-positions.ts` for any service state read during render;
- `pages/position-list-page.tsx` and co-located CSS/logic;
- `pages/position-detail-page.tsx`;
- `pages/position-edit-page.tsx`;
- focused components for the position form and sanitized rich-text editor/viewer where useful.

Wire the singleton service through `src/app/core/di/services.ts` and `ServicesProvider`. Reuse the search-owned `SearchCriteriaForm`, `SearchCriteriaSummary`, `SearchCriteriaDialog` where appropriate, and `SearchResults`; do not copy their filter logic into the positions feature. Permission checks use `usePermission()` at component top level. Errors use `useErrorToast()` or `errorText()` with `TranslatableError` keys.

Add all new and changed UI copy to `src/assets/i18n/es.json` under flat `positions.*` and `nav.positions` keys. Required visible wording includes **Posiciones**, **Nueva posición**, **Abierta**, **Cerrada**, **Cargar preset**, **Guardar como preset**, **Candidatos coincidentes**, conflict/error messages and accessible labels. Do not add files to `LEGACY_HARDCODED_COPY`. Format timestamps with the shared locale helpers.

## Acceptance criteria

1. **Authorized listing:** Given an actor with `positions.read`, when they open **Posiciones**, then they see the first page of open positions ordered by latest update, with the confirmed columns and no description or requirement payload in the list response.
2. **Navigation order and responsive behavior:** Given the desktop or mobile shell, when navigation is rendered, then **Posiciones** appears after **Búsqueda** and before **Admin**, remains keyboard accessible and uses the existing responsive navigation behavior.
3. **Fail-closed read:** Given an unauthenticated actor or one without `positions.read`, when they call either read endpoint with any input, then the API returns forbidden before validation or lookup and discloses no position data or existence.
4. **Create:** Given an actor with `positions.manage`, when they submit a unique title and valid requirements, then an open position is persisted with a v7 UUID, normalized filters, timestamps and version, and the UI opens its detail page.
5. **Unique title:** Given an existing title `Programador sénior`, when another position is created or renamed to `programador SENIOR`, then the API returns the title-conflict problem and neither position changes.
6. **Safe rich text:** Given description input containing allowed formatting plus scripts, event handlers, links, images or styles, when it is submitted, then only canonical allowlisted markup is stored and returned, no active content executes, and the unsafe content never reaches logs.
7. **Optimistic concurrency:** Given two editors loaded the same version, when both save, then the first succeeds and the second receives the concurrency-conflict problem without overwriting the first.
8. **Close and reopen:** Given an open position, when a manager closes it, then it disappears from the default open list, remains available under closed/all filters, can be reopened, and no row is deleted.
9. **Preset copy:** Given a permitted user loads a preset into a draft, when the preset later changes, then the position draft and any saved position retain the copied filters unchanged.
10. **Preset permissions:** Given a user without `presets.manage`, when they edit a position, then they cannot invoke save-as-preset; direct calls to the preset write endpoint remain forbidden. Loading still follows the existing `candidates.read` guard.
11. **Dynamic matching:** Given a saved position and a candidate that newly satisfies its requirements, when an authorized user next opens or refreshes the detail, then the candidate appears through the existing paged search without a position-candidate association being created.
12. **Candidate privacy boundary:** Given an actor with `positions.read` but without `candidates.read`, when they open a position, then position data is shown but no candidate search is sent and no candidate item, count or match-derived fact is disclosed.
13. **Search semantics:** Given multiple requirement families and ANY/ALL criteria, when matches are loaded, then the existing AND-between-families, ANY/ALL-within-family and no-duplicate rules are preserved.
14. **Invalid stored requirements:** Given a position row whose filter document has an unsupported schema version or malformed content, when it is read or evaluated, then the operation fails with a stable problem and never broadens to an empty search.
15. **Least privilege:** Given the migrated database role `ktl_runtime`, when privileges are inspected, then it can select, insert and update `"OPS_Positions"` but cannot delete it or perform DDL.
16. **List performance:** Given representative position volume, when a default or filtered page is requested, then PostgreSQL uses the intended indexes, executes bounded paging, and does not scan or deserialize requirement JSON for the list projection.
17. **Accessibility and localization:** Given keyboard-only use and a narrow viewport, when the list, form, preset controls and matches are used, then labels, focus order, validation feedback and controls are operable, copy is Spanish through `t()`, and no horizontal page overflow is introduced.

## Test coverage and evidence

- **Backend unit (`backend/Tests/UnitTests`)**: domain normalization/invariants, title and rich-text validation/sanitization, filter serialization, guards for every handler, status transitions, version forwarding, and log-redaction sentinel tests.
- **Backend integration (`backend/Tests/IntegrationTests`)**: all four position endpoints, paging/filtering/sorting, duplicate title race, optimistic concurrency, malformed stored filters, sanitized persistence, seeded permissions, PostgreSQL constraints/indexes, and query plans.
- **Security (`tests/security`)**: unauthenticated and unauthorized matrices before validation/lookup; `positions.read` without `candidates.read`; `positions.manage` without unrelated permissions; runtime grants proving no `DELETE`; responses/logs containing no unsafe HTML, requirement payloads on lists or candidate data across the boundary.
- **Frontend unit (`tests/unit`)**: service request contracts, signal subscription hook, list states and pagination, controlled create/edit form, close/reopen, preset-copy independence, permission-gated actions, conflict rendering, sanitized viewer, and route/nav guards.
- **Frontend integration**: position service through `ApiTransport`, live candidate search cancellation and preset application without local authoritative storage.
- **Playwright (`tests/e2e/positions.spec.ts` and navigation/accessibility coverage)**: create, edit, close, reopen, filter list, load/save preset, see changing candidate matches, denied candidate matches, responsive navigation and keyboard journey. Select by role, accessible name or `data-testid`, not hardcoded Spanish text.
- Run and inspect the affected targeted suites, then the required gates: `npm test`, `npm run test:backend`, targeted `npm run e2e`, `npm run security:rls`, `npm run security:storage`, `npm run lint`, `npm run format:check`, and `npm run build:all`.

## Documentation and specification impact

- Create the OpenSpec capability `position-management` and update `primary-navigation`, `identity-and-access-control`, `postgresql-persistence` and any shared search/preset specs whose observable contract changes.
- Add `docs/ktl-15/positions.md` for the API, status lifecycle, requirement-copy semantics, rich-text allowlist and permission matrix, plus `docs/ktl-15/release-notes.md`.
- Update `docs/ktl-5/database-conventions.md` to record `OPS_` ownership of position workflow state and the position table/grants.
- Update the Spanish `README.md` only if its user-visible feature or permission overview enumerates application sections.

## Out of scope

- Candidate-to-position assignments, stages, interview workflows, offers, vacancies/headcount and notifications.
- Historical match snapshots or an audit-quality record of who matched at a past instant.
- Position deletion, bulk import/export or duplicate/clone operations.
- New search criteria or changes to candidate-search result fields and semantics.
- Linking a position to a preset after filters are copied.
- Additional position statuses beyond `open` and `closed`.
