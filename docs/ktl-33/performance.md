# KTL-33 search performance over encrypted fields

Captured by `EncryptedSearchPerformanceTests` against 12,000 active candidates
with notes of 200–600 characters, in a Testcontainers `postgres:17.6-alpine` on the developer machine.
Each case: 3 warm-up runs, then 20 timed runs of `CandidateSearchQuery.SearchAsync` (every
statement plus decryption, matching, sorting and paging; HTTP and JSON excluded).
Budget: p95 ≤ 300 ms at the ceiling.

| Case | Median (ms) | p95 (ms) | Max (ms) |
| ---- | ----------: | -------: | -------: |
| Text matching one candidate | 201.3 | 279.4 | 292.2 |
| Text matching most candidates | 157.0 | 205.9 | 211.0 |
| Text found only in notes | 165.1 | 203.4 | 217.1 |
| Text matching nothing | 183.5 | 204.2 | 226.9 |
| Last-name order, first page | 70.1 | 91.9 | 96.0 |
| Last-name order, deep page 120 | 66.7 | 99.3 | 100.8 |
| Text and status, last-name order | 45.5 | 51.9 | 54.7 |

Searches with neither free text nor the last-name order are unchanged by KTL-33 and
keep their SQL plan evidence in [`docs/ktl-10/query-plans.md`](../ktl-10/query-plans.md).
