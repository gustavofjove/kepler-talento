# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 1.8 ms

```
Aggregate  (cost=449.88..449.89 rows=1 width=4) (actual time=1.811..1.812 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=378
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..440.50 rows=3750 width=0) (actual time=0.016..1.552 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=378
Planning Time: 0.107 ms
Execution Time: 1.847 ms
```

Execution time: 0.2 ms

```
Limit  (cost=0.67..11.29 rows=25 width=58) (actual time=0.120..0.125 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
  Buffers: shared hit=17
  ->  Incremental Sort  (cost=0.67..1593.18 rows=3750 width=58) (actual time=0.119..0.121 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
        Sort Key: o."UpdatedAtUtc" DESC, o."Id"
        Presorted Key: o."UpdatedAtUtc"
        Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
        Buffers: shared hit=17
        ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1424.43 rows=3750 width=58) (actual time=0.052..0.076 rows=26 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
              Index Cond: ((o."Status")::text = 'open'::text)
              Buffers: shared hit=17
Planning Time: 0.135 ms
Execution Time: 0.192 ms
```

## Title contains, all statuses

Execution time: 2.2 ms

```
Aggregate  (cost=453.25..453.26 rows=1 width=4) (actual time=2.211..2.213 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=378
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..453.00 rows=101 width=0) (actual time=0.017..2.195 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=378
Planning:
  Buffers: shared hit=2
Planning Time: 0.187 ms
Execution Time: 2.237 ms
```

Execution time: 5.8 ms

```
Limit  (cost=455.85..455.91 rows=25 width=58) (actual time=5.767..5.775 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
  Buffers: shared hit=378
  ->  Sort  (cost=455.85..456.10 rows=101 width=58) (actual time=5.764..5.768 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
        Sort Key: o."UpdatedAtUtc" DESC, o."Id"
        Sort Method: top-N heapsort  Memory: 31kB
        Buffers: shared hit=378
        ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..453.00 rows=101 width=58) (actual time=0.018..5.681 rows=111 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin
              Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
              Rows Removed by Filter: 4889
              Buffers: shared hit=378
Planning:
  Buffers: shared hit=2
Planning Time: 0.222 ms
Execution Time: 5.836 ms
```

## Location contains, closed

Execution time: 1.6 ms

```
Aggregate  (cost=450.02..450.04 rows=1 width=4) (actual time=1.434..1.436 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=388
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..449.58 rows=179 width=0) (actual time=0.367..1.409 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=374
        Buffers: shared hit=388
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.259..0.260 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.198 ms
Execution Time: 1.623 ms
```

Execution time: 6.2 ms

```
Limit  (cost=0.28..180.88 rows=25 width=76) (actual time=0.074..6.159 rows=25 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedTitle"
  Buffers: shared hit=147
  ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..1293.35 rows=179 width=76) (actual time=0.072..6.153 rows=25 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedTitle"
        Index Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 153
        Buffers: shared hit=147
Planning:
  Buffers: shared hit=2
Planning Time: 0.212 ms
Execution Time: 6.218 ms
```

## Deep page sorted by location

Execution time: 1.6 ms

```
Aggregate  (cost=440.50..440.51 rows=1 width=4) (actual time=1.575..1.576 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=378
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..428.00 rows=5000 width=0) (actual time=0.012..1.170 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=378
Planning:
  Buffers: shared hit=2
Planning Time: 0.101 ms
Execution Time: 1.600 ms
```

Execution time: 17.4 ms

```
Limit  (cost=747.44..747.69 rows=100 width=65) (actual time=17.111..17.135 rows=100 loops=1)
  Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
  Buffers: shared hit=378
  ->  Sort  (cost=735.19..747.69 rows=5000 width=65) (actual time=14.717..15.496 rows=5000 loops=1)
        Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
        Sort Key: o."NormalizedLocation", o."Id"
        Sort Method: quicksort  Memory: 857kB
        Buffers: shared hit=378
        ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..428.00 rows=5000 width=65) (actual time=0.026..4.675 rows=5000 loops=1)
              Output: "Id", "Title", "Location", "Status", "UpdatedAtUtc", xmin, "NormalizedLocation"
              Buffers: shared hit=378
Planning Time: 0.098 ms
Execution Time: 17.395 ms
```
