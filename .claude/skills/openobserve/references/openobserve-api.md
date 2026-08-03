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

Error filter: `span_status = 'ERROR'`.

> **Exceptions can hide on `UNSET` spans.** The SDK's `SetError(ex)` sets
> `span_status = 'ERROR'` *and* records an `exception` event, but paths that only
> `AddException(...)` (e.g. the global unhandled-exception handler) leave the status
> `UNSET`. To catch those too, also match `events LIKE '%exception%'`. Exception
> detail (`exception.type`, `exception.message`, `exception.stacktrace`) lives inside
> the `events` JSON. Warnings emitted via `AddWarning(...)` are span events tagged
> `severity=warning`, findable with `events LIKE '%warning%'`.

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

-- All spans of one trace (follow a trace_id found above)
SELECT operation_name, span_status, duration, span_id, reference_parent_span_id
FROM "ihc" WHERE trace_id = '<TRACE_ID>' ORDER BY start_time;

-- Slowest spans (duration is microseconds)
SELECT operation_name, duration, trace_id FROM "ihc" ORDER BY duration DESC;

-- Error counts by service
SELECT service_name, count(*) AS n FROM "ihc"
WHERE severity IN ('Error','Critical','Fatal') GROUP BY service_name ORDER BY n DESC;
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
