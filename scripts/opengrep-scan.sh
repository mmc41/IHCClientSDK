#!/usr/bin/env bash
#
# The Opengrep scan, defined once so that a local run and the CI run are the same scan.
# .github/workflows/opengrep.yml calls this script; so should you. Any argument given here is
# passed through to `opengrep scan`, which is how CI adds its SARIF output.
#
# Rules are pinned to a commit instead of being fetched with `--config auto`. Auto downloads a
# mutable ruleset from semgrep.dev at scan time -- a different set of rules on different days,
# and the request carries the project URL -- which is neither reproducible nor something a
# security gate should depend on.
#
# Findings do NOT fail this script; a scanner failure does. Add --error to fail on findings.
set -euo pipefail

RULES_REPO=https://github.com/opengrep/opengrep-rules.git
RULES_REF=f1d2b562b414783763fd02a6ed2736eaed622efa
RULES_DIR=${OPENGREP_RULES_DIR:-.opengrep-rules}

cd "$(dirname "$0")/.."

if ! command -v opengrep >/dev/null 2>&1; then
  echo "opengrep not found on PATH. See https://github.com/opengrep/opengrep for install options." >&2
  exit 127
fi

if [ ! -d "$RULES_DIR/.git" ]; then
  git clone --quiet --filter=blob:none --no-checkout "$RULES_REPO" "$RULES_DIR"
fi
git -C "$RULES_DIR" fetch --quiet --depth 1 origin "$RULES_REF"
git -C "$RULES_DIR" checkout --quiet --detach "$RULES_REF"

# .github/opengrep carries the rules upstream does not have. --exclude keeps the rules checkout
# from being scanned as though it were project source.
exec opengrep scan \
  --config "$RULES_DIR/csharp" \
  --config "$RULES_DIR/python" \
  --config "$RULES_DIR/yaml" \
  --config .github/opengrep \
  --exclude "$RULES_DIR" \
  "$@" \
  .
