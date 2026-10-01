# KTL-35 — CV from the candidate tables

## What changes for users

- The candidate list, the advanced search results and both tables on a position page end with a
  «CV» column, shown only to users who may download documents. Each row offers:
  - a **disk** button that downloads the primary CV when it is clean, in any format;
  - an **eye** button that shows a clean PDF on the same page, crossed out to hide it again.
- On screens at least ~1400px wide the CV opens in a panel beside the table (arrow →, ← to
  hide); on narrower screens it opens under the candidate's row (arrow ↓, ↑ to hide). Resizing
  across that width keeps the CV open and moves it without downloading it again.
- One CV is open per page, across both position tables; the open row is highlighted. The CV
  closes when its row leaves the table (paging, sorting, filters, a new search, removal from the
  position).
- The «CV» tick (KTL-34) and the search page's «Abrir CV» button are gone; the row download
  replaces «Abrir CV» everywhere.
- The candidate page shows its CV preview only when the primary CV is a clean PDF. A pending
  primary CV shows the preview as soon as its scan reports it clean.

## Contract

- Search items (`POST /api/candidates/search`) and position candidate items
  (`GET /api/positions/{id}/candidates`, and the add and stage-change responses) gain
  `primaryCvPreviewable` and `primaryCvDownloadable`. Both are `false` for actors without
  `documents.download`.
- **Breaking:** search items no longer carry `primaryCvDocumentId`. Downloads resolve the document
  through `GET /api/candidates/{id}/documents`.
- No new endpoint, permission, table, migration or grant. Content still comes only from the
  audited, clean-scan-gated document download.

See [`docs/ktl-10/search.md`](../ktl-10/search.md) and
[`docs/ktl-18/list-contract.md`](../ktl-18/list-contract.md) for the item shape.
