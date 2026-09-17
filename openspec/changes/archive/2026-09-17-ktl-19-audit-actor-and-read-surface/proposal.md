## Why

`AuditEvent` records an event type, a subject, a correlation id, an outcome and a timestamp — and
not who acted. The remark on `CandidateAuditEvents` already claims "an audit event carries the
actor, the candidate identifier, one of these types and the request correlation identifier", which
is what the design intended and not what the code does.

Nothing reads the table either: there is no endpoint, no query and no screen. And only writes are
recorded, so opening a candidate's record or downloading their CV leaves no trace at all.

An audit trail that cannot name the actor and cannot be read does not answer the question it exists
for. After a data-protection incident the question is "who saw this candidate, and when", and today
the answer is nowhere (brief: `openspec/KTL-19.md`).

## What Changes

- **BREAKING (data)** `AuditEvent` gains an actor: the **internal user id** from KTL-16. Never an
  email, never a display name, never the external subject.
- **Backfill is impossible**, so rows written before this change keep a null actor. The read surface
  says so explicitly rather than rendering a blank that reads like "nobody".
- **Reads of personal data are recorded**, not only writes: opening a candidate's detail and
  downloading a document. Searches and list pages are **not** recorded — they name no one
  individually, and recording them would multiply the trail by orders of magnitude while thinning
  its signal.
- **New `audit.read` permission** and `GET /api/audit`: filterable by date range, event type, actor
  and subject, paged like search. It returns identifiers and codes. Resolving a subject to a
  candidate's name stays the candidate endpoint's job, behind its own permission.
- **New Admin › Auditoría page** listing events, resolving the actor's display name through the users
  endpoint from KTL-16. Added to `nav-items.ts` with its permission in `NAV_PERMISSIONS` — never as
  another `NavLink` in `app-layout.tsx`.
- **BREAKING (database)** `AUD_Events` becomes append-only for the application: `ktl_runtime` loses
  `UPDATE` and `DELETE` on it. It holds all four today.
- **A system actor is named.** Rows written by a process rather than a person — the reconciler, a
  purge — record a system actor rather than a null one, so that null means only "written before
  KTL-19".
- The event type vocabulary becomes a closed catalogue in code, so the filter can be trusted.
- `CandidateAuditEvents`' remark is corrected: it currently describes behaviour the code does not
  have, and after this change it will describe behaviour the code does have, including that reads
  are now audited.

**Actors:**

- Data-protection officers and administrators holding `audit.read`, who answer "who saw this".
- Every actor whose reads and writes are recorded — including the people reading the trail.
- Unauthenticated and unauthorized callers, refused before validation.
- System processes, which act without a person and are recorded as such.

**Key entities:**

- **Audit event** — id, event type, subject id, actor (internal user id, or system, or unknown for
  pre-KTL-19 rows), correlation id, outcome code, timestamp.
- **Event type catalogue** — a closed set of `<resource>.<verb>` strings defined in code.
- **`audit.read`** — the permission governing the read surface.

**Assumptions:**

- KTL-16 has shipped, so an internal user id exists and `ICurrentActor` carries it.
- A candidate detail read and a document download are the personal-data reads worth recording; this
  is a decision, recorded in the design with its reasoning.
- Audit rows are not personal data about the _candidate_ — they carry identifiers and codes — but
  they are personal data about the _actor_, and are treated as such.
- Retention and anonymization of audit rows themselves is a later ticket; this change says what it
  expects rather than implementing it.

**Edge cases:**

- A row written before this change, with no actor.
- A row written by a system process with no person behind it.
- An actor whose user record was later deactivated, or whose display name changed.
- An audit event whose subject is a candidate that has since been removed.
- A read that fails authorization — is the attempt recorded?
- A read that succeeds but returns nothing.
- A filter naming an actor who has no events.
- Someone holding `audit.read` reading their own events.
- A write that fails partway: the business change rolls back, so the audit row must too.

**Success criteria:**

- Every write **and** every recorded personal-data read produces an `AUD_Events` row naming the
  actor, proven by a test per audited operation.
- A test asserts that every audited handler supplies an actor — so the trail cannot be half-complete,
  which is worse than absent because it looks whole.
- An audit row contains no email, display name, external subject or candidate personal data.
- `GET /api/audit` answers 401 unauthenticated and 403 without `audit.read`, before validation runs.
- Filtering by date range, event type, actor and subject returns the expected rows, and paging is
  deterministic.
- Pre-KTL-19 rows render as an explicitly unknown actor, distinct from the system actor.
- `ktl_runtime` holds no `UPDATE` and no `DELETE` on `AUD_Events`, asserted by querying the catalog.
- The Auditoría entry reaches the nav through `nav-items.ts` and `NAV_PERMISSIONS`.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Capabilities

### New Capabilities

- `audit-trail`: what an audit event records, which operations produce one, how the actor is
  attached, the append-only guarantee, the read surface and its authorization, and the Auditoría
  screen.

### Modified Capabilities

- `candidate-management`: the candidate auditing requirement gains the actor and extends to detail
  reads, which are currently explicitly not audited.
- `private-document-storage`: the document lifecycle auditing requirement gains the actor and extends
  to downloads.
- `business-catalogs`: the catalog auditing requirement gains the actor.
- `primary-navigation`: the `Auditoría` entry joins the Admin group, governed by `audit.read`.

`identity-and-access-control` gains the `audit.read` permission, but no requirement of that
capability changes: its catalogue is defined as closed and code-owned, and adding a permission that
guards a real operation is exactly what it already permits. No delta is needed, so none is written.

**Note on existing compliance.** `candidate-management`, `business-catalogs` and
`private-document-storage` already require the audit event to carry "the acting identity", and
`private-document-storage` already requires a download to be audited. The implementation does
neither. Those specs are therefore modified here not to add the idea but to pin down _what_ the
acting identity is — an internal user id, never an email or display name — and this change brings
the code into compliance with requirements it has been failing since it was written.

## Impact

- **Backend:**
  - `Domain/Auditing/AuditEvent.cs` gains the actor and a closed event-type catalogue; its
    constructor changes, which is why every call site is touched.
  - Call sites: `CandidateRepository.cs:169,222`, `CatalogRepository.cs:47`, `DocumentRepository.cs:51`,
    `DocumentStorageReconciler.cs:85` (the system actor).
  - `Domain/Candidates/CandidateAuditEvents.cs` — new read event type, and its remark corrected.
  - New `Application/Features/Audit/` — the paged query, its validator and its handler behind
    `AuditGuards.RequireRead`.
  - New read-audit hooks on the candidate detail read and the document download.
  - `Infrastructure/Persistence`: an `AuditEventConfiguration` gaining the actor column and its
    indexes, and a migration adding the column, adding the filter indexes and **revoking** `UPDATE`
    and `DELETE` from `ktl_runtime` on `AUD_Events`.
  - `Web/Features/Audit/AuditEndpoints.cs` and its registration in `Program.cs`.
  - `Permissions` gains `AuditRead`.
- **Frontend:**
  - New `features/admin/audit/` — service, list page, filters.
  - `nav-items.ts` and `NAV_PERMISSIONS` gain the entry and its permission; `PrimaryNav` calls
    `usePermission` for it at the top.
  - `auth.models.ts` gains `audit.read`; the seeded roles that hold it are updated.
  - `es.json` gains `admin.audit.*`.
- **Tests:** unit tests per audited operation asserting the actor; a test asserting no audited handler
  omits it; integration tests for the read surface's authorization, filters and paging; a grants test;
  Vitest specs for the service and page; a Playwright audit journey.
- **Docs:** new `docs/ktl-19/`; `docs/ktl-16/authentication-and-authorization.md` gains `audit.read`.
- **Dependencies:** none added or removed.
- **Personal data, storage and roles:**
  - This change touches personal data in two directions. Audit rows carry **no candidate** personal
    data — identifiers and codes only — but they are personal data **about the actor**: a record of
    what a named employee looked at.
  - Principle 1 is upheld as follows: the actor is stored as an internal id, never an email, display
    name or external subject; the subject is an identifier that only the candidate endpoint can
    resolve to a name, behind its own permission; no field value is ever recorded; the read surface
    is paged and filtered, not exportable; the trail's own retention is named as a follow-up rather
    than left unconsidered.
  - Principle 3 is upheld as follows: the read endpoint checks authentication and `audit.read` before
    validating; `ktl_runtime` loses `UPDATE` and `DELETE` so the application cannot rewrite history;
    the revocation ships in the same migration as the column.
  - Role definitions change: a new permission is added and granted narrowly — an audit trail readable
    by everyone it records is not much of a control.
  - No RLS policy and no storage access changes.
