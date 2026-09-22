# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 2.0 ms

```
Aggregate  (cost=450.88..450.89 rows=1 width=4) (actual time=1.959..1.961 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=379
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..441.50 rows=3750 width=0) (actual time=0.018..1.678 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=379
Planning Time: 0.124 ms
Execution Time: 2.001 ms
```

Execution time: 0.2 ms

```
Limit  (cost=0.67..11.31 rows=25 width=58) (actual time=0.117..0.122 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
  Buffers: shared hit=12
  ->  Incremental Sort  (cost=0.67..1596.64 rows=3750 width=58) (actual time=0.115..0.117 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
        Sort Key: o."UpdatedAtUtc" DESC, o."Id"
        Presorted Key: o."UpdatedAtUtc"
        Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
        Buffers: shared hit=12
        ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1427.89 rows=3750 width=58) (actual time=0.051..0.072 rows=26 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
              Index Cond: ((o."Status")::text = 'open'::text)
              Buffers: shared hit=12
Planning Time: 0.172 ms
Execution Time: 0.182 ms
```

## Title contains, all statuses

Execution time: 2.3 ms

```
Aggregate  (cost=454.25..454.26 rows=1 width=4) (actual time=2.293..2.295 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=379
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..454.00 rows=101 width=0) (actual time=0.029..2.276 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=379
Planning:
  Buffers: shared hit=2
Planning Time: 0.187 ms
Execution Time: 2.322 ms
```

Execution time: 2.1 ms

```
Limit  (cost=456.85..456.91 rows=25 width=58) (actual time=2.046..2.054 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
  Buffers: shared hit=379
  ->  Sort  (cost=456.85..457.10 rows=101 width=58) (actual time=2.044..2.048 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
        Sort Key: o."UpdatedAtUtc" DESC, o."Id"
        Sort Method: top-N heapsort  Memory: 31kB
        Buffers: shared hit=379
        ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..454.00 rows=101 width=58) (actual time=0.029..1.972 rows=111 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
              Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
              Rows Removed by Filter: 4889
              Buffers: shared hit=379
Planning:
  Buffers: shared hit=2
Planning Time: 0.154 ms
Execution Time: 2.129 ms
```

## Location contains, closed

Execution time: 1.4 ms

```
Aggregate  (cost=451.02..451.04 rows=1 width=4) (actual time=1.310..1.312 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=388
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..450.58 rows=179 width=0) (actual time=0.344..1.286 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=374
        Buffers: shared hit=388
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.249..0.250 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.331 ms
Execution Time: 1.434 ms
```

Execution time: 0.6 ms

```
Limit  (cost=0.28..181.33 rows=25 width=76) (actual time=0.162..0.550 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedTitle"
  Buffers: shared hit=135
  ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..1296.61 rows=179 width=76) (actual time=0.159..0.543 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedTitle"
        Index Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 153
        Buffers: shared hit=135
Planning:
  Buffers: shared hit=2
Planning Time: 0.248 ms
Execution Time: 0.610 ms
```

## Deep page sorted by location

Execution time: 1.6 ms

```
Aggregate  (cost=441.50..441.51 rows=1 width=4) (actual time=1.529..1.531 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=379
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..429.00 rows=5000 width=0) (actual time=0.009..1.158 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=379
Planning:
  Buffers: shared hit=2
Planning Time: 0.094 ms
Execution Time: 1.558 ms
```

Execution time: 15.0 ms

```
Limit  (cost=748.44..748.69 rows=100 width=65) (actual time=14.738..14.755 rows=100 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
  Buffers: shared hit=379
  ->  Sort  (cost=736.19..748.69 rows=5000 width=65) (actual time=14.165..14.510 rows=5000 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
        Sort Key: o."NormalizedLocation", o."Id"
        Sort Method: quicksort  Memory: 857kB
        Buffers: shared hit=379
        ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..429.00 rows=5000 width=65) (actual time=0.016..2.506 rows=5000 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
              Buffers: shared hit=379
Planning Time: 0.087 ms
Execution Time: 14.979 ms
```
