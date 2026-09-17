## Context

See `proposal.md` for motivation and `specs/` for the required behaviour. This design covers how the
actor reaches an audit row, which reads are recorded, and how the trail is read.

Current state:

- **`AuditEvent` has no actor.** `Domain/Auditing/AuditEvent.cs` is `(Id, EventType, SubjectId,
CorrelationId, CreatedAtUtc, OutcomeCode?)`. Its private parameterless constructor is for EF.
- **The remark already claims otherwise.** `Domain/Candidates/CandidateAuditEvents.cs` states that
  "an audit event carries the actor, the candidate identifier, one of these types and the request
  correlation identifier" and that "Reads are not audited; only changes are." The first half
  describes behaviour the code does not have.
- **The specs already require it too.** `candidate-management`, `business-catalogs` and
  `private-document-storage` each say the event carries "the acting identity", and
  `private-document-storage` explicitly requires a download to be audited. None of that is
  implemented. This change is partly a compliance fix, not only new work.
- **Five call sites.** `CandidateRepository.cs:169` and `:222`, `CatalogRepository.cs:47`,
  `DocumentRepository.cs:51`, and `DocumentStorageReconciler.cs:85` — the last has no person behind
  it at all.
- **Events are written through the repositories**, inside the same `SaveChanges` as the business
  change, via `dbContext.AuditEvents.Add(...)`. The transactional coupling the spec requires already
  exists.
- **The table is not append-only.** `20260825090844_InitialInfrastructure.cs:141` grants
  `SELECT, INSERT, UPDATE, DELETE` on `"CND_Candidates", "CND_Documents", "OPS_Operations",
"AUD_Events"` to `ktl_runtime`. `RevokeCandidateDelete` is the precedent for narrowing a grant.
- **Indexes.** `AuditEventConfiguration` indexes `CorrelationId` only — nothing the required filters
  (date range, event type, actor, subject) can use.

## Goals / Non-Goals

**Goals:**

- Make the actor structurally impossible to omit, rather than something each new audited operation
  must remember.
- Record the two reads that name a person, and be explicit about not recording the ones that do not.
- Make the trail unrewritable by the application, so that its value does not depend on the
  application being trustworthy.

**Non-Goals:**

- Retention or anonymization of audit rows. Named below as a follow-up with what this change expects
  of it, but not implemented.
- Alerting, anomaly detection or any analysis beyond listing and filtering.
- Exporting the trail. An export is a bulk extract of who-looked-at-what, and it needs its own
  thinking.
- Backfilling actors onto historic rows. It is not possible, and guessing is worse than a null.

## Decisions

### D1 — The actor is a required constructor parameter, not an ambient lookup

`AuditEvent`'s public constructor gains a required `AuditActor` parameter. It is not an optional
argument defaulting to null, and it is not read ambiently from `ICurrentActor` inside the repository.

Ambient would be less code and would look tidier at the five call sites. It is rejected because the
failure mode is silent: a future operation on a code path where no actor is in scope writes a null
actor and nobody finds out until someone needs the trail. A required parameter makes the same
mistake a compile error.

This is what the spec's _No audited operation may omit the actor_ requirement is enforcing, and it is
why that requirement exists as a requirement rather than as a note.

### D2 — `AuditActor` is a small value type with three cases

```
AuditActor.User(Guid userId)   →  stored as the id
AuditActor.System              →  stored as a reserved sentinel
AuditActor.Unknown             →  never written; only read back from historic rows
```

Two columns would work — a nullable `ActorUserId` plus an `ActorKind` — and that is what is stored.
What matters is that `Unknown` has **no constructor callable by writing code**: it is producible only
by materialising a pre-KTL-19 row. A null actor therefore means exactly one thing, which is what
makes the read surface's "unknown" label honest.

The alternative of a single nullable id, with null meaning both "system" and "historic", was
rejected for that reason: those are different claims and conflating them makes the trail lie about
the reconciler.

### D3 — Only candidate detail reads and document downloads are audited

Decided per the ticket's decision 2. A read is recorded when it **names an individual**: opening one
candidate's record, and downloading one document. A search or a list does not — it returns a page of
whoever matched, and the actor may never have looked at any of them.

The volume argument reinforces the principle rather than driving it. Recording every search and list
would add roughly one row per page view per user per day, dwarfing the write events and leaving the
trail dominated by noise at exactly the moment someone needs to read it.

What this loses is the "who browsed the database" case. That is a real loss and it is recorded here
rather than glossed: if it is needed later, it is a separate decision with a partitioning plan
attached, because it changes the table's growth profile by an order of magnitude.

### D4 — A refused read is not audited in this change

The spec requires only that a refusal not be recorded _as a read that happened_. This change records
nothing for a refused read.

The argument for recording refusals is real — repeated forbidden attempts are the signal an incident
looks like. The argument against, for now, is that an unauthenticated flood would let an anonymous
caller write unbounded rows into an append-only table that the application cannot prune, which is a
denial-of-service against the audit trail itself. That needs the retention story (D8) to exist first.

So: not now, deliberately, with the reason recorded. The spec is written to permit adding it later
without a spec change, provided it is distinguishable from a successful read.

### D5 — The read hook sits in the handler, not in the repository

Write events are added by the repositories, because that is where the business change and its event
share a transaction. A read has no write transaction to join, so its event is written by the query
handler after the read succeeds — and only after, so a read that returned nothing or was refused
records nothing.

This means a read audit is a second round trip. That is acceptable for a detail read and a download;
it is another reason searches are not audited (D3), where it would be a second round trip on the
hottest path in the product.

### D6 — Event types become a closed catalogue, assembled from the per-feature classes

`CandidateAuditEvents`, `CatalogAuditEvents` and the document equivalents stay where they are — they
belong with their features. A new `AuditEventTypes.All` aggregates them, and the `AuditEvent`
constructor validates against it.

The filter then has an exhaustive list to offer, and an event type that nobody registered cannot be
written. Free strings would make "filter by event type" a substring guess that silently misses rows,
which for an audit filter is the worst kind of wrong.

`CandidateAuditEvents` gains `Read = "candidate.read"`, and its remark is corrected: it currently
describes an actor the code does not carry and states that reads are not audited, both of which stop
being true here.

### D7 — Indexes for the filters, and the revocation, ship in the migration

`AddAuditActor` adds the two actor columns, adds indexes supporting the required filters —
`(CreatedAtUtc DESC)`, `(EventType, CreatedAtUtc DESC)`, `(ActorUserId, CreatedAtUtc DESC)`,
`(SubjectId, CreatedAtUtc DESC)` — and revokes `UPDATE, DELETE` on `AUD_Events` from `ktl_runtime`,
following `RevokeCandidateDelete`.

The revocation and the column ship together on purpose. Splitting them leaves a window in which the
trail carries actors and is still rewritable, which is the worst of both: it looks authoritative and
is not.

Paging follows the search convention — ordered by `CreatedAtUtc` descending with the event id as
tie-breaker, default 25, maximum 100.

### D8 — Revoking `DELETE` makes retention a migrator-role job, and that is stated now

Once `ktl_runtime` cannot delete, the eventual retention ticket cannot purge old audit rows through
the application. It will need `ktl_migrator` and a deliberate operational action.

That is the correct trade — an application that can delete its own audit trail has an audit trail
only as long as it is not compromised — but it is a constraint this change imposes on a later one,
so it belongs in the docs now rather than being discovered then. The runbook says what the retention
ticket will have to do.

### D9 — The display name is resolved client-side, per page, from the users endpoint

The event carries an internal user id. The Auditoría page collects the distinct actor ids on the page
it is showing and resolves them in one call to the users endpoint from KTL-16, behind `users.manage`.

Resolving server-side would mean the audit response carries display names — putting an actor's
personal data into every audit payload and into anything that caches it. Keeping the join in the
client keeps the audit contract to identifiers and codes, which is what the spec requires.

A reader who holds `audit.read` but not `users.manage` sees the internal id rather than a name. That
is a legible degradation and it is the honest one: they are not entitled to the user directory.

## Risks / Trade-offs

- **A half-complete trail.** If some audited operations name the actor and others do not, the trail
  presents itself as whole while lying. → D1 makes it a compile error, and a test enumerates the
  audited operations and asserts each supplies an actor.
- **The trail is surveillance data.** It records what named employees looked at, in an HR system. →
  `audit.read` is granted only to the roles documented as governing the installation, never to the
  recruiter roles; the docs state what the trail is for; and D9 keeps actor names out of the payload.
- **Audit-write failures could block business writes.** Sharing a transaction is required, so a
  failure to write the event fails the change. → That is the intended direction: a change that cannot
  be audited should not happen. It is called out so nobody "fixes" it later by catching and
  swallowing.
- **Read auditing adds a write to every detail open.** → One insert per detail read, on a path that
  already does a multi-table aggregate load. D3 keeps it off the search path where it would matter.
- **Volume once reads are recorded.** Detail reads and downloads are a moderate multiplier, not an
  order of magnitude. → Sized in the docs; the indexes in D7 are chosen for the filters rather than
  added afterwards. If searches are ever added (D3), partitioning becomes a prerequisite.
- **No retention story yet (D8).** The table grows without bound and cannot be pruned by the
  application. → Explicit follow-up ticket, with the constraint documented now.
- **Changing `AuditEvent`'s constructor touches five call sites and the EF model.** → Small and
  compile-checked; the migration is additive on a table nothing currently reads.

## Migration Plan

1. `AddAuditActor` migration: add the actor columns nullable (historic rows keep null), add the four
   filter indexes, revoke `UPDATE, DELETE` on `AUD_Events` from `ktl_runtime`. Runs through the
   existing `--migrate` entry point as `ktl_migrator`.
2. Deploy the API: the constructor change, the five call sites, the read hooks, the closed catalogue
   and `GET /api/audit`. From this point every new row names an actor.
3. Deploy the SPA with the Auditoría screen and the navigation entry.
4. Verify: perform a write, a detail read and a download; confirm three rows with the correct actor;
   confirm a pre-existing row still renders as unknown; confirm `ktl_runtime` cannot update or delete.

**Rollback.** The SPA rolls back independently — the audit screen simply disappears. The API rolls
back by redeploying the previous image, which writes rows without actors again; those rows are
indistinguishable from historic ones, which is a real (small) loss of fidelity and the reason the
API rollback window should be short. `Down` drops the columns and the indexes and **restores the
`UPDATE, DELETE` grant**, so a rollback genuinely returns the previous state rather than leaving the
old code unable to write.

## Open Questions

- Which seeded roles hold `audit.read`. `system_admin` is the obvious one; whether `rrhh_admin` also
  holds it is an installation-governance question rather than a technical one. It is a value in the
  seed data, changes no schema, no endpoint and no task, and an administrator can adjust it through
  the roles screen afterwards.
- The retention window for audit rows, and whether the eventual retention ticket anonymizes the actor
  or removes the row. Out of scope here by design (D8); recorded so the follow-up ticket starts from
  the constraint rather than rediscovering it.
