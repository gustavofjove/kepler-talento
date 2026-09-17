# KTL-19 audit trail contract

The audit trail answers one question: **who did what to which record, and when**. This document
is the contract for what an audit row holds, which operations write one, and how the trail is
read. Design decisions are in `openspec/changes/ktl-19-audit-actor-and-read-surface/design.md`
(archived with the change).

## The event row (`AUD_Events`)

| Column          | Type                       | Meaning                                                                     |
| --------------- | -------------------------- | --------------------------------------------------------------------------- |
| `Id`            | `uuid`                     | Event id (uuid v7 for new rows).                                            |
| `EventType`     | `varchar(100)`             | One value from the closed catalogue below.                                  |
| `SubjectId`     | `varchar(100)`             | What the event is about, as identifiers only (see _Subjects_).              |
| `CorrelationId` | `varchar(100)`             | The request or background run that produced it.                             |
| `OutcomeCode`   | `varchar(100)`, nullable   | A code such as `served`, `applied`, `accepted` or a scanner result code.    |
| `CreatedAtUtc`  | `timestamp with time zone` | When the event was recorded.                                                |
| `ActorKind`     | `varchar(16)`, nullable    | `user`, `system`, or null on a row written before KTL-19.                   |
| `ActorUserId`   | `uuid`, nullable           | The acting user's **internal** id (`ADM_Users.Id`) when the kind is `user`. |

`CK_AUD_Events_Actor` holds the two actor columns together: both null (historic), `system` with no
user id, or `user` with one. There is deliberately no foreign key to `ADM_Users`: an audit row must
outlive anything that happens to the user table.

A row never carries a candidate field value, a document's filename, content or storage key, free
text from a caller, an email address, a display name, or the identity provider's subject.

> Rows written before KTL-19 may carry the caller's opaque correlation key in `OutcomeCode`
> (`applied|actor:<key>` for documents, the bare key for candidates and catalogs). They are left
> untouched — rewriting history is exactly what this change forbids — and are listed for the
> retention ticket in the [runbook](runbook.md).

## Actors

| Case      | Stored as                           | Written by                                                                          |
| --------- | ----------------------------------- | ----------------------------------------------------------------------------------- |
| `user`    | `ActorKind = 'user'`, `ActorUserId` | Every request-driven operation, from `ICurrentActor.UserId`.                        |
| `system`  | `ActorKind = 'system'`, no user id  | Background work with no person behind it: the malware scan, the storage reconciler. |
| `unknown` | both null                           | Never written. Only a pre-KTL-19 row reads back this way.                           |

The actor is a **required** constructor argument of `AuditEvent`, so an audited operation without
one does not compile. A request whose caller has no stored user — the synthetic development actor
without `DevelopmentActor:UserId`, for instance — is **refused** (`InvalidOperationException`, a 500) rather than recorded as the system or as nobody.

An import commit runs in a worker, so its `candidate.created` and `candidate.relations_changed`
events name the batch's uploader (`ImportBatch.CreatedByUserId`). A batch with no uploader id fails
with `import.actor.missing` instead of loading.

## Event-type catalogue

`AuditEventTypes.All` in `backend/Domain/Auditing/AuditEventTypes.cs`. An `AuditEvent` with any
other type throws before it can be stored.

| Type                          | Subject                        | Actor  | Outcome                     |
| ----------------------------- | ------------------------------ | ------ | --------------------------- |
| `candidate.created`           | candidate id                   | user   | —                           |
| `candidate.updated`           | candidate id                   | user   | —                           |
| `candidate.status_changed`    | candidate id                   | user   | —                           |
| `candidate.removed`           | candidate id                   | user   | —                           |
| `candidate.restored`          | candidate id                   | user   | —                           |
| `candidate.relations_changed` | candidate id                   | user   | —                           |
| `candidate.documents_changed` | candidate id                   | user   | —                           |
| `candidate.read`              | candidate id                   | user   | `served`                    |
| `catalog.created`             | catalog item id                | user   | —                           |
| `catalog.updated`             | catalog item id                | user   | —                           |
| `catalog.reordered`           | catalog family                 | user   | —                           |
| `catalog.activation_changed`  | catalog item id                | user   | —                           |
| `document.upload.accepted`    | `candidate:<id>;document:<id>` | user   | `accepted`                  |
| `document.scan`               | `candidate:<id>;document:<id>` | system | scanner code                |
| `document.downloaded`         | `candidate:<id>;document:<id>` | user   | `served`                    |
| `document.primary.changed`    | `candidate:<id>;document:<id>` | user   | `applied`                   |
| `document.removed`            | `candidate:<id>;document:<id>` | user   | `applied`                   |
| `document.reconciliation`     | document id or `orphan-<hash>` | system | `document.reconciliation.*` |

Ids are written in the compact 32-hex-digit form.

## Which reads are recorded, and which deliberately are not

**Recorded** — reads that name one person:

- `GET /api/candidates/{id}` — `candidate.read`, written by the query handler **after** the read
  succeeded, so a refused or not-found read records nothing.
- `GET /api/candidates/{id}/documents/{documentId}/content` — `document.downloaded`.

**Not recorded:**

- **Searching and listing candidates** (`POST /api/candidates/search`, `GET /api/candidates`). A
  page of results names whoever matched, and the reader may never have looked at any of them.
  Recording them would add roughly one row per page view, bury the reads that do identify someone,
  and change the table's growth by an order of magnitude. The lost case — "who browsed the
  database" — is real; adding it later is a separate decision that needs a partitioning plan.
- **Refused reads.** An unauthenticated flood would let an anonymous caller write unbounded rows
  into a table the application cannot prune. Recording refusals waits for the retention story; if
  added, a refusal must be distinguishable from a read that happened.
- Document metadata reads and catalog reads (catalog values are not personal data).

## Append-only

Migration `AddAuditActor` revokes `UPDATE`, `DELETE` and `TRUNCATE` on `AUD_Events` from
`ktl_runtime`. The API can only `SELECT` and `INSERT`. Write events share the transaction of the
change they describe, so a rolled-back change leaves no event.

## Read surface: `GET /api/audit/events`

Requires an authenticated caller holding `audit.read`, checked as an endpoint policy before the
query is bound or validated and again in the handler. No other permission implies it.

| Query       | Format                                                                             |
| ----------- | ---------------------------------------------------------------------------------- |
| `from`      | ISO 8601 date or date-time; a value without an offset is UTC. Inclusive.           |
| `to`        | As `from`. Inclusive. Must not be earlier than `from`.                             |
| `eventType` | One catalogue value, exact.                                                        |
| `actor`     | A user id, `system` or `unknown`.                                                  |
| `subject`   | An exact subject id. A candidate id also matches that candidate's document events. |
| `page`      | ≥ 1, default 1.                                                                    |
| `pageSize`  | 1–100, default 25.                                                                 |

Ordered by `CreatedAtUtc` descending, then `Id` descending, so paging over unchanged data neither
repeats nor skips an event.

```json
{
  "items": [
    {
      "id": "01995c1e-…",
      "eventType": "candidate.read",
      "subjectId": "01995b0a0c7a7b3e9a3c2f0d1e2f3a4b",
      "actorKind": "user",
      "actorUserId": "01932f00-…",
      "outcomeCode": "served",
      "createdAt": "2026-09-17T08:30:00+00:00"
    }
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 1
}
```

| Status | When                                                                                                                                                                                                               |
| ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 401    | No valid token — whatever the query.                                                                                                                                                                               |
| 403    | Authenticated without `audit.read` — whatever the query.                                                                                                                                                           |
| 400    | Validation problem with a stable code: `audit.eventType.unknown`, `audit.date.invalid`, `audit.dateRange.invalid`, `audit.actor.invalid`, `audit.subject.invalid`, `audit.page.invalid`, `audit.pageSize.invalid`. |

The response never resolves a subject to a candidate or an actor to a name. The Auditoría screen
resolves actor display names itself through `GET /api/admin/users`, which needs `users.manage`; a
reader without it sees the internal id.

Filter indexes: `(CreatedAtUtc DESC)`, `(EventType, CreatedAtUtc DESC)`,
`(ActorUserId, CreatedAtUtc DESC)`, `(SubjectId, CreatedAtUtc DESC)`.
