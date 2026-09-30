# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 2.5 ms

```
Aggregate  (cost=488.88..488.89 rows=1 width=4) (actual time=2.492..2.493 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..479.50 rows=3750 width=0) (actual time=0.026..2.154 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=417
Planning Time: 0.190 ms
Execution Time: 2.529 ms
```

Execution time: 0.4 ms

```
Limit  (cost=0.71..250.48 rows=25 width=62) (actual time=0.187..0.254 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=74
  ->  Result  (cost=0.71..37466.81 rows=3750 width=62) (actual time=0.186..0.249 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=74
        ->  Incremental Sort  (cost=0.71..1720.99 rows=3750 width=58) (actual time=0.163..0.165 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Presorted Key: o."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=24
              ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1552.24 rows=3750 width=58) (actual time=0.080..0.132 rows=26 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
                    Index Cond: ((o."Status")::text = 'open'::text)
                    Buffers: shared hit=24
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.002..0.002 rows=1 loops=25)
                Output: (count(*))::integer
                Buffers: shared hit=50
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=50
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=50
Planning:
  Buffers: shared hit=3
Planning Time: 0.223 ms
Execution Time: 0.427 ms
```

## Title contains, all statuses

Execution time: 2.4 ms

```
Aggregate  (cost=492.25..492.26 rows=1 width=4) (actual time=2.338..2.340 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=0) (actual time=0.017..2.321 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.187 ms
Execution Time: 2.369 ms
```

Execution time: 2.9 ms

```
Limit  (cost=494.85..733.22 rows=25 width=62) (actual time=2.660..2.727 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=467
  ->  Result  (cost=494.85..1457.86 rows=101 width=62) (actual time=2.657..2.720 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=467
        ->  Sort  (cost=494.85..495.10 rows=101 width=58) (actual time=2.615..2.620 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Sort Method: top-N heapsort  Memory: 31kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=58) (actual time=0.022..2.553 rows=111 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
                    Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
                    Rows Removed by Filter: 4889
                    Buffers: shared hit=417
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.003..0.003 rows=1 loops=25)
                Output: (count(*))::integer
                Buffers: shared hit=50
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.002..0.002 rows=0 loops=25)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=50
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=50
Planning:
  Buffers: shared hit=5
Planning Time: 0.305 ms
Execution Time: 2.855 ms
```

## Location contains, closed

Execution time: 1.8 ms

```
Aggregate  (cost=489.02..489.04 rows=1 width=4) (actual time=1.673..1.676 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=426
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..488.58 rows=179 width=0) (actual time=0.320..1.646 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=412
        Buffers: shared hit=426
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.242..0.242 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.155 ms
Execution Time: 1.793 ms
```

Execution time: 0.6 ms

```
Limit  (cost=0.28..436.20 rows=25 width=80) (actual time=0.151..0.426 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedTitle"
  Buffers: shared hit=197
  ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..3121.42 rows=179 width=80) (actual time=0.149..0.416 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedTitle"
        Index Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 153
        Buffers: shared hit=197
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.004..0.004 rows=1 loops=25)
                Output: (count(*))::integer
                Buffers: shared hit=50
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.003..0.003 rows=0 loops=25)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=50
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=50
Planning:
  Buffers: shared hit=5
Planning Time: 0.578 ms
Execution Time: 0.634 ms
```

## Deep page sorted by location

Execution time: 1.7 ms

```
Aggregate  (cost=479.50..479.51 rows=1 width=4) (actual time=1.657..1.658 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=0) (actual time=0.013..1.221 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.146 ms
Execution Time: 1.696 ms
```

Execution time: 32.6 ms

```
Limit  (cost=47494.31..48447.78 rows=100 width=69) (actual time=25.125..32.052 rows=100 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedLocation"
  Buffers: shared hit=10417
  ->  Result  (cost=774.19..48447.78 rows=5000 width=69) (actual time=14.893..31.705 rows=5000 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedLocation"
        Buffers: shared hit=10417
        ->  Sort  (cost=774.19..786.69 rows=5000 width=65) (actual time=14.806..15.428 rows=5000 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
              Sort Key: o."NormalizedLocation", o."Id"
              Sort Method: quicksort  Memory: 857kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=65) (actual time=0.025..4.758 rows=5000 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
                    Buffers: shared hit=417
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.002..0.002 rows=1 loops=5000)
                Output: (count(*))::integer
                Buffers: shared hit=10000
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=5000)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=10000
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=5000)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=10000
Planning:
  Buffers: shared hit=3
Planning Time: 0.175 ms
Execution Time: 32.590 ms
```
