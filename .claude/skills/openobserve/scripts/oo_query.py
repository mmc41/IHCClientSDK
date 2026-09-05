#!/usr/bin/env python3
"""
oo_query.py - Query OpenObserve logs, traces and metrics for errors during development.

Reads the `telemetry` section of ihcsettings.json (the same settings the IHC apps
export to), discovers the real log/trace/metric stream names, and runs OpenObserve's
`_search` API over `curl`. Built for an LLM to check for and diagnose errors while
developing against the IHC SDK/apps -- and, with --trace/--run, to read a trace's
parent/child nesting as a tree rather than as a flat rowset to reassemble by hand.

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

    # ONE TRACE AS A TREE - what ran inside what, and how long the parent covered:
    python oo_query.py --trace 5bab50c64950fa09bae9eecdc2c9083a

    # EVERY TRACE OF THE NEWEST LAUNCH, as trees. The view that answers "is this
    # workflow one tree, and does its root time the whole thing":
    python oo_query.py --run --since 2h --size 500

    # Run any SQL yourself (stream names are lowercased by OpenObserve). `:run` expands to
    # the newest launch's instance predicate, with the right field name for the signal:
    python oo_query.py --type traces --sql 'SELECT * FROM "ihc" WHERE duration > 500000 ORDER BY duration DESC' --size 20
    python oo_query.py --type traces --sql 'SELECT operation_name, duration FROM "ihc" WHERE :run ORDER BY duration DESC'

    # Metrics: every point of one instrument, newest first, from the last app launch.
    # The dotted instrument name is accepted; OpenObserve's underscore form also works.
    python oo_query.py --metric ihc.edit.apply --latest-run

    # A histogram resolves to its whole _bucket/_count/_sum family in one call:
    python oo_query.py --metric ihc.edit.apply.duration --latest-run

    # What instruments has anything ever exported?
    python oo_query.py --type metrics --list-streams

METRICS DIFFER FROM LOGS AND TRACES IN FOUR WAYS THAT CHANGE HOW YOU QUERY THEM.
  1. There is no single metrics stream. OpenObserve creates ONE STREAM PER INSTRUMENT,
     named with dots replaced by underscores, so `ihc.edit.apply` is stream
     `ihc_edit_apply`. `--metric` does that translation for you.
  2. A histogram is DECOMPOSED, Prometheus-style, into `<name>_bucket`, `<name>_count`
     and `<name>_sum` (explicit-boundary histograms additionally get `_min`/`_max`).
     There is no stream under the bare histogram name holding the whole distribution.
  3. In `_bucket` rows the boundary lives in the `le` column and the counts are
     CUMULATIVE ("less than or equal to"), with a final `le = 'inf'` row carrying the
     total -- so the per-bucket count is a difference between adjacent rows, not `value`.
  4. Metric rows scope to a launch with `service_instance_id`, the LOGS spelling.
     Traces use `service_service_instance_id`. `--latest-run` picks the right one.
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

# Suffixes OpenObserve appends when it decomposes a histogram into separate streams.
# `_min`/`_max` appear for explicit-boundary histograms only, so a resolver must treat
# every one of these as optional rather than assume a fixed family.
HISTOGRAM_SUFFIXES = ("bucket", "count", "sum", "min", "max")

# Metric rows carry the per-launch id under the LOGS spelling, not the traces one.
# Measured against a live collector; getting this wrong silently scopes to nothing.
INSTANCE_FIELD = {"logs": "service_instance_id",
                  "traces": "service_service_instance_id",
                  "metrics": "service_instance_id"}


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
        # The Traces/Logs branch pins the scheme through its regex; this fallback is the only
        # path where an arbitrary settings value reaches urlopen, which also speaks file:// and
        # ftp://. Reject anything that is not HTTP here rather than at the request.
        if not host.lower().startswith(("http://", "https://")):
            sys.exit(
                f"telemetry.Host must be an http:// or https:// URL, not {host!r}. "
                "Fix the telemetry section of ihcsettings.json."
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
        # The only non-literal part of `url` is the base from load_telemetry, which rejects
        # every scheme but http/https, and the stdlib is a deliberate constraint here: this
        # skill must run unconfigured on Windows/Linux/mac.
        # nosemgrep: dynamic-urllib-use-detected
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


def resolve_metric_streams(base, org, auth, metric):
    """
    Map an instrument name to the stream(s) OpenObserve actually stores it under.

    Accepts the dotted name the instrument is declared with (`ihc.edit.apply.duration`)
    or the underscore name the collector shows.

    A histogram is stored ONLY as its `_bucket`/`_count`/`_sum` family. OpenObserve also
    registers an EMPTY stream under the histogram's bare name, which the streams API lists
    but the search API rejects with "Search stream not found" -- so when a family exists the
    bare name is deliberately excluded rather than queried and failed. A counter or gauge
    has no family and is returned under its own name.
    """
    wanted = metric.strip().replace(".", "_").lower()
    existing = set(list_streams(base, org, auth, "metrics"))
    family = [f"{wanted}_{s}" for s in HISTOGRAM_SUFFIXES if f"{wanted}_{s}" in existing]
    if family:
        return family
    return [wanted] if wanted in existing else []


def summarize_metric(h, now):
    ts = micros_to_iso(h.get("_timestamp"))
    name = h.get("__name__") or "?"
    value = h.get("value")
    # Dimensions are whatever the instrument was tagged with, flattened to underscores.
    skip = {"__name__", "__hash__", "_timestamp", "value", "le", "exemplars", "flag",
            "start_time", "aggregation_temporality", "is_monotonic"}
    dims = {k: v for k, v in h.items()
            if k not in skip and not k.startswith(("service_", "telemetry_sdk_", "instrumentation_library_"))}
    dim_text = " ".join(f"{k}={v}" for k, v in sorted(dims.items()))
    le = f" le={h['le']}" if h.get("le") is not None else ""
    temporality = (h.get("aggregation_temporality") or "").replace("AGGREGATION_TEMPORALITY_", "")
    exemplars = ""
    raw = h.get("exemplars")
    if raw and raw not in ("[]", "null"):
        try:
            exemplars = f" exemplars={len(json.loads(raw) if isinstance(raw, str) else raw)}"
        except (json.JSONDecodeError, TypeError):
            exemplars = " exemplars=?"
    return (f"[{ts} | {fmt_age(h.get('_timestamp'), now)}] {name}{le} = {value}"
            f"  {temporality:10} run={short_inst(h)} {dim_text}{exemplars}")


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


# Columns a tree needs. Explicit rather than SELECT *: `events` alone can carry a whole stack trace per
# span, and a trace view asks for every span at once.
TREE_COLUMNS = ("_timestamp, service_name, service_service_instance_id, trace_id, span_id, "
                "reference_parent_span_id, operation_name, span_status, duration, start_time, links, events")


def link_targets(h):
    """The (trace_id, span_id) pairs this span LINKS to. Empty for the overwhelming majority."""
    raw = h.get("links")
    if not raw or raw == "[]":
        return []
    try:
        links = json.loads(raw) if isinstance(raw, str) else raw
    except (ValueError, TypeError):
        return []
    out = []
    for link in links or []:
        ctx = (link or {}).get("context") or {}
        if ctx.get("traceId"):
            out.append((ctx["traceId"], ctx.get("spanId") or ""))
    return out


def build_tree(hits):
    """Group spans into parent -> children. Returns (children_by_parent, roots, dangling_ids).

    A span whose parent is not among the rows is treated as a root and REPORTED as dangling rather
    than silently re-rooted: the usual cause is --size truncation, and a tree that hides that reads
    as a complete picture of an incomplete one.
    """
    by_id = {h.get("span_id"): h for h in hits if h.get("span_id")}
    children, roots, dangling = {}, [], set()
    for h in hits:
        parent = h.get("reference_parent_span_id")
        if parent and parent in by_id:
            children.setdefault(parent, []).append(h)
        else:
            roots.append(h)
            if parent:
                dangling.add(h.get("span_id"))
    for kids in children.values():
        kids.sort(key=lambda s: s.get("start_time") or 0)
    roots.sort(key=lambda s: s.get("start_time") or 0)
    return children, roots, dangling


def duration_ms(h):
    try:
        return int(h["duration"]) / 1000.0
    except (KeyError, ValueError, TypeError):
        return None


def render_span(h, depth, dangling, known_traces):
    ms = duration_ms(h)
    dur = f"  {ms:.1f}ms" if ms is not None else ""
    status = h.get("span_status")
    flag = f"  [{status}]" if status not in (None, "", "UNSET") else ""
    notes = []
    for trace_id, _ in link_targets(h):
        seen = " (shown above)" if trace_id in known_traces else ""
        notes.append(f"-> links to trace {trace_id[:8]}{seen}")
    if h.get("span_id") in dangling:
        notes.append(f"!! parent {str(h.get('reference_parent_span_id'))[:8]} is not in these rows")
    note = ("  " + "  ".join(notes)) if notes else ""
    line = f"{'  ' * depth}{h.get('operation_name') or '?'}{dur}{flag}{note}"
    exc = extract_exception(h.get("events"))
    if exc:
        line += f"\n{'  ' * (depth + 1)}! {exc}"
    return line


def render_tree(hits, known_traces):
    """The indented tree, plus what the caller needs to summarise it.

    Returns (lines, anomalies, roots, dangling): the rendered lines, the "child outlasts parent" notes, the
    root spans and the ids whose parent was not among the rows.
    """
    children, roots, dangling = build_tree(hits)
    lines, anomalies, visited = [], [], set()

    def walk(span, depth):
        span_id = span.get("span_id")
        if span_id in visited:      # a malformed parent chain must not hang the renderer
            lines.append(f"{'  ' * depth}(cycle at {str(span_id)[:8]})")
            return
        visited.add(span_id)
        lines.append(render_span(span, depth, dangling, known_traces))
        parent_ms = duration_ms(span)
        for kid in children.get(span_id, []):
            kid_ms = duration_ms(kid)
            if parent_ms is not None and kid_ms is not None and kid_ms > parent_ms:
                anomalies.append(
                    f"{kid.get('operation_name')} ({kid_ms:.1f}ms) outlasts its parent "
                    f"{span.get('operation_name')} ({parent_ms:.1f}ms)")
            walk(kid, depth + 1)

    for root in roots:
        walk(root, 0)
    return lines, anomalies, roots, dangling


def id_predicate(field, value):
    """Match an id EXACTLY when it is full length, as a PREFIX when it is shorter.

    The trees print ids abbreviated to 8 characters, so an abbreviation is exactly what a reader will
    paste back; a tool that then matched nothing would be teaching its own output to be useless. The
    charset check is what keeps a value out of the SQL it is spliced into.
    """
    if not re.fullmatch(r"[0-9a-fA-F-]{4,64}", value):
        sys.exit(f"'{value}' is not an id: expected hex, optionally with dashes (4-64 chars).")
    return f"{field} = '{value}'" if len(value) >= 32 else f"{field} LIKE '{value}%'"


def trace_mode(args, base, org, auth, trace_stream, start, end, quiet):
    """--trace / --run: spans as a TREE rather than a flat rowset.

    This is step 3 of the diagnosis workflow ("follow the trace_id") done for you: assembling parents
    by hand from a flat table is what every trace deep-dive otherwise begins with, and the shape - what
    ran inside what, and how long the parent covered - is the whole question a nesting bug turns on.
    """
    if not trace_stream:
        sys.exit("No traces stream exists yet; nothing to draw.")

    scope, where = "", None
    if args.run:
        run = args.run
        if run == "latest":
            run = latest_instance(base, org, auth, trace_stream, "traces",
                                  INSTANCE_FIELD["traces"], start, end)
            if not run:
                # Explained HERE, and said so, because the caller's generic "no spans found" would follow it
                # with advice about an id the user never supplied.
                print(f"\nNo run found in the last {args.since}. Widen --since, or run the app first.")
                args.explained = True
                return 0
        where = id_predicate(INSTANCE_FIELD["traces"], run)
        scope = f"run {run[:8]}"
    else:
        where = id_predicate("trace_id", args.trace)
        scope = f"trace {args.trace}"

    sql = f'SELECT {TREE_COLUMNS} FROM "{trace_stream}" WHERE {where} ORDER BY start_time'
    hits = search(base, org, auth, "traces", sql, start, end, args.size, quiet)
    if args.json:
        for h in hits:
            print(json.dumps(h))
        return len(hits)
    if not hits:
        return 0

    by_trace = {}
    for h in hits:
        by_trace.setdefault(h.get("trace_id"), []).append(h)
    # Oldest trace first, so a run reads in the order it happened.
    ordered = sorted(by_trace.items(), key=lambda kv: min(s.get("start_time") or 0 for s in kv[1]))

    all_anomalies, total_roots, total_dangling, drawn = [], 0, 0, set()
    blocks = []
    for trace_id, spans in ordered:
        lines, anomalies, roots, dangling = render_tree(spans, drawn)
        drawn.add(trace_id)
        total_roots += len(roots)
        total_dangling += len(dangling)
        all_anomalies.extend(anomalies)
        # The FULL id in the header, because this is the line a reader copies to drill into one trace;
        # the abbreviations elsewhere are cross-references, not things to paste.
        header = f"--- trace {trace_id} ({len(spans)} spans)" if args.run else ""
        blocks.append(("\n".join([header] + lines) if header else "\n".join(lines)))

    # ASCII only: this prints to a Windows console whose default code page mangles anything else.
    counts = f"{len(hits)} spans, {total_roots} roots" if args.trace \
        else f"{len(hits)} spans, {len(by_trace)} traces, {total_roots} roots"
    print(f"\n=== {scope}: {counts} ===")
    print("\n\n".join(blocks))

    if args.trace and len(by_trace) > 1:
        print(f"\nNOTE: '{args.trace}' is a PREFIX that matched {len(by_trace)} traces, drawn above in order. "
              "Pass more characters to single one out.")
    if len(hits) >= args.size:
        print(f"\nNOTE: {len(hits)} rows returned with --size {args.size}, so this may be TRUNCATED - "
              f"raise --size. Truncation is also what usually explains a '!! parent not in these rows'.")
    if total_dangling:
        print(f"\n{total_dangling} span(s) reference a parent that is not in these rows (marked !!).")
    if all_anomalies:
        print("\nchildren outlasting their parent:")
        for line in all_anomalies:
            print(f"  - {line}")
        print("  (a parent that ended first did not await its child - deliberate for work posted to "
              "another thread, a defect where the parent could have awaited)")
    return len(hits)


def run_once(args, base, org, auth, log_stream, trace_stream, start, end, quiet):
    """One pass. `end` doubles as 'now' for age display. Returns total hit count."""
    now = end
    total = 0
    want_logs = args.type in ("logs", "both")
    want_traces = args.type in ("traces", "both")

    if args.trace or args.run:
        return trace_mode(args, base, org, auth, trace_stream, start, end, quiet)

    if args.sql:
        stream_type = args.type if args.type in ("logs", "traces", "metrics") else "logs"
        field = INSTANCE_FIELD.get(stream_type, "service_instance_id")
        if ":run" in args.sql:
            # The two traps this removes, both documented and both easy to hit: the instance field is
            # spelled differently per signal, and the id has to be looked up first. Expanded to the whole
            # predicate rather than to the bare id, so neither has to be got right by hand.
            #
            # A metrics query resolves the id from the LOGS stream: one launch has one
            # service.instance.id across all three signals, and there is no single metrics stream to ask
            # (one per instrument), so the query's own stream is not discoverable from here.
            src_type = "logs" if stream_type == "metrics" else stream_type
            src_stream = {"logs": log_stream, "traces": trace_stream}.get(src_type) or args.stream \
                or pick_stream(base, org, auth, src_type, None)
            iid = latest_instance(base, org, auth, src_stream, src_type,
                                  INSTANCE_FIELD[src_type], start, end) if src_stream else None
            if not iid:
                sys.exit(f"`:run` found no launch in the last {args.since}. Widen --since, or drop `:run`.")
            args.sql = args.sql.replace(":run", f"{field} = '{iid}'")
            print(f"scope: :run -> {field} = '{iid[:8]}...'", file=sys.stderr)
        elif args.latest_run:
            # Raw SQL is passed through verbatim, so the instance filter the other branches add cannot be
            # applied here without rewriting a query this script does not parse. Say so loudly: a silent
            # no-op reads as "these rows are one launch" and quietly merges runs, which is the single
            # easiest way to mistake a previous build's telemetry for the run just made.
            print(f"\nWARNING: --latest-run does NOT apply to --sql; the query ran unscoped over the whole "
                  f"window. Put `:run` in the WHERE clause and it expands to "
                  f"\"{INSTANCE_FIELD.get(stream_type, 'service_instance_id')} = '<newest id>'\" - or write "
                  f"that predicate yourself, or select the column and check it per row.",
                  file=sys.stderr)
        hits = search(base, org, auth, stream_type, args.sql, start, end, args.size, quiet)
        print(f"\n=== {stream_type} ({len(hits)}) ===")
        printer = {"traces": summarize_trace, "metrics": summarize_metric}.get(stream_type, summarize_log)
        for h in hits:
            print(json.dumps(h) if args.json else printer(h, now))
        return len(hits)

    if args.metric:
        streams = resolve_metric_streams(base, org, auth, args.metric)
        if not streams:
            print(f"\nNo metrics stream matches '{args.metric}'. "
                  "`--type metrics --list-streams` shows what exists; remember a histogram "
                  "is stored only as its _bucket/_count/_sum family.")
            return 0
        found = 0
        for stream in streams:
            where, scope = "1 = 1", ""
            try:
                if args.latest_run:
                    iid = latest_instance(base, org, auth, stream, "metrics", INSTANCE_FIELD["metrics"], start, end)
                    if iid:
                        where = f"{INSTANCE_FIELD['metrics']} = '{iid}'"
                        scope = f" [latest run {iid[:8]}]"
                # _bucket rows order by boundary so the cumulative shape reads top to bottom;
                # everything else is newest-first like the other signals.
                order = "CAST(le AS DOUBLE)" if stream.endswith("_bucket") else "_timestamp DESC"
                sql = f'SELECT * FROM "{stream}" WHERE {where} ORDER BY {order}'
                hits = search(base, org, auth, "metrics", sql, start, end, args.size, quiet)
            except SystemExit as exc:
                # One unqueryable member must not abort the others: a histogram family is
                # reported per stream, and a partial answer beats no answer at all.
                print(f"\n=== metric '{stream}' (query failed) ===\n{exc}")
                continue
            found += len(hits)
            print(f"\n=== metric '{stream}'{scope} ({len(hits)}) ===")
            for h in hits:
                print(json.dumps(h) if args.json else summarize_metric(h, now))
        return found

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
    p = argparse.ArgumentParser(
        description="Query OpenObserve for errors in IHC logs, traces and metrics, and draw a "
                    "trace (or a whole app launch) as a TREE.")
    p.add_argument("--config", help="Path to ihcsettings.json (default: search upward from cwd).")
    # --errors is a no-op alias: the canned error query is already the default action
    # (run whenever neither --list-streams nor --sql is given). Kept so the documented,
    # self-explaining `--errors` invocation works.
    p.add_argument("--errors", action="store_true", help="Canned error query over logs and/or traces (the default action; this flag is optional).")
    p.add_argument("--type", choices=["logs", "traces", "metrics", "both"], default="both",
                   help="Which streams to query. 'both' stays logs+traces, so existing invocations are unchanged; "
                        "metrics are opt-in because the canned error query has no meaning for them.")
    p.add_argument("--metric",
                   help="Canned metrics query: every point of this instrument, dotted or underscore name. "
                        "A histogram resolves to its whole _bucket/_count/_sum family. Pair with --latest-run.")
    p.add_argument("--since", default="15m", help="Look-back window: 30s, 15m, 2h, 1d. Default 15m.")
    p.add_argument("--size", type=int, default=100, help="Max rows to return. Default 100.")
    p.add_argument("--stream", help="Override the discovered stream name.")
    p.add_argument("--sql", help="Run custom SQL instead of the canned error query (use with --type logs|traces|metrics). "
                                 "`:run` anywhere in the SQL expands to the newest launch's instance predicate "
                                 "(the right field name per signal), so a custom query can be scoped to one run.")
    p.add_argument("--trace", metavar="ID",
                   help="Draw one trace as a TREE: parent/child nesting, each span's duration and status. "
                        "Always reads the traces stream, whatever --type says.")
    p.add_argument("--run", nargs="?", const="latest", metavar="ID",
                   help="Draw EVERY trace of one app launch as trees, newest launch when given no id "
                        "(--run). This is the view that answers 'is this workflow one tree'.")
    p.add_argument("--list-streams", action="store_true", help="List stream names and exit.")
    p.add_argument("--latest-run", action="store_true",
                   help="Scope to the newest app launch, so stale errors from earlier runs are excluded. Uses "
                        "service_instance_id for logs and metrics, service_service_instance_id for traces.")
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
        print("logs   :", ", ".join(list_streams(base, org, auth, "logs")) or "(none)")
        print("traces :", ", ".join(list_streams(base, org, auth, "traces")) or "(none)")
        # One stream per instrument, so this list is long and is the metric inventory.
        print("metrics:", ", ".join(sorted(list_streams(base, org, auth, "metrics"))) or "(none)")
        return

    if args.trace and args.run:
        sys.exit("--trace draws ONE trace and --run draws every trace of a launch; pass one or the other.")
    if args.trace or args.run:
        # A tree is a traces question whatever --type happens to say, and forcing it here means the log
        # stream is not picked (one fewer API call) and the traces stream always is.
        args.type = "traces"

    if args.type == "metrics" and not (args.sql or args.metric):
        sys.exit("--type metrics needs --metric <instrument> or --sql: there is no canned "
                 "error query for metrics. `--type metrics --list-streams` lists instruments.")

    log_stream = pick_stream(base, org, auth, "logs", args.stream) if args.type in ("logs", "both") else None
    trace_stream = pick_stream(base, org, auth, "traces", args.stream) if args.type in ("traces", "both") else None
    # Streams the canned query can actually hit. Empty means none of the requested types
    # exist yet — a different situation from "stream exists but held no matching rows".
    queried_streams = [s for s in (log_stream, trace_stream) if s]

    now = now_micros()
    start = now - since_to_micros(args.since)

    print(f"config: {config_path}  |  base: {base}  org: {org}", file=sys.stderr)
    print(f"now: {micros_to_iso(now)}  |  window: last {args.since}"
          + ("  |  scope: latest run only" if args.latest_run and not args.sql else ""), file=sys.stderr)
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
        if (args.trace or args.run) and not getattr(args, "explained", False):
            what = f"trace '{args.trace}'" if args.trace else (
                "the newest launch" if args.run == "latest" else f"run '{args.run}'")
            print(f"\nNo spans found for {what} in the last {args.since}. A short id matches by PREFIX, so "
                  "check the prefix is right rather than lengthening it; widen --since for an older run, and "
                  "remember OpenObserve indexes on a short delay.")
        elif not args.sql and not queried_streams:
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
