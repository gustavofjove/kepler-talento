# KTL-28: CV preview beside the candidate sections

Recruiters can now read a candidate's CV next to the data they are checking or transcribing.

**Wider content area.** Every page inside the application shell now caps its content at 1440px
(`main` max width, padding included) instead of 1180px. Lists, search, positions and the admin
pages keep their fluid layouts and simply use the extra room.

**CV beside the data.** On the candidate detail page and on the edit page of an existing
candidate, the CV preview becomes a right-hand column next to the sections when the page's content
area is at least 1360px wide. On a typical 1920px office monitor that means:

- The shell widens up to 1920px on those pages only, so both columns have room.
- The preview column sticks below the application header while the sections scroll.
- Below the threshold, for example on a 1366px laptop or a phone, the preview follows the last
  section as before.

**Preview on the edit page.** The edit page of an existing candidate gains the same preview as the
detail page, under the same rule: it appears only for users holding `documents.download`. The
new-candidate page has no preview.

**Field grids follow their column.** Two-column field groups inside the sections column (main data,
Formación, Experiencia, Documentos) drop to one column when that column is narrower than 620px,
whatever the viewport width.

When the preview is not shown (no permission, no documents), the page keeps a single column at the
default width and no empty column remains. Section order, `data-testid` and `name=` attributes are
unchanged; the layout is pure CSS (`.page-split` in `frontend/src/styles.css`, keyed on the
preview's presence with `:has()` and on the content width with container queries).

This release changes only the frontend. The document endpoints, permissions, scan gating, audit,
PostgreSQL schema, grants and private-document access remain unchanged.
