#!/usr/bin/env python3
"""
oo_query.py - Query OpenObserve logs and traces for errors during development.

Reads the `telemetry` section of ihcsettings.json (the same settings the IHC apps
export to), discovers the real log/trace stream names, and runs OpenObserve's
`_search` API over `curl`. Built for an LLM to check for and diagnose errors while
developing against the IHC SDK/apps.

Nothing here prints the Authorization credential: the equivalent curl command is
echoed to stderr with the auth value redacted so the call stays reproducible.

Examples:
    # Newest errors from both logs and traces in the last 15 minutes (the default):
    python oo_query.py --errors

    # Widen the window while hunting for an error you just triggered:
    python oo_query.py --errors --since 2h

    # Poll a few times to defeat indexing delay (see note at bottom of output):
    python oo_query.py --errors --since 30m --wait 20 --retries 4

    # List the actual stream names OpenObserve stores:
    python oo_query.py --list-streams

    # Run any SQL yourself (stream names are lowercased by OpenObserve):
    python oo_query.py --type traces --sql 'SELECT * FROM "ihc" WHERE duration > 500000 ORDER BY duration DESC' --size 20
"""
import argparse
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

CONNECT_HINT = (
    "Could not reach OpenObserve. Is the collector running and is telemetry.Host in "
    "ihcsettings.json correct? Try opening the Host URL in a browser."
)
# Log severities that count as an error. OpenObserve stores the .NET LogLevel name
# verbatim (title-case), but we include upper-case variants for other exporters.
ERROR_SEVERITIES = ("Error", "Critical", "Fatal", "ERROR", "CRITICAL", "FATAL")


def find_config(explicit):
    if explicit:
        return explicit
    d = os.getcwd()
    while True:
        candidate = os.path.join(d, "ihcsettings.json")
        if os.path.isfile(candidate):
            return candidate
        parent = os.path.dirname(d)
        if parent == d:
            break
        d = parent
    # Fall back to a repo-root guess relative to this script (.../.claude/skills/openobserve/scripts).
    guess = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", "..", "ihcsettings.json"))
    return guess if os.path.isfile(guess) else "ihcsettings.json"


def load_telemetry(config_path):
    """Return (base_url, org, auth_header_value) from the telemetry section."""
    with open(config_path, "r", encoding="utf-8") as f:
        cfg = json.load(f)
    tel = cfg.get("telemetry")
    if not tel:
        sys.exit(f"No 'telemetry' section in {config_path}.")
    endpoint = tel.get("Traces") or tel.get("Logs") or ""
    m = re.match(r"^(https?://[^/]+)/api/([^/]+)/v1/(?:traces|logs)/?$", endpoint.strip())
    if not m:
        host = (tel.get("Host") or "").strip().rstrip("/")
        if not host:
            sys.exit(
                "Could not derive the API base URL/org from telemetry.Traces/Logs, and "
                "telemetry.Host is empty. Fill in the telemetry section of ihcsettings.json."
            )
        base, org = host, None
    else:
        base, org = m.group(1), m.group(2)

    headers = {}
    for part in (tel.get("Headers") or "").split(","):
        key, sep, val = part.strip().partition("=")
        if not sep:
            continue
        headers[key.strip().lower()] = val.strip()
    if org is None:
        org = headers.get("organization", "default")
    auth = headers.get("authorization")
    if not auth:
        sys.exit(
            "No Authorization found in telemetry.Headers. Expected "
            "'Authorization=Basic <token>, stream-name=..., organization=...'."
        )
    return base, org, auth


def http_json(auth, url, body=None, quiet=False):
    """
    Issue the request via the Python stdlib (works identically on Windows/Linux/mac, no
    external binary needed) and return parsed JSON. Echoes an equivalent, credential-
    redacted curl command to stderr so the call is transparent and copy-pasteable.
    OpenObserve replies with JSON even on 4xx (a bad query -> {"code","message"}), so
    HTTP errors are read and parsed rather than treated as failures.
    """
    if not quiet:
        shown = ["curl", "-s", "-H", "Authorization: Basic ***"]
        if body is not None:
            shown += ["-H", "Content-Type: application/json", "-X", "POST", url, "-d", body]
        else:
            shown.append(url)
        print("+ " + " ".join(shown), file=sys.stderr)

    data = body.encode("utf-8") if body is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if data is not None else "GET")
    req.add_header("Authorization", auth)
    if data is not None:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            raw = resp.read().decode("utf-8")
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            sys.exit(
                f"OpenObserve rejected the credentials (HTTP {e.code}) at {url}. Check the "
                f"Authorization value in telemetry.Headers of ihcsettings.json."
            )
        raw = e.read().decode("utf-8", "replace")  # Other 4xx (e.g. a bad query) carry a JSON error body.
    except (urllib.error.URLError, TimeoutError, OSError) as e:
        # Connection refused / DNS failure / timeout: the collector is almost certainly not running
        # or the endpoint is wrong. Report cleanly (no stack trace) so the caller knows what to fix.
        reason = getattr(e, "reason", None) or e
        sys.exit(f"{CONNECT_HINT}\nEndpoint: {url}\nDetail: {reason}")
    if not raw.strip():
        sys.exit(CONNECT_HINT)
    try:
        return json.loads(raw)
    except json.JSONDecodeError:
        sys.exit(f"Unexpected non-JSON response:\n{raw[:500]}")


def list_streams(base, org, auth, stream_type):
    data = http_json(auth, f"{base}/api/{org}/streams?type={stream_type}", quiet=True)
    return [s["name"] for s in data.get("list", [])]


def pick_stream(base, org, auth, stream_type, override):
    if override:
        return override
    streams = list_streams(base, org, auth, stream_type)
    if not streams:
        return None
    # Prefer the first non-'default' stream (the app's own stream), else 'default'.
    non_default = [s for s in streams if s != "default"]
    return non_default[0] if non_default else streams[0]


SINCE_UNITS = {"s": 1, "m": 60, "h": 3600, "d": 86400}


def since_to_micros(since):
    m = re.match(r"^(\d+)([smhd])$", since.strip())
    if not m:
        sys.exit("--since must look like 15m, 2h, 30s or 1d.")
    return int(m.group(1)) * SINCE_UNITS[m.group(2)] * 1_000_000


def search(base, org, auth, stream_type, sql, start, end, size, quiet):
    body = json.dumps({"query": {"sql": sql, "start_time": start, "end_time": end, "from": 0, "size": size}})
    data = http_json(auth, f"{base}/api/{org}/_search?type={stream_type}", body=body, quiet=quiet)
    if "hits" not in data:
        # OpenObserve returns {"code":.., "message":..} on a bad query.
        msg = data.get("message") or data.get("error") or json.dumps(data)[:300]
        sys.exit(f"Search failed: {msg}\nSQL was: {sql}")
    return data["hits"]


def in_list(field, values):
    return f"{field} IN (" + ", ".join("'" + v.replace(chr(39), chr(39) * 2) + "'" for v in values) + ")"


def now_micros():
    return int(time.time() * 1_000_000)


def micros_to_iso(us):
    try:
        return time.strftime("%Y-%m-%d %H:%M:%S", time.localtime(int(us) / 1_000_000))
    except (ValueError, TypeError):
        return str(us)


def fmt_age(us, now):
    """Human 'age' of a record relative to now (both microseconds), e.g. '54m ago'."""
    try:
        secs = max(0.0, (now - int(us)) / 1_000_000)
    except (ValueError, TypeError):
        return "?"
    for unit, n in (("d", 86400), ("h", 3600), ("m", 60)):
        if secs >= n:
            return f"{secs / n:.0f}{unit} ago"
    return f"{secs:.0f}s ago"


def short_inst(h):
    """First 8 chars of the per-launch service_instance_id, so runs are distinguishable."""
    iid = h.get("service_instance_id") or h.get("service_service_instance_id") or ""
    return iid[:8] if iid else "--------"


def summarize_log(h, now):
    ts = micros_to_iso(h.get("_timestamp"))
    svc = h.get("service_name") or h.get("service_namespace") or "?"
    sev = h.get("severity") or "?"
    body = (h.get("body") or "").strip().replace("\n", " ")
    return f"[{ts} | {fmt_age(h.get('_timestamp'), now)}] {sev:8} {svc} run={short_inst(h)}: {body}"


def extract_exception(events):
    """Best-effort pull of exception.type/message out of the trace 'events' JSON string."""
    if not events or events in ("[]", "null"):
        return None
    try:
        arr = json.loads(events) if isinstance(events, str) else events
    except (json.JSONDecodeError, TypeError):
        return None
    for ev in arr if isinstance(arr, list) else []:
        if not isinstance(ev, dict):
            continue
        etype = ev.get("exception.type") or ev.get("exception_type")
        emsg = ev.get("exception.message") or ev.get("exception_message")
        if etype or emsg or ev.get("name") == "exception":
            return f"{etype or 'exception'}: {(emsg or '').strip()}".strip()
    return None


def summarize_trace(h, now):
    ts = micros_to_iso(h.get("_timestamp"))
    svc = h.get("service_name") or "?"
    op = h.get("operation_name") or "?"
    status = h.get("span_status") or "?"
    dur_ms = ""
    try:
        dur_ms = f" {int(h['duration']) / 1000:.1f}ms"
    except (KeyError, ValueError, TypeError):
        pass
    line = f"[{ts} | {fmt_age(h.get('_timestamp'), now)}] {status:6} {svc} :: {op}{dur_ms}  trace_id={h.get('trace_id')}"
    exc = extract_exception(h.get("events"))
    if exc:
        line += f"\n         ! {exc}"
    return line


def latest_instance(base, org, auth, stream, stream_type, inst_field, start, end):
    """Newest per-launch instance id in the stream, so we can scope to 'the run I just did'."""
    sql = (f'SELECT {inst_field} FROM "{stream}" WHERE {inst_field} IS NOT NULL '
           f"ORDER BY _timestamp DESC")
    hits = search(base, org, auth, stream_type, sql, start, end, 1, quiet=True)
    return hits[0].get(inst_field) if hits else None


def run_once(args, base, org, auth, log_stream, trace_stream, start, end, quiet):
    """One pass. `end` doubles as 'now' for age display. Returns total hit count."""
    now = end
    total = 0
    want_logs = args.type in ("logs", "both")
    want_traces = args.type in ("traces", "both")

    if args.sql:
        stream_type = args.type if args.type in ("logs", "traces") else "logs"
        hits = search(base, org, auth, stream_type, args.sql, start, end, args.size, quiet)
        print(f"\n=== {stream_type} ({len(hits)}) ===")
        for h in hits:
            print(json.dumps(h) if args.json else (summarize_trace(h, now) if stream_type == "traces" else summarize_log(h, now)))
        return len(hits)

    if want_logs and log_stream:
        sev = ERROR_SEVERITIES + ("Warning", "WARN", "WARNING") if args.include_warnings else ERROR_SEVERITIES
        where = in_list("severity", sev)
        scope = ""
        if args.latest_run:
            iid = latest_instance(base, org, auth, log_stream, "logs", "service_instance_id", start, end)
            if iid:
                where += f" AND service_instance_id = '{iid}'"
                scope = f" [latest run {iid[:8]}]"
        sql = (f'SELECT _timestamp, service_name, service_instance_id, severity, body FROM "{log_stream}" '
               f"WHERE {where} ORDER BY _timestamp DESC")
        hits = search(base, org, auth, "logs", sql, start, end, args.size, quiet)
        total += len(hits)
        print(f"\n=== error logs in '{log_stream}'{scope} ({len(hits)}) ===")
        for h in hits:
            print(json.dumps(h) if args.json else summarize_log(h, now))

    if want_traces and trace_stream:
        where = "(span_status = 'ERROR'"
        where += " OR events LIKE '%exception%')" if not args.no_exception_events else ")"
        scope = ""
        if args.latest_run:
            iid = latest_instance(base, org, auth, trace_stream, "traces", "service_service_instance_id", start, end)
            if iid:
                where += f" AND service_service_instance_id = '{iid}'"
                scope = f" [latest run {iid[:8]}]"
        cols = ("_timestamp, service_name, service_service_instance_id, operation_name, "
                "span_status, duration, trace_id, events")
        sql = f'SELECT {cols} FROM "{trace_stream}" WHERE {where} ORDER BY _timestamp DESC'
        try:
            hits = search(base, org, auth, "traces", sql, start, end, args.size, quiet)
        except SystemExit:
            # Fall back to status-only if the events LIKE clause upset the query engine.
            where2 = "span_status = 'ERROR'" + (where[where.index(" AND "):] if " AND " in where else "")
            sql = f'SELECT {cols} FROM "{trace_stream}" WHERE {where2} ORDER BY _timestamp DESC'
            hits = search(base, org, auth, "traces", sql, start, end, args.size, quiet)
        total += len(hits)
        print(f"\n=== error/exception spans in '{trace_stream}'{scope} ({len(hits)}) ===")
        for h in hits:
            print(json.dumps(h) if args.json else summarize_trace(h, now))

    return total


def main():
    p = argparse.ArgumentParser(description="Query OpenObserve for errors in IHC logs and traces.")
    p.add_argument("--config", help="Path to ihcsettings.json (default: search upward from cwd).")
    # --errors is a no-op alias: the canned error query is already the default action
    # (run whenever neither --list-streams nor --sql is given). Kept so the documented,
    # self-explaining `--errors` invocation works.
    p.add_argument("--errors", action="store_true", help="Canned error query over logs and/or traces (the default action; this flag is optional).")
    p.add_argument("--type", choices=["logs", "traces", "both"], default="both", help="Which streams to query.")
    p.add_argument("--since", default="15m", help="Look-back window: 30s, 15m, 2h, 1d. Default 15m.")
    p.add_argument("--size", type=int, default=100, help="Max rows to return. Default 100.")
    p.add_argument("--stream", help="Override the discovered stream name.")
    p.add_argument("--sql", help="Run custom SQL instead of the canned error query (use with --type logs|traces).")
    p.add_argument("--list-streams", action="store_true", help="List stream names and exit.")
    p.add_argument("--latest-run", action="store_true",
                   help="Scope to the newest app launch (service_instance_id), so stale errors from earlier runs are excluded.")
    p.add_argument("--include-warnings", action="store_true", help="Also include Warning-severity logs.")
    p.add_argument("--no-exception-events", action="store_true", help="Traces: match only span_status='ERROR'.")
    p.add_argument("--json", action="store_true", help="Print raw JSON hits instead of a compact summary.")
    p.add_argument("--wait", type=int, default=0, help="Seconds to wait between retries (for indexing delay).")
    p.add_argument("--retries", type=int, default=1, help="Number of passes; pair with --wait to poll (without --wait they run back-to-back). Default 1.")
    p.add_argument("--quiet", action="store_true", help="Do not echo the equivalent curl command.")
    args = p.parse_args()

    config_path = find_config(args.config)
    if not os.path.isfile(config_path):
        sys.exit(f"ihcsettings.json not found (looked at {config_path}). Pass --config PATH.")
    base, org, auth = load_telemetry(config_path)

    if args.list_streams:
        print(f"config: {config_path}\nbase: {base}  org: {org}")
        print("logs  :", ", ".join(list_streams(base, org, auth, "logs")) or "(none)")
        print("traces:", ", ".join(list_streams(base, org, auth, "traces")) or "(none)")
        return

    log_stream = pick_stream(base, org, auth, "logs", args.stream) if args.type in ("logs", "both") else None
    trace_stream = pick_stream(base, org, auth, "traces", args.stream) if args.type in ("traces", "both") else None
    # Streams the canned query can actually hit. Empty means none of the requested types
    # exist yet — a different situation from "stream exists but held no matching rows".
    queried_streams = [s for s in (log_stream, trace_stream) if s]

    now = now_micros()
    start = now - since_to_micros(args.since)

    print(f"config: {config_path}  |  base: {base}  org: {org}", file=sys.stderr)
    print(f"now: {micros_to_iso(now)}  |  window: last {args.since}"
          + ("  |  scope: latest run only" if args.latest_run else ""), file=sys.stderr)
    total = 0
    for attempt in range(1, max(1, args.retries) + 1):
        end = now_micros()
        total = run_once(args, base, org, auth, log_stream, trace_stream, start, end, quiet=args.quiet or attempt > 1)
        if total > 0 or attempt >= args.retries:
            break
        if args.wait > 0:
            print(f"\n(no hits yet; waiting {args.wait}s for indexing, attempt {attempt}/{args.retries}...)", file=sys.stderr)
            time.sleep(args.wait)

    if total == 0:
        if not args.sql and not queried_streams:
            print(
                f"\nNo '{args.type}' stream exists yet in org '{org}'. Nothing has been ingested for it, "
                "so there is nothing to query (more --wait/--since will not help). Run the app so it "
                "exports telemetry, then re-check. `--list-streams` shows what currently exists."
            )
        else:
            print(
                "\nNo matching rows found. NOTE: OpenObserve indexes ingested data on a short delay "
                "(a few seconds up to ~30s+ under load), so an error you just triggered may not be searchable "
                "yet. Re-run with a wider --since and/or `--wait 20 --retries 4` before concluding it is clean."
            )


if __name__ == "__main__":
    main()
