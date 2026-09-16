# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 9.8 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=9.426..9.555 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.656..9.428 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.601..2.860 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.016..0.891 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.018..0.504 rows=2000 loops=1)
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
Planning Time: 0.248 ms
Execution Time: 9.787 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 10.2 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=9.714..9.910 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.634..9.735 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.406..2.618 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.021..0.920 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.016..0.504 rows=2000 loops=1)
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
Planning Time: 0.181 ms
Execution Time: 10.223 ms
```

## Deep page 30 sorted by LastName Ascending

Execution time: 8.9 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=8.611..8.793 rows=100 loops=1)
  Buffers: shared hit=9303
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.664..8.640 rows=3000 loops=1)
        Buffers: shared hit=9303
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.067..2.905 rows=3000 loops=1)
              Sort Key: c."LastName", c."FirstName", c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=1234
              ->  Index Scan using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.017..1.323 rows=3000 loops=1)
                    Buffers: shared hit=1234
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.294 rows=2000 loops=1)
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
Planning Time: 0.151 ms
Execution Time: 8.909 ms
```

## Deep page 30 sorted by LastName Descending

Execution time: 9.1 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=8.712..8.882 rows=100 loops=1)
  Buffers: shared hit=9327
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=1.019..8.726 rows=3000 loops=1)
        Buffers: shared hit=9327
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.083..2.592 rows=3000 loops=1)
              Sort Key: c."LastName" DESC, c."FirstName" DESC, c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=1258
              ->  Index Scan Backward using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.025..1.131 rows=3000 loops=1)
                    Buffers: shared hit=1258
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.011..0.454 rows=2000 loops=1)
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
Planning Time: 0.251 ms
Execution Time: 9.068 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 6.9 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=6.628..6.746 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.235..6.625 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.581..2.699 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.009..0.473 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.009..0.281 rows=2000 loops=1)
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
Planning Time: 0.172 ms
Execution Time: 6.919 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 7.1 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=6.778..6.900 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.156..6.736 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.589..2.702 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.008..0.446 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.009..0.259 rows=2000 loops=1)
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
Planning Time: 0.178 ms
Execution Time: 7.070 ms
```

## Unfiltered first page

Execution time: 0.7 ms

```
Limit  (cost=0.40..419.29 rows=25 width=109) (actual time=0.608..0.640 rows=25 loops=1)
  Buffers: shared hit=144
  ->  Result  (cost=0.40..50267.47 rows=3000 width=109) (actual time=0.608..0.637 rows=25 loops=1)
        Buffers: shared hit=144
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=92) (actual time=0.039..0.041 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=8
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=92) (actual time=0.017..0.022 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=8
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.254 rows=2000 loops=1)
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
Planning Time: 0.141 ms
Execution Time: 0.739 ms
```

## Free text (leading wildcard)

Execution time: 7.7 ms

```
Limit  (cost=163.51..180.11 rows=1 width=109) (actual time=7.578..7.582 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=109) (actual time=7.576..7.580 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=92) (actual time=7.576..7.577 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=92) (actual time=7.565..7.566 rows=0 loops=1)
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
Planning Time: 0.568 ms
Execution Time: 7.656 ms
```

## Status subset and primary CV

Execution time: 0.8 ms

```
Limit  (cost=1.84..449.72 rows=25 width=109) (actual time=0.645..0.676 rows=25 loops=1)
  Buffers: shared hit=263
  ->  Result  (cost=1.84..14333.97 rows=800 width=109) (actual time=0.644..0.673 rows=25 loops=1)
        Buffers: shared hit=263
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=92) (actual time=0.092..0.094 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=119
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=92) (actual time=0.026..0.076 rows=26 loops=1)
                    Buffers: shared hit=119
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=92) (actual time=0.015..0.037 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=15
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.007..0.265 rows=2000 loops=1)
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
Planning Time: 0.359 ms
Execution Time: 0.840 ms
```

## Skill ANY across three values

Execution time: 1.5 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=109) (actual time=1.263..1.297 rows=25 loops=1)
  Buffers: shared hit=524
  ->  Result  (cost=28.89..118770.59 rows=2625 width=109) (actual time=1.262..1.294 rows=25 loops=1)
        Buffers: shared hit=524
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=92) (actual time=0.676..0.680 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=386
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=92) (actual time=0.633..0.663 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=386
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.039..0.157 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7916-9dc9-09df2ee670de}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.026..0.026 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7916-9dc9-09df2ee670de}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.023..0.098 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7e1e-a0c4-4cd9da3f20bd}'::uuid[]))
                            Heap Blocks: exact=121
                            Buffers: shared hit=123
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.013..0.013 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7e1e-a0c4-4cd9da3f20bd}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.024..0.178 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-755e-8039-38930a06f9c1}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.014..0.014 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-755e-8039-38930a06f9c1}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.273 rows=2000 loops=1)
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
Planning Time: 0.281 ms
Execution Time: 1.486 ms
```

## Skill ALL across three values

Execution time: 1.1 ms

```
Limit  (cost=339.43..372.64 rows=2 width=109) (actual time=1.017..1.021 rows=0 loops=1)
  Buffers: shared hit=624
  ->  Result  (cost=339.43..372.64 rows=2 width=109) (actual time=1.015..1.019 rows=0 loops=1)
        Buffers: shared hit=624
        ->  Sort  (cost=339.43..339.44 rows=2 width=92) (actual time=1.015..1.017 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=624
              ->  Nested Loop Semi Join  (cost=142.86..339.42 rows=2 width=92) (actual time=1.007..1.010 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=624
                    ->  Nested Loop Semi Join  (cost=142.57..332.73 rows=12 width=124) (actual time=0.264..0.991 rows=10 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=591
                          ->  Hash Semi Join  (cost=142.29..277.51 rows=99 width=108) (actual time=0.211..0.821 rows=108 loops=1)
                                Hash Cond: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=222
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.009..0.425 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=141.05..141.05 rows=99 width=16) (actual time=0.192..0.193 rows=108 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 14kB
                                      Buffers: shared hit=126
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.21..141.05 rows=99 width=16) (actual time=0.037..0.172 rows=108 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7916-9dc9-09df2ee670de}'::uuid[]))
                                            Filter: ("LevelId" = ANY ('{01a0aaf5-b2a3-7aef-9ca8-d954cc393d50}'::uuid[]))
                                            Rows Removed by Filter: 279
                                            Heap Blocks: exact=124
                                            Buffers: shared hit=126
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.022..0.023 rows=387 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0aaf5-b2a3-7916-9dc9-09df2ee670de}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=108)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a0aaf5-b2a3-7e1e-a0c4-4cd9da3f20bd}'::uuid[]))
                                Rows Removed by Filter: 3
                                Buffers: shared hit=369
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=10)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a0aaf5-b2a3-755e-8039-38930a06f9c1}'::uuid[]))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=33
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 0
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (never executed)
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (never executed)
                      Index Cond: ("CandidateId" = c."Id")
Planning:
  Buffers: shared hit=67
Planning Time: 1.001 ms
Execution Time: 1.144 ms
```

## Every family combined

Execution time: 1.6 ms

```
Limit  (cost=150.98..167.59 rows=1 width=109) (actual time=1.478..1.480 rows=0 loops=1)
  Buffers: shared hit=103
  ->  Result  (cost=150.98..167.59 rows=1 width=109) (actual time=1.477..1.479 rows=0 loops=1)
        Buffers: shared hit=103
        ->  Sort  (cost=150.98..150.99 rows=1 width=92) (actual time=1.477..1.478 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=103
              ->  Nested Loop Semi Join  (cost=31.40..150.97 rows=1 width=92) (actual time=1.470..1.471 rows=0 loops=1)
                    Buffers: shared hit=103
                    ->  Nested Loop Semi Join  (cost=31.12..150.41 rows=1 width=140) (actual time=1.470..1.471 rows=0 loops=1)
                          Buffers: shared hit=103
                          ->  Nested Loop Semi Join  (cost=30.84..149.84 rows=1 width=124) (actual time=1.470..1.471 rows=0 loops=1)
                                Buffers: shared hit=103
                                ->  Nested Loop Semi Join  (cost=30.56..148.84 rows=1 width=108) (actual time=1.469..1.470 rows=0 loops=1)
                                      Buffers: shared hit=103
                                      ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=30.28..140.53 rows=1 width=92) (actual time=1.469..1.469 rows=0 loops=1)
                                            Recheck Cond: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Filter: ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text))
                                            Rows Removed by Filter: 600
                                            Heap Blocks: exact=96
                                            Buffers: shared hit=103
                                            ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_Status_Id"  (cost=0.00..30.28 rows=600 width=0) (actual time=0.036..0.036 rows=600 loops=1)
                                                  Index Cond: (("IsActive" = true) AND (("Status")::text = ANY ('{available}'::text[])))
                                                  Buffers: shared hit=7
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0aaf5-b2a3-7916-9dc9-09df2ee670de}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.42 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0aaf5-b2a2-7fe9-9f78-0f1cfaf382d3}'::uuid[])) AND ("LevelId" = ANY ('{01a0aaf5-b2a3-77f9-83b0-7bd65863e383}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0aaf5-b2a2-78cb-a989-60938beb7395}'::uuid[]))
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
Planning Time: 1.831 ms
Execution Time: 1.582 ms
```

