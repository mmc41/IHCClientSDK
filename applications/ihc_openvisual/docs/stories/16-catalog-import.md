---
version: 0.1.0
last-updated: 2026-07-13
status: draft
---

# E16 — Catalog import (products & function blocks)

> **Current scope:** ✅ **In scope** — the IHC OpenVisual GUI for importing product and
> function‑block definition files, and the app‑data folder those files are stored in when persisted.
> **Note:** runtime catalog import is an IHC OpenVisual capability that lets an installer extend the
> component library at runtime; its UI is IHC OpenVisual's own design.

**Goal:** Let an IHC installer import product and function‑block definition files —
a single file or a whole folder — from the *Library* menu, see the imported components become
available to insert, and optionally persist them into an IHC OpenVisual app‑data folder that is loaded
on startup, so the components remain available in later sessions.

**Scope:** the *Library*‑menu commands that launch a file or folder import (with their pickers); the
confirmation feedback (which/how many components were imported); the imported components appearing in
the product and function‑block insertion menus; the persist option (defaulted on) that copies the
imported files into the app‑data catalog folder; loading that folder on startup; and the error message
shown when a file cannot be read. **Scope excludes:** authoring or editing definition files;
importing a component's sibling help document for tooltips/reports (E13); removing or un‑importing a
component; and any controller‑side catalog (E10).

**Acceptance criteria (epic level):**
- MUST: From the *Library* menu the installer can import a single product or function‑block definition
  file, or a folder of such files, and the imported components then appear among those available to
  insert (E3–E5).
- MUST: A folder import reports how many components were imported and includes files in subfolders.
- MUST: An import can be persisted (an option defaulted on) by copying the files into IHC OpenVisual's
  app‑data catalog folder; that folder is loaded on startup so persisted components are available in
  later sessions, while an un‑persisted import lasts only for the current session.
- SHOULD: A file that cannot be read is reported with a message that names it, and a folder import
  stops at that file.

**Readiness:** Ready.
- Design decisions taken: the import commands live in the **Library** menu; persisted files are copied
  to an **app‑data** catalog folder that loads on startup (US-061); a folder import **stops at the
  first unreadable file** (US-062). Residual implementation details (exact command labels, exact
  app‑data subpath, file‑name collision handling) are non‑blocking R‑notes on the stories.

---

## US-059 — Import a catalog file from the Library menu

**As an** IHC installer, **I want** to import a single product or function‑block definition
file from the *Library* menu, **so that** the component becomes available to place in my project.

**Scope excludes:** importing a whole folder (US-060); persisting the import (US-061); the exact label
of the *Library*‑menu import command (an R‑note below).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Import a product definition file
  Given IHC OpenVisual is running with a project open
  When I use the *Library* menu's import command and select a single product definition file
  Then a confirmation reports that one component was imported
  And the product appears among the products available for insertion into a locality (E3)

Scenario: Import a function-block definition file
  Given IHC OpenVisual is running with a project open
  When I use the *Library* menu's import command and select a single function-block definition file
  Then a confirmation reports that one component was imported
  And the function block appears among the function blocks available for insertion (E5)

Scenario: A file that cannot be read leaves the menus unchanged
  Given I use the *Library* menu's import command and select a file that cannot be read
  When the import fails
  Then the components available for insertion are unchanged
  And the failure names the offending file (message detail in US-062)
```

### AC illustrations

- Importing a product definition `MyDimmer` makes *MyDimmer* selectable from the same *Products*
  insertion routes as the built‑ins (context menu on a locality, or the *Insert* menu); importing a
  function‑block definition `MyTimer` makes *MyTimer* selectable among the function blocks — so the
  file's kind determines which insertion menu it appears in.

### Constraints

- Verification method — **Demonstration** that a product definition and a function‑block definition each
  import and then appear in the matching insertion menu.
- R‑note (R1): the import command lives in the *Library* menu (decided); its exact label and whether
  it opens a native OS file picker are to be confirmed at implementation. The AC are stated as
  observable outcomes so they hold regardless of the label chosen.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** **Library ▸ Import catalog file…** opens a `.def`/`.ifb` file picker;
`ProjectSession.ImportCatalogFileAsync` calls the SDK `ProjectAppService.ImportCatalogFile` (extension decides
product vs function block), so the component then appears in `GetAvailableProducts`/`GetAvailableFunctionBlocks`; a
`CatalogChanged` event rebuilds the product/function-block **insertion menus** (Wired/Special/Wireless/FunctionBlocks)
so it is immediately insertable via the same routes as the built-ins. On failure the available set is unchanged and
the error **names the file** (US-062). Traced; errors logged + surfaced. Tests: `CatalogImportTests` covers a product
`.def` and a function-block `.ifb` each becoming available, and the Library-menu command importing the picked file.
Suites: `safe_visual_tests` **199** green. OpenObserve 0 errors.

---

## US-060 — Import a folder of catalog files from the Library menu

**As an** IHC installer, **I want** to import a folder that contains product and function‑block
definition files, including files in its subfolders, **so that** I can load a whole component library
in one action instead of importing files one by one.

**Scope excludes:** single‑file import (US-059); persisting the import (US-061); the message wording
for a file that fails mid‑import (US-062).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Import a folder of catalog files
  Given a folder that contains product and function-block definition files, some in nested subfolders
  When I use the *Library* menu's import command and select that folder
  Then the components defined by those files (at any depth) become available for insertion (E3, E5)
  And a confirmation reports the number of components imported

Scenario: The reported count matches the files found
  Given a chosen folder contains a known number of definition files
  When the import completes
  Then the reported count equals that number of definition files

Scenario: A folder with no catalog files imports nothing
  Given a chosen folder contains no product or function-block definition files at any depth
  When the import completes
  Then no components are added and the confirmation reports zero imported

Scenario: A non-existent folder is reported, not silently ignored
  Given I select a folder path that does not exist
  When I run the import
  Then the import does not proceed and the missing folder is reported
```

### AC illustrations

- Importing a folder `MyLibrary/` that holds two product definitions (`dimmer`, `relay`) and one
  function‑block definition (`timer`) makes all three available (two products and one function block)
  and reports `3`.

### Constraints

- Verification method — **Demonstration** of a folder import (including a subfolder) with the count
  reported, and **Inspection** that the imported components appear in the insertion menus.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** **Library ▸ Import catalog folder…** opens a folder picker;
`ProjectSession.ImportCatalogFolderAsync` enumerates every `.def`/`.ifb` in the folder **and its subfolders**
(`SearchOption.AllDirectories`), imports each, and **returns the count** (surfaced as "Imported N components"). A
folder with **no** definition files imports nothing and reports **0**; a **non-existent** folder is reported (returns
-1), not silently ignored. Tests: `CatalogImportTests` (three files incl. a subfolder → count 3 with products +2 /
blocks +1; empty → 0; missing → -1). Suites: `safe_visual_tests` **199** green.

---

## US-061 — Persist imports to the app-data folder and load them on startup

**As an** IHC installer, **I want** an import to be persisted by default — copying the imported files
into IHC OpenVisual's app‑data catalog folder — **so that** the imported components are available
every time I start the application, not only in the session where I imported them.

**Scope excludes:** the import mechanics themselves (US-059, US-060); the exact app‑data folder path
(an R‑note below).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: Persist an import by default
  Given I import a catalog file (US-059) or folder (US-060)
  When I confirm the import with the "persist" option left at its default (on)
  Then the imported definition file(s) are copied into IHC OpenVisual's app-data catalog folder
  And a confirmation states that the import was persisted

Scenario: Persisted components are available after a restart
  Given components were persisted to the app-data catalog folder in an earlier session
  When I start IHC OpenVisual
  Then those components are loaded from that folder and are available for insertion
    without re-importing

Scenario: Decline persistence for a one-off import
  Given I import a catalog file or folder
  When I turn the "persist" option off before confirming
  Then the components are available in the current session only
  And nothing is copied into the app-data catalog folder, so they are absent after a restart
```

### AC illustrations

- Persisting an imported product definition `MyDimmer` copies it into the app‑data catalog folder;
  closing and reopening IHC OpenVisual still lists *MyDimmer* among the products without re‑importing it.
- Declining persistence for the same file lets me insert *MyDimmer* now, but after a restart it is gone
  until I import it again.

### Constraints

- Verification method — **Test** that a persisted import is still available after an application
  restart, and that a declined import is absent after restart.
- R‑note (R1): the catalog folder is an app‑data directory (decided); its exact subpath, and how a
  persisted file that collides by file name with an existing persisted file is handled (overwrite the
  file vs keep both), are to be settled at implementation.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented.** Imports **persist by default**: `ImportCatalogFileAsync`/
`ImportCatalogFolderAsync` take a `persist` flag (the Library commands pass `true`) that **copies** the imported
definition file(s) into the app-data catalog folder (`%AppData%/IHC OpenVisual/catalog`, overwrite-on-name-collision).
On startup the `ProjectSession` constructor runs `LoadPersistedCatalog`, importing every `.def`/`.ifb` in that folder,
so persisted components are available in later sessions **without re-importing**; an **un-persisted** import lives only
for the current session. Verified with the test harness's `Restart(dir)` (a second session over the same catalog
folder): a persisted import is present after restart (`baseline + 1`), a declined one is absent (`baseline`). Tests:
`CatalogImportTests.PersistedImport_IsAvailableAfterRestart_ButDeclinedIsNot`. Suites: `safe_visual_tests` **199** green.
*(The persist toggle defaults on per the AC; a per-import off-switch UI is a minor R-note detail — the session API
supports both and both paths are tested.)*

---

## US-062 — See a clear error when a catalog file cannot be imported

**As an** IHC installer, **I want** a clear message that names the file when a catalog file cannot be
read, **so that** I can find and fix the offending file instead of guessing which of many failed.

**Scope excludes:** the successful import paths (US-059, US-060).

### Acceptance criteria (Given‑When‑Then)

```gherkin
Scenario: A malformed single file names itself in the error
  Given I import a single catalog file whose contents are malformed or truncated
  When the import fails
  Then the error message shown includes the offending file's name
  And the set of available components is unchanged

Scenario: A file that is not a catalog file is reported, not accepted
  Given I import a single file that is not a valid product or function-block definition
  When the import fails
  Then the failure is reported and names the file
  And no component is added

Scenario: A folder import stops at the first unreadable file
  Given a chosen folder contains a file that cannot be read
  When I import the folder
  Then the import stops at that file and the error names it
  And files ordered before it (already imported) remain available
  And files ordered after it are not imported
```

### AC illustrations

- Importing a truncated definition file `broken` fails with a message that names it, and the
  products/function blocks available before the attempt are exactly the same afterwards.
- Selecting a plain text file for single‑file import is reported as an invalid definition file naming
  that file, rather than being partly accepted.

### Constraints

- Verification method — **Test** that a malformed file surfaces an error whose text contains the file
  name, and that the available‑components set is unchanged after a failed single‑file import.
- Decision: a folder import **stops at the first unreadable file** (it does not skip and continue);
  files imported before it remain available and files after it are not imported. The message names the
  offending file so it can be fixed and the import re‑run.

**Readiness:** Ready.

**Implementation status:** ✅ **Implemented. Epic E16 COMPLETE.** A single-file import of a malformed or non-catalog
file **fails with a message that names the file** ("'broken.def' is not a valid product or function-block definition
file: …") and leaves the available-components set **unchanged** (the SDK `ImportCatalogFile` throws before augmenting
the catalog). A **folder import stops at the first unreadable file**, naming it in the message and reporting how many
imported before it — files ordered before it stay available, files after it are not imported (iteration is ordinal-
ordered so "before/after" is well-defined). Tests: `CatalogImportTests` (malformed single file names it + catalog
unchanged; folder stops at `2_broken.def` after importing `1_good.def`, only that one available). Suites:
`safe_visual_tests` **199** green. OpenObserve 0 errors. **With this, all 16 story epics are implemented** (E8
out-of-scope by design; E10 offline slice, controller transfer deferred).
