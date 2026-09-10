# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

## Unfiltered first page

Execution time: 0.7 ms

```
Limit  (cost=0.40..419.29 rows=25 width=108) (actual time=0.561..0.588 rows=25 loops=1)
  Buffers: shared hit=140
  ->  Result  (cost=0.40..50267.47 rows=3000 width=108) (actual time=0.560..0.586 rows=25 loops=1)
        Buffers: shared hit=140
        ->  Incremental Sort  (cost=0.40..467.47 rows=3000 width=91) (actual time=0.047..0.049 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=4
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.017..0.022 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=4
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.008..0.240 rows=2000 loops=1)
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
Planning Time: 0.196 ms
Execution Time: 0.690 ms
```

## Free text (leading wildcard)

Execution time: 6.6 ms

```
Limit  (cost=163.51..180.11 rows=1 width=108) (actual time=6.498..6.500 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=163.51..180.11 rows=1 width=108) (actual time=6.497..6.498 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=163.51..163.51 rows=1 width=91) (actual time=6.496..6.497 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..163.50 rows=1 width=91) (actual time=6.480..6.481 rows=0 loops=1)
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
Planning Time: 0.473 ms
Execution Time: 6.567 ms
```

## Status subset and primary CV

Execution time: 0.7 ms

```
Limit  (cost=1.84..449.72 rows=25 width=108) (actual time=0.568..0.595 rows=25 loops=1)
  Buffers: shared hit=254
  ->  Result  (cost=1.84..14333.97 rows=800 width=108) (actual time=0.567..0.593 rows=25 loops=1)
        Buffers: shared hit=254
        ->  Incremental Sort  (cost=1.84..1053.97 rows=800 width=91) (actual time=0.082..0.083 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=110
              ->  Nested Loop Semi Join  (cost=0.56..1017.97 rows=800 width=91) (actual time=0.025..0.067 rows=26 loops=1)
                    Buffers: shared hit=110
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..339.97 rows=1200 width=91) (actual time=0.013..0.029 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=6
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.56 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.005..0.226 rows=2000 loops=1)
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
Planning Time: 0.282 ms
Execution Time: 0.699 ms
```

## Skill ANY across three values

Execution time: 2.7 ms

```
Limit  (cost=29.02..1163.00 rows=25 width=108) (actual time=2.405..2.433 rows=25 loops=1)
  Buffers: shared hit=536
  ->  Result  (cost=29.02..119097.03 rows=2625 width=108) (actual time=2.404..2.431 rows=25 loops=1)
        Buffers: shared hit=536
        ->  Incremental Sort  (cost=29.02..75522.03 rows=2625 width=91) (actual time=1.904..1.906 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=400
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75403.91 rows=2625 width=91) (actual time=0.650..1.890 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Buffers: shared hit=400
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=30.11..184.99 rows=2300 width=16) (actual time=0.050..0.331 rows=2300 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c0a-cef2-71e6-aac0-c0d1c66bcb06}'::uuid[]))
                            Heap Blocks: exact=129
                            Buffers: shared hit=132
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..29.54 rows=2300 width=0) (actual time=0.035..0.035 rows=2300 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a08c0a-cef2-71e6-aac0-c0d1c66bcb06}'::uuid[]))
                                  Buffers: shared hit=3
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=29.48..183.45 rows=2219 width=16) (actual time=0.041..0.378 rows=2219 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c0a-cef3-77c8-a2f4-3e03f8309bc1}'::uuid[]))
                            Heap Blocks: exact=129
                            Buffers: shared hit=132
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..28.93 rows=2219 width=0) (actual time=0.031..0.031 rows=2219 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a08c0a-cef3-77c8-a2f4-3e03f8309bc1}'::uuid[]))
                                  Buffers: shared hit=3
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=29.58..183.69 rows=2232 width=16) (actual time=0.043..0.257 rows=2232 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a08c0a-cef3-7e7d-b3ef-5b56225cbf2c}'::uuid[]))
                            Heap Blocks: exact=129
                            Buffers: shared hit=132
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..29.02 rows=2232 width=0) (actual time=0.032..0.032 rows=2232 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a08c0a-cef3-7e7d-b3ef-5b56225cbf2c}'::uuid[]))
                                  Buffers: shared hit=3
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.245 rows=2000 loops=1)
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
Planning Time: 0.243 ms
Execution Time: 2.686 ms
```

## Skill ALL across three values

Execution time: 1.7 ms

```
Limit  (cost=7.57..583.96 rows=25 width=108) (actual time=1.507..1.537 rows=25 loops=1)
  Buffers: shared hit=3228
  ->  Result  (cost=7.57..9829.27 rows=426 width=108) (actual time=1.506..1.534 rows=25 loops=1)
        Buffers: shared hit=3228
        ->  Incremental Sort  (cost=7.57..2757.67 rows=426 width=91) (actual time=1.048..1.051 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=3092
              ->  Nested Loop Semi Join  (cost=1.14..2738.50 rows=426 width=91) (actual time=0.045..1.031 rows=26 loops=1)
                    Join Filter: (c0."CandidateId" = c2."CandidateId")
                    Buffers: shared hit=3092
                    ->  Nested Loop Semi Join  (cost=0.85..2418.91 rows=573 width=123) (actual time=0.040..0.955 rows=88 loops=1)
                          Join Filter: (c0."CandidateId" = c1."CandidateId")
                          Buffers: shared hit=2819
                          ->  Nested Loop Semi Join  (cost=0.57..1987.21 rows=774 width=107) (actual time=0.022..0.808 rows=176 loops=1)
                                Buffers: shared hit=2282
                                ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..332.47 rows=3000 width=91) (actual time=0.010..0.125 rows=727 loops=1)
                                      Index Cond: ("IsActive" = true)
                                      Buffers: shared hit=50
                                ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c0  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=727)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("SkillId" = ANY ('{01a08c0a-cef2-71e6-aac0-c0d1c66bcb06}'::uuid[])) AND ("LevelId" = ANY ('{01a08c0a-cef3-70ab-9919-db6c01200778}'::uuid[])))
                                      Rows Removed by Filter: 2
                                      Buffers: shared hit=2232
                          ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=176)
                                Index Cond: ("CandidateId" = c."Id")
                                Filter: ("SkillId" = ANY ('{01a08c0a-cef3-77c8-a2f4-3e03f8309bc1}'::uuid[]))
                                Rows Removed by Filter: 2
                                Buffers: shared hit=537
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c2  (cost=0.29..0.55 rows=1 width=16) (actual time=0.001..0.001 rows=0 loops=88)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: ("SkillId" = ANY ('{01a08c0a-cef3-7e7d-b3ef-5b56225cbf2c}'::uuid[]))
                          Rows Removed by Filter: 2
                          Buffers: shared hit=273
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..89.00 rows=2000 width=16) (actual time=0.006..0.215 rows=2000 loops=1)
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
Planning Time: 0.960 ms
Execution Time: 1.670 ms
```

## Every family combined

Execution time: 1.6 ms

```
Limit  (cost=176.93..193.54 rows=1 width=108) (actual time=1.499..1.501 rows=0 loops=1)
  Buffers: shared hit=96
  ->  Result  (cost=176.93..193.54 rows=1 width=108) (actual time=1.499..1.500 rows=0 loops=1)
        Buffers: shared hit=96
        ->  Sort  (cost=176.93..176.94 rows=1 width=91) (actual time=1.498..1.499 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=96
              ->  Nested Loop Semi Join  (cost=1.12..176.92 rows=1 width=91) (actual time=1.491..1.492 rows=0 loops=1)
                    Join Filter: (c0."CandidateId" = c3."CandidateId")
                    Buffers: shared hit=96
                    ->  Nested Loop Semi Join  (cost=0.84..176.51 rows=1 width=139) (actual time=1.491..1.492 rows=0 loops=1)
                          Join Filter: (c0."CandidateId" = c2."CandidateId")
                          Buffers: shared hit=96
                          ->  Nested Loop Semi Join  (cost=0.56..176.09 rows=1 width=123) (actual time=1.491..1.491 rows=0 loops=1)
                                Join Filter: (c."Id" = c0."CandidateId")
                                Buffers: shared hit=96
                                ->  Nested Loop Semi Join  (cost=0.29..175.60 rows=1 width=107) (actual time=1.491..1.491 rows=0 loops=1)
                                      Buffers: shared hit=96
                                      ->  Seq Scan on "CND_Candidates" c  (cost=0.00..167.25 rows=1 width=91) (actual time=1.490..1.491 rows=0 loops=1)
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])) AND ((("FirstName")::text ~~* '%ez%'::text) OR (("LastName")::text ~~* '%ez%'::text) OR (("Email")::text ~~* '%ez%'::text) OR (("Phone")::text ~~* '%ez%'::text) OR ("Notes" ~~* '%ez%'::text)))
                                            Rows Removed by Filter: 3000
                                            Buffers: shared hit=96
                                      ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..8.34 rows=1 width=16) (never executed)
                                            Index Cond: ("CandidateId" = c."Id")
                                            Filter: ("SkillId" = ANY ('{01a08c0a-cef2-71e6-aac0-c0d1c66bcb06}'::uuid[]))
                                ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.47 rows=1 width=16) (never executed)
                                      Index Cond: ("CandidateId" = c1."CandidateId")
                                      Heap Fetches: 0
                          ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.41 rows=1 width=16) (never executed)
                                Index Cond: ("CandidateId" = c1."CandidateId")
                                Filter: (("LanguageId" = ANY ('{01a08c0a-ce95-723f-ae0b-8e33df80e37f}'::uuid[])) AND ("LevelId" = ANY ('{01a08c0a-cef3-7857-8fda-ff161e887788}'::uuid[])))
                    ->  Index Scan using "IX_CND_CandidatePrograms_CandidateId" on "CND_CandidatePrograms" c3  (cost=0.28..0.40 rows=1 width=16) (never executed)
                          Index Cond: ("CandidateId" = c1."CandidateId")
                          Filter: ("ProgramId" = ANY ('{01a08c0a-cef2-7770-a15f-1db8cd82de4f}'::uuid[]))
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
Planning Time: 1.440 ms
Execution Time: 1.594 ms
```

