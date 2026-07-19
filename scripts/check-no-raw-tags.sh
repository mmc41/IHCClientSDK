#!/usr/bin/env bash
# fablerefac W3-10 — the raw-schema-access gate for the OpenVisual GUI.
#
# The GUI must read element classification and attribute values through the SDK read surface
# (ihcclient: ProjectElementRead extension members — element.Kind / element.IsCommand / … —
# and project.View(element).Effective/Name/Note), NOT by hand-matching raw element tags or
# calling GetAttribute. This script fails the build if the GUI regresses to raw access.
#
# It flags, on ELEMENT access, every schema-tag/attribute literal form:
#   - element.GetAttribute("…")          (raw attribute read; use project.View(e).Effective/Name/…)
#   - x.Tag == "…" / x.Tag is "…"         (inline tag compare; use an element.IsX predicate / element.Kind)
#   - { Tag: "…" } property pattern        (same, in a pattern)
#   - switch (x.Tag) { case "…": }         (same, in a switch)
#
# Deliberately NOT flagged (documented):
#   - comment lines (the pattern only appears in prose);
#   - a line carrying a `// raw-schema-ok: <reason>` marker (an explicitly-justified exception);
#   - the ProductMenuItemViewModel menu `Tag` binding (a GUI menu payload, not a schema tag);
#   - tag-string CLASSIFICATION helpers that take a `string tag` parameter (IsDeletableNode /
#     CanContain) or localized static tag sets — the tag knowledge is legitimately localized
#     there, mirroring the SDK's ProductClassifier(tag); the GUI passes element.Tag in, it does
#     not inline-match it. (These use a `tag`/`sourceTag` identifier, so `\.Tag` never matches.)
#
# Usage: scripts/check-no-raw-tags.sh [root]   (root defaults to applications/ihc_openvisual)
set -uo pipefail

root="${1:-applications/ihc_openvisual}"
pattern='GetAttribute\(|\.Tag[[:space:]]*==[[:space:]]*"|\.Tag[[:space:]]+is[[:space:]]+"|\{[[:space:]]*Tag:[[:space:]]*"|switch[[:space:]]*\([^)]*\.Tag'

hits=$(grep -rnE "$pattern" "$root" --include='*.cs' 2>/dev/null \
  | grep -v '/bin/\|/obj/' \
  | grep -vE ':[0-9]+:[[:space:]]*(//|///|\*)' \
  | grep -v 'raw-schema-ok:' \
  | grep -v 'ProductMenuItemViewModel' \
  || true)

if [ -n "$hits" ]; then
  echo "FAIL: raw schema access in the GUI (use ProjectElementRead predicates / project.View):"
  echo "$hits"
  exit 1
fi
echo "OK: no raw GetAttribute / element .Tag literals in $root"
exit 0
