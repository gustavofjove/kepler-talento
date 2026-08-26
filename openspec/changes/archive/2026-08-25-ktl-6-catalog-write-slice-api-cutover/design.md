## Context

See `proposal.md` — Why. The constraints that shape this design:

- KTL-5 delivered the read-path skeleton: MediatR slices under `Application/Features/`, a
  `ValidationBehavior` turning FluentValidation failures into problems, `ApplicationExceptions`
  (`ForbiddenException`, `NotFoundException`), `GlobalExceptionHandler` producing RFC 9457
  problem details, `ICorrelationContext`, `ICurrentActor` with a single `candidates.read`
  capability, `ApplicationDbContext` with quoted uppercase table names (`CND_Candidates`,
  `AUD_`, `OPS_`), an `IsRowVersion` concurrency token on `Candidate`, `DatabaseInitializer`
  for migrate-and-seed, and `ApiTransport` on the frontend mapping problems onto `AppError`.
  This slice extends those pieces rather than introducing new machinery.
- The current `CatalogService` is fully synchronous: it hydrates a `signal<CatalogState>` from
  `localStorage` in its constructor, and `list()` / `activeNames()` are pure reads during
  render. Seven components depend on that synchrony — the admin page plus five candidate form
  sections and `search-filters`.
- Candidates are still `localStorage`-backed until KTL-8, so the server cannot yet observe
  which catalog values are in use.
- Architecture tests already enforce the inward dependency direction, and
  `DatabaseNamingTests` already asserts the naming convention; both must keep passing.

## Goals / Non-Goals

**Goals:**

- Settle the write conventions — validation codes, optimistic concurrency, auditing, logical
  deactivation, atomic collection reordering — on the cheapest aggregate available.
- Establish the loading/error pattern that every later API-backed feature service reuses.
- Keep the catalog administration and catalog-consuming screens behaving as users expect,
  with Spanish copy and accents preserved.

**Non-Goals:**

- A generic CRUD framework or code generation. Nine families share one table and one set of
  slices; abstraction beyond that is premature.
- A caching layer, an offline mode, or optimistic UI updates. Catalogs are small and edited
  rarely; a plain load-then-refetch loop is enough and keeps the pattern legible for reuse.
- Migrating anyone's existing `rrhh-catalogs` browser data.

## Decisions

### Per-slice frontend cutover — departure from the blueprint

**Departure**: the Stack Blueprint's adoption sequence step 9 implies a single terminal SPA
cutover after all backend slices exist. This change amends that: every backend slice ships with
its own React service already calling the API.

**Reason**: endpoints with no consumer are unproven integration debt. The asynchronous break,
the error surface, the permission mapping, and the Spanish copy alignment are only really
tested by a real consumer, and batching seven services' worth of that risk into one late change
makes it un-reviewable and un-rollback-able.

**Simpler alternative considered**: ship `CAT_` tables and endpoints now, keep `CatalogService`
on `localStorage`, and cut all services over in one change after KTL-10. Rejected because it
defers the entire integration risk into a single change and leaves acceptance criterion 5 (the
`rrhh-catalogs` key removed) unverifiable in this slice.

**Mitigation**: the cutover is confined to one feature service behind `ServicesProvider`, which
remains the component-facing seam; the catalog data is non-personal, so a defect exposes no
personal data; and the change is independently revertible because no other feature service is
touched. The Playwright catalog flow proves the whole path before the change is considered
done.

### One `CAT_CatalogItems` table, family as a column

Nine families share one table with a `Family` discriminator column rather than nine tables or a
`CAT_Families` parent table. The families are a closed, code-defined set (they map one-to-one
onto `CatalogFamily` in TypeScript and onto candidate relation kinds), so a families table would
add a join and a referential-integrity surface without adding a decision anyone can make at
runtime. `Family` is validated against the closed set at the application boundary and constrained
by a database check constraint.

Columns: `Id` (Guid, PK), `Family` (text, checked), `Code` (text), `NameEs` (text),
`NameNormalized` (text, persisted), `NameEn` (text, null), `SortOrder` (int), `IsActive` (bool),
`Version` (`IsRowVersion`). Unique indexes on `(Family, NameNormalized)` and `(Family, Code)`.

### Name uniqueness via a persisted normalized column

`NameNormalized` is written by the application as the trimmed, case-folded,
accent-stripped form of `NameEs`, and the unique index is on that column rather than on a
functional expression. Alternatives considered: a PostgreSQL expression index using `unaccent`
(needs an extension — principle 2 requires a documented reason for a new runtime dependency,
and one is not warranted here) and application-only checking (loses the concurrency guarantee
the spec requires). A persisted column keeps normalization identical to what the frontend
displays and keeps the constraint enforceable by the database.

The unique-index violation is caught in the write slices and translated to the stable
`catalog.name.duplicate` code with the message `Ya existe un valor con ese nombre.` — the
application pre-check stays for the ordinary case, and the constraint is the race-condition
backstop.

### Stable validation codes

`catalog.family.invalid`, `catalog.name.required`, `catalog.name.duplicate`,
`catalog.code.required`, `catalog.not_found`, `catalog.reorder.incomplete`,
`catalog.concurrency.conflict`. Spanish messages are copied verbatim from today's
`CatalogService` so the user-visible copy does not shift.

### Reorder is one whole-family operation

`PUT /api/catalogs/{family}/order` takes the complete ordered list of item identifiers for the
family and rewrites `SortOrder` as consecutive integers in one transaction, rejecting a list
that does not cover the family exactly. The admin page's existing up/down buttons compute the
new full order client-side and submit it. Per-item swap endpoints were rejected: two concurrent
swaps can interleave into a corrupt order, and the whole-family form makes the last writer's
order win in full, which is what the spec requires.

### Endpoints

```
GET    /api/catalogs/{family}?includeInactive=false   catalogs.read
POST   /api/catalogs/{family}                          catalogs.manage
PUT    /api/catalogs/{family}/{id}                     catalogs.manage
PUT    /api/catalogs/{family}/order                    catalogs.manage
PUT    /api/catalogs/{family}/{id}/active              catalogs.manage
```

There is deliberately no `DELETE`. Activation is a separate sub-resource rather than a field on
the update payload, so that "rename" and "deactivate" audit as distinct events and so that the
absence of a delete verb is visible in the published contract.

### Authorization model

`Permissions` gains `CatalogsRead = "catalogs.read"` and `CatalogsManage = "catalogs.manage"`.
The `DevelopmentActor` grants both alongside `candidates.read`; production continues to fail
closed with no real actor, exactly as KTL-5 established. Each handler checks
`actor.IsAuthenticated && actor.HasPermission(...)` and throws `ForbiddenException`, matching
`GetReferenceCandidateHandler`. Both capabilities map onto the single existing frontend
`manage_catalogs` permission, which continues to gate only navigation and route access — the UI
is not the control.

### Auditing

Reuses the existing `AuditEvent` (`EventType`, `SubjectId`, `CorrelationId`, `OutcomeCode`).
`EventType` is `catalog.created` / `catalog.updated` / `catalog.reordered` /
`catalog.activation_changed`; `SubjectId` is the item identifier, or `{family}` for a reorder;
`CorrelationId` comes from `ICorrelationContext`. The audit row is written in the same
transaction as the change, so a rejected change cannot leave an applied-change event behind.
Reads are not audited — catalog values are not personal data.

### Referential protection, given candidates are not yet server-side

The spec's "values in use are protected" requirement is implemented now against server-side
candidate relations, which are empty until KTL-8. Rename-into-collision is enforced
unconditionally by the unique index, so that half of the rule is fully load-bearing today. The
"no physical delete" half is structural — the verb does not exist. Only the in-use lookup is
inert, and it becomes load-bearing when KTL-8 populates the relations.

Meanwhile the frontend keeps its existing local in-use guard as a transitional pre-check, so the
admin screens behave as users expect today. It is marked in code as removable at KTL-8. The
alternative — dropping the guard now — would silently change admin behavior in a way users
would read as a regression, for a rule the server cannot yet evaluate.

### Seeding

`DatabaseInitializer` gains `SeedCatalogsAsync`, invoked from the same startup path as the
existing reference seed. It inserts a family's defaults only when that family has no rows at
all, so administrator edits survive redeploys, and it is safe to re-run. The defaults are the
Spanish values of `DEFAULT_CATALOGS`, accents intact, in their declared order. Per-value
`IF NOT EXISTS` seeding was rejected: it would resurrect values an administrator deliberately
removed from a family.

### Least-privilege grants

The migration grants the runtime role `SELECT, INSERT, UPDATE` on `CAT_CatalogItems` and no
`DELETE`, so "no physical delete" is enforced by the database as well as by the absence of an
endpoint. The migration role retains DDL. This ships in the same migration as the table, per the
design rule.

### Frontend service shape

`CatalogService` keeps its `signal`-based surface so `useCatalogs` and the seven consuming
components keep their subscription pattern, but the signal now holds a load state:

```ts
type CatalogState = {
  status: 'idle' | 'loading' | 'loaded' | 'error';
  items: Partial<Record<CatalogFamily, CatalogItem[]>>;
  error?: AppError;
};
```

`list(family, includeInactive)` and `activeNames(family)` stay synchronous reads of that state
and return `[]` while loading, and the service exposes `status` and `error` so components can
render loading and failure states instead of an empty list. `useCatalogs` triggers the load on
first use; write methods await the API and then refetch the affected family. This keeps the
change to the seven consumers additive — they render a loading/error branch — rather than
requiring each to be rewritten around promises.

`remove()` is deleted; the admin page's delete control becomes the deactivate control.

## Risks / Trade-offs

- **Seven components must handle loading and error states, and one loses its delete action** →
  Confine the change to a loading/error branch per component and one relabelled control; cover
  each with a Vitest case, and prove the whole flow with the Playwright catalog spec.
- **`list()` returns `[]` while loading, which a careless consumer reads as "no values"** →
  The spec forbids presenting a loading collection as complete; `status` is exposed alongside
  the data and every consumer is required to branch on it, with tests asserting the loading
  branch renders.
- **Accent-insensitive normalization done in application code can drift from what users see** →
  Normalization is a single pure function with unit tests over the Spanish default values
  (`Inglés`/`ingles`, `Tecnología`, `Básico`), and the database index is on its stored output,
  so drift cannot produce two forms of truth.
- **The in-use rule is only partly enforceable until KTL-8** → Implemented now, documented as
  inert for the lookup half, with the frontend guard retained; KTL-8 removes the transitional
  guard and gains an integration test that a referenced value cannot be renamed into a
  collision.
- **Removing the `localStorage` fallback means an API outage empties every catalog dropdown** →
  Intentional: a silently divergent local vocabulary is worse than a visible failure. The error
  state is explicit and carries the Spanish message and correlation identifier.
- **Existing per-browser catalog edits are lost** → Values were per-browser and seeded from the
  same defaults; the deployment seed restores the shared baseline. Called out in the release
  note.

## Migration Plan

1. Deploy the migration (creates `CAT_CatalogItems`, its constraints and indexes, and the
   runtime grants). It is additive — no existing table is touched.
2. Startup runs `SeedCatalogsAsync`, populating the nine families.
3. Deploy the SPA with the rewritten `CatalogService`. Browsers stop reading `rrhh-catalogs`;
   the stale key is simply abandoned.
4. Verify the Playwright catalog flow against the deployed environment.

**Rollback**: revert the SPA. The previous build re-seeds its own defaults into `localStorage`
on first load, so the frontend is self-sufficient again. The `CAT_` tables can be left in place
(nothing else references them) and dropped separately if the change is abandoned.

## Open Questions

- Whether `NameEn` should eventually become required for an English UI is a product question
  that does not affect this slice's schema (the column is nullable either way) or its
  requirements.
