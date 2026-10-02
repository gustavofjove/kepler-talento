## Why

The candidate page gives audit timestamps and routine dates the same weight as availability and retention review, making the most useful information harder to find. With availability now a separate block, the summary row and header can present each fact where readers need it.

## What Changes

- Pair Datos principales with a Disponibilidad panel in the first row; remove the Auditoría panel and move availability out of the header. Preserve availability actions and immediate-save behavior.
- Show a removed candidate's «(Inactivo)» state beside the name. Keep the breadcrumb's plain name.
- Show one relative «Actualizado …» line beneath the status action, or in its place for readers, with a keyboard-accessible control revealing exact creation and update times.
- Give the name and contact line more vertical room within the candidate page.
- Label the retention review date «Revisión LOPD», explain its purpose in the form, and mark overdue and upcoming dates in read mode.
- Hide Recepción in read mode when its calendar day equals the creation day; keep it visible when empty or different and always show it in the form.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `candidate-profile-pages`: Change the summary layout, status and timestamp presentation, availability placement, and retention and reception date presentation.

## Impact

Recruiters and compliance readers get a clearer candidate summary. Editors keep the existing status and availability actions. The affected entity is the candidate record, including personal data in its contact, audit, reception, and retention fields. Date comparisons use the viewer's local calendar day; an empty review date has no urgency marker, and an empty reception date remains visible.

Changes are confined to the React candidate page, its panel/form helpers and styles, localization catalogues, tests, and the candidate-profile-pages spec. The API contract, stored data, permissions, RLS policies, role definitions, and private document storage do not change. Existing API authorization and document access remain the boundary; the UI only presents fields already available to authorized readers. Success means the stated wide and narrow layouts, status toggling, accessible exact timestamps, review-date boundaries, and conditional reception row pass unit and browser checks without changing availability behavior.
