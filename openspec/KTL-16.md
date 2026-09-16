# KTL-16 — Finish the platform foundations: identity, access control, audit and server-side data paths

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-5 (runtime, actor abstraction), KTL-8 (candidate writes), KTL-10 (paged
search), KTL-14 (shared presets and `presets.manage`)

## Summary

Candidates, catalogs, documents and search now live behind the API. The pieces underneath
them do not. The browser still decides who the user is and what they may do, users and
roles live in `localStorage`, the import path never reaches the server, the candidate list
downloads every candidate to sort them, and the audit trail records what happened without
recording who did it.

This ticket closes those gaps so that every later slice — Positions (KTL-15) included —
can rely on a real caller identity, a real permission set and a usable audit trail.

It is the largest remaining structural work in the product and it is sequenced in four
workstreams. Workstream 1 blocks the other three.

**Split (2026-09-16).** Under decision 5 below, workstreams 2, 3 and 4 have been given their
own tickets. They are independent of each other, share almost no code, and keeping them here
would have produced a change folder that cannot be verified in one pass — the first risk this
ticket lists.

| Workstream                       | Ticket                   | Status                                      |
| -------------------------------- | ------------------------ | ------------------------------------------- |
| 1 — Identity and access control  | **KTL-16 (this ticket)** | change `ktl-16-identity-and-access-control` |
| 2 — Import on the server         | [KTL-17](./KTL-17.md)    | proposed, blocked by KTL-16                 |
| 3 — Candidate list on the server | [KTL-18](./KTL-18.md)    | proposed, blocked by KTL-16                 |
| 4 — Audit trail with an actor    | [KTL-19](./KTL-19.md)    | proposed, blocked by KTL-16                 |

Sections "Workstream 2", "Workstream 3" and "Workstream 4" below are kept as the record of
what was scoped here originally. The three tickets above supersede them; read those instead.
The acceptance criteria and security evidence in this ticket that belong to those workstreams
move with them.

## Context — what exists today

| Concern             | Today                                                                                                                      | Evidence                                                                   |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Authentication      | `AuthService.signIn` accepts any email and password, builds a `UserProfile` in the browser and stores it in `localStorage` | `src/app/core/auth/auth.service.ts`                                        |
| Caller identity     | The API has no identity at all. `ICurrentActor` is only ever `DevelopmentActor`, and `ApiTransport` sends no credentials   | `backend/Web/Program.cs:74`, `src/app/core/http/api-transport.ts`          |
| MFA                 | A client-side service and page, with no server-side enforcement                                                            | `src/app/core/auth/mfa.service.ts`                                         |
| Users               | `localStorage` key `rrhh-admin-users`, created and edited synchronously in the browser                                     | `src/app/features/admin/users/profile.service.ts`                          |
| Roles               | `localStorage` key `rrhh-admin-roles`, seeded from `DEFAULT_ROLES`                                                         | `src/app/features/admin/roles/role.service.ts`                             |
| Permission names    | Two disjoint vocabularies: `view_candidates` in the browser, `candidates.read` on the server, with no mapping between them | `src/app/shared/models/auth.models.ts`, `Application/.../ICurrentActor.cs` |
| Permission coverage | Six frontend permissions have no server-side counterpart at all                                                            | see [Permission mapping](#permission-mapping)                              |
| Import              | CSV validated and committed entirely in the browser; batches in `localStorage` key `rrhh.import.batches.v1`                | `src/app/features/admin/import/import.service.ts`                          |
| Candidate list      | `GET /candidates?includeInactive=` returns every candidate; sorting and filtering happen in the browser                    | `candidate.api.ts`, `candidate-list-page.tsx`                              |
| Audit               | `AUD_Events` rows are written by candidate, catalog and document handlers — with no actor, and with no way to read them    | `backend/Domain/Auditing/AuditEvent.cs`                                    |

The consequence is that non-negotiable 3 ("fail closed") holds only in form. Handlers do
call `ICurrentActor` before dispatching, but in a deployment the actor is either the
development actor or unauthenticated, so the guards have nothing real to check.

## Workstream 1 — Identity and access control on the API

The blocking piece. Everything else in this ticket depends on the API knowing who is
calling.

### In scope

- **Authenticate against the corporate identity provider.** Add OIDC / JWT bearer
  authentication (Microsoft Entra ID is the expected provider; confirm before designing)
  so that `ICurrentActor` is built from a validated token instead of configuration. MFA
  becomes the identity provider's responsibility.
- **Persist users and roles** in `ADM_` tables: a user row keyed by the provider's stable
  subject claim, a role, an active flag, a version, and audit timestamps. Roles carry a
  name, a label, a system flag and a permission set. Provision a user on first successful
  sign-in, or from an administrator-created invitation — pick one in the design.
- **One permission vocabulary.** Settle on the server-side `<resource>.<action>` form and
  make the frontend `Permission` type use it, or define an explicit, tested mapping. Two
  vocabularies that drift silently is worse than either one.
- **Fill the permission gaps.** Every permission the UI offers needs a server-side guard
  on a real endpoint, or it must be removed from the UI.
- **Endpoints** under `Web/Features/Admin`: list, create, update and deactivate users;
  list, create, update and deactivate roles; set a role's permissions; and a `GET
/api/me` returning the caller's display name, role and effective permissions. All of
  them guarded by `users.manage` / `roles.manage`, all with optimistic concurrency.
- **Frontend cutover.** `AuthService` reads the session from the identity provider and the
  profile from `GET /api/me`; `ProfileService` and `RoleService` become API gateways
  following the `CandidateApi` shape; the `localStorage` keys are evicted through
  `evict-legacy-storage.ts`; `supabase-client.service.ts` and `mfa.service.ts` are removed
  if nothing else uses them.
- **Keep `DevelopmentActor` for local work only.** It stays impossible to enable in
  Production, and it must not be the fallback when a token is absent or invalid — an
  unauthenticated caller gets 401, never a permissive actor.

### Invariants

- No physical deletes: users and roles are deactivated (non-negotiable 5).
- System roles cannot be renamed, have their permissions emptied, or be deactivated.
- The last active user holding `users.manage` cannot lose it or be deactivated. The
  browser-side version of this rule in `profile.service.ts` moves to the server.
- A user cannot change their own role or deactivate themselves.
- A permission change takes effect on the next request, without requiring a new sign-in;
  if permissions are carried in the token instead, the design says how revocation
  propagates and how fast.

### Permission mapping

The design must produce this table filled in. Gaps are decisions, not omissions.

| Frontend permission            | Server-side guard    | State                                         |
| ------------------------------ | -------------------- | --------------------------------------------- |
| `view_candidates`              | `candidates.read`    | exists                                        |
| `create_candidates`            | `candidates.create`  | exists                                        |
| `edit_candidates`              | `candidates.update`  | exists                                        |
| `delete_candidates`            | `candidates.delete`  | exists (governs deactivation and restoration) |
| `download_candidate_documents` | `documents.download` | exists                                        |
| `upload_candidate_documents`   | `documents.upload`   | exists                                        |
| `manage_catalogs`              | `catalogs.manage`    | exists                                        |
| `manage_presets`               | `presets.manage`     | exists (KTL-14)                               |
| `view_all_candidates`          | —                    | inert by KTL-10 decision: keep inert, or drop |
| `export_candidates`            | —                    | **no server-side export exists** (see below)  |
| `import_candidates`            | —                    | workstream 2                                  |
| `manage_users`                 | —                    | workstream 1                                  |
| `manage_roles`                 | —                    | workstream 1                                  |

`export_candidates` currently guards nothing anywhere: there is no export code path in
`src/app/features/**`. Either this ticket removes the permission, or the export feature
gets its own ticket and the permission waits for it. Do not leave a permission in the UI
that grants nothing.

## Workstream 2 — Import on the server

### In scope

- Upload a CSV or Excel file to the API; validate it as a **dry run**; return a per-row
  outcome report; commit the validated batch as a second, explicit step. The two-step
  shape the current page already uses stays; only the execution moves.
- Run validation and commit as **durable operations** (`OPS_`), so a restart does not lose
  a batch and a commit is idempotent and re-runnable, as `ktl-migrate` already is.
- Reuse the `Tools/DataMigration` row-outcome and reference-resolution semantics —
  reference values are resolved, never silently created. Do not reference `Tools/` from a
  production project; extract the shared rules into `Application` if needed.
- The uploaded file is personal data: it goes to private storage under an opaque key, is
  scanned before parsing, and is purged on a documented schedule after the batch closes.
- Guard every endpoint with `candidates.import`. Store batch history in `ADM_` or `OPS_`
  tables instead of `localStorage`, and show it on the Importación page.
- The row report shows outcomes and error codes, never candidate personal data in logs.

### Out of scope

Parsing CVs, and any import format beyond the documented CSV/Excel contract.

## Workstream 3 — Candidate list on the server

### In scope

- The candidate list moves to the paged search endpoint from KTL-10: filtering, sorting
  and paging happen in PostgreSQL, the response carries the documented page envelope, and
  the default and maximum page sizes match the search contract (25 / 100).
- The list projection stays minimal — the same rule search follows. The list must not
  return the full aggregate.
- Sorting fields become part of the contract and are validated server-side; an unknown
  sort field is refused with a stable validation code.
- `candidate-list-page.tsx` and `candidate.service.ts` drop their in-browser sort and
  filter; the URL carries page, sort and filter state so a list view can be shared.
- `includeInactive` stays available and stays guarded.

### Out of scope

Reworking the search criteria model or the shared criteria form (KTL-14 territory).

## Workstream 4 — Audit trail with an actor, and a way to read it

### In scope

- **Add the actor to `AuditEvent`.** Today a row records event type, subject, correlation
  id, outcome and timestamp — but not who acted. Store the internal user id from
  workstream 1, never an email or a display name. Backfill is not possible; existing rows
  keep a null actor and the read surface says so.
- **Record reads of personal data**, not only writes: candidate detail reads, document
  downloads and exports. This is what makes a data-protection incident answerable.
- **`GET /api/audit`**, guarded by `audit.read`, filterable by date range, event type,
  actor and subject, paged like search. It returns identifiers and codes; resolving a
  subject to a candidate name stays the candidate endpoint's job, behind its own
  permission.
- **Admin › Auditoría** page listing the events, with the actor's display name resolved
  through the users endpoint. Add it to `nav-items.ts` with its permission in
  `NAV_PERMISSIONS`; never as another `NavLink` in `app-layout.tsx`.
- Audit rows are append-only: no `UPDATE` or `DELETE` grant for `ktl_runtime` on
  `AUD_Events`.

## Out of scope for the whole ticket

- Positions (KTL-15) and anything in the dashboard proposal beyond what these workstreams
  require.
- Row-level or ownership-based visibility of candidates. `view_all_candidates` stays inert
  unless workstream 1 decides to drop it.
- Candidate export, CV parsing, retention and anonymization workflows — each gets its own
  ticket.
- Changing the nine catalog families or the search semantics.

## Decisions to make in the design

1. **Identity provider and token shape.** Entra ID? Which claim is the stable subject?
   Are permissions resolved from the database on each request, or carried in the token —
   and if carried, how is a revoked permission propagated?
2. **User provisioning.** Just-in-time on first sign-in, or administrator-created
   invitations only. JIT is friendlier; invitations keep the user list closed.
3. **Are roles a fixed set or freely editable?** The current UI allows creating roles,
   which makes the permission set data rather than code. Decide before the schema.
4. **Development and e2e story.** Playwright and the Testcontainers integration tests need
   a deterministic actor that is not the production path and cannot be enabled in
   Production. Extending `DevelopmentActor` with a permission set is the obvious route;
   confirm it and write the guard test.
5. **Sequencing.** Workstream 1 ships first. 2, 3 and 4 are independent of each other and
   can ship in any order, or as their own tickets if this one proves too large to track.
6. **Permission vocabulary.** Rename the frontend to `<resource>.<action>`, or keep both
   with an explicit map. State which, once.

## Acceptance criteria

- An unauthenticated request to every business endpoint answers 401; an authenticated
  request lacking the specific permission answers 403 — both **before** validation runs.
- No production code path builds a caller identity from anything the browser sent other
  than a validated token.
- `ALL_PERMISSIONS` contains no permission that fails to guard a real server-side
  operation.
- The `localStorage` keys `rrhh-demo-profile`, `rrhh-admin-users`, `rrhh-admin-roles` and
  `rrhh.import.batches.v1` are gone, evicted on upgrade, and no new `localStorage` or
  Supabase data path is introduced.
- Importing a CSV of N rows produces the same outcome whether it is committed once or
  re-run, and a restart mid-commit leaves no partial batch.
- The candidate list issues one paged request; the response contains no relation
  collections, notes, consent or retention data.
- Every write and every personal-data read produces an `AUD_Events` row naming the actor,
  and the audit page shows it.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`,
  `npm run lint`, `npm run format:check`, `npm run security:rls` and
  `npm run security:storage` all pass.

## Security evidence required

This ticket touches identity, permissions, grants and personal data, so it is not done
without tests that fail closed:

- Integration tests per endpoint for: no token, expired token, invalid signature, valid
  token without the permission, deactivated user with a still-valid token.
- A test proving `DevelopmentActor` cannot be enabled when the environment is Production,
  and that an absent token never falls back to it.
- Tests for the invariants: last administrator, self-role change, self-deactivation,
  system-role edits.
- A grants test showing `ktl_runtime` has no `DELETE` on the new `ADM_` tables and no
  `UPDATE`/`DELETE` on `AUD_Events`.
- Log assertions: no email, display name, token or candidate personal data reaches the
  logs through the new paths.

## Risks

- **Scope.** Four workstreams in one ticket is a lot. Split at the first sign of a change
  folder that cannot be verified in one pass.
- **Lockout.** A permission model change can lock every user out of administration. The
  migration must seed at least one active administrator, and the runbook must document
  how to recover one against the database.
- **Identity provider dependency.** If the corporate provider is not available for
  development, workstream 1 needs a documented local alternative that is still a real
  token flow, not a bypass.
- **Existing data.** Users and roles currently live in each browser's `localStorage`.
  There is no central copy to migrate, so the seeded set is the source of truth and any
  local role customisation is lost. Say so in the release notes.

## Documentation to update

- `docs/ktl-16/` — authentication and authorization contract, the permission table, the
  import contract, and a runbook covering administrator recovery and rollback.
- `README.md` (Spanish) — how to sign in for local development.
- `AGENTS.md` — remove the "remaining Supabase / `localStorage` paths are legacy" note for
  every path this ticket closes.
