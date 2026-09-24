# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 4.8 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=4.597..4.695 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=1.780..4.595 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=1.265..1.352 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.009..0.401 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.258 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.122 ms
Execution Time: 4.812 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 4.8 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=4.618..4.717 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=1.783..4.617 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=1.245..1.389 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.005..0.371 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.220 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.120 ms
Execution Time: 4.826 ms
```

## Deep page 30 sorted by LastName Ascending

Execution time: 4.9 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=4.700..4.847 rows=100 loops=1)
  Buffers: shared hit=9008
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.490..4.746 rows=3000 loops=1)
        Buffers: shared hit=9008
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.039..1.540 rows=3000 loops=1)
              Sort Key: c."LastName", c."FirstName", c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=939
              ->  Index Scan using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.012..0.577 rows=3000 loops=1)
                    Buffers: shared hit=939
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.004..0.207 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.154 ms
Execution Time: 4.929 ms
```

## Deep page 30 sorted by LastName Descending

Execution time: 5.0 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=4.801..4.939 rows=100 loops=1)
  Buffers: shared hit=9032
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.588..4.837 rows=3000 loops=1)
        Buffers: shared hit=9032
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.048..1.550 rows=3000 loops=1)
              Sort Key: c."LastName" DESC, c."FirstName" DESC, c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=963
              ->  Index Scan Backward using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.015..0.572 rows=3000 loops=1)
                    Buffers: shared hit=963
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.228 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.135 ms
Execution Time: 5.043 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 5.7 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=5.481..5.579 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=2.641..5.479 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.162..2.264 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.007..0.406 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.221 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.130 ms
Execution Time: 5.690 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 9.3 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=8.993..9.182 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=4.598..9.066 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=3.874..4.060 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.008..0.589 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.011..0.229 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.142 ms
Execution Time: 9.318 ms
```

## Unfiltered first page

Execution time: 0.7 ms

```
Limit  (cost=0.40..419.29 rows=25 width=109) (actual time=0.593..0.620 rows=25 loops=1)
  Buffers: shared hit=145
  ->  Result  (cost=0.40..50267.47 rows=3000 width=109) (actual time=0.593..0.618 rows=25 loops=1)
        Buffers: shared hit=145
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=92) (actual time=0.037..0.038 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=9
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=92) (actual time=0.017..0.022 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=9
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.224 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=67
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=67
Planning:
  Buffers: shared hit=4
Planning Time: 0.147 ms
Execution Time: 0.701 ms
```

## Free text (leading wildcard)

Execution time: 6.5 ms

```
Limit  (cost=163.51..180.11 rows=1 width=109) (actual time=6.469..6.471 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=109) (actual time=6.468..6.470 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=92) (actual time=6.468..6.469 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=92) (actual time=6.461..6.462 rows=0 loops=1)
                    Filter: ("IsActive" AND ((("FirstName")::text ~~* '%ez 1234%'::text) OR (("LastName")::text ~~* '%ez 1234%'::text) OR (("Email")::text ~~* '%ez 1234%'::text) OR (("Phone")::text ~~* '%ez 1234%'::text) OR ("Notes" ~~* '%ez 1234%'::text)))
                    Rows Removed by Filter: 3000
                    Buffers: shared hit=96
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 0
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (never executed)
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (never executed)
                      Index Cond: ("CandidateId" = c."Id")
Planning:
  Buffers: shared hit=4
Planning Time: 0.355 ms
Execution Time: 6.532 ms
```

## Status subset and primary CV

Execution time: 0.7 ms

```
Limit  (cost=1.84..449.72 rows=25 width=109) (actual time=0.551..0.579 rows=25 loops=1)
  Buffers: shared hit=276
  ->  Result  (cost=1.84..14333.97 rows=800 width=109) (actual time=0.550..0.576 rows=25 loops=1)
        Buffers: shared hit=276
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=92) (actual time=0.081..0.083 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=132
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=92) (actual time=0.022..0.068 rows=26 loops=1)
                    Buffers: shared hit=132
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=92) (actual time=0.011..0.032 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=28
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.220 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.295 ms
Execution Time: 0.667 ms
```

## Skill ANY across three values

Execution time: 1.3 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=109) (actual time=1.094..1.124 rows=25 loops=1)
  Buffers: shared hit=529
  ->  Result  (cost=28.89..118770.59 rows=2625 width=109) (actual time=1.094..1.121 rows=25 loops=1)
        Buffers: shared hit=529
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=92) (actual time=0.572..0.574 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=391
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=92) (actual time=0.539..0.559 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=391
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.028..0.128 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7d07-b893-0c94758b70d4}'::uuid[]))
                            Heap Blocks: exact=121
                            Buffers: shared hit=123
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.017..0.017 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7d07-b893-0c94758b70d4}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.060..0.135 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7c3d-b6dc-68a7d4d20fbc}'::uuid[]))
                            Heap Blocks: exact=122
                            Buffers: shared hit=124
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.051..0.051 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7c3d-b6dc-68a7d4d20fbc}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.024..0.095 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7ee6-a34b-252bae289de8}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.014..0.014 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7ee6-a34b-252bae289de8}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.266 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=69
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=69
Planning:
  Buffers: shared hit=10
Planning Time: 0.238 ms
Execution Time: 1.296 ms
```

## Skill ALL across three values

Execution time: 1.1 ms

```
Limit  (cost=456.15..555.76 rows=6 width=109) (actual time=0.983..0.992 rows=3 loops=1)
  Buffers: shared hit=428
  ->  Result  (cost=456.15..555.76 rows=6 width=109) (actual time=0.982..0.990 rows=3 loops=1)
        Buffers: shared hit=428
        ->  Sort  (cost=456.15..456.16 rows=6 width=92) (actual time=0.962..0.963 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=410
              ->  Nested Loop Semi Join  (cost=290.32..456.07 rows=6 width=92) (actual time=0.493..0.956 rows=3 loops=1)
                    Buffers: shared hit=410
                    ->  Hash Semi Join  (cost=290.04..430.34 rows=46 width=124) (actual time=0.362..0.908 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=347
                          ->  Hash Semi Join  (cost=144.53..283.27 rows=357 width=108) (actual time=0.171..0.711 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=220
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.005..0.368 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.160..0.160 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=124
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.022..0.116 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7c3d-b6dc-68a7d4d20fbc}'::uuid[]))
                                            Heap Blocks: exact=122
                                            Buffers: shared hit=124
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.013..0.013 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7c3d-b6dc-68a7d4d20fbc}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.169..0.170 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=127
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.028..0.125 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7ee6-a34b-252bae289de8}'::uuid[]))
                                      Heap Blocks: exact=125
                                      Buffers: shared hit=127
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.016..0.016 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a0d2e2-4e19-7ee6-a34b-252bae289de8}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "UX_CND_CandidateSkills_CandidateId_SkillId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=30)
                          Index Cond: (("CandidateId" = c."Id") AND ("SkillId" = ANY ('{01a0d2e2-4e19-7d07-b893-0c94758b70d4}'::uuid[])))
                          Filter: ("LevelId" = ANY ('{01a0d2e2-4e19-7564-97af-72f3f8e964c1,01a0d2e2-4e19-75b0-bbac-480674aa63f9,01a0d2e2-4e19-7a69-bc90-9cb3fbfd3b02,01a0d2e2-4e19-7e78-9f15-11d65a165418}'::uuid[]))
                          Buffers: shared hit=63
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.005..0.005 rows=1 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 3
                Buffers: shared hit=9
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3)
                Buffers: shared hit=9
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=9
Planning:
  Buffers: shared hit=67
Planning Time: 0.909 ms
Execution Time: 1.122 ms
```

## Every family combined

Execution time: 1.4 ms

```
Limit  (cost=150.99..167.60 rows=1 width=109) (actual time=1.288..1.289 rows=0 loops=1)
  Buffers: shared hit=103
  ->  Result  (cost=150.99..167.60 rows=1 width=109) (actual time=1.287..1.288 rows=0 loops=1)
        Buffers: shared hit=103
        ->  Sort  (cost=150.99..151.00 rows=1 width=92) (actual time=1.287..1.288 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=103
              ->  Nested Loop Semi Join  (cost=31.40..150.98 rows=1 width=92) (actual time=1.281..1.282 rows=0 loops=1)
                    Buffers: shared hit=103
                    ->  Nested Loop Semi Join  (cost=31.12..150.42 rows=1 width=140) (actual time=1.281..1.282 rows=0 loops=1)
                          Buffers: shared hit=103
                          ->  Nested Loop Semi Join  (cost=30.84..149.84 rows=1 width=124) (actual time=1.281..1.281 rows=0 loops=1)
                                Buffers: shared hit=103
                                ->  Nested Loop Semi Join  (cost=30.56..148.84 rows=1 width=108) (actual time=1.281..1.281 rows=0 loops=1)
                                      Buffers: shared hit=103
                                      ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=30.28..140.53 rows=1 width=92) (actual time=1.281..1.281 rows=0 loops=1)
                                            Recheck Cond: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Filter: ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text))
                                            Rows Removed by Filter: 600
                                            Heap Blocks: exact=96
                                            Buffers: shared hit=103
                                            ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_Status_Id"  (cost=0.00..30.28 rows=600 width=0) (actual time=0.034..0.035 rows=600 loops=1)
                                                  Index Cond: (("IsActive" = true) AND (("Status")::text = ANY ('{available}'::text[])))
                                                  Buffers: shared hit=7
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0d2e2-4e19-7d07-b893-0c94758b70d4}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.43 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0d2e2-4ddf-7db4-bb6c-ad9ac5457151}'::uuid[])) AND ("LevelId" = ANY ('{01a0d2e2-4e19-7000-b2dc-dcfc6adcca3d,01a0d2e2-4e19-7110-a34b-7922895ed30f,01a0d2e2-4e19-740a-89cb-1b4e0c3b5d25,01a0d2e2-4e19-759b-a290-b7043c944dd2,01a0d2e2-4e19-78b0-a5a6-15cfc2884cd4,01a0d2e2-4e19-7ef0-a5e1-1e3ab55a3ad6}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0d2e2-4e19-702e-9da4-cfd2b269280b}'::uuid[]))
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 0
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (never executed)
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c5  (cost=0.28..8.29 rows=1 width=16) (never executed)
                      Index Cond: ("CandidateId" = c."Id")
Planning:
  Buffers: shared hit=92
Planning Time: 1.490 ms
Execution Time: 1.375 ms
```

