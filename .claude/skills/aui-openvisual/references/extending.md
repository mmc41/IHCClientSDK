# Extending the command vocabulary

The driver is a **declarative registry + generic mechanisms**. Most new commands are one JSON row.

## 1. Reusing an existing mechanism (no code)

Add an object to the `commands` array in `scripts/commands.json`. Required keys: `id`, `status`
(`confirmed`/`partial`/`planned`), `mutating` (`no`/`noState`/`yes`), `mechanism`, `gates` (array),
`description`. Then add the mechanism-specific keys:

| Mechanism | Extra keys | What it does |
|-----------|-----------|--------------|
| `invoke` | `automationId` | Invoke a control by AutomationId (toolbar buttons). |
| `key` | `gesture` | Send a fixed SendKeys gesture to the window (`^s`, `^+b`, `{F3}`, `{ESC}`). |
| `keySend` | — | Send a caller-supplied `--gesture`; optional `--path` selects a node first. |
| `menu` | `menuPath` | Walk `Top/Item/Sub…` via the ExpandCollapse/Invoke patterns (click fallback), invoking the leaf. Segments match an AutomationId **or** a label; `--id <AutomationId>` skips the path. `--menu-path` appends (dynamic catalogs). |
| `menuBarDump` | `menu` (optional) | Enumerate the menu bar into `data.titles[]`; opens submenus by hover only. |
| `contextMenu` | `item` | Select `--path`, open its context flyout, invoke `item`. |
| `contextMenuDump` | — | Select `--path`, open its flyout, enumerate items + screenshot, close. |
| `treeSelect` | — | Select `--path` in `--tree`. |
| `treeDump` | — | Recursive dump of `--tree` into `data.root` (`--expand-all`, `--path`, `--after`, `--depth`, `--with-kind`). Resolves `--path` without selecting it. |
| `expandCollapse` | `action` (`expand`/`collapse`) | Expand/collapse `--path` via the ExpandCollapse pattern. |
| `doubleClick` | — | Double-click `--path` at its clickable point (`--x-offset N` to click right of the label). |
| `rightClick` | — | REAL right-click on `--path`, reporting whether the caret moved and what flyout it raised. Deliberately does not select first. |
| `nodeDrag` | — | Real mouse drag `--from` onto `--to` within one pane; verified against the pane's row order. |
| `readProperty` | `property` (`helpText`/`name`) | Read a UIA property of `--path`. Resolves without selecting. |
| `fileDialog` | `automationId` or `menuPath`, `dialogKind` (`open`/`save`) | Raise the OS picker, type `--path`, commit, verify by title. |
| `dialogRead` | — | Enumerate the OPEN modal's controls (read-only). |
| `dialogSetText` | — | Set `--field` to `--text` in the open modal via ValuePattern, with readback. |
| `dialogButton` | — | Invoke `--button` in the open modal. |
| `capture` | `scope` (`window`/`modal`/`control`) | Screenshot to PNG; control scope resolves `--id <AutomationId>`. |
| `passive` / `static` / `dialogCancel` / `notImplemented` | — | Inspection / self-describe / dismiss modal / stub. |

Keep this table in step with the `switch` in `Invoke-Command-Spec`: a mechanism that exists in the
dispatcher but not here reads as unavailable, and the whole point of the table is that most new
commands need no code.

The command is immediately live: `aui catalog commands` lists it and the dispatcher runs it. Because
the toolbar buttons and top-level menus already carry stable AutomationIds, and the two trees are
`InstallationTree`/`FunctionsTree`, most new OpenVisual features map to an existing mechanism.

**Gotcha — accessible names are localized.** `invoke`/`menu`/`contextMenu` match by the app's English
display text today. Prefer `automationId` (locale-independent) where the control exposes one.

## 1b. Hard-won facts about this app's UI (verified live 2026-07-15)

Do not re-derive these; they shaped the mechanisms above.

- **Avalonia MenuItems expose NO UIA patterns** — not `Invoke`, not `ExpandCollapse` — at *any* level,
  menu-bar root or realized popup. Pattern-driven menu walking is impossible; `menu`/`menuBarDump`
  therefore use real mouse clicks and hovers on element rects.
- **Alt access-key chords do not open menus** via `SendKeys` even when the app truly has the
  foreground. The old `menuKey: "%f"` rows never worked; `menuPath` replaced them.
- **Clicking a menu item INVOKES it.** A menu *dump* must open submenus by **hover** only, or walking
  `File` presses `Exit` and walking `Controller` presses `Send project`. Only the eight menu-bar
  roots are safe to click — they are always containers.
- **Menus open on hover once the bar is in menu mode**, so a pointer left resting on a root makes the
  next "click the root to open it" *toggle it shut*. `Close-AllMenus` parks the pointer for this reason.
- **UIA RuntimeIds are regenerated on every menu reopen** (`Theme` moved `42.5178760.4.x` →
  `42.5309832.4.x`). Identities may only be compared *within one open session*.
- **`SetForegroundWindow` returns `true` while doing nothing** when the caller lacks foreground
  rights. Always read `GetForegroundWindow` back — `Set-Foreground` does, and returns a bool.
- **The file picker is an in-process `#32770`** whose control ids are *not* the classic ones (`1148`
  is a Pane with no Edit; `1`/`2` collide with file-list ListItems). It opens with focus already in
  the file-name field, so typing the path + Enter is the reliable route.
- **Tree items carry `<kind>#<element id>` as the AutomationId**, and their UIA `Name` is the visible
  label. `tree dump --with-kind` reports both halves separately: `kind` (the bare token, what a census
  partitions by) and `id` (the whole locator, what addresses ONE row).
  (History: this bullet first read "Tree items carry no AutomationId" — measured, then expired when
  `MainWindow.axaml`'s `TreeNodeItemTheme` started binding the id, corrected 2026-08-02. It then read
  "carry their KIND", which was true but was a *collision*: ten sibling localities all answered to
  `locality`, so no client could address one of them. The element id was appended 2026-08-08 —
  `TreeNodeViewModel.AutomationId`.)
- **Dialogs are separate top-level windows** (`Views\ResultDialog.cs` → `ShowDialog(owner)`), not
  in-window overlays, and one of them is captioned `About IHC OpenVisual` — so "is this the main
  window?" must be answered by window handle, never by matching the product name in the title.

## 1c. Contract rules for new mechanisms

- **Never synthesize input without `Assert-Foreground $Window` first.** It returns `$null` when the app
  has the foreground, otherwise a failure result to return unchanged. Skipping it types into whatever
  window the user actually has in front.
- **Read `$Spec` keys defensively.** `Set-StrictMode -Version Latest` turns a missing key into a
  `PropertyNotFound` throw, not a clean `InvalidInput`: guard with
  `$Spec.PSObject.Properties.Name -contains '<key>'`.
- **Return arrays unrolled** (`return $out`) and wrap at the call site with `@(...)`. `return ,@($out)`
  *plus* `@(...)` double-wraps into a 1-element array holding the array, which then silently
  member-enumerates (`$x.Current.Name` becomes an array) instead of erroring.
- **Read options through the right door.** `Get-OptValue` falls back to a positional *by design* for a
  command's primary argument. Anything else must be `-NamedOnly` (numeric flags, secondary options) or
  declare its `-PositionalIndex` (a genuine second positional such as `nodeDrag`'s `--to`). Sharing
  index 0 across two lookups is how `node drag A B` came to drag A onto itself. Parse integers with
  `Get-OptInt` so a bad value is `InvalidInput`, not a cast that the top-level catch relabels
  `MutationFailed`. `scripts/aui-options.tests.ps1` scans for regressions here.
- **Every Code needs a row in `$script:ExitTier`.** An unmapped code silently exits 1 — the
  "not running / not implemented" tier — so a hard failure reports as an inapplicable no-op.
- **Keep non-ASCII out of string literals** in `aui.ps1`. The file is BOM-less UTF-8; under Windows
  PowerShell 5.1 it is read as CP1252, and several UTF-8 sequences mojibake into `U+201D`, which the
  parser treats as a quote. Comments are safe (the parser skips them); strings are not.

## 2. A genuinely new behavior (small code change)

If no mechanism fits, add one to `scripts/aui.ps1`:

1. Write `function Invoke-Mechanism-<Name> { param($Spec,$Opts,$Window) … return (New-Result …) }`.
   Use the helpers: `Find-ByAutomationId`, `Find-ByName`, `Get-Pattern`, `Select-TreePath`
   (returns `.element`), `Set-Foreground`, `Get-Context`, and `New-Result`/exit `Code`s.
2. Add a `case '<name>' { Invoke-Mechanism-<Name> $Spec $Opts $Window }` in the `Invoke-Command-Spec`
   dispatcher switch.
3. Add the `commands.json` row with `"mechanism": "<name>"` and any params your function reads off
   `$Spec`.

## 3. Keeping the app automatable

New GUI affordances stay drivable if they follow the conventions the app already uses:

- Give command controls a stable `AutomationProperties.AutomationId` (toolbar/menu items).
- Ensure new tree content flows through the same row template (accessible name + tooltip→HelpText).
- Icon-only buttons need `AutomationProperties.Name`.
- Custom tree containers should expose the ExpandCollapse pattern (see the app's `AccessibleTreeView`).

## Design note

This surface deliberately mirrors a stable `domain.verb` + label-path + JSON-`CommandResult` shape so
an OpenVisual automation run reads the same as a run of the equivalent flow in the vendor tool it
reimplements — the two can be diffed command-for-command. Keep new ids semantic and consistent with
that scheme.
