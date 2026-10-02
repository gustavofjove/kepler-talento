# KTL-37 — Candidate page header, summary row and dates

**Status:** Draft
**Depends on:** KTL-34 (main data and audit side by side, readable dates), KTL-36 (availability
block)

## Summary

Rework the top of the candidate page (`candidate-detail-page.tsx`) now that availability has its
own block (KTL-36), and reduce the number of dates competing for attention on it.

The «Auditoría» panel beside «Datos principales» is replaced by the availability block; the
candidate's active state moves into the page heading; the record timestamps shrink to one
relative line under the «Baja lógica» / «Alta lógica» action; the heading block gets more vertical
room; and the dates that remain are labelled and emphasised by what they are for.

Today the page shows five dates with equal weight: Creado, Actualizado, Recepción, Revisión and
the availability «Comprobado el». They answer different questions for different people:

| Date                     | Question it answers                                         | Reader                |
| ------------------------ | ----------------------------------------------------------- | --------------------- |
| Comprobado el            | Can I trust the availability shown?                         | Recruiter, every time |
| Revisión (`reviewDueAt`) | When must we review whether we may keep this personal data? | Compliance            |
| Recepción (`receivedAt`) | When did the CV arrive?                                     | Recruiter, sometimes  |
| Creado / Actualizado     | When was the record entered / last changed?                 | System, audit         |

`reviewDueAt` is retention metadata (`openspec/config.yaml`, KTL-7, KTL-8): the day on which the
company should review whether it may still hold the candidate's data. Nothing in the application
acts on it today. The «Pendientes de revisión» dashboard count was removed in KTL-18 because the
search contract has no review-date filter. Its current label «Revisión» sits next to the
availability «Comprobado el» and reads as "when to check on this candidate again".

None of this changes the API contract, permissions or stored data. It is a presentation change
only.

## Specs updated

- `openspec/specs/candidate-profile-pages/spec.md`:
  - _Competencias panel and stacked sections_: the first row pairs Datos principales with
    Disponibilidad instead of Auditoría; the panel order no longer lists Auditoría.
  - _Availability block on the candidate page_: the block is a panel in the summary row, not part
    of the page header under the contact line.
  - _Detail page keeps viewing and status actions_: the record timestamps sit under the
    activate/deactivate action; an inactive candidate's heading carries «(Inactivo)».
  - _Per-panel edit mode_ (the requirement listing which panels offer «Editar»): Auditoría is no
    longer a panel; Disponibilidad offers no «Editar» (it acts immediately).
  - _Candidate values are shown in readable form_: the audit-time rule becomes the relative
    «Actualizado» line with exact times on demand; Recepción is shown in read mode only when it
    differs from the creation day; the review date is labelled as an LOPD review and marked when
    due or overdue.

## Changes

### 1. Availability replaces Auditoría in the summary row

**Today.** The summary row (`candidate-summary-row`) shows «Datos principales» on the left and
«Auditoría» (Creado, Actualizado, Activo) on the right. The KTL-36 availability block sits in the
page header, under the contact line.

**Change.** The right-hand panel of the summary row becomes a «Disponibilidad» panel holding the
existing `CandidateAvailabilityBlock`, unchanged in behaviour: same lines, actions, inline form,
«Deshacer», live region and test ids. It is removed from the page header.

- It is an `article.panel` with an `h2` «Disponibilidad» (`candidate.availability.title`), so the
  page keeps one heading per panel. It offers no «Editar»: it still acts immediately and stays
  outside the per-panel edit-mode coordinator (KTL-36 rules unchanged).
- The KTL-34 layout rules carry over: two equal columns on a wide sections column; stacked, Datos
  principales first, below 620px or while Datos principales is in edit mode.
- The «Auditoría» panel, its `candidate-audit` test id and the `candidate.detail.audit` key are
  removed.

### 2. «(Inactivo)» after the name; no «Activo» field

**Today.** The active state is only visible as «Activo: Sí/No» in «Auditoría».

**Change.** For a logically removed candidate, the page `h1` reads «{Nombre} {Apellidos}
(Inactivo)», with «(Inactivo)» in muted styling (`candidate.detail.inactiveSuffix`, rendered via
interpolation, not concatenated) and a `candidate-inactive` test id. Active candidates show the
name alone. The pipeline badges stay after it.

- The «Activo» row, the `candidate-active` test id and the `candidate.detail.active` /
  `.yes` / `.no` keys are removed.
- The breadcrumb keeps the plain name.
- The existing editor hint for removed candidates (`candidate-removed-hint`) is unchanged.

### 3. One relative timestamp under the status action

**Today.** «Auditoría» prints Creado and Actualizado as two full timestamps.

**Change.** The top-right toolbar shows, under the «Baja lógica» / «Alta lógica» button, one muted
line «Actualizado {elapsed}» («Actualizado hace 3 días», `candidate.detail.updatedAgo`), using the
existing `formatElapsed` helper. Test id `candidate-timestamps`.

- Next to it, an info control (a focusable button with an accessible name such as «Ver fechas del
  registro») reveals both exact times in the KTL-34 format: «Creado 30 sept 2026, 11:49 ·
  Actualizado 28 sept 2026, 09:10». It works with keyboard and screen readers, not only on
  hover; the exact times are never available through a `title` attribute alone.
- Shown to **every** reader. Readers without `candidates.update` see no button, and the line takes
  its place.
- Right-aligned on wide screens; at 390px it wraps under the heading block without horizontal
  scroll.
- `candidate.detail.created` / `updated` are replaced by the new keys.

### 4. More space around the name and contact line

**Change.** The heading block (`h1` and the contact line) gets more vertical margin above and
below, using the Kepler spacing tokens, scoped to the candidate page (no change to the global
`.page-header`). Exact values to be settled in design; target roughly one spacing step more than
today on each side.

### 5. Review date labelled as an LOPD review

**Change.** Copy only, in `es.json` (and `en.json`):

| Key                            | Today          | New                    | English          |
| ------------------------------ | -------------- | ---------------------- | ---------------- |
| `candidate.detail.reviewDueAt` | Revisión       | Revisión LOPD          | LOPD review      |
| `candidate.form.reviewDueAt`   | Fecha revisión | Fecha de revisión LOPD | LOPD review date |

The form field gets a hint under it (`candidate.form.reviewDueAtHint`): «Fecha en la que revisar
si se pueden seguir conservando los datos del candidato.», linked with `aria-describedby`.

### 6. Due and overdue LOPD reviews are marked

**Change.** In «Datos principales» read mode, the review date stays quiet until it matters:

- **Overdue** (the date is before today, in the viewer's local day): «(vencida)» after the date, in
  the danger style (`candidate.detail.reviewOverdue`).
- **Due soon** (today or within the next 30 days): «(próxima)» in the warning style
  (`candidate.detail.reviewDueSoon`).
- Otherwise, or when empty («Pendiente»), no marker.

The marker is text, not colour alone. The comparison uses the same calendar-day rules as
`formatDay` and the availability «(vencido)» (KTL-36), so a viewer west of UTC never sees a date
flip a day early. The 30-day window is a frontend constant in a `*.logic.ts` helper, with unit
tests at the boundaries (yesterday, today, +30, +31 days).

This is a display hint only. It triggers no workflow, notification or filter.

### 7. Recepción shown only when it adds information

**Today.** «Recepción» is always shown. For candidates entered by hand it is usually the same day
as Creado, which is now hidden behind the info control (change 3).

**Change.** In «Datos principales» **read mode**, the Recepción row is shown when it is empty
(«Pendiente», a prompt to fill it) or when its day differs from the local creation day. When it is
the same day as creation it is hidden. The core record form always shows and edits the field.

## Out of scope

- Availability behaviour, rules and API (KTL-36).
- The admin «Auditoría» section, which is unrelated and unchanged.
- **`consentAt`.** It is stored and imported but is neither shown on the candidate page nor
  editable in the form, so the retention picture HR sees is incomplete. Surfacing it is a
  separate decision.
- **Server-side retention review.** A search filter or dashboard count of overdue LOPD reviews
  (the removed «Pendientes de revisión»), a default review date derived from `consentAt`, and
  enforcing «review date not before reception date» in the API validators. All of these change
  the contract and need their own ticket.

## Acceptance criteria

- On a wide screen, «Datos principales» and «Disponibilidad» sit side by side in the first row;
  no «Auditoría» panel exists; the page header no longer holds the availability block.
- Every KTL-36 availability scenario (reconfirm, change, undo, lapsed, reader without actions,
  check while another panel is edited) still passes with the block in its new place.
- Opening «Datos principales» for editing makes it full width with «Disponibilidad» below; closing
  restores the two columns. At 390px the two stack.
- A removed candidate's heading ends with «(Inactivo)»; an active one's does not; deactivating and
  reactivating toggles it without reload. No «Activo» field is shown anywhere.
- Under the status button (or in its place for readers) one line reads «Actualizado hace …»; the
  info control reveals both exact timestamps and is reachable and announced by keyboard and screen
  reader.
- The name and contact line have visibly more space above and below; no horizontal scroll at
  390px.
- Datos principales reads «Revisión LOPD»; the form reads «Fecha de revisión LOPD» with its hint.
- A review date in the past shows «(vencida)»; today or within 30 days shows «(próxima)»; later or
  empty shows no marker, in every time zone.
- A candidate created the day the CV was received shows no Recepción row in read mode; one received
  on an earlier day, or with no reception date, shows it; the form always shows the field.
- Tests updated: `tests/unit/candidate-detail-page.spec.tsx` (summary row, active state,
  timestamps), the main panel spec (review marker, conditional Recepción), and
  `tests/e2e/candidate-crud.spec.ts` (replaces `candidate-active` assertions with
  `candidate-inactive` presence/absence).

## Open questions

- Should «(Inactivo)» also appear in the breadcrumb or the browser tab title? Proposed: no.
- Panel title «Disponibilidad»: confirm wording.
- «LOPD» vs «LOPDGDD» vs «RGPD»: the current Spanish law is the LOPDGDD (2018), which complements
  the RGPD; «LOPD» is the familiar name. Proposed: «LOPD», as requested.
- Due-soon window: 30 days proposed.
