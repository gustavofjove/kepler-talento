# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Unfiltered first page

Execution time: 0.9 ms

```
Limit  (cost=0.40..419.29 rows=25 width=108) (actual time=0.750..0.787 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=108) (actual time=0.749..0.784 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=91) (actual time=0.047..0.049 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.022..0.028 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.333 rows=2000 loops=1)
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
Planning Time: 0.243 ms
Execution Time: 0.905 ms
```

## Free text (leading wildcard)

Execution time: 10.9 ms

```
Limit  (cost=163.51..180.11 rows=1 width=108) (actual time=10.758..10.762 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=108) (actual time=10.756..10.760 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=91) (actual time=10.755..10.757 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=91) (actual time=10.745..10.746 rows=0 loops=1)
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
Planning Time: 0.546 ms
Execution Time: 10.860 ms
```

## Status subset and primary CV

Execution time: 0.9 ms

```
Limit  (cost=1.84..449.72 rows=25 width=108) (actual time=0.740..0.779 rows=25 loops=1)
  Buffers: shared hit=258
  ->  Result  (cost=1.84..14333.97 rows=800 width=108) (actual time=0.739..0.776 rows=25 loops=1)
        Buffers: shared hit=258
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=91) (actual time=0.107..0.111 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=114
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=91) (actual time=0.032..0.089 rows=26 loops=1)
                    Buffers: shared hit=114
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=91) (actual time=0.016..0.038 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=10
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.007..0.295 rows=2000 loops=1)
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
Planning Time: 0.534 ms
Execution Time: 0.896 ms
```

## Skill ANY across three values

Execution time: 1.7 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=108) (actual time=1.448..1.486 rows=25 loops=1)
  Buffers: shared hit=520
  ->  Result  (cost=28.89..118770.59 rows=2625 width=108) (actual time=1.446..1.482 rows=25 loops=1)
        Buffers: shared hit=520
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=91) (actual time=0.824..0.828 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=382
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=91) (actual time=0.774..0.803 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=382
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.037..0.171 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aa6b-6174-722f-9580-2a3db26f638a}'::uuid[]))
                            Heap Blocks: exact=122
                            Buffers: shared hit=124
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.021..0.022 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aa6b-6174-722f-9580-2a3db26f638a}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.028..0.131 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aa6b-6174-7b05-8614-fc8076d529c0}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.016..0.016 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aa6b-6174-7b05-8614-fc8076d529c0}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.037..0.198 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0aa6b-6174-7f1b-9ecd-84941e38b68c}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.023..0.023 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0aa6b-6174-7f1b-9ecd-84941e38b68c}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.007..0.305 rows=2000 loops=1)
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
Planning Time: 0.528 ms
Execution Time: 1.725 ms
```

## Skill ALL across three values

Execution time: 1.5 ms

```
Limit  (cost=339.43..372.64 rows=2 width=108) (actual time=1.223..1.227 rows=0 loops=1)
  Buffers: shared hit=584
  ->  Result  (cost=339.43..372.64 rows=2 width=108) (actual time=1.221..1.224 rows=0 loops=1)
        Buffers: shared hit=584
        ->  Sort  (cost=339.43..339.44 rows=2 width=91) (actual time=1.220..1.222 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=584
              ->  Nested Loop Semi Join  (cost=142.86..339.42 rows=2 width=91) (actual time=1.162..1.164 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=584
                    ->  Nested Loop Semi Join  (cost=142.57..332.73 rows=12 width=123) (actual time=0.330..1.142 rows=10 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=554
                          ->  Hash Semi Join  (cost=142.29..277.51 rows=99 width=107) (actual time=0.237..0.947 rows=108 loops=1)
                                Hash Cond: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=220
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..126.00 rows=3000 width=91) (actual time=0.020..0.535 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=96
                                ->  Hash  (cost=141.05..141.05 rows=99 width=16) (actual time=0.205..0.205 rows=108 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 14kB
                                      Buffers: shared hit=124
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.21..141.05 rows=99 width=16) (actual time=0.054..0.183 rows=108 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0aa6b-6174-722f-9580-2a3db26f638a}'::uuid[]))
                                            Filter: ("LevelId" = ANY ('{01a0aa6b-6178-7d68-9fb1-b6edb31b3865}'::uuid[]))
                                            Rows Removed by Filter: 279
                                            Heap Blocks: exact=122
                                            Buffers: shared hit=124
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.037..0.037 rows=387 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0aa6b-6174-722f-9580-2a3db26f638a}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=108)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a0aa6b-6174-7b05-8614-fc8076d529c0}'::uuid[]))
                                Rows Removed by Filter: 3
                                Buffers: shared hit=334
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=10)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a0aa6b-6174-7f1b-9ecd-84941e38b68c}'::uuid[]))
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
Planning Time: 2.147 ms
Execution Time: 1.514 ms
```

## Every family combined

Execution time: 2.4 ms

```
Limit  (cost=177.70..194.30 rows=1 width=108) (actual time=2.291..2.295 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=177.70..194.30 rows=1 width=108) (actual time=2.289..2.293 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=177.70..177.70 rows=1 width=91) (actual time=2.289..2.291 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Nested Loop Semi Join  (cost=1.12..177.69 rows=1 width=91) (actual time=2.279..2.281 rows=0 loops=1)
                    Buffers: shared hit=96
                    ->  Nested Loop Semi Join  (cost=0.84..177.13 rows=1 width=139) (actual time=2.278..2.280 rows=0 loops=1)
                          Buffers: shared hit=96
                          ->  Nested Loop Semi Join  (cost=0.56..176.56 rows=1 width=123) (actual time=2.277..2.279 rows=0 loops=1)
                                Buffers: shared hit=96
                                ->  Nested Loop Semi Join  (cost=0.28..175.56 rows=1 width=107) (actual time=2.277..2.278 rows=0 loops=1)
                                      Buffers: shared hit=96
                                      ->  Seq Scan on "CND_Candidates" c  (cost=0.00..167.25 rows=1 width=91) (actual time=2.276..2.276 rows=0 loops=1)
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])) AND ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text)))
                                            Rows Removed by Filter: 3000
                                            Buffers: shared hit=96
                                      ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..8.29 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Heap Fetches: 0
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c0."CandidateId")
                                      Filter: ("SkillId" = ANY ('{01a0aa6b-6174-722f-9580-2a3db26f638a}'::uuid[]))
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.42 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c0."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a0aa6b-616b-798b-9564-edbfb3f0dbce}'::uuid[])) AND ("LevelId" = ANY ('{01a0aa6b-6177-74ed-b8f0-31c3fd94c294}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.42 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a0aa6b-616d-7aa3-8fcd-90e7185a453f}'::uuid[]))
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
Planning Time: 2.600 ms
Execution Time: 2.396 ms
```

