## Why

Recruiters work a position by opening its candidates one after another. Today every candidate
page shows `Candidatos › name`, so the breadcrumb leads to the candidate list rather than back to
the position they came from, and returning takes a trip through the position list. KTL-23 made the
trail static on purpose; KTL-44 asks for the one origin-aware trail that matters in practice.

## What Changes

- A candidate page opened from a position page shows `Posiciones` › the position title › the
  candidate's full name. `Posiciones` opens the position list and the title opens that position.
- Every way into a candidate from a position page carries the origin: the «Candidatos de la
  posición» rows and name links, the matching candidates' rows and name links, and «Ver ficha» in
  the row CV panel opened on that page.
- Every other entry (candidate list, Búsqueda, Inicio, a candidate's own positions, a new tab, a
  typed or pasted URL) keeps `Candidatos` › the candidate's full name.
- The trail never exceeds three segments: links leaving a candidate page never carry an origin and
  the position page's trail is fixed, so moving candidate → position → candidate starts afresh.
- While the candidate loads, or when it fails to load, the position segments stay offered.
- The KTL-23 rule "the trail is the same whichever page the user arrived from" is replaced for this
  single case. No other page's trail changes.

**Actors:** any signed-in profile that can open a position page (`positions.read`) and a candidate
page (`candidates.read`).

**Edge cases:** a malformed or foreign navigation state is ignored; a position renamed after the
navigation keeps the title captured at navigation time on that history entry until the user
navigates again; a profile without `positions.read` (only reachable through a crafted state) sees
the position segments as plain text.

**Success criteria:** from a position, one activation of the breadcrumb title returns to it; from
anywhere else the trail is unchanged; no sequence of navigations yields more than three segments.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `primary-navigation`: the "Breadcrumb trail on sub-pages" requirement gains an origin-aware
  candidate trail when arriving from a position page, replacing the "does not depend on the
  origin" scenario.

## Impact

- **Frontend only** (`frontend/`): a small candidate-origin module (type, context, shape check),
  `useRowLink` accepting navigation state, the position detail page and its two candidate tables,
  the row CV panel's «Ver ficha» link, and the candidate detail page's trail.
- **No API, schema, grant, permission or dependency change.**
- **Personal data:** no candidate data enters the navigation state; it carries only the position id
  and its stored title, which is not personal data. Nothing is added to the URL, document title or
  logs (principle 1). No role, storage or RLS change; route guards and the API keep enforcing access
  (principle 3).
- **Tests:** unit specs for the shape check, the candidate trail and the position page links; one
  Playwright journey in `positions.spec.ts`.
