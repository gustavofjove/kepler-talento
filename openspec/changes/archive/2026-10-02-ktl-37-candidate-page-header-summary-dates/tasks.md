## 0. Create Feature Branch

- [x] 0.1 Create and switch to `feat/KTL-37` before implementation; inspect the existing worktree and preserve the untracked ticket brief. (All proposal requirements)

## 1. Summary Row and Header

- [x] 1.1 Move the existing availability block into a headed panel beside Datos principales, remove the Auditoría panel, update the availability CSS's old header margin, and preserve responsive stacking and the block's immediate behavior and test IDs. (Competencias panel and stacked sections; Availability block on the candidate page; One candidate page with per-panel edit mode)
- [x] 1.2 Add the interpolated inactive suffix (`candidate-inactive`) and the accessible relative and exact timestamp disclosure (`candidate-timestamps`); convert `updatedAt` to the viewer's local calendar day before calling `formatElapsed`, and adjust candidate-page header spacing and wrapping at 390 pixels. (Detail page keeps viewing and status actions; Candidate values are shown in readable form)
- [x] 1.3 Update Spanish and English localization keys, removing obsolete candidate audit and active-row copy and preserving the separate admin Auditoría copy. (Detail page keeps viewing and status actions; Availability block on the candidate page)
- [x] 1.4 Rename the availability submit action to «Registrar» and place the date fields with the form buttons in a wrapping row, with aligned control tops and heights; verify wide and narrow layouts. (Availability block on the candidate page)
- [x] 1.5 Remove the repeated availability label below the panel heading, increase line spacing, and verify the text and wide/narrow panel layout. (Availability block on the candidate page)
- [x] 1.6 Put check metadata to the right of the bold availability value when space permits, retaining wrapping and accessible text; verify both widths. (Availability block on the candidate page)

## 2. Retention and Reception Dates

- [x] 2.1 Add a pure local-calendar-day helper for review urgency and conditional Recepción, including invalid and empty values. (Candidate values are shown in readable form)
- [x] 2.2 Update Datos principales read mode and the core form's LOPD label, hint and `aria-describedby`; use text plus warning/danger styling for markers. (Candidate values are shown in readable form)
- [x] 2.3 Add or update `docs/ktl-37/` documentation for the visible date and layout behavior; keep the active delta as the spec source until OpenSpec sync or archive. (All modified requirements)

## 3. Verification

- [x] 3.1 Review and update affected existing unit tests for the summary row, active suffix, accessible timestamps, main panel, form hint, availability interactions and obsolete test IDs; add day-boundary tests for yesterday, today, +30 and +31 days and a western time zone. (All modified requirements)
- [x] 3.2 Run the affected frontend unit and integration suites from `frontend/`; inspect their results and confirm the API responses and stored PostgreSQL candidate fields remain unchanged by display-only interactions. Retain legacy Supabase integration checks for unchanged legacy paths. (Candidate values are shown in readable form; Availability block on the candidate page)
- [x] 3.3 Run affected backend authorization tests and `frontend/tests/security` checks for unauthenticated and unauthorized candidate and document access; run the least-privilege database and private-storage security gates, inspect results and verify PostgreSQL and storage state. (Detail page keeps viewing and status actions; personal-data boundary)
- [x] 3.4 Update and run `tests/e2e/candidate-crud.spec.ts`, replacing `candidate-active` assertions with `candidate-inactive` presence and absence; run targeted candidate availability and responsive browser scenarios at wide and 390-pixel widths, inspect results and restore or clean up test data. (All modified requirements)
- [x] 3.5 Run `npm run lint` and `npm run format:check` from `frontend/` and inspect results before considering the change done. (All modified requirements)
