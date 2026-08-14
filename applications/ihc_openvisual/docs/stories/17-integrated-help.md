---
version: 0.1.0
last-updated: 2026-08-14
status: draft
---

# E17 — Integrated component help

> **Scope:** In scope. A catalog component can carry a description of what it does and a description per
> terminal or pin. The application holds those texts and shows them nowhere, so an installer meeting an
> unfamiliar component has nothing to read. This epic makes the texts readable at
> the four moments they are needed — choosing a component, identifying a placed one, documenting it, and
> asking for help on it. They are catalog data: read-only, and a different thing from the installer's own
> documentation note (US-047), which is project data.

**Goal:** Let an IHC installer read what a catalog component and each of its terminals do, from inside the
open project, while choosing, identifying, documenting or asking for help on that component, so that a
project can be built without a separate product manual and without placing a component to discover what it
is.

**Scope:** the catalog's component-level description and its per-terminal descriptions, surfaced at four
points of need — the help action for the current selection (US-075), the product and function-block insert
lists (US-076), the properties dialogs of a placed component or terminal (US-077), and the tree-node hover
tooltip (US-078) — plus keeping the selected element's help visible while working (US-079).
**Scope excludes:** authoring or editing the catalog texts (they arrive with the component, built in or
imported — E16); the installer's own documentation note, which is project data authored in the properties
dialogs (E2–E5) and read on hover (US-047); the documentation reports, whose content is fixed by
US-040/US-041/US-073 (E9); and restating catalog text in another language — catalog text is data and is
shown as the component supplies it.

**Acceptance criteria (epic level):**

- MUST: For a component whose catalog carries a description, the installer can read that description
  without leaving the project and without inserting or opening the component.
- MUST: For a terminal whose catalog carries a description, the installer can read that description while
  identifying or documenting that terminal.
- MUST: Catalog text is read-only wherever it appears, and is distinguishable from the installer's own
  documentation note when both appear in one view.
- MUST: A component or terminal whose catalog carries no description leaves each of those surfaces usable —
  the absence is stated where help was explicitly requested, and passes silently where it was not.

**Constraints:**

- The help the application itself writes is Danish, as the rest of its UI is; the catalog's own text is
  shown as the component supplies it.
- Verification method — **Demonstration**, per story, on one documented function block and one product
  whose catalog carries no description, so the populated and empty cases are both exercised.

**Assumptions (recorded gap-fills):**

- **Assumption:** the role throughout is the *IHC installer* — basis: every epic in this collection uses
  that role, and it is the primary persona in `product.md`.
- **Assumption:** *catalog description* is the canonical name for the component/terminal text in these
  stories — basis: the collection already uses *documentation note* for the project-authored text, and the
  two must not share a name.
- **Assumption:** the absence of a description is **stated** where the installer asked for help (US-075,
  US-079) and **silent** where they did not (US-076, US-077, US-078) — basis: the help action already
  answers a no-help request with a message, while the other surfaces have nothing to suppress.
- **Assumption:** node-specific text precedes type-level text in the hover tooltip (US-078) — basis: the
  order was not stated anywhere; the installer's own note is the more specific of the two and must not be
  displaced by catalog text.

**Requirement:** FR-11.1 (context-sensitive Danish help for the selected element/view) and the
*Component help is inline, not a separate document* entry in `product.md`'s differences register. The
exclusion in that register is narrower than this epic: it excludes the original's product help **files**,
not help itself.

**Readiness:** Not Ready — open items:

- R5 — the rule for a **renamed** terminal is undecided. Catalog per-terminal text is matched to the
  terminal it describes, and an installer may rename a placed terminal; whether the renamed terminal keeps
  its catalog description or shows none must be decided. Affects US-075, US-077, US-078.
- R5 — the product half of the catalog carries no descriptions today (the function-block half does), so the
  product-facing value of every story here depends on those texts existing. The behaviour for the empty
  case is specified; the texts themselves are catalog content, not application behaviour.
- R1 — the Danish wording that labels catalog text where it appears beside the installer's own note is not
  fixed.

---

## US-075 — Read the catalog's help for the selected element

**As an** IHC installer, **I want** the help action to answer with the catalog's description of the selected
element and of the selected terminal, **so that** I can work out an unfamiliar component's purpose from
inside the project instead of consulting a separate product manual.

**Scope excludes:** help for a component that is not yet placed (US-076); the hover tooltip (US-078);
editing the catalog text.

### Acceptance criteria (Checklist)

- MUST: Requesting help for a placed component shows the catalog description of that component's type,
  together with the type name the catalog gives it.
- MUST: Requesting help for a terminal or a function-block pin shows the catalog description of that
  terminal or pin.
- MUST: The catalog description is read-only and distinguishable from the element's own documentation note,
  which continues to be shown when the element carries one.
- MUST: Requesting help for an element whose catalog carries no description states that no help exists for
  that element, instead of showing an empty result.
- MUST: Requesting help for a node that belongs to no catalog component — a locality, or a tree root —
  states that no help exists for it.
- SHOULD: Requesting help with no element selected states that no element-specific help exists, rather than
  leaving the request unanswered.

### AC illustrations

- Selecting the *Kip* input of a placed *Kip tænd sluk* function block and requesting help shows the block's
  description together with that input's own description, "skifter udgangen til modsat tilstand".
- Selecting a placed *LK FUGA Tryk 2 tast* product and requesting help states that no help exists for it,
  because the product catalog carries no description for that product.

### Constraints

- Verification method — **Demonstration**: request help on a documented function block, on one of its
  documented pins, on a product whose catalog carries no description, and with nothing selected; confirm
  all four answers.

**Readiness:** Not Ready — open items:

- R5 — the behaviour for a renamed terminal is undecided (see the epic's open items).

**Implementation status:** ⛔ Not implemented — the help action answers with the element's own documentation
note, or a message that no specific help exists; it does not read the catalog description.

---

## US-076 — See what a catalog component does before placing it

**As an** IHC installer, **I want** each entry in the product and function-block insert lists to reveal that
component's catalog description while I browse, **so that** I can pick the right component out of a long
category list without placing one to find out what it is.

### Acceptance criteria (Checklist)

- MUST: Browsing to a component entry in the product or function-block insert list reveals that component's
  catalog description without inserting the component.
- MUST: A component whose catalog carries no description stays listed and insertable, and reveals no
  description text.
- MUST: Browsing away from an entry withdraws the description it revealed.
- SHOULD: Reaching an entry by keyboard reveals its description on the same terms as reaching it by pointer
  (US-045).
- SHOULD: A component imported at runtime (US-059–US-062) reveals its description on the same terms as a
  built-in one, when the imported component supplies a description.

### AC illustrations

- Browsing to *Kip tænd sluk* in the function-block insert list reveals its description and inserts nothing;
  moving to the neighbouring entry withdraws it.
- Browsing to *LK FUGA Tryk 2 tast* in the product insert list reveals no description text, and choosing the
  entry still inserts the product.

**Readiness:** Ready.

**Implementation status:** ⛔ Not implemented — an insert entry carries the component's name only.

---

## US-077 — Read the catalog's description while documenting a component or terminal

**As an** IHC installer, **I want** the properties dialog of a placed component or terminal to state what the
catalog says it is, **so that** I fill in its placement, cable and identification fields against the right
device without leaving the dialog to check which device it is.

**Scope excludes:** the dialog's own editable fields, including the note (US-011, US-012); editing the
catalog text, which is not a field of the dialog.

### Acceptance criteria (Checklist)

- MUST: A placed component's properties dialog shows the catalog description of its component type,
  read-only.
- MUST: A terminal's properties dialog shows the catalog description of that terminal, read-only.
- MUST: Confirming the dialog without changing a field leaves the project unchanged: the catalog
  description is read-only and is not among the values the dialog writes.
- MUST: A component or terminal whose catalog carries no description opens its dialog with no description
  area and no placeholder in its place.
- SHOULD: The catalog description is distinguishable from the dialog's editable note field.

### AC illustrations

- Opening the properties of a documented function block's pin shows that pin's catalog description
  separately from the editable *Note* field; confirming the dialog without editing anything leaves the
  project unchanged.
- Opening the properties of a placed *LK FUGA Tryk 2 tast* shows the dialog with no description area,
  because the product catalog carries no description for that product.

**Readiness:** Not Ready — open items:

- R5 — the behaviour for a renamed terminal is undecided (see the epic's open items).

**Implementation status:** ⛔ Not implemented — the properties dialogs show the element's own fields only.

---

## US-078 — Read the catalog's description on hover

**As an** IHC installer, **I want** a tree node's hover tooltip to include the catalog's description of what
that node is, **so that** I can identify an unfamiliar product or terminal while scanning a tree without
stopping to ask for help.

**Scope excludes:** the documentation-note and resource-ID content of the tooltip, which US-047 and US-048
specify and which this story leaves unchanged.

### Acceptance criteria (Checklist)

- MUST: Hovering a node that belongs to a catalog component shows that component's — or that terminal's —
  catalog description in the tooltip, alongside the node's documentation note (US-047) and IHC resource ID
  (US-048).
- MUST: The tooltip distinguishes the catalog description from the node's own documentation note, so a
  reader can tell type-level text from project-authored text.
- MUST: A node whose catalog carries no description shows the tooltip US-047 and US-048 already specify,
  with no empty or placeholder description line.
- SHOULD: The node's own texts — its documentation note and its resource ID — precede the catalog
  description, so the most specific information reads first.
- SHOULD: The tooltip shows at most `[TBD]` characters of the catalog description, with the remainder
  reachable through the help action (US-075).

### AC illustrations

- Hovering the *Kip* input of a placed *Kip tænd sluk* block shows that input's note (when it carries one),
  its resource ID, and the catalog description "skifter udgangen til modsat tilstand", with the catalog text
  identifiable as catalog text.
- Hovering a placed *LK FUGA Tryk 2 tast* product shows the note-and-resource-ID tooltip US-047 and US-048
  specify, because the product catalog carries no description for that product.

**Readiness:** Not Ready — open items:

- R3 — the excerpt length is unset: whether hover shows the whole catalog description or a bounded excerpt
  is a display decision nobody has taken.
- R5 — the behaviour for a renamed terminal is undecided (see the epic's open items).

**Implementation status:** ⛔ Not implemented — a tooltip carries the documentation note and the resource ID
(US-047, US-048).

---

## US-079 — Keep the selected element's help visible while working

**As an** IHC installer, **I want** the help for whatever I have selected to stay visible while I keep
editing, **so that** I can wire and document a component while reading its description instead of asking
for help again at every element.

### Acceptance criteria (Checklist)

- MUST: While help is visible, selecting another element replaces the shown help with that element's help.
- MUST: While help is visible, the installer can go on selecting, editing and saving without dismissing it.
- MUST: The installer can dismiss the visible help and return to the working area unchanged.
- SHOULD: Selecting an element whose catalog carries no description keeps the help visible and states that
  no help exists for that element.

**Readiness:** Not Ready — open items:

- R1 — whether help should stay visible alongside the project at all is an open product decision. US-075
  already satisfies context-sensitive help on request; this story is an addition to it, not a replacement.
- R4 — depends on US-075, which fixes what an element's help contains.

**Implementation status:** ⛔ Not implemented.

---

### Story collection

| ID | Title | Readiness | Epic/Feature | Priority | Dependencies |
|----|-------|-----------|--------------|----------|--------------|
| US-075 | Read the catalog's help for the selected element | Not Ready | E17 | Must | -- |
| US-076 | See what a catalog component does before placing it | Ready | E17 | Must | -- |
| US-077 | Read the catalog's description while documenting a component or terminal | Not Ready | E17 | Must | US-011, US-012 |
| US-078 | Read the catalog's description on hover | Not Ready | E17 | Must | US-047, US-048 |
| US-079 | Keep the selected element's help visible while working | Not Ready | E17 | May | US-075 |
