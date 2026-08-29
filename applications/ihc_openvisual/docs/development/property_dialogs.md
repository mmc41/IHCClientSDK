# How a tree node opens its dialog

Developer reference (HOW) for the route from a gesture on a tree row to a modal properties dialog and back to an
applied edit. The behavioural spec (WHAT) is in [`../stories/`](../stories/); the layering invariants this route
obeys are in [`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) and [ADR-002](../../../../docs/adr/).

Symbols are named rather than line-numbered, because line numbers drift and member names do not.

## The governing idea

**A tree row does not decide which dialog opens — the SDK element does.** The row carries an `ElementId` and a
structural `Kind`; the `Kind` gates *menus*. Which dialog appears, what it contains, and what it writes are all
answered from the element behind that id, read through typed SDK views. That is what keeps the shell thin: a new
node type is a new branch over SDK facts, not a new tree-row flavour.

## The chain at a glance

```
gesture on a row
   │
   ├─ double-click ── Views/MainWindow.axaml (TreeNodeTemplate, DoubleTapped)
   │                  → Views/MainWindow.axaml.cs  OnNodeDoubleTapped
   │                  → MainWindowViewModel.ActivateNodeCommand → ActivateNodeAsync
   │
   ├─ F2 / right-click "Egenskaber…" ── CommandSpec("node.properties") → Properties → OpenPropertiesAsync
   │
   └─ just-placed product ── InsertProductAsync → PropertiesDialogCoordinator.OpenForInsertAsync
                              │
                              ▼
              PropertiesDialogCoordinator.OpenAsync(ElementId)      ← the one dispatch
                              │  project.FindById(id) → branch on the ELEMENT
                              ▼
                   IDialogService.Edit*Async(...)                   ← the seam; no Avalonia above this
                              │
                   AvaloniaDialogService → WithOwnerAsync(owner => XxxWindow.ShowAsync(owner, input))
                              │
                   ResultDialog<TResult>.ShowDialogForResult        ← modal; OK → Accept(value), Cancel → null
                              │
                              ▼
              applyAndReport(session.Commands.Xxx(...), "<Danish status>")
```

## 1. Identify — the row carries an id, not a dialog choice

`ViewModels/ProjectTreeProjector.cs` builds every `TreeNodeViewModel` from an SDK `ProjectElement`, stamping the
element's `ElementId` and a single `TreeNodeKind`. In `ViewModels/TreeNodeViewModel.cs` the kind flags
(`IsPin`, `IsLocality`, `IsLinkRow`, `IsBlockSection`, `IsSceneTarget`, …) are **computed** from that one `Kind`, so
the projector classifies a row once instead of setting a string plus a set of booleans. State that is not a kind —
linked, locked, output, backup — stays stored on the row.

A single click only selects. `SelectedInstallationNode` / `SelectedFunctionsNode` are two-way bound per pane and
funnel into the shared `SelectedNode`, whose change handler calls `RebuildContext()` and rebuilds the
selection-dependent menus. No dialog is involved.

## 2. Trigger — three routes, and two of them are not the same route

### Double-click

`DoubleTapped` is handled on the item template's own `StackPanel` in `Views/MainWindow.axaml`, **not** on the
`TreeView`. The event only bubbles, and `TreeViewItem` toggles expansion from a handler on its header presenter —
that panel's ancestor — so the row content is the last point that still runs before the toggle.

`OnNodeDoubleTapped` in `Views/MainWindow.axaml.cs` selects the node under the pointer, executes
`ActivateNodeCommand`, and sets `e.Handled = true` for *every* node kind, including the ones that open nothing.
Marking it handled everywhere is what suppresses the expand/collapse default, matching the reference application.
Suppressing from `PointerPressed` does not work: Avalonia synthesises `DoubleTapped` from the pointer stream
regardless of whether the pointer event was handled.

### F2 and the context menu

`MainWindowViewModel.RegisterNodeRows` registers `CommandSpec("node.properties", "F2", …)` on both the menu bar and
the context menu. Its gate allows any node with an id that is not the localities root; its `SurfacePolicy` hides the
row on a **link row in the context menu only** — the menu bar keeps it. The context flyout itself is swapped per row
by `ApplyContextFlyout`: a block section gets `SectionContextMenu`, every other node the shared `NodeContextMenu`.
The swap is applied to the *tree's* `ContextFlyout`, not the row's, so both flyouts keep the `MainWindowViewModel`
as their `DataContext`.

A surface that addresses a specific row passes it as the `ICommand` parameter; the `CommandRegistry`
command-parameter bridge selects that node first, so the context the row's `Execute` reads *is* that node.

### Activate ≠ Properties

`ActivateNodeAsync` is deliberately not `PropertiesCommand`. Exactly two cells differ:

| Node | Double-click (`ActivateNode`) | F2 / Egenskaber… (`node.properties`) |
| --- | --- | --- |
| Data-line pin | opens its **parent product's** dialog (`IsPinOfProduct` walks up) | opens the pin addressing dialog |
| Localities root, link row | opens nothing | root refused by the gate; link row keeps the menu-bar item |
| everything else | identical | identical |

The pin redirect exists because the reference application has no per-pin dialog at all — a terminal is configured
from inside the product dialog. Only a pin redirects; a scenes container is a product child too, but it has its own
dialog.

### Insert

Placing a product raises the same dialog as part of placing it. `InsertProductAsync` applies the `AddProduct`
command *without announcing it*, then calls `OpenForInsertAsync`. Cancel rolls the insert back — a snapshot
rollback, not `Undo`, so no id is burned and no redo entry is left. The success line is written only after the
dialog commits. `OpenForInsertAsync` returns whether the installer **accepted** the dialog, never whether the edit
changed anything: OK without touching a field is an ordinary commit.

## 3. Dispatch — one ladder, over SDK facts

`ViewModels/PropertiesDialogCoordinator.cs` `OpenAsync(ElementId)` resolves `project.FindById(id)` and branches on
the element:

| Element fact | Flow | Dialog |
| --- | --- | --- |
| `ProductClassifier.IsProduct(element.Tag)` | `OpenComposedDialogAsync` | `ProductDialogWindow` |
| `ElementKind.DatalinePin` | `OpenPinAsync` | `PinPropertiesWindow` |
| `IsScenesContainer` | `OpenSceneContainerAsync` | `SceneContainerWindow` |
| `IsSceneMember && !IsSceneShutter` | `OpenSceneValueAsync` | `SceneValueWindow` |
| `ElementKind.EnumResource` | `OpenVariableAsync` | `VariablePropertiesWindow` |
| `ElementKind.Resource` | `OpenVariableAsync` | `VariablePropertiesWindow` |
| `Tag == "conditions"` | `OpenConditionsAsync` | `PropertiesWindow` (+ AND/OR field) |
| `IsLocalityGroup` or `ElementKind.FunctionBlock` | `OpenNameNoteAsync` | `PropertiesWindow` |

Every product family — wired, wireless, modem, LED dimmer, S0 — takes the first row. There is no per-family branch
left to get wrong.

There is a **second entry point** into the same flows. `ExecuteAsync(DialogHop)` opens a dialog for a route that has
already been decided — a *Problemer* row activated in the panel — rather than for a node the installer clicked:

| Entry point | Given | Decides |
| --- | --- | --- |
| `OpenAsync(ElementId)` | a tree node | which dialog, from the element, by the ladder above |
| `ExecuteAsync(DialogHop)` | a finished route | nothing — it carries out the plan it is handed |

The asymmetry is the point. A plan already names the owner, the site and the field, and re-deriving any of them here
would give the row's promise and the click's destination two authors. `ExecuteAsync` translates the hop into a
`ProductDialogShowOptions` and opens the owner's dialog; every non-product owner falls back to the ladder, because
landing *in* a dialog rather than merely opening it needs that family's focus keys, and those arrive family by
family.

Each flow reads the element through a typed SDK read view (`ProductView`, `PinView`, `FunctionBlockView`,
`ElementView`) rather than raw schema attributes, builds the dialog's input record, awaits the dialog, and turns the
result into a `ProjectCommand`. Dialog-shape decisions that are per node type — a function block's fixed
`Funktionsblok egenskaber` title and captioned user group versus a locality's `Rediger <navn> egenskaber`, a
library block's read-only provenance group, whether a time editor shows milliseconds — are decided here, from the
element, and passed in as input.

## 4. Create and show — the view-model never touches `Window`

View-models call `IDialogService.Edit*Async` (`Services/IDialogService.cs`), which trades in plain input/result
records (`PropertiesResult`, `VariablePropertiesInput`, `ProductDialogEdits`, …). This is the seam that keeps
view-models free of Avalonia types; the headless suites substitute `NullDialogService` (`Services/NullServices.cs`).

`Services/AvaloniaDialogService.cs` implements each member as a one-liner:

```csharp
public Task<PropertiesResult?> EditPropertiesAsync(string title, string name, string note, ...) =>
    WithOwnerAsync(owner => PropertiesWindow.ShowAsync(owner, title, name, note, ...));
```

`WithOwnerAsync` is the single "there is no owner window yet" guard — a headless or design-time instance has no
`Owner`, and showing a modal without one throws. Having it in one place means a newly added dialog inherits the
guard instead of having to remember it.

Every editor window derives from `ResultDialog<TResult>` (`Views/ResultDialog.cs`), which owns the whole contract:

- a static `ShowAsync(owner, …)` constructs the window, calls `Populate(…)`, `FocusOnOpen(NameBox)` where a
  pre-filled name should be selected and overtypable, then awaits `ShowDialogForResult(owner)`;
- the OK handler calls `Accept(value)`, which records the result and closes;
- `OnCancel` and the title-bar close leave the result null — **null is the cancellation signal** everywhere above;
- `Populate` and the internal `AcceptedResult` are the seams the headless view tests drive, since
  `ShowDialogForResult` needs a modal loop they do not run.

Code-built confirm/message boxes take a different path — `AvaloniaDialogService.ShowButtonsAsync` builds them from
controls and resolves a title-bar close to the **last** (safe) button. They share one `AutomationId` because they
share one shape.

## 5. The composed product dialog — one window for every family

This is the only dialog whose *content* is data rather than XAML.

`OpenComposedDialogAsync` asks the SDK for a `ProductDialogDescriptor`:

```
ProjectWorkflow.GetProductDialog → ProjectAppService.GetProductDialog → ProductDialogComposer
```

The descriptor (`ihcclient/src/vis/products/ProductDialogDescriptor.cs`) is an immutable snapshot with no project or
element reference: a title, groups, and fields where each field already carries the `ElementId` **and** attribute it
writes, its current effective value, its control kind, its rule and numeric bounds, and a stable automation id
`dlg.<group>.<field>`. Nothing is left for the renderer or the write-back to work out, which is what lets both be
family-agnostic.

Rows that are element **data** rather than dialog metadata — the terminal grid rows and the `Indstillinger` settings
rows — are read by the coordinator and passed alongside. The descriptor says *whether* a grid appears; the
coordinator says what goes in it. Setting values are rendered through the app's own `VariableValueFormat`, not
printed raw, because how a stored value is displayed is a presentation concern on this side of the boundary.

`ViewModels/ProductDialogViewModel.cs` turns the descriptor into groups of `ProductDialogFieldViewModel`, and
`Controls/DialogFieldTemplate.cs` selects one `DataTemplate` per `DialogControlKind` (the templates themselves are
authored in `ProductDialogWindow.axaml`). It is a template *selector* rather than several editors toggled with
`IsVisible`, because hiding is not the same as not building: hidden-but-focusable duplicates give one automation id
several elements and make a screen reader walk phantom boxes. A kind with no template throws
`UnknownControlKindException` — fail loud, because rendering a caption with no editor is a dialog silently missing a
value.

The flow is a **loop**, not a single call. A widget action returned by the window — *Konfigurer* / double-click on a
terminal row, or *Avanceret* on a dimmer — commits the documentation already typed, opens the sub-dialog over that
committed state, and re-opens the product dialog. A value that breaks its rule keeps the window open with the
refusal stated, and that gate covers the widget routes too, so an invalid value cannot leave by the side door.

Adding a product family is adding a preset, not a window. `tests/safe_architecture_tests/DialogMetadataIsolationArchitectureTests.cs`
pins the other half of that: nothing that produces bytes may read the dialog-metadata layer, so adding a preset
cannot change a saved file.

## 5a. Boundary — what the SDK knows about a dialog, and what it does not

The descriptor puts dialog metadata **inside the SDK**, which raises a fair question: does `ihcclient` now know about
presentation details that would differ in another frontend? Partly yes, deliberately, and it is worth knowing exactly
which parts, because the answer is not uniform.

The line is ADR-002's: the SDK owns raw values, legality, selection and mutation; *display* interpretation is
frontend-owned — a future frontend re-implements display conventions by design, never legality. Sorted against it:

| Descriptor member | Which side |
| --- | --- |
| Which fields are offered, and in what order | **Domain.** The DTD says which attributes *exist*; the dialog says which the installer is *offered*, and those are different sets ([`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) §7a) |
| `Caption`, `TitleSuffix` (Danish) | **Domain.** The SDK already owns user-facing Danish text — the same rule that makes a refusal Danish in the SDK |
| `Target` + `Attribute`, `Rule`, `Minimum`/`Maximum`, `ReadOnly` | **Domain.** Legality and where a value lives |
| `AutomationId` (`dlg.<group>.<field>`) | Borderline. A stable per-field key is useful to any frontend; only the name says UI |
| `DialogControlKind` | **Presentation, mild.** An abstract widget vocabulary — toolkit-agnostic, but a UI concept |
| `Columns`, `ColumnMajor`, `ColumnSpan` | **Presentation.** Pure layout |
| `DialogWidgetKind` | **Presentation, most specific.** Names composites drawn in one frontend's window |
| `Collapsible` | **Presentation.** Whether a group is drawn behind a disclosure; the fields compose and commit either way |
| `DisplayDivisor` | **Domain.** It is the relationship between the stored unit and the captioned one, and the write-back is its exact inverse — a frontend that re-decided it would store a different number |

Two GUI-side types deliberately stay out of the SDK altogether. `PinDialogField` is a **dialog-local** vocabulary,
so no control identity ever enters an SDK contract; and `NavigationKind`/`DialogHop` are the host's route model,
built from facts the SDK already exposes. The SDK's `dlg.*` ids are the tolerated maximum in the other direction:
they are consumed, not extended.

What the SDK does **not** know: no toolkit type, no size, font, colour, theme, gesture, focus order or window class.
The app also keeps display interpretation on its own side even where the descriptor could have carried it — the
`Indstillinger` values are rendered through `VariableValueFormat` here rather than shipped pre-formatted, because how
a stored value reads is presentation policy.

**Why the presentation rows are tolerated.** This app's goal is vendor parity, so a dialog's shape is *measured
product behaviour*, not a design decision. While each family was a hand-written window, the differences lived in
markup where nothing could compare them against the recorded oracle. `ColumnMajor` is the sharpest example: it was
measured both ways across families — the modem's telephone group reads down, the S0's reads across — so one global
renderer choice fixes one family and breaks another.

**What contains the coupling.** The dialog metadata lives in one named layer (`Ihc.Vis.Products`), reached by the
composer, `ProductDialogCommands` and the `ProjectAppService` door — nothing else in the SDK consumes it. It is never
serialized, and `DialogMetadataIsolationArchitectureTests` structurally forbids either byte writer from depending on
it, so what a dialog offers can never influence what a file serializes to. (One inward reference is not dialog
metadata at all: validation's declarative phone-number rule delegates to `DialogValueRule`, the shared operative pair
that the dialog and the commit path both consult.) **A frontend that wants none of this simply never calls
`GetProductDialog`** and drives `ProjectAppService` / `ProjectCommands` directly; it loses the composed form and
nothing else.

**The cost that would actually bite a second frontend.** Consuming a descriptor transfers cleanly — captions, values,
targets and rules are frontend-neutral, the layout members are declared hints that never change field order, and the
widget kinds say *whether* a composite applies, leaving the frontend to draw its own or omit it. The friction runs the
other way: a frontend needing a control the vocabulary does not have must **grow an SDK enum**, and by this
codebase's own rule every admission to that closed vocabulary requires a measurement against the vendor oracle. That
gate is a parity gate, which a non-parity frontend has no way to satisfy. Anyone adding a second frontend should
decide that question before binding to the descriptor rather than after.

## 5b. Focus — landing in a control, not merely in a window

Three seams, in order of how specific they are.

| Seam | Signature | Whose vocabulary |
| --- | --- | --- |
| `ResultDialog<T>.FocusOnOpen(Control)` | any control; a `TextBox` is additionally selected | the base dialog's |
| `PinPropertiesInput.Focus` | `PinDialogField?` — `Address`, `CableColour`, `Note`, `InitialValue`, `Backup` | the dialog's own |
| `ProductDialogShowOptions.FocusAutomationId` | a `dlg.*` id from the composed descriptor | the descriptor's |

A hand-written dialog gets a **dialog-local enum**, and the window maps a key to its own control by compiled
`x:Name` reference — so a rename is a compile error rather than a focus that silently lands nowhere. The
coordinator is the single place the two vocabularies meet (`PinFieldFor`): the SDK above it knows attribute names,
the window below it knows controls, and neither acquires the other's. An attribute the dialog does not render
answers `null` rather than a guess at a nearby field.

The composed dialog needs no enum, because its fields already carry stable ids. `FocusField` focuses the named id
and scrolls it into view; an id the dialog does not contain focuses **nothing**, which is the same honest answer the
route planner gives when it degrades such a route to dialog-level.

Two absences are deliberate. A control that is **hidden** is not focused — an input pin has no initial-value
control, and a route asking for one must land nowhere rather than on something invisible. A control that is
**read-only** cannot take focus at all, which is why the planner never promises a field for a read-only attribute.

## 5c. Stepping into a composite — the dialog stays open

`ProductDialogStep` is the seam: the product dialog calls it and **stays open**, so a sub-dialog appears over a
parent that is still there.

```
ProductDialogWindow.Step(action)
  → TryCommit()                      refuses an invalid value, exactly as OK does
  → await step(action)               the coordinator opens the sub-dialog OVER this window
  → viewModel.Refresh(...)           re-projected from the caller's answer, never from what is on screen
```

It replaced a protocol that CLOSED the window and let the caller re-open it. That was visible — the installer saw
the dialog vanish and come back — and lossy: closing destroyed the window, so anything it held that had not reached
the document was gone. The old path survives only for a composite this seam has not taken over, and a window with no
handler still uses it.

**The refresh re-projects from the authoritative state**, which is the visit's pending values laid over the
document. Neither source alone is right mid-visit: the document has not been told about a terminal addressed inside
the visit, and the dialog's own rendered rows are a *rendering* — deriving values back out of them is the point at
which a formatting change silently becomes a data change.

**The visit is the transaction.** A sub-dialog's OK joins a pending overlay; nothing reaches the document until the
product dialog's own OK, which commits the whole visit through one command and so one undo entry; Annuller discards
all of it. The same sub-dialog opened directly from the tree has no visit to belong to and commits straight
through — same window, two commit semantics, stated here because the difference is invisible from inside it.

## 5d. Owner — a nested dialog parents on the dialog that raised it

`AvaloniaDialogService.Innermost(shell)` walks Avalonia's own window-ownership chain to the innermost visible
window, and every modal is shown over that rather than over the shell.

It is **read**, not maintained as a parallel stack of our own. A hand-kept stack has to be popped on every exit
route a dialog has — OK, Cancel, Esc, the title-bar X, a throw on the way out — and one missed pop leaves every
later modal parented on a window that has closed. The chain cannot drift, because it is the fact the window manager
is already keeping.

A sub-dialog parented on the shell would not be modal to the dialog that raised it: the installer could reach
behind it and edit the very values it was opened to change, or close its parent out from under it. The code-built
message boxes go through the same resolution, though they build their window inline rather than through the shared
owner guard.

## 6. Result to command

Every flow ends the same way — the coordinator's injected `applyAndReport`:

```csharp
await applyAndReport(session.Commands.RenameLocality(project, id, result.Name, result.Note),
    $"Omdøbt til {result.Name}.");
```

No flow writes attributes directly; the SDK owns command execution. Two conventions worth keeping:

- **A separate attribute is a separate command, applied only when it actually changed** — so an untouched field
  leaves no second entry in the undo history (the conditions operator and the variable power-loss flag both do this).
- **Status text is written when it is true.** The insert path applies the command before the dialog so the dialog can
  be built from the placed element, but announces only after the installer commits.

## 7. The envelope

All three routes run inside `MainWindowViewModel.RunAsync(operation, action)`, which opens an `Activity` on the app's
`ActivitySource` and catches everything. An exception message is an English developer diagnostic naming element tags
and `_0x` ids, so it goes to the log; the installer gets one fixed Danish sentence plus the host problem box. This is
the widest instance of that channel and the one place that cannot name what failed.

## 8. Adding a dialog for a new node type

1. Add the branch to `PropertiesDialogCoordinator.OpenAsync`, over an SDK element fact — a `Kind`, a classifier, a
   tag — never over a `TreeNodeViewModel` flag.
2. Read the element through a typed SDK view. If the view lacks what the dialog needs, extend the view; do not read
   schema attributes here.
3. Declare the input/result records in `Services/IDialogService.cs` and add the `Edit…Async` member.
4. Implement it in `AvaloniaDialogService` as a single `WithOwnerAsync(owner => XxxWindow.ShowAsync(owner, input))`,
   and in `NullDialogService`.
5. Derive the window from `ResultDialog<TResult>`; expose `ShowAsync` + an internal `Populate` so headless view tests
   can drive it without a modal loop. Cancel must leave the result null.
6. Apply the result through `session.Commands.…` and `applyAndReport`. Never mutate the project from the window.
7. If the node should also respond to double-click, check `ActivateNodeAsync` — the default is that it does, so only
   a deliberate exception (redirect, or open nothing) needs code there.
8. Stamp automation ids on the new controls; `tests/safe_visual_tests/AutomationCoverageTests.cs` and the
   `aui-openvisual` driver address the dialog by them.

For a new **product family**, none of the above applies: add a preset to the composer and the generic dialog picks it
up. Add a `DialogControlKind` only when no existing editor fits, and give it a template in `ProductDialogWindow.axaml`
plus an arm in `DialogFieldTemplate.ForKind`.

## 9. Where this is tested

| Layer | Suite |
| --- | --- |
| Dispatch, activate route, insert/rollback | `tests/safe_visual_tests/MainWindowViewModelTests.cs` |
| Window shape, fields, titles, parity | `tests/safe_visual_tests/` — `ProductDialogWindowTests`, `DialogTitleParityTests`, `VariableDialogFieldParityTests`, `ConditionsPropertiesParityTests`, `FunctionBlockPropertiesParityTests`, `EnumVariablePropertiesParityTests`, `InsertProductDialogParityTests`, `DialogFillDirectionTests`, `DialogBoundsFromMetadataTests` |
| Descriptor composition and write-back | `tests/safe_project_tests/products/` — `ProductDialogComposerTests`, `ApplyProductDialogTests`, `ProductDialogPresetTests`, `ProductDialogCatalogSweepTests`, `DialogValueRuleTests` |
| Dialog metadata cannot reach the writers | `tests/safe_architecture_tests/DialogMetadataIsolationArchitectureTests.cs` |

## Related

- [`ARCHITECTURE.md`](../../../../ARCHITECTURE.md) — the thin-shell invariants this route implements
- [`../stories/INDEX.md`](../stories/INDEX.md) — the behaviour (US-007, US-011, US-012, US-013, US-027, US-044, …)
- [`../icon_codes.md`](../icon_codes.md) — how a row picks its icon, the other half of projection
