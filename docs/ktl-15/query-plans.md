# KTL-15 position list query plans

Captured by `PositionQueryPlanTests` against 5.000 positions, each with a
representative description and requirements document; the 100 most recently updated
carry 60 candidate links each, spread over every stage, so the per-stage counts
(KTL-40) are part of the plan. Regenerate by running
`dotnet test backend/Tests/IntegrationTests/IntegrationTests.csproj --filter PositionQueryPlanTests`.

## Default: open, updated descending, first page

Execution time: 2.3 ms

```
Aggregate  (cost=488.88..488.89 rows=1 width=4) (actual time=2.264..2.266 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..479.50 rows=3750 width=0) (actual time=0.018..1.929 rows=3750 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: ((o."Status")::text = 'open'::text)
        Rows Removed by Filter: 1250
        Buffers: shared hit=417
Planning Time: 0.139 ms
Execution Time: 2.297 ms
```

Execution time: 2.1 ms

```
Nested Loop Left Join  (cost=5.45..2113.72 rows=25 width=82) (actual time=0.137..1.899 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, COALESCE(((count(*))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer), 0)
  Buffers: shared hit=132
  ->  Limit  (cost=0.71..12.18 rows=25 width=58) (actual time=0.085..0.095 rows=25 loops=1)
        Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
        Buffers: shared hit=20
        ->  Incremental Sort  (cost=0.71..1721.12 rows=3750 width=58) (actual time=0.082..0.087 rows=25 loops=1)
              Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Presorted Key: o."UpdatedAtUtc"
              Full-sort Groups: 1  Sort Method: quicksort  Average Memory: 28kB  Peak Memory: 28kB
              Buffers: shared hit=20
              ->  Index Scan Backward using "IX_OPS_Positions_Status_UpdatedAtUtc_Id" on public."OPS_Positions" o  (cost=0.28..1552.37 rows=3750 width=58) (actual time=0.035..0.054 rows=26 loops=1)
                    Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
                    Index Cond: ((o."Status")::text = 'open'::text)
                    Buffers: shared hit=20
  ->  GroupAggregate  (cost=4.75..84.03 rows=1 width=40) (actual time=0.071..0.071 rows=1 loops=25)
        Output: (count(*))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer, o0."PositionId"
        Buffers: shared hit=112
        ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.75..82.36 rows=60 width=24) (actual time=0.030..0.037 rows=60 loops=25)
              Output: o0."Id", o0."PositionId", o0."CandidateId", o0."Stage", o0."AddedAtUtc", o0."UpdatedAtUtc"
              Recheck Cond: (o0."PositionId" = o."Id")
              Heap Blocks: exact=51
              Buffers: shared hit=112
              ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.73 rows=60 width=0) (actual time=0.023..0.023 rows=60 loops=25)
                    Index Cond: (o0."PositionId" = o."Id")
                    Buffers: shared hit=61
Planning Time: 0.469 ms
Execution Time: 2.115 ms
```

## Title contains, all statuses

Execution time: 7.9 ms

```
Aggregate  (cost=492.25..492.26 rows=1 width=4) (actual time=7.830..7.831 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=0) (actual time=0.035..7.812 rows=111 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
        Rows Removed by Filter: 4889
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.193 ms
Execution Time: 7.852 ms
```

Execution time: 4.1 ms

```
Nested Loop Left Join  (cost=499.60..2596.45 rows=25 width=82) (actual time=3.884..3.943 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, COALESCE(((count(*))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer), 0)
  Buffers: shared hit=467
  ->  Limit  (cost=494.85..494.91 rows=25 width=58) (actual time=3.842..3.848 rows=25 loops=1)
        Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
        Buffers: shared hit=417
        ->  Sort  (cost=494.85..495.10 rows=101 width=58) (actual time=3.839..3.842 rows=25 loops=1)
              Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
              Sort Key: o."UpdatedAtUtc" DESC, o."Id"
              Sort Method: top-N heapsort  Memory: 31kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..492.00 rows=101 width=58) (actual time=0.029..3.771 rows=111 loops=1)
                    Output: o."Id", o."Location", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
                    Filter: (((o."NormalizedTitle")::text ~~ '%desarrollador 12%'::text) OR ((o."NormalizedLocation")::text ~~ '%desarrollador 12%'::text))
                    Rows Removed by Filter: 4889
                    Buffers: shared hit=417
  ->  GroupAggregate  (cost=4.75..84.03 rows=1 width=40) (actual time=0.003..0.003 rows=0 loops=25)
        Output: (count(*))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer, o0."PositionId"
        Buffers: shared hit=50
        ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.75..82.36 rows=60 width=24) (actual time=0.002..0.002 rows=0 loops=25)
              Output: o0."Id", o0."PositionId", o0."CandidateId", o0."Stage", o0."AddedAtUtc", o0."UpdatedAtUtc"
              Recheck Cond: (o0."PositionId" = o."Id")
              Buffers: shared hit=50
              ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.73 rows=60 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                    Index Cond: (o0."PositionId" = o."Id")
                    Buffers: shared hit=50
Planning:
  Buffers: shared hit=2
Planning Time: 0.492 ms
Execution Time: 4.096 ms
```

## Location contains, closed

Execution time: 2.1 ms

```
Aggregate  (cost=489.02..489.04 rows=1 width=4) (actual time=1.980..1.982 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=422
  ->  Bitmap Heap Scan on public."OPS_Positions" o  (cost=49.70..488.58 rows=179 width=0) (actual time=1.133..1.956 rows=179 loops=1)
        Recheck Cond: ((o."Status")::text = 'closed'::text)
        Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
        Rows Removed by Filter: 1071
        Heap Blocks: exact=408
        Buffers: shared hit=422
        ->  Bitmap Index Scan on "IX_OPS_Positions_Status_UpdatedAtUtc_Id"  (cost=0.00..49.66 rows=1250 width=0) (actual time=0.193..0.194 rows=1250 loops=1)
              Index Cond: ((o."Status")::text = 'closed'::text)
              Buffers: shared hit=14
Planning:
  Buffers: shared hit=2
Planning Time: 0.271 ms
Execution Time: 2.095 ms
```

Execution time: 5.3 ms

```
Nested Loop Left Join  (cost=5.03..2299.70 rows=25 width=100) (actual time=4.881..5.090 rows=25 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, COALESCE(((count(*))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer), 0), o."NormalizedTitle"
  Buffers: shared hit=179
  ->  Limit  (cost=0.28..198.16 rows=25 width=76) (actual time=4.853..4.997 rows=25 loops=1)
        Output: o."Id", o."Location", o."NormalizedTitle", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
        Buffers: shared hit=129
        ->  Index Scan using "IX_OPS_Positions_Status_NormalizedTitle_Id" on public."OPS_Positions" o  (cost=0.28..1417.10 rows=179 width=76) (actual time=4.850..4.991 rows=25 loops=1)
              Output: o."Id", o."Location", o."NormalizedTitle", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
              Index Cond: ((o."Status")::text = 'closed'::text)
              Filter: (((o."NormalizedTitle")::text ~~ '%malaga%'::text) OR ((o."NormalizedLocation")::text ~~ '%malaga%'::text))
              Rows Removed by Filter: 153
              Buffers: shared hit=129
  ->  GroupAggregate  (cost=4.75..84.03 rows=1 width=40) (actual time=0.003..0.003 rows=0 loops=25)
        Output: (count(*))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer, o0."PositionId"
        Buffers: shared hit=50
        ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.75..82.36 rows=60 width=24) (actual time=0.002..0.002 rows=0 loops=25)
              Output: o0."Id", o0."PositionId", o0."CandidateId", o0."Stage", o0."AddedAtUtc", o0."UpdatedAtUtc"
              Recheck Cond: (o0."PositionId" = o."Id")
              Buffers: shared hit=50
              ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.73 rows=60 width=0) (actual time=0.001..0.001 rows=0 loops=25)
                    Index Cond: (o0."PositionId" = o."Id")
                    Buffers: shared hit=50
Planning:
  Buffers: shared hit=2
Planning Time: 0.656 ms
Execution Time: 5.348 ms
```

## Deep page sorted by location

Execution time: 1.4 ms

```
Aggregate  (cost=479.50..479.51 rows=1 width=4) (actual time=1.376..1.377 rows=1 loops=1)
  Output: (count(*))::integer
  Buffers: shared hit=417
  ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=0) (actual time=0.017..0.997 rows=5000 loops=1)
        Output: "Id", "Title", "NormalizedTitle", "Description", "Location", "NormalizedLocation", "Status", "Requirements", "FilterSchemaVersion", "CreatedAtUtc", "UpdatedAtUtc"
        Buffers: shared hit=417
Planning:
  Buffers: shared hit=2
Planning Time: 0.108 ms
Execution Time: 1.409 ms
```

Execution time: 11.6 ms

```
Nested Loop Left Join  (cost=791.19..9192.85 rows=100 width=89) (actual time=10.177..11.299 rows=100 loops=1)
  Output: o."Id", o."Title", o."Location", o."Status", o."UpdatedAtUtc", o.xmin, COALESCE(((count(*))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer), 0), COALESCE(((count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer), 0), o."NormalizedLocation"
  Buffers: shared hit=651
  ->  Limit  (cost=786.44..786.69 rows=100 width=65) (actual time=10.122..10.148 rows=100 loops=1)
        Output: o."Id", o."Location", o."NormalizedLocation", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
        Buffers: shared hit=417
        ->  Sort  (cost=774.19..786.69 rows=5000 width=65) (actual time=9.442..9.835 rows=5000 loops=1)
              Output: o."Id", o."Location", o."NormalizedLocation", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
              Sort Key: o."NormalizedLocation", o."Id"
              Sort Method: quicksort  Memory: 857kB
              Buffers: shared hit=417
              ->  Seq Scan on public."OPS_Positions" o  (cost=0.00..467.00 rows=5000 width=65) (actual time=0.013..1.870 rows=5000 loops=1)
                    Output: o."Id", o."Location", o."NormalizedLocation", o."Status", o."Title", o."UpdatedAtUtc", o.xmin
                    Buffers: shared hit=417
  ->  GroupAggregate  (cost=4.75..84.03 rows=1 width=40) (actual time=0.011..0.011 rows=0 loops=100)
        Output: (count(*))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'new'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'shortlisted'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'interview'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'hired'::text)))::integer, (count(*) FILTER (WHERE ((o0."Stage")::text = 'rejected'::text)))::integer, o0."PositionId"
        Buffers: shared hit=234
        ->  Bitmap Heap Scan on public."OPS_PositionCandidates" o0  (cost=4.75..82.36 rows=60 width=24) (actual time=0.005..0.006 rows=8 loops=100)
              Output: o0."Id", o0."PositionId", o0."CandidateId", o0."Stage", o0."AddedAtUtc", o0."UpdatedAtUtc"
              Recheck Cond: (o0."PositionId" = o."Id")
              Heap Blocks: exact=30
              Buffers: shared hit=234
              ->  Bitmap Index Scan on "UX_OPS_PositionCandidates_PositionId_CandidateId"  (cost=0.00..4.73 rows=60 width=0) (actual time=0.004..0.004 rows=8 loops=100)
                    Index Cond: (o0."PositionId" = o."Id")
                    Buffers: shared hit=204
Planning Time: 0.292 ms
Execution Time: 11.592 ms
```
