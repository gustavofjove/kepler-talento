# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Unfiltered first page

Execution time: 0.6 ms

```
Limit  (cost=0.40..419.29 rows=25 width=108) (actual time=0.533..0.560 rows=25 loops=1)
  Buffers: shared hit=142
  ->  Result  (cost=0.40..50267.47 rows=3000 width=108) (actual time=0.533..0.558 rows=25 loops=1)
        Buffers: shared hit=142
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=91) (actual time=0.032..0.033 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=6
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.014..0.019 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=6
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.246 rows=2000 loops=1)
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
Planning Time: 0.130 ms
Execution Time: 0.630 ms
```

## Free text (leading wildcard)

Execution time: 6.5 ms

```
Limit  (cost=163.51..180.11 rows=1 width=108) (actual time=6.448..6.450 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=108) (actual time=6.447..6.449 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=91) (actual time=6.447..6.448 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=91) (actual time=6.440..6.441 rows=0 loops=1)
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
Planning Time: 0.394 ms
Execution Time: 6.507 ms
```

## Status subset and primary CV

Execution time: 0.7 ms

```
Limit  (cost=1.84..449.72 rows=25 width=108) (actual time=0.563..0.604 rows=25 loops=1)
  Buffers: shared hit=272
  ->  Result  (cost=1.84..14333.97 rows=800 width=108) (actual time=0.562..0.602 rows=25 loops=1)
        Buffers: shared hit=272
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=91) (actual time=0.079..0.080 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=128
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=91) (actual time=0.020..0.066 rows=26 loops=1)
                    Buffers: shared hit=128
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=91) (actual time=0.010..0.031 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=24
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.004..0.236 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.002..0.002 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.242 ms
Execution Time: 0.682 ms
```

## Skill ANY across three values

Execution time: 2.6 ms

```
Limit  (cost=28.89..1159.76 rows=25 width=108) (actual time=2.326..2.356 rows=25 loops=1)
  Buffers: shared hit=406
  ->  Result  (cost=28.89..118770.59 rows=2625 width=108) (actual time=2.325..2.353 rows=25 loops=1)
        Buffers: shared hit=406
        ->  Incremental Sort  (cost=28.89..75195.59 rows=2625 width=91) (actual time=1.628..1.631 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=270
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75077.47 rows=2625 width=91) (actual time=1.585..1.604 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Buffers: shared hit=270
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=29.71..184.02 rows=2249 width=16) (actual time=0.051..0.387 rows=2249 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7e72-b85c-cf264f1a6e4c}'::uuid[]))
                            Heap Blocks: exact=129
                            Buffers: shared hit=132
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..29.15 rows=2249 width=0) (actual time=0.035..0.035 rows=2249 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7e72-b85c-cf264f1a6e4c}'::uuid[]))
                                  Buffers: shared hit=3
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=29.69..183.96 rows=2246 width=16) (actual time=0.044..0.420 rows=2246 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7293-90a1-008096d1e94a}'::uuid[]))
                            Heap Blocks: exact=129
                            Buffers: shared hit=132
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..29.13 rows=2246 width=0) (actual time=0.034..0.034 rows=2246 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7293-90a1-008096d1e94a}'::uuid[]))
                                  Buffers: shared hit=3
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=29.61..183.75 rows=2235 width=16) (never executed)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7cf4-a957-2d943cf71fe7}'::uuid[]))
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..29.05 rows=2235 width=0) (never executed)
                                  Index Cond: ("SkillId" = ANY ('{01a08c4f-9ca3-7cf4-a957-2d943cf71fe7}'::uuid[]))
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.010..0.336 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=67
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=67
Planning:
  Buffers: shared hit=10
Planning Time: 0.272 ms
Execution Time: 2.565 ms
```

## Skill ALL across three values

Execution time: 1.4 ms

```
Limit  (cost=7.50..582.03 rows=25 width=108) (actual time=1.096..1.125 rows=25 loops=1)
  Buffers: shared hit=1472
  ->  Result  (cost=7.50..9912.47 rows=431 width=108) (actual time=1.095..1.122 rows=25 loops=1)
        Buffers: shared hit=1472
        ->  Incremental Sort  (cost=7.50..2757.87 rows=431 width=91) (actual time=0.612..0.613 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=1336
              ->  Nested Loop Semi Join  (cost=1.14..2738.48 rows=431 width=91) (actual time=0.059..0.548 rows=26 loops=1)
                    Join Filter: (c0."CandidateId" = c1."CandidateId")
                    Buffers: shared hit=1336
                    ->  Nested Loop Semi Join  (cost=0.85..2417.77 rows=575 width=123) (actual time=0.051..0.493 rows=50 loops=1)
                          Join Filter: (c0."CandidateId" = c2."CandidateId")
                          Buffers: shared hit=1177
                          ->  Nested Loop Semi Join  (cost=0.57..1987.19 rows=772 width=107) (actual time=0.044..0.423 rows=76 loops=1)
                                Buffers: shared hit=939
                                ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.028..0.082 rows=284 loops=1)
                                      Index Cond: ("IsActive" = true)
                                      Buffers: shared hit=44
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c0  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=284)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("SkillId" = ANY ('{01a08c4f-9ca3-7e72-b85c-cf264f1a6e4c}'::uuid[])) AND ("LevelId" = ANY ('{01a08c4f-9ca3-7104-949a-5f8910de11ab}'::uuid[])))
                                      Rows Removed by Filter: 3
                                      Buffers: shared hit=895
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=76)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a08c4f-9ca3-7cf4-a957-2d943cf71fe7}'::uuid[]))
                                Rows Removed by Filter: 2
                                Buffers: shared hit=238
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=50)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a08c4f-9ca3-7293-90a1-008096d1e94a}'::uuid[]))
                          Rows Removed by Filter: 2
                          Buffers: shared hit=159
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.009..0.226 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=69
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=67
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=67
Planning:
  Buffers: shared hit=67
Planning Time: 1.179 ms
Execution Time: 1.433 ms
```

## Every family combined

Execution time: 1.6 ms

```
Limit  (cost=176.91..193.51 rows=1 width=108) (actual time=1.500..1.501 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=176.91..193.51 rows=1 width=108) (actual time=1.499..1.501 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=176.91..176.91 rows=1 width=91) (actual time=1.499..1.500 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Nested Loop Semi Join  (cost=1.12..176.90 rows=1 width=91) (actual time=1.494..1.494 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c3."CandidateId")
                    Buffers: shared hit=96
                    ->  Nested Loop Semi Join  (cost=0.84..176.48 rows=1 width=139) (actual time=1.493..1.494 rows=0 loops=1)
                          Join Filter: (c0."CandidateId" = c2."CandidateId")
                          Buffers: shared hit=96
                          ->  Nested Loop Semi Join  (cost=0.56..176.05 rows=1 width=123) (actual time=1.493..1.494 rows=0 loops=1)
                                Join Filter: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=96
                                ->  Nested Loop Semi Join  (cost=0.29..175.56 rows=1 width=107) (actual time=1.493..1.494 rows=0 loops=1)
                                      Buffers: shared hit=96
                                      ->  Seq Scan on "CND_Candidates" c  (cost=0.00..167.25 rows=1 width=91) (actual time=1.493..1.493 rows=0 loops=1)
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])) AND ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text)))
                                            Rows Removed by Filter: 3000
                                            Buffers: shared hit=96
                                      ->  Index Only Scan using "UX_CND_CandidateSkills_CandidateId_SkillId" on "CND_CandidateSkills" c1  (cost=0.29..8.30 rows=1 width=16) (never executed)
                                            Index Cond: (("CandidateId" = c."Id") AND ("SkillId" = ANY ('{01a08c4f-9ca3-7e72-b85c-cf264f1a6e4c}'::uuid[])))
                                            Heap Fetches: 0
                                ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.48 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c1."CandidateId")
                                      Heap Fetches: 0
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.41 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c1."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a08c4f-9ca3-75d9-9bdd-2f3d027ae787}'::uuid[])) AND ("LevelId" = ANY ('{01a08c4f-9ca3-769a-8919-9549adef4b28}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.41 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c1."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a08c4f-9ca3-71fb-875b-b0256fcbd136}'::uuid[]))
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
Planning Time: 1.362 ms
Execution Time: 1.572 ms
```

