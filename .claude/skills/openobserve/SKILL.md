---
name: openobserve
description: >-
  Check for and diagnose runtime errors in this repo's OpenObserve logs and traces
  during development, using the OpenObserve query API with the telemetry settings in
  ihcsettings.json (cross-platform, Python-stdlib only). Use this WHENEVER you need to know whether a run
  actually failed and why — after running an IHC app/example/utility, after a
  reported bug or exception, when an operation "does nothing" or silently fails, when
  investigating errors/warnings/timeouts/slow spans, or when the user mentions
  OpenObserve, OTel, telemetry, traces, spans, or observability. Prefer this over
  guessing from source code alone, since the controller and OTLP export happen out of
  process. Accounts for OpenObserve's short indexing delay so a just-triggered error
  is not missed.
---

# OpenObserve error lookup & diagnosis

The IHC apps, examples, and utilities export OpenTelemetry **logs** and **traces** to
an OpenObserve collector configured in the `telemetry` section of `ihcsettings.json`.
Much of what goes wrong at runtime — controller/SOAP failures, dropped telemetry,
unhandled exceptions, slow operations — is only visible there, not in the source or
the console. This skill queries that data so you can confirm whether a run failed and
diagnose why.

## When to reach for this

- Right after running any IHC app/example/utility, to verify it did what it should.
- A user reports a bug, exception, "it silently does nothing", a timeout, or slowness.
- You changed something and want evidence it works end-to-end, not just that tests pass.
- The user mentions OpenObserve, telemetry, OTel, traces, spans, logs, or observability.

If the answer lives in runtime behavior, look here rather than inferring from code.

## The one tool you need

`scripts/oo_query.py` reads `ihcsettings.json`, discovers the real stream names, and
runs the OpenObserve `_search` API. It uses only the Python 3 standard library, so it
behaves identically on Windows, Linux, and macOS with no `curl`/`jq` to install. It
never prints the credential — for transparency it echoes the equivalent `curl`
command (auth redacted) so any call is easy to copy, paste, and reproduce by hand.
Run it from the repo root (it also searches upward for `ihcsettings.json`).

```bash
# Newest errors from BOTH logs and traces in the last 15 minutes — the default check:
python .claude/skills/openobserve/scripts/oo_query.py --errors

# "I just ran X" — scope to the newest launch so OLD errors from earlier runs
# are not mistaken for this run. Widen the window; --latest-run does the filtering:
python .claude/skills/openobserve/scripts/oo_query.py --errors --since 1d --latest-run

# Verify a run you just did, defeating indexing delay by polling:
python .claude/skills/openobserve/scripts/oo_query.py --errors --since 30m --wait 20 --retries 4

# See what streams/data actually exist:
python .claude/skills/openobserve/scripts/oo_query.py --list-streams

# Only traces, or only logs:
python .claude/skills/openobserve/scripts/oo_query.py --errors --type traces --since 2h

# Diagnose deeper with your own SQL (stream name is lowercase; see the reference):
python .claude/skills/openobserve/scripts/oo_query.py --type traces \
  --sql 'SELECT operation_name,span_status,duration,trace_id FROM "ihc" ORDER BY duration DESC' --size 20
```

Useful flags: `--since 30s|15m|2h|1d` (window, default 15m), `--latest-run` (only the
newest app launch), `--size N`, `--include-warnings`, `--no-exception-events` (traces:
match only `span_status='ERROR'`, skip exception-carrying spans), `--stream NAME`
(override discovery), `--json` (raw hits), `--config PATH`, `--quiet` (don't echo the
curl command), `--wait S --retries N` (poll for delayed data). `--errors` is optional —
the error query is the default action.

## Don't confuse old errors with new ones

The stream accumulates telemetry from *every* past app launch, so an error you find may
be stale — from a run hours ago, not the one you just did. Two safeguards, both built in:

- **Every hit shows its age and run id**, e.g. `[2026-07-13 10:13:08 | 57m ago] Error
  IhcLab run=7f98afbe`. The header prints the current time (`now:`). Read the age
  before attributing an error to the current run — "57m ago" is almost certainly not
  "the run I just did". Each app launch has a distinct `run=` id (its
  `service_instance_id`).
- **`--latest-run` scopes to the newest launch**, filtering out every earlier run's
  noise. Use it whenever the user says "I just ran X" / "did my run error". A narrow
  time window is the wrong tool for this — it can miss an error that landed just outside
  the window *and* still include a stale one; instead sweep a wide `--since` and let
  `--latest-run` (or the printed age) separate current from old.

## Mind the indexing delay — this is the common trap

OpenObserve makes ingested data searchable on a short delay: usually a few seconds,
up to ~30s+ under load or because of OTLP batching. So **finding no error right after
a run does not mean the run was clean.** When you're checking something you just
triggered:

1. Give it a moment, or just use `--wait 20 --retries 4` so the script re-queries
   until data appears or the budget is spent.
2. Widen the window (`--since 30m`) — the timestamp is the app's clock, and a wider
   window absorbs both the delay and small clock skew.
3. Only conclude "no errors" after a real look-back has come up empty. The script
   prints this reminder whenever it finds nothing.

## Diagnosis workflow

1. **Scan** for errors: `--errors` over the window that covers the run.
2. **Read the summary.** Each error log shows time, severity, service, and message.
   Each error/exception span shows time, `span_status`, service, `operation_name`,
   duration, `trace_id`, and — when present — the exception type and message pulled
   from the span's `events`.
3. **Follow the `trace_id`.** It is the join key between a log line and its span. Pull
   the whole trace to see the operation's parent/child spans and where it broke:
   `--type traces --sql 'SELECT operation_name,span_status,duration,span_id,reference_parent_span_id FROM "ihc" WHERE trace_id='"'"'<ID>'"'"' ORDER BY start_time'`.
4. **Correlate with the code.** Span `operation_name`s map to SDK methods (the
   `ihcclient` ActivitySource); input args and return values are recorded as span tags
   (`input.*`, `retv`). Use them to pinpoint the failing call, then read that source.
5. **Report** the concrete finding: what failed, the message/exception, the service
   and operation, and the `trace_id` so a human can open the full waterfall in the
   OpenObserve UI (`telemetry.Host`).

## What counts as an error

- **Logs:** `severity IN ('Error','Critical','Fatal')` (OpenObserve stores the .NET
  level name title-cased). Add warnings with `--include-warnings`.
- **Traces:** `span_status = 'ERROR'`, plus spans carrying an `exception` event even
  when their status stayed `UNSET` (the script matches both by default). Exception
  detail lives in the span's `events` field.

## When it can't connect

The script fails fast with a clear, single-line message (no stack trace) and a non-zero
exit code when it can't get data, and the message tells you which case you're in:

- **"Could not reach OpenObserve … Endpoint: … Detail: connection refused/timeout"** —
  the collector is not running or `telemetry.Host`/endpoints are wrong. Confirm by
  opening the `Host` URL in a browser and check the app's startup telemetry self-check.
- **"OpenObserve rejected the credentials (HTTP 401/403)"** — the `Authorization` value
  in `telemetry.Headers` is wrong or expired; refresh it from the OpenObserve UI.
- **"No 'telemetry' section"** — telemetry isn't configured for this repo/settings file.

Do not report "no errors" when the query never actually ran — an error exit means
*unknown*, not *clean*.

## Going deeper

For the exact API shapes, the full verified field schemas for logs and traces, a SQL
cookbook, and raw `curl` recipes, read `references/openobserve-api.md`. Consult it
before writing non-trivial custom SQL.
