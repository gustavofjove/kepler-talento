# Release notes — KTL-18 server-side candidate list

## What changes for users

- **Sorting and filtering now apply to every matching candidate**, not only to the candidates the
  browser had downloaded. Sorting by _Nombre_ puts the alphabetically first candidate in the whole
  set on page one. This is a correctness fix, even where it looks like a change.
- **A page now shows 25 candidates by default** (it was 10). You can choose 25, 50 or 100.
- **The text filter also searches notes.** A word that appears in a candidate's notes now finds
  that candidate, as advanced search already did. Common words may return more results than before.
- **Selection belongs to the page you are looking at.** Changing page, sort or a filter clears it,
  and the list says how many candidates on this page are selected. _Baja lógica masiva_ and _Alta
  lógica masiva_ act only on those. Selections no longer carry over between pages.
- **"Incluir inactivos" now requires the permission to remove and restore candidates**
  (`candidates.delete`). People without it no longer see the option.
- **You can share a list view.** The page, sort, status, CV and "incluir inactivos" choices are in
  the address, so a copied link, a reload or the back button reopens the same view. **The text you
  type is deliberately not included**, because names and emails must not end up in web server logs;
  it is cleared on reload.
- **The dashboard shows fewer figures.** _Pendientes de revisión_ and _Recibidos este mes_ are gone;
  _Sin CV adjunto_ is now _Sin CV principal_ (a candidate with documents but no primary CV counts);
  _Inactivos_ is shown only to people who may see removed candidates.

## Privacy

The list no longer downloads every candidate's notes, consent and retention dates, location and
source. The browser receives one page of names, contact details, status and CV presence.

## For operators

- Migration `Ktl18CandidateSortIndexes` adds two indexes on `CND_Candidates` for sorting by name
  and by status. It changes no grants and runs through the usual `migrator` container.
- `POST /api/candidates/search` accepts `sortField`, `sortDirection` and `includeInactive`, and
  each result carries `isActive`. Requests without them behave as before. See the
  [list contract](list-contract.md).
- `GET /api/candidates` (unpaged) is unused by this SPA but kept for rollback; it is removed in the
  next release.
