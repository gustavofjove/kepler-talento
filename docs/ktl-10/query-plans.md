# KTL-10 query plans

Captured by `SearchQueryPlanTests` against 3.000 candidates with their
relations and primary documents — the scale KTL-7 reconciled. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj`.

Since KTL-33, free text and the last-name order run in the API over decrypted values, so
they issue no SQL of their own worth a plan here. Their evidence is
[`docs/ktl-33/performance.md`](../ktl-33/performance.md).

## Deep page 30 sorted by UpdatedAt Ascending

Execution time: 4.2 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=324) (actual time=3.939..3.967 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=324) (actual time=3.221..3.893 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=321) (actual time=2.011..2.178 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.011..0.780 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.265 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.332..0.332 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.340..0.340 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.276 ms
Execution Time: 4.189 ms
```

## Deep page 30 sorted by UpdatedAt Descending

Execution time: 3.9 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=324) (actual time=3.698..3.729 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=324) (actual time=2.959..3.656 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=321) (actual time=1.975..2.153 rows=3000 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.009..0.748 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.007..0.346 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.207..0.207 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.170..0.170 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.238 ms
Execution Time: 3.900 ms
```

## Deep page 30 sorted by AvailabilityCheckedOn Ascending

Execution time: 7.7 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=325) (actual time=7.398..7.441 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=325) (actual time=6.318..7.314 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=322) (actual time=5.351..5.561 rows=3000 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=322) (actual time=0.015..1.422 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.012..0.305 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.234..0.234 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.162..0.162 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.545 ms
Execution Time: 7.700 ms
```

## Deep page 30 sorted by AvailabilityCheckedOn Descending

Execution time: 5.4 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=325) (actual time=5.230..5.253 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=325) (actual time=4.559..5.179 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=322) (actual time=3.640..3.781 rows=3000 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=322) (actual time=0.007..0.953 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.010..0.308 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.187..0.187 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.166..0.166 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.221 ms
Execution Time: 5.418 ms
```

## Deep page 30 sorted by CreatedAt Ascending

Execution time: 3.9 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=332) (actual time=3.678..3.703 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=332) (actual time=3.051..3.628 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=329) (actual time=2.120..2.214 rows=3000 loops=1)
              Sort Key: c."CreatedAtUtc", c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=329) (actual time=0.008..0.852 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.007..0.300 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.197..0.197 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.166..0.166 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.222 ms
Execution Time: 3.854 ms
```

## Deep page 30 sorted by CreatedAt Descending

Execution time: 3.7 ms

```
Limit  (cost=72789.76..75281.26 rows=100 width=332) (actual time=3.492..3.516 rows=100 loops=1)
  Buffers: shared hit=585
  ->  Result  (cost=536.26..75281.26 rows=3000 width=332) (actual time=2.849..3.440 rows=3000 loops=1)
        Buffers: shared hit=585
        ->  Sort  (cost=536.26..543.76 rows=3000 width=329) (actual time=1.910..1.997 rows=3000 loops=1)
              Sort Key: c."CreatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 1620kB
              Buffers: shared hit=333
              ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=329) (actual time=0.008..0.730 rows=3000 loops=1)
                    Buffers: shared hit=333
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.006..0.257 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.190..0.190 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.195..0.195 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.216 ms
Execution Time: 3.668 ms
```

## Newest active candidates first, home page panel

Execution time: 0.9 ms

```
Limit  (cost=0.28..126.27 rows=5 width=332) (actual time=0.828..0.832 rows=5 loops=1)
  Buffers: shared hit=255
  ->  Index Scan using "IX_CND_Candidates_IsActive_CreatedAtUtc" on "CND_Candidates" c  (cost=0.28..75592.53 rows=3000 width=332) (actual time=0.827..0.831 rows=5 loops=1)
        Buffers: shared hit=255
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.248 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.173..0.173 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.162..0.162 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.204 ms
Execution Time: 0.898 ms
```

## Unfiltered first page

Execution time: 1.1 ms

```
Limit  (cost=0.58..631.85 rows=25 width=324) (actual time=0.967..0.975 rows=25 loops=1)
  Buffers: shared hit=268
  ->  Result  (cost=0.58..75753.03 rows=3000 width=324) (actual time=0.966..0.973 rows=25 loops=1)
        Buffers: shared hit=268
        ->  Incremental Sort  (cost=0.58..1015.53 rows=3000 width=321) (actual time=0.047..0.049 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=16
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..880.53 rows=3000 width=321) (actual time=0.017..0.028 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Buffers: shared hit=16
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.006..0.301 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.196..0.196 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.165..0.165 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.289 ms
Execution Time: 1.070 ms
```

## Availability subset and primary CV

Execution time: 1.0 ms

```
Limit  (cost=1.97..660.89 rows=25 width=324) (actual time=0.924..0.932 rows=25 loops=1)
  Buffers: shared hit=358
  ->  Result  (cost=1.97..35135.71 rows=1333 width=324) (actual time=0.923..0.930 rows=25 loops=1)
        Buffers: shared hit=358
        ->  Incremental Sort  (cost=1.97..1927.34 rows=1333 width=321) (actual time=0.073..0.075 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=106
              ->  Nested Loop Semi Join  (cost=0.56..1867.36 rows=1333 width=321) (actual time=0.021..0.056 rows=26 loops=1)
                    Buffers: shared hit=106
                    ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..888.03 rows=2000 width=321) (actual time=0.012..0.026 rows=26 loops=1)
                          Index Cond: ("IsActive" = true)
                          Filter: (("AvailabilityState")::text = ANY ('{available,unavailable}'::text[]))
                          Rows Removed by Filter: 12
                          Buffers: shared hit=28
                    ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..0.48 rows=1 width=16) (actual time=0.001..0.001 rows=1 loops=26)
                          Index Cond: ("CandidateId" = c."Id")
                          Heap Fetches: 26
                          Buffers: shared hit=78
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.005..0.275 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..124.00 rows=1 width=16) (actual time=0.170..0.170 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..119.00 rows=1 width=16) (actual time=0.164..0.164 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=20
Planning Time: 0.379 ms
Execution Time: 1.021 ms
```

## Checked from a recent date

Execution time: 1.0 ms

```
Limit  (cost=223.72..846.60 rows=25 width=325) (actual time=0.943..0.950 rows=25 loops=1)
  Buffers: shared hit=352
  ->  Result  (cost=223.72..2715.22 rows=100 width=325) (actual time=0.942..0.948 rows=25 loops=1)
        Buffers: shared hit=352
        ->  Sort  (cost=223.72..223.97 rows=100 width=322) (actual time=0.172..0.173 rows=25 loops=1)
              Sort Key: ((c."AvailabilityCheckedOn" IS NULL)), c."AvailabilityCheckedOn" DESC, c."Id"
              Sort Method: top-N heapsort  Memory: 44kB
              Buffers: shared hit=100
              ->  Bitmap Heap Scan on "CND_Candidates" c  (cost=5.06..220.90 rows=100 width=322) (actual time=0.032..0.118 rows=100 loops=1)
                    Recheck Cond: (("AvailabilityCheckedOn" >= '2026-09-01'::date) AND "IsActive")
                    Heap Blocks: exact=97
                    Buffers: shared hit=100
                    ->  Bitmap Index Scan on "IX_CND_Candidates_IsActive_AvailabilityCheckedOn"  (cost=0.00..5.03 rows=100 width=0) (actual time=0.017..0.017 rows=100 loops=1)
                          Index Cond: ("AvailabilityCheckedOn" >= '2026-09-01'::date)
                          Buffers: shared hit=3
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c0  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.225 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c1  (cost=0.00..124.00 rows=1 width=16) (actual time=0.152..0.152 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c2  (cost=0.00..119.00 rows=1 width=16) (actual time=0.146..0.146 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=7
Planning Time: 0.225 ms
Execution Time: 1.048 ms
```

## Skill ANY across three values

Execution time: 1.5 ms

```
Limit  (cost=29.10..1373.00 rows=25 width=324) (actual time=1.351..1.360 rows=25 loops=1)
  Buffers: shared hit=680
  ->  Result  (cost=29.10..141138.97 rows=2625 width=324) (actual time=1.350..1.358 rows=25 loops=1)
        Buffers: shared hit=680
        ->  Incremental Sort  (cost=29.10..75743.65 rows=2625 width=321) (actual time=0.548..0.550 rows=25 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Presorted Key: c."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 37kB  Peak Memory: 37kB
              Buffers: shared hit=428
              ->  Index Scan Backward using "IX_CND_Candidates_IsActive_UpdatedAtUtc" on "CND_Candidates" c  (cost=0.28..75625.53 rows=2625 width=321) (actual time=0.504..0.532 rows=26 loops=1)
                    Index Cond: ("IsActive" = true)
                    Filter: ((ANY ("Id" = (hashed SubPlan 8).col1)) OR (ANY ("Id" = (hashed SubPlan 10).col1)) OR (ANY ("Id" = (hashed SubPlan 12).col1)))
                    Rows Removed by Filter: 43
                    Buffers: shared hit=428
                    SubPlan 8
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c0  (cost=7.28..140.64 rows=387 width=16) (actual time=0.029..0.138 rows=387 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a11536-ccb7-7720-af6e-0a6eb63b48c6}'::uuid[]))
                            Heap Blocks: exact=122
                            Buffers: shared hit=124
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.19 rows=387 width=0) (actual time=0.017..0.017 rows=387 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a11536-ccb7-7720-af6e-0a6eb63b48c6}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 10
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.024..0.095 rows=357 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a11536-ccb7-7ddf-aa50-f9acf1e05c94}'::uuid[]))
                            Heap Blocks: exact=125
                            Buffers: shared hit=127
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.015..0.015 rows=357 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a11536-ccb7-7ddf-aa50-f9acf1e05c94}'::uuid[]))
                                  Buffers: shared hit=2
                    SubPlan 12
                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.023..0.095 rows=388 loops=1)
                            Recheck Cond: ("SkillId" = ANY ('{01a11536-ccb7-7e0e-8052-6882a9238e9b}'::uuid[]))
                            Heap Blocks: exact=124
                            Buffers: shared hit=126
                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.014..0.014 rows=388 loops=1)
                                  Index Cond: ("SkillId" = ANY ('{01a11536-ccb7-7e0e-8052-6882a9238e9b}'::uuid[]))
                                  Buffers: shared hit=2
        SubPlan 2
          ->  Seq Scan on "CND_Documents" c3  (cost=0.00..104.00 rows=2000 width=16) (actual time=0.004..0.231 rows=2000 loops=1)
                Filter: "IsPrimary"
                Buffers: shared hit=84
        SubPlan 4
          ->  Seq Scan on "CND_Documents" c4  (cost=0.00..124.00 rows=1 width=16) (actual time=0.184..0.185 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("ContentType")::text = 'application/pdf'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
        SubPlan 6
          ->  Seq Scan on "CND_Documents" c5  (cost=0.00..119.00 rows=1 width=16) (actual time=0.147..0.147 rows=0 loops=1)
                Filter: ("IsPrimary" AND (("ScanState")::text = 'Clean'::text) AND (("SourceKey" IS NULL) OR (btrim(("Sha256")::text, ' 	
'::text) <> ''::text)))
                Rows Removed by Filter: 2000
                Buffers: shared hit=84
Planning:
  Buffers: shared hit=13
Planning Time: 0.342 ms
Execution Time: 1.525 ms
```

## Skill ALL across three values

Execution time: 1.7 ms

```
Limit  (cost=693.25..842.74 rows=6 width=324) (actual time=1.488..1.500 rows=3 loops=1)
  Buffers: shared hit=719
  ->  Result  (cost=693.25..842.74 rows=6 width=324) (actual time=1.487..1.499 rows=3 loops=1)
        Buffers: shared hit=719
        ->  Sort  (cost=693.25..693.26 rows=6 width=321) (actual time=1.452..1.454 rows=3 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 26kB
              Buffers: shared hit=692
              ->  Nested Loop Semi Join  (cost=290.32..693.17 rows=6 width=321) (actual time=0.708..1.442 rows=3 loops=1)
                    Buffers: shared hit=692
                    ->  Hash Semi Join  (cost=290.04..667.34 rows=46 width=353) (actual time=0.506..1.370 rows=30 loops=1)
                          Hash Cond: (c."Id" = c2."CandidateId")
                          Buffers: shared hit=586
                          ->  Hash Semi Join  (cost=144.53..520.27 rows=357 width=337) (actual time=0.255..1.122 rows=357 loops=1)
                                Hash Cond: (c."Id" = c1."CandidateId")
                                Buffers: shared hit=460
                                ->  Seq Scan on "CND_Candidates" c  (cost=0.00..363.00 rows=3000 width=321) (actual time=0.021..0.711 rows=3000 loops=1)
                                      Filter: "IsActive"
                                      Buffers: shared hit=333
                                ->  Hash  (cost=140.07..140.07 rows=357 width=16) (actual time=0.222..0.222 rows=357 loops=1)
                                      Buckets: 1024  Batches: 1  Memory Usage: 25kB
                                      Buffers: shared hit=127
                                      ->  Bitmap Heap Scan on "CND_CandidateSkills" c1  (cost=7.05..140.07 rows=357 width=16) (actual time=0.028..0.177 rows=357 loops=1)
                                            Recheck Cond: ("SkillId" = ANY ('{01a11536-ccb7-7ddf-aa50-f9acf1e05c94}'::uuid[]))
                                            Heap Blocks: exact=125
                                            Buffers: shared hit=127
                                            ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..6.96 rows=357 width=0) (actual time=0.018..0.018 rows=357 loops=1)
                                                  Index Cond: ("SkillId" = ANY ('{01a11536-ccb7-7ddf-aa50-f9acf1e05c94}'::uuid[]))
                                                  Buffers: shared hit=2
                          ->  Hash  (cost=140.66..140.66 rows=388 width=16) (actual time=0.214..0.214 rows=388 loops=1)
                                Buckets: 1024  Batches: 1  Memory Usage: 27kB
                                Buffers: shared hit=126
                                ->  Bitmap Heap Scan on "CND_CandidateSkills" c2  (cost=7.29..140.66 rows=388 width=16) (actual time=0.035..0.162 rows=388 loops=1)
                                      Recheck Cond: ("SkillId" = ANY ('{01a11536-ccb7-7e0e-8052-6882a9238e9b}'::uuid[]))
                                      Heap Blocks: exact=124
                                      Buffers: shared hit=126
                                      ->  Bitmap Index Scan on "IX_CND_CandidateSkills_SkillId_SkillFamily"  (cost=0.00..7.20 rows=388 width=0) (actual time=0.020..0.020 rows=388 loops=1)
                                            Index Cond: ("SkillId" = ANY ('{01a11536-ccb7-7e0e-8052-6882a9238e9b}'::uuid[]))
                                            Buffers: shared hit=2
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c0  (cost=0.29..0.56 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=30)
                          Index Cond: ("CandidateId" = c."Id")
                          Filter: (("SkillId" = ANY ('{01a11536-ccb7-7720-af6e-0a6eb63b48c6}'::uuid[])) AND ("LevelId" = ANY ('{01a11536-ccb7-7035-ac0f-bc2bd065f88c,01a11536-ccb7-717f-81c2-8df777c83880,01a11536-ccb7-746f-ac86-9b910b585008,01a11536-ccb7-7ea1-a9f2-4ef59e729976}'::uuid[])))
                          Rows Removed by Filter: 3
                          Buffers: shared hit=106
        SubPlan 1
          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c3  (cost=0.28..8.29 rows=1 width=0) (actual time=0.007..0.007 rows=1 loops=3)
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
Planning Time: 1.080 ms
Execution Time: 1.663 ms
```

## Every SQL family combined

Execution time: 0.4 ms

```
Limit  (cost=407.45..432.37 rows=1 width=324) (actual time=0.257..0.259 rows=0 loops=1)
  Buffers: shared hit=325
  ->  Result  (cost=407.45..432.37 rows=1 width=324) (actual time=0.256..0.258 rows=0 loops=1)
        Buffers: shared hit=325
        ->  Sort  (cost=407.45..407.46 rows=1 width=321) (actual time=0.256..0.257 rows=0 loops=1)
              Sort Key: c."UpdatedAtUtc" DESC, c."Id"
              Sort Method: quicksort  Memory: 25kB
              Buffers: shared hit=325
              ->  Nested Loop Semi Join  (cost=55.88..407.44 rows=1 width=321) (actual time=0.250..0.252 rows=0 loops=1)
                    Buffers: shared hit=325
                    ->  Nested Loop Semi Join  (cost=55.60..406.43 rows=1 width=369) (actual time=0.160..0.244 rows=1 loops=1)
                          Buffers: shared hit=321
                          ->  Nested Loop Semi Join  (cost=55.32..405.33 rows=1 width=353) (actual time=0.148..0.232 rows=1 loops=1)
                                Buffers: shared hit=318
                                ->  Nested Loop  (cost=55.04..395.40 rows=12 width=337) (actual time=0.095..0.200 rows=18 loops=1)
                                      Buffers: shared hit=264
                                      ->  HashAggregate  (cost=54.76..55.50 rows=74 width=16) (actual time=0.080..0.085 rows=74 loops=1)
                                            Group Key: c3."CandidateId"
                                            Batches: 1  Memory Usage: 24kB
                                            Buffers: shared hit=42
                                            ->  Bitmap Heap Scan on "CND_CandidatePrograms" c3  (cost=4.85..54.58 rows=74 width=16) (actual time=0.018..0.065 rows=74 loops=1)
                                                  Recheck Cond: ("ProgramId" = ANY ('{01a11536-ccb7-70da-a695-0ea27f713f7c}'::uuid[]))
                                                  Heap Blocks: exact=40
                                                  Buffers: shared hit=42
                                                  ->  Bitmap Index Scan on "IX_CND_CandidatePrograms_ProgramId_ProgramFamily"  (cost=0.00..4.83 rows=74 width=0) (actual time=0.011..0.011 rows=74 loops=1)
                                                        Index Cond: ("ProgramId" = ANY ('{01a11536-ccb7-70da-a695-0ea27f713f7c}'::uuid[]))
                                                        Buffers: shared hit=2
                                      ->  Index Scan using "PK_CND_Candidates" on "CND_Candidates" c  (cost=0.28..4.68 rows=1 width=321) (actual time=0.001..0.001 rows=0 loops=74)
                                            Index Cond: ("Id" = c3."CandidateId")
                                            Filter: ("IsActive" AND (("AvailabilityState")::text = ANY ('{available}'::text[])) AND ("AvailabilityCheckedOn" >= '2026-01-01'::date))
                                            Rows Removed by Filter: 1
                                            Buffers: shared hit=222
                                ->  Index Scan using "IX_CND_CandidateLanguages_CandidateId" on "CND_CandidateLanguages" c2  (cost=0.28..0.83 rows=1 width=16) (actual time=0.002..0.002 rows=0 loops=18)
                                      Index Cond: ("CandidateId" = c."Id")
                                      Filter: (("LanguageId" = ANY ('{01a11536-ccb7-7f1c-88d5-af6c3d23f24a}'::uuid[])) AND ("LevelId" = ANY ('{01a11536-ccb7-7217-9870-49fb41518cfd,01a11536-ccb7-7493-9e76-286f08b231ea,01a11536-ccb7-7697-bc30-5c868dc436d8,01a11536-ccb7-7705-9eb7-199727e1971f,01a11536-ccb7-7ad0-a716-718a9cb52daa,01a11536-ccb7-7d89-b091-c5be41735b90}'::uuid[])))
                                      Rows Removed by Filter: 1
                                      Buffers: shared hit=54
                          ->  Index Only Scan using "UX_CND_Documents_CandidateId_Primary" on "CND_Documents" c0  (cost=0.28..1.10 rows=1 width=16) (actual time=0.011..0.011 rows=1 loops=1)
                                Index Cond: ("CandidateId" = c."Id")
                                Heap Fetches: 1
                                Buffers: shared hit=3
                    ->  Index Scan using "IX_CND_CandidateSkills_CandidateId" on "CND_CandidateSkills" c1  (cost=0.29..0.65 rows=1 width=16) (actual time=0.007..0.007 rows=0 loops=1)
                          Index Cond: ("CandidateId" = c0."CandidateId")
                          Filter: ("SkillId" = ANY ('{01a11536-ccb7-7720-af6e-0a6eb63b48c6}'::uuid[]))
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
Planning Time: 1.332 ms
Execution Time: 0.374 ms
```

