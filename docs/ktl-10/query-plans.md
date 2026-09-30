# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

Since KTL-33, free text and the last-name order run in the API over decrypted values, so
they issue no SQL of their own worth a plan here. Their evidence is
[`docs/ktl-33/performance.md`](../ktl-33/performance.md).

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 6.0 ms

```
Limit  (cost=48684.51..50344.76 rows=100 width=332) (actual time=5.659..5.809 rows=100 loops=1)
  Buffers: shared hit=8418
  ->  Result  (cost=537.26..50344.76 rows=3000 width=332) (actual time=2.718..5.709 rows=3000 loops=1)
        Buffers: shared hit=8418
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=2.202..2.351 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.931 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.008..0.256 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.134 ms
Execution Time: 5.973 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 5.6 ms

```
Limit  (cost=48684.51..50344.76 rows=100 width=332) (actual time=5.305..5.421 rows=100 loops=1)
  Buffers: shared hit=8418
  ->  Result  (cost=537.26..50344.76 rows=3000 width=332) (actual time=2.420..5.321 rows=3000 loops=1)
        Buffers: shared hit=8418
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=1.910..2.078 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.006..0.749 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.252 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.116 ms
Execution Time: 5.593 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 8.3 ms

```
Limit  (cost=48684.51..50344.76 rows=100 width=332) (actual time=7.999..8.114 rows=100 loops=1)
  Buffers: shared hit=8418
  ->  Result  (cost=537.26..50344.76 rows=3000 width=332) (actual time=3.985..7.998 rows=3000 loops=1)
        Buffers: shared hit=8418
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=2.829..3.176 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.720 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.072..0.565 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.124 ms
Execution Time: 8.318 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 6.8 ms

```
Limit  (cost=48684.51..50344.76 rows=100 width=332) (actual time=6.554..6.653 rows=100 loops=1)
  Buffers: shared hit=8418
  ->  Result  (cost=537.26..50344.76 rows=3000 width=332) (actual time=3.671..6.554 rows=3000 loops=1)
        Buffers: shared hit=8418
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=3.127..3.253 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.768 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.013..0.264 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=3000)
                Buffers: shared hit=8000
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.000..0.000 rows=1 loops=3000)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=8000
Planning:
  Buffers: shared hit=4
Planning Time: 0.153 ms
Execution Time: 6.840 ms
```

## Unfiltered first page

Execution time: 0.7 ms

```
Limit  (cost=0.58..424.06 rows=25 width=332) (actual time=0.574..0.602 rows=25 loops=1)
  Buffers: shared hit=168
  ->  Result  (cost=0.58..50817.84 rows=3000 width=332) (actual time=0.573..0.599 rows=25 loops=1)
        Buffers: shared hit=168
        ->  Incremental Sort  (cost=0.58..1017.84 rows=3000 width=315) (actual time=0.047..0.048 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=17
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..882.84 rows=3000 width=315) (actual time=0.016..0.026 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=17
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.006..0.260 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=67
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c1  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=67
Planning:
  Buffers: shared hit=4
Planning Time: 0.140 ms
Execution Time: 0.689 ms
```

## Status subset and primary CV

Execution time: 0.7 ms

```
Limit  (cost=2.60..469.53 rows=25 width=332) (actual time=0.572..0.601 rows=25 loops=1)
  Buffers: shared hit=326
  ->  Result  (cost=2.60..14944.34 rows=800 width=332) (actual time=0.572..0.598 rows=25 loops=1)
        Buffers: shared hit=326
        ->  Incremental Sort  (cost=2.60..1664.34 rows=800 width=315) (actual time=0.097..0.098 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=167
              ->  Nested Loop Semi Join  (cost=0.56..1628.34 rows=800 width=315) (actual time=0.023..0.081 rows=26 loops=1)
                    Buffers: shared hit=167
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..890.34 rows=1200 width=315) (actual time=0.011..0.043 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=63
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.61 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.234 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                Buffers: shared hit=75
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c2  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=75
Planning:
  Buffers: shared hit=17
Planning Time: 0.266 ms
Execution Time: 0.684 ms
```

## Skill ANY across three values

Execution time: 1.3 ms

```
Limit  (cost=29.10..1165.21 rows=25 width=332) (actual time=1.109..1.138 rows=25 loops=1)
  Buffers: shared hit=580
  ->  Result  (cost=29.10..119320.97 rows=2625 width=332) (actual time=1.108..1.135 rows=25 loops=1)
        Buffers: shared hit=580
        ->  Incremental Sort  (cost=29.10..75745.97 rows=2625 width=315) (actual time=0.544..0.546 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=427
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75627.84 rows=2625 width=315) (actual time=0.502..0.530 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 5).col1)) OR (ANY ("Id" = (hashed SubPlan 7).col1)) OR (ANY ("Id" = (hashed SubPlan 9).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=427
                    SubPlan 5
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.029..0.144 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f161-f9de-7ba3-b2fe-fa9db312d23e}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.017..0.017 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f161-f9de-7ba3-b2fe-fa9db312d23e}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 7
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.022..0.094 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f161-f9de-72ff-8681-47d62b0eecbd}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.012..0.012 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f161-f9de-72ff-8681-47d62b0eecbd}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 9
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.022..0.093 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f161-f9de-7525-89d2-b343f9ba09cf}'::uuid[]))
                            Heap Blocks: exact=126
                            Buffers: shared hit=128
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.012..0.012 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f161-f9de-7525-89d2-b343f9ba09cf}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.003..0.284 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 3
          ->  Limit  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.002 rows=1 loops=25)
                Buffers: shared hit=69
                ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=25)
                      Index Cond: ("CandidateId" = c."Id")
                      Buffers: shared hit=69
Planning:
  Buffers: shared hit=10
Planning Time: 0.256 ms
Execution Time: 1.318 ms
```

## Skill ALL across three values

Execution time: 1.4 ms

```
Limit  (cost=694.15..793.76 rows=6 width=332) (actual time=1.277..1.287 rows=3 loops=1)
  Buffers: shared hit=670
  ->  Result  (cost=694.15..793.76 rows=6 width=332) (actual time=1.276..1.286 rows=3 loops=1)
        Buffers: shared hit=670
        ->  Sort  (cost=694.15..694.16 rows=6 width=315) (actual time=1.249..1.251 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 26kB
              Buffers: shared hit=652
              ->  Nested Loop Semi Join  (cost=290.32..694.07 rows=6 width=315) (actual time=0.612..1.242 rows=3 loops=1)
                    Buffers: shared hit=652
                    ->  Hash Semi Join  (cost=290.04..668.34 rows=46 width=347) (actual time=0.419..1.190 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=589
                          ->  Hash Semi Join  (cost=144.53..521.27 rows=357 width=331) (actual time=0.175..0.944 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=461
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.004..0.600 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=334
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.163..0.164 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=127
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.024..0.103 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0f161-f9de-72ff-8681-47d62b0eecbd}'::uuid[]))
                                            Heap Blocks: exact=125
                                            Buffers: shared hit=127
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.015..0.015 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0f161-f9de-72ff-8681-47d62b0eecbd}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.212..0.212 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=128
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.034..0.159 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a0f161-f9de-7525-89d2-b343f9ba09cf}'::uuid[]))
                                      Heap Blocks: exact=126
                                      Buffers: shared hit=128
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.020..0.020 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a0f161-f9de-7525-89d2-b343f9ba09cf}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "UX_CND_CandidateSkills_CandidateId_SkillId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.001..0.002 rows=0 loops=30)
                          Index Cond: (("CandidateId" = c."Id") AND ("SkillId" = ANY ('{01a0f161-f9de-7ba3-b2fe-fa9db312d23e}'::uuid[])))
                          Filter: ("LevelId" = ANY ('{01a0f161-f9e0-70f3-b6bd-2c6b2995f090,01a0f161-f9e0-72b4-afb8-887c92d7d1b6,01a0f161-f9e0-7a13-93bc-32577731613b,01a0f161-f9e0-7cd2-8a3b-eca9902a950a}'::uuid[]))
                          Buffers: shared hit=63
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.008..0.008 rows=1 loops=3)
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
Planning Time: 1.105 ms
Execution Time: 1.440 ms
```

## Every SQL family combined

Execution time: 0.4 ms

```
Limit  (cost=407.41..424.01 rows=1 width=332) (actual time=0.286..0.288 rows=0 loops=1)
  Buffers: shared hit=320
  ->  Result  (cost=407.41..424.01 rows=1 width=332) (actual time=0.285..0.287 rows=0 loops=1)
        Buffers: shared hit=320
        ->  Sort  (cost=407.41..407.41 rows=1 width=315) (actual time=0.285..0.286 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=320
              ->  Nested Loop Semi Join  (cost=55.88..407.40 rows=1 width=315) (actual time=0.277..0.278 rows=0 loops=1)
                    Buffers: shared hit=320
                    ->  Nested Loop Semi Join  (cost=55.60..406.39 rows=1 width=363) (actual time=0.217..0.270 rows=1 loops=1)
                          Buffers: shared hit=317
                          ->  Nested Loop Semi Join  (cost=55.32..405.45 rows=1 width=347) (actual time=0.207..0.259 rows=1 loops=1)
                                Buffers: shared hit=314
                                ->  Nested Loop  (cost=55.04..394.75 rows=15 width=331) (actual time=0.121..0.226 rows=17 loops=1)
                                      Buffers: shared hit=263
                                      ->  HashAggregate  (cost=54.76..55.50 rows=74 width=16) (actual time=0.090..0.096 rows=74 loops=1)
                                            Group Key: c3."CandidateId"
                                            Batches: 1  Memory Usage: 24kB
                                            Buffers: shared hit=41
                                            ->  Bitmap Heap Scan on "CND_CandidatePrograms" c3  (cost=4.85..54.58 rows=74 width=16) (actual time=0.026..0.072 rows=74 loops=1)
                                                  Recheck Cond: ("ProgramId" = ANY ('{01a0f161-f9dc-7eb5-9327-3a0f8fe85c8f}'::uuid[]))
                                                  Heap Blocks: exact=39
                                                  Buffers: shared hit=41
                                                  ->  Bitmap Index Scan on "IX_CND_CandidatePrograms_ProgramId_ProgramFamily"  (cost=0.00..4.83 rows=74 width=0) (actual time=0.017..0.018 rows=74 loops=1)
                                                        Index Cond: ("ProgramId" = ANY ('{01a0f161-f9dc-7eb5-9327-3a0f8fe85c8f}'::uuid[]))
                                                        Buffers: shared hit=2
                                      ->  Index Scan using "PK_CND_Candidates" on "CND_Candidates" c  (cost=0.28..4.68 rows=1 width=315) (actual time=0.002..0.002 rows=0 loops=74)
                                            Index Cond: ("Id" = c3."CandidateId")
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Rows Removed by Filter: 1
                                            Buffers: shared hit=222
                                ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.71 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=17)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("LanguageId" = ANY ('{01a0f161-f9dc-7c10-900b-ce716ad914ff}'::uuid[])) AND ("LevelId" = ANY ('{01a0f161-f9df-78d0-a82d-8a6a67830180,01a0f161-f9df-794c-8dc7-1bfb5ceaf7e2,01a0f161-f9df-7a89-98f2-dbfc76a7b1e0,01a0f161-f9e0-7203-860e-003a0bae5cc8,01a0f161-f9e0-79f2-a646-f8afb61eea12,01a0f161-f9e0-7e03-a2bc-b16f678569ba}'::uuid[])))
                                      Rows Removed by Filter: 1
                                      Buffers: shared hit=51
                          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.92 rows=1 width=16) (actual time=0.009..0.009 rows=1 loops=1)
                                Index Cond: ("CandidateId" = c."Id")
                                Heap Fetches: 1
                                Buffers: shared hit=3
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (actual time=0.008..0.008 rows=0 loops=1)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("SkillId" = ANY ('{01a0f161-f9de-7ba3-b2fe-fa9db312d23e}'::uuid[]))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=3
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
Planning Time: 1.664 ms
Execution Time: 0.421 ms
```

