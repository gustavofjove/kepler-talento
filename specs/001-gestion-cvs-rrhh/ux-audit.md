# UX Audit And Improvement Plan

## Scope

Audit performed on implemented MVP surfaces: app shell, candidate list, advanced search, catalogs, import/export operations, and detail/document actions.

## Method

- Heuristic review (Nielsen + enterprise task efficiency).
- Daily-operation walkthrough for RRHH personas (`rrhh_admin`, `rrhh_user`, `readonly`).
- Interaction quality checks: clarity, feedback loops, error prevention, consistency, keyboard flow.

## Findings (Ordered By Severity)

### Critical

1. Confirmation patterns are inconsistent (`window.confirm` in destructive actions, no unified confirmation UX).
2. Keyboard and screen-reader discoverability is partial in global shell (missing skip-link and explicit live region semantics for toasts).

### High

1. Candidate list filters are effective but not visually represented as removable active criteria chips.
2. Catalog operations can be performed, but edit context and action intent are not explicit enough for high-volume admin usage.
3. Import flow is understandable but lacks stronger guided progression cues for step state (`validated`, `ready to commit`, `committed`).

### Medium

1. Terminology and microcopy vary across screens (`Busqueda`, `Importacion`, `edicion` without accents), lowering perceived polish.
2. Status visual hierarchy relies mostly on color/tag style and can be improved with semantic labels and contextual help.
3. Search/export history is present but does not yet support sorting/filtering for operational audits.

### Low

1. Dense table actions on narrow screens reduce scan speed.
2. Some pages rely on toast-only confirmation for completed actions where inline hints could reduce cognitive load.

## Personas And Flow Friction

### RRHH Admin

- Needs fast operation over many records.
- Main friction: repeated filter setup and destructive action confidence.

### RRHH User

- Needs clear path from search to candidate profile and CV actions.
- Main friction: contextual feedback and visual indication of current working state.

### Readonly

- Needs clear understanding of why actions are disabled.
- Main friction: permission boundaries are technically enforced but not always proactively explained.

## UX Improvement Principles

1. Clarity first: each screen states current state, next action, and operational impact.
2. Safe speed: optimize frequent tasks while protecting destructive actions.
3. Consistency: one interaction pattern for confirmations, statuses, and feedback.
4. Accessible by default: keyboard-first navigation and clear live announcements.

## Implementation Waves

### Wave UX-1 (Quick Wins)

- Add shell accessibility foundation (skip-link, semantic live regions, improved toast structure).
- Add active filter chips with one-click removal in candidate list.
- Improve catalog page operation context (editing mode visibility and guardrail hints).
- Normalize key microcopy in high-traffic surfaces.

### Wave UX-2 (Operational Safety)

- Replace `window.confirm` with unified confirm dialog component.
- Add actionable empty-states by permission and context.
- Add inline post-action summaries in list/import/search screens.

### Wave UX-3 (Efficiency)

- Saved table views for candidate list (columns + sort + page size).
- Bulk action preview (affected records count and warnings).
- Search/export history filters and quick re-run actions.

### Wave UX-4 (Accessibility Hardening)

- Keyboard traversal audit and focus order fixes per screen.
- Contrast and semantic status review for all critical labels.
- Add E2E accessibility smoke checks for top workflows.

## Success Metrics

- Time-to-target-candidate in list/search reduced by 30% on seeded dataset.
- Mis-clicked destructive action retries reduced by 50%.
- Keyboard-only completion for top 5 workflows without blockers.
- Reduction in support clarifications around permissions and flow state.
