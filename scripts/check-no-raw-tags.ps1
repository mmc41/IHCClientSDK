<#
    fablerefac W3-10 — the raw-schema-access gate for the OpenVisual GUI (PowerShell peer of
    check-no-raw-tags.sh; same rules).

    The GUI must read element classification and attribute values through the SDK read surface
    (ihcclient: ProjectElementRead extension members — element.Kind / element.IsCommand / … —
    and project.View(element).Effective/Name/Note), NOT by hand-matching raw element tags or
    calling GetAttribute. This script fails if the GUI regresses to raw access.

    Flags, on ELEMENT access: GetAttribute("…"), x.Tag == "…", x.Tag is "…", { Tag: "…" }
    property patterns, and switch (x.Tag). Excludes: comment lines, lines marked
    `// raw-schema-ok:`, the ProductMenuItemViewModel menu-Tag binding, and tag-string
    classification helpers (which take a `string tag`, so `.Tag` never matches).

    Usage: pwsh scripts/check-no-raw-tags.ps1 [-Root <path>]
#>
param([string]$Root = "applications/ihc_openvisual")

$pattern = 'GetAttribute\(|\.Tag\s*==\s*"|\.Tag\s+is\s+"|\{\s*Tag:\s*"|switch\s*\([^)]*\.Tag'

$hits = Get-ChildItem -Path $Root -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $file = $_.FullName
        $n = 0
        foreach ($line in Get-Content -LiteralPath $file) {
            $n++
            if ($line -match $pattern -and
                $line -notmatch '^\s*(//|///|\*)' -and
                $line -notmatch 'raw-schema-ok:' -and
                $line -notmatch 'ProductMenuItemViewModel') {
                "{0}:{1}:{2}" -f $file, $n, $line.Trim()
            }
        }
    }

if ($hits) {
    Write-Output "FAIL: raw schema access in the GUI (use ProjectElementRead predicates / project.View):"
    $hits | ForEach-Object { Write-Output $_ }
    exit 1
}
Write-Output "OK: no raw GetAttribute / element .Tag literals in $Root"
exit 0
