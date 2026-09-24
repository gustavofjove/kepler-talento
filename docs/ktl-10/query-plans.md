# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 12.5 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=11.714..12.224 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.624..11.912 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.561..2.938 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.010..0.881 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.013..0.505 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.236 ms
Execution Time: 12.534 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 12.5 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=11.999..12.275 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=5.006..12.070 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=3.887..4.137 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.015..1.101 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.017..0.486 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.257 ms
Execution Time: 12.535 ms
```

## Deep page 30 sorted by LastName Ascending

Execution time: 11.8 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=11.442..11.631 rows=100 loops=1)
  Buffers: shared hit=8785
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=1.302..11.481 rows=3000 loops=1)
        Buffers: shared hit=8785
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.103..3.030 rows=3000 loops=1)
              Sort Key: c."LastName", c."FirstName", c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=716
              ->  Index Scan using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.040..1.409 rows=3000 loops=1)
                    Buffers: shared hit=716
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.012..0.563 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.242 ms
Execution Time: 11.783 ms
```

## Deep page 30 sorted by LastName Descending

Execution time: 9.0 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=8.521..8.842 rows=100 loops=1)
  Buffers: shared hit=8809
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=1.338..8.662 rows=3000 loops=1)
        Buffers: shared hit=8809
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.099..2.508 rows=3000 loops=1)
              Sort Key: c."LastName" DESC, c."FirstName" DESC, c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=740
              ->  Index Scan Backward using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.039..0.934 rows=3000 loops=1)
                    Buffers: shared hit=740
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.011..0.608 rows=2000 loops=1)
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
Execution Time: 9.047 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 11.1 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=10.522..10.756 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=4.750..10.587 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=4.052..4.232 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.019..1.017 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.018..0.300 rows=2000 loops=1)
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
Planning Time: 0.238 ms
Execution Time: 11.056 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 12.3 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=11.819..12.054 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=5.703..11.877 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=4.714..4.921 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.019..1.018 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.016..0.465 rows=2000 loops=1)
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
Planning Time: 0.234 ms
Execution Time: 12.300 ms
```

## Unfiltered first page

Execution time: 1.7 ms

```
Limit  (cost=0.40..419.29 rows=25 width=109) (actual time=1.411..1.473 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=109) (actual time=1.409..1.469 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=92) (actual time=0.089..0.093 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=92) (actual time=0.041..0.050 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.014..0.590 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=25)
                Buffers: shared hit=67
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=67
Planning:
  Buffers: shared hit=4
Planning Time: 0.250 ms
Execution Time: 1.663 ms
```

## Free text (leading wildcard)

Execution time: 8.4 ms

```
Limit  (cost=163.51..180.11 rows=1 width=109) (actual time=8.322..8.324 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=109) (actual time=8.320..8.323 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=92) (actual time=8.320..8.321 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=92) (actual time=8.312..8.312 rows=0 loops=1)
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
Planning Time: 0.649 ms
Execution Time: 8.402 ms
```

## Status subset and primary CV

Execution time: 1.6 ms

```
Limit  (cost=1.84..449.72 rows=25 width=109) (actual time=1.245..1.306 rows=25 loops=1)
  Buffers: shared hit=254
  ->  Result  (cost=1.84..14333.97 rows=800 width=109) (actual time=1.244..1.302 rows=25 loops=1)
        Buffers: shared hit=254
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=92) (actual time=0.190..0.193 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=110
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=92) (actual time=0.063..0.157 rows=26 loops=1)
                    Buffers: shared hit=110
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=92) (actual time=0.029..0.066 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=6
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.485 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.723 ms
Execution Time: 1.621 ms
```

## Skill ANY across three values

Execution time: 2.8 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=109) (actual time=2.374..2.432 rows=25 loops=1)
  Buffers: shared hit=523
  ->  Result  (cost=28.89..118770.59 rows=2625 width=109) (actual time=2.372..2.427 rows=25 loops=1)
        Buffers: shared hit=523
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=92) (actual time=1.131..1.136 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=385
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=92) (actual time=1.068..1.108 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=385
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.053..0.291 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d228-9889-73e0-baf0-742f1b324819}'::uuid[]))
                            Heap Blocks: exact=123
                            Buffers: shared hit=125
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.031..0.031 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d228-9889-73e0-baf0-742f1b324819}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.042..0.241 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d228-9889-7004-8686-fc93a7b112ce}'::uuid[]))
                            Heap Blocks: exact=126
                            Buffers: shared hit=128
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.025..0.025 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d228-9889-7004-8686-fc93a7b112ce}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.036..0.201 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d228-9889-79d0-bc6d-0cd1b0bbfa67}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.021..0.021 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d228-9889-79d0-bc6d-0cd1b0bbfa67}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.029..0.586 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                Buffers: shared hit=69
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=69
Planning:
  Buffers: shared hit=10
Planning Time: 1.007 ms
Execution Time: 2.829 ms
```

## Skill ALL across three values

Execution time: 2.3 ms

```
Limit  (cost=339.43..372.64 rows=2 width=109) (actual time=1.970..1.975 rows=0 loops=1)
  Buffers: shared hit=579
  ->  Result  (cost=339.43..372.64 rows=2 width=109) (actual time=1.968..1.972 rows=0 loops=1)
        Buffers: shared hit=579
        ->  Sort  (cost=339.43..339.44 rows=2 width=92) (actual time=1.967..1.970 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=579
              ->  Nested Loop Semi Join  (cost=142.86..339.42 rows=2 width=92) (actual time=1.947..1.950 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=579
                    ->  Nested Loop Semi Join  (cost=142.57..332.73 rows=12 width=124) (actual time=0.627..1.898 rows=10 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=549
                          ->  Hash Semi Join  (cost=142.29..277.51 rows=99 width=108) (actual time=0.467..1.521 rows=108 loops=1)
                                Hash Cond: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=221
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.024..0.808 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=141.05..141.05 rows=99 width=16) (actual time=0.420..0.421 rows=108 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 14kB
                                      Buffers: shared hit=125
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.21..141.05 rows=99 width=16) (actual time=0.083..0.373 rows=108 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0d228-9889-73e0-baf0-742f1b324819}'::uuid[]))
                                            Filter: ("LevelId" = ANY ('{01a0d228-988a-72d8-a889-38b773509d7c}'::uuid[]))
                                            Rows Removed by Filter: 279
                                            Heap Blocks: exact=123
                                            Buffers: shared hit=125
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.054..0.054 rows=387 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0d228-9889-73e0-baf0-742f1b324819}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.003..0.003 rows=0 loops=108)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a0d228-9889-7004-8686-fc93a7b112ce}'::uuid[]))
                                Rows Removed by Filter: 3
                                Buffers: shared hit=328
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.005..0.005 rows=0 loops=10)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a0d228-9889-79d0-bc6d-0cd1b0bbfa67}'::uuid[]))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=30
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
Planning Time: 2.506 ms
Execution Time: 2.329 ms
```

## Every family combined

Execution time: 2.7 ms

```
Limit  (cost=150.98..167.59 rows=1 width=109) (actual time=2.307..2.311 rows=0 loops=1)
  Buffers: shared hit=103
  ->  Result  (cost=150.98..167.59 rows=1 width=109) (actual time=2.304..2.308 rows=0 loops=1)
        Buffers: shared hit=103
        ->  Sort  (cost=150.98..150.99 rows=1 width=92) (actual time=2.304..2.307 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=103
              ->  Nested Loop Semi Join  (cost=31.40..150.97 rows=1 width=92) (actual time=2.287..2.290 rows=0 loops=1)
                    Buffers: shared hit=103
                    ->  Nested Loop Semi Join  (cost=31.12..150.41 rows=1 width=140) (actual time=2.286..2.289 rows=0 loops=1)
                          Buffers: shared hit=103
                          ->  Nested Loop Semi Join  (cost=30.84..149.84 rows=1 width=124) (actual time=2.286..2.288 rows=0 loops=1)
                                Buffers: shared hit=103
                                ->  Nested Loop Semi Join  (cost=30.56..148.84 rows=1 width=108) (actual time=2.285..2.287 rows=0 loops=1)
                                      Buffers: shared hit=103
                                      ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=30.28..140.53 rows=1 width=92) (actual time=2.284..2.285 rows=0 loops=1)
                                            Recheck Cond: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Filter: ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text))
                                            Rows Removed by Filter: 600
                                            Heap Blocks: exact=96
                                            Buffers: shared hit=103
                                            ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_Status_Id"  (cost=0.00..30.28 rows=600 width=0) (actual time=0.070..0.071 rows=600 loops=1)
                                                  Index Cond: (("IsActive" = true) AND (("Status")::text = ANY ('{available}'::text[])))
                                                  Buffers: shared hit=7
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0d228-9889-73e0-baf0-742f1b324819}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.42 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0d228-985c-76e1-b5fa-e4ff84f7b209}'::uuid[])) AND ("LevelId" = ANY ('{01a0d228-9889-7af1-b890-395b7605775f}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0d228-9888-7651-a136-28ed0b7434f1}'::uuid[]))
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
Planning Time: 4.343 ms
Execution Time: 2.678 ms
```

