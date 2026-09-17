## 0. Create Feature Branch

- [x] 0.1 Create and switch to branch `feat/KTL-19` from an up-to-date `main`, after KTL-16 has
      merged — this change stores the internal user id that KTL-16 introduces

## 1. The actor on the audit event

Covers: Every audit event names its actor; No audited operation may omit the actor. Design D1, D2.

- [x] 1.1 Add `Domain/Auditing/AuditActor.cs` with the three cases from design D2: `User(Guid)`,
      `System`, and `Unknown` — where **`Unknown` has no constructor callable by writing code** and is
      producible only by materialising a historic row. That restriction is the point; do not add a
      convenience factory for it.
- [x] 1.2 Add the actor to `Domain/Auditing/AuditEvent.cs` as a **required** constructor parameter,
      not an optional one defaulting to null (design D1). Store it as a nullable `ActorUserId` plus an
      `ActorKind`.
- [x] 1.3 Add `Domain/Auditing/AuditEventTypes.cs` aggregating the per-feature event classes into a
      closed `All` catalogue, and validate the event type against it in the constructor (design D6).
      Covers: Audit events carry identifiers and codes only.
- [x] 1.4 Add `Read = "candidate.read"` to `Domain/Candidates/CandidateAuditEvents.cs` and **correct
      its remark** — it currently claims the event carries the actor (it did not) and that reads are
      not audited (they now are).
- [x] 1.5 Update the five call sites to supply an actor: `CandidateRepository.cs:169`, `:222`,
      `CatalogRepository.cs:47`, `DocumentRepository.cs:51`, and `DocumentStorageReconciler.cs:85`
      — the reconciler supplies `AuditActor.System`.
- [x] 1.6 Thread the actor's internal user id from `ICurrentActor` (KTL-16) to each write call site.

## 2. Persistence, indexes and the append-only revocation

Covers: The trail is append-only to the application; Audit read surface.

- [x] 2.1 Update `AuditEventConfiguration`: map the two actor columns, keep the `CorrelationId` index,
      and add the filter indexes from design D7 — `(CreatedAtUtc DESC)`,
      `(EventType, CreatedAtUtc DESC)`, `(ActorUserId, CreatedAtUtc DESC)`,
      `(SubjectId, CreatedAtUtc DESC)`.
- [x] 2.2 Generate the migration with
      `dotnet ef migrations add AddAuditActor --project backend/Infrastructure --startup-project backend/Web --output-dir Persistence/Migrations`
      and extend it: columns nullable so historic rows keep a null actor; the four indexes; and
      **`REVOKE UPDATE, DELETE ON "AUD_Events" FROM ktl_runtime`**, following the
      `RevokeCandidateDelete` precedent. Column and revocation ship together (design D7).
- [x] 2.3 Write a `Down` that drops the columns and indexes **and restores the `UPDATE, DELETE`
      grant**, so a rollback genuinely returns the previous state rather than leaving the old code
      unable to write (design, Migration Plan).
- [x] 2.4 Apply the migration against the local stack and inspect the columns, indexes and the
      resulting `ktl_runtime` privileges in PostgreSQL.

## 3. Auditing reads of personal data

Covers: Reads of personal data are audited. Design D3, D4, D5.

- [x] 3.1 Record an audit event when a single candidate's record is read, written by the **query
      handler after the read succeeds** — not by the repository, which has no transaction to join
      (design D5).
- [x] 3.2 Record an audit event on a successful document download, with the document and candidate
      identifiers and no filename, content or storage key.
- [x] 3.3 Confirm by test that searching and listing candidates record **nothing** (design D3), and
      that a refused or empty read records nothing (design D4).

## 4. The audit read surface

Covers: Audit read surface; Audit read authorization fails closed.

- [x] 4.1 Add `Permissions.AuditRead = "audit.read"` to `Application/Abstractions/Identity`, include
      it in `Permissions.All`, and grant it in the seeded roles decided in the design's open question
      — narrowly, not to the recruiter roles.
- [x] 4.2 Add `Application/Features/Audit/AuditGuards.cs` (`RequireRead`) and the paged query, its
      FluentValidation validator and its sealed handler with the guard repeated **first**. Filters:
      date range, event type (validated against the closed catalogue), actor, subject.
- [x] 4.3 Page deterministically: `CreatedAtUtc` descending with the event id as tie-breaker, default
      25, maximum 100, matching the search convention.
- [x] 4.4 Reject an unknown event type or malformed date range with a stable validation problem rather
      than silently narrowing the result.
- [x] 4.5 Add `Web/Features/Audit/AuditEndpoints.cs` as a `MapGroup("/api/audit")` extension with
      nested request records, `.WithName()` and explicit `.Produces*` metadata, dispatching through
      `ISender`; register it in `Program.cs`.
- [x] 4.6 Confirm the response carries identifiers and codes only — no candidate name, no actor
      display name, no email (design D9).

## 5. Backend tests and security evidence

- [x] 5.1 Unit test per audited operation asserting the event carries the expected actor: each
      candidate write, each catalog write, each document lifecycle event, the reconciler's system
      actor, the candidate detail read and the document download.
- [x] 5.2 **Test enumerating the audited operations and asserting every one supplies an actor**, so
      the trail cannot become half-complete as new operations are added (design D1).
- [x] 5.3 Test asserting `AuditActor.Unknown` cannot be constructed by writing code and is only
      produced by reading a historic row.
- [x] 5.4 Test asserting an event type outside the closed catalogue is rejected rather than stored.
- [x] 5.5 Test asserting an audit event contains no email, display name, external subject or candidate
      field value.
- [x] 5.6 Test asserting a rolled-back transaction leaves no audit event, and that an event implies
      the change was applied.
- [x] 5.7 Integration tests for the read endpoint: unauthenticated 401; authenticated without
      `audit.read` 403; an actor holding every candidate, catalog and document permission still 403 —
      each sent with a malformed filter to prove the refusal precedes validation.
- [x] 5.8 Integration tests for each filter and for deterministic paging over a data set with tied
      timestamps.
- [x] 5.9 **Grants test: `ktl_runtime` holds no `UPDATE` and no `DELETE` on `AUD_Events`**, asserted
      by querying the catalog rather than reading the migration text.
- [x] 5.10 Test asserting a historic row with a null actor reads back as unknown and is distinguishable
      from the system actor.
- [x] 5.11 Review and update the existing tests affected by the `AuditEvent` constructor change.
- [x] 5.12 **Run** `npm run test:backend` with Docker running and inspect the output.

## 6. Auditoría screen

Covers: Auditoría administration screen; the primary-navigation delta.

- [x] 6.1 Add `audit.read` to the `Permission` union and `ALL_PERMISSIONS` in `auth.models.ts`, and to
      the seeded roles that hold it.
- [x] 6.2 Add the `Auditoría` entry to `ADMIN_GROUP` in `src/app/core/layout/nav-items.ts` and
      `audit.read` to `NAV_PERMISSIONS`, and call `usePermission` for it at the top of `PrimaryNav`.
      **Do not add a `NavLink` to `app-layout.tsx`.**
- [x] 6.3 Add `src/app/features/admin/audit/audit.service.ts` as an API gateway with a subscribing
      `useAudit()` hook, following the `CandidateApi` shape.
- [x] 6.4 Add the list page with the documented filters and paging, rendering each event's type,
      subject, actor, outcome and timestamp. Format timestamps with `formatDate`, never a hardcoded
      locale.
- [x] 6.5 Resolve actor display names client-side: collect the distinct actor ids on the current page
      and resolve them in one call to the users endpoint (design D9). A reader without `users.manage`
      sees the internal id — a legible degradation, not an error.
- [x] 6.6 Render an unknown actor and the system actor as **distinct explicit labels**, never as a
      blank cell, which reads as "nobody" and is a different claim.
- [x] 6.7 Add the route behind `RequirePermission` for `audit.read`.
- [x] 6.8 Put all copy in `src/assets/i18n/es.json` under flat `admin.audit.*` keys, using whole
      sentences with interpolation. Do not add the new files to `LEGACY_HARDCODED_COPY`.
- [x] 6.9 Put a `name=` attribute on every filter control and a `data-testid` on every element the
      Playwright suite will bind to.

## 7. Frontend tests

- [x] 7.1 Review and update the existing navigation unit tests — the entry count and the
      administration-group scenarios change.
- [x] 7.2 Specs for the audit service: filters, paging, and the error paths.
- [x] 7.3 Specs for the page: rendering, filtering, paging, actor name resolution, and the distinct
      unknown and system labels.
- [x] 7.4 Spec asserting the `Auditoría` entry is absent without `audit.read` and that the route is
      redirected.
- [x] 7.5 **Run** `npm test` and inspect the output.

## 8. End-to-end verification

- [x] 8.1 Add `tests/e2e/audit-trail.spec.ts`: perform a candidate write and a candidate detail read,
      then open Auditoría and confirm both events appear naming the actor. No selector may hardcode
      Spanish text.
- [x] 8.2 **Run** `npm run e2e` with `docker compose up` running, inspect the output, and restore seed
      data afterwards.
- [x] 8.3 **Run** the existing candidate, document, catalog and navigation e2e specs to confirm the
      audit writes did not break them.

## 9. Security gates and documentation

- [x] 9.1 Add `tests/security/ktl-19-audit-boundary.spec.ts` asserting the audit surface fails closed
      for unauthenticated and unauthorized callers and leaks no candidate or actor personal data.
- [x] 9.2 **Run** `npm run security:rls` and `npm run security:storage` and inspect the output.
- [x] 9.3 Write `docs/ktl-19/audit-contract.md`: the event row shape, the closed event-type catalogue,
      the actor cases, the filters, which reads are recorded and **which deliberately are not**, with
      the reasoning from design D3.
- [x] 9.4 Write `docs/ktl-19/runbook.md`: who should hold `audit.read` and why narrowly; what the
      trail is for; expected growth; and **what the future retention ticket will have to do now that
      `ktl_runtime` cannot delete** (design D8).
- [x] 9.5 Update `docs/ktl-16/authentication-and-authorization.md` to add `audit.read` to the
      permission table.
- [x] 9.6 Update `README.md` (Spanish) if the Auditoría section needs explaining.

## 10. Done checks

- [x] 10.1 **Run** `npm run build:all` and confirm it is clean (warnings are errors).
- [x] 10.2 **Run** `npm run lint` and `npm run format:check`.
- [x] 10.3 **Run** `npm test` and `npm run test:backend` once more against the final tree and inspect
      both outputs.
- [x] 10.4 Confirm by inspection that no application code path can update or delete an audit event.
- [x] 10.5 Run `openspec validate ktl-19-audit-actor-and-read-surface --strict`.
