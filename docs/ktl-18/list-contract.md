# KTL-18 candidate list contract

The candidate list is a search with preset criteria. It issues **one** paged request per view
against the KTL-10 search endpoint, and PostgreSQL does the filtering, counting, ordering and
paging. The browser holds one page of the minimal search projection and nothing else.

There is no second list endpoint. The list and advanced search share one request, one page
envelope, one projection and one set of ordering rules; see
[`docs/ktl-10/search.md`](../ktl-10/search.md) for everything this document does not change.

## Request

`POST /api/candidates/search`

```jsonc
{
  "filters": {
    "text": "", // identity, contact and notes; see "Text filter" below
    "statusValues": ["hired"], // the list sends at most one status; [] = unrestricted
    "hasCv": "", // "", "yes" or "no" — presence of a primary CV in any scan state
  },
  "page": 1,
  "pageSize": 25,
  "sortField": "updatedAt", // optional; see "Sorting"
  "sortDirection": "desc", // optional; "asc" or "desc"
  "includeInactive": false, // optional; see "Removed candidates"
}
```

Every new member is optional. A request that omits them behaves exactly as KTL-10 did, which is
what let the backend ship before the frontend moved onto it.

## Page envelope

```jsonc
{
  "items": [
    {
      "candidateId": "…",
      "firstName": "…",
      "lastName": "…",
      "phone": "…",
      "email": "…",
      "status": "available",
      "hasPrimaryCv": true,
      "primaryCvDocumentId": "…",
      "updatedAt": "…",
      "isActive": true, // KTL-18: false only when removed candidates were requested
    },
  ],
  "page": 1,
  "pageSize": 25,
  "totalCount": 57,
}
```

No relation collections, notes, consent metadata, retention metadata, location, source,
storage key, filename or scan state. The list previously received every candidate's notes,
consent and retention dates, location and source in order to render a page of names.

| Rule         | Value                                                    |
| ------------ | -------------------------------------------------------- |
| Default page | 1                                                        |
| Default size | **25** (the list used 10 before KTL-18)                  |
| Maximum size | 100; the list offers 25, 50 and 100                      |
| Refused      | page < 1, size < 1, size > 100 (`400`, before any query) |
| Past the end | `200` with an empty `items` and the true `totalCount`    |

## Sorting

| `sortField` | Ordering                              |
| ----------- | ------------------------------------- |
| `updatedAt` | `UpdatedAtUtc` (default, with `desc`) |
| `lastName`  | `LastName`, then `FirstName`          |
| `status`    | `Status` code, alphabetically         |

`sortDirection` applies to every term of the chosen field. The candidate identifier ascending is
**always** the final term, whatever the field and direction, so pages never overlap or skip for
unchanged data even when most candidates share a status.

The field is parsed against this closed set into an enum before the query layer is reached.
Anything else — including a differently cased name such as `LastName` — is refused with `400`
and the stable code `search.sort_field.invalid`; an invalid direction with
`search.sort_direction.invalid`. No caller text is ever part of the executed SQL.

Supporting indexes (migration `Ktl18CandidateSortIndexes`, grants unchanged):

- `IX_CND_Candidates_IsActive_LastName_FirstName_Id`
- `IX_CND_Candidates_IsActive_Status_Id`
- `IX_CND_Candidates_IsActive_UpdatedAtUtc` (existing, KTL-6)

`SearchQueryPlanTests` captures each field in both directions at the last page of a 3,000-row
dataset into [`docs/ktl-10/query-plans.md`](../ktl-10/query-plans.md).

## Removed candidates

`includeInactive: true` adds logically removed candidates to the pages and the count, each with
`isActive: false`. It requires **`candidates.delete`** — the permission that governs removal and
restoration — in addition to `candidates.read`.

The check runs after authentication and **before** validation. A caller holding
`candidates.read` alone gets `403`, not an active-only result: silently narrowing would let the
caller believe they had seen everything. The list hides the "Incluir inactivos" control for
those callers as well, but hiding it is not the control.

## Text filter

The list's free-text filter is the search text family: first name, last name, email, phone
**and notes**. Before KTL-18 the list matched name, email and phone only.

## URL parameters

Page, sort and the status, CV and inactive filters live in the URL as the single source of truth,
so the back button, a reload and a pasted link reopen the same view. Defaults are omitted, so
`/app/candidates` is the default view.

| Parameter  | Meaning              | Default     | Malformed value              |
| ---------- | -------------------- | ----------- | ---------------------------- |
| `page`     | page number          | `1`         | default                      |
| `pageSize` | 1–100                | `25`        | default                      |
| `sort`     | sort field           | `updatedAt` | **sent to the API, refused** |
| `dir`      | `asc` / `desc`       | per field¹  | default                      |
| `status`   | one candidate status | none        | ignored                      |
| `cv`       | `yes` / `no`         | none        | ignored                      |
| `inactive` | `1` includes removed | absent      | ignored                      |

¹ `desc` for `updatedAt`, `asc` for the others.

An unknown sort field is the one value not silently replaced: a silently ignored sort is a wrong
answer presented as a right one, so the page shows the API's refusal.

**The free-text filter is never written to the URL.** A search term is personal data, and a URL
is recorded by every access log a reload or a shared link passes through — the same reason search
is a POST. The text filter is held by the page and resets on reload.

## Selection

Selection is scoped to the page on screen. Changing page, sort or any filter clears it, and the
header states how many rows on this page are selected. A bulk action acts only on selected rows of
the current page, loading each one's aggregate for its concurrency version first.

## Dashboard

The dashboard's counts are the `totalCount` of one-row searches (`pageSize: 1`): active, without
primary CV, with primary CV, and — for `candidates.delete` holders — inactive. "Pendientes de
revisión" and "Recibidos este mes" were removed: the search contract has no filter for them, and
computing them meant downloading every candidate's retention metadata.

## Unpaged route

`GET /api/candidates?includeInactive=` still exists so the previous SPA can be redeployed as a
rollback. Nothing in `src/` calls it. It is removed in the following release, after confirming by
grep that neither `src/` nor `tests/e2e/` (whose seed helper still uses it) calls it.
