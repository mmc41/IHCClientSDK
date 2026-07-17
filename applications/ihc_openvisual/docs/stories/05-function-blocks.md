---
version: 0.3.0
last-updated: 2026-07-17
status: draft
---

# E5 — Function blocks: insert & structure

> **Implementation status:** 🟡 Mostly implemented — the embedded FB catalog is measured **aligned** with
> IHC Visual (F‑042) and *Unlock* is now measured aligned too (US-020, F‑064/F‑065; its old open
> measurement is closed). ⚠ One divergence remains: the *Functions* pane **renders containers IHC Visual
> hides** (US-018).

> **Current scope:** ✅ **In scope** — inserting, structuring and unlocking function blocks and
> managing FB folders is project CRUD.

**Goal:** Let an installer add ready‑made library or empty function blocks into localities in the
*Functions* pane, understand a function block’s internal structure, unlock library blocks for editing, and
organise blocks into their own and favourite folders — so control functions can be assembled and
reused.

**Scope:** inserting preprogrammed library blocks from the library folders; inserting an empty block (which
enters programming mode); the four variable sections and the program subtree of a block; unlocking library
blocks; and managing custom / *Favourites* folders on disk. **Scope excludes:** the
actual logic authoring inside a block (E7) and product↔block links (E6).

**Acceptance criteria (epic level):**
- MUST: The installer can insert a preprogrammed library block or an empty block into a selected locality in
  the *Functions* pane, confirmed in the status bar.
- MUST: A function block **owns** four variable sections (Input, Output, Settings, Internal variables) and a
  program subtree (Programs > Program > Events / Commands). **Which of them the tree draws depends on the
  mode and on whether the section is empty** — see US-018's rendering rules; do not read this criterion as
  "all four are always visible".
- SHOULD: Library blocks are marked with a library badge and must be *unlocked* before their internals can be
  edited; custom (own) folders persist across software updates.

**Readiness:** Ready.

---

## US-018 — Insert a preprogrammed library function block

**As an** IHC installer, **I want** to insert a ready‑made library function block into a locality from the
library folders, **so that** I get a tested sub‑program without writing logic.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert a library block from the library folders
  Given the "Functions" pane shows a locality (e.g. "Living room")
  When I right-click the locality and choose a block from the library folder list
  Then the block is inserted as a child of that locality
  And the status bar reads: Function block '<block>' has been inserted under <locality>
  And the block node carries the library function-block icon and can be expanded to show its structure

Scenario: Library folders are the standard set
  Given the function-block folder list is shown
  Then it presents the library folders the catalog defines, named as the vendor names them

Scenario: A block bundles its variables and program
  Given a library block has been inserted
  When I expand it
  Then it shows its Input/Output/Settings sections with typed pins carrying default values
  And (after unlocking, US-020) its program can be opened in programming mode (US-026)
```

### Business rules (the function‑block catalog)

- MUST: The function‑block catalog is **IHC Visual's**, embedded in the SDK — the installer gets the
  vendor's blocks without a vendor installation (US-063).
- MUST: The library **categories keep the vendor's own names, verbatim** — not renamed, re‑homed or
  translated.
- MAY: IHC OpenVisual's own catalog‑import entries (US-059/US-060) sit alongside the vendor's categories.

> **Confirmed 2026‑07‑16 — aligned.** The embedded catalog carries **72 function blocks** under the
> vendor's own Danish category names, preserved verbatim (`00. Foretrukne`, `01. Lysstyring`, `02. Tid, ur
> og kalender`, `03. Persienne og vindue`, `04. Specielle funktioner`, `05. Klimastyring`, `06. Alarm`,
> `08. Viewer`, `AutoProof` — 07 is absent in the vendor's set too). **Contrast with the product catalog**
> (US-010), which IHC OpenVisual partly re‑homed and partly translated and which therefore diverges
> (F‑028): the FB half is what "embedded verbatim" looks like, and is the reason the two catalogs need
> different treatment rather than one rule. Evidence: `RESULTS.md` **F‑042**.
> ⚠ Not a leaf‑exact match — the vendor's Insert‑FB **menu** categories were not dumped and the 72 blocks
> were not diffed leaf‑for‑leaf.

### Business rules — which of a block's sections the tree draws

In **configuration mode** (the *Functions* pane's normal view), IHC Visual does not draw every section a
block owns. Two rules decide it:

- MUST: A section with **no members is not drawn at all**. An `Input` or `Settings` section that holds no
  pins is absent from the tree, not shown empty.
- MUST: **`Internal variables` is not drawn in configuration mode**, whether or not it holds members. It is
  a programming‑mode section (US-026, US-027) — internals are the block author's business, not the
  installer's.
- MUST: Suppression is **display‑only** — every hidden section stays in the `.vis` and is written back
  verbatim on save. This is the same discipline US-010's hidden product rows follow.

> **Added 2026‑07‑17.** ⭐ **The data underneath is perfect and the divergence is 100% chrome** — worth
> stating plainly, because a **+525‑row** difference reads like a modelling bug and is not one. The two
> panes were dumped deep and diffed for the first time: **24/24 localities match, every locality's block
> count matches, and of 321 section pairs present on both there are 0 pin‑count mismatches.** Every one of
> the 525 extra rows is accounted for by exactly these two rules — the empty‑section rule at **30/30 cases,
> 0 falsifications** (+30 rows), and `Internal variables` at **0 of 117** on the vendor against 117 of 117
> in IHC OpenVisual (+495 rows). Same family as US-010's hidden product rows: *the vendor hides; IHC
> OpenVisual renders the file faithfully.* Evidence: `RESULTS.md` **F‑068** (which closes **F‑062**).
>
> ⚠ **One open measurement, and it decides the shape of the second rule — do not implement past it.**
> Whether IHC Visual shows `Internal variables` **inside programming mode** could not be driven (the
> vendor's *Vis program* command would not fire from the automation harness — `RESULTS.md` **F‑069**, an
> open **E** with a diagnosed next step). If it does, the fix is *hide the section in configuration mode*,
> as written above. If it never shows internals anywhere, the section should not exist in IHC OpenVisual at
> all. US-026 already specs the former — *"Internal variables (visible only in programming mode)"* — and
> that rule predates the comparison, so it is **corroboration, not proof**: it was written from the vendor's
> documentation, and this workstream has already caught that documentation contradicting the app (US-045's
> arrow keys). **Measure F‑069 before touching the section.** The empty‑section rule is independent and can
> ship now.

### AC illustrations

- Inserting a `<function block>` under `Room` gives a block whose **Input** section holds its
  catalog-defined input pins (`<pin>`), and whose **Output** section holds its catalog-defined
  output pins (`<pin>`).
- Inserting a `<function block>` under `Utility room` shows its catalog-defined **Input**, **Output**
  and **Settings** pins (`<pin>`), each Settings pin carrying its catalog default value; the
  status bar reads `Function block '<block>' has been inserted under Utility room`.
- A block with no input pins shows only `Output` and `Settings` — no empty `Input` row — and shows no
  `Internal variables` row until its program is opened (US-026).

**Readiness:** Ready — the empty‑section rule is measured and unblocked. The `Internal variables` rule waits
on one capture (F‑069) that does not block it.

**Implementation status:** 🟡 Implemented (insert + catalog, both measured aligned — F‑042) — ⚠ **except the
section‑rendering rules**: IHC OpenVisual draws all four sections on every block including empty ones and
including `Internal variables`, which IHC Visual never shows in configuration mode (F‑068).

---

## US-019 — Insert an empty function block

**As an** IHC installer, **I want** to insert an empty function block, **so that** I can author a
custom function from scratch.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Insert an empty block into a locality
  Given a locality is selected in the "Functions" pane, in configuration mode
  When I right-click it and choose "Empty function block", or press Ctrl+Shift+B
  Then an empty block named "Empty block" is inserted under the locality
  And the status bar reads: Empty block was inserted under <locality>

Scenario: An empty block exposes the four variable sections
  Given an empty block is open in programming mode
  When I expand it
  Then it shows exactly: "Input", "Output", "Settings" and "Internal variables",
    each with its own icon

Scenario: Editing the block enters programming mode
  Given an empty block is selected
  When I press F3 (or otherwise open it)
  Then the view switches to programming mode (US-026), where the right pane shows
    "Programs" > "Program" > { "Events", "Commands" }
  And I can name the block by selecting it and pressing F2
```

### AC illustrations

- After inserting an empty block under `Garage` and entering programming mode, both pane headers read
  `Empty block`; the left pane shows `Empty block > {Input, Output, Settings, Internal variables}` and the
  right shows `Empty block > Programs > Program > {Events, Commands}`.

### Constraints

- ⚠ **Open, and deliberately not answered here: what a *brand‑new empty* block looks like in configuration
  mode.** US-018's empty‑section rule says IHC Visual omits a section with no members — and an empty block's
  sections are *all* empty, so read literally the rule says such a block shows no sections at all. That may
  well be right, but it is **an extrapolation, not a measurement**: the rule was measured on 117 populated
  library blocks, and no empty block was inserted on the vendor. The scenario above is scoped to
  **programming mode**, where the four sections are the authoring surface and the question does not arise.
  **Measure an empty block on the vendor before making the configuration‑mode view follow the rule.**

**Readiness:** Ready.

**Implementation status:** ✅ Implemented (programming‑mode structure). ⚠ Its configuration‑mode view
inherits US-018's section‑rendering divergence (F‑068).

---

## US-020 — Unlock a library function block for editing

**As an** IHC installer, **I want** to unlock a supplied library block, **so that** I can modify a tested
block instead of starting from an empty one — and undo the unlock if I did not mean it.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Unlock a library block
  Given a block carrying the library function-block icon is selected
  When I right-click it and choose "Unlock"
  Then the block is unlocked immediately, with no warning dialog
  And the icon changes to the editable function-block icon
  And I can now work with the block as with any custom block (edit variables and program)
  And "Unlock" no longer appears on the block's context menu

Scenario: An unlock can be taken back
  Given I have just unlocked a library block
  When I press Ctrl+Z (US-052)
  Then the block is locked again, its library icon returns, and "Unlock" reappears on its context menu
  And the application keeps running normally

Scenario: Locked blocks resist internal edits
  Given a library block that has not been unlocked
  Then its internals are treated as read-only until "Unlock" is applied
```

### Business rules (reversibility)

- MUST: Unlocking is an **ordinary undoable edit** — one *Undo* restores the lock completely (US-052). It is
  not a one‑way door.
- MUST: Unlocking raises **no warning**. It needs none: undo is the protection, and it is a better one than
  a dialog because it also covers the installer who meant to unlock and changed their mind afterwards.

> **⭐ Rewritten 2026‑07‑17 — this story specced a warning that should never be built, and the spec is
> deleted rather than implemented.** *(Was: "the unlock is warned about first, because it cannot be undone",
> plus a *Decline the warning* scenario and an irreversibility MUST — granted as deliberate exception #1,
> "IHC OpenVisual keeps its guards where the vendor is silent".)*
>
> **The exception's whole rationale was `an irreversible action with no other guard deserves a warning` —
> and the premise is false for this application.** Two measurements, taken together, dismantle it:
> - **F‑065 — the unlock IS reversible in IHC OpenVisual.** `Ctrl+Z` immediately after an unlock reported
>   *"Undid the last change"*, raised no modal, left the process alive and responding, and **fully re‑locked
>   the block** (its context menu went 7 → 8 items with *Unlock* restored). The irreversibility this story
>   was built on is IHC Visual's property, not IHC OpenVisual's — the earlier pass inherited it from the
>   vendor without checking whether it held here.
> - **F‑064 — IHC OpenVisual does not warn today**, and that is now the *measured* answer to the old TBD,
>   not an unknown: `fb.unlock` produced *"Unlocked Lamper v. hoveddør."* with **no dialog**, verified by
>   effect (the menu dropped 8 → 7 items and *Unlock* disappeared). So the app already matches the vendor's
>   silence, and the story was the only thing asking for a warning.
>
> ⭐ **So the gap here was story‑vs‑app, and the story was wrong.** Building the warning would have added a
> dialog nobody needs, in front of an action that is already safe, on the strength of a property the app
> does not have. Exception #1 still stands as a principle — it is why the delete confirmations survive
> (US-053) — it simply does not reach this case.
>
> ✅ **And IHC OpenVisual is strictly better than the vendor here, not merely equal:** the same undo that
> works cleanly here **crashed IHC Visual outright** (`RESULTS.md` **F‑046**), which is why US-052's
> graceful‑degradation rule exists and why the vendor's behaviour is class **D** — a defect, not a spec.
>
> Evidence: `RESULTS.md` **F‑064** and **F‑065** (both effect‑verified on the same block in one session).
> **F‑043**'s open **OpenVisual** half — its *"Undetermined"* column, which is what made it a class **E** — is
> what **F‑064**/**F‑065** closed. Its **vendor** half was never open: the silent unlock was measured in
> **F‑043 itself** (`RESULTS.md:207`).
>
> ⚠ **One honest caveat about the weakest link.** The vendor's *silence* is asserted in F‑043's vendor column,
> but that row's evidence column cites only the **menu‑item removal** (`&Oplås` vanishing) — which proves the
> unlock **fired**, not that **no dialog appeared**. It is the least‑supported claim in this chain, and the
> "delete the specced warning rather than build it" conclusion rests on it. Note it does not rest on it
> *alone*: **F‑065** independently removes exception #1's rationale by showing the unlock is undoable here, so
> the conclusion survives even if the vendor turns out to warn.

### AC illustrations

- A library block shows a distinct library badge (a red‑outlined square marker in the tree). Choosing
  *Unlock* switches the badge to the plain function‑block icon, signalling it is now editable, and removes
  *Unlock* from its context menu. `Ctrl+Z` puts the badge and the menu item back.
- In this project **109 of 117** blocks ship locked, so unlock is a routine step on the way to editing a
  library block — not a rare, dangerous one.

### Constraints

- Verification method — **Test** (`safe_visual_tests`): unlock a locked block, assert it unlocked with **no
  dialog**, then `Ctrl+Z` and assert the block is **locked again** and the application is still running.
  That last assertion is the regression guard for the sequence that kills IHC Visual (US-052, F‑046).
- **Do not add a warning to "protect" this action.** It was specced once and deleted on measurement — the
  unlock is undoable, so a confirmation would guard nothing. See the rewrite note above before re‑proposing
  one.

**Readiness:** Ready.

**Implementation status:** ✅ Implemented — and **measured aligned** with IHC Visual: the unlock is silent on
both apps (**F‑043** for the vendor half, **F‑064** for the IHC OpenVisual half — F‑064 measures only the
latter). IHC OpenVisual's undo of it is **verified good** and better than the vendor's: the block re‑locks and
the app survives, where IHC Visual crashes (F‑065 / F‑046). The story's former "does it warn?" TBD is closed:
**it does not, by design.**

---

## US-021 — Manage own and favourite FB folders

**As an** IHC installer, **I want** to create my own folders, save blocks into them, and keep
frequently used blocks in *Favourites*, **so that** I can reuse blocks quickly and keep
them across software updates.

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Create an own folder on disk
  Given IHC OpenVisual's function‑block library (`<catalog folder>`) folder
  When I right-click the empty area and choose "New" > "Folder" and name it (e.g. "My_blocks")
  Then a new folder is created; empty folders do not appear in IHC OpenVisual's block list,
    and own folders are preserved when IHC OpenVisual is updated

Scenario: Save a block into a folder
  Given a block is selected in the "Functions" pane
  When I right-click it and choose "Save..." — or press Ctrl+G — give it a name and optional note, and pick a target folder
  Then the block is stored there and its note is shown as a tooltip when hovering the block later

Scenario: Add and use a favourite
  Given a block that lives in a library folder (`<catalog folder>`)
  When I copy it and paste it into the "Favourites" folder
  Then it becomes available under "Favourites" when inserting blocks into a locality
```

### AC illustrations

- Hovering a saved block shows the note entered in its *Save...* dialog as a tooltip.

### Constraints

- Verification method — **Demonstration** of folder creation, block save, and favourite reuse; and
  **Inspection** that empty folders are hidden and own folders survive an update.

**Readiness:** Ready.

**Implementation status:** 🟡 Core implemented (Save block); folder/favourites management adapted for the install‑free design.
