# KTL-19 — Give the audit trail an actor, personal-data reads, and a way to read it

**Status:** Proposed
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-16 (identity and access control — supplies the internal user id that an audit
row records), KTL-8 / KTL-9 / KTL-6 (the handlers that already write audit rows)

## Summary

This is workstream 4 of [KTL-16](./KTL-16.md), split out as its own ticket alongside
[KTL-17](./KTL-17.md) and [KTL-18](./KTL-18.md).

`AUD_Events` rows are written by the candidate, catalog and document handlers. A row records the
event type, the subject, the correlation id, the outcome and the timestamp — but **not who did it**,
and there is no way to read the table from the application at all.

An audit trail that cannot name the actor and cannot be read does not answer the question it exists
for. After a data-protection incident, "who looked at this candidate, and when" is the question, and
today the answer is nowhere: reads of personal data are not recorded either, only writes.

This ticket closes both halves — the actor and the read surface — and extends recording to the
personal-data reads that matter.

## Context — what exists today

| Concern         | Today                                                                      | Evidence                                |
| --------------- | -------------------------------------------------------------------------- | --------------------------------------- |
| Event shape     | Event type, subject, correlation id, outcome, timestamp                    | `backend/Domain/Auditing/AuditEvent.cs` |
| Actor           | Not recorded at all                                                        | same                                    |
| What is audited | Writes only: candidate, catalog and document changes                       | the respective handlers                 |
| Reads           | Not audited — a candidate detail read or a document download leaves no row | —                                       |
| Read surface    | None. No endpoint, no page, no query                                       | —                                       |
| Grants          | `ktl_runtime` privileges on `AUD_Events` need confirming and tightening    | `Infrastructure/Persistence/Migrations` |

## In scope

- **Add the actor to `AuditEvent`.** Store the **internal user id** from KTL-16 — never an email,
  never a display name, never the external subject. Backfill is not possible, so existing rows keep
  a null actor and the read surface says so explicitly rather than rendering a blank.
- **Record reads of personal data**, not only writes. Candidate detail reads, document downloads and
  exports. This is the half that makes an incident answerable.
- **`GET /api/audit`**, guarded by a new `audit.read` permission, filterable by date range, event
  type, actor and subject, and paged like search. It returns identifiers and codes only: resolving a
  subject to a candidate name stays the candidate endpoint's job, behind its own permission.
- **Admin › Auditoría page** listing the events, resolving the actor's display name through the
  users endpoint from KTL-16. Add it to `nav-items.ts` with its permission in `NAV_PERMISSIONS` —
  never as another `NavLink` in `app-layout.tsx`.
- **Append-only in the database.** `ktl_runtime` gets `SELECT` and `INSERT` on `AUD_Events` and no
  `UPDATE` and no `DELETE`. A migration ships the revocation and a test asserts it against the
  catalog.
- **`audit.read` is granted narrowly.** Decide which seeded roles get it; an audit trail readable by
  everyone it records is not much of a control.

## Out of scope

- Retention and anonymization of audit rows themselves — their own ticket, though the design should
  say what it expects.
- Alerting, anomaly detection or any analysis beyond listing and filtering.
- Exporting the audit trail.
- Changing what the existing write events record, beyond adding the actor.

## Decisions to make in the design

1. **How the actor reaches the event.** Ambiently from `ICurrentActor` at the point of writing, or
   passed explicitly through each command. Ambient is less code and easier to get wrong silently;
   explicit is noisier and harder to forget. Pick one and apply it everywhere.
2. **Which reads are recorded.** Candidate detail and document download are clear. Is a list or
   search page a personal-data read? Recording every search makes the trail enormous and its signal
   thin; recording none loses the "who browsed the database" case. State the rule and the reasoning.
3. **Event type vocabulary.** A closed catalogue in code, versioned the way permissions are, or free
   strings. A trail you cannot filter reliably is hard to use.
4. **Null actors.** How the read surface presents rows written before this ticket, and rows written
   by a system process rather than a person (the migrator, a purge job).
5. **Which roles hold `audit.read`.** And whether an actor can see rows naming themselves when they
   lack it.
6. **Volume.** Expected rows per day once reads are recorded, the indexes the filters need, and
   whether the table needs partitioning at that rate.

## Acceptance criteria

- Every write **and** every recorded personal-data read produces an `AUD_Events` row naming the
  actor, and the audit page shows it.
- An audit row contains no email, display name, external subject or candidate personal data —
  identifiers and codes only.
- `GET /api/audit` answers 401 unauthenticated and 403 without `audit.read`, before validation runs.
- Filtering by date range, event type, actor and subject returns the expected rows, and paging is
  deterministic.
- Rows predating this change are shown with an explicitly unknown actor, not a blank or a
  fabricated one.
- `ktl_runtime` holds no `UPDATE` and no `DELETE` on `AUD_Events`, asserted by querying the catalog.
- The Auditoría entry reaches the nav through `nav-items.ts` and `NAV_PERMISSIONS`, not through
  `app-layout.tsx`.
- `npm run build:all`, `npm test`, `npm run test:backend`, `npm run e2e`, `npm run lint`,
  `npm run format:check`, `npm run security:rls` and `npm run security:storage` all pass.

## Security evidence required

This ticket touches personal data, permissions and database grants:

- Per-endpoint integration tests for unauthenticated and unauthorized callers, refused before
  validation.
- A grants test showing `ktl_runtime` has no `UPDATE` and no `DELETE` on `AUD_Events`.
- A test proving an audit row carries the internal user id and none of the actor's personal data.
- A test proving a candidate detail read and a document download each produce a row naming the
  actor.
- A test proving the audit response resolves no subject to a candidate name.
- Log assertions: the audit path itself puts no personal data in the logs.

## Risks

- **A trail nobody can trust.** If some paths record the actor and others do not, the trail is worse
  than none because it looks complete. Decision 1 must be applied exhaustively, and a test should
  assert that every audited handler supplies an actor.
- **Volume.** Recording reads can multiply the row count by an order of magnitude. Size it in the
  design rather than discovering it in production.
- **Audit as surveillance.** An HR audit trail records what named employees looked at. Grant
  `audit.read` narrowly, say in the docs what the trail is for, and keep it out of reach of the
  people it would be misused against.
- **Append-only versus retention.** Revoking `DELETE` makes the eventual retention ticket harder,
  because purging will need the migrator role. That is the correct trade, but say so now.

## Documentation to update

- `docs/ktl-19/` — the audit contract: event types, the row shape, the filters, what is recorded on
  read, and who should hold `audit.read`.
- `docs/ktl-16/authentication-and-authorization.md` — add `audit.read` to the permission table.
- `README.md` (Spanish) if the Auditoría section needs explaining.
