---
version: 0.1.0
last-updated: 2026-07-13
status: out-of-scope
---

# E8 — Simulation & debugging

> **Current scope:** ⛔ **Out of scope** — the offline simulation engine is not yet specified in enough
> detail to build faithfully; it also only *validates* a project rather than doing CRUD on its content.
> Kept as documentation, not slated for implementation.

> **Implementation status:** ⛔ Out of scope.

**Goal:** Let a commissioning technician or programmer validate the project offline — driving inputs
and outputs, watching red/green states, setting breakpoints, stepping, simulating clock/date, and
logging — so faults are found on the PC before deployment.

**Scope:** offline simulation in both the configuration and programming views; input/output actuation
(*follow* vs *toggle*); breakpoints and step execution; the simulation time/date dialog; and the
simulation log. **Scope excludes:** online/live simulation (out of scope for this app) and
controller transfer (E10).

**Acceptance criteria (epic level):**
- MUST: The technician can start and stop offline simulation; while simulating, inputs/outputs are
  coloured by state and editing/configuring is disabled.
- MUST: Inputs and outputs can be driven with *follow* (hold) and *toggle* actions.
- SHOULD: Breakpoints, step execution, a settable simulation clock/date, and a configurable log are
  available.

**Readiness:** Out of scope — not slated for implementation (the simulation engine is not yet specified
in enough detail to build faithfully). The stories below are retained as documentation only.

---

## US-034 — Start and stop offline simulation

**As a** commissioning technician, **I want** to start and stop offline simulation and see states
colour‑coded, **so that** I can verify behaviour without a controller.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Start simulation
  Given a project open in configuration or programming view
  When I choose "Simulation" > "Start simulation" (or press F8)
  Then the app enters simulation mode in the same view
  And inputs/outputs are coloured by state: red = OFF, green = ON (red/green arrows in the program view)

Scenario: Simulation is read-only for the model
  Given simulation mode is active
  Then I cannot program or configure; to edit I must leave simulation mode first

Scenario: Leave simulation mode
  Given simulation mode is active
  When I press F7
  Then simulation ends and I return to editing

Scenario: Power-up check on entry
  Given I enter simulation mode
  Then the app checks all function blocks that contain a "Powerup" event, using the PC clock as the
    power-up time
```

### AC illustrations

- In the configuration view, starting simulation recolours an input pin red (OFF) and its driven lamp
  output red; toggling the input green (US-035) turns the lamp green if the logic connects them.

**Readiness:** Ready.

---

## US-035 — Drive inputs and outputs during simulation

**As a** commissioning technician, **I want** to actuate inputs and outputs while simulating, **so
that** I can verify a sensor’s effect on an actuator or a block’s behaviour.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Toggle an input
  Given simulation mode is active
  When I select an input (a product input, or a function-block input) and press Ctrl+Space
  Then the input flips state and holds (toggle)

Scenario: Follow an input (hold)
  Given simulation mode is active
  When I select an input and hold Space
  Then the input is ON only while Space is held (follow), returning to OFF on release

Scenario: Drive a function-block output directly
  Given simulation mode is active
  When I select a function-block output and press Ctrl+Space (toggle) or hold Space (follow)
  Then the output changes state, letting me exercise downstream logic

Scenario: Simulate a power outage
  Given simulation mode is active
  When I choose "Power loss"
  Then the app simulates a PowerUp event: it activates programs with a Powerup event and resets every
    variable not ticked "Save value on power loss"
```

### AC illustrations

- Holding `Space` on a push‑button input in a `<function block>` keeps the linked output green
  only while held; using `Ctrl+Space` on a `<function block>`’s input toggles the output on each press.

**Readiness:** Ready.

---

## US-036 — Set breakpoints and step through a program

**As an** IHC programmer, **I want** to place breakpoints and step a program line by line, **so that**
I can localise a fault to a specific command.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a breakpoint
  Given the programming view with a function block expanded
  When I right-click a program line and choose "Breakpoint"
  Then a breakpoint (full-stop) icon appears at the start of that line
  And the same toggle is available via the Break key

Scenario: Run to a breakpoint
  Given a breakpoint is set and simulation is running
  When execution reaches the breakpoint
  Then the simulation stops at that line

Scenario: Step execution
  Given simulation has stopped (at a breakpoint or start)
  When I press F9
  Then the simulation executes the next line and stops again (step)

Scenario: Remove a breakpoint
  Given a line with a breakpoint
  When I right-click it and choose "Breakpoint" again
  Then the breakpoint is removed
```

### AC illustrations

- Placing a breakpoint on the event line (`<pin>`) of a clock block, then pressing `F9`,
  advances one line per press so the technician can watch the output flip at 21:00 (see US-037).

**Readiness:** Ready.

---

## US-037 — Simulate system time and date

**As a** commissioning technician, **I want** to set the simulated clock and date, **so that** I can
trigger time‑ and date‑driven programs on demand.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Open the simulation time/date dialog
  Given simulation mode is active
  When I press Ctrl+E
  Then a dialog appears with a "Time" field (format hh:mm:ss) and a "Date" field (format dd-mm-yyyy)
  And the "Date" field offers a calendar picker via its arrow

Scenario: Event-driven timing requires setting time just before the target
  Given a program that triggers at 21:00:00
  When I set the simulation time to just before it (e.g. 20:59:55) and confirm with "OK"
  Then the status bar's bottom-right shows the simulation time and date
  And when the clock reaches 21:00 the driven output changes from OFF to ON

Scenario: Simulation clock is not remembered between runs
  Given I stop the simulation and later start a new one
  Then the app does not remember the previous simulation time/date; it uses the PC's own settings
```

### AC illustrations

- Setting *Time* = `20:59:55` and *Date* = a Thursday shows `20:59:55` and `Thursday 29 June 2017`
  bottom‑right; the light output flips ON as the simulated clock ticks past 21:00.

### Constraints

- Verification method — **Demonstration** of the event‑driven trigger and the non‑persistence of the
  simulation clock.

**Readiness:** Ready.

---

## US-038 — Capture a simulation log

**As a** commissioning technician, **I want** to log selected simulation activity and export it, **so
that** I can analyse behaviour over time or during fault‑finding.

### Acceptance criteria (Checklist)

- [ ] MUST: `Ctrl+L` toggles a simulation‑log dialog (shown and hidden alternately).
- [ ] MUST: The log offers checkboxes to include: **Events**, **Conditions**,
  **Commands**, **Value change**, **Links**, and **Log marked** (marked
  only); checked items appear in the window as the program executes them.
- [ ] SHOULD: Selecting **Log marked** means only inputs/outputs flagged as *marked* are logged; when
  using it, **Value change** should not also be checked.
- [ ] SHOULD: An input/output can be marked by right‑click > *Log marked* (works during simulation too)
  or `Ctrl+M`, so only marked terminals are logged.
- [ ] SHOULD: Three buttons are present — **Clear log** (clear), **Stop** (pause logging; renames to
  *Start*; simulation itself keeps running), and **Save** (export the log to an Excel file).
- [ ] MAY: The log is useful alongside step execution (`F9`) and for long‑running fault‑finding.

### AC illustrations

- Checking only *Commands* logs each command as it runs; pressing *Stop* freezes the log (button
  becomes *Start*) while simulation continues; *Save* writes the accumulated log to Excel.

**Readiness:** Ready.
