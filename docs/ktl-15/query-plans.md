# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 8.9 ms

```
Aggregate  (cost=488.88..488.89 rows=1 width=4) (actual time=8.865..8.866 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..479.50 rows=3750 width=0) (actual time=0.018..1.591 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=417
Planning Time: 0.118 ms
Execution Time: 8.912 ms
```

Execution time: 0.4 ms

```
Limit  (cost=0.71..250.48 rows=25 width=62) (actual time=0.126..0.203 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=69
  ->  Result  (cost=0.71..37466.98 rows=3750 width=62) (actual time=0.124..0.198 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=69
        ->  Incremental Sort  (cost=0.71..1721.16 rows=3750 width=58) (actual time=0.107..0.110 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Presorted Key: o."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=19
              ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1552.41 rows=3750 width=58) (actual time=0.034..0.061 rows=26 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
                    Index Cond: ((o."Status")::text = 'open'::text)
                    Buffers: shared hit=19
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.002..0.003 rows=1 loops=25)
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
Planning Time: 0.219 ms
Execution Time: 0.386 ms
```

## Title contains, all statuses

Execution time: 2.3 ms

```
Aggregate  (cost=492.25..492.26 rows=1 width=4) (actual time=2.265..2.268 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=0) (actual time=0.120..2.249 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.172 ms
Execution Time: 2.300 ms
```

Execution time: 2.7 ms

```
Limit  (cost=494.85..733.22 rows=25 width=62) (actual time=2.357..2.433 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=467
  ->  Result  (cost=494.85..1457.86 rows=101 width=62) (actual time=2.355..2.428 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=467
        ->  Sort  (cost=494.85..495.10 rows=101 width=58) (actual time=2.318..2.322 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Sort Method: top-N heapsort  Memory: 31kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=58) (actual time=0.051..2.194 rows=111 loops=1)
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
Planning Time: 0.425 ms
Execution Time: 2.711 ms
```

## Location contains, closed

Execution time: 1.3 ms

```
Aggregate  (cost=489.02..489.04 rows=1 width=4) (actual time=1.194..1.196 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=422
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..488.58 rows=179 width=0) (actual time=0.299..1.175 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=408
        Buffers: shared hit=422
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.223..0.223 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.175 ms
Execution Time: 1.326 ms
```

Execution time: 0.6 ms

```
Limit  (cost=0.28..436.22 rows=25 width=80) (actual time=0.104..0.377 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedTitle"
  Buffers: shared hit=194
  ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..3121.62 rows=179 width=80) (actual time=0.102..0.372 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedTitle"
        Index Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 153
        Buffers: shared hit=194
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
Planning Time: 0.302 ms
Execution Time: 0.576 ms
```

## Deep page sorted by location

Execution time: 1.5 ms

```
Aggregate  (cost=479.50..479.51 rows=1 width=4) (actual time=1.429..1.431 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=0) (actual time=0.016..0.972 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.122 ms
Execution Time: 1.465 ms
```

Execution time: 72.7 ms

```
Limit  (cost=47494.31..48447.78 rows=100 width=69) (actual time=71.976..72.284 rows=100 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedLocation"
  Buffers: shared hit=10417
  ->  Result  (cost=774.19..48447.78 rows=5000 width=69) (actual time=21.595..71.997 rows=5000 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedLocation"
        Buffers: shared hit=10417
        ->  Sort  (cost=774.19..786.69 rows=5000 width=65) (actual time=21.543..32.823 rows=5000 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
              Sort Key: o."NormalizedLocation", o."Id"
              Sort Method: quicksort  Memory: 857kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=65) (actual time=0.042..2.618 rows=5000 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
                    Buffers: shared hit=417
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.005..0.005 rows=1 loops=5000)
                Output: (count(*))::integer
                Buffers: shared hit=10000
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.004..0.004 rows=0 loops=5000)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=10000
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.003..0.003 rows=0 loops=5000)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=10000
Planning:
  Buffers: shared hit=3
Planning Time: 0.203 ms
Execution Time: 72.650 ms
```
