# KTL-9 — Candidate relations and documents

**Status:** Ready for OpenSpec planning
**Architecture source:** [Kepler Talento Stack Blueprint](./kepler-talento-stack-blueprint.md)
**Depends on:** KTL-6 (catalogs), KTL-8 (candidate aggregate root)
**Blocks:** KTL-10 (search filters on relations)

## Summary

Move the five candidate relation collections and candidate document metadata to
PostgreSQL, and connect the CV upload/download flow to the private document storage and
malware scanning pipeline that KTL-5 built but never wired to real UI.

## Why

KTL-8 moves the candidate record; its nested collections stay behind. This ticket
finishes the aggregate: languages, programs, education, experience, skills, and
documents.

It also closes the largest gap left by KTL-5. That ticket delivered `IFileStorage`, the
ClamAV `IFileScanner` adapter, quarantine, fail-closed promotion and the durable
operations worker — all proven in tests, none of it reachable from the product. Today
[document.service.ts](src/app/features/documents/services/document.service.ts) does not
store file content at all: it validates type and size, builds a `CandidateDocument`
metadata record, and hands it to `CandidateService`. The bytes are discarded. Users have
a CV upload that stores no CV.

## In scope

### Backend

- Tables and configuration for the five relation collections, each keyed to the candidate
  aggregate with cascade-appropriate delete behaviour:
  - `CandidateLanguage` — `language`, `level`, `certification?`, `notes?`
  - `CandidateProgram` — `program`, `level`, `yearsExperience?`, `notes?`
  - `CandidateSkill` — `skill`, `level`, `notes?`
  - `CandidateEducation` — `educationType`, `degree`, `specialty?`, `institution`,
    `status`, `endYear?`, `notes?`
  - `CandidateExperience` — `company`, `position`, `sector`, `functions?`, `startDate?`,
    `endDate?`, `yearsExperience?`, `isCurrent`, `notes?`
- Reference values resolve against `CAT_` catalog families from KTL-6.
- Preserve the current duplicate rules, which are enforced in memory today — for example
  `CandidateRelationsService` rejects a second entry for the same language, compared
  case- and whitespace-insensitively.
- Relation write slices: add and remove per collection.
- Document slices: upload, list, set primary, download, remove.
- Upload path: accept content → quarantine → scan → promote only if clean. Infected,
  timed-out, errored and unscannable uploads fail closed. Previously clean documents stay
  downloadable while the scanner is unavailable.
- Exactly one primary CV per candidate, enforced server-side — today this is an in-memory
  invariant in `CandidateService.addDocument`.
- Reconcile the two divergent limits: the frontend enforces **10 MB, PDF only**
  ([document.service.ts](src/app/features/documents/services/document.service.ts)), while
  KTL-5 specified **20 MB** across an allowlist of PDF, DOC, DOCX, ODT, RTF, TXT, JPEG,
  PNG, TIFF and BMP. The API contract is authoritative; the frontend follows it.
- Extend the capability catalogue with `documents.upload` and `documents.download`,
  mapped to `upload_candidate_documents` and `download_candidate_documents`.
- Audit events for upload, scan outcome, primary change, download and removal — without
  document contents.

### Frontend

- Rewrite `CandidateRelationsService` and `DocumentService` against the shared transport.
- Real upload: send file content as `FormData` through
  [api-transport.ts](src/app/core/http/api-transport.ts), which already declines to set a
  `Content-Type` for `FormData` bodies.
- Surface scan state in the UI. A document is no longer available the instant it uploads
  — it is quarantined until clean, which is a genuinely new user-visible state needing
  Spanish copy.
- Download through the transport's `download()` path, which already handles
  `Content-Disposition` filename sanitisation.

## Out of scope

- Search over relations (KTL-10).
- Authentication.
- In-browser Office conversion or document preview — explicitly out of scope in KTL-5 and
  still out here.
- Bulk document operations.

## Personal-data and security impact

**Highest security surface after KTL-7.** CV documents are externally sourced binaries
containing personal data.

- Principle 3: only opaque relative keys are stored; no response may expose a host path,
  drive letter, UNC path, mount point or permanent URL.
- Fail closed: unscanned and unclean content is never downloadable, and a scanner failure
  must not degrade into permissive behaviour.
- Downloads are permission-checked per request and carry `private, no-store`, following
  the pattern in
  [ReferenceCandidateEndpoints.cs](backend/Web/Features/Candidates/ReferenceCandidateEndpoints.cs).
- Original filenames are metadata, never physical paths, and are sanitised before display.
- Document contents never reach logs.
- Raising the frontend limit from PDF-only/10 MB to the KTL-5 allowlist/20 MB **widens
  the accepted content surface**. That is the deliberate KTL-5 decision, but the design
  must state it explicitly and the security tests must cover each newly accepted type
  along with the rejected ones (executables, scripts, HTML/SVG, archives, encrypted
  files, DOCM).

## Acceptance criteria

1. Relation add/remove succeeds through the real path for all five collections, with the
   existing duplicate rules and Spanish messages preserved.
2. Relation values resolve against catalog families; an unknown value is rejected with a
   stable code.
3. A clean upload is quarantined, scanned, promoted, and becomes downloadable.
4. An EICAR-style infected upload never becomes available and raises an audit event.
5. An upload that cannot be scanned fails closed.
6. A previously clean document remains downloadable while the scanner is down.
7. Exactly one primary CV per candidate holds under concurrent writes.
8. Files above 20 MB and disallowed types are rejected at the Nginx, API and scanner
   boundaries.
9. No response exposes an internal storage path.
10. Download fails closed without `download_candidate_documents`.
11. Uploaded document content is actually persisted and retrievable — the current
    behaviour of discarding bytes is gone.
12. Backend unit, integration, architecture, security and frontend tests pass, plus the
    candidate-profile and document Playwright flows.

## Deferred decisions

- Whether upload responds synchronously after scanning or returns a durable operation the
  UI polls. The KTL-5 worker supports the asynchronous shape; the synchronous shape is
  simpler but couples the request to scan latency. Decide in design against the 20 MB
  worst case.

## Next step

```
/enrich-us openspec/KTL-9.md
/opsx:new
```
