# IHC OpenVisual — icon design guidelines

How the icon set in [`../Assets/`](../Assets/) is drawn, and how to add new icons that fit it.
Follow this whenever you create, edit, or replace an icon so the whole set keeps reading as **one
family**.

The set is a **modern flat-line** icon family for the IHC OpenVisual app: single-ink line glyphs on a
24-unit grid, themeable via `currentColor`, tuned to stay legible at 16 px in a tree row.

---

## 1. Design principles

1. **Metaphor is the identity.** An icon *is* its idea — a pennant, an hourglass, an `&`, a
   pressing hand. Design the metaphor and keep it simple; never add decorative detail the metaphor
   doesn't need.
2. **Monochrome glyph + a separate state-colour layer.** Every glyph is drawn in a single ink
   (`currentColor`). Colour is never baked into a glyph — the app applies it to signal **state**
   (see §5). This keeps icons legible without colour (accessibility) and makes colour mean *state*,
   not decoration. The **severity family is the one exception** (§5.1): those four glyphs pin a
   signal ink on the sub-shape that carries the badness, because the badness is not the whole
   glyph and one runtime ink cannot say two things.
3. **Legible at 16 px.** Icons render in a tree row at 16 px. Every design must survive downscaling
   with no fused or lost strokes. This is the hardest constraint and it wins over detail.
4. **Keep the grammar.** The set has a small visual language (§4). Reuse it; don't reinvent it.

---

## 2. Canvas & format rules (hard requirements)

Every icon file MUST follow these. They are mechanically checked (§7).

| Aspect | Rule |
|---|---|
| Format | SVG, one file per semantic key |
| Canvas | `viewBox="0 0 24 24"` |
| Live area | all artwork within **2 … 22** (a 2-unit safe margin on every side) |
| Optical size | glyph fills roughly the **3 … 21** box, visually centered |
| Root paint | `fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"` |
| Namespace | `xmlns="http://www.w3.org/2000/svg"` on the root (required by Avalonia's Skia SVG loader) |
| Accessibility | `aria-hidden="true"` on the root — icons are **decorative**; they always sit beside a text label. No `role`, no `<title>` |
| Identifiers | a **semantic `id`** on every drawn element (e.g. `id="pole"`, `id="pennant"`, `id="check"`) |
| Colour | **`currentColor` only** — no hex/named colours, no gradients. Sole exception: the severity family's two declared signal inks (§5.1) |
| Forbidden | no `<text>`, no `style=` attributes, no filters, no `<image>`, no embedded raster |
| Budgets | ≤ 2048 bytes/file · ≤ 12 path commands per `<path>` · ≤ 2 decimal places |

Canonical skeleton (a pure-stroke glyph):

```svg
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" aria-hidden="true"
     fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
  <polyline id="check" points="4 13 10 19 20 5"/>
</svg>
```

### Filled sub-shapes (the one allowed style mix)

Most icons are pure strokes. When a mark reads better solid — a filled arrowhead, a pennant, a
lightning bolt, an `!` bar/dot, a terminal dot — draw *that element* filled and override paint on
the element itself:

```svg
<rect id="bar" x="10.5" y="4" width="3" height="10" rx="1.5" fill="currentColor" stroke="none"/>
<circle id="dot" cx="12" cy="19" r="1.9" fill="currentColor" stroke="none"/>
```

This is deliberate and permitted. The validator emits `W-MIXED-STYLE` (per file) and `W-SET-STYLE`
(across the set) for these — **those two warnings are expected and accepted**; do not "fix" them by
removing the fills. Everything else the validator flags should be resolved.

### Letters & digits — never `<text>`

Glyphs that are letters/symbols (`N`, `F`, `#`, `≥1`, `?`, `&`, `S0`, `AUTO`, `7`) are built from
`<line>` / `<polyline>` / `<path>` stroke geometry, **not** `<text>` (fonts aren't guaranteed at
render time and don't scale predictably). Keep them bold and centered so they hold at 16 px.

---

## 3. Stroke, weight & construction

- **One weight for the family: `stroke-width="2"`.** Do not vary it per icon. If a detail looks
  heavy, simplify the shape — don't thin the stroke.
- **Round caps and joins** everywhere (`stroke-linecap="round"`, `stroke-linejoin="round"`).
- **Corner radius:** boxes and cards use a small radius (≈ `rx="2"`–`3`) — soft, not pill-shaped.
- **Minimum gap:** keep ≥ ~2 units of clear space between parallel strokes so they don't fuse at
  16 px. If two lines merge when downscaled, spread them or drop one.
- **Snap to the grid** where it helps hinting: prefer integer or `.5` coordinates for key strokes.
- **Prefer fewer primitives.** A `polyline` of 3 points beats a fussy `path`. Simpler geometry
  survives 16 px and stays under budget.

---

## 4. The visual grammar (reuse these cues)

New icons must respect the conventions the set already encodes:

- **Direction: `→` = IN, `←` = OUT.** Everywhere. Never flip this.
- **Three arrow weights, kept visually distinct:**
  - **section** = a bold arrow **framed inside a box** (a section of a block),
  - **pin** = a **bare bold arrow** with a solid filled head (heaviest), no frame,
  - **link** = a **thin open chevron** with a short shaft (lightest).
  A new connectivity icon must slot into one of these weights, not invent a fourth.
- **The box family must stay distinguishable by silhouette** (they share one ink colour):
  `locality` = offset card/stack · `fb-lk` = closed rounded box · `fb-editable` = open-top "U" ·
  `scenario` = box-within-box · `rs485-module` = box with terminal rows. Any new box-like icon
  needs its own unmistakable silhouette against these.
- **Group vs single = a drop-shadow/offset-duplicate cue.** `event-group` / `command-group` repeat
  the single glyph with a faint offset copy behind it. Reuse this exact cue for any new
  group/single pair; don't use size alone.
- **IEC logic symbols are canonical:** `&` (AND), `≥1` (OR), `?` (condition). Keep them.
- **Product icons are real-world objects** (hand, lampshade, plug, `AUTO` + rail, `S0`) drawn in
  the same line weight as everything else.

If an icon needs a mark this list doesn't cover, prefer a **platform-familiar** metaphor (what a
user already knows) and keep it to the same weight and rounding as the family.

---

## 5. Colour & theming (how icons are coloured at runtime)

Icons ship as `currentColor` monochrome. The app supplies the ink and any **state** colour — never
hard-code colour in the SVG.

**Ink** follows the theme:

| Token | Light | Dark |
|---|---|---|
| `ink` (default glyph) | `#1A1A1A` | `#E8E8E8` |
| `ink-muted` (link chevrons, connectors) | `#6B7280` | `#9AA3AF` |

**State layer** — applied by the app as the glyph colour for a given state, as an *overlay*, not
baked into the artwork:

| State | Light | Dark | Meaning |
|---|---|---|---|
| `select` | `#0078D7` | `#3B93E8` | selection / focus |
| `fb` | `#E10E0E` | `#FF5A5A` | function-block identity (e.g. `fb-lk` shown red) |
| `state-off` | `#D33` | `#FF6B6B` | simulation OFF |
| `state-on` | `#2F9E44` | `#51CF66` | simulation ON |
| `warn` | `#E8A400` | `#FFC53D` | unlinked (`!`) |

Because glyphs are monochrome, the same SVG serves every state — you set the colour, not the file.
For example `fb-lk.svg` is drawn in `currentColor` and the tree colours it red via the `fb` token;
there is no separate red asset.

### 5.1 The severity family's baked signal ink (the one exception)

`severity-error.svg`, `severity-warning.svg`, `severity-info.svg` and `severity-fatal.svg` are the
only glyphs allowed a colour literal, and they carry **at most two** — an allow-list, not a licence:

| Ink | Value | Pairs with | Carried by |
|---|---|---|---|
| signal red | `#B91C1C` | `ErrorBrush` (light) — the red a refusing dialog writes in | `severity-error` cross · all of `severity-fatal` |
| signal blue | `#1E5AA8` | `PaneHeaderBackgroundBrush` — the Problemer heading's brand blue, fixed in both themes | `severity-warning` bang bar + dot |

**Why these four break rule 2.** A severity glyph says two things at once — *which* mark (the shape)
and *how bad* (the ink) — and the mark carrying the badness is a **sub-shape**: the error ring stays
theme ink while its cross goes red. One `CurrentColor` sets one ink for the whole glyph, so it
cannot express both. The rule the family keeps instead:

- **Only the signal shape is coloured; the surround stays `currentColor`.** That surround is what
  keeps the glyph readable on a dark surface, where a baked light-theme red would sink. The
  exception is `severity-fatal`, which colours whole — it is the one severity glyph that appears
  alone in a dialog rather than as a row among neighbouring tiers.
- **The ink is never the only carrier.** Every severity cell and filter toggle pairs its glyph with
  the Danish tier word, so the meaning survives colour-blindness and a greyscale render.
- **The literals are copies of App.axaml tokens** — an SVG cannot reference an Avalonia resource.
  `SeverityIconConformanceTests.TheSignalInksAreTheAppsOwnLightThemeTokens` reads them back out of
  `App.axaml`, so retuning a brush fails the build until the artwork follows.

Adding a colour to any *other* glyph is still wrong; use the runtime state layer above.

### Avalonia usage

Render with `Avalonia.Svg.Skia` and drive the ink through the control so `currentColor` follows the
theme / state:

```xml
<!-- 16px tree-row icon; IconCss flips with theme and state -->
<Svg Path="/Assets/pin-in.svg" Width="16" Height="16"
     Css="{Binding IconCss}" />   <!-- e.g.  * { color: #1A1A1A }  /  fb -> #E10E0E -->
```

Keep the rendered size at the intended pixel size (16 for tree rows, 20/24 for toolbars). Don't
scale a 16-px-tuned icon up to 48 and expect detail — there is none to reveal; that's by design.

---

## 6. Authoring workflow (render-verify-fix)

Never ship an icon whose render you haven't looked at. A quality loop of *draft → validate →
render → inspect → fix* is what keeps the set consistent.

1. **Draft** the SVG per §2–§4, in a scratch dir (not `Assets/` yet). Give every part a semantic id.
2. **Validate** structure, paint, budgets and set-consistency with a linter (the icon-creator
   skill's `validate_icon.py` does all of these). Clear every ERROR; keep only the accepted
   `W-MIXED-STYLE` / `W-SET-STYLE` warnings.
3. **Render & read** at 96, 24 and 16 px and actually inspect the PNGs. Ask: is the metaphor
   unmistakable at 96? does it survive 16 (no fused/lost strokes)? is it centered and the right
   optical size? does it match the family weight?
   - Rasterizer note: on this machine the working backend is `@resvg/resvg-js` (via Node). Windows'
     `convert.exe` is a disk utility, **not** ImageMagick, and cannot rasterize SVG.
4. **Fix** with targeted edits and re-render. If two iterations don't fix it, the construction is
   wrong — redesign, don't nudge.
5. **Family check:** render the new icon *alongside* its neighbours (a contact sheet of the whole
   `Assets/` folder) and confirm it looks like one set — same weight, size, rounding.
6. **Validate the whole set** in one command so cross-file gates run, then copy the final SVG into
   `Assets/` (pin the family width, e.g. `--stroke-width 2`).

---

## 7. New-icon checklist

- [ ] `viewBox="0 0 24 24"`, artwork within 2 … 22, glyph optically centered in ~3 … 21.
- [ ] Root: `xmlns`, `aria-hidden="true"`, `fill="none" stroke="currentColor" stroke-width="2"`,
      round cap/join.
- [ ] `currentColor` only — no colours, gradients, filters, `style=`, `<text>`. (A severity glyph
      may pin a signal ink from §5.1's two-value allow-list, on the signal shape only.)
- [ ] Semantic `id` on every drawn element.
- [ ] Filled sub-shapes (if any) pair their fill with `stroke="none"`; only `W-MIXED-STYLE` /
      `W-SET-STYLE` warnings remain.
- [ ] Grammar respected: `→` in / `←` out; correct arrow weight; box silhouette distinct; group =
      shadow-offset cue.
- [ ] Validation = 0 errors, both alone and in the full-set run.
- [ ] Rendered and **eyeballed at 16 px** — no fused/lost strokes — and checked next to the family.
- [ ] Metaphor is immediately recognisable at a glance (the 5-second test).

---

## 8. Exemplars to copy from

Open these in `../Assets/` as reference constructions:

- **Pure stroke:** `command.svg` (a 3-point `polyline`), `fb-editable.svg` (open-top box).
- **Filled sub-shape:** `event.svg` (bar + dot), `var-energy.svg` (lightning bolt),
  `pin-in.svg` (shaft + filled arrowhead).
- **Letters from strokes:** `var-integer.svg` (`N`), `cond-and.svg` (`&`), `product-s0.svg` (`S0`),
  `product-sensor.svg` (`AUTO`).
- **Object metaphor:** `product-button.svg` (pressing hand + button), `var-temperature.svg`
  (thermometer), `section-settings.svg` (pencil).
- **Group/single pair:** `event.svg` ↔ `event-group.svg`; `command.svg` ↔ `command-group.svg`.
