#!/usr/bin/env bash
#
# The repository's static checks, defined once so that a local run and the build run the same
# checks. Directory.Build.targets calls this after compiling ihcclient; so can you, from any
# directory:
#
#   scripts/static_check.sh
#
# scripts/static_check.ps1 is the PowerShell peer, called on Windows. Any check added here must be
# added there too — the build picks one by platform, so a check that lives in only one of them is
# a check half the contributors never run.
#
# Exit code contract, which the build depends on: non-zero means a check could not RUN (its tool is
# missing, or it failed), never that a check found something. These checks advise; they do not gate.
# The build turns a non-zero exit into a warning and leaves the verdict to the compiler.
set -uo pipefail

cd "$(dirname "$0")/.."

status=0

# Copy/paste detection. Everything about the scan — which files, which excludes, which reporter, and
# the output directory below — is declared in .jscpd.json rather than on the command line, so that
# `jscpd .` by hand from the repository root is the same scan this runs.
#
# The ai reporter writes to stdout rather than to the output directory the config names, so the
# redirect is what puts the report on disk — and keeps 700-odd clone lines out of every build log.
#
# .txt, not .md: despite what the reporter is called, it emits plain text (a header, one line per
# clone pair, a rule, a summary) with no markdown in it, and read as markdown those lines would
# collapse into a single paragraph. jscpd's `markdown` reporter is the one that writes real markdown.
JSCPD_REPORT=artifacts/jscpd/jscpd-ai.txt
if command -v jscpd >/dev/null 2>&1; then
  mkdir -p "$(dirname "$JSCPD_REPORT")"
  if jscpd --config .jscpd.json --no-colors --no-tips . > "$JSCPD_REPORT"; then
    echo "jscpd copy/paste report: $JSCPD_REPORT"
  else
    # The redirect created the file before jscpd ran, so a failure leaves an empty or half-written
    # one behind. Removing it keeps the rule that a report on disk is a report some run produced.
    rm -f "$JSCPD_REPORT"
    echo "jscpd failed, so $JSCPD_REPORT was not written." >&2
    status=1
  fi
else
  echo "jscpd not found on PATH, so copy/paste detection was skipped. Install it with: npm install -g jscpd" >&2
  status=1
fi

exit $status
