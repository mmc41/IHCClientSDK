<#
.SYNOPSIS
  Value-level tests for the aui driver's coordinate-space contract (declared points and rectangles).

.DESCRIPTION
  Plain self-testing script: prints one line per case and exits 0 only when every case passed.
  Deliberately NOT Pester -- only the ancient Pester 3.4.0 is present on this machine, and a module
  dependency would break the skill's "nothing to install" property.

  The functions under test live in aui.ps1, whose bottom half EXECUTES a command, so this script
  cannot dot-source it. It lifts the named function definitions out of the parsed syntax tree
  instead, which tests the real shipping code rather than a copy of it.

  These cases stand in for the type-level guarantee the C# side of this contract gets from
  LogicalPoint / PhysicalPoint record structs: PowerShell has no compile step, so the same
  "unrepresentable" property is bought here by asserting the values.

.USAGE
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File aui-coordinate-space.tests.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$auiPath = Join-Path $PSScriptRoot 'aui.ps1'
if (-not (Test-Path $auiPath)) { Write-Host "FAIL: aui.ps1 not found next to this script."; exit 2 }

# ---------------------------------------------------------------------------
# Load the units under test out of aui.ps1 (functions + the single space constant)
# ---------------------------------------------------------------------------
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($auiPath, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count -gt 0) {
    Write-Host "FAIL: aui.ps1 does not parse ($($parseErrors.Count) error(s)); first: $($parseErrors[0].Message)"
    exit 2
}

$wantedFunctions = @(
    'New-MonitorGeometry'
    'Get-RoundedOffset'
    'ConvertTo-LogicalPoint'
    'ConvertTo-PhysicalPoint'
    'New-DeclaredPoint'
    'New-DeclaredRect'
    'New-ScreenshotMetadata'
    'New-DisplayBlock'
)
$found = @{}
foreach ($fn in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    if ($wantedFunctions -contains $fn.Name) {
        . ([scriptblock]::Create($fn.Extent.Text))
        $found[$fn.Name] = $true
    }
}
$missing = @($wantedFunctions | Where-Object { -not $found.ContainsKey($_) })
if ($missing.Count -gt 0) {
    Write-Host "FAIL: aui.ps1 defines no $($missing -join ', ') -- the coordinate contract is not implemented."
    exit 1
}

# The space name is spelled in exactly ONE place in the driver (a typo'd tag is unfalsifiable in
# JSON), so the tests read that one place rather than restating it.
$spaceAssign = @($ast.FindAll({
    $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] -and
    $args[0].Left.Extent.Text -eq '$script:NativeCoordSpace' }, $true))
if ($spaceAssign.Count -ne 1) {
    Write-Host "FAIL: expected exactly one assignment of `$script:NativeCoordSpace in aui.ps1, found $($spaceAssign.Count)."
    exit 1
}
. ([scriptblock]::Create($spaceAssign[0].Extent.Text))

# ---------------------------------------------------------------------------
# Harness
# ---------------------------------------------------------------------------
$script:Failed = 0
$script:Ran = 0
function Test-Case {
    param([string] $Name, $Expected, $Actual)
    $script:Ran++
    $e = if ($null -eq $Expected) { '<null>' } else { [string]$Expected }
    $a = if ($null -eq $Actual) { '<null>' } else { [string]$Actual }
    if ($e -ceq $a) { Write-Host "PASS  $Name" }
    else { $script:Failed++; Write-Host "FAIL  $Name`n        expected: $e`n        actual  : $a" }
}
function Format-Point { param($P) if ($null -eq $P) { '<null>' } else { "$($P.x),$($P.y)" } }

# The three origins the contract must behave identically at: the trivial one, a positive secondary
# monitor, and a negative one (a monitor placed left of / above the primary).
$origins = @(
    @{ name = '(0,0)';          x = 0;     y = 0 }
    @{ name = '(2560,0)';       x = 2560;  y = 0 }
    @{ name = '(-1920,-120)';   x = -1920; y = -120 }
)

# ---------------------------------------------------------------------------
# (a) D05's formula in BOTH directions, at scale 1.0 and 1.75, at all three origins
# ---------------------------------------------------------------------------
foreach ($o in $origins) {
    $g1 = New-MonitorGeometry -LogicalX $o.x -LogicalY $o.y -PhysicalX $o.x -PhysicalY $o.y -Scale 1.0
    # At scale 1.0 the two spaces coincide: this is the case that would still pass on a 100% display,
    # and it is exactly why an author on such a display never discovers this bug class.
    $p = ConvertTo-LogicalPoint -X ($o.x + 400) -Y ($o.y + 260) -Geometry $g1
    Test-Case "a1 scale 1.0 origin $($o.name): physical->logical is identity" "$($o.x + 400),$($o.y + 260)" (Format-Point $p)
    $p = ConvertTo-PhysicalPoint -X ($o.x + 400) -Y ($o.y + 260) -Geometry $g1
    Test-Case "a2 scale 1.0 origin $($o.name): logical->physical is identity" "$($o.x + 400),$($o.y + 260)" (Format-Point $p)

    $g = New-MonitorGeometry -LogicalX $o.x -LogicalY $o.y -PhysicalX $o.x -PhysicalY $o.y -Scale 1.75
    # 336/1.75 = 192 exactly; 499/1.75 = 285.14... -> 285
    $p = ConvertTo-LogicalPoint -X ($o.x + 336) -Y ($o.y + 499) -Geometry $g
    Test-Case "a3 scale 1.75 origin $($o.name): physical->logical" "$($o.x + 192),$($o.y + 285)" (Format-Point $p)
    # 192*1.75 = 336 exactly; 285*1.75 = 498.75 -> 499
    $p = ConvertTo-PhysicalPoint -X ($o.x + 192) -Y ($o.y + 285) -Geometry $g
    Test-Case "a4 scale 1.75 origin $($o.name): logical->physical" "$($o.x + 336),$($o.y + 499)" (Format-Point $p)
}

# ---------------------------------------------------------------------------
# (b) The SAME offset from all three origins converts IDENTICALLY.
#     This is what pins rounding-on-the-OFFSET: rounding the absolute coordinate instead would make
#     the (-1920,-120) case disagree (e.g. Round(-1917/1.75) = -1095, not origin + 2).
# ---------------------------------------------------------------------------
$offsets = @()
foreach ($o in $origins) {
    $g = New-MonitorGeometry -LogicalX $o.x -LogicalY $o.y -PhysicalX $o.x -PhysicalY $o.y -Scale 1.75
    $p = ConvertTo-LogicalPoint -X ($o.x + 3) -Y ($o.y + 3) -Geometry $g
    $offsets += "$([int]$p.x - $o.x),$([int]$p.y - $o.y)"
}
Test-Case 'b1 offset +3 converts identically from all three origins' '2,2 | 2,2 | 2,2' ($offsets -join ' | ')

$offsets = @()
foreach ($o in $origins) {
    $g = New-MonitorGeometry -LogicalX $o.x -LogicalY $o.y -PhysicalX $o.x -PhysicalY $o.y -Scale 1.75
    $p = ConvertTo-LogicalPoint -X ($o.x - 3) -Y ($o.y - 3) -Geometry $g
    $offsets += "$([int]$p.x - $o.x),$([int]$p.y - $o.y)"
}
Test-Case 'b2 offset -3 converts identically from all three origins' '-2,-2 | -2,-2 | -2,-2' ($offsets -join ' | ')

# Rounding is half AWAY FROM ZERO, not to-even: 6*1.75 = 10.5 -> 11 (to-even would give 10),
# and the negative side must be symmetric: -6*1.75 = -10.5 -> -11.
$gz = New-MonitorGeometry -LogicalX 0 -LogicalY 0 -PhysicalX 0 -PhysicalY 0 -Scale 1.75
Test-Case 'b3 midpoint +10.5 rounds away from zero (not to-even)' '11,11' (Format-Point (ConvertTo-PhysicalPoint -X 6 -Y 6 -Geometry $gz))
Test-Case 'b4 midpoint -10.5 rounds away from zero (symmetric)'   '-11,-11' (Format-Point (ConvertTo-PhysicalPoint -X -6 -Y -6 -Geometry $gz))
Test-Case 'b5 Get-RoundedOffset is half-away-from-zero at +2.5'   '3'  ([string](Get-RoundedOffset 2.5))
Test-Case 'b6 Get-RoundedOffset is half-away-from-zero at -2.5'   '-3' ([string](Get-RoundedOffset -2.5))

# NOTE deliberately absent: no case asserts that physical -> logical -> physical returns the original
# value. The conversion is lossy at non-integer scales (D05), and a driver that made it exact would
# have to lie about one of the two numbers.

# ---------------------------------------------------------------------------
# (c) D07: when the geometry cannot be probed, the sibling is OMITTED, never faked -- but `space`
#     is still declared, because "I do not know the scale" and "the scale is 1.0" are different facts.
# ---------------------------------------------------------------------------
$noGeo = New-DeclaredPoint -X 711 -Y 512 -Geometry $null
Test-Case 'c1 no geometry: still declares the space' $script:NativeCoordSpace ([string]$noGeo.space)
Test-Case 'c2 no geometry: emits NO logical sibling' 'False' ([string]$noGeo.Contains('logical'))
Test-Case 'c3 no geometry: serialized form' '{"x":711,"y":512,"space":"physical"}' (ConvertTo-Json $noGeo -Compress -Depth 4)

# ---------------------------------------------------------------------------
# (d) The emitted schema, key for key, in order -- so a harness can parse both drivers with one reader
# ---------------------------------------------------------------------------
$g175 = New-MonitorGeometry -LogicalX 0 -LogicalY 0 -PhysicalX 0 -PhysicalY 0 -Scale 1.75
$declared = New-DeclaredPoint -X 711 -Y 512 -Geometry $g175
Test-Case 'd1 declared point: key order' 'x,y,space,logical' (@($declared.Keys) -join ',')
Test-Case 'd2 declared point: exact serialization' '{"x":711,"y":512,"space":"physical","logical":{"x":406,"y":293}}' (ConvertTo-Json $declared -Compress -Depth 4)
Test-Case 'd3 declared point: native x/y preserved verbatim' '711,512' (Format-Point $declared)
# The sibling is a PLAIN point: stating the space twice would give it two places to disagree.
Test-Case 'd4 sibling carries no nested space tag' 'x,y' (@($declared.logical.Keys) -join ',')

# The space TAG must be spelled in exactly one place in the whole driver -- a tag mistyped at an
# emission site is unfalsifiable in JSON. Hashtable KEYS named `physical` are excluded: doctor's
# display block is required to carry `logical`/`physical` keys (vendor parity), and a key is never
# emitted as a space tag. So this counts only literals in VALUE position, which is what D06 protects.
$hashtableKeys = New-Object System.Collections.ArrayList
foreach ($ht in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.HashtableAst] }, $true)) {
    foreach ($pair in $ht.KeyValuePairs) { [void]$hashtableKeys.Add($pair.Item1) }
}
$literals = @($ast.FindAll({
    $args[0] -is [System.Management.Automation.Language.StringConstantExpressionAst] -and
    $args[0].Value -eq $script:NativeCoordSpace }, $true) | Where-Object {
        $node = $_
        -not (@($hashtableKeys | Where-Object { [object]::ReferenceEquals($_, $node) }).Count) })
Test-Case "d5 '$($script:NativeCoordSpace)' is spelled exactly once in VALUE position in aui.ps1" '1' ([string]$literals.Count)

# ---------------------------------------------------------------------------
# (e) The remaining emission sites: node.doubleClick's point and the dialog.read rectangle
# ---------------------------------------------------------------------------
# node.doubleClick emits the same declared point as node.rightClick. Uses the point measured live on
# this machine (336,499 physical -> 192,285 logical) rather than repeating the rightClick case.
$dbl = New-DeclaredPoint -X 336 -Y 499 -Geometry $g175
Test-Case 'e1 doubleClick point: native x/y preserved verbatim' '336,499' (Format-Point $dbl)
Test-Case 'e2 doubleClick point: exact serialization' '{"x":336,"y":499,"space":"physical","logical":{"x":192,"y":285}}' (ConvertTo-Json $dbl -Compress -Depth 4)

# The rectangle, using a control rect read live from the Project information dialog
# (OkButton: 1200,1354 147x54). Corners: 1200/1.75 = 685.71 -> 686, 1354/1.75 = 773.71 -> 774,
# right 1347/1.75 = 769.71 -> 770, bottom 1408/1.75 = 804.57 -> 805.
$rect = New-DeclaredRect -X 1200 -Y 1354 -Width 147 -Height 54 -Geometry $g175
Test-Case 'e3 rect: native x/y/width/height preserved verbatim' '1200,1354 147x54' "$($rect.x),$($rect.y) $($rect.width)x$($rect.height)"
Test-Case 'e4 rect: exact serialization' '{"x":1200,"y":1354,"width":147,"height":54,"space":"physical","logical":{"x":686,"y":774,"width":84,"height":31}}' (ConvertTo-Json $rect -Compress -Depth 4)
Test-Case 'e5 rect: key order' 'x,y,width,height,space,logical' (@($rect.Keys) -join ',')
Test-Case 'e6 rect: sibling key order, no nested space tag' 'x,y,width,height' (@($rect.logical.Keys) -join ',')

# THE both-corners rule, with the case that separates it from the wrong implementation.
# 101,101 3x3 at scale 1.75: corners give 101/1.75 = 57.71 -> 58 and 104/1.75 = 59.43 -> 59, so the
# logical extent is 59-58 = 1. Scaling the extent IN ISOLATION gives Round(3/1.75) = Round(1.714) = 2.
# A driver that double-rounds this way passes every rect whose corners happen to align and drifts on
# the ones that do not, which is the worst possible failure distribution.
$tiny = New-DeclaredRect -X 101 -Y 101 -Width 3 -Height 3 -Geometry $g175
Test-Case 'e7 rect sibling is derived from BOTH CORNERS (isolated scaling would give 2x2)' '58,58 1x1' "$($tiny.logical.x),$($tiny.logical.y) $($tiny.logical.width)x$($tiny.logical.height)"

# D07 again, for the rectangle: no geometry -> space, no sibling. A null rect stays null at the
# call site (ConvertTo-RectDump returns before reaching here), which is why there is no "declared
# empty rect" shape to test.
$rectNoGeo = New-DeclaredRect -X 1200 -Y 1354 -Width 147 -Height 54 -Geometry $null
Test-Case 'e8 rect, no geometry: serialized form omits the sibling' '{"x":1200,"y":1354,"width":147,"height":54,"space":"physical"}' (ConvertTo-Json $rectNoGeo -Compress -Depth 4)

# The old shapes must be GONE, not merely joined by the new ones -- D02 is a deliberate breaking
# change, and a driver emitting both spellings would let a stale reader keep working by accident.
$rawSource = Get-Content $auiPath -Raw
Test-Case 'e9 no bare "x,y" point string remains in aui.ps1' 'False' ([string]$rawSource.Contains('[int]$pt.X),$('))
# Text search cannot ask this one ("$window = " contains "w = "), so ask the syntax tree: no hashtable
# key may be named w or h.
$shortKeys = @()
foreach ($ht in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.HashtableAst] }, $true)) {
    foreach ($pair in $ht.KeyValuePairs) {
        $k = $pair.Item1.Extent.Text.Trim("'", '"')
        if ($k -eq 'w' -or $k -eq 'h') { $shortKeys += "$k at line $($pair.Item1.Extent.StartLineNumber)" }
    }
}
Test-Case 'e10 no w/h rect keys remain in aui.ps1' '' ($shortKeys -join ', ')

# ---------------------------------------------------------------------------
# (f) Screenshot metadata: an image and a point in the SAME envelope must not be silently in
#     different spaces. The pixel dimensions of a PNG grabbed with CopyFromScreen are physical, and
#     until now nothing in the envelope said so while the points beside it said nothing either.
# ---------------------------------------------------------------------------
$shot = New-ScreenshotMetadata -Path 'C:\tmp\aui-window-1.png' -Width 1750 -Height 1190 -Scope 'window'
Test-Case 'f1 screenshot metadata: key order' 'path,width,height,space,scope,mimeType' (@($shot.Keys) -join ',')
Test-Case 'f2 screenshot metadata: declares the measured native space' $script:NativeCoordSpace ([string]$shot.space)
Test-Case 'f3 screenshot metadata: exact serialization' '{"path":"C:\\tmp\\aui-window-1.png","width":1750,"height":1190,"space":"physical","scope":"window","mimeType":"image/png"}' (ConvertTo-Json $shot -Compress -Depth 4)
# NO logical sibling here, deliberately: width/height are the PNG's real pixel count, and a "logical"
# image size would name a file that does not exist at that size. `space` alone answers the only
# question a caller has -- which space the pixels in this file are counted in.
Test-Case 'f4 screenshot metadata: no logical sibling (the file has one real pixel count)' 'False' ([string]$shot.Contains('logical'))

# Both capture sites must route through the one helper. If a site kept building the shape inline it
# would keep emitting an undeclared image, and nothing else in these tests would notice.
$mimeLiterals = @($ast.FindAll({
    $args[0] -is [System.Management.Automation.Language.StringConstantExpressionAst] -and
    $args[0].Value -eq 'image/png' }, $true))
Test-Case 'f5 image/png is spelled once, so every capture site goes through the helper' '1' ([string]$mimeLiterals.Count)
$shotCalls = @($ast.FindAll({
    $args[0] -is [System.Management.Automation.Language.CommandAst] -and
    $args[0].GetCommandName() -eq 'New-ScreenshotMetadata' }, $true))
Test-Case 'f6 both emission sites call the helper' '2' ([string]$shotCalls.Count)

# ---------------------------------------------------------------------------
# (g) doctor's display block -- what makes a coordinate answer CHECKABLE. Printing scale 1.0 on a
#     100% machine and 1.75 here is also what makes this whole bug class discoverable at all.
# ---------------------------------------------------------------------------
# A synthetic SECOND monitor at a non-zero, partly negative origin, with DIFFERENT origins in the two
# spaces -- the case that separates a real implementation from one that assumes the primary display at
# (0,0). Windows virtualizes per monitor, so a secondary display's virtualized origin is not its
# physical origin scaled, which is exactly why both rectangles are read rather than computed.
$displayBlock = New-DisplayBlock -Monitor '\\.\DISPLAY2' -Dpi 168 `
    -LogicalRect  ([ordered]@{ x = 2560; y = -120; width = 2194; height = 1234 }) `
    -PhysicalRect ([ordered]@{ x = 4480; y = -210; width = 3840; height = 2160 })
Test-Case 'g1 display block: key order matches the vendor doctor' 'monitor,dpi,logical,physical,scale' (@($displayBlock.Keys) -join ',')
Test-Case 'g2 display block: monitor identity is carried verbatim' '\\.\DISPLAY2' ([string]$displayBlock.monitor)
Test-Case 'g3 display block: scale is dpi/96 as a double' '1.75' ([string]$displayBlock.scale)
Test-Case 'g4 display block: exact serialization' '{"monitor":"\\\\.\\DISPLAY2","dpi":168,"logical":{"x":2560,"y":-120,"width":2194,"height":1234},"physical":{"x":4480,"y":-210,"width":3840,"height":2160},"scale":1.75}' (ConvertTo-Json $displayBlock -Compress -Depth 5)

# THE SELF-CONSISTENCY CASE: the geometry doctor PUBLISHES must be the geometry the conversion USES.
# Without it the driver could describe one monitor while every emitted coordinate came from another,
# and both halves would look right in isolation. Carrying the published PHYSICAL rect through the
# conversion built from the same monitor must land exactly on the published LOGICAL rect.
$geoFromBlock = New-MonitorGeometry -LogicalX $displayBlock.logical.x -LogicalY $displayBlock.logical.y `
    -PhysicalX $displayBlock.physical.x -PhysicalY $displayBlock.physical.y -Scale $displayBlock.scale
$roundTrip = (New-DeclaredRect -X $displayBlock.physical.x -Y $displayBlock.physical.y `
    -Width $displayBlock.physical.width -Height $displayBlock.physical.height -Geometry $geoFromBlock).logical
Test-Case 'g5 the geometry doctor publishes is the geometry the conversion uses' `
    "$($displayBlock.logical.x),$($displayBlock.logical.y) $($displayBlock.logical.width)x$($displayBlock.logical.height)" `
    "$($roundTrip.x),$($roundTrip.y) $($roundTrip.width)x$($roundTrip.height)"

# A 100% monitor must report scale 1.0 rather than omitting the block: "no scaling in play" is a fact
# a caller needs, and it is the reading that tells an author on a 100% display why their machine never
# reproduces this bug class.
$plain = New-DisplayBlock -Monitor '\\.\DISPLAY1' -Dpi 96 `
    -LogicalRect  ([ordered]@{ x = 0; y = 0; width = 1920; height = 1080 }) `
    -PhysicalRect ([ordered]@{ x = 0; y = 0; width = 1920; height = 1080 })
Test-Case 'g6 unscaled monitor reports scale 1 with both rects equal' '1|0,0 1920x1080|0,0 1920x1080' `
    "$($plain.scale)|$($plain.logical.x),$($plain.logical.y) $($plain.logical.width)x$($plain.logical.height)|$($plain.physical.x),$($plain.physical.y) $($plain.physical.width)x$($plain.physical.height)"

# ---------------------------------------------------------------------------
# (h) The vocabulary itself: a point EXPORTED is not a point OFFERED. Every DPI failure in the
#     campaign that produced this contract came from external code consuming a driver-reported point,
#     and all of these commands are path-addressed, so no caller needs a screen coordinate at all.
# ---------------------------------------------------------------------------
$registryPath = Join-Path $PSScriptRoot 'commands.json'
if (-not (Test-Path $registryPath)) { Write-Host 'FAIL: commands.json not found next to this script.'; exit 2 }
$registry = Get-Content $registryPath -Raw | ConvertFrom-Json
$noteMarker = 'COORDINATE NOTE:'
function Get-CommandNote {
    param([string] $Id)
    $cmd = @($registry.commands | Where-Object { $_.id -eq $Id })
    if ($cmd.Count -ne 1) { return "<no such command: $Id>" }
    $at = $cmd[0].description.IndexOf($noteMarker)
    if ($at -lt 0) { return '' }
    return $cmd[0].description.Substring($at)
}

$noteDouble = Get-CommandNote 'node.doubleClick'
$noteRight  = Get-CommandNote 'node.rightClick'
Test-Case 'h1 node.doubleClick carries the coordinate note' 'True' ([string]($noteDouble.Length -gt 0))
Test-Case 'h2 node.rightClick carries the coordinate note'  'True' ([string]($noteRight.Length -gt 0))
# One note, not two paraphrases: two spellings of one rule drift, and a caller comparing them cannot
# tell which is current.
Test-Case 'h3 both notes are byte-identical' 'True' ([string]($noteDouble -ceq $noteRight))

# BOTH halves must be present. "Do not use this" alone leaves the caller with a problem and no
# answer, which is how a warning gets read as pedantry and skipped.
Test-Case 'h4 the note says the point is a DIAGNOSTIC' 'True' ([string]($noteDouble -cmatch 'diagnostic'))
Test-Case 'h5 the note says what to use INSTEAD (the path-addressed form)' 'True' ([string]($noteDouble -cmatch '--path'))

# Driven from D11's inventory rather than from a list retyped here: exactly the two point-exporting
# commands carry it. A warning on everything is one callers learn to skip, so the commands that
# export no point -- node.drag (label paths only) and tree.select -- must NOT carry it.
$carriers = @($registry.commands | Where-Object { $_.description -and $_.description.Contains($noteMarker) } |
    ForEach-Object { $_.id } | Sort-Object)
Test-Case 'h6 exactly the point-exporting commands carry the note' 'node.doubleClick,node.rightClick' ($carriers -join ',')
Test-Case 'h7 node.drag does not carry it (it exports no point)' '' (Get-CommandNote 'node.drag')
Test-Case 'h8 tree.select does not carry it (it exports no point)' '' (Get-CommandNote 'tree.select')

# ---------------------------------------------------------------------------
Write-Host ''
if ($script:Failed -gt 0) {
    Write-Host "$($script:Ran) case(s), $($script:Failed) FAILED."
    exit 1
}
Write-Host "$($script:Ran) case(s), all passed."
exit 0
