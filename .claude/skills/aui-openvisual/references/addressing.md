# Addressing & result contract

## Node addressing (label path)

Selection-relative commands (`tree select`, `node *`, `locality insert`, …) locate a node with a
**path** passed as `--path` or the first positional argument:

- **Label path** — slash-separated node names, matched case-**in**sensitively against the visible tree
  labels: `"Localities/Kitchen"`, `"Localities/Living room/PIR sensor"`. A trailing space in a label is
  tolerated (real labels here end in one, and a shell drops it from the last segment but not the middle).
- **Index path** — 0-based child indices: `"0"` = first root, `"0/2"` = third child of the first root.
  Segments can be mixed with labels: `"0/Kitchen"`.

A literal `/` **inside** a label is written `\/`: `"Stue/Lampe \/ Pin 1 \/ Udgang"`. Link rows are
labelled with the opposite end's full path joined by ` / `, so without the escape that entire node kind
was reachable only by index. Same grammar as `--menu-path`, which needs it for catalog leaves like
`"Lux \/ Temperatur sensor med logning"`.

**A label that matches more than one sibling is refused**, with `Code=TargetAmbiguous` (tier 3) naming
the indices that matched. Duplicate sibling labels are ordinary here — two products of the same type
under one locality, two link rows onto the same target — and picking the first would let a *mutating*
command act on the wrong row and report success, which nothing in the envelope could reveal. Address the
one you mean by index: the message tells you which.

`--tree` chooses the pane:

| Selector | Pane | AutomationId |
|----------|------|--------------|
| `TV1` (default) | Installation (localities + products) | `InstallationTree` |
| `TV2` | Functions (function blocks + programs) | `FunctionsTree` |

The driver expands each ancestor on the way down (via the ExpandCollapse pattern) and, after
selecting the leaf, **reads the caret back to verify** it landed — returning `Code=TargetNotFound`
if the path did not resolve. A raw AutomationId can also be passed to `--tree` for future panes.

Readers (`tree dump --path`, `node tooltip`) **resolve** the row without selecting it, so inspecting
the tree never moves the caret out from under a measurement. `menu dump-context` is the exception and
must select + focus its target, because Shift+F10 acts on whatever holds keyboard focus.

### Positionals vs named flags

The first positional is the `--path` (or, for `key send`, the `--gesture`). Exactly two commands take
a **second** positional: `node drag <from> <to>` and `dialog set-text <field> <text>`. Every other
option is **named-only** and can never absorb a positional — `--depth` (`tree dump`, `menu dump-bar`),
`--after` (`tree dump`), `--x-offset` (`node double-click`), and `key send`'s optional `--path`.

`--x-offset N` clicks N px right of the row's **label** instead of on it (census C16). The
hit-test-back guard still applies, so an offset that leaves the row or the viewport is refused rather
than clamped, and `data.hitArea` echoes which point ran.

A non-numeric value for a numeric flag is `Code=InvalidInput` (tier 2), never an unhandled error.
Grammar tests: `scripts/aui-options.tests.ps1`.

## Result envelope

Every command prints one line of JSON:

| Field | Meaning |
|-------|---------|
| `ok` | Boolean success. |
| `code` | Machine-readable outcome (table below). |
| `message` | Human-readable summary. |
| `verified` | True when the driver confirmed the effect (e.g. read the caret / expand state back). |
| `warnings` | Array of non-fatal notes. |
| `context` | Live snapshot — see below. |
| `screenshot` | For `capture.*`: `{path,width,height,space,scope,mimeType}` (PNG on disk). Else null. |
| `data` | Command-specific payload (e.g. `{state:"Expanded"}`, `catalog commands` rows, `node tooltip` value). |

### `context`

| Field | Meaning |
|-------|---------|
| `appRunning` | False (and everything else null) when no app process was found. |
| `windowTitle` | The main window's title. Dialogs are separate windows and never reported here. |
| `toolbarVisible` / `statusBarVisible` | Whether each bar is currently shown. |
| `statusText` | **The last-action hint, and it NAMES the node acted on** ("Cut X", "Copied X", "Moved."). The only readback that proves a selection-relative gesture hit the node it was *addressed* with rather than the one that happened to be selected — and the only observable effect `node cut`/`copy` and `link.*` have at all. |
| `openModal` | `{title, id}` of the open dialog, or null. Identified by window handle, so a dialog whose caption ends with the product name (`Om IHC OpenVisual`) is still reported. **Branch on `id`, not `title`**: `id` is the window's AutomationId (`AboutWindow`, `ReportPickerWindow`, `ConfirmDialog` for the code-built message boxes) and does not move, while the title is Danish and several dialogs retitle themselves from project data. An empty `id` means the window declares none — never "no dialog", which is `null`. |
| `focusedPane` | `TV1` \| `TV2` \| `Other` \| `None` \| `Unknown` — keyboard **focus**, which is not selection. F6's whole effect is to move this and nothing else. |
| `selection` | The first pane that has one, `{tree,name}`. Kept for compatibility. |
| `selections` | **Every** pane's selection. Prefer this: reporting only the first hides cross-pane effects (a jump that moves the *opposite* pane's caret reads as "nothing happened" through `selection`). |

## Code vocabulary → exit tier

Every code the driver can emit is mapped. Codes marked ᴿ are **reserved**: part of the shared
vocabulary the vendor-side `ihcvisual` driver also speaks, kept in step so one harness reader parses
both, but not emitted here today.

| Exit | Codes |
|------|-------|
| 0 | `Ok` |
| 1 | `AppNotRunning`, `NotImplemented`, `OkUnverified`ᴿ |
| 2 | `PlatformUnsupported`, `NotAllowed`, `ConfirmationRequired`, `Unverified`, `PreconditionMissing`, `InvalidInput`, `BadScope`ᴿ |
| 3 | `TargetNotFound`, `TargetExists`, `ControlNotFound`, `TargetAmbiguous`, `DiscoveryFailed`ᴿ |
| 4 | `DialogNotFound`, `DialogTimeout`, `DialogError`, `DialogBlocked`, `NoEffect`, `MutationFailed`, `CaptureFailed`, `CaptureOccluded`, `PostFailed`ᴿ |

`ok` and the exit tier never disagree: `ok:true` is always tier 0, so an unverified-but-successful
action carries `Code=Ok` with `verified:false` — read `verified`, not the exit code, to tell those
apart. `DialogBlocked` means a modal or picker is in the way and the command did **not** complete
(commonly the unsaved-changes guard in front of `project open`).

**One envelope, always.** Bootstrap — reading the registry, loading the UI-Automation assemblies,
resolving the main window — runs inside the same guard as the command itself, so a corrupt
`commands.json` or a host missing the UIA assemblies answers in JSON rather than as a PowerShell error
record with nothing on stdout.

### How `project open` / `project save-as` verify

The picker closing proves nothing, so both check the effect. `data.verifiedBy` says which check carried it:

| Value | Meaning |
|-------|---------|
| `titleBaseName` | The title bar starts with the target's **base name** — an exact, literal comparison (`[` and `]` are legal in a filename and would be wildcard syntax to a pattern match). |
| `fileOnDisk` | Save only, and only for a **rooted** `--path`: the file is really there afterwards. |

`titleBaseName` alone is the weaker signal — the caption carries no directory, so it cannot tell "loaded
the file I asked for" from "a file of that name was already open". A rooted save upgrades to `fileOnDisk`;
for `project open`, the leftover-modal check (`DialogBlocked`) is what catches the common version of that
trap, an unsaved-changes guard swallowing the command while the caption already read right.

## Coordinate space contract

Every coordinate this driver emits is **declared**: it states which space it is in and carries its
sibling in the other space. On a 100% display the two spaces are identical and none of this shows;
on a scaled display (this machine runs 175%) a number re-used in the wrong space lands about 1.75x
away, and the failure is the dangerous kind -- not an error, but a confident wrong answer. A hover
over empty canvas reports "no tooltip"; a drag between two empty points reports "the drop was
refused". Both read as findings about the application.

### The declared shapes

A point (`node.rightClick`, `node.doubleClick` -> `data.point`):

```json
{"x":711,"y":512,"space":"physical","logical":{"x":406,"y":293}}
```

A rectangle (`dialog read` -> every control's `rect`):

```json
{"x":1200,"y":1354,"width":147,"height":54,"space":"physical","logical":{"x":686,"y":774,"width":84,"height":31}}
```

- `x`/`y` (and `width`/`height`) are exactly what the driver read, unconverted.
- `space` is this driver's native space. It is **`physical`**, because UIA rectangles in this host are
  physical pixels -- measured, not assumed: the window's UIA extent matches its true device-pixel
  frame bounds to within 0.3%, where the virtualized alternative would have been 75% smaller, and a
  point derived from a UIA rect and set with `SetCursorPos` reads back unchanged from a
  per-monitor-aware thread.
- The sibling (`logical`) is a **plain** point/rect with no nested `space` tag: stating the same fact
  twice gives it two places to disagree.
- When the monitor geometry cannot be probed, the **sibling is omitted and `space` is still emitted**.
  A sibling computed from an assumed scale of 1.0 would be exactly the confident wrong answer this
  contract exists to remove.

Screenshot metadata (`screenshot`) carries `space` but **no sibling**: `width`/`height` are the PNG's
real pixel count, so a converted pair would describe a file that does not exist at that size.

`node.drag` exports no point at all (it reports `from`/`to` label paths), and is not meant to gain one.

### The conversion

Conversion is a pure function of `(point, monitorLogicalOrigin, monitorPhysicalOrigin, scale)`:

```
physical = physicalOrigin + Round((logical  - logicalOrigin ) * scale)
logical  = logicalOrigin  + Round((physical - physicalOrigin) / scale)
```

- Both axes convert independently, and `scale` is `dpi / 96.0` as a double.
- `Round` is half-**away-from-zero**, applied to the **offset from the origin** and never to the
  absolute coordinate -- so behaviour is symmetric about the monitor origin and unaffected by a
  negative or non-zero one. A monitor placed left of the primary has negative coordinates, and
  rounding the absolute value there bends the result toward the desktop origin.
- **Rectangles convert by BOTH CORNERS, with the extent re-derived** from the converted corners.
  Scaling `width`/`height` in isolation rounds a second time, independently of where the rectangle
  sits: at scale 1.75 a 3 px extent at x=101 is 1 px logical (58 -> 59), while the isolated form
  claims 2. That drifts only on rectangles whose corners do not happen to align, which is the worst
  possible failure distribution -- right often enough to be trusted.
- The conversion is **lossy** physical -> logical at non-integer scales. Round-tripping is not exact,
  and nothing in the driver or its tests asserts that it is.

### `doctor`'s display block

`doctor` publishes the monitor the app is on, which is what makes a coordinate answer checkable --
apply the formula by hand to these numbers and the siblings fall out:

```json
"display": { "monitor": "\\\\.\\DISPLAY1", "dpi": 168,
             "logical":  { "x": 0, "y": 0, "width": 2194, "height": 1234 },
             "physical": { "x": 0, "y": 0, "width": 3840, "height": 2160 }, "scale": 1.75 }
```

Both rectangles are **measured**, each read under its own DPI-awareness context, and neither is
derived from the other: Windows virtualizes per monitor, so 2194 * 1.75 = 3839.5, not 3840, and a
derived bound would be wrong by construction. The block is additive -- it never participates in
`ready` -- and is `null` when the geometry cannot be probed, for the same reason a sibling is omitted.

It is also what makes this whole bug class discoverable: an author working on a 100% display reads
`scale: 1` here and learns why their machine never reproduces it.

### The exported point is a diagnostic, not an input

`data.point` records where the driver actually clicked, so a mis-aimed gesture stays readable after
the fact. Do not hand it to `SetCursorPos` or do coordinate math on it -- every pointing command here
is path-addressed, so address the node with `--path` (and `--tree`) instead. The point being declared
is what makes a wrong landing diagnosable; it is not an invitation to re-use the number.

### Cross-driver parity with the `ihcvisual` driver is UNVERIFIED

The `ihcvisual` driver for the vendor's IHC Visual app implements the same contract, and the two are
intended to emit byte-identical schemas so one harness reader can parse both. **That agreement has
not been observed:** the two drivers have never been run side by side in one session, so
**cross-driver parity is UNVERIFIED**. Verifying it needs a single session running BOTH apps
(IHC Visual elevated, OpenVisual at matching integrity), which is the separate plan
`tmp/parity_crossdriver_elevated.md`. Until that plan runs, treat agreement between the two drivers
as an intention, not a measured fact.

### Tests

`scripts/aui-coordinate-space.tests.ps1` -- a plain self-testing script (not Pester, so there is still
nothing to install). Run it with
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\aui-coordinate-space.tests.ps1`;
it prints one line per case and exits 0 only when every case passed.

## Gates

Destructive/irreversible commands refuse unless explicitly confirmed:

- `--confirm-destructive` — required by `node delete` and `product delete` (gate `confirmDestructive`),
  and by **any command sending `{DELETE}`/`{DEL}`**, including the otherwise-ungated `key send`. The gate
  means **irreversible removal**, and the app routes `Key.Delete` to the same `edit.delete` command the
  named ones use, so a raw gesture was the same removal with the gate taken off. `node cut` used to carry
  the gate and no longer does: cut removes nothing (it stages a move that `node paste` completes), so it
  sat on the harmless half of the pair while `node paste` itself was ungated.

  `dialog click --button Yes` stays ungated on purpose. It can *answer* a confirmation prompt but never
  raise one, and a modal a caller cannot dismiss blocks every later command in the run.
- `--confirm-caution` — required by caution-gated commands (gate `confirmCaution`): `controller send`,
  `controller retrieve`, `enum create-type`. Controller traffic is gated wherever it appears, so the
  `{F5}` refusal in `key send` cannot be walked around through a registry row.
- `--allow-unverified` — required to attempt a `status=planned` command. Note that `partial` commands
  run freely, so a gate is the only thing standing in front of a dangerous `partial` row.

## Deterministic launch

The app comes up on the standard empty project with no start-up prompt of its own, so a launch is
already deterministic. A stray modal from any source can be cleared with `aui dialog cancel`.
