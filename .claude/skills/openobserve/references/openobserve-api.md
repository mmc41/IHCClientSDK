# OpenObserve query API — reference for the `openobserve` skill

This is the detail layer. Read it when the helper script is not enough — building a
custom SQL query, understanding a field, or issuing a request by hand. The script
(`scripts/oo_query.py`) uses the Python standard library so it needs no `curl`/`jq`
installed, but the `curl` recipes below are handy for a quick manual poke and match
what the script echoes. Everything here was verified against the live IHC collector.

## How config maps to the API

The IHC apps export OTLP to OpenObserve using the `telemetry` section of
`ihcsettings.json`:

```json
"telemetry": {
  "Host":    "http://localhost:5080",
  "Traces":  "http://localhost:5080/api/default/v1/traces",
  "Logs":    "http://localhost:5080/api/default/v1/logs",
  "Headers": "Authorization=Basic <base64>, stream-name=Ihc, organization=default"
}
```

Derive the query API from it:

| Query API needs | Where it comes from |
| --- | --- |
| Base URL | scheme+host of `Traces`/`Logs` (`http://localhost:5080`) |
| `org_id` | path segment after `/api/` (`default`); or the `organization=` header |
| `Authorization` header | the `Authorization=...` pair in `Headers`, used **verbatim** |
| Stream name | discover it — do **not** trust `stream-name` |

> **Stream names are lowercased by OpenObserve.** The header says `stream-name=Ihc`
> but the real stream is `ihc`. Always discover names via the streams API rather than
> hardcoding. The `Authorization` value is plain HTTP Basic auth (base64), *not*
> affected by the `encryption.isEncrypted` flag, which only covers the `ihcclient`
> password — so the header is usable as-is.

## Endpoints

### List streams
```
GET {base}/api/{org}/streams?type=logs      # -> {"list":[{"name":"ihc",...}], ...}
GET {base}/api/{org}/streams?type=traces
GET {base}/api/{org}/streams?type=metrics   # one entry PER INSTRUMENT, not one per app
GET {base}/api/{org}/streams                # every type, each row carrying stream_type
```

### Search (logs, traces, metrics)
```
POST {base}/api/{org}/_search?type=logs      # type = logs | traces | metrics; logs is default
Content-Type: application/json
Authorization: Basic <base64>

{
  "query": {
    "sql": "SELECT * FROM \"ihc\" WHERE severity = 'Error' ORDER BY _timestamp DESC",
    "start_time": 1749372477912036,
    "end_time":   1783932477912229,
    "from": 0,
    "size": 100
  }
}
```
Response: `{ "hits": [ {...row...}, ... ], "total": N, "took": ms }`. A bad query
returns `{ "code": .., "message": ".." }` instead of `hits`.

- **Time is epoch MICROSECONDS** (Unix seconds × 1,000,000 — 16 digits). Both
  `start_time` and `end_time` are required despite the OpenAPI marking them optional.
- The stream name in the SQL `FROM` clause must be double-quoted and lowercase.
- `type=traces` is **required** to search the traces stream; omit `type` (or use
  `type=logs`) for logs.

### Raw curl (fallback / transparency)
```bash
curl -s -X POST "http://localhost:5080/api/default/_search?type=traces" \
  -H "Authorization: Basic <base64>" \
  -H "Content-Type: application/json" \
  -d '{"query":{"sql":"SELECT * FROM \"ihc\" WHERE span_status='"'"'ERROR'"'"'","start_time":1749372477912036,"end_time":1783932477912229,"from":0,"size":50}}'
```

## Field schemas (verified live)

### Logs stream (e.g. `ihc`)
`_timestamp` (µs), `time`, `severity` (**title-case**: `Information`, `Error`,
`Warning`, `Critical`), `body` (the message), `service_name`, `service_namespace`,
`service_version`, `service_instance_id`, `area`, `source` (Avalonia sink fields),
`instrumentation_library_name`, `telemetry_sdk_*`, `_originalformat_`.

Error filter: `severity IN ('Error','Critical','Fatal')` (.NET's top level is
`Critical`; `Fatal` is included only to catch non-.NET exporters). Warnings are
separate — add them only when asked.

### Traces stream (e.g. `ihc`)
`_timestamp` (µs), `start_time`, `end_time`, `duration` (**microseconds**),
`operation_name` (span name), `span_status` (`UNSET` | `OK` | `ERROR`), `span_kind`,
`service_name`, `trace_id`, `span_id`, `reference_parent_trace_id`,
`reference_parent_span_id`, `events` (JSON-string array of span events), `links`,
`flags`. IHC-specific span tags surface as their own columns, e.g.
`arguments_failed_count`, `arguments_restored_count`.

`links` is a JSON string, `[]` when there are none, otherwise
`[{"context":{"traceId":…,"spanId":…,…},…}]`. **Links survive ingestion**, so a span that
deliberately starts its own trace and links back stays joinable in SQL
(`links LIKE '%<trace_id>%'`) — which is the only way to reach one, since a link is not a
parent edge and no tree walk will find it. `--trace`/`--run` print them as
`-> links to trace <id>`.

Error filter: `span_status = 'ERROR'`.

> **Exceptions can hide on `UNSET` spans.** The SDK's `SetError(ex)` sets
> `span_status = 'ERROR'` *and* records an `exception` event, but paths that only
> `AddException(...)` (e.g. the global unhandled-exception handler) leave the status
> `UNSET`. To catch those too, also match `events LIKE '%exception%'`. Exception
> detail (`exception.type`, `exception.message`, `exception.stacktrace`) lives inside
> the `events` JSON. Warnings emitted via `AddWarning(...)` are span events tagged
> `severity=warning`, findable with `events LIKE '%warning%'`.

### Metrics streams (one per instrument) — verified live

The OTLP metrics endpoint is `{Host}/api/{org}/v1/metrics`, the sibling of the `Traces`
and `Logs` endpoints in `ihcsettings.json`. Storage differs from the other two signals in
ways that break naive queries:

**Stream naming.** One stream per instrument, dots replaced by underscores:
`ihc.edit.apply` is stream `ihc_edit_apply`. A histogram `x` is stored ONLY as the family
`x_bucket`, `x_count`, `x_sum` — plus `x_min` and `x_max` for an explicit-boundary
histogram. OpenObserve also registers an EMPTY stream named `x`, which `?type=metrics`
lists but `_search` rejects with `"Search stream not found"`. Never query the bare name of
a histogram.

**Common columns.** `_timestamp` (µs), `__name__` (the stream/metric name), `value`
(float), `start_time` (nanoseconds, unlike `_timestamp`), `aggregation_temporality`
(`AGGREGATION_TEMPORALITY_DELTA` | `..._CUMULATIVE`), `is_monotonic` (counters),
`flag`, `exemplars`, `__hash__`, the resource fields (`service_name`,
`service_namespace`, `service_version`, `service_instance_id`,
`instrumentation_library_name`, `telemetry_sdk_*`), and one column per instrument
dimension with dots flattened to underscores (`ihc.service` → `ihc_service`).

**`le` on `_bucket` rows is the boundary and counts are CUMULATIVE**, Prometheus-style,
ending at a literal `le = 'inf'` row holding the total. One bucket's own count is the
difference between adjacent rows. `le` is stored as a STRING, so ordering needs
`ORDER BY CAST(le AS DOUBLE)` — `'inf'` casts to infinity and sorts last correctly.

**Run scoping uses `service_instance_id`** — the LOGS spelling, NOT the traces
`service_service_instance_id`. Using the traces spelling here matches nothing and looks
like "the metric never arrived".

**Exemplars survive.** `exemplars` is a JSON-string array of
`{trace_id, span_id, value, _timestamp}` recorded when a measurement was taken inside a
sampled Activity (the exporting SDK must enable a trace-based exemplar filter). This is
the metric→trace join: a suspicious bucket leads straight to the span that filled it.
Note that the array is repeated on EVERY row of a histogram family, so it is a poor thing
to `SELECT *` in bulk.

> **A Base2 exponential histogram survives ingestion but is NOT queryable as a
> distribution.** Its `_bucket` rows carry a bucket INDEX in `le` (observed range −47…63
> for values spanning 0.004–44 s), the counts are per-bucket rather than cumulative, and
> no scale or base field is exported anywhere on the row — so the indices cannot be
> converted back to seconds by any query. It also amplifies rows enormously: 8
> measurements produced 108 bucket rows versus 11 for the explicit-boundary form.
> `_sum` and `_count` remain correct, so nothing is lost, but percentiles and heatmaps
> are not derivable. **Prefer explicit boundaries** for anything you intend to query.

## SQL cookbook

```sql
-- Newest error logs
SELECT _timestamp, service_name, severity, body FROM "ihc"
WHERE severity IN ('Error','Critical','Fatal') ORDER BY _timestamp DESC;

-- Error / exception spans
SELECT _timestamp, service_name, operation_name, span_status, duration, trace_id, events
FROM "ihc" WHERE span_status = 'ERROR' OR events LIKE '%exception%' ORDER BY _timestamp DESC;

-- Full text hunt in log bodies
SELECT _timestamp, service_name, severity, body FROM "ihc"
WHERE body LIKE '%timeout%' ORDER BY _timestamp DESC;

-- All spans of one trace (follow a trace_id found above). `--trace <ID>` DRAWS this as a
-- tree; the SQL is here for when you want the raw rows or extra columns.
SELECT operation_name, span_status, duration, span_id, reference_parent_span_id
FROM "ihc" WHERE trace_id = '<TRACE_ID>' ORDER BY start_time;

-- Every span of ONE app launch (`--run` draws these as trees). The traces spelling of the
-- run id; `:run` in --sql expands to exactly this predicate for the newest launch.
SELECT operation_name, span_id, reference_parent_span_id, trace_id, duration, start_time, links
FROM "ihc" WHERE service_service_instance_id = '<RUN_ID>' ORDER BY start_time;

-- Which operations START a trace (i.e. have no parent), and how often. A root that is not a
-- named operation - a gesture, a launch, a deliberately linked background run - is a fragment.
-- Scope to ONE run before concluding anything: over a long window this mixes builds, and an
-- operation that was a root only in an older build is not evidence about today.
SELECT operation_name, count(*) AS n FROM "ihc"
WHERE reference_parent_span_id IS NULL AND service_name = 'IhcOpenVisual'
GROUP BY operation_name ORDER BY n DESC;

-- Slowest spans (duration is microseconds)
SELECT operation_name, duration, trace_id FROM "ihc" ORDER BY duration DESC;

-- Error counts by service
SELECT service_name, count(*) AS n FROM "ihc"
WHERE severity IN ('Error','Critical','Fatal') GROUP BY service_name ORDER BY n DESC;
```

Metrics (`?type=metrics`; note `service_instance_id`, the LOGS spelling):

```sql
-- Every point of a counter for one launch
SELECT _timestamp, value, aggregation_temporality FROM "ihc_edit_apply"
WHERE service_instance_id = '<RUN_ID>' ORDER BY _timestamp DESC;

-- A histogram's distribution: cumulative counts per boundary, 'inf' last
SELECT le, value FROM "ihc_edit_apply_duration_bucket"
WHERE service_instance_id = '<RUN_ID>' ORDER BY CAST(le AS DOUBLE);

-- Mean duration of a histogram over a window
SELECT (SELECT sum(value) FROM "ihc_edit_apply_duration_sum") /
       (SELECT sum(value) FROM "ihc_edit_apply_duration_count") AS mean_seconds;

-- Which dimension values a counter was actually recorded with. Four now: ok / refused / cancelled / failed.
-- Two renames sit in the history of these columns, and neither falls back — rows exported by an older
-- build keep the old column name and are NOT matched by the new one:
--   ihc_edit_status -> ihc_operation_status   (the status is on every operation, not only edits)
--   ihc_operation   -> ihc_operation_name     (the old name was a value AND the namespace of the one above)
SELECT ihc_operation_status, sum(value) AS n FROM "ihc_edit_apply"
GROUP BY ihc_operation_status ORDER BY n DESC;

-- A gesture's own failure rate, which is what the roll-up bought: a handled failure deep in a workflow now
-- reaches the invocation count, so this needs no join to the spans underneath.
SELECT ihc_command_id, ihc_operation_status, sum(value) AS n FROM "ihc_command_invocation"
GROUP BY ihc_command_id, ihc_operation_status ORDER BY n DESC;

-- Follow a metric point to its trace (exemplars is a JSON string)
SELECT _timestamp, value, exemplars FROM "ihc_edit_apply"
WHERE exemplars <> '[]' ORDER BY _timestamp DESC;
```

## Indexing delay

OpenObserve buffers ingestion (memtable → WAL → parquet) and makes data searchable
on a short delay: usually a few seconds, up to ~30s+ under load or with batching/OTLP
export intervals. Consequences for diagnosis:

- **Absence of an error is not proof of health** right after you triggered it. Wait
  and re-query before concluding a run was clean.
- **Old vs new:** the stream holds every past launch. Each launch has a distinct
  `service_instance_id` (traces: `service_service_instance_id`) — the script's `run=`
  tag and `--latest-run` filter use it to keep a stale error from an earlier run from
  being mistaken for the current one. Always weigh a hit's age (printed as `… ago`)
  against the current time before attributing it to "the run I just did".
- When hunting an error you just produced, widen `--since`, then poll: the script's
  `--wait 20 --retries 4` re-runs until something shows up or the budget is spent.
- Timestamps are the controller/app clock; a wide-enough window absorbs small clock skew.

## Deep-dive in the UI

To hand a human the full waterfall, build a link from `telemetry.Host`:
`{Host}/web/traces?stream=ihc&org_identifier={org}` and have them search the
`trace_id`. The `trace_id` printed by the script is the join key between a log line
and its span.

For your own reading, `--trace <ID>` and `--run` draw the same nesting in the terminal —
faster than a browser round-trip, and the output is diffable between two runs.
