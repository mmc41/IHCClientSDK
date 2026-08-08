---
name: aui-openvisual
description: >-
  Remote-control the IHC OpenVisual desktop app (applications/ihc_openvisual) through Windows UI
  Automation. Use this WHENEVER you need to drive, script, screenshot, or functionally test the
  running OpenVisual GUI — launching it, navigating the locality/function trees, invoking toolbar,
  menu and context-menu commands, expanding/collapsing, single- or double-clicking nodes, reading
  tooltips, capturing the window, or running a repeatable command sequence and checking its JSON
  result. Prefer this over ad-hoc PowerShell/pywinauto whenever the target is the OpenVisual app.
  Exposes a stable `domain.verb` command vocabulary with label-path node addressing and a uniform
  JSON result envelope, so runs are scriptable and diffable. WINDOWS ONLY — it errors with
  Code=PlatformUnsupported on macOS/Linux (UI Automation is a Windows API).
---

# aui-openvisual — UI-Automation driver for IHC OpenVisual

Drive a running (or freshly launched) **IHC OpenVisual** instance via Windows UI Automation. Every
command prints ONE JSON result to stdout and sets a process exit code, so multi-step runs are
scriptable and comparable line-by-line.

## When to use

- Functionally verifying an OpenVisual change end-to-end in the real app (not just headless tests).
- Scripting a repeatable GUI flow (open a project, navigate the tree, invoke a command, screenshot).
- Reading the live automation state (selection, open modal, tree contents) as JSON.
- Any "drive / click / screenshot / automate the OpenVisual window" request.

Do **not** use it to test SDK/engine logic (use the headless `safe_*` test suites) or on non-Windows.

## Running it

```bash
pwsh <skill>/scripts/aui.ps1 <domain> <verb> [positional] [--flag value] [--switch]
```

`pwsh` (PowerShell 7) or Windows PowerShell 5.1 both work — the driver uses the built-in
`System.Windows.Automation` client, so there is **nothing to install**. If `pwsh` is not on the
machine, substitute `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <path>\aui.ps1 …`;
the driver reads its registry as explicit UTF-8 so the 5.1 ANSI default cannot corrupt menu labels.

```bash
pwsh aui.ps1 catalog commands                 # list the whole vocabulary + status (self-describing)
pwsh aui.ps1 doctor --launch                  # launch the app if needed, then readiness preflight
pwsh aui.ps1 session status                   # live context snapshot
pwsh aui.ps1 tree select "Localities/Kitchen" # select a node by label path
pwsh aui.ps1 node expand "Localities"         # expand via the ExpandCollapse pattern
pwsh aui.ps1 project save                     # invoke the Save toolbar command
pwsh aui.ps1 capture window                   # screenshot the window to a PNG
```

**Always start a session with `doctor --launch`.** It launches the app **with `--skip-recovery`** so
no crash-recovery modal can block an unattended run, then confirms the window is usable.  Pass
`--launch` on any command to auto-start the app if it isn't running.

> **`doctor` gates readiness on a real DESCENDANT read, so trust it.** It reports
> `ready:false` + `Code=PreconditionMissing` when the main window resolves but its descendants do not
> (`data.deepUiaUsable:false`) — which is what an **integrity mismatch** looks like: run this session at
> the *same* elevation as OpenVisual. This matters because in that state every deep field
> (`selection`, `statusText`, `toolbarVisible`, tree contents) reads empty/false, and *empty is
> indistinguishable from the app genuinely being that way* — so a `ready:true` here would invite a whole
> session of confident readings of an empty tree. If doctor says blind, **record nothing** until it is fixed.

## Command vocabulary

Commands are `domain.verb` ids, invoked as `<domain> <verb>` (kebab verbs and nested words also
resolve, e.g. `view toolbar-toggle` → `view.toolbar.toggle`, `node double-click` → `node.doubleClick`).
The authoritative, self-describing list is **`scripts/commands.json`** — or run `aui catalog commands`
to print every id with its `status` and one-line description. Highlights:

| Area | Commands |
|------|----------|
| Session/inspect | `doctor`, `session status`, `session probe`, `catalog commands` |
| Dialogs | `dialog read`, `dialog set-text --field --text`, `dialog click --button`, `dialog cancel` |
| Capture | `capture window`, `capture modal`, `capture client` |
| Project | `project new`, `project open --path`, `project save`, `project save-as --path [--overwrite]`, `project recent list` |
| View/mode | `view configuration`, `programming enter`, `view toolbar-toggle`, `view statusbar-toggle` |
| Tree nav | `tree select`, `tree dump`, `node select`, `node expand`, `node collapse`, `node double-click`, `node right-click`, `node tooltip` |
| Gestures | `key send --gesture` (raw keys; refuses `{F5}`, gates `{DELETE}`) |
| Menus | `menu invoke --id <AutomationId>`, `menu dump-context --path`, `menu dump-bar [--menu X] [--depth N] [--with-id]` |
| Edit | `node cut/copy/paste`, `node delete`, `node drag --from --to`, `edit undo/redo`, `edit move-up/move-down` |
| Insert | `locality insert`, `product insert --menu-path`, `fb insert-template --menu-path`, `fb insert-empty`, `fb unlock`, `link start-from-here`, `link to-here` |
| Catalog | `catalog products [--depth N]`, `catalog function-blocks [--depth N]` |
| Docs | `projectInfo get`, `modules list`, `data-tables list`, `report generate --menu-path "<report>"` |
| Help | `help about`, `help for-selection`, `help settings`, `help telemetry` |

**Dialogs block the queue, so read and close them.** Every OpenVisual dialog is a separate top-level
window; while one is up it owns the input. `session status` reports it as `context.openModal`,
`dialog read` inventories its controls, `dialog set-text` fills a field, and `dialog click --button` /
`dialog cancel` dismisses it. Anything that opens one (`node get-properties`, `node double-click`,
`projectInfo get`, `report generate`, `help about`) is only the first step of that sequence.

**Safety note.** Input-synthesizing commands refuse to run unless the app verifiably holds the
foreground (`Code=PreconditionMissing`), because a synthesized key goes to whatever window *is* in
front. `key send` additionally refuses `{F5}` — the controller Send-project gesture — and the one
command that does send it, `controller send`, is caution-gated (`--confirm-caution`). `menu dump-bar`
opens submenus by **hover**, never a click, so enumerating `File` cannot press `Exit`. Irreversible
removal needs `--confirm-destructive`; `node cut` does not, because it stages a move rather than
removing anything.

The removal gate is on the **effect, not the command name**: `node delete`, `product delete`, and any
gesture of `{DELETE}`/`{DEL}` — the app routes `Key.Delete` to the same command — all require it, so
`key send`, ungated by design, is not a way around it. `dialog click --button Yes` stays ungated: it can
answer a confirmation prompt but never raise one, and an undismissable modal blocks the rest of the run.

Each command has a **status**: `confirmed` (verified working), `partial` (mechanism wired, a sub-flow
unverified — usually opens a dialog you then drive/dismiss), or `planned` (declared, not yet wired;
needs `--allow-unverified` to attempt). This is honest about coverage — prefer `confirmed` commands
and check `catalog commands` before relying on a `partial`/`planned` one.

## Node addressing

Selection-relative commands take a **label path** in `--path` (or as the first positional):
slash-separated node names, e.g. `"Localities/Kitchen"`, or 0-based **index** segments, e.g. `"0/2"`
(first root, third child). A literal `/` inside a label is escaped `\/` (link rows are labelled
`"a / b / c"`), and a label matching **more than one sibling** is refused with `Code=TargetAmbiguous`
rather than silently taking the first — address that one by index. `--tree TV1` is the installation pane
(default); `--tree TV2` is the functions pane. The driver expands ancestors as needed and verifies the
caret landed (`Code=TargetNotFound` if not). Two commands take a **second** positional —
`node drag <from> <to>` and `dialog set-text <field> <text>`. Everything else (`--depth`, `--x-offset`,
`--after`, `key send`'s `--path`) is a **named flag only**, so a positional path can never be read as
one. See `references/addressing.md`.

## Output contract

Every command prints one JSON envelope and maps its outcome to an exit code:

```json
{ "ok": true, "code": "Ok", "message": "...", "verified": true,
  "warnings": [], "context": { "appRunning": true, "windowTitle": "...", "selection": {...} },
  "screenshot": null, "data": null }
```

Exit tiers: `0` ok · `1` app-not-running / not-implemented · `2` usage/policy refusal ·
`3` target resolution · `4` runtime/interaction failure. A successful-but-unconfirmed action still
exits `0` — read the `verified` flag (false when the driver could not read the effect back) rather
than the exit code to tell verified from unverified success. `ok` and the exit tier never disagree:
`ok:true` always means tier 0. The `code` field is a fixed machine-readable vocabulary (`Ok`,
`TargetNotFound`, `PreconditionMissing`, `DialogNotFound`, …), and every code the driver can emit is
mapped to a tier — see `references/addressing.md`. Screenshot commands write a PNG to
`%TEMP%\AuiOpenVisualCaptures` and return its path in `screenshot`.

## Coordinates are declared, and are diagnostics

Every coordinate the driver emits says which space it is in and carries its sibling in the other
space. The native space is **`physical`** (measured, not assumed -- UIA rectangles in this host are
device pixels):

```json
"point": {"x":711,"y":512,"space":"physical","logical":{"x":406,"y":293}}
"rect":  {"x":1200,"y":1354,"width":147,"height":54,"space":"physical","logical":{"x":686,"y":774,"width":84,"height":31}}
```

The conversion is `physical = physicalOrigin + Round((logical - logicalOrigin) * scale)` and
`logical = logicalOrigin + Round((physical - physicalOrigin) / scale)`, with `Round` half-away-from-zero
applied to the **offset** from the monitor origin; rectangles convert by **both corners** with the
extent re-derived. `doctor` publishes the monitor it all came from in `data.display`
(`monitor`, `dpi`, `scale`, and the `logical` and `physical` bounds, both measured), so you can apply
the formula by hand and watch the siblings fall out. When the geometry cannot be probed the sibling
is **omitted** rather than faked, and `space` is still emitted.

**Do not feed an exported point back into a gesture.** It is a diagnostic for a mis-aimed click, not
an input: every pointing command is path-addressed, so use `--path` (and `--tree`). Full contract,
including the rounding rule and the lossiness caveat, in `references/addressing.md`.

> **Cross-driver parity with the `ihcvisual` driver is UNVERIFIED.** Both drivers implement this
> contract and are intended to emit identical schemas, but they have never been run side by side in
> one session. Closing that needs the separate elevated plan `tmp/parity_crossdriver_elevated.md`.

Value-level tests: `scripts/aui-coordinate-space.tests.ps1` for the coordinate contract and
`scripts/aui-options.tests.ps1` for the CLI grammar (plain scripts, no Pester, nothing to install —
run each with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <path>`; they exit 0 only when
every case passes and need no running app).

## Extending the vocabulary

The driver is **registry-driven**: `scripts/commands.json` declares each command, and a generic
*mechanism* executes it. **Adding a command that reuses an existing mechanism needs no code — add one
row to `commands.json`.** Mechanisms available:

- `invoke` — invoke a control by `automationId` (toolbar buttons).
- `key` — send a fixed keyboard `gesture` to the window (accelerators, e.g. `^s`, `{F3}`); `keySend`
  sends a caller-supplied `--gesture`.
- `menu` — walk a `menuPath` (`"View/Toolbar"`), opening each container and invoking the leaf through
  the **ExpandCollapse/Invoke patterns** (falling back to a click where a pattern is missing; `data.routes`
  reports which carried each step). Each segment matches an **AutomationId or a label**, and `--id`
  skips the path entirely. `--menu-path` appends for dynamic catalogs. (Alt access-key chords do **not**
  work here — see `references/extending.md`.)
- `menuBarDump` / `contextMenuDump` — enumerate the menu bar (hover-only) or a row's context flyout.
- `contextMenu` — select `--path`, open the row's context flyout, invoke `item`.
- `treeSelect` / `treeDump` / `expandCollapse` (with `action`) / `doubleClick` / `rightClick` /
  `nodeDrag` — tree operations.
- `readProperty` (with `property`) — read a UIA property (e.g. tooltip via `helpText`).
- `fileDialog` (with `dialogKind`) — raise the OS picker, type `--path`, commit, verify by effect.
- `dialogRead` / `dialogSetText` / `dialogButton` / `dialogCancel` — drive the open modal.
- `capture` (with `scope`) — screenshot. `passive`/`static`/`notImplemented` — inspection / stub.

Example new command (a toolbar button that already has an AutomationId):

```json
{ "id": "project.retrieve", "status": "partial", "mutating": "yes", "mechanism": "invoke",
  "automationId": "ToolbarRetrieve", "gates": [], "description": "Retrieve from controller." }
```

A new row starts at `partial`, never `confirmed`: `confirmed` means the effect has been **read back**
from the running app, not that the wiring looks right. (`help for-selection` shipped as `confirmed`
on the strength of being wired and had never been driven at all.)

Genuinely new behavior needs a new `Invoke-Mechanism-*` function in `scripts/aui.ps1` plus a `case`
in its dispatcher — see `references/extending.md`.
