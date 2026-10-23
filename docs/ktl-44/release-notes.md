# KTL-44 release notes

Frontend-only. No endpoint, contract, migration, grant or role changes.

- **A candidate opened from a position leads back to it.** Opening a candidate from a position's
  page shows «Posiciones › _position title_ › _full name_» instead of «Candidatos › _full name_».
  The title opens that position and «Posiciones» opens the position list. This holds for every way
  into a candidate on that page: a row or name in «Candidatos de la posición», a row or name in
  «Candidatos que encajan», and «Ver ficha» in the CV shown beside them.
- **Everywhere else the trail is unchanged.** The candidate list, Búsqueda, Inicio and a
  candidate's own «Posiciones» panel still lead to «Candidatos › _full name_». So do a new tab
  (Ctrl/⌘-click or middle-click) and an address typed or pasted into the browser.
- **History keeps it.** Returning to the candidate page with Back or Forward keeps the trail it was
  opened with. The title is the one shown on the position page at the time; a later rename appears
  on the next visit from the position.
- **Never more than three segments.** Links leaving a candidate page carry no origin, and a
  position's own trail is always «Posiciones › _title_». Going candidate → position → candidate
  therefore starts again from that position.
- **Links follow permissions.** As in KTL-23, «Posiciones» and the title are links only with
  `positions.read`; otherwise they are plain text. Route guards and the API still enforce access.
- **Loading and failures.** While the candidate loads, or when it fails to load or is not found,
  the trail offers «Posiciones › _title_».
- **Personal data.** The origin travels in the browser's navigation state, not the URL. It holds
  only the position's identifier and title, never candidate data, and is not logged.
- **Stable selectors for tests.** The position segments reuse `breadcrumb-positions` and
  `breadcrumb-position`, as on the position edit form.
