## Context

See [proposal.md](proposal.md) for motivation and the [candidate-profile-pages delta](specs/candidate-profile-pages/spec.md) for behavior. The React detail page currently puts `CandidateAvailabilityBlock` under the contact line and renders an audit panel beside `CandidateMainPanel`. `CandidateMainPanel` renders reception and review dates unconditionally. The existing date helpers provide localized timestamps, elapsed text, calendar-day formatting and the viewer's local day. KTL-34 already gives the summary row responsive two-column styling; KTL-36 owns availability behavior.

## Goals / Non-Goals

**Goals:** Recompose the existing components without changing their service contracts or edit coordination. Make all remaining date cues understandable, accessible, and stable across time zones. Keep the narrow layout within 390 pixels.

**Non-Goals:** No new retention workflow, API filter, default review date, consent display, availability rule, or backend write.

## Decisions

1. **Move the availability component into a headed `article.panel` in the summary row.** The page keeps ownership of the summary grid and the block keeps its own immediate actions, undo state and live region. Use the existing two-column class only while Datos principales is read-only. This avoids changing the availability service or placing its action in the per-panel coordinator. Remove audit markup and its test IDs and copy keys. The component's `key={item.id}` continues to preserve its state across aggregate updates and reset it for a different candidate. Place the inline form's dates and «Registrar»/«Cancelar» actions in one wrapping flex row. Let the form use the panel width: a 32rem cap would force the two-date «No disponible» form's buttons onto another row even when a wide panel has enough space. Narrow panels may wrap naturally.

2. **Keep status and audit information in the header.** Render the inactive heading as one localized message with an interpolated candidate name and a styled suffix using `Trans` from the existing `react-i18next` dependency; keep the suffix key `candidate.detail.inactiveSuffix` and render its marked suffix through a muted component. This allows Spanish word order and spacing to live in the catalogue rather than JSX concatenation. The breadcrumb uses `candidateFullName` alone, and pipeline badges follow the heading text. Place a single relative update line in the right toolbar beneath the status button. `formatElapsed` accepts a calendar-day string, so convert the `updatedAt` timestamp to the viewer's local day first (`localDay(new Date(item.updatedAt))`); passing the timestamp directly would produce an empty result. Use a real button to toggle an inline disclosure with labelled, exact times formatted using the existing `formatDate` options; expose expansion state and an association to the disclosure. This provides keyboard and screen-reader access without a hover-only tooltip. For readers lacking `candidates.update`, the line occupies the button's position. Existing permission checks only control UI; the API remains the authority.

3. **Use local calendar-day comparisons in a pure sibling helper.** A `candidate-main-panel.logic.ts` helper derives review urgency from stored `YYYY-MM-DD` values and a supplied local today, using a 30-calendar-day inclusive window. It also decides whether to show Recepción by comparing its stored day with the local calendar day of `createdAt`; empty reception stays visible. Comparing normalized day strings or day ordinals avoids UTC midnight shifts and daylight-saving elapsed-hour errors. Invalid stored dates retain the existing display fallback and get no urgency marker. Unit tests cover yesterday, today, day 30, day 31, and a western time zone. The marker is text as well as color.

4. **Scope spacing and localization.** Add a candidate-page-specific class around the header. Use 8px of vertical margin above and below the name/contact group and an 8px internal gap, one step above the global header's 4px gap. The current stylesheet has no spacing custom properties, so these values follow the documented Kepler spacing scale without adding a global token solely for this page. Remove the availability block's old header-specific top margin when it moves into the panel. Add/replace Spanish and English catalog keys for status, timestamp disclosure, LOPD labels and markers; associate the form hint with the review input through `aria-describedby`. Avoid any new runtime dependency.

5. **Avoid duplicate availability labels and tighten the panel's visual hierarchy.** The panel heading already names the property, so render the localized value directly instead of wrapping it in «Disponibilidad: {value}». Put the bold value and regular-weight check metadata in one wrapping flex summary, with space between them when they fit on one row. Increase the block's grid gap from 6px to 12px between this summary, hints and actions, and set their text line height to 1.5. This keeps wrapped text readable on narrow screens.

6. **Maintain the existing security and data boundaries.** The React SPA continues to read the same candidate aggregate through its service and API transport. There is no data-model change, migration, PostgreSQL grant or RLS change, role change, storage change, or API contract change. Candidate audit and retention data are personal data; the page shows only fields already available to authenticated candidate readers and does not log or export them. Document preview and downloads retain their API permission checks and private storage behavior. Existing backend and frontend security suites supply regression evidence, alongside focused UI tests.

## Risks / Trade-offs

- **Exact timestamps could be inaccessible if a disclosure only responds to pointer hover** → Use a focusable button, visible inline content, `aria-expanded`, and a unit test with keyboard activation.
- **Calendar-day boundaries can drift west of UTC or across daylight-saving transitions** → Compare local calendar days and test boundary dates with a western time zone.
- **Moving availability can reset undo state or break a concurrent panel edit** → Keep the same component key and aggregate update path; rerun KTL-36 interaction scenarios, including a check during a main-panel draft.
- **The extra header content can overflow at narrow widths** → Scope wrapping rules to the candidate header and verify at 390 pixels with Playwright.

## Migration Plan

Deploy as a frontend bundle update. No database migration, API coordination, or data backfill is needed. Rollback is a frontend bundle rollback. Update the candidate-profile-pages spec when the change is archived.
