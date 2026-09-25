# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 7.7 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=7.290..7.416 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.549..7.279 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=2.856..2.973 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.015..0.917 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.278 rows=2000 loops=1)
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
Planning Time: 0.400 ms
Execution Time: 7.652 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 6.5 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=6.015..6.170 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=2.311..6.005 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=1.725..1.851 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.007..0.509 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.262 rows=2000 loops=1)
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
Planning Time: 0.145 ms
Execution Time: 6.520 ms
```

## Deep page 30 sorted by LastName Ascending

Execution time: 6.5 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=6.224..6.403 rows=100 loops=1)
  Buffers: shared hit=8767
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.661..6.191 rows=3000 loops=1)
        Buffers: shared hit=8767
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.057..1.929 rows=3000 loops=1)
              Sort Key: c."LastName", c."FirstName", c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=698
              ->  Index Scan using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.014..0.706 rows=3000 loops=1)
                    Buffers: shared hit=698
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.283 rows=2000 loops=1)
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
Planning Time: 0.202 ms
Execution Time: 6.521 ms
```

## Deep page 30 sorted by LastName Descending

Execution time: 7.6 ms

```
Limit  (cost=48783.78..50465.96 rows=100 width=109) (actual time=7.139..7.389 rows=100 loops=1)
  Buffers: shared hit=8791
  ->  Result  (cost=0.47..50465.96 rows=3000 width=109) (actual time=0.636..7.262 rows=3000 loops=1)
        Buffers: shared hit=8791
        ->  Incremental Sort  (cost=0.47..665.96 rows=3000 width=92) (actual time=0.063..2.327 rows=3000 loops=1)
              Sort Key: c."LastName" DESC, c."FirstName" DESC, c."Id"
              Presorted Key: c."LastName", c."FirstName"
              Full-sort Groups: 94  Sort Method: quicksort  Average Memory: 29kB  Peak Memory: 29kB
              Buffers: shared hit=722
              ->  Index Scan Backward using "IX_CND_Candidates_LastName_FirstName" on "CND_Candidates" c  (cost=0.28..530.96 rows=3000 width=92) (actual time=0.023..0.898 rows=3000 loops=1)
                    Buffers: shared hit=722
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.266 rows=2000 loops=1)
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
Planning Time: 0.169 ms
Execution Time: 7.592 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 8.1 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=7.821..7.944 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=3.865..7.819 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=3.248..3.385 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.008..0.600 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.015..0.280 rows=2000 loops=1)
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
Planning Time: 0.133 ms
Execution Time: 8.096 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 10.1 ms

```
Limit  (cost=48446.51..50106.76 rows=100 width=109) (actual time=9.652..9.803 rows=100 loops=1)
  Buffers: shared hit=8165
  ->  Result  (cost=299.26..50106.76 rows=3000 width=109) (actual time=5.118..9.655 rows=3000 loops=1)
        Buffers: shared hit=8165
        ->  Sort  (cost=299.26..306.76 rows=3000 width=92) (actual time=4.346..4.502 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 495kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.017..0.974 rows=3000 loops=1)
                    Buffers: shared hit=96
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.022..0.338 rows=2000 loops=1)
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
Planning Time: 0.523 ms
Execution Time: 10.147 ms
```

## Unfiltered first page

Execution time: 1.9 ms

```
Limit  (cost=0.40..419.29 rows=25 width=109) (actual time=1.570..1.645 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=109) (actual time=1.567..1.639 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=92) (actual time=0.092..0.096 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=92) (actual time=0.045..0.058 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.012..0.655 rows=2000 loops=1)
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
Planning Time: 0.821 ms
Execution Time: 1.943 ms
```

## Free text (leading wildcard)

Execution time: 9.1 ms

```
Limit  (cost=163.51..180.11 rows=1 width=109) (actual time=9.065..9.068 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=109) (actual time=9.064..9.067 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=92) (actual time=9.063..9.064 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=92) (actual time=9.054..9.055 rows=0 loops=1)
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
Planning Time: 0.526 ms
Execution Time: 9.138 ms
```

## Status subset and primary CV

Execution time: 1.8 ms

```
Limit  (cost=1.84..449.72 rows=25 width=109) (actual time=1.545..1.613 rows=25 loops=1)
  Buffers: shared hit=256
  ->  Result  (cost=1.84..14333.97 rows=800 width=109) (actual time=1.543..1.608 rows=25 loops=1)
        Buffers: shared hit=256
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=92) (actual time=0.215..0.222 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=112
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=92) (actual time=0.080..0.170 rows=26 loops=1)
                    Buffers: shared hit=112
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=92) (actual time=0.037..0.072 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=8
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.020..0.610 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.890 ms
Execution Time: 1.843 ms
```

## Skill ANY across three values

Execution time: 1.6 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=109) (actual time=1.357..1.392 rows=25 loops=1)
  Buffers: shared hit=522
  ->  Result  (cost=28.89..118770.59 rows=2625 width=109) (actual time=1.356..1.389 rows=25 loops=1)
        Buffers: shared hit=522
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=92) (actual time=0.736..0.740 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=384
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=92) (actual time=0.695..0.717 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=384
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.037..0.174 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7b24-8809-ac5391591195}'::uuid[]))
                            Heap Blocks: exact=122
                            Buffers: shared hit=124
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.021..0.022 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7b24-8809-ac5391591195}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.027..0.114 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-757b-a5d6-06cf07881f5b}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.017..0.017 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-757b-a5d6-06cf07881f5b}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.029..0.154 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7cb4-bb57-4f5301d5b817}'::uuid[]))
                            Heap Blocks: exact=127
                            Buffers: shared hit=129
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.017..0.017 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7cb4-bb57-4f5301d5b817}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.292 rows=2000 loops=1)
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
Planning Time: 0.293 ms
Execution Time: 1.584 ms
```

## Skill ALL across three values

Execution time: 1.5 ms

```
Limit  (cost=456.15..555.76 rows=6 width=109) (actual time=1.335..1.348 rows=3 loops=1)
  Buffers: shared hit=432
  ->  Result  (cost=456.15..555.76 rows=6 width=109) (actual time=1.334..1.347 rows=3 loops=1)
        Buffers: shared hit=432
        ->  Sort  (cost=456.15..456.16 rows=6 width=92) (actual time=1.300..1.304 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=414
              ->  Nested Loop Semi Join  (cost=290.32..456.07 rows=6 width=92) (actual time=0.655..1.290 rows=3 loops=1)
                    Buffers: shared hit=414
                    ->  Hash Semi Join  (cost=290.04..430.34 rows=46 width=124) (actual time=0.459..1.208 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=351
                          ->  Hash Semi Join  (cost=144.53..283.27 rows=357 width=108) (actual time=0.205..0.938 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=222
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=92) (actual time=0.005..0.475 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.188..0.189 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=126
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.030..0.115 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-757b-a5d6-06cf07881f5b}'::uuid[]))
                                            Heap Blocks: exact=124
                                            Buffers: shared hit=126
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.020..0.020 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-757b-a5d6-06cf07881f5b}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.226..0.226 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=129
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.037..0.168 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7cb4-bb57-4f5301d5b817}'::uuid[]))
                                      Heap Blocks: exact=127
                                      Buffers: shared hit=129
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.020..0.020 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a0d81a-0ad8-7cb4-bb57-4f5301d5b817}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "UX_CND_CandidateSkills_CandidateId_SkillId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=30)
                          Index Cond: (("CandidateId" = c."Id") AND ("SkillId" = ANY ('{01a0d81a-0ad8-7b24-8809-ac5391591195}'::uuid[])))
                          Filter: ("LevelId" = ANY ('{01a0d81a-0ad9-71d5-8ea8-6bcc34ed85af,01a0d81a-0ad9-724c-80c6-bb96a324c421,01a0d81a-0ad9-77ee-99b5-3872c45d5c8b,01a0d81a-0ad9-7d5a-b103-e692d85bcc18}'::uuid[]))
                          Buffers: shared hit=63
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.008..0.008 rows=1 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 3
                Buffers: shared hit=9
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.003..0.003 rows=1 loops=3)
                Buffers: shared hit=9
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=3)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=9
Planning:
  Buffers: shared hit=67
Planning Time: 0.990 ms
Execution Time: 1.504 ms
```

## Every family combined

Execution time: 2.3 ms

```
Limit  (cost=150.99..167.60 rows=1 width=109) (actual time=2.154..2.158 rows=0 loops=1)
  Buffers: shared hit=103
  ->  Result  (cost=150.99..167.60 rows=1 width=109) (actual time=2.152..2.156 rows=0 loops=1)
        Buffers: shared hit=103
        ->  Sort  (cost=150.99..151.00 rows=1 width=92) (actual time=2.152..2.155 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=103
              ->  Nested Loop Semi Join  (cost=31.40..150.98 rows=1 width=92) (actual time=2.142..2.145 rows=0 loops=1)
                    Buffers: shared hit=103
                    ->  Nested Loop Semi Join  (cost=31.12..150.42 rows=1 width=140) (actual time=2.141..2.144 rows=0 loops=1)
                          Buffers: shared hit=103
                          ->  Nested Loop Semi Join  (cost=30.84..149.84 rows=1 width=124) (actual time=2.141..2.143 rows=0 loops=1)
                                Buffers: shared hit=103
                                ->  Nested Loop Semi Join  (cost=30.56..148.84 rows=1 width=108) (actual time=2.141..2.142 rows=0 loops=1)
                                      Buffers: shared hit=103
                                      ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=30.28..140.53 rows=1 width=92) (actual time=2.140..2.141 rows=0 loops=1)
                                            Recheck Cond: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Filter: ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text))
                                            Rows Removed by Filter: 600
                                            Heap Blocks: exact=96
                                            Buffers: shared hit=103
                                            ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_Status_Id"  (cost=0.00..30.28 rows=600 width=0) (actual time=0.054..0.055 rows=600 loops=1)
                                                  Index Cond: (("IsActive" = true) AND (("Status")::text = ANY ('{available}'::text[])))
                                                  Buffers: shared hit=7
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0d81a-0ad8-7b24-8809-ac5391591195}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.43 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0d81a-0a59-7d30-ab96-13d87c080325}'::uuid[])) AND ("LevelId" = ANY ('{01a0d81a-0ad8-7087-92b6-d43ad7b25fdd,01a0d81a-0ad8-772f-83e5-8dd306caafc8,01a0d81a-0ad8-7952-a0eb-dac2eed4b57a,01a0d81a-0ad8-7d8b-afa5-2c5a9213697e,01a0d81a-0ad8-7f0e-b39d-3b2a4eeddc43,01a0d81a-0ad8-7f7a-9b85-08a8917722eb}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0d81a-0ad6-7c34-99f2-d2a03e2f42c6}'::uuid[]))
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
Planning Time: 2.860 ms
Execution Time: 2.311 ms
```

