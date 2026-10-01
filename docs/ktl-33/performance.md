# KTL-33 search performance over encrypted fields

Captured by `EncryptedSearchPerformanceTests` against 12,000 active candidates
with notes of 200–600 characters, in a Testcontainers `postgres:17.6-alpine` on the developer machine.
Each case: 3 warm-up runs, then 20 timed runs of `CandidateSearchQuery.SearchAsync` (every
statement plus decryption, matching, sorting and paging; HTTP and JSON excluded).
Budget: p95 ≤ 300 ms at the ceiling.

| Case | Median (ms) | p95 (ms) | Max (ms) |
| ---- | ----------: | -------: | -------: |
| Text matching one candidate | 170.3 | 194.7 | 205.5 |
| Text matching most candidates | 184.1 | 212.7 | 217.0 |
| Text found only in notes | 187.2 | 238.4 | 265.0 |
| Text matching nothing | 189.8 | 232.4 | 258.2 |
| Last-name order, first page | 75.8 | 85.1 | 107.9 |
| Last-name order, deep page 120 | 73.8 | 105.2 | 118.3 |
| Text matching most candidates, availability-date order | 196.5 | 239.5 | 261.1 |
| Text and availability, last-name order | 69.5 | 91.6 | 99.6 |

Searches with neither free text nor the last-name order are unchanged by KTL-33 and
keep their SQL plan evidence in [`docs/ktl-10/query-plans.md`](../ktl-10/query-plans.md).
