# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

Since KTL-33, free text and the last-name order run in the API over decrypted values, so
they issue no SQL of their own worth a plan here. Their evidence is
[`docs/ktl-33/performance.md`](../ktl-33/performance.md).

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 4.2 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=324) (actual time=3.991..4.021 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=324) (actual time=3.239..3.946 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=321) (actual time=2.343..2.530 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.008..0.955 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.288 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.163..0.163 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.172..0.172 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.240 ms
Execution Time: 4.203 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 4.5 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=324) (actual time=4.246..4.282 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=324) (actual time=3.460..4.207 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=321) (actual time=2.581..2.786 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.006..0.964 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.260 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.159..0.160 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.204..0.204 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.220 ms
Execution Time: 4.475 ms
```

## Deep page 30 sorted by AvailabilityCheckedOn Ascending

Execution time: 5.3 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=325) (actual time=5.152..5.176 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=325) (actual time=4.483..5.102 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=322) (actual time=3.657..3.800 rows=3000 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=322) (actual time=0.007..0.901 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.266 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.160..0.161 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.145..0.145 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.461 ms
Execution Time: 5.347 ms
```

## Deep page 30 sorted by AvailabilityCheckedOn Descending

Execution time: 5.6 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=325) (actual time=5.340..5.365 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=325) (actual time=4.677..5.291 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=322) (actual time=3.746..3.886 rows=3000 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=322) (actual time=0.006..0.822 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.015..0.327 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.174..0.175 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.145..0.145 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.222 ms
Execution Time: 5.551 ms
```

## Unfiltered first page

Execution time: 0.9 ms

```
Limit  (cost=0.58..631.85 rows=25 width=324) (actual time=0.816..0.824 rows=25 loops=1)
  Buffers: shared hit=273
  ->  Result  (cost=0.58..75753.03 rows=3000 width=324) (actual time=0.815..0.821 rows=25 loops=1)
        Buffers: shared hit=273
        ->  Incremental Sort  (cost=0.58..1015.53 rows=3000 width=321) (actual time=0.043..0.044 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=21
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..880.53 rows=3000 width=321) (actual time=0.015..0.026 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=21
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.230 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.159..0.159 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.146..0.146 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.215 ms
Execution Time: 0.916 ms
```

## Availability subset and primary CV

Execution time: 1.0 ms

```
Limit  (cost=1.97..660.89 rows=25 width=324) (actual time=0.881..0.903 rows=25 loops=1)
  Buffers: shared hit=361
  ->  Result  (cost=1.97..35135.71 rows=1333 width=324) (actual time=0.880..0.901 rows=25 loops=1)
        Buffers: shared hit=361
        ->  Incremental Sort  (cost=1.97..1927.34 rows=1333 width=321) (actual time=0.073..0.075 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=109
              ->  Nested Loop Semi Join  (cost=0.56..1867.36 rows=1333 width=321) (actual time=0.020..0.056 rows=26 loops=1)
                    Buffers: shared hit=109
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..888.03 rows=2000 width=321) (actual time=0.011..0.027 rows=26 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("AvailabilityState")::text = ANY ('{available,unavailable}'::text[]))
                          Rows Removed by Filter: 12
                          Buffers: shared hit=31
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.48 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=26)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=78
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.241 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..124.00 rows=1 width=16) (actual time=0.161..0.161 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..119.00 rows=1 width=16) (actual time=0.145..0.145 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=20
Planning Time: 0.401 ms
Execution Time: 1.002 ms
```

## Checked from a recent date

Execution time: 1.1 ms

```
Limit  (cost=223.72..846.60 rows=25 width=325) (actual time=0.961..0.969 rows=25 loops=1)
  Buffers: shared hit=354
  ->  Result  (cost=223.72..2715.22 rows=100 width=325) (actual time=0.960..0.967 rows=25 loops=1)
        Buffers: shared hit=354
        ->  Sort  (cost=223.72..223.97 rows=100 width=322) (actual time=0.176..0.178 rows=25 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn" DESC, c."Id"
              Sort Method: top-N heapsort  Memory: 44kB
              Buffers: shared hit=102
              ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=5.06..220.90 rows=100 width=322) (actual time=0.030..0.122 rows=100 loops=1)
                    Recheck Cond: (("AvailabilityCheckedOn" >= '2026-09-01'::date) AND "IsActive")
                    Heap Blocks: exact=99
                    Buffers: shared hit=102
                    ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_AvailabilityCheckedOn"  (cost=0.00..5.03 rows=100 width=0) (actual time=0.016..0.016 rows=100 loops=1)
                          Index Cond: ("AvailabilityCheckedOn" >= '2026-09-01'::date)
                          Buffers: shared hit=3
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.239 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.157..0.157 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.145..0.145 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.214 ms
Execution Time: 1.106 ms
```

## Skill ANY across three values

Execution time: 1.7 ms

```
Limit  (cost=29.10..1373.00 rows=25 width=324) (actual time=1.415..1.425 rows=25 loops=1)
  Buffers: shared hit=685
  ->  Result  (cost=29.10..141138.97 rows=2625 width=324) (actual time=1.415..1.423 rows=25 loops=1)
        Buffers: shared hit=685
        ->  Incremental Sort  (cost=29.10..75743.65 rows=2625 width=321) (actual time=0.610..0.613 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=433
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75625.53 rows=2625 width=321) (actual time=0.563..0.592 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 8).col1)) OR (ANY ("Id" = (hashed SubPlan 10).col1)) OR (ANY ("Id" = (hashed SubPlan 12).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=433
                    SubPlan 8
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.030..0.146 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f722-ac70-72e3-9fed-8b3dfb8a9e03}'::uuid[]))
                            Heap Blocks: exact=128
                            Buffers: shared hit=130
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.017..0.018 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f722-ac70-72e3-9fed-8b3dfb8a9e03}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 10
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.023..0.094 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f722-ac70-77d8-a97e-3b2a3ee19a66}'::uuid[]))
                            Heap Blocks: exact=120
                            Buffers: shared hit=122
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.013..0.013 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f722-ac70-77d8-a97e-3b2a3ee19a66}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 12
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.022..0.096 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f722-ac70-73ca-91e1-5698be146272}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.013..0.013 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f722-ac70-73ca-91e1-5698be146272}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.236 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c4  (cost=0.00..124.00 rows=1 width=16) (actual time=0.158..0.158 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c5  (cost=0.00..119.00 rows=1 width=16) (actual time=0.148..0.148 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=13
Planning Time: 0.393 ms
Execution Time: 1.658 ms
```

## Skill ALL across three values

Execution time: 1.5 ms

```
Limit  (cost=693.25..842.74 rows=6 width=324) (actual time=1.319..1.330 rows=3 loops=1)
  Buffers: shared hit=717
  ->  Result  (cost=693.25..842.74 rows=6 width=324) (actual time=1.318..1.329 rows=3 loops=1)
        Buffers: shared hit=717
        ->  Sort  (cost=693.25..693.26 rows=6 width=321) (actual time=1.286..1.288 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 26kB
              Buffers: shared hit=690
              ->  Nested Loop Semi Join  (cost=290.32..693.17 rows=6 width=321) (actual time=0.571..1.280 rows=3 loops=1)
                    Buffers: shared hit=690
                    ->  Hash Semi Join  (cost=290.04..667.34 rows=46 width=353) (actual time=0.370..1.216 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=581
                          ->  Hash Semi Join  (cost=144.53..520.27 rows=357 width=337) (actual time=0.148..0.988 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=455
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.004..0.670 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=333
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.135..0.136 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=122
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.023..0.093 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0f722-ac70-77d8-a97e-3b2a3ee19a66}'::uuid[]))
                                            Heap Blocks: exact=120
                                            Buffers: shared hit=122
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.015..0.015 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0f722-ac70-77d8-a97e-3b2a3ee19a66}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.195..0.196 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=126
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.031..0.149 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a0f722-ac70-73ca-91e1-5698be146272}'::uuid[]))
                                      Heap Blocks: exact=124
                                      Buffers: shared hit=126
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.019..0.019 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a0f722-ac70-73ca-91e1-5698be146272}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=30)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: (("SkillId" = ANY ('{01a0f722-ac70-72e3-9fed-8b3dfb8a9e03}'::uuid[])) AND ("LevelId" = ANY ('{01a0f722-ac70-7236-b105-c77271650a15,01a0f722-ac70-7679-8866-7d7d26285fc2,01a0f722-ac70-7ad9-99e9-8ac259f9a4fe,01a0f722-ac70-7d3e-8c0a-3843792c97e5}'::uuid[])))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=109
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.007..0.007 rows=1 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 3
                Buffers: shared hit=9
        SubPlan 3
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.30 rows=1 width=0) (actual time=0.003..0.003 rows=0 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 1
                Buffers: shared hit=9
        SubPlan 5
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c5  (cost=0.28..8.30 rows=1 width=0) (actual time=0.002..0.002 rows=0 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
                Rows Removed by Filter: 1
                Buffers: shared hit=9
Planning:
  Buffers: shared hit=70
Planning Time: 0.959 ms
Execution Time: 1.478 ms
```

## Every SQL family combined

Execution time: 0.4 ms

```
Limit  (cost=407.45..432.37 rows=1 width=324) (actual time=0.268..0.270 rows=0 loops=1)
  Buffers: shared hit=324
  ->  Result  (cost=407.45..432.37 rows=1 width=324) (actual time=0.268..0.269 rows=0 loops=1)
        Buffers: shared hit=324
        ->  Sort  (cost=407.45..407.46 rows=1 width=321) (actual time=0.267..0.268 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=324
              ->  Nested Loop Semi Join  (cost=55.88..407.44 rows=1 width=321) (actual time=0.261..0.262 rows=0 loops=1)
                    Buffers: shared hit=324
                    ->  Nested Loop Semi Join  (cost=55.60..406.43 rows=1 width=369) (actual time=0.237..0.253 rows=1 loops=1)
                          Buffers: shared hit=320
                          ->  Nested Loop Semi Join  (cost=55.32..405.33 rows=1 width=353) (actual time=0.226..0.242 rows=1 loops=1)
                                Buffers: shared hit=317
                                ->  Nested Loop  (cost=55.04..395.40 rows=12 width=337) (actual time=0.109..0.208 rows=18 loops=1)
                                      Buffers: shared hit=263
                                      ->  HashAggregate  (cost=54.76..55.50 rows=74 width=16) (actual time=0.073..0.078 rows=74 loops=1)
                                            Group Key: c3."CandidateId"
                                            Batches: 1  Memory Usage: 24kB
                                            Buffers: shared hit=41
                                            ->  Bitmap Heap Scan on "CND_CandidatePrograms" c3  (cost=4.85..54.58 rows=74 width=16) (actual time=0.022..0.058 rows=74 loops=1)
                                                  Recheck Cond: ("ProgramId" = ANY ('{01a0f722-ac70-7d0b-931d-b4492089cb26}'::uuid[]))
                                                  Heap Blocks: exact=39
                                                  Buffers: shared hit=41
                                                  ->  Bitmap Index Scan on "IX_CND_CandidatePrograms_ProgramId_ProgramFamily"  (cost=0.00..4.83 rows=74 width=0) (actual time=0.015..0.015 rows=74 loops=1)
                                                        Index Cond: ("ProgramId" = ANY ('{01a0f722-ac70-7d0b-931d-b4492089cb26}'::uuid[]))
                                                        Buffers: shared hit=2
                                      ->  Index Scan using "PK_CND_Candidates" on "CND_Candidates" c  (cost=0.28..4.68 rows=1 width=321) (actual time=0.002..0.002 rows=0 loops=74)
                                            Index Cond: ("Id" = c3."CandidateId")
                                            Filter: ("IsActive" AND (("AvailabilityState")::text = ANY ('{available}'::text[])) AND ("AvailabilityCheckedOn" >= '2026-01-01'::date))
                                            Rows Removed by Filter: 1
                                            Buffers: shared hit=222
                                ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.83 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=18)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("LanguageId" = ANY ('{01a0f722-ac70-731d-9583-e8b804fd1b2c}'::uuid[])) AND ("LevelId" = ANY ('{01a0f722-ac70-7100-9d4c-bf71e048d418,01a0f722-ac70-7308-81e4-a9333e31f2ed,01a0f722-ac70-779d-b546-566315c9e406,01a0f722-ac70-799b-907a-d11576b1a6f7,01a0f722-ac70-7e36-a546-c0a4cc6317a7,01a0f722-ac70-7f0d-bcd8-0e5882734c89}'::uuid[])))
                                      Rows Removed by Filter: 1
                                      Buffers: shared hit=54
                          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..1.10 rows=1 width=16) (actual time=0.010..0.010 rows=1 loops=1)
                                Index Cond: ("CandidateId" = c."Id")
                                Heap Fetches: 1
                                Buffers: shared hit=3
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (actual time=0.008..0.008 rows=0 loops=1)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("SkillId" = ANY ('{01a0f722-ac70-72e3-9fed-8b3dfb8a9e03}'::uuid[]))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=4
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 0
        SubPlan 3
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c5  (cost=0.28..8.30 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
        SubPlan 5
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c6  (cost=0.28..8.30 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, '
'::text) <> ''::text)))
Planning:
  Buffers: shared hit=95
Planning Time: 1.452 ms
Execution Time: 0.396 ms
```

