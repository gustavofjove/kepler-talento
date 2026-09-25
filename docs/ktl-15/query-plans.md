# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 1.1 ms

```
Aggregate  (cost=448.88..448.89 rows=1 width=4) (actual time=1.052..1.053 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=377
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..439.50 rows=3750 width=0) (actual time=0.011..0.886 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=377
Planning Time: 0.080 ms
Execution Time: 1.077 ms
```

Execution time: 0.4 ms

```
Limit  (cost=0.67..249.60 rows=25 width=62) (actual time=0.122..0.192 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=64
  ->  Result  (cost=0.67..37339.36 rows=3750 width=62) (actual time=0.121..0.188 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=64
        ->  Incremental Sort  (cost=0.67..1593.54 rows=3750 width=58) (actual time=0.102..0.105 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Presorted Key: o."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=14
              ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1424.79 rows=3750 width=58) (actual time=0.041..0.060 rows=26 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
                    Index Cond: ((o."Status")::text = 'open'::text)
                    Buffers: shared hit=14
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
Planning Time: 0.187 ms
Execution Time: 0.389 ms
```

## Title contains, all statuses

Execution time: 1.0 ms

```
Aggregate  (cost=452.25..452.26 rows=1 width=4) (actual time=0.983..0.984 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=377
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..452.00 rows=101 width=0) (actual time=0.013..0.976 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=377
Planning:
  Buffers: shared hit=2
Planning Time: 0.165 ms
Execution Time: 1.005 ms
```

Execution time: 1.2 ms

```
Limit  (cost=454.85..693.22 rows=25 width=62) (actual time=1.014..1.078 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1))
  Buffers: shared hit=427
  ->  Result  (cost=454.85..1417.86 rows=101 width=62) (actual time=1.013..1.074 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1)
        Buffers: shared hit=427
        ->  Sort  (cost=454.85..455.10 rows=101 width=58) (actual time=0.995..0.997 rows=25 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Sort Method: top-N heapsort  Memory: 31kB
              Buffers: shared hit=377
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..452.00 rows=101 width=58) (actual time=0.017..0.964 rows=111 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin
                    Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
                    Rows Removed by Filter: 4889
                    Buffers: shared hit=377
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.002..0.002 rows=1 loops=25)
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
Planning Time: 0.153 ms
Execution Time: 1.153 ms
```

## Location contains, closed

Execution time: 0.8 ms

```
Aggregate  (cost=449.02..449.04 rows=1 width=4) (actual time=0.714..0.716 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=385
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..448.58 rows=179 width=0) (actual time=0.162..0.702 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=371
        Buffers: shared hit=385
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.116..0.117 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.095 ms
Execution Time: 0.764 ms
```

Execution time: 0.2 ms

```
Limit  (cost=0.28..418.45 rows=25 width=80) (actual time=0.037..0.157 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedTitle"
  Buffers: shared hit=202
  ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..2994.37 rows=179 width=80) (actual time=0.036..0.153 rows=25 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedTitle"
        Index Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 153
        Buffers: shared hit=202
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.001..0.001 rows=1 loops=25)
                Output: (count(*))::integer
                Buffers: shared hit=50
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=50
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.000..0.000 rows=0 loops=25)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=50
Planning:
  Buffers: shared hit=5
Planning Time: 0.160 ms
Execution Time: 0.216 ms
```

## Deep page sorted by location

Execution time: 0.8 ms

```
Aggregate  (cost=439.50..439.51 rows=1 width=4) (actual time=0.772..0.773 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=377
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..427.00 rows=5000 width=0) (actual time=0.005..0.533 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=377
Planning:
  Buffers: shared hit=2
Planning Time: 0.077 ms
Execution Time: 0.790 ms
```

Execution time: 11.3 ms

```
Limit  (cost=47454.31..48407.78 rows=100 width=69) (actual time=11.034..11.155 rows=100 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, ((SubPlan 1)), o."NormalizedLocation"
  Buffers: shared hit=10377
  ->  Result  (cost=734.19..48407.78 rows=5000 width=69) (actual time=5.137..10.948 rows=5000 loops=1)
        Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, (SubPlan 1), o."NormalizedLocation"
        Buffers: shared hit=10377
        ->  Sort  (cost=734.19..746.69 rows=5000 width=65) (actual time=5.104..5.488 rows=5000 loops=1)
              Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
              Sort Key: o."NormalizedLocation", o."Id"
              Sort Method: quicksort  Memory: 857kB
              Buffers: shared hit=377
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..427.00 rows=5000 width=65) (actual time=0.027..1.005 rows=5000 loops=1)
                    Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, o."NormalizedLocation"
                    Buffers: shared hit=377
        SubPlan 1
          ->  Aggregate  (cost=9.51..9.52 rows=1 width=4) (actual time=0.001..0.001 rows=1 loops=5000)
                Output: (count(*))::integer
                Buffers: shared hit=10000
                ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.16..9.50 rows=2 width=0) (actual time=0.000..0.000 rows=0 loops=5000)
                      Recheck Cond: (o0."PositionId" = o."Id")
                      Buffers: shared hit=10000
                      ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.16 rows=2 width=0) (actual time=0.000..0.000 rows=0 loops=5000)
                            Index Cond: (o0."PositionId" = o."Id")
                            Buffers: shared hit=10000
Planning:
  Buffers: shared hit=3
Planning Time: 0.144 ms
Execution Time: 11.314 ms
```
