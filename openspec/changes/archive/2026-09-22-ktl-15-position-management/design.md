## Context

See `proposal.md` for motivation and the delta specs for normative behavior. The current system already has the hard parts of candidate matching: a normalized `SearchFiltersInput` contract, a versioned `SearchFilterDocument`, `POST /api/candidates/search`, shared criteria editor/summary components and an API-backed shared preset library. KTL-15 should compose those pieces rather than introduce another search dialect.

There is no position aggregate, position route, table or permission today. The React application uses explicit singleton services and signals, ASP.NET Core features are vertical slices, EF Core owns schema changes, and the runtime role is deliberately less privileged than the migrator. Candidate search results and free-text requirements may contain personal data; no new binary storage or RLS policy is involved.

The required rich-text field introduces two hazards absent from current plain-text forms: maintaining a usable WYSIWYG selection model in React and ensuring hostile pasted markup cannot become stored XSS. The browser is not an authority for sanitization.

## Goals / Non-Goals

**Goals:**

- Add one position aggregate and four small, permission-guarded API use cases without coupling it to candidates or presets by foreign key.
- Keep one search-filter normalization/serialization implementation and one candidate query path.
- Store only canonical sanitized HTML and make the allowlist testable at the application boundary.
- Make position listing bounded, deterministic and supported by measured PostgreSQL indexes.
- Preserve independent permissions while giving seeded HR roles a complete working journey.
- Deliver schema, constraints, indexes, role seeds and runtime grants as one explicit migration.

**Non-Goals:**

- A recruitment pipeline, candidate assignment, match snapshot, vacancy/headcount model or position history table.
- A general-purpose rich-text/document platform, links, media, arbitrary HTML or user-defined styling.
- A position-specific preset library or changes to candidate-search semantics.
- Moving authorization into PostgreSQL RLS; the API/current-actor boundary remains authoritative.
- Auditing candidate searches or list reads; the existing audit contract deliberately excludes them.

## Decisions

### D1. Model a position as an independent aggregate with copied requirements

`Domain/Positions/Position.cs` owns title, normalized title, sanitized description HTML, location, normalized location, status, serialized requirements, filter schema version and timestamps. New ids use `Guid.CreateVersion7()`; EF maps PostgreSQL `xmin` as `Version`.

The aggregate has `Update(...)` as its only mutation. It applies trimmed scalar values and a status from the closed `PositionStatuses` vocabulary; it receives already-sanitized HTML and already-serialized validated requirements from the application handler. Closing and reopening use that same optimistic update, so no second status endpoint or hidden delete path exists.

Requirements are copied values, not a preset id. There is no candidate-position join table and no count column. This makes later preset and candidate changes independent and keeps the live match truthful.

_Alternatives considered:_ a foreign key to `ADM_SearchPresets` would make deleting or editing a shared preset change a position unexpectedly; a candidate-position join would turn a query view into an assignment workflow and immediately require provenance and history semantics outside KTL-15.

### D2. Expose four position endpoints and reuse existing search/preset endpoints

`Web/Features/Positions/PositionEndpoints.cs` maps:

| Route                     | Application slice       | Guard              |
| ------------------------- | ----------------------- | ------------------ |
| `GET /api/positions`      | `ListPositionsQuery`    | `positions.read`   |
| `GET /api/positions/{id}` | `GetPositionQuery`      | `positions.read`   |
| `POST /api/positions`     | `CreatePositionCommand` | `positions.manage` |
| `PUT /api/positions/{id}` | `UpdatePositionCommand` | `positions.manage` |

The endpoint delegate checks `ICurrentActor` before parsing string query parameters or dispatching; each handler repeats `PositionGuards.RequireRead/RequireManage`. Requests keep status/sort values as strings until after the guard so unsupported input becomes the standard stable validation problem rather than binder behavior. Endpoints declare explicit success and Problem Details metadata and are registered from `Program.cs`.

The full response is `PositionResponse`; the list uses a separate `PositionListItem` projection so EF never reads description or JSON requirements. Stable codes use the `position.*` namespace, including `position.not_found`, `position.title.conflict`, `position.concurrency.conflict`, `position.status.invalid`, `position.requirements.invalid` and bounded list errors.

The detail page separately calls the existing candidate and preset endpoints. A server-side `GET /positions/{id}/candidates` aggregator was rejected: it would either duplicate search authorization/query logic or make the position slice depend on an HTTP-shaped version of another application slice.

### D3. Reuse and minimally generalize the search-filter document codec

The position handlers call `SearchFilterNormalization.TryNormalize` and `SearchFilterDocument.Serialize`, exactly as preset writes do. `SearchFilterDocument.Parse` gains a caller-supplied invalid-document error factory (or equivalent result-based overload); its current preset overload remains behavior-compatible. Position mapping supplies `position.requirements.invalid`, while preset mapping keeps `search_preset.filters.invalid`.

The serialized JSON document remains the source for `FilterSchemaVersion`; the separate integer column supports constraints and migration discovery. Reading always parses and validates. It never substitutes `EMPTY_SEARCH_FILTERS` for bad stored data.

_Alternative considered:_ a `PositionRequirement` table per filter family was rejected because no query searches positions by individual requirement, it would duplicate the candidate-search schema, and every new filter family would need coordinated tables and joins.

### D4. Sanitize on the server with a focused port and a production HTML parser

Add `IPositionDescriptionSanitizer` in Application and an Infrastructure implementation backed by the centrally pinned NuGet package `HtmlSanitizer` (`Ganss.Xss`). The dependency is justified because context-aware HTML parsing and XSS handling are security-critical and cannot safely be recreated with regular expressions or ad-hoc DOM string manipulation.

The implementation clears the library defaults and explicitly permits only `p`, `br`, `strong`, `em`, `ul`, `ol` and `li`, with no attributes, CSS, URI schemes or embedded content. Common bold/italic aliases from paste are canonicalized before the final allowlist pass. The handler rejects raw input above 20,000 characters, sanitizes, verifies the canonical output remains bounded, and passes only the result into the aggregate. Empty markup normalizes to the empty string.

The sanitizer lives behind the port so Domain and Application do not reference the parser package. Backend tests use the real implementation for an attack corpus and a small fake for handler orchestration. `DescriptionHtml`, requirements, title and location are included in log-redaction sentinel coverage; handlers and endpoints never log request bodies.

_Alternative considered:_ sanitizing only in React was rejected because callers can bypass the SPA and sanitizers drift. Storing raw plus sanitized HTML was rejected because it retains hostile content without a product need.

### D5. Use Lexical for the bounded rich-text editor; keep the server authoritative

The frontend adds the minimum compatible Lexical packages needed for React rich text and lists, pinned in `package-lock.json`. Lexical is justified over a hand-rolled `contentEditable`: React-controlled selection, paste normalization, keyboard behavior, undo and list semantics are difficult to implement reliably and accessibly, while the product explicitly requires a rich-text box. The editor enables only paragraphs, bold, italic and ordered/unordered lists and emits HTML into the controlled position draft. Links, images, headings, code, tables and arbitrary styles are not registered.

The detail component renders only the API's canonical `descriptionHtml` in a dedicated container. It does not implement a second sanitizer: the API contract guarantees canonical safe HTML, and a second independent allowlist would create disagreement. CSP and React's normal event model remain defense in depth, while malicious-response fixtures verify the contract in backend/security tests.

_Alternatives considered:_ a textarea containing HTML is not a rich editor; `document.execCommand` is deprecated and inconsistent; a full editor suite would add features and dependency surface outside the allowlist.

### D6. Persist positions in `OPS_Positions` with normalized search columns

`OPS_` is extended from durable import/export work to operational recruitment workflow state and the registry is updated. A new prefix would add architectural vocabulary for one table; `CND_` would incorrectly imply that a position is candidate profile data.

The table contains:

- `Id uuid` primary key;
- `Title varchar(200)` and `NormalizedTitle varchar(200)`;
- `DescriptionHtml varchar(20000)`;
- `Location varchar(200)` and `NormalizedLocation varchar(200)`;
- `Status varchar(20)`;
- `Requirements jsonb` and `FilterSchemaVersion integer`;
- `CreatedAtUtc`/`UpdatedAtUtc timestamptz`;
- `xmin` concurrency token.

`PositionText.Normalize` reuses the established `CatalogName.Normalize` folding behavior for title uniqueness and title/location filtering. A unique B-tree index on `NormalizedTitle` is the concurrent authority for duplicates. A composite B-tree index on `(Status, UpdatedAtUtc, Id)` serves the default list, with B-tree indexes ending in `Id` for the remaining contracted sort paths.

Text filtering is contains matching over normalized title and location. The migration enables trusted PostgreSQL `pg_trgm` with `CREATE EXTENSION IF NOT EXISTS` and creates GIN trigram indexes for both normalized columns. The deployment runbook verifies that `ktl_migrator` can install/use the extension before rollout; `ktl_runtime` receives no extension or DDL privilege. The down migration drops KTL-15 indexes/table but deliberately does not drop the shared database extension.

_Alternative considered:_ leading-wildcard scans are acceptable for today's tiny dataset but contradict the contracted representative-volume evidence; prefix-only matching is faster but does not meet normal list-filter expectations.

### D7. Keep the migration additive, repeatable and least-privileged

The EF Core migration creates the extension/table, constraints and indexes; adds the two permission strings to the named system-role JSON arrays using idempotent set semantics; and grants `SELECT`, `INSERT`, `UPDATE` on `"OPS_Positions"` to `ktl_runtime` only when that role exists. It explicitly revokes `DELETE` as defense against inherited or historical grants. Custom roles are not changed.

Role JSON remains a sorted unique array compatible with `Role.ReplacePermissions`. `positions.read` is added to all five system roles; `positions.manage` only to `rrhh_admin` and `rrhh_user`. The application permission catalogues change in the same delivery so stored values never lead the validators deployed with them.

The repository maps PostgreSQL unique violation by the named title index, concurrency failure separately, and other constraint failures to a generic stable problem without exposing database text. After save it reloads `xmin` before returning the response.

### D8. Preserve independent authorization across composed UI capabilities

`positions.read` guards list/detail and navigation. `positions.manage` guards writes and create/edit controls. Neither implies the other in code. Seeded managers have both, but custom roles are allowed to hold either independently.

The SPA subscribes once per permission at component top level using `usePermission`. A manager without read permission can create via the API, but the SPA does not pretend it can load an edit/detail screen; seeded roles provide the intended end-to-end journey. Candidate results are rendered and requested only when `candidates.read` is true. Preset listing/apply requires `candidates.read`; save-as-preset requires `presets.manage`; catalog-backed criteria options continue to require `catalogs.read`; CV actions continue to require `documents.download`.

The client checks are presentation only. Direct API calls still fail closed at their owning endpoints and handlers.

### D9. Centralize position state and reuse search-owned components

`PositionService` uses `ApiTransport` and owns a `WritableSignal<PositionsState>` for list pages. `usePositions()` subscribes to that signal; components never read it through `useServices()` during render. Detail/edit pages may hold request-local draft/loading state while all writes remain methods on the service. The service never stores positions in `localStorage`.

The feature reuses `SearchCriteriaForm`, `SearchCriteriaSummary`, `SearchPresetsService`, `CandidateSearchService` and `SearchResults`. Applying a preset clones the returned filter arrays before placing them in the draft. The saved position response carries only requirements, never a preset id.

Candidate result requests are aborted on position change, page change or unmount. Closed positions follow the same path. A reader without `candidates.read` sees localized explanatory content and no request is issued. The position list uses server paging and resets to page 1 when text, status or sorting changes.

### D10. Route and navigation composition stay data-driven

`TOP_LEVEL_ITEMS` receives a `nav.positions` item after search, `NAV_PERMISSIONS` gains `positions.read`, and `PrimaryNav` hoists one corresponding `usePermission` call. The single DOM tree and CSS breakpoint remain unchanged.

`app.tsx` adds the four routes under the existing authenticated layout. List/detail use the read guard; create uses the manage guard; edit requires manage and a readable position in practice. Components use Spanish i18n keys under `positions.*`, stable names/test ids, shared date formatting and table containment at narrow widths.

### D11. Position writes emit identifier-only audit events; reads preserve current search policy

Create, content update and status transition emit closed-catalogue events (`position.created`, `position.updated`, `position.status_changed`) in the same transaction as the row write. Events contain position id, actor, correlation id, event type and outcome code only—never title, description, location or requirements. The audit UI gains Spanish labels for the new types.

Position list/detail and candidate matching do not emit audit rows. This follows the existing rule that searches/lists are not audited and avoids turning every live match refresh into trail noise. Reading a specific candidate from a result continues to be audited by the candidate capability.

### D12. Test at the boundary that owns each risk

- Domain/unit tests cover normalization, lifecycle, validation, handler guard matrices, mapping, filter codec error selection and repository outcome mapping with hand-written doubles.
- Sanitizer unit/security tests run the real parser against scripts, SVG/MathML, malformed nesting, encoded protocols, event attributes, CSS and paste aliases, and assert canonical output.
- PostgreSQL integration tests cover constraints, unique races, `xmin`, all endpoints, authorization-before-validation, minimal projections, role seeds, grants/revokes and `EXPLAIN` plans over representative data.
- Frontend Vitest/Testing Library tests cover service requests, the signal hook, routes, permission combinations, editor serialization, list state, preset copying and no-search behavior without candidate permission.
- Playwright covers create/edit/close/reopen, preset copy/save, live candidate changes, paging, responsive navigation and keyboard use.
- Existing candidate search, preset, identity, navigation and permission-vocabulary suites are run because KTL-15 composes those boundaries.

## Risks / Trade-offs

- **[Rich-text dependency admits more syntax than intended]** → Register only the necessary Lexical nodes; sanitize independently on the server with an empty-by-default allowlist and an attack corpus.
- **[Sanitizer and editor serialize equivalent markup differently]** → Treat server output as canonical, reload the returned response after writes and compare semantics rather than raw editor HTML in UI tests.
- **[`pg_trgm` cannot be installed by the production migrator]** → Add a preflight/runbook check and fail the explicit migration before the table is exposed; do not fall back silently to an unindexed contract.
- **[Positions with empty criteria expose a broad candidate result]** → Preserve the confirmed search meaning, require `candidates.read`, page at 25 and show the criteria summary so the breadth is visible.
- **[A custom manage-only role has an incomplete SPA journey]** → Keep permissions genuinely independent, hide read-dependent screens/actions and document that the seeded HR manager roles carry both.
- **[Stored filters become unreadable after a future schema change]** → Keep the version in JSON and a column, use one codec, add explicit migrations for future versions and fail closed today.
- **[Dynamic matches are mistaken for assignments]** → Label them `Candidatos coincidentes`, store no relationship/count and document that results change with candidate data.
- **[Position free text leaks through diagnostics]** → Expand redaction sentinel coverage and log only ids, status codes, safe paging metadata and correlation ids.

## Migration Plan

1. Add and centrally pin the backend sanitizer and frontend editor dependencies; restore/build them in CI before generating the migration.
2. Add domain/application/infrastructure mappings and generate the EF Core KTL-15 migration. Review generated SQL plus the explicit extension, constraints, role updates and grants.
3. In staging, verify `ktl_migrator` can create/use `pg_trgm`, back up PostgreSQL, run the explicit `--migrate` entry point, inspect grants and execute schema/query-plan integration evidence.
4. Deploy the API containing the new permission catalogue and position routes, then deploy the SPA. The database change is additive, so the previous API/SPA remains compatible during rollout.
5. Verify seeded/custom roles, unauthorized matrices, create/read/update, sanitizer output, live matching and mobile navigation; publish KTL-15 release notes.

Rollback the SPA and API first. If no position data must be retained, the reviewed EF down migration may remove `OPS_Positions` and its KTL-15 indexes after backup; it does not remove `pg_trgm`. If position data exists, keep the additive table or export it and obtain explicit operator approval before a destructive down migration. Reverting permission strings from seeded roles is safe only after no deployed application recognizes or requires them.
