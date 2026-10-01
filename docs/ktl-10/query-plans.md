# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

Since KTL-33, free text and the last-name order run in the API over decrypted values, so
they issue no SQL of their own worth a plan here. Their evidence is
[`docs/ktl-33/performance.md`](../ktl-33/performance.md).

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 4.4 ms

```
Limit  (cost=72790.76..75282.26 rows=100 width=318) (actual time=4.139..4.168 rows=100 loops=1)
  Buffers: shared hit=586
  ->  Result  (cost=537.26..75282.26 rows=3000 width=318) (actual time=3.305..4.093 rows=3000 loops=1)
        Buffers: shared hit=586
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=2.411..2.640 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.008..0.913 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.252 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.161..0.161 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.212..0.212 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.233 ms
Execution Time: 4.418 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 3.7 ms

```
Limit  (cost=72790.76..75282.26 rows=100 width=318) (actual time=3.525..3.552 rows=100 loops=1)
  Buffers: shared hit=586
  ->  Result  (cost=537.26..75282.26 rows=3000 width=318) (actual time=2.781..3.479 rows=3000 loops=1)
        Buffers: shared hit=586
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=1.982..2.157 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.789 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.008..0.243 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.160..0.160 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.150..0.150 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.218 ms
Execution Time: 3.731 ms
```

## Deep page 30 sorted by Status Ascending

Execution time: 4.7 ms

```
Limit  (cost=72790.76..75282.26 rows=100 width=318) (actual time=4.447..4.474 rows=100 loops=1)
  Buffers: shared hit=586
  ->  Result  (cost=537.26..75282.26 rows=3000 width=318) (actual time=3.770..4.401 rows=3000 loops=1)
        Buffers: shared hit=586
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=2.959..3.088 rows=3000 loops=1)
              Sort Key: c."Status", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.742 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.257 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.155..0.156 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.146..0.147 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.242 ms
Execution Time: 4.651 ms
```

## Deep page 30 sorted by Status Descending

Execution time: 4.7 ms

```
Limit  (cost=72790.76..75282.26 rows=100 width=318) (actual time=4.447..4.472 rows=100 loops=1)
  Buffers: shared hit=586
  ->  Result  (cost=537.26..75282.26 rows=3000 width=318) (actual time=3.756..4.400 rows=3000 loops=1)
        Buffers: shared hit=586
        ->  Sort  (cost=537.26..544.76 rows=3000 width=315) (actual time=2.945..3.072 rows=3000 loops=1)
              Sort Key: c."Status" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=334
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.008..0.781 rows=3000 loops=1)
                    Buffers: shared hit=334
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.245 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.156..0.156 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.145..0.145 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.210 ms
Execution Time: 4.651 ms
```

## Unfiltered first page

Execution time: 1.2 ms

```
Limit  (cost=0.58..631.87 rows=25 width=318) (actual time=1.039..1.050 rows=25 loops=1)
  Buffers: shared hit=271
  ->  Result  (cost=0.58..75755.34 rows=3000 width=318) (actual time=1.038..1.047 rows=25 loops=1)
        Buffers: shared hit=271
        ->  Incremental Sort  (cost=0.58..1017.84 rows=3000 width=315) (actual time=0.055..0.057 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=19
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..882.84 rows=3000 width=315) (actual time=0.019..0.030 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=19
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.007..0.318 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.200..0.200 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.148..0.148 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.244 ms
Execution Time: 1.178 ms
```

## Status subset and primary CV

Execution time: 1.0 ms

```
Limit  (cost=2.60..677.34 rows=25 width=318) (actual time=0.894..0.903 rows=25 loops=1)
  Buffers: shared hit=431
  ->  Result  (cost=2.60..21594.34 rows=800 width=318) (actual time=0.893..0.901 rows=25 loops=1)
        Buffers: shared hit=431
        ->  Incremental Sort  (cost=2.60..1664.34 rows=800 width=315) (actual time=0.099..0.101 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=179
              ->  Nested Loop Semi Join  (cost=0.56..1628.34 rows=800 width=315) (actual time=0.023..0.082 rows=26 loops=1)
                    Buffers: shared hit=179
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..890.34 rows=1200 width=315) (actual time=0.012..0.045 rows=39 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("Status")::text = ANY ('{available,in_process}'::text[]))
                          Rows Removed by Filter: 59
                          Buffers: shared hit=75
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.61 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=39)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=104
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.235 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..124.00 rows=1 width=16) (actual time=0.173..0.173 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..119.00 rows=1 width=16) (actual time=0.152..0.152 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=20
Planning Time: 0.386 ms
Execution Time: 1.020 ms
```

## Skill ANY across three values

Execution time: 1.9 ms

```
Limit  (cost=29.10..1373.03 rows=25 width=318) (actual time=1.688..1.709 rows=25 loops=1)
  Buffers: shared hit=681
  ->  Result  (cost=29.10..141141.28 rows=2625 width=318) (actual time=1.686..1.704 rows=25 loops=1)
        Buffers: shared hit=681
        ->  Incremental Sort  (cost=29.10..75745.97 rows=2625 width=315) (actual time=0.707..0.714 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=429
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75627.84 rows=2625 width=315) (actual time=0.655..0.690 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 8).col1)) OR (ANY ("Id" = (hashed SubPlan 10).col1)) OR (ANY ("Id" = (hashed SubPlan 12).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=429
                    SubPlan 8
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.033..0.168 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7756-8ee1-68097398c6a6}'::uuid[]))
                            Heap Blocks: exact=123
                            Buffers: shared hit=125
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.019..0.019 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7756-8ee1-68097398c6a6}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 10
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.027..0.114 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f67a-a14f-74da-8489-2785b5ba7bfe}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.017..0.017 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f67a-a14f-74da-8489-2785b5ba7bfe}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 12
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.030..0.117 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7696-950c-a046a077d6d1}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.018..0.019 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7696-950c-a046a077d6d1}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.007..0.267 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c4  (cost=0.00..124.00 rows=1 width=16) (actual time=0.192..0.192 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c5  (cost=0.00..119.00 rows=1 width=16) (actual time=0.236..0.236 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=13
Planning Time: 0.454 ms
Execution Time: 1.941 ms
```

## Skill ALL across three values

Execution time: 1.5 ms

```
Limit  (cost=694.25..843.74 rows=6 width=318) (actual time=1.349..1.360 rows=3 loops=1)
  Buffers: shared hit=719
  ->  Result  (cost=694.25..843.74 rows=6 width=318) (actual time=1.348..1.358 rows=3 loops=1)
        Buffers: shared hit=719
        ->  Sort  (cost=694.25..694.26 rows=6 width=315) (actual time=1.315..1.317 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 26kB
              Buffers: shared hit=692
              ->  Nested Loop Semi Join  (cost=290.32..694.17 rows=6 width=315) (actual time=0.672..1.307 rows=3 loops=1)
                    Buffers: shared hit=692
                    ->  Hash Semi Join  (cost=290.04..668.34 rows=46 width=347) (actual time=0.485..1.244 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=587
                          ->  Hash Semi Join  (cost=144.53..521.27 rows=357 width=331) (actual time=0.196..0.954 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=460
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..364.00 rows=3000 width=315) (actual time=0.007..0.581 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=334
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.176..0.176 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=126
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.038..0.132 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a0f67a-a14f-74da-8489-2785b5ba7bfe}'::uuid[]))
                                            Heap Blocks: exact=124
                                            Buffers: shared hit=126
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.027..0.027 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a0f67a-a14f-74da-8489-2785b5ba7bfe}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.257..0.258 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=127
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.032..0.203 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7696-950c-a046a077d6d1}'::uuid[]))
                                      Heap Blocks: exact=125
                                      Buffers: shared hit=127
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.018..0.018 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a0f67a-a14f-7696-950c-a046a077d6d1}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=30)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: (("SkillId" = ANY ('{01a0f67a-a14f-7756-8ee1-68097398c6a6}'::uuid[])) AND ("LevelId" = ANY ('{01a0f67a-a14f-7004-b7cf-a8096b10fb6a,01a0f67a-a14f-7240-b702-46616f625f39,01a0f67a-a14f-7c89-9bb9-fbf3c6af30db,01a0f67a-a14f-7db1-a6dd-b21631ab7248}'::uuid[])))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=105
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.006..0.006 rows=1 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 3
                Buffers: shared hit=9
        SubPlan 3
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.30 rows=1 width=0) (actual time=0.003..0.003 rows=0 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 1
                Buffers: shared hit=9
        SubPlan 5
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c5  (cost=0.28..8.30 rows=1 width=0) (actual time=0.003..0.003 rows=0 loops=3)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 1
                Buffers: shared hit=9
Planning:
  Buffers: shared hit=70
Planning Time: 1.021 ms
Execution Time: 1.524 ms
```

## Every SQL family combined

Execution time: 0.5 ms

```
Limit  (cost=407.41..432.32 rows=1 width=318) (actual time=0.295..0.297 rows=0 loops=1)
  Buffers: shared hit=319
  ->  Result  (cost=407.41..432.32 rows=1 width=318) (actual time=0.294..0.296 rows=0 loops=1)
        Buffers: shared hit=319
        ->  Sort  (cost=407.41..407.41 rows=1 width=315) (actual time=0.294..0.296 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=319
              ->  Nested Loop Semi Join  (cost=55.88..407.40 rows=1 width=315) (actual time=0.287..0.288 rows=0 loops=1)
                    Buffers: shared hit=319
                    ->  Nested Loop Semi Join  (cost=55.60..406.39 rows=1 width=363) (actual time=0.225..0.278 rows=1 loops=1)
                          Buffers: shared hit=315
                          ->  Nested Loop Semi Join  (cost=55.32..405.45 rows=1 width=347) (actual time=0.214..0.266 rows=1 loops=1)
                                Buffers: shared hit=312
                                ->  Nested Loop  (cost=55.04..394.75 rows=15 width=331) (actual time=0.102..0.230 rows=17 loops=1)
                                      Buffers: shared hit=261
                                      ->  HashAggregate  (cost=54.76..55.50 rows=74 width=16) (actual time=0.083..0.090 rows=74 loops=1)
                                            Group Key: c3."CandidateId"
                                            Batches: 1  Memory Usage: 24kB
                                            Buffers: shared hit=39
                                            ->  Bitmap Heap Scan on "CND_CandidatePrograms" c3  (cost=4.85..54.58 rows=74 width=16) (actual time=0.025..0.066 rows=74 loops=1)
                                                  Recheck Cond: ("ProgramId" = ANY ('{01a0f67a-a14f-7a56-bff3-c0c26a16461b}'::uuid[]))
                                                  Heap Blocks: exact=37
                                                  Buffers: shared hit=39
                                                  ->  Bitmap Index Scan on "IX_CND_CandidatePrograms_ProgramId_ProgramFamily"  (cost=0.00..4.83 rows=74 width=0) (actual time=0.017..0.017 rows=74 loops=1)
                                                        Index Cond: ("ProgramId" = ANY ('{01a0f67a-a14f-7a56-bff3-c0c26a16461b}'::uuid[]))
                                                        Buffers: shared hit=2
                                      ->  Index Scan using "PK_CND_Candidates" on "CND_Candidates" c  (cost=0.28..4.68 rows=1 width=315) (actual time=0.002..0.002 rows=0 loops=74)
                                            Index Cond: ("Id" = c3."CandidateId")
                                            Filter: ("IsActive" AND (("Status")::text = ANY ('{available}'::text[])))
                                            Rows Removed by Filter: 1
                                            Buffers: shared hit=222
                                ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.71 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=17)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("LanguageId" = ANY ('{01a0f67a-a14f-753d-873d-c7522b3b11eb}'::uuid[])) AND ("LevelId" = ANY ('{01a0f67a-a14f-72db-9b08-34ee9d52abf5,01a0f67a-a14f-740d-a11a-8aa1c95ac2ab,01a0f67a-a14f-7445-9aeb-b35d987e4faf,01a0f67a-a14f-779a-886d-7516108861e4,01a0f67a-a14f-78cb-8ea6-86d49a2d3985,01a0f67a-a14f-7c1d-9513-946527adb9de}'::uuid[])))
                                      Rows Removed by Filter: 1
                                      Buffers: shared hit=51
                          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.92 rows=1 width=16) (actual time=0.010..0.010 rows=1 loops=1)
                                Index Cond: ("CandidateId" = c."Id")
                                Heap Fetches: 1
                                Buffers: shared hit=3
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (actual time=0.009..0.009 rows=0 loops=1)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("SkillId" = ANY ('{01a0f67a-a14f-7756-8ee1-68097398c6a6}'::uuid[]))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=4
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c4  (cost=0.28..8.29 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Heap Fetches: 0
        SubPlan 3
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c5  (cost=0.28..8.30 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
        SubPlan 5
          ->  Index Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c6  (cost=0.28..8.30 rows=1 width=0) (never executed)
                Index Cond: ("CandidateId" = c."Id")
                Filter: ((("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
Planning:
  Buffers: shared hit=95
Planning Time: 1.691 ms
Execution Time: 0.465 ms
```

