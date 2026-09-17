# KTL-19 audit trail runbook

## What the trail is for

The audit trail records which employee changed or opened which candidate record, and when. It
exists to answer data-protection questions — a subject access request, a suspected misuse, an
incident review. It is **not** a productivity report and must not be used to monitor how much
individual recruiters work. It records what named employees looked at in an HR system, which makes
it surveillance data in its own right.

The event contract is in [audit-contract.md](audit-contract.md).

## Who should hold `audit.read`

- Seeded on **`system_admin` only**. `rrhh_admin`, `rrhh_user`, `manager_reader` and `readonly` do
  not hold it, and holding every candidate, catalog and document permission does not imply it.
- Grant it to the people who govern the installation: the data protection contact and the
  technical administrators who handle incidents. A trail readable by everyone it records is not a
  control.
- Change it through _Administración → Roles_; every grant change is immediate for the next request.
- A reader who also holds `users.manage` sees actor display names. Without it the screen shows the
  internal user id, which can be resolved by someone who does hold it.

## Deploy

1. Back up PostgreSQL (KTL-5 operator runbook).
2. Run the migrator as `ktl_migrator`. `AddAuditActor` adds `ActorKind` and `ActorUserId`
   (nullable), four filter indexes and `CK_AUD_Events_Actor`; revokes `UPDATE`, `DELETE` and
   `TRUNCATE` on `AUD_Events` from `ktl_runtime`; and adds `audit.read` to `system_admin`.
3. Deploy the API, then the SPA.
4. Verify:
   - Change a candidate, open one candidate's detail and download a document. Three new rows carry
     `ActorKind = 'user'` and your user id.
   - A row that existed before the deploy reads back in _Auditoría_ as _Actor desconocido_.
   - As `ktl_runtime`:
     ```sql
     SELECT has_table_privilege('ktl_runtime', '"AUD_Events"', 'UPDATE'),
            has_table_privilege('ktl_runtime', '"AUD_Events"', 'DELETE');
     ```
     Both must be `false`.

### Development and tests

The synthetic development actor has no stored user, so every audited operation refuses it unless
`DevelopmentActor:UserId` names one. Compose does not use it (it signs in through the development
token issuer), so the local stack is unaffected.

## Expected growth

Every candidate, catalog and document write already produced a row. KTL-19 adds one row per
candidate detail opened and one per document downloaded. Searches and list views add nothing. As a
sizing rule, expect roughly _(detail opens + downloads + writes) per user per working day_; for
tens of users that is thousands of rows a day and low millions a year. The four filter indexes are
sized for that; partitioning is not needed at this volume. If searches are ever audited, it is.

## Rolling back

- The SPA rolls back on its own; the Auditoría entry disappears.
- Rolling back the API makes new rows actorless again, indistinguishable from historic ones. Keep
  that window short.
- The migration's `Down` drops the columns, indexes and constraint, removes `audit.read` from every
  role, and **restores `UPDATE, DELETE` on `AUD_Events` to `ktl_runtime`**, because the previous API
  was written against that grant. Rolling back the schema therefore also gives up the append-only
  guarantee.

## What the retention ticket will have to do

KTL-19 does not implement retention. It constrains how retention can be implemented:

1. **The application can no longer delete audit rows.** `ktl_runtime` has neither `DELETE` nor
   `TRUNCATE`. Purging or anonymizing old rows is a `ktl_migrator` job — a scheduled operator
   action or a dedicated maintenance entry point — never an API endpoint or a background worker
   running as the runtime role. Do not re-grant `DELETE` to the runtime role to make it easier.
2. **Decide delete versus anonymize.** Anonymizing means nulling `ActorUserId` — but a null actor
   already means "historic, unknown", so an anonymized row needs its own distinguishable marker
   (and `CK_AUD_Events_Actor` needs to allow it) rather than masquerading as a pre-KTL-19 row.
3. **Pre-KTL-19 rows** may carry the caller's opaque correlation key in `OutcomeCode`. The retention
   job should clear or remove those along with the rest of the expired history.
4. **Recording refused reads** (deliberately not done here) should wait for retention to exist, so
   an anonymous flood cannot grow an append-only table without bound.
5. **Document the window** in this runbook and in the data-protection record, and prove by test
   that the purge runs as `ktl_migrator` and that `ktl_runtime` still holds no `DELETE`.
