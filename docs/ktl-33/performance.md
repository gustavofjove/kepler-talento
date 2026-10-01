# KTL-33 search performance over encrypted fields

Captured by `EncryptedSearchPerformanceTests` against 12,000 active candidates
with notes of 200–600 characters, in a Testcontainers `postgres:17.6-alpine` on the developer machine.
Each case: 3 warm-up runs, then 20 timed runs of `CandidateSearchQuery.SearchAsync` (every
statement plus decryption, matching, sorting and paging; HTTP and JSON excluded).
Budget: p95 ≤ 300 ms at the ceiling.

| Case | Median (ms) | p95 (ms) | Max (ms) |
| ---- | ----------: | -------: | -------: |
| Text matching one candidate | 183.5 | 213.5 | 229.4 |
| Text matching most candidates | 206.2 | 236.4 | 239.0 |
| Text found only in notes | 189.6 | 217.0 | 227.8 |
| Text matching nothing | 189.1 | 217.4 | 224.5 |
| Last-name order, first page | 57.3 | 97.4 | 116.1 |
| Last-name order, deep page 120 | 53.8 | 97.5 | 125.6 |
| Text and status, last-name order | 36.5 | 46.0 | 89.8 |

Searches with neither free text nor the last-name order are unchanged by KTL-33 and
keep their SQL plan evidence in [`docs/ktl-10/query-plans.md`](../ktl-10/query-plans.md).
