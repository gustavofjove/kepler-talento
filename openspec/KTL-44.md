## [original]

Would it be possible to somehow detect if the user has landed onto a candidate page from a position
page, so we show in the breadcrumb Posiciones > [Position] > [Candidate] rather than Candidatos >
[Candidate]? Obviously we need to avoid loops so this is the max level of nesting.

## [enhanced]

### User story

**As** a recruiter reviewing the candidates of a position,
**I want** the candidate page I open from that position to show `Posiciones › position › candidate`
in its breadcrumb,
**so that** I can return to the position I was working on in one activation instead of going
through the position list.

### Decisions taken while refining

| Topic                | Decision                                                                                                                                                                                                                                              |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Spec change          | This reverses a KTL-23 decision: the `primary-navigation` spec requires that the trail not depend on the origin. The requirement and its "does not depend on the origin" scenario are modified.                                                       |
| How the origin moves | React Router location state on the navigation, carrying the position id and its stored title. Nothing in the URL, no extra request. Chosen over a `?position=<id>` query parameter, which would need a fetch and handling for stale or forbidden ids. |
| Entry points         | Every way into a candidate from a position page: the «Candidatos de la posición» rows and name links, the matching candidates' rows and name links, and «Ver ficha» in the row CV panel opened on that page.                                          |
| Fallback             | No state, malformed state, a new tab (Ctrl/⌘- or middle-click) or a pasted URL gives the usual `Candidatos › name`.                                                                                                                                   |
| Persistence          | The origin belongs to the history entry: Back/Forward keep it. It is never carried forward to the next navigation. A reload signs the user out (the token lives in memory), so it is not a case.                                                      |
| Depth                | Three segments at most, by construction: links leaving the candidate page never carry an origin, and the position page's trail is fixed.                                                                                                              |
| Permissions          | `Posiciones` and the position title follow the existing rule: activatable only with `positions.read`, plain text otherwise.                                                                                                                           |

### Functional description

1. Opening a candidate from a position page shows `Posiciones` › the position title › the
   candidate's full name. `Posiciones` opens the position list; the title opens that position.
2. While the candidate loads, or when it fails to load or is not found, the trail shows
   `Posiciones` › the position title, both still activatable.
3. Opening a candidate from anywhere else (candidate list, Búsqueda, Inicio, a candidate's own
   positions) keeps `Candidatos` › the candidate's full name.
4. Going from that candidate to one of its positions and back to a candidate starts a fresh
   three-segment trail from the new position; it never grows.

### API and data contract

No change. The position title already reaches the position page; the candidate page reads it from
the navigation state.

### Files to change (`frontend/`)

| File                                                                       | Change                                                                   |
| -------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `src/app/features/candidates/candidate-origin.ts` (new)                    | Origin type, context, `readCandidateOrigin` shape check, link-state hook |
| `src/app/shared/components/row-link.ts`                                    | `rowLink(to, state?)` passes state to `navigate`                         |
| `src/app/features/positions/position-detail-page.tsx`                      | Provides the origin for its candidate links                              |
| `src/app/features/positions/components/position-candidates-panel.tsx`      | Rows and name links carry the origin                                     |
| `src/app/features/search/components/search-results.tsx`                    | Rows and name links carry the origin when one is provided                |
| `src/app/features/candidates/components/row-cv-preview/row-cv-preview.tsx` | «Ver ficha» carries the origin when one is provided                      |
| `src/app/features/candidates/pages/candidate-detail-page.tsx`              | Builds the position trail from a valid origin                            |

Reused test ids: `breadcrumb-positions`, `breadcrumb-position` (as on the position edit page).
No new copy: `breadcrumb.positions` already exists.

### Acceptance criteria

1. **From a linked candidate.** Activating a candidate in «Candidatos de la posición» shows
   `Posiciones › title › name`; activating the title returns to that position.
2. **From a match.** The same holds for a matching candidate and for «Ver ficha» in the CV panel.
3. **Elsewhere.** From the candidate list or Búsqueda the trail is `Candidatos › name`.
4. **No growth.** Candidate (from position A) → position B → candidate gives
   `Posiciones › B › name`.
5. **Fallback.** A malformed origin, a new tab or a typed URL gives `Candidatos › name`.
6. **History.** Going back to a candidate page opened from a position shows the position trail again.
7. **Loading and failure.** The position segments are shown, activatable, while loading and after
   a failed load.
8. **Permission.** Without `positions.read`, `Posiciones` and the title are plain text.

### Test coverage

| Suite                                                | Coverage                                                                     |
| ---------------------------------------------------- | ---------------------------------------------------------------------------- |
| `frontend/tests/unit/candidate-origin.spec.ts` (new) | Shape check accepts a valid origin and rejects everything else               |
| `frontend/tests/unit/candidate-detail-page.spec.tsx` | Default trail, position trail, loading, failure, malformed state, permission |
| `frontend/tests/unit/position-pages.spec.tsx`        | Candidate links on the position page carry the origin                        |
| `frontend/tests/e2e/positions.spec.ts`               | Position → linked candidate → breadcrumb title returns to the position       |

### Documentation

- `openspec/specs/primary-navigation/spec.md`: breadcrumb requirement and scenarios.

### Non-functional requirements

- **Privacy:** no candidate data in the navigation state; the position title never enters the URL,
  the document title or a log.
- **Security:** no endpoint, permission or data path. The trail is a convenience; route guards and
  the API still enforce access.
- **Accessibility and responsive:** unchanged trail semantics; a long title and name fit at 390px.

### Out of scope

- Origin-aware trails for any other page pair (e.g. position opened from a candidate).
- Restoring the position page's scroll, pagination or open CV when returning.
