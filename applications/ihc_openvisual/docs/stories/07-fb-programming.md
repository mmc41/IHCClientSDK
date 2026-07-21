---
version: 0.3.1
last-updated: 2026-07-21
status: draft
---

# E7 — Function-block programming

**Goal:** Let an IHC programmer author the control logic inside a function block — declaring variables,
building programs from events/conditions/commands by dragging variables, composing logic (AND/OR/NOT),
using enumerators and case statements, doing arithmetic, and handling power-up — so a block performs
its intended function.

**Scope:** programming mode; the four variable sections and the ~19 resource types; program vs.
sub-program structure (Events / Conditions / Commands); logic groups and operators; enumerators;
case statements; arithmetic (decimals, ×/÷, integer conversion); power-up events; and
function-block-to-function-block variable links. **Scope excludes:** inserting the block (E5),
product↔block links, including scene links (E6, US-024), and simulation (E8).

**Acceptance criteria (epic level):**
- MUST: The programmer can switch a selected block into programming mode and back, add variables to the
  correct section, and build a working program by dragging variables onto event/condition/command
  groups.
- MUST: The tool composes events with OR, lets conditions be AND/OR/NOT-combined into nested logic
  groups, and executes commands top-to-bottom.
- SHOULD: Enumerators, case statements, arithmetic, power-up handling, and direct
  function-block-to-function-block variable links are available and behave as specified.

**Readiness:** Ready.

---

## US-026 — Enter and leave programming mode

**As an** IHC programmer, **I want** to open a function block's program in programming mode and return
to configuration mode, **so that** I can edit logic and then go back to the installation view.

### Acceptance criteria (Given-When-Then)

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

### Business rules (what a mode transition changes)

- MUST: Entering programming mode **re-roots both panes to the function block's own name** — the left pane
  to its variable sections (*Input* / *Output* / *Settings* / *Internal variables*), the right pane to
  *Programs*.
- MUST: Leaving programming mode re-roots **both** panes back to *Localities*.
- MUST: The pane roots are what tell the two modes apart — configuration mode roots at *Localities*,
  programming mode roots at the block's name. (This is the only reliable signal of which mode the view is
  in; IHC OpenVisual additionally reports the transition in the status bar.)
- MUST: **`Internal variables` is a programming-mode section.** It appears when the block's program is open
  and **not** in the configuration view (E5, US-018) — internals are the block author's business, not the
  installer's.
- MUST: **Programming mode on a *locked* (library) block is view-only.** Its program **renders for reading**
  — the lock never gates viewing — but **every authoring command is gated on the block being unlocked**:
  variable / program / enum inserts, `Ctrl+I` / `Ctrl+U` pin inserts, **and the mutations *Delete* and
  *Move up/down***, are **removed (not greyed)** on a locked block. *Properties* stays — it is offered
  on every node. Unlocking (US-020) is the separate, deliberate act that enables editing. This must be
  enforced both by removing the commands (UI) and by an **engine guard**, so a locked block keeps matching
  its master whoever drives the editor.

### AC illustrations

- Pressing `F3` on an empty block named `Empty block` shows both headers as `Empty block`, left pane
  `Empty block > {Input, Output, Settings, Internal variables}`, right pane
  `Empty block > Programs > Program > {Events, Commands}`.

### Constraints

- Verification method — **Demonstration** that entering re-roots both panes to the block name and leaving
  restores both to *Localities*.
- Commands route to the **selected element**, not to whichever pane holds keyboard focus.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — the mode transition works, `Internal variables` is correctly
hidden in configuration mode, and the locked-block view-only **UI** gate withdraws the insert/delete/move
commands. ⚠ The **engine-level** view-only guard is incomplete — a non-UI edit (or the still-ungated AND/OR
toggle, save-current-value, log-mark, or *Properties*-driven rename/enum-edit) can still mutate a `locked` block.

---

## US-027 — Add variables (resource types) to a function block

**As an** IHC programmer, **I want** to add typed variables to the correct section of a block and set
their name/note/initial value/persistence, **so that** the program has the data it needs.

### Acceptance criteria (Business Rules)

**Placement rules:**
- MUST: Variables are added in programming mode by selecting the target section and choosing
  *Insert > Variables > <type>*, or by right-clicking the section and picking the type from the popup.
  Two types sit **off** the flat variable bar: **`Input`** is inserted from the section's context menu or
  via **`Ctrl+I`**, and **`Enum`** from the section context menu's **`Enum` submenu** (then pick an
  enumerator type, US-030).
- MUST: A section constrains which types it accepts:
  - **Input** / **Output** — for a *function link*, no further variables may be added; otherwise the
    block's input/output pins.
  - **Settings** — all variables **except** inputs, outputs and function blocks (user-adjustable settings).
  - **Internal variables** (Internal) — all variable types (hidden from block users).

**Property rules (select variable, press `F2` or right-click > Properties):**
- MUST: Set **Name**, **Note**, and an **initial value**.
- SHOULD: A checkbox **Save value on power loss** — leave unchecked unless needed, as enabling it weakens
  performance.

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
| Time-of-day | **Time of day** | hh:mm:ss 00:00:00…23:59:59 |
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

- Filling a block's *Internal variables* section shows rows like `Weekday = Monday`, `Number = 0`,
  `Flag = OFF`, `Counter = 0`, `Date = 01:01`, `Timer = 00:00:00.000`, `Decimal = 0.00`,
  `Humidity = 0.0% RH`, `Temperature = 0.0 °C` — each with its type icon and localized default; the
  status bar confirms each add, e.g. `Temperature was inserted under Internal variables`.
- `Input` cannot be added to *Settings* (settings exclude input/output pins).

### Constraints

- Verification method — **Inspection** of the section/type matrix and the properties dialog.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — the wired variable palette and section matrix work. ⚠
Editing a generic variable's Name/Note/initial value via *Properties* has no dialog route (the item is offered
but no-ops); adding an enum variable always creates a new type instead of offering existing ones; and the
kW/kWh/W/Wh power/energy types are suppressed from the palette pending a decision.

---

## US-028 — Author a program with events and commands

**As an** IHC programmer, **I want** to insert a program and build it by dragging variables onto its
event and command groups, **so that** events trigger the commands that realise the function.

### Acceptance criteria (Given-When-Then)

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

### Business rules (the operator vocabulary)

- MUST: **The target group decides the row family; the dragged pin's type decides the operator list.**
  Dropping a pin on an **Events** group raises the event popup, on a **Commands** group the command popup, on
  a **Conditions** group the condition popup (US-029) — one drag gesture, three families. There is **no
  separate "add event" verb**: an event is authored by dropping a pin on the Events group like any other row.
- MUST: The operator each popup offers is a function of the pin's type. The per-type lists are the authoring
  vocabulary IHC OpenVisual reproduces:

  | Pin type → target group | Operators offered |
  |---|---|
  | **bool input → Events** | `→ ON` · `→ OFF` · `→ <pin>` · `NOT → <pin>` · `is changed` · `is written` |
  | **bool output → Commands** | `= ON` · `= OFF` · `= <pin>` · `= NOT` · **`Toggle`** *(bool-output only)* |
  | **bool → Conditions** | `= ON` · `= OFF` · `= <pin>` · `NOT =` |
  | **analog (humidity, …) → Events** | `is changed` · `is written` |
  | **weekday → Events** | `System weekday → <pin>` · `is changed` · `is written` |
  | **timer → Commands** | `= 0` · `= initial value` · `= <pin>` · `= Timer +` · `= Timer −` · `Activate count-down … with initial value` · `Activate count-up` · `Activate count-down` · `Stop counting` |

- MUST: A **two-operand** operator (`→ <pin>`, `= <pin>`, `NOT → <pin>`, `NOT =`) takes a **second pin**.
  IHC OpenVisual lets the author **pick both ends** of such a row (US-029) — it must not silently auto-bind
  the second operand.

### AC illustrations

- A `<function block>` with one input and one output: the program's single event `Input -> ON` runs
  the single command `Toggle Output`, so each ON press toggles the output.
- A press-and-release string sets the output to follow the input: event `Input is changed`,
  command sets `Output` to follow `Input` (output ON while the button is held).

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — bool event and command authoring works. ⚠ The operator list
is keyed by category, not by the pin's type: the bool lists are incomplete, the `NOT`-condition is a unary
`%P <> ON` rather than the two-operand `NOT =`, the analog/weekday/timer operator sets are absent, `Toggle` is
offered on any variable (not only bool outputs), two-operand comparison rows can't be authored, and a pin can't
be dragged onto a **Conditions** group (only Events/Commands).

---

## US-029 — Build conditional subprograms and logic groups

**As an** IHC programmer, **I want** to add conditions and combine them with AND/OR/NOT into nested
logic groups, **so that** commands run only when the intended logical expression is true.

### Acceptance criteria (Given-When-Then)

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

### Business rules (tree label)

- MUST: A conditional-command node (`program_sub`, *"Betinget kommando"*) that carries a user-set **`name`**
  renders that name as its tree label — e.g. `Kip udgang`, `Tænd`, `Sluk` — falling back to the default
  *Sub-program* token (in English) **only when `name` is absent or default**. A fixed *Sub-program* for every
  one would discard the name and collapse distinct sub-programs to indistinguishable rows.
- MUST: Inserting a sub-program appends the four-node skeleton the first scenario describes — *Sub-program →
  { Conditions, Commands when conditions true, Commands when conditions false }*.

### AC illustrations

- Two conditions `Output1=OFF` and `Output2=ON` in one group with `&` mean `(Output1=OFF) AND
  (Output2=ON)`; switching the group to `>=1` makes it `(Output1=OFF) OR (Output2=ON)`.
- When one of the program's events fires, the sub-program evaluates the conditions; if all true it runs
  the *true* commands, otherwise the *false* commands. Events and conditions are independent — a
  condition is only evaluated when an event occurs.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — sub-program + conditions authoring with AND/OR logic groups, and the
user-set sub-program **name** renders as the tree label (falling back to the default *Sub-program* token only
when the name is absent or still the default).

---

## US-030 — Create and use enumerators

**As an** IHC programmer, **I want** to define an enumerator type with named states and use it as a
variable, **so that** the program reads more clearly than with many flags.

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Two built-in enumerators exist
  Given any project
  Then two read-only built-in enumerator types are always present: "Persienne tilstand"
    (blind state, 5 ordered values) and "Logning" (logging, 6 ordered values)
  And both remain listed whether or not any variable references them

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

### Business rules

- MUST: The two built-in enumerator types are `Persienne tilstand` (5 ordered values) and `Logning`
  (6 ordered values). These names are **project data, rendered verbatim** (not translated), and both types
  are **always present** whether or not any variable references them — so a `Logning` type with zero
  references is the built-in, never deleted-resource residue.
- **Known gap:** a **standalone / empty** custom enumerator *type* (0 states, referenced by no variable) is
  not currently authorable — enumerator types are created only while adding an enum variable to a *Settings*
  section, and there is no bare-enum-type route.

### AC illustrations

- A `Mode` enum with states `Direct`, `With delay`, `Switched off` lets one block behave as a
  direct link, a delayed link, or off, selected by the enum's value — testable with a cascade of
  conditional commands.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — creating a new enum type + typed variable, and the two
built-ins, work. ⚠ Adding an enum variable always creates a new type instead of offering existing ones; editing
is append-only — an existing state's label can't be *changed*; and a standalone/empty enum type has no
authoring route (the Known gap above).

---

## US-031 — Use case statements

**As an** IHC programmer, **I want** to add a case structure keyed on a variable, **so that** the block
runs different commands per value without a long condition cascade.

### Acceptance criteria (Given-When-Then)

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

- A toilet-cleaning counter drives a case: value branches for `100` (little clean) and `1000` (main
  clean) set the respective outputs; all other counts fall through to `Else`.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented — case insert and literal value branches work. ⚠ On an
enum-keyed case, adding a value no-ops — the enum-criterion branch path is unreachable from the app.

---

## US-032 — Author arithmetic with decimals and integers

**As an** IHC programmer, **I want** to compute with decimals (and multiply/divide), doing one
operation per command line, **so that** the block can derive values like averages or conversions.

### Acceptance criteria (Business Rules)

**Rules:**
- MUST: A command line performs **at most one** arithmetic operation; larger formulas are built as a
  sequence of one-operation command lines using a running "display" register.
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

- Verification method — **Test** the one-operation-per-line rule and the truncation behaviour of
  decimal→integer conversion.
- Decimals display with a point separator (`0.33`, `38.96`) per English locale.

**Readiness:** Ready.

**Implementation status:** 🟡 Partly implemented (add/subtract, one-operation-per-line; ×/÷ pending).

---

## US-033 — Handle power-up (system) events

**As an** IHC programmer, **I want** to react to controller power-up and control which values survive a
power loss, **so that** the installation restores a sensible state after an outage.

### Acceptance criteria (Given-When-Then)

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

### Business rules

- MUST: *Powerup* is inserted as a **menu** event — *Insert ▸ Special ▸ Powerup event* — not by a drag onto
  the Events group, and it carries **no operand or link** (it triggers on the block, unconditionally).

### AC illustrations

- A light meant to stay on for 10 s: on power loss the timer value is saved, and on `Powerup` the
  program restores it and continues lighting for the remaining time.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (Powerup event + Save-current-value persistence on FB & physical outputs).

---

## US-033b — Link variables directly between function blocks

**As an** IHC programmer, **I want** to link a variable in one function block directly to a variable in the
same or another function block, **so that** blocks can share state — or a block can feed its own output back
to its own input — without routing every signal through physical product pins.

**Scope excludes:** product↔function-block links (E6, US-022/US-023); the internal logic that consumes
the linked variable (US-028/US-029).

### Acceptance criteria (Given-When-Then)

```gherkin
Scenario: Link a variable from one block to a variable in another block
  Given two function blocks exist in the "Functions" pane, each carrying variables
  When I link a source variable of block A (e.g. a "Flag" or "Output") onto a compatible
    target variable of block B (e.g. a "Flag" or "Input")
  Then a direct function-block-to-function-block variable link is created between them

Scenario: Link a block's output back to its own input (self-link)
  Given a function block whose output and input are both shown in the "Functions" pane
  When I link the block's own output onto its own input (a feedback pattern)
  Then the self-link is created — the same block at both ends is allowed

Scenario: Compatible endpoints
  Given I am linking variables within the same block or between two blocks
  Then a flag or output may be linked to a flag or an input of the same or another block

Scenario: An incompatible pair is refused
  Given I link a source variable onto a target the rule does not allow (e.g. an output onto an output)
  Then no link is created and the app tells me the link is incompatible
```

### Business rules (legality)

- MUST: A block-to-block variable link is legal when the **source** is an output or a flag and the
  **target** is an input or a flag — **including a block's output to its own input** (a legitimate feedback
  pattern). Anything else is refused and explained.
- MUST: This is the **same predicate** US-022 applies to the other link families — one rule, three families,
  not three parallel rules. US-022 owns the statement of it; this story is the block↔block case of it.

### AC illustrations

- Linking block A's `Output` to block B's `Input` (or a `Flag` in each) lets block A drive block B
  directly, with no physical product in between — the function-block-to-function-block linking case.
- Dragging block A's `Output` onto block B's `Output` is refused with an *Incompatible link* message —
  **both are "outputs", and that is not what makes it illegal**: the rule is about which end produces a
  signal and which consumes one (US-022).

### Constraints

- Verification method — **Demonstration** that a variable link between two blocks propagates the source
  value to the target, and **Test** of the refusals (the legality matrix lives in US-022).
- MUST: Linking variables between compatible endpoints is done **by dragging one pin onto another**
  (US-022's gesture and legality rule apply); the two-step *Link from here* /
  *Link to here* is the non-drag **supplement**.
- A block's output feeding **its own** input is **allowed**; do not re-tighten to a different-blocks-only
  rule by symmetry.
- The *Incompatible link* message is a **deliberate feature** — IHC OpenVisual explains the refusal rather than failing silently. It stays; only
  its ergonomics are in scope (letting `Esc` dismiss it and focusing the safe button, applied across every
  modal — US-069).

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — the data-flow legality rule works, **including** the self-link
case: a block's output → its own input is allowed (a legitimate feedback pattern).
