# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 6.3 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=5.968..6.094 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=2.303..5.962 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=1.681..1.798 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.011..0.524 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.009..0.280 rows=2000 loops=1)
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
Planning Time: 0.240 ms
Execution Time: 6.329 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 8.9 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=8.592..8.719 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=4.191..8.514 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.916..3.093 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.013..1.008 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.018..0.576 rows=2000 loops=1)
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
Execution Time: 8.927 ms
```

## Deep page 30 sorted by LastName Ascending

Execution time: 13.4 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=12.715..13.136 rows=100 loops=1)
  Buffers: shared hit=8789
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=1.179..12.983 rows=3000 loops=1)
        Buffers: shared hit=8789
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.081..3.557 rows=3000 loops=1)
              Sort Key: c."LastName", c."FirstName", c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=720
              ->  Index Scan using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.029..1.632 rows=3000 loops=1)
                    Buffers: shared hit=720
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.501 rows=2000 loops=1)
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
Planning Time: 0.232 ms
Execution Time: 13.420 ms
```

## Deep page 30 sorted by LastName Descending

Execution time: 6.6 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=6.276..6.455 rows=100 loops=1)
  Buffers: shared hit=8813
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.668..6.323 rows=3000 loops=1)
        Buffers: shared hit=8813
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.054..1.995 rows=3000 loops=1)
              Sort Key: c."LastName" DESC, c."FirstName" DESC, c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=744
              ->  Index Scan Backward using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.021..0.739 rows=3000 loops=1)
                    Buffers: shared hit=744
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.282 rows=2000 loops=1)
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
Planning Time: 0.151 ms
Execution Time: 6.582 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 13.4 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=12.771..13.039 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=5.912..12.895 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=4.711..5.059 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.016..1.086 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.034..0.547 rows=2000 loops=1)
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
Planning Time: 0.388 ms
Execution Time: 13.371 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 9.5 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=9.169..9.297 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=5.394..9.168 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=4.063..4.195 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.009..0.765 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.018..0.562 rows=2000 loops=1)
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
Planning Time: 0.152 ms
Execution Time: 9.532 ms
```

## Unfiltered first page

Execution time: 0.8 ms

```
Limit  (cost=0.40..419.29 rows=25 width=109) (actual time=0.617..0.650 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=109) (actual time=0.616..0.647 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=92) (actual time=0.041..0.042 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=92) (actual time=0.017..0.022 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.007..0.266 rows=2000 loops=1)
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
Planning Time: 0.256 ms
Execution Time: 0.763 ms
```

## Free text (leading wildcard)

Execution time: 10.2 ms

```
Limit  (cost=163.51..180.11 rows=1 width=109) (actual time=10.118..10.121 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=109) (actual time=10.117..10.119 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=92) (actual time=10.116..10.116 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=92) (actual time=10.097..10.097 rows=0 loops=1)
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
Planning Time: 0.974 ms
Execution Time: 10.227 ms
```

## Status subset and primary CV

Execution time: 1.7 ms

```
Limit  (cost=1.84..449.72 rows=25 width=109) (actual time=1.391..1.452 rows=25 loops=1)
  Buffers: shared hit=254
  ->  Result  (cost=1.84..14333.97 rows=800 width=109) (actual time=1.390..1.449 rows=25 loops=1)
        Buffers: shared hit=254
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=92) (actual time=0.190..0.193 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=110
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=92) (actual time=0.070..0.161 rows=26 loops=1)
                    Buffers: shared hit=110
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=92) (actual time=0.033..0.067 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=6
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.015..0.489 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.003 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.646 ms
Execution Time: 1.670 ms
```

## Skill ANY across three values

Execution time: 3.4 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=109) (actual time=2.402..2.469 rows=25 loops=1)
  Buffers: shared hit=526
  ->  Result  (cost=28.89..118770.59 rows=2625 width=109) (actual time=2.400..2.464 rows=25 loops=1)
        Buffers: shared hit=526
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=92) (actual time=1.212..1.219 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=388
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=92) (actual time=1.148..1.184 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=388
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.069..0.301 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7b0b-ad6c-ccd8ecd61d23}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.044..0.044 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7b0b-ad6c-ccd8ecd61d23}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.093..0.238 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7a80-bafc-4c2b97480cc4}'::uuid[]))
                            Heap Blocks: exact=126
                            Buffers: shared hit=128
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.042..0.042 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7a80-bafc-4c2b97480cc4}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.066..0.210 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7331-babf-ca0e6764e065}'::uuid[]))
                            Heap Blocks: exact=126
                            Buffers: shared hit=128
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.043..0.043 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7331-babf-ca0e6764e065}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.012..0.506 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=25)
                Buffers: shared hit=69
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=69
Planning:
  Buffers: shared hit=10
Planning Time: 0.677 ms
Execution Time: 3.396 ms
```

## Skill ALL across three values

Execution time: 2.0 ms

```
Limit  (cost=339.43..372.64 rows=2 width=109) (actual time=1.736..1.739 rows=0 loops=1)
  Buffers: shared hit=580
  ->  Result  (cost=339.43..372.64 rows=2 width=109) (actual time=1.734..1.737 rows=0 loops=1)
        Buffers: shared hit=580
        ->  Sort  (cost=339.43..339.44 rows=2 width=92) (actual time=1.733..1.735 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=580
              ->  Nested Loop Semi Join  (cost=142.86..339.42 rows=2 width=92) (actual time=1.717..1.719 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=580
                    ->  Nested Loop Semi Join  (cost=142.57..332.73 rows=12 width=124) (actual time=0.432..1.676 rows=10 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=550
                          ->  Hash Semi Join  (cost=142.29..277.51 rows=99 width=108) (actual time=0.330..1.385 rows=108 loops=1)
                                Hash Cond: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=223
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.013..0.804 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=141.05..141.05 rows=99 width=16) (actual time=0.298..0.299 rows=108 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 14kB
                                      Buffers: shared hit=127
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.21..141.05 rows=99 width=16) (actual time=0.069..0.255 rows=108 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7b0b-ad6c-ccd8ecd61d23}'::uuid[]))
                                            Filter: ("LevelId" = ANY ('{01a0aee8-10ce-75ac-9fc5-19033ccc3d56}'::uuid[]))
                                            Rows Removed by Filter: 279
                                            Heap Blocks: exact=125
                                            Buffers: shared hit=127
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.044..0.044 rows=387 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0aee8-10cc-7b0b-ad6c-ccd8ecd61d23}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=108)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a0aee8-10cc-7a80-bafc-4c2b97480cc4}'::uuid[]))
                                Rows Removed by Filter: 3
                                Buffers: shared hit=327
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.004..0.004 rows=0 loops=10)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a0aee8-10cc-7331-babf-ca0e6764e065}'::uuid[]))
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
Planning Time: 2.227 ms
Execution Time: 2.028 ms
```

## Every family combined

Execution time: 1.8 ms

```
Limit  (cost=150.98..167.59 rows=1 width=109) (actual time=1.677..1.680 rows=0 loops=1)
  Buffers: shared hit=103
  ->  Result  (cost=150.98..167.59 rows=1 width=109) (actual time=1.676..1.679 rows=0 loops=1)
        Buffers: shared hit=103
        ->  Sort  (cost=150.98..150.99 rows=1 width=92) (actual time=1.675..1.677 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=103
              ->  Nested Loop Semi Join  (cost=31.40..150.97 rows=1 width=92) (actual time=1.667..1.668 rows=0 loops=1)
                    Buffers: shared hit=103
                    ->  Nested Loop Semi Join  (cost=31.12..150.41 rows=1 width=140) (actual time=1.666..1.668 rows=0 loops=1)
                          Buffers: shared hit=103
                          ->  Nested Loop Semi Join  (cost=30.84..149.84 rows=1 width=124) (actual time=1.666..1.667 rows=0 loops=1)
                                Buffers: shared hit=103
                                ->  Nested Loop Semi Join  (cost=30.56..148.84 rows=1 width=108) (actual time=1.666..1.667 rows=0 loops=1)
                                      Buffers: shared hit=103
                                      ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=30.28..140.53 rows=1 width=92) (actual time=1.665..1.666 rows=0 loops=1)
                                            Recheck Cond: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Filter: ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text))
                                            Rows Removed by Filter: 600
                                            Heap Blocks: exact=96
                                            Buffers: shared hit=103
                                            ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_Status_Id"  (cost=0.00..30.28 rows=600 width=0) (actual time=0.044..0.045 rows=600 loops=1)
                                                  Index Cond: (("IsActive" = true) AND (("Status")::text = ANY ('{available}'::text[])))
                                                  Buffers: shared hit=7
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0aee8-10cc-7b0b-ad6c-ccd8ecd61d23}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.42 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0aee8-106c-7587-ab1d-51c5319fc166}'::uuid[])) AND ("LevelId" = ANY ('{01a0aee8-10cd-764b-b0e2-582957bff3c9}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0aee8-10c9-7abd-8946-8c9928d7e9bc}'::uuid[]))
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
Planning Time: 3.756 ms
Execution Time: 1.821 ms
```

