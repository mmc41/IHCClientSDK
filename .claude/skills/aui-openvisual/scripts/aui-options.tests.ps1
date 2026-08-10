<#
.SYNOPSIS
  Value-level tests for the aui driver's CLI grammar: option parsing, the positional fallback, and
  tree-selector resolution.

.DESCRIPTION
  Plain self-testing script: prints one line per case and exits 0 only when every case passed.
  Deliberately NOT Pester, for the same reason as aui-coordinate-space.tests.ps1 -- a module
  dependency would break the skill's "nothing to install" property.

  Like that script, this one lifts the named function definitions out of aui.ps1's parsed syntax tree
  rather than dot-sourcing it (the file's bottom half EXECUTES a command), so the real shipping code
  is under test.

  WHY THIS FILE EXISTS. The positional fallback used to be unconditional and index-0 for every option
  lookup, so any option read on a command that also takes a positional path silently received THAT
  PATH. It cost `node double-click <path>` -- the form the skill documents -- an unhandled [int] cast
  that surfaced as Code=MutationFailed, i.e. a plain usage form reported as a runtime interaction
  failure; it gave node.drag the same node for --from and --to; and it stamped tree.dump's
  capturedAfter with the subtree being dumped. None of it was visible from the JSON envelope, and
  none of it needs a running app to test.

.USAGE
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File aui-options.tests.ps1
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$auiPath = Join-Path $PSScriptRoot 'aui.ps1'
if (-not (Test-Path $auiPath)) { Write-Host 'FAIL: aui.ps1 not found next to this script.'; exit 2 }

$source = Get-Content -LiteralPath $auiPath -Raw
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($auiPath, [ref]$null, [ref]$parseErrors)
if ($parseErrors -and $parseErrors.Count -gt 0) {
    Write-Host "FAIL: aui.ps1 does not parse ($($parseErrors.Count) error(s)); first: $($parseErrors[0].Message)"
    exit 2
}

$wantedFunctions = @('Parse-Options', 'Get-OptValue', 'Get-OptInt', 'Get-PathOpt', 'Resolve-TreeId',
                     'Split-TreePath', 'Resolve-ChildIndex', 'Test-DestructiveGesture', 'Test-TitleNamesFile')
$found = @{}
foreach ($fn in $ast.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true)) {
    if ($wantedFunctions -contains $fn.Name) {
        . ([scriptblock]::Create($fn.Extent.Text))
        $found[$fn.Name] = $true
    }
}
$missing = @($wantedFunctions | Where-Object { -not $found.ContainsKey($_) })
if ($missing.Count -gt 0) {
    Write-Host "FAIL: aui.ps1 defines no $($missing -join ', ')."
    exit 1
}

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

# ---------------------------------------------------------------------------
# (a) Tokenizing: --flag value, bare --switch, positionals
# ---------------------------------------------------------------------------
$o = Parse-Options @('Localities/Kitchen', '--tree', 'TV2', '--expand-all')
Test-Case 'a1 --flag takes the following value'        'TV2'                (Get-OptValue $o @('tree'))
Test-Case 'a2 a trailing --switch is $true'            'True'               ([string]$o['expand-all'])
Test-Case 'a3 the bare word is a positional'           'Localities/Kitchen' ($o['_positional'][0])
Test-Case 'a4 flags are not positionals'               '1'                  ([string]@($o['_positional']).Count)

# ---------------------------------------------------------------------------
# (b) THE REGRESSION: a positional path must not be readable as a numeric option
# ---------------------------------------------------------------------------
$o = Parse-Options @('Localities/Kitchen')
Test-Case 'b1 double-click: the path resolves as the path'   'Localities/Kitchen' (Get-PathOpt $o)
Test-Case 'b2 double-click: --x-offset stays at its default' '0'    ([string](Get-OptInt $o @('x-offset') 0).value)
Test-Case 'b3 double-click: and it is not a parse failure'   'True' ([string](Get-OptInt $o @('x-offset') 0).ok)
$o = Parse-Options @('Localities')
Test-Case 'b4 tree dump: --depth keeps its default'          '40'   ([string](Get-OptInt $o @('depth') 40).value)
Test-Case 'b5 tree dump: --after is not stamped with the path' '<null>' (Get-OptValue $o @('after') -NamedOnly)
$o = Parse-Options @('{F2}')
Test-Case 'b6 key send: the gesture resolves as the gesture' '{F2}'   (Get-OptValue $o @('gesture', 'key'))
Test-Case 'b7 key send: --path is not the gesture'           '<null>' (Get-OptValue $o @('path') -NamedOnly)

# ---------------------------------------------------------------------------
# (c) A SECOND positional is addressed by index, not shared with the first
# ---------------------------------------------------------------------------
$o = Parse-Options @('Stue/Lampe', 'Kokken')
Test-Case 'c1 node drag: --from is the first positional' 'Stue/Lampe' (Get-OptValue $o @('from', 'path') -PositionalIndex 0)
Test-Case 'c2 node drag: --to is the SECOND (not a self-drag)' 'Kokken' (Get-OptValue $o @('to', 'target') -PositionalIndex 1)
$o = Parse-Options @('NameBox', 'New name')
Test-Case 'c3 dialog set-text: --field'  'NameBox'  (Get-OptValue $o @('field', 'control', 'id') -PositionalIndex 0)
Test-Case 'c4 dialog set-text: --text'   'New name' (Get-OptValue $o @('text', 'value') -PositionalIndex 1)
# The named form still wins over any positional.
$o = Parse-Options @('ignored', '--to', 'Named')
Test-Case 'c5 a named flag beats the positional at its index' 'Named' (Get-OptValue $o @('to', 'target') -PositionalIndex 1)

# ---------------------------------------------------------------------------
# (d) A bad value is the CALLER's error (InvalidInput), never an unhandled cast
# ---------------------------------------------------------------------------
$o = Parse-Options @('--depth', 'abc')
Test-Case 'd1 non-numeric --depth does not parse' 'False' ([string](Get-OptInt $o @('depth') 40).ok)
Test-Case 'd2 and it says what it wanted' "--depth expects a whole number, got 'abc'." (Get-OptInt $o @('depth') 40).message
$o = Parse-Options @('--depth', '6')
Test-Case 'd3 a numeric --depth parses'          '6'    ([string](Get-OptInt $o @('depth') 40).value)
$o = Parse-Options @('--depth')
Test-Case 'd4 a bare --depth falls back to the default' '40' ([string](Get-OptInt $o @('depth') 40).value)

# ---------------------------------------------------------------------------
# (e) Bare switches must not become target names
# ---------------------------------------------------------------------------
$o = Parse-Options @('--path')
Test-Case 'e1 a bare --path is not a node named "True"' '<null>' (Get-PathOpt $o)
$o = Parse-Options @('--tree')
Test-Case 'e2 a bare --tree falls back to TV1'  'InstallationTree' (Resolve-TreeId $o)
$o = Parse-Options @('--tree', 'TV2')
Test-Case 'e3 TV2 maps to the functions pane'   'FunctionsTree'    (Resolve-TreeId $o)
$o = Parse-Options @('--tree', 'tv1')
Test-Case 'e4 the selector is case-insensitive' 'InstallationTree' (Resolve-TreeId $o)
$o = Parse-Options @('--tree', 'SomeOtherTree')
Test-Case 'e5 an unknown selector passes through as a raw AutomationId' 'SomeOtherTree' (Resolve-TreeId $o)
$o = Parse-Options @()
Test-Case 'e6 no --tree at all is TV1'          'InstallationTree' (Resolve-TreeId $o)

# ---------------------------------------------------------------------------
# (f) Every numeric/secondary option in the driver is read NamedOnly or by index.
#     Asked of the syntax tree, so a new call site cannot quietly reintroduce the shared fallback.
# ---------------------------------------------------------------------------
$mustBeGuarded = @('depth', 'after', 'x-offset')
function Find-LooseOptionReads {
    param($Tree)
    $loose = @()
    foreach ($call in $Tree.FindAll({
        $args[0] -is [System.Management.Automation.Language.CommandAst] -and
        $args[0].GetCommandName() -eq 'Get-OptValue' }, $true)) {
        $text = $call.Extent.Text
        if ($text -match '-NamedOnly' -or $text -match '-PositionalIndex') { continue }
        foreach ($k in $mustBeGuarded) {
            if ($text -match "'$([regex]::Escape($k))'") { $loose += "$k at line $($call.Extent.StartLineNumber)" }
        }
    }
    return $loose
}
Test-Case 'f1 no numeric/secondary option reads the shared positional' '' ((Find-LooseOptionReads $ast) -join ', ')

# ARMED-DETECTOR CHECK. A scan that matches nothing passes whether or not it works, so prove it fires:
# the same predicate, over a seeded violation, must report it. Without this, f1 would keep passing if
# the pattern were broken -- which is exactly how a rule quietly stops guarding anything.
$decoyErrors = $null
$decoy = [System.Management.Automation.Language.Parser]::ParseInput(
    '$d = Get-OptValue $Opts @(''depth'')', [ref]$null, [ref]$decoyErrors)
Test-Case 'f2 the f1 scan is armed (it flags a seeded violation)' 'depth at line 1' ((Find-LooseOptionReads $decoy) -join ', ')

# ---------------------------------------------------------------------------
# (g) A label path splits on UNESCAPED '/' only
#
#     The app labels every link row with the opposite end's full path joined by " / "
#     (TreeLabelFormatter.LinkOppositePath), so an unescapable '/' made that whole node kind
#     unreachable by label -- addressable only by the index form the docs warn against.
# ---------------------------------------------------------------------------
Test-Case 'g1 a plain path splits per segment'    'Localities|Kitchen' ((Split-TreePath 'Localities/Kitchen') -join '|')
Test-Case 'g2 empty segments are dropped'         'a|b'                ((Split-TreePath '/a//b/') -join '|')
Test-Case 'g3 an escaped slash stays in the label' 'Stue|Lux / Temperatur' ((Split-TreePath 'Stue/Lux \/ Temperatur') -join '|')
Test-Case 'g4 a link-row label survives whole'    'Stue|Lampe / Pin 1 / Udgang' `
    ((Split-TreePath 'Stue/Lampe \/ Pin 1 \/ Udgang') -join '|')
Test-Case 'g5 one segment, no slash at all'       'Localities'         ((Split-TreePath 'Localities') -join '|')
Test-Case 'g6 an empty path is no segments'       '0'                  ([string]@(Split-TreePath '').Count)

# ---------------------------------------------------------------------------
# (h) Which sibling a segment names -- and a REFUSAL when more than one answers to it
#
#     Duplicate sibling labels are ordinary here (two products of the same type under one locality).
#     First-match-wins let a mutating command act on the wrong row and report success, which no envelope
#     field could have revealed.
# ---------------------------------------------------------------------------
$siblings = @('Kitchen', 'Lampe', 'Lampe', 'Ved dor ')
Test-Case 'h1 an index segment picks by position'  '1' ([string](Resolve-ChildIndex $siblings '1').index)
Test-Case 'h2 a unique label resolves'             '0' ([string](Resolve-ChildIndex $siblings 'Kitchen').index)
Test-Case 'h3 a trailing space is tolerated'       '3' ([string](Resolve-ChildIndex $siblings 'Ved dor').index)
Test-Case 'h4 a duplicate label is NOT the first'  'False' ([string](Resolve-ChildIndex $siblings 'Lampe').ok)
Test-Case 'h5 ... it is TargetAmbiguous'           'TargetAmbiguous' (Resolve-ChildIndex $siblings 'Lampe').code
Test-Case 'h6 ... and it names the candidates'     'True' `
    ([string]((Resolve-ChildIndex $siblings 'Lampe').message -match 'indices 1, 2'))
Test-Case 'h7 an unknown label is TargetNotFound'  'TargetNotFound' (Resolve-ChildIndex $siblings 'Nope').code
Test-Case 'h8 an out-of-range index is not silent' 'TargetNotFound' (Resolve-ChildIndex $siblings '9').code
Test-Case 'h9 an index still wins over duplicates' '2' ([string](Resolve-ChildIndex $siblings '2').index)
# Uniform shape: reading .code off a success result must not throw under StrictMode.
Test-Case 'h10 success carries a code too'         'Ok' (Resolve-ChildIndex $siblings 'Kitchen').code

# ---------------------------------------------------------------------------
# (i) A raw gesture that destroys is gated like the command that does it by name
# ---------------------------------------------------------------------------
Test-Case 'i1 {DELETE} is destructive'         'True'  ([string](Test-DestructiveGesture '{DELETE}'))
Test-Case 'i2 its {DEL} spelling too'          'True'  ([string](Test-DestructiveGesture '{DEL}'))
Test-Case 'i3 case does not launder it'        'True'  ([string](Test-DestructiveGesture '{delete}'))
Test-Case 'i4 nor does a compound chord'       'True'  ([string](Test-DestructiveGesture '+{DELETE}'))
Test-Case 'i5 navigation is not destructive'   'False' ([string](Test-DestructiveGesture '{DOWN}'))
Test-Case 'i6 nor is a rename'                 'False' ([string](Test-DestructiveGesture '{F2}'))

# ---------------------------------------------------------------------------
# (j) "Does the title bar name this file?" is a literal test, not a wildcard match
#
#     '[' and ']' are legal in a Windows filename and are character-class syntax to -like, so the old
#     `-like "$leaf*"` reported NoEffect for a save that had worked.
# ---------------------------------------------------------------------------
Test-Case 'j1 the plain prefix matches'        'True'  ([string](Test-TitleNamesFile 'project3.vis - IHC OpenVisual' 'project3.vis'))
Test-Case 'j2 brackets are literal, not a class' 'True' ([string](Test-TitleNamesFile 'project[1].vis - IHC OpenVisual' 'project[1].vis'))
Test-Case 'j3 ... and do not match their expansion' 'False' ([string](Test-TitleNamesFile 'project1.vis - IHC OpenVisual' 'project[1].vis'))
Test-Case 'j4 case-insensitive, as the filesystem is' 'True' ([string](Test-TitleNamesFile 'PROJECT3.VIS - IHC OpenVisual' 'project3.vis'))
Test-Case 'j5 a different file does not verify' 'False' ([string](Test-TitleNamesFile 'other.vis - IHC OpenVisual' 'project3.vis'))
Test-Case 'j6 an empty title verifies nothing'  'False' ([string](Test-TitleNamesFile '' 'project3.vis'))

# ---------------------------------------------------------------------------
# (k) Structural rules, asked of the syntax tree: things a value test cannot reach because they need a
#     live app -- but whose REGRESSION is a source-level shape.
# ---------------------------------------------------------------------------
function Get-FunctionAst { param($Tree, [string] $Name)
    return $Tree.FindAll({ $args[0] -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                           $args[0].Name -eq $Name }, $true) | Select-Object -First 1
}

# k1: $treeId must be assigned OUTSIDE the optional --path branch. It is read unconditionally by the
# delta the command returns, so assigning it only inside that `if` made StrictMode throw AFTER SendWait
# had fired: the gesture happened, the envelope said MutationFailed, and a retry would repeat it.
function Test-AssignedAtTopLevel { param($Fn, [string] $VarName)
    if (-not $Fn) { return 'no such function' }
    foreach ($st in $Fn.Body.EndBlock.Statements) {
        if ($st -is [System.Management.Automation.Language.AssignmentStatementAst] -and
            $st.Left.Extent.Text -eq "`$$VarName") { return 'top-level' }
    }
    return 'nested or absent'
}
Test-Case 'k1 keySend assigns $treeId unconditionally' 'top-level' `
    (Test-AssignedAtTopLevel (Get-FunctionAst $ast 'Invoke-Mechanism-KeySend') 'treeId')
$decoyErrors = $null
$decoy2 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function F { if ($p) { $treeId = 1 }; $x = $treeId }', [ref]$null, [ref]$decoyErrors)
Test-Case 'k2 the k1 check is armed' 'nested or absent' `
    (Test-AssignedAtTopLevel (Get-FunctionAst $decoy2 'F') 'treeId')

# k3: every TreeItem walk goes through Get-TreeItemChildren. FindAll(TreeScope::Children) does not return
# a TreeViewItem's own rows in this app -- Resolve-TreePath moved to a TreeWalker for that reason and said
# so in a comment, while tree.dump, the comparison's structural ORACLE, kept the rejected enumeration and
# stamped its output verified:true.
function Find-RawTreeItemWalks {
    param($Tree)
    $bad = @()
    foreach ($call in $Tree.FindAll({
        $args[0] -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and
        "$($args[0].Member.Extent.Text)" -eq 'FindAll' }, $true)) {
        $text = $call.Extent.Text
        if ($text -match 'ChildScope' -and $text -match 'ControlType\]::TreeItem') {
            $bad += "line $($call.Extent.StartLineNumber)"
        }
    }
    return $bad
}
Test-Case 'k3 no TreeItem walk uses FindAll(Children)' '' ((Find-RawTreeItemWalks $ast) -join ', ')
$decoy3 = [System.Management.Automation.Language.Parser]::ParseInput(
    '$k = $el.FindAll($script:ChildScope, (New-PropCondition $p ([System.Windows.Automation.ControlType]::TreeItem)))',
    [ref]$null, [ref]$decoyErrors)
Test-Case 'k4 the k3 scan is armed' 'line 1' ((Find-RawTreeItemWalks $decoy3) -join ', ')

# k5: both key-sending mechanisms consult the destructive-gesture gate. key.send is ungated by design, and
# the app routes Key.Delete to edit.delete, so without this the driver shipped an unlocked side door to the
# removal it advertises as needing --confirm-destructive.
function Test-CallsGate { param($Fn)
    if (-not $Fn) { return 'no such function' }
    $hit = $Fn.FindAll({ $args[0] -is [System.Management.Automation.Language.CommandAst] -and
                         $args[0].GetCommandName() -eq 'Test-DestructiveGesture' }, $true)
    return $(if (@($hit).Count -gt 0) { 'gated' } else { 'UNGATED' })
}
Test-Case 'k5 keySend checks the destructive gate' 'gated' (Test-CallsGate (Get-FunctionAst $ast 'Invoke-Mechanism-KeySend'))
Test-Case 'k6 the fixed-gesture mechanism too'    'gated' (Test-CallsGate (Get-FunctionAst $ast 'Invoke-Mechanism-Key'))
$decoy4 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function F { [System.Windows.Forms.SendKeys]::SendWait($g) }', [ref]$null, [ref]$decoyErrors)
Test-Case 'k7 the k5/k6 check is armed' 'UNGATED' (Test-CallsGate (Get-FunctionAst $decoy4 'F'))

# k8: bootstrap runs INSIDE the try that converts an exception into an envelope. Add-Type failing on a host
# without the UIA assemblies, or a UIA fault while resolving the window, used to print a PowerShell error
# record and no JSON -- breaking the one-envelope-per-invocation contract on exactly the failures a scripted
# caller cannot otherwise diagnose.
function Find-UnguardedBootstrap {
    param($Tree)
    $tries = @($Tree.FindAll({ $args[0] -is [System.Management.Automation.Language.TryStatementAst] }, $true))
    $loose = @()
    foreach ($name in @('Initialize-Uia', 'Resolve-MainWindow', 'Import-Registry')) {
        foreach ($call in $Tree.FindAll({
            $args[0] -is [System.Management.Automation.Language.CommandAst] -and
            $args[0].GetCommandName() -eq $name }, $true)) {
            # Only the top-level invocations matter; a call inside a function body is not the bootstrap.
            if ($call.Extent.StartLineNumber -lt $script:MainSectionLine) { continue }
            $inside = $false
            foreach ($t in $tries) {
                if ($t.Body.Extent.StartOffset -le $call.Extent.StartOffset -and
                    $t.Body.Extent.EndOffset   -ge $call.Extent.EndOffset) { $inside = $true; break }
            }
            if (-not $inside) { $loose += "$name at line $($call.Extent.StartLineNumber)" }
        }
    }
    return $loose
}
# The "Main" banner marks where the script stops defining and starts executing.
$script:MainSectionLine = 0
foreach ($line in Get-Content -LiteralPath $auiPath) {
    $script:MainSectionLine++
    if ($line -match '^#\s*Main\s*$') { break }
}
Test-Case 'k8 no bootstrap step runs outside a try' '' ((Find-UnguardedBootstrap $ast) -join ', ')
$decoy5 = [System.Management.Automation.Language.Parser]::ParseInput('Initialize-Uia', [ref]$null, [ref]$decoyErrors)
$savedLine = $script:MainSectionLine; $script:MainSectionLine = 0
Test-Case 'k9 the k8 scan is armed' 'Initialize-Uia at line 1' ((Find-UnguardedBootstrap $decoy5) -join ', ')
$script:MainSectionLine = $savedLine

# k10: a separator must not set the truncation flag. Separators are deliberately INCLUDED in menu dumps, and
# sharing one condition with the depth limit made every menu that merely groups its items warn that deeper
# submenus had been omitted -- about menus that have none.
function Test-SeparatorNotTruncation {
    param($Tree)
    $fn = Get-FunctionAst $Tree 'Get-MenuLevel'
    if (-not $fn) { return 'no Get-MenuLevel' }
    $guard = $fn.FindAll({ $args[0] -is [System.Management.Automation.Language.IfStatementAst] -and
                           "$($args[0].Clauses[0].Item1.Extent.Text)" -match '-not\s+\$isSeparator' }, $true) |
             Select-Object -First 1
    if (-not $guard) { return 'no separator guard' }
    # The THEN-block, not the whole statement: an `else` belongs to the if's extent too, and that is exactly
    # where the bug lived -- `if (depth -and -not separator) {...} else { truncated = true }` reads as
    # guarded if you measure the statement instead of the branch it guards.
    $body = $guard.Clauses[0].Item2.Extent
    foreach ($a in $fn.FindAll({ $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] -and
                                 $args[0].Left.Extent.Text -match 'MenuTruncated' }, $true)) {
        if ($a.Extent.StartOffset -lt $body.StartOffset -or
            $a.Extent.EndOffset   -gt $body.EndOffset) { return "reachable from a separator (line $($a.Extent.StartLineNumber))" }
    }
    return 'guarded'
}
Test-Case 'k10 a separator cannot set MenuTruncated' 'guarded' (Test-SeparatorNotTruncation $ast)
$decoy6 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function Get-MenuLevel { if ($Depth -lt $m -and -not $isSeparator) { $x = 1 } else { $script:MenuTruncated = $true } }',
    [ref]$null, [ref]$decoyErrors)
Test-Case 'k11 the k10 check is armed' 'reachable from a separator (line 1)' (Test-SeparatorNotTruncation $decoy6)

# k12: Escape is an application command (program.leaveMode), not a popup-only primitive. Menu cleanup
# must not call any of this driver's keyboard, click, right-click, or drag emitters.
function Test-MenuCleanupAvoidsKeyboardAndClick {
    param($Tree)
    $fn = Get-FunctionAst $Tree 'Close-AllMenus'
    if (-not $fn) { return 'no Close-AllMenus' }
    $members = @($fn.FindAll({
        $args[0] -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and
        $args[0].Member.Extent.Text -in @('SendWait', 'keybd_event', 'Click', 'RightClick', 'Drag', 'mouse_event')
    }, $true))
    foreach ($member in $members) {
        if ($member.Member.Extent.Text -in @('SendWait', 'keybd_event')) { return 'keyboard input' }
        return 'application-content input'
    }
    foreach ($command in $fn.FindAll({ $args[0] -is [System.Management.Automation.Language.CommandAst] }, $true)) {
        $name = $command.GetCommandName()
        if ($name -and $name -match '(?i)(click|drag)') { return 'application-content input' }
    }
    return 'safe'
}
Test-Case 'k12 menu cleanup calls no keyboard or application-content input emitter' 'safe' (Test-MenuCleanupAvoidsKeyboardAndClick $ast)
$decoy7 = [System.Management.Automation.Language.Parser]::ParseInput(
    "function Close-AllMenus { [System.Windows.Forms.SendKeys]::SendWait(`$computedGesture) }",
    [ref]$null, [ref]$decoyErrors)
Test-Case 'k13 the k12 keyboard check is armed' 'keyboard input' (Test-MenuCleanupAvoidsKeyboardAndClick $decoy7)
$decoy7b = [System.Management.Automation.Language.Parser]::ParseInput(
    'function Close-AllMenus { Invoke-ElementClick $row }', [ref]$null, [ref]$decoyErrors)
Test-Case 'k13b the k12 wrapped-click check is armed' 'application-content input' `
    (Test-MenuCleanupAvoidsKeyboardAndClick $decoy7b)

# k14: popup identity comes from one item: the condition positively re-hit-tests `$live`, and the
# close coordinates come from that same item's fresh bounding rectangle.
function Test-MenuCleanupGuardsNativePopupClose {
    param($Tree)
    $fn = Get-FunctionAst $Tree 'Close-AllMenus'
    if (-not $fn) { return 'no Close-AllMenus' }
    $calls = @($fn.FindAll({
        $args[0] -is [System.Management.Automation.Language.InvokeMemberExpressionAst] -and
        $args[0].Member.Extent.Text -eq 'CloseOwnedPopupAtPoint' }, $true))
    if ($calls.Count -eq 0) { return 'missing native popup close' }
    if ($calls.Count -gt 1) { return 'multiple native popup closes' }
    $call = $calls[0]
    $enclosed = $false
    foreach ($if in $fn.FindAll({ $args[0] -is [System.Management.Automation.Language.IfStatementAst] }, $true)) {
        $body = $if.Clauses[0].Item2.Extent
        if ($body.StartOffset -gt $call.Extent.StartOffset -or $body.EndOffset -lt $call.Extent.EndOffset) { continue }
        $enclosed = $true
        $conditionText = $if.Clauses[0].Item1.Extent.Text
        if ($conditionText -match '-not' -or
            $conditionText -notmatch '\$live\s+-and\s+\(?\s*Test-LivePopupItem\s+\$live\s*\)?') { continue }
        $rectAssignment = $fn.FindAll({
            $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] -and
            $args[0].Left.Extent.Text -eq '$r' -and
            $args[0].Right.Extent.Text -eq '$live.Current.BoundingRectangle' }, $true) |
            Where-Object { $_.Extent.StartOffset -ge $body.StartOffset -and
                           $_.Extent.EndOffset -lt $call.Extent.StartOffset } |
            Select-Object -First 1
        if (-not $rectAssignment) { continue }
        if ($call.Extent.Text -notmatch '\$r\.X' -or $call.Extent.Text -notmatch '\$r\.Width' -or
            $call.Extent.Text -notmatch '\$r\.Y' -or $call.Extent.Text -notmatch '\$r\.Height') {
            continue
        }
        return 'guarded'
    }
    return $(if ($enclosed) { 'wrong live-item provenance' } else { 'unguarded native popup close' })
}
Test-Case 'k14 menu cleanup guards its native popup close' 'guarded' (Test-MenuCleanupGuardsNativePopupClose $ast)
$decoy8 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function Close-AllMenus { if (Test-LivePopupItem $other) { $r = $live.Current.BoundingRectangle; [Aui.Win32]::CloseOwnedPopupAtPoint(1, $main, 12, 100) } }',
    [ref]$null, [ref]$decoyErrors)
Test-Case 'k15 the k14 check is armed' 'wrong live-item provenance' (Test-MenuCleanupGuardsNativePopupClose $decoy8)

# k16: after the call-site hit test establishes popup identity, the native helper must still refuse
# the main window and any foreign, hidden, non-owned, titled, or non-Avalonia top-level HWND.
function Test-NativePopupCloseGuards {
    param([string] $Text)
    $m = [regex]::Match($Text,
        '(?ms)^\s*public static bool CloseOwnedPopupAtPoint\(.*?^\s{4}\}')
    if (-not $m.Success) { return 'no native popup close helper' }
    $body = $m.Value
    $missing = @()
    if ($body -notmatch 'main\s*==\s*IntPtr\.Zero') { $missing += 'main' }
    if ($body -notmatch 'target\s*==\s*IntPtr\.Zero\s*\|\|\s*target\s*==\s*main') { $missing += 'target' }
    if ($body -notmatch 'GetWindowThreadProcessId\(target,\s*out\s+ownerPid\)' -or
        $body -notmatch 'ownerPid\s*!=\s*\(uint\)pid') { $missing += 'process' }
    if ($body -notmatch '!IsWindowVisible\(target\)') { $missing += 'visibility' }
    if ($body -notmatch 'GetWindow\(target,\s*GW_OWNER\)\s*!=\s*main') { $missing += 'owner' }
    if ($body -notmatch 'StartsWith\("Avalonia-",\s*StringComparison\.Ordinal\)') { $missing += 'class' }
    if ($body -notmatch 'TextOf\(target\)\.Length\s*!=\s*0') { $missing += 'title' }
    if ($body -notmatch 'PostMessageW\(target,\s*WM_CLOSE') { $missing += 'post-target' }
    return $(if ($missing.Count -eq 0) { 'guarded' } else { 'missing: ' + ($missing -join ',') })
}
Test-Case 'k16 native popup close enforces its target guards' 'guarded' (Test-NativePopupCloseGuards $source)
$decoy9 = "    public static bool CloseOwnedPopupAtPoint(int pid, IntPtr main, int x, int y) {`n" +
          "      PostMessageW(main, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);`n" +
          "      return true;`n" +
          "    }"
Test-Case 'k17 the k16 check is armed' 'missing: main,target,process,visibility,owner,class,title,post-target' `
    (Test-NativePopupCloseGuards $decoy9)

# ---------------------------------------------------------------------------
# (l) --help is a terminal CLI route, not an option passed through to a command mechanism
#
#     Run the shipping script in a child process: function-only tests cannot prove that Main exits
#     before app resolution and dispatch. `catalog commands` is intentionally harmless on the broken
#     path, while its normal payload is distinct enough to prove whether help intercepted it.
# ---------------------------------------------------------------------------
function Get-PropertyValue {
    param($Object, [string] $Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($property) { return $property.Value }
    return $null
}

$helpOutput = @(& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $auiPath catalog commands --help 2>&1)
$helpExit = $LASTEXITCODE
$helpEnvelope = $null
try { $helpEnvelope = (($helpOutput -join "`n") | ConvertFrom-Json) } catch { }
$helpData = Get-PropertyValue $helpEnvelope 'data'
$helpCommand = Get-PropertyValue $helpData 'command'

Test-Case 'l1 command --help exits successfully' '0' ([string]$helpExit)
Test-Case 'l2 command --help returns one JSON envelope' 'True' ([string]($null -ne $helpEnvelope))
Test-Case 'l3 command --help identifies the help payload' 'commandHelp' (Get-PropertyValue $helpData 'kind')
Test-Case 'l4 command --help identifies the resolved command' 'catalog.commands' (Get-PropertyValue $helpCommand 'id')
Test-Case 'l5 command --help never resolves live app context' '<null>' (Get-PropertyValue $helpEnvelope 'context')
Test-Case 'l5b help advertises only JSON-envelope flags' '--help|-h' `
    (@(Get-PropertyValue $helpData 'helpFlags') -join '|')

function Test-HelpRouteOrder {
    param([string] $Text)
    $marker = [regex]::Match($Text, '(?m)^# Main\r?$')
    if (-not $marker.Success) { return 'missing Main marker' }
    $main = $Text.Substring($marker.Index)
    $help = $main.LastIndexOf('if ($helpRequested)')
    $bootstrap = $main.IndexOf('Initialize-Uia')
    $dispatch = $main.IndexOf('Invoke-Command-Spec')
    if ($help -lt 0) { return 'missing help route' }
    if ($bootstrap -lt 0 -or $dispatch -lt 0) { return 'missing bootstrap/dispatch' }
    return $(if ($help -lt $bootstrap -and $help -lt $dispatch) { 'early' } else { 'LATE' })
}
Test-Case 'l6 command help exits before UIA bootstrap and dispatch' 'early' (Test-HelpRouteOrder $source)
$lateHelp = "# Main`r`nInitialize-Uia`r`nif (`$helpRequested) { Write-Result `$help }`r`nInvoke-Command-Spec"
Test-Case 'l7 the l6 ordering check is armed' 'LATE' (Test-HelpRouteOrder $lateHelp)

# (m) node.drag addresses each endpoint's pane independently
#
#     A single --tree can drive a reorder but cannot express TV1 resource -> TV2 program-group.
#     S2-10 needs distinct endpoint selectors while preserving --tree as the legacy fallback.
function Test-NodeDragEndpointTrees {
    param($Tree)
    $fn = Get-FunctionAst $Tree 'Invoke-Mechanism-NodeDrag'
    if (-not $fn) { return 'no nodeDrag mechanism' }
    $text = $fn.Extent.Text
    if ($text -notmatch "Resolve-TreeId\s+\`$Opts\s+@\('from-tree'\)") { return 'missing from-tree resolution' }
    if ($text -notmatch "Resolve-TreeId\s+\`$Opts\s+@\('to-tree'\)") { return 'missing to-tree resolution' }
    $sourceResolutions = @([regex]::Matches($text, 'Resolve-TreePath\s+\$Window\s+\$fromTreeId\s+\$from')).Count
    $targetResolutions = @([regex]::Matches($text, 'Resolve-TreePath\s+\$Window\s+\$toTreeId\s+\$to')).Count
    if ($sourceResolutions -lt 2) { return 'source re-resolution uses wrong tree' }
    if ($targetResolutions -lt 2) { return 'target re-resolution uses wrong tree' }
    return 'separate endpoint trees'
}
Test-Case 'm1 node.drag resolves source and target panes independently' 'separate endpoint trees' `
    (Test-NodeDragEndpointTrees $ast)
$decoy10 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function Invoke-Mechanism-NodeDrag { $treeId = Resolve-TreeId $Opts; Resolve-TreePath $Window $treeId $from; Resolve-TreePath $Window $treeId $to }',
    [ref]$null, [ref]$decoyErrors)
Test-Case 'm2 the m1 endpoint-tree check is armed' 'missing from-tree resolution' `
    (Test-NodeDragEndpointTrees $decoy10)

$o = Parse-Options @('--tree', 'TV2')
Test-Case 'm3 omitted from-tree inherits legacy --tree' 'FunctionsTree' (Resolve-TreeId $o @('from-tree'))
Test-Case 'm4 omitted to-tree inherits legacy --tree' 'FunctionsTree' (Resolve-TreeId $o @('to-tree'))

function Test-NodeDragCrossPaneOracle {
    param($Tree)
    $fn = Get-FunctionAst $Tree 'Invoke-Mechanism-NodeDrag'
    if (-not $fn) { return 'no nodeDrag mechanism' }
    $text = $fn.Extent.Text
    $assignments = @($fn.FindAll({
        $args[0] -is [System.Management.Automation.Language.AssignmentStatementAst] -and
        $args[0].Left.Extent.Text -eq '$statusNamesEndpoints'
    }, $true))
    if ($assignments.Count -eq 0) { return 'missing endpoint-named status oracle' }
    foreach ($assignment in $assignments) {
        $rhs = $assignment.Right.Extent.Text
        if ($rhs -notmatch '\$statusChanged' -or $rhs -notmatch 'IndexOf\(\$sourceName' -or
            $rhs -notmatch 'IndexOf\(\$targetName') { return 'status assignment does not prove both endpoints' }
    }
    if ($text -notmatch '\$effectObserved\s*=\s*if\s*\(\$crossPane\)\s*\{\s*\$statusNamesEndpoints\s*\}\s*else\s*\{\s*\$structureChanged\s*\}') {
        return 'cross-pane effect is not isolated from realized-row churn'
    }
    if ($text -notmatch '\$moved\s*=\s*\(-not\s+\$crossPane\)\s+-and\s+\$structureChanged') {
        return 'cross-pane drag can report moved'
    }
    return 'cross-pane status isolated from row churn'
}
Test-Case 'm5 cross-pane drag accepts a changed status only when it names both endpoints' `
    'cross-pane status isolated from row churn' (Test-NodeDragCrossPaneOracle $ast)
$decoy11 = [System.Management.Automation.Language.Parser]::ParseInput(
    'function Invoke-Mechanism-NodeDrag { $statusChanged = $before.statusText -ne $after.statusText; $effectObserved = $crossPane -and $statusChanged }',
    [ref]$null, [ref]$decoyErrors)
Test-Case 'm6 the m5 status-oracle check is armed' 'missing endpoint-named status oracle' `
    (Test-NodeDragCrossPaneOracle $decoy11)

Write-Host ''
if ($script:Failed -gt 0) {
    Write-Host "$($script:Ran) case(s), $($script:Failed) FAILED."
    exit 1
}
Write-Host "$($script:Ran) case(s), all passed."
exit 0
