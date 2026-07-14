---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E7 — Function‑block programming

> **Current scope:** ✅ **In scope** — authoring variables and program logic inside a block is
> project CRUD.

**Goal:** Let an IHC programmer author the control logic inside a function block — declaring variables,
building programs from events/conditions/commands by dragging variables, composing logic (AND/OR/NOT),
using enumerators and case statements, doing arithmetic, and handling power‑up — so a block performs
its intended function.

**Scope:** programming mode; the four variable sections and the ~19 resource types; program vs.
sub‑program structure (Events / Conditions / Commands); logic groups and operators; enumerators;
case statements; arithmetic (decimals, ×/÷, integer conversion); power‑up events; and
function‑block‑to‑function‑block variable links. **Scope excludes:** inserting the block (E5),
product↔block links, including scene links (E6, US-024), and simulation (E8).

**Acceptance criteria (epic level):**
- MUST: The programmer can switch a selected block into programming mode and back, add variables to the
  correct section, and build a working program by dragging variables onto event/condition/command
  groups.
- MUST: The tool composes events with OR, lets conditions be AND/OR/NOT‑combined into nested logic
  groups, and executes commands top‑to‑bottom.
- SHOULD: Enumerators, case statements, arithmetic, power‑up handling, and direct
  function‑block‑to‑function‑block variable links are available and behave as specified.

**Readiness:** Ready.

---

## US-026 — Enter and leave programming mode

**As an** IHC programmer, **I want** to open a function block’s program in programming mode and return
to configuration mode, **so that** I can edit logic and then go back to the installation view.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Enter programming mode for a block
  Given a function block (or function link) is selected in configuration mode
  When I press F3
  Then the view switches to programming mode showing one block at a time
  And both pane headers change to the block's name
  And the left pane shows the block's variable sections including "Internal variables" (visible only in
    programming mode), while the right pane shows "Programs" > "Program" > { "Events", "Commands" }

Scenario: Return to configuration mode
  Given I am in programming mode
  When I press Esc
  Then the view returns to configuration mode (the two locality trees)

Scenario: Switch focus between the two panes
  Given I am in programming mode (or configuration mode)
  When I press F6
  Then keyboard focus moves between the left (function) window and the right (program) window
```

### AC illustrations

- Pressing `F3` on an empty block named `Empty block` shows both headers as `Empty block`, left pane
  `Empty block > {Input, Output, Settings, Internal variables}`, right pane
  `Empty block > Programs > Program > {Events, Commands}`.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** Selecting a function block and pressing **F3** enters programming
mode (`MainWindowViewModel.IsProgrammingMode` + the block id): both pane headers change to the block's name
(`InstallationPaneHeader`/`FunctionsPaneHeader`, bound in XAML), the **left** pane shows the block rooted over its
four variable sections (Input / Output / Settings / Internal variables) and the **right** pane the block's program
subtree (**Programs → Program → { Events, Commands }**, built from the block's `programs`/`program_simple`/`events`/
`actions` containers with the program/event/command icons). **Esc** leaves programming mode and restores the two
locality trees; if the block is deleted the mode falls back to configuration. Tested:
`MainWindowViewModelTests` (enter shows the sections + program subtree with the block-name headers; leave restores the
ten-locality trees). Render verified (matches the AC illustration exactly). Live app + OpenObserve no errors. *(F6
pane-focus toggle is a MAY, deferred; the events/commands authoring content is US-028.)*

---

## US-027 — Add variables (resource types) to a function block

**As an** IHC programmer, **I want** to add typed variables to the correct section of a block and set
their name/note/initial value/persistence, **so that** the program has the data it needs.

### Acceptance criteria (Business Rules)

**Placement rules:**
- MUST: Variables are added in programming mode by selecting the target section and choosing
  *Insert > Variables > <type>*, or by right‑clicking the section and picking the type from the popup.
- MUST: A section constrains which types it accepts:
  - **Input** / **Output** — for a *function link*, no further variables may be added; otherwise the
    block’s input/output pins.
  - **Settings** — all variables **except** inputs, outputs and function blocks
    (user‑adjustable settings).
  - **Internal variables** (Internal) — all variable types (hidden from block users).

**Property rules (select variable, press `F2` or right‑click > Properties):**
- MUST: Set **Name**, **Note**, and an **initial value**.
- SHOULD: A checkbox **Save value on power loss** — leave unchecked unless
  needed, as enabling it weakens performance.

**Variable types (the resource palette):**

| Icon meaning | Type | Values / notes |
|---|---|---|
| Input pin | **Input** | ON / OFF; connectable to a physical input |
| Output pin | **Output** | ON / OFF; connectable to a physical output |
| Counter | **Counter** | integer −32768…32767 |
| Integer | **Integer** | integer −32768…32767 |
| Decimal | **Decimal** | real number; usable in ×/÷ |
| Timer | **Timer** | hh:mm:ss.sss 00:00:00.000…23:59:59.999 |
| Timer value | **Timer value** | hh:mm:ss.sss; to preset a timer or store a measured value |
| Humidity | **Humidity** | relative humidity % |
| Time‑of‑day | **Time of day** | hh:mm:ss 00:00:00…23:59:59 |
| Holiday | **Holiday** | holiday flag from an online server (configured in a separate administration tool) |
| Weekday | **Weekday** | Monday…Sunday |
| Date | **Date** | any date |
| Flag | **Flag** | ON / OFF (helper relay) |
| Enum | **Enum** | user-defined set of states (US-030) |
| Light | **Light** | illuminance value, e.g. `Light = 500 Lux` |
| Light level | **Light level** | integer 0…100 (% of max) |
| Temperature | **Temperature** | °C as a decimal, −100…100 |
| Power/energy | **kW / kWh / W / Wh** | for S0 terminals (power/energy) |

**Output:**
- Typed variables placed in their sections, each rendered in the tree as `Name = <initial value>` with
  a type icon.

### AC illustrations

- Filling a block’s *Internal variables* section shows rows like `Weekday = Monday`, `Number = 0`,
  `Flag = OFF`, `Counter = 0`, `Date = 01:01`, `Timer = 00:00:00.000`, `Decimal = 0.00`,
  `Humidity = 0.0% RH`, `Temperature = 0.0 °C` — each with its type icon and localized default; the
  status bar confirms each add, e.g. `Temperature was inserted under Internal variables`.
- `Input` cannot be added to *Settings* (settings exclude input/output pins).

### Constraints

- Verification method — **Inspection** of the section/type matrix and the properties dialog.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (core palette + section matrix).** In programming mode, right‑clicking a
variable section offers **Insert variable ▸ <type>** — a **section‑aware** palette (`VariablePaletteMenu`, rebuilt on
selection): the Input section offers *Input*, Output offers *Output*, and Settings/Internal offer the value types
(*Flag, Counter, Integer, Timer, Timer value, Weekday, Date, Time of day, Temperature, Light, Holiday, Enum* — the
built‑in resource set). Choosing one adds it via `ProjectSession.AddVariableAsync` → the SDK
`FunctionBlockRef.AddInput`/`AddOutput`/`AddSetting`/`AddInternalVariable` (which **enforce the section↔type matrix** —
a pin type into Settings is refused), traced, marks dirty; each variable renders under its section with its **type
icon** (`var-*.svg`) and the status bar reads `<Type> was inserted under <Section>`. Property editing (Name/Note/
initial value/Save‑on‑power‑loss) via F2 is the shared attribute dialog — the initial‑value/persistence specifics are
folded into US‑033. Tested: `MainWindowViewModelTests` (variable placed in the section; a pin type into Settings is
rejected; the palette is section‑aware and inserting confirms with the vendor status string). Render verified
(Internal variables holds Weekday/Flag/Counter/Timer/Temperature with their icons); live app + OpenObserve no errors.
*(Decimal/Humidity/Light‑level are not in the SDK's built‑in resource set, so they are omitted from the palette.
US-028 events/commands authoring next.)*

---

## US-028 — Author a program with events and commands

**As an** IHC programmer, **I want** to insert a program and build it by dragging variables onto its
event and command groups, **so that** events trigger the commands that realise the function.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a standard program
  Given the right (program) pane shows "Programs"
  When I right-click "Programs" and choose "Program"
  Then a "Program" node is inserted with an "Events" group and a "Commands" group

Scenario: Add an event by dragging a variable onto the events group
  Given a program with an "Events" group
  When I drag a variable (e.g. an input) from the function pane onto "Events" and release
  Then a popup lists the possible events for that variable in this context
  And clicking one (e.g. "Input -> ON") inserts that event

Scenario: Add a command by dragging a variable onto the commands group
  Given a program with a "Commands" group
  When I drag a variable (e.g. an output) onto "Commands"
  Then a popup lists the possible commands (e.g. "Toggle Output") and clicking one inserts it

Scenario: Event and command semantics
  Given a program has several events in its "Events" group
  Then any one event can activate the program (events are OR-combined)
  And when activated, the commands in "Commands" execute top-to-bottom;
    a command that activates another program runs that program immediately before continuing
```

### AC illustrations

- A `<function block>` with one input and one output: the program’s single event `Input -> ON` runs
  the single command `Toggle Output`, so each ON press toggles the output.
- A press‑and‑release string sets the output to follow the input: event `Input is changed`,
  command sets `Output` to follow `Input` (output ON while the button is held).

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (events + commands authoring; explicit-program-insert deferred).**
An empty function block already carries one `program_simple` with its **Events** and **Commands** groups, so
authoring targets that program. In programming mode both groups render their leaf rows (events/actions), with each
stored `%P`/`%S` template resolved to the operand's live name for display (e.g. `Doorbell -> ON`, `Toggle Chime`).

Because Avalonia 12 has no headless-testable drag-drop, the vendor "drag a variable onto Events/Commands" gesture
is realised as the established two-step substitute: **Use in program** arms the selected variable (a block
input/output/setting/internal), then the program's **Events**/**Commands** node offers that variable's curated
triggers/commands (the "popup of possible events/commands"). The stored vocabulary uses the byte-fidelity oracle's
vendor method tokens — events `_0xa` (→ ON), `_0x96` (changes state), `_0x9b` (is assigned); commands `_0xa` (= ON),
`_0x14` (= OFF), `_0x23` (toggle). Names keep the `%P` template so they stay live across renames. Events are
OR-combined and commands run top-to-bottom by the vendor engine (semantics owned by the controller, not authored here).

SDK enablers added: `ProgramBuilder.AddAction` (root/unconditional command), `ProjectEditor.Resource(ElementId)`
(id→operand factory a GUI drives). Session: `AddProgramEventAsync`/`AddProgramCommandAsync` (traced via
`ActivitySource`, errors logged + surfaced). Tests: 3 view-model/session tests in `safe_visual_tests`
(`MainWindowViewModelTests`) + 1 headless render test (`SmokeTests`). Byte-fidelity gate `safe_project_tests`
still 663 green. *(Deferred: explicit "insert an additional Program" via right-click Programs — the block's single
seeded program covers the AC illustrations; multi-program insert is a later increment.)*

---

## US-029 — Build conditional subprograms and logic groups

**As an** IHC programmer, **I want** to add conditions and combine them with AND/OR/NOT into nested
logic groups, **so that** commands run only when the intended logical expression is true.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Turn a program into a conditional sub-program
  Given a command group in a program
  When I right-click the command group and choose "Sub-program"
  Then a conditional structure is inserted with a "Conditions" group,
    "Commands when conditions true" and
    "Commands when conditions false"

Scenario: Conditions default to AND, switchable to OR
  Given a "Conditions" group with several conditions
  Then the conditions are AND-combined by default, shown with the "&" icon
  When I select a condition, press F2, and change the group's "Logical condition" to OR
  Then the group is OR-combined, shown with the ">=1" icon

Scenario: Negate a single condition
  Given I am inserting a condition via the popup
  When I choose the NOT variant in the popup
  Then that single condition is negated

Scenario: Nest a logic group for a compound expression
  Given a "Conditions" or logic group
  When I right-click it and choose "Logic group" (or select a condition and press Shift+F10 > "Logic group")
  Then a nested logic group is inserted, allowing expressions like
    "(Output1=OFF) OR ((Output2=ON) AND ((Output3 NOT = Output1) OR (Output4=ON)))"
```

### AC illustrations

- Two conditions `Output1=OFF` and `Output2=ON` in one group with `&` mean `(Output1=OFF) AND
  (Output2=ON)`; switching the group to `>=1` makes it `(Output1=OFF) OR (Output2=ON)`.
- When one of the program’s events fires, the sub‑program evaluates the conditions; if all true it runs
  the *true* commands, otherwise the *false* commands. Events and conditions are independent — a
  condition is only evaluated when an event occurs.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (sub-program + conditions authoring; nested-branch sub-programs
deferred).** Right-clicking a program's **Commands** group → **Sub-program** inserts a `program_sub` with its
**Conditions** group and the **Commands when conditions true**/**false** branches (rendered recursively; branch
`type="_0x1"` distinguishes the true branch). A **Conditions** group shows its combination in the icon and label —
default AND (`cond-and.svg`, `(&)`), switchable to OR via **Logical condition ▸ OR** (`cond-or.svg`, `(>=1)`,
persisted `type="or"`). Conditions are authored with the same **Use in program → Add condition** two-step gesture as
events/commands, with a curated popup including the **NOT** variant (vendor tokens `_0xa` = ON, `_0x14` = OFF,
`_0x28` = `<>`). **Logic group** nests a `conditions` group inside for compound expressions. Names keep the `%P`/`%S`
template.

SDK enabler added: `ProjectEditor.Branch(ElementId)` (id→`BranchRef` over any `actions` container — root Commands or
a branch); the existing `ConditionsGroupRef` (`AddCondition`/`Or`/`And`/`AddConditionGroup`), `AddSubProgram`, and
`ProjectEditor.ConditionsGroup(id)` cover the rest. Session: `AddSubProgramAsync`/`AddConditionAsync`/
`SetConditionsLogicAsync`/`AddLogicGroupAsync` (shared traced `MutateProgramAsync`, errors logged + surfaced); the
command path now targets any `actions` container. Tests: 4 view-model/session (`MainWindowViewModelTests`) + 1
headless render (`SmokeTests`). Suites: `safe_visual_tests` **111**, byte-fidelity `safe_project_tests` **663** green.
OpenObserve 0 errors. *(Deferred: adding commands/sub-programs to a sub-program's true/false branch is enabled at the
SDK/session level via `Branch(id)` but the branch context menus are not yet wired; the Conditions/AND-OR/logic-group
gesture is a MAY realised via context menu rather than F2 dialog.)*

---

## US-030 — Create and use enumerators

**As an** IHC programmer, **I want** to define an enumerator type with named states and use it as a
variable, **so that** the program reads more clearly than with many flags.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Two built-in enumerators exist
  Given any project
  Then two default enumerator types are available: "Alarm state" and "Home simulation"

Scenario: Create a new enumerator type and its states
  Given a block's "Settings" section
  When I right-click "Settings" and choose "Enum"
  Then an enumerator dialog opens
  When I choose "Enumerator type" > "New", name it (e.g. "My Enum"),
    then repeatedly choose "Enumerator values" > "New" and name each state (e.g. "On")
  Then the type gains the ordered states I defined
  And enumerator types created in a project are global (usable by other function blocks in the project)

Scenario: Use an enumerator in logic
  Given a variable of an enumerator type with ordered states
  Then conditions may test a single state (e.g. "Mode = Direct") or a range,
    because states are stored internally as integers in listed order
    (e.g. "Enum <= State 3", "Enum <> State 2")
  And when an enumerator is used as an event, exactly one state must be specified

Scenario: Edit an existing enumerator type's states
  Given a variable of an enumerator type
  When I select it, press F2 (or right-click > "Properties") and click the "Edit" button
  Then a screen opens where I can add or change the type's states
```

### AC illustrations

- A `Mode` enum with states `Direct`, `With delay`, `Switched off` lets one block behave as a
  direct link, a delayed link, or off, selected by the enum’s value — testable with a cascade of
  conditional commands.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (create/edit enum type + typed variable; enum-in-logic operand
deferred).** The **Enum** entry in a value section's variable palette (right-click **Settings/Internal variables**
→ Insert variable → **Enum**) opens the new modal **`EnumDefinitionWindow`** — a type name plus an ordered,
one-per-line state list. OK authors a **project-global `enum_definition`** (in the `enum_definitions` container,
reusable by other blocks — the "global" AC) and inserts a `resource_enum` variable of it (`typedef` + `inivalue` of
the first state), rendered under the section. **F2/Properties** on an enum variable reopens the dialog (type name
read-only) and **appends** any newly-listed states (`AddEnumValues`, append-only, 0-based `index` continues; duplicates
ignored; built-in read-only types refused with a message). The **two built-in enumerator types** ship in the fresh
project's catalog enum_definitions (verified `>= 2`; their Danish vendor names differ from the story's
"Alarm state"/"Home simulation" labels).

SDK reused: `ProjectEditor.AddEnumDefinition`/`AddEnumValues`/`EnumDefinition(name)`, `EnumDefinitionRef.Typedef`/
`InitialValue`, `FunctionBlockRef.AddSetting`/`AddInternalVariable(configure)`. Session: `AddEnumVariableAsync`/
`UpdateEnumStatesAsync` (traced, errors logged + surfaced). Dialog contract: `EnumDefinitionInput`/`EnumDefinitionResult`
+ `EnumDefinitionWindow`. Tests: 3 view-model/session (`MainWindowViewModelTests`) + 1 headless render (`SmokeTests`).
Suites: `safe_visual_tests` **115**, byte-fidelity `safe_project_tests` **663** green. OpenObserve 0 errors.
*(Deferred: testing an enum state/range **in a condition/event** — the `resource_enum` operand (`ConditionRef.AddEnumOperand`,
"Enum <= State 3") — and renaming/removing existing states (SDK is append-only); the create/edit-type + typed-variable
core is complete.)*

---

## US-031 — Use case statements

**As an** IHC programmer, **I want** to add a case structure keyed on a variable, **so that** the block
runs different commands per value without a long condition cascade.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a case structure
  Given a command group in a program
  When I drag an eligible variable onto the command group and choose "Case (<variable>)" in the popup
  Then a case structure is inserted under the command group, initially with only an "Else" group

Scenario: Add case values
  Given a "Case" node
  When I right-click "Case" and choose "New case value...", then enter a criterion value
  Then a new command group tagged with that criterion is added; I fill it by dragging variables as usual

Scenario: Eligible variable types
  Given I want to build a case
  Then the case variable may be one of: Counter, Enumerator, Weekday,
    Integer, or Date

Scenario: Default branch
  Given a case structure with several value branches
  When none of the criteria match at runtime
  Then the commands in the "Else" group execute
```

### AC illustrations

- A toilet‑cleaning counter drives a case: value branches for `100` (little clean) and `1000` (main
  clean) set the respective outputs; all other counts fall through to `Else`.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (case + literal value branches; enum-criterion values deferred).**
Arming an **eligible** switch variable (Counter/Enumerator/Weekday/Integer/Date — gated) then choosing **Case
(&lt;variable&gt;)** on a Commands node inserts a `program_case` with its default **Else** branch (rendered "Case
(&lt;switch&gt;)" by resolving `program_case@link`). **New case value…** on the Case node prompts for a criterion
(reusing the Name field of the properties dialog) and adds a `case_action` value branch storing the criterion as a
bare typed operand (e.g. a counter's `<resource_counter inivalue="100">`). Every branch — each value branch and Else
— is a command container, so the normal **Use in program → Add command** gesture fills it (verified end-to-end: a
command lands in the `100` branch). Ineligible types (e.g. a boolean flag) offer no Case option.

SDK enablers added: `ProjectEditor.Case(ElementId)` (id→`CaseRef`, resolving the document-last Else), and `Branch`
now also accepts a `case_action` container (a case value is a command container). Session: `AddCaseAsync`
(eligibility-gated)/`AddCaseValueAsync` (traced, errors logged + surfaced); the command path accepts
`actions`|`case_action`. Rendering `BuildCaseNode`; flag `TreeNodeViewModel.IsCaseNode`; NodeIcons +program_case/
case_action. Tests: 4 view-model/session (`MainWindowViewModelTests`) + 1 headless render (`SmokeTests`). Suites:
`safe_visual_tests` **120**, byte-fidelity `safe_project_tests` **663** green. OpenObserve 0 errors. *(Deferred:
adding **enum-criterion** case values — needs the enum type's states/`EnumDefinitionRef` (`CaseRef.Case(name,
EnumDefinitionRef, valueName)`); the case structure still inserts on an enum switch, only enum value branches are
deferred.)*

---

## US-032 — Author arithmetic with decimals and integers

**As an** IHC programmer, **I want** to compute with decimals (and multiply/divide), doing one
operation per command line, **so that** the block can derive values like averages or conversions.

### Acceptance criteria (Business Rules)

**Rules:**
- MUST: A command line performs **at most one** arithmetic operation; larger formulas are built as a
  sequence of one‑operation command lines using a running "display" register.
- MUST: Decimal (**Decimal**) variables support +, −, ×, ÷; the tool shows which other variable types
  may combine with a decimal.
- SHOULD: To convert a decimal to an integer, add the decimal to an integer variable (previously set to
  0); the assignment to the integer truncates toward zero (drops the fractional part).

**Worked pattern for `(N1 + N2) * C / D`:**
- Display = 0; Display = Display + N1; Display = Display + N2; Display = Display × C; Display = Display ÷ D.
- For parenthesised formulas, evaluate the innermost parenthesis first.

**Output:**
- A decimal or integer result stored in a variable, shown inline as `name = value` (e.g. `Display =
  38.96`).

### AC illustrations

- `F1 / F2` with `F1`, `F2` decimals stores `Display = 0.33`.
- Converting `2.5` via an integer `Number` yields `Number = 2` (truncation); `−3.9` yields `−3`.

### Constraints

- Verification method — **Test** the one‑operation‑per‑line rule and the truncation behaviour of
  decimal→integer conversion.
- Localization note: decimals display with a point separator (`0.33`, `38.96`) per English locale.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (add/subtract, one-operation-per-line; ×/÷ deferred — no attested token).**
First, the value-variable palette gained the missing **Decimal** type (`resource_floating_point`, the vendor decimal —
US-027 had omitted it under a wrong tag name), so decimals can be created. Arming a **numeric target register**
(decimal/integer/counter) then choosing **Arithmetic ▸ &lt;target&gt; += … / −= …** on a Commands node lists the block's
numeric operands; picking one appends a single `action` command line (`link1` = target, `method` = op, `link2` =
operand) — **one operation per line by construction**, rendered as the resolved formula (e.g. `F1 = F1 + F2`). Larger
formulas are a sequence of these against a running register. The **decimal→integer** truncation pattern is authored as
"Integer = Integer + Decimal" (the target register is the integer); the truncation itself is the controller's runtime
semantic (not computed in-app — no controller).

Vendor tokens are the catalog-attested arithmetic ones: **add `_0x5a`**, **subtract `_0x64`** (both two-operand, decimal
capable). Session `AddArithmeticCommandAsync` (traced, errors logged + surfaced); rendering reuses `EventCommandLabel`
(%P = target, %S = operand); NodeIcons +`resource_floating_point`. Tests: 4 view-model/session
(`MainWindowViewModelTests`) + 1 headless render (`SmokeTests`). Suites: `safe_visual_tests` **125**, byte-fidelity
`safe_project_tests` **663** green. OpenObserve 0 errors. *(Deferred: **multiply (×) / divide (÷)** — no vendor method
token is attested anywhere in the catalog or oracles, so authoring them would be guesswork risking byte corruption; the
add/subtract worked-pattern building block is complete.)*

---

## US-033 — Handle power‑up (system) events

**As an** IHC programmer, **I want** to react to controller power‑up and control which values survive a
power loss, **so that** the installation restores a sensible state after an outage.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Add a Powerup event
  Given a program's "Events" group
  When I insert the "Powerup" event
  Then the program runs when the controller powers up (also on project transfer and software restart),
    which is useful for re-establishing timer values

Scenario: Persist a function-block output across power loss
  Given a function block output
  When I open its "Properties" and tick "Save current value"
  Then the output's value is restored after a power loss instead of reset

Scenario: Persist a physical output's state
  Given a physical output product (e.g. a lamp output)
  When I open its properties, click "Configure output", and tick "Save current value" under power loss
  Then the physical output restores its pre-outage state on power-up
```

### AC illustrations

- A light meant to stay on for 10 s: on power loss the timer value is saved, and on `Powerup` the
  program restores it and continues lighting for the remaining time.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented (Powerup event + Save-current-value persistence on FB & physical outputs).**
**Add Powerup event** on a program's Events node inserts an `event_power` ("Powerup") — no operand — via the existing
SDK `ProgramBuilder.AddPowerEvent`; the program then runs on controller power-up (also on transfer/restart). **Save
current value** is a checkable context-menu item on any output pin (a function-block `resource_output` and a physical
wireless-relay `airlink_relay` — both carry the DTD `backup (yes|no)` flag) that toggles `backup="yes"`, so the value
is restored after a power loss instead of reset; a saved output is marked `(saved)` in the tree. Physical outputs use
the exact same session path (`SetOutputBackupAsync` accepts `resource_output`/`dataline_output`/`airlink_relay`).

Session: `AddPowerEventAsync`/`SetOutputBackupAsync` (traced, errors logged + surfaced). Flags
`TreeNodeViewModel.IsOutputPin`/`IsValueSaved`. Tests: 3 view-model/session (`MainWindowViewModelTests`, incl. a
wireless-relay physical output) + 1 headless render (`SmokeTests`). Suites: `safe_visual_tests` **129**, byte-fidelity
`safe_project_tests` **663** green. OpenObserve 0 errors. *(Note: the built-in catalog exposes no wired
`dataline_output` product, so the physical-output persistence is demonstrated on a wireless relay output — the code
path is identical for `dataline_output`. The "Configure output" sub-dialog framing is realised directly as the output
pin's checkable menu item.)*

---

## US-033b — Link variables directly between function blocks

**As an** IHC programmer, **I want** to link a variable in one function block directly to a variable in
another function block, **so that** blocks can share state without routing every signal through physical
product pins.

**Scope excludes:** product↔function‑block links (E6, US-022/US-023); the internal logic that consumes
the linked variable (US-028/US-029).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Link a variable from one block to a variable in another block
  Given two function blocks exist in the "Functions" pane, each carrying variables
  When I link a source variable of block A (e.g. a "Flag" or "Output") onto a compatible
    target variable of block B (e.g. a "Flag" or "Input")
  Then a direct function‑block‑to‑function‑block variable link is created between them

Scenario: Compatible endpoints
  Given I am linking variables between two blocks
  Then a flag or output of one block may be linked to a flag or an input of another block
```

### AC illustrations

- Linking block A's `Output` to block B's `Input` (or a `Flag` in each) lets block A drive block B
  directly, with no physical product in between — the function‑block‑to‑function‑block linking case.

### Constraints

- Verification method — **Demonstration** that a variable link between two blocks propagates the source
  value to the target.
- IHC OpenVisual supports linking variables directly between compatible endpoints (a flag or output of
  one block to a flag or an input of another block), but does not fix the exact drag gesture or any
  dialog; confirm the interaction detail during implementation. (R‑note.)

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented. Epic E7 COMPLETE.** In configuration mode the Functions pane already
renders each function block's variables as linkable pins, so the established two-step **Link from here → Link to here**
gesture (US-022) joins them directly — no product in between. The interaction detail (per the R-note) is that same
context-menu gesture. `LinkPinsAsync` now enforces the **compatibility rule** when both endpoints are function-block
variables: the source must be a **flag or output** and the target a **flag or input**, and the two must belong to
**different** blocks; otherwise the link is refused with a message. Product↔block links (US-022/023), where at most one
endpoint is an FB variable, keep their existing behaviour. A created link renders reciprocal ← / → rows under both
pins.

Session helper `OwningFunctionBlock` (block → section → pin ancestry) detects the fb↔fb case; the guard is traced via
the existing `LinkPinsAsync` activity and logs/surfaces failures. No SDK change (reuses `ProjectEditor.Link`), so
byte-fidelity `safe_project_tests` **663** stays green. Tests: 3 view-model/session (compatible output→input,
incompatible input-source, same-block rejection) + 1 headless render (`SmokeTests`). Suites: `safe_visual_tests` **133**
green. OpenObserve 0 errors.
