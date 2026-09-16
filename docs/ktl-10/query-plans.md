# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Unfiltered first page

Execution time: 0.7 ms

```
Limit  (cost=0.40..419.29 rows=25 width=108) (actual time=0.577..0.606 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=108) (actual time=0.576..0.603 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=91) (actual time=0.050..0.051 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.020..0.025 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.253 rows=2000 loops=1)
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
Planning Time: 0.209 ms
Execution Time: 0.700 ms
```

## Free text (leading wildcard)

Execution time: 8.2 ms

```
Limit  (cost=163.51..180.11 rows=1 width=108) (actual time=8.149..8.151 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=108) (actual time=8.148..8.150 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=91) (actual time=8.147..8.147 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=91) (actual time=8.136..8.137 rows=0 loops=1)
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
Planning Time: 0.477 ms
Execution Time: 8.233 ms
```

## Status subset and primary CV

Execution time: 0.7 ms

```
Limit  (cost=1.84..449.72 rows=25 width=108) (actual time=0.581..0.611 rows=25 loops=1)
  Buffers: shared hit=258
  ->  Result  (cost=1.84..14333.97 rows=800 width=108) (actual time=0.580..0.608 rows=25 loops=1)
        Buffers: shared hit=258
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=91) (actual time=0.087..0.089 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=114
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=91) (actual time=0.026..0.072 rows=26 loops=1)
                    Buffers: shared hit=114
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=91) (actual time=0.014..0.032 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=10
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.235 rows=2000 loops=1)
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
Planning Time: 0.322 ms
Execution Time: 0.717 ms
```

## Skill ANY across three values

Execution time: 1.3 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=108) (actual time=1.035..1.064 rows=25 loops=1)
  Buffers: shared hit=523
  ->  Result  (cost=28.89..118770.59 rows=2625 width=108) (actual time=1.034..1.062 rows=25 loops=1)
        Buffers: shared hit=523
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=91) (actual time=0.551..0.554 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=385
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=91) (actual time=0.519..0.539 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=385
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.031..0.149 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0a972-6296-7b82-9222-97a2544cf35e}'::uuid[]))
                            Heap Blocks: exact=122
                            Buffers: shared hit=124
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.019..0.019 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0a972-6296-7b82-9222-97a2544cf35e}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.023..0.096 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0a972-6296-75db-9992-059efcbbab59}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.014..0.014 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0a972-6296-75db-9992-059efcbbab59}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.022..0.095 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0a972-6296-7d9b-ab24-2d61790a03df}'::uuid[]))
                            Heap Blocks: exact=127
                            Buffers: shared hit=129
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.012..0.012 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0a972-6296-7d9b-ab24-2d61790a03df}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.229 rows=2000 loops=1)
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
Planning Time: 0.266 ms
Execution Time: 1.272 ms
```

## Skill ALL across three values

Execution time: 1.1 ms

```
Limit  (cost=339.43..372.64 rows=2 width=108) (actual time=0.951..0.954 rows=0 loops=1)
  Buffers: shared hit=582
  ->  Result  (cost=339.43..372.64 rows=2 width=108) (actual time=0.950..0.953 rows=0 loops=1)
        Buffers: shared hit=582
        ->  Sort  (cost=339.43..339.44 rows=2 width=91) (actual time=0.949..0.952 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=582
              ->  Nested Loop Semi Join  (cost=142.86..339.42 rows=2 width=91) (actual time=0.943..0.945 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=582
                    ->  Nested Loop Semi Join  (cost=142.57..332.73 rows=12 width=123) (actual time=0.218..0.928 rows=10 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=552
                          ->  Hash Semi Join  (cost=142.29..277.51 rows=99 width=107) (actual time=0.171..0.712 rows=108 loops=1)
                                Hash Cond: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=220
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=91) (actual time=0.007..0.393 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=141.05..141.05 rows=99 width=16) (actual time=0.156..0.157 rows=108 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 14kB
                                      Buffers: shared hit=124
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.21..141.05 rows=99 width=16) (actual time=0.030..0.138 rows=108 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0a972-6296-7b82-9222-97a2544cf35e}'::uuid[]))
                                            Filter: ("LevelId" = ANY ('{01a0a972-6297-7fd0-b664-db7644e5d08d}'::uuid[]))
                                            Rows Removed by Filter: 279
                                            Heap Blocks: exact=122
                                            Buffers: shared hit=124
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.019..0.019 rows=387 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0a972-6296-7b82-9222-97a2544cf35e}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=108)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a0a972-6296-75db-9992-059efcbbab59}'::uuid[]))
                                Rows Removed by Filter: 3
                                Buffers: shared hit=332
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=10)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a0a972-6296-7d9b-ab24-2d61790a03df}'::uuid[]))
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
Planning Time: 1.020 ms
Execution Time: 1.079 ms
```

## Every family combined

Execution time: 1.8 ms

```
Limit  (cost=177.70..194.30 rows=1 width=108) (actual time=1.686..1.689 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=177.70..194.30 rows=1 width=108) (actual time=1.685..1.688 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=177.70..177.70 rows=1 width=91) (actual time=1.684..1.686 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Nested Loop Semi Join  (cost=1.12..177.69 rows=1 width=91) (actual time=1.674..1.675 rows=0 loops=1)
                    Buffers: shared hit=96
                    ->  Nested Loop Semi Join  (cost=0.84..177.13 rows=1 width=139) (actual time=1.673..1.675 rows=0 loops=1)
                          Buffers: shared hit=96
                          ->  Nested Loop Semi Join  (cost=0.56..176.56 rows=1 width=123) (actual time=1.673..1.674 rows=0 loops=1)
                                Buffers: shared hit=96
                                ->  Nested Loop Semi Join  (cost=0.28..175.56 rows=1 width=107) (actual time=1.673..1.674 rows=0 loops=1)
                                      Buffers: shared hit=96
                                      ->  Seq Scan on "CND_Candidates" c  (cost=0.00..167.25 rows=1 width=91) (actual time=1.672..1.673 rows=0 loops=1)
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])) AND ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text)))
                                            Rows Removed by Filter: 3000
                                            Buffers: shared hit=96
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0a972-6296-7b82-9222-97a2544cf35e}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.42 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0a972-61ff-7200-9bac-edcfec036228}'::uuid[])) AND ("LevelId" = ANY ('{01a0a972-6297-752c-ad4b-10d5b9d8c68c}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0a972-6295-7aca-a048-20e90d3c4181}'::uuid[]))
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
Planning Time: 2.006 ms
Execution Time: 1.787 ms
```

