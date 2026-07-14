---
version: 0.1.0
last-updated: 2026-07-03
status: draft
---

# E10 — Controller transfer

> **Current scope:** 🕒 **Deferred** — controller send / retrieve needs a live controller / transfer
> API and does not CRUD the local project; out of the current iteration.

**Goal:** Let a commissioning technician send the finished project to the controller (where it is
stored in EPROM) and retrieve it back to the PC, so the installation runs and can be re‑edited later.

**Scope:** *Controller > Send project* and *Controller > Retrieve project*, the unlinked‑wireless warning,
the overwrite confirmation, the transfer‑status dialog, and the *Close on success* option. **Scope
excludes:** wireless linking (E4), online simulation and runtime control (out of scope for this app).

**Acceptance criteria (epic level):**
- MUST: The technician can send the project to the controller and retrieve the project from the
  controller.
- SHOULD: Sending warns if not all wireless products are linked and confirms before overwriting an
  existing controller project.
- SHOULD: Retrieve is disabled when the controller holds no project.

**Readiness:** Not Ready — both stories describe live‑controller behaviour not yet confirmed against a
running installation; observable dialogs are described in prose only.

---

## US-042 — Send a project to the controller

**As a** commissioning technician, **I want** to send the project to the controller, being warned about
unlinked wireless products and about overwriting an existing project, **so that** the installation runs
the intended configuration.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Send the project
  Given a project is ready and a controller is connected
  When I choose "Controller" > "Send project" (or press F5)
  Then the transfer begins after any warnings are acknowledged

Scenario: Warn about unlinked wireless products
  Given not all wireless products have been linked
  When I start "Send project"
  Then a dialog notes the unlinked products (they can be linked later)
  And I can choose "Send" to continue anyway

Scenario: Confirm overwriting an existing controller project
  Given the controller already contains a project
  When I proceed with "Send"
  Then the app warns the existing project will be overwritten
  And clicking "Send" overwrites it and shows an upload-status/progress dialog ending in success

Scenario: Auto-close on success
  Given the send-status dialog is shown
  When "Close on success" is ticked
  Then the dialog closes automatically once the transfer succeeds
```

### AC illustrations

- Sending a project with two unlinked wireless products shows the unlinked warning; choosing *Send*
  proceeds; because the controller already had a project, an overwrite warning follows; confirming
  uploads and shows a success status that auto‑closes if *Close on success* is ticked.

### Constraints

- Verification method — **Demonstration** against a live controller.

**Readiness:** Not Ready.
- [R4] External dependency: requires a connected controller; the exact upload‑status dialog layout is
  described in prose only and not yet confirmed against a running installation.

**Implementation status:** ◑ **Offline slice implemented; the controller transfer itself deferred (no-controller
constraint).** The actual send-to-EPROM, overwrite confirmation, upload-status/progress dialog and *Close on success*
are **live-controller** behaviours (epic-level "Deferred — needs a live controller"; verification is Demonstration
against a live controller), which this build must not perform — it never contacts a controller (no controller side
effects). What **is** delivered, controller-free: **Controller ▸ Send project** (and **F5**) runs the offline
pre-flight — `ProjectSession.GetUnlinkedWirelessProducts` scans the project and, when wireless products are not yet
linked, shows the **unlinked-wireless warning** listing them ("they can be linked later"); declining cancels,
accepting proceeds. The command then reports that the transfer requires a connected controller and stops (no
controller contact). Traced via `RunAsync`; errors logged + surfaced. Tests: 3 in `ControllerTransferTests`
(unlinked-wireless detection; decline cancels; accept → controller-required notice). Suites: `safe_visual_tests`
**160**, byte-fidelity `safe_project_tests` **663** green. OpenObserve 0 errors. *(Deferred to a controller-enabled
build: the transfer, overwrite confirm, status/progress dialog, Close-on-success — all require a live controller and
the SDK `ProjectAppService.UploadTo` bridge.)*

---

## US-043 — Retrieve a project from the controller

**As a** commissioning technician, **I want** to retrieve the project stored in the controller, **so
that** I can edit an installation whose source file I do not have locally.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Retrieve the controller's project
  Given a controller is connected and contains a project
  When I choose "Controller" > "Retrieve project"
  Then a dialog shows data about the project currently in the controller
  And clicking "Get" downloads it to the PC, ending in a success indication

Scenario: Retrieve disabled when the controller is empty
  Given the controller holds no project
  When I open "Retrieve project"
  Then the "Get" action is not highlighted/enabled (nothing to retrieve)

Scenario: Auto-close on success
  Given the retrieve-status dialog is shown
  When "Close on success" is ticked
  Then the dialog closes automatically once the retrieve succeeds
```

### AC illustrations

- Opening *Retrieve project* against a controller with a stored project shows its metadata and an enabled
  *Get*; against an empty controller, *Get* is greyed out.

### Constraints

- Verification method - **Demonstration** against a live controller.
- **Open discrepancy to resolve (do not implement blindly):** one part of the guidance
  cites `F5` for both *Send project* and *Retrieve project*, while the keyboard shortcut set assigns `F5`
  to *Send project* only. IHC OpenVisual should resolve during implementation whether `F5` opens retrieve
  before assigning it to *Retrieve project*.

**Readiness:** Not Ready.
- [R4] External dependency: requires a connected controller with a stored project; the retrieve dialog
  contents are documented in prose only.

**Implementation status:** ◑ **Menu present; the retrieve transfer deferred (no-controller constraint).** Retrieving a
project stored in the controller's EPROM is a **live-controller** operation (epic "Deferred — needs a live controller";
verification is Demonstration against a live controller) with no offline part — there is nothing to pre-flight locally.
This build never contacts a controller (no controller side effects). **Controller ▸ Retrieve project** is present and,
when invoked, reports that a connected controller is required and performs no transfer (no controller contact). The
`F5` open-discrepancy is resolved conservatively — F5 is bound to **Send project** only (matching the keyboard-shortcut
set), not Retrieve. Traced via `RunAsync`. Test: 1 in `ControllerTransferTests` (reports controller-required). Suites:
`safe_visual_tests` **160**, byte-fidelity `safe_project_tests` **663** green. OpenObserve 0 errors. *(Deferred to a
controller-enabled build: the retrieve, the in-controller project metadata dialog, Get enable/disable, Close-on-success —
all require a live controller and the SDK `ProjectAppService.DownloadFrom` bridge.)*
