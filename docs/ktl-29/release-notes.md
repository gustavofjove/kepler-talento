# KTL-29: One candidate page with per-panel editing

Recruiters now view and edit a candidate on a single page, one section at a time, with the CV
still beside the data.

**One page per candidate.** `/app/candidates/:id` is the only page for an existing candidate. The
separate edit page introduced by KTL-22 is gone. Its address, `/app/candidates/:id/edit`, now
redirects to the candidate page, replacing itself in the browser history, for anyone holding
`candidates.read`. Old bookmarks keep working. After the first save on the create page, every
creator lands on the new candidate's page.

**Edit a panel in place.** Datos principales, Competencias, Formación, Experiencia, Notas and
Documentos each have an «Editar» button. It switches that panel, in its place on the page, to the
editor the edit page used to show. The other panels stay read-only. Auditoría has no «Editar».
Panel layouts are unchanged.

**Form panels save only themselves.** Datos principales, Competencias, Formación and Experiencia
keep changes as a draft until the panel's own «Guardar». «Cancelar» discards the draft.

- **Datos principales** sends one core-record write.
- **Formación** and **Experiencia** each send one write with the whole resulting list.
- **Competencias** writes only the families that changed, one after another. If the API refuses
  one family, the families already saved stay saved. The refused family keeps its draft and shows
  its Spanish error, and a further «Guardar» retries only that family. Families are not saved
  atomically.

This replaces the KTL-22 and KTL-27 behaviour, where each relation add, change or removal was
written immediately. A new language, skill or program still joins at its catalog's lowest active
level. Duplicates and invalid entries are refused before they join the draft.

**Notes and documents act immediately.** In Notas and Documentos, adding, editing or retiring a
note and uploading, marking primary or removing a document apply immediately, with their existing
confirmations. «Hecho» returns the panel to read-only. Document download and the CV preview are
available in both modes.

**One panel at a time, no silent loss.** Opening another panel while the current one has unsaved
changes asks whether to discard them. So do navigating away and reloading or closing the tab.
"Unsaved" means one of the following:

- a form panel whose draft differs from what is saved;
- note text being typed, or a note being edited;
- a selected file that is not uploaded yet.

**Who sees «Editar».** Each panel follows the permission the API checks for its writes:

- `candidates.update` for Datos principales, Competencias, Formación, Experiencia and Notas;
- `documents.upload` for Documentos.

A role holding `documents.upload` without `candidates.update` can therefore now manage documents
from the candidate page, which the edit route used to block. Hiding a button is a convenience
only: the API remains the authorization boundary, and its checks are unchanged.

**Removed candidates.** The API refuses relation and note writes on a removed candidate. For such
a candidate, Competencias, Formación, Experiencia and Notas offer no «Editar», and the page
explains that the candidate must be reactivated first. Datos principales and Documentos keep
following their permissions.

**Breadcrumb.** The `Candidatos › name › Editar` trail disappears with the edit page. The
candidate page keeps `Candidatos › name`, which always shows the stored name, never an unsaved
draft.

This release changes only the frontend. Every endpoint it uses already existed, and the
optimistic concurrency rule is unchanged: another user's edit since the page loaded makes a save
fail with a Spanish conflict message, and the draft is kept. PostgreSQL schema, grants, document
storage, scan gating and audit are unchanged.

**Test identifiers.**

- Added: `candidate-panel-<panel>-edit`, `-save`, `-cancel` and `-done`, where `<panel>` is `main`,
  `competencies`, `education`, `experience`, `notes` or `documents`.
- Added: `candidate-removed-hint`.
- Added: `confirm-cancel` on the shared confirmation dialog.
- Removed with the edit page: `candidate-edit-view` and `candidate-edit-hint`.
- Renamed: the create page's hint is now `candidate-create-hint`.
- Every other `data-testid` and `name=` attribute is kept.
