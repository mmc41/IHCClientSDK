# IHC OpenVisual — icon selection reference (`.vis`/`.ifb` element → `Assets/*.svg`)

Companion to [`icons_design.md`](./icons_design.md), which defines our SVG glyph set (semantic
keys like `fb-lk.svg`). This doc maps each `.vis`/`.ifb` XML element — and the vendor
`icon="_0xNN"` code it may carry — onto the SVG a GUI editor should render for it.
[§7](#7-text-only-rendering--unicode-stand-ins) carries the same mapping one step further, onto
1–3 Unicode characters, for surfaces that cannot embed SVG (plain-text reports, console dumps).

**Select the icon by element *type*, not by the `_0xNN` code** (plus lock state / product
identity / condition method where noted). The code is unreliable as a key because:

- distinct icons share one code (`program_simple` and `program_sub` are both `_0x7`;
  `link_from_resource` and `scene_link` are both `_0x47`);
- one code spans unrelated products (`_0x86` = lamp outlet **and** dimmer **and** RS-485
  dimmer channel);
- several element types carry **no** `icon` attribute at all and fall back to a DTD default.

The `_0xNN` value is an opaque index into IHC Visual's built-in icon resource; the files
contain no vendor artwork, so the tables below map each element to *our* SVG.

---

## 0. Quick lookup — icon code → `Assets/*.svg`

Reverse index of every `_0xNN` code → the asset that renders it, sorted by numeric value. A
convenience lookup only — resolve the actual SVG by *element type* (§1–§6), because the code is
neither unique nor total: `_0x7` and `_0x86` each fan out to two assets (disambiguate by element
tag), and 9 assets have no code at all (see notes below).

| `icon` | dec | `Assets/*.svg` | Source element(s) |
|---|---:|---|---|
| `_0x4` | 4 | `section-input.svg` | `inputs` |
| `_0x7` | 7 | `prog-program.svg` **/** `prog-subprogram.svg` | by tag: `program_simple` / `program_sub` |
| `_0x8` | 8 | `command-group.svg` | `actions` |
| `_0x9` | 9 | `command.svg` | `action` |
| `_0xb` | 11 | `event-group.svg` | `events` |
| `_0xc` | 12 | `event.svg` | `event` |
| `_0xd` | 13 | `section-settings.svg` | `settings` |
| `_0xe` | 14 | `fb-lk.svg` | `functionblock` (locked) |
| `_0xf` | 15 | `fb-editable.svg` | `functionblock` (unlocked) |
| `_0x13` | 19 | `section-internal-vars.svg` | `internalsettings` |
| `_0x14` | 20 | `section-output.svg` | `outputs` |
| `_0x15` | 21 | `locality.svg` | `groups`, `group` |
| `_0x16` | 22 | — *(no asset; container)* | `conditions` |
| `_0x19` | 25 | — *(no asset; container)* | `programs` |
| `_0x1a` | 26 | `condition.svg` **/** `cond-and.svg` **/** `cond-or.svg` | `condition` — pick by logic method |
| `_0x22` | 34 | `var-enum.svg` | `resource_enum` |
| `_0x29` | 41 | `var-date.svg` | `resource_date` |
| `_0x2c` | 44 | `var-weekday.svg` | `resource_weekday` |
| `_0x2f` | 47 | `var-time.svg` | `resource_time` |
| `_0x33` | 51 | `var-flag.svg` | `resource_flag` |
| `_0x36` | 54 | `pin-in.svg` | `resource_input` (also `rs485_led_dimmer_error_state_*`) |
| `_0x39` | 57 | `pin-out.svg` | `resource_output` |
| `_0x43` | 67 | `var-timer.svg` | `resource_timer` |
| `_0x47` | 71 | `link-from.svg` | `link_from_resource`, `scene_link` |
| `_0x4a` | 74 | `link-to.svg` | `link_to_resource` |
| `_0x4d` | 77 | `var-timer-duration.svg` | `resource_timertime` |
| `_0x83` | 131 | `product-sensor.svg` | `product_dataline` (PIR / Lux-Temp) |
| `_0x85` | 133 | `product-button.svg` | `product_dataline` / `product_airlink` (Tryk) |
| `_0x86` | 134 | `product-lamp.svg` **/** `rs485-module.svg` | by tag: dataline/airlink lamp+dimmer vs `product_rs485_led_dimmer`/`rs485_led_dimmer_channel` |
| `_0x88` | 136 | `product-socket.svg` | `product_dataline` (Stikkontakt) |
| `_0x89` | 137 | `scenario.svg` | `resource_scene` (DTD default; elided in file) |
| `_0x99` | 153 | `product-s0.svg` | `s0_device` |
| `_0x9b` | 155 | `var-holiday.svg` | `resource_holiday` |

**Assets with no vendor code** — keyed purely by element type (the §3b types) plus one app-only
marker; these never appear under an `_0xNN`:

`var-integer.svg`, `var-decimal.svg`, `var-counter.svg`, `var-temperature.svg`, `var-humidity.svg`,
`var-illuminance.svg`, `var-light-level.svg`, `var-energy.svg`, and `breakpoint.svg` (simulation UI
marker — no `.vis` element).

**Shared codes** — disambiguate by element tag / logic method: `cond-and.svg` / `cond-or.svg` share
`_0x1a` with `condition.svg` (choose by the condition's logic method); `prog-subprogram.svg` shares
`_0x7` with `prog-program.svg`; `rs485-module.svg` shares `_0x86` with `product-lamp.svg`.

---

## 1. Structure & sections

| Vendor element | `icon` | Our key | Notes |
|---|---|---|---|
| `groups`, `group` | `_0x15` | `locality` | localities root + each room use the same code |
| `functionblock` (locked) | `_0xe` | `fb-lk` | `locked="yes"` — the closed LK block |
| `functionblock` (unlocked) | `_0xf` | `fb-editable` | no `locked` attr — the open/editable block |
| `inputs` | `_0x4` | `section-input` | |
| `outputs` | `_0x14` | `section-output` | |
| `settings` | `_0xd` | `section-settings` | |
| `internalsettings` | `_0x13` | `section-internal-vars` | |
| `programs` | `_0x19` | *(container — no dedicated key)* | |
| `conditions` | `_0x16` | *(container — no dedicated key)* | |

The `functionblock` code reflects lock state: `_0xe` (locked) → `fb-lk` closed box, `_0xf`
(unlocked) → `fb-editable` open-top "U".

## 2. Programs & logic

| Vendor element | `icon` | Our key | Notes |
|---|---|---|---|
| `program_simple` | `_0x7` | `prog-program` | shares `_0x7` with sub-program |
| `program_sub` | `_0x7` | `prog-subprogram` | — distinguish by element type |
| `events` | `_0xb` | `event-group` | |
| `event` | `_0xc` | `event` | |
| `actions` | `_0x8` | `command-group` | |
| `action` | `_0x9` | `command` | |
| `condition` | `_0x1a` | `condition` | pick `cond-and` / `cond-or` by the condition's logic method |

## 3. Resources (function-block variables)

Variable types split two ways: those carrying an explicit `icon` code (§3a), and those with no
code where the icon is chosen purely from the element type (§3b).

### 3a. Types with an explicit `icon` code

| Vendor element | `icon` | Our key | Meaning |
|---|---|---|---|
| `resource_input` | `_0x36` | `pin-in` | Boolean input (Indgang / Kip) |
| `resource_output` | `_0x39` | `pin-out` | Boolean output (Udgang) |
| `resource_flag` | `_0x33` | `var-flag` | Flag |
| `resource_enum` | `_0x22` | `var-enum` | Enum (user-defined type) |
| `resource_timer` | `_0x43` | `var-timer` | Timer |
| `resource_date` | `_0x29` | `var-date` | Date (Dato) |
| `resource_time` | `_0x2f` | `var-time` | Time of day (Tidspunkt) |
| `resource_weekday` | `_0x2c` | `var-weekday` | Weekday (Ugedag) |
| `resource_timertime` | `_0x4d` | `var-timer-duration` | Timer duration (Timertid) |
| `resource_holiday` | `_0x9b` | `var-holiday` | Holiday (Helligdag) |

### 3b. Types with no code — key the SVG on the element type

No `icon` is written; render the type's glyph from the element tag.

| Vendor element | Our key | Meaning / glyph |
|---|---|---|
| `resource_integer` | `var-integer` | Integer (Tal, `N`) |
| `resource_floating_point` | `var-decimal` | Decimal (Kommatal, `F`) |
| `resource_counter` | `var-counter` | Counter (Tæller) |
| `resource_temperature` | `var-temperature` | Temperature (thermometer) |
| `resource_humidity_level` | `var-humidity` | Humidity (droplet) |
| `resource_light` | `var-illuminance` | Illuminance = Lux (sun) |
| `resource_light_level` | `var-light-level` | Light level = % (bulb) |
| `resource_scene` | `scenario` | Scene (Scenarie) — DTD default `_0x89`; see §4 for its link |
| `kW`, `kWh`, `W`, `Wh` | `var-energy` | S0/meter power & energy (lightning) — **unit-named element tags**, not `resource_*` |

## 4. Links

| Vendor element | `icon` | Our key | Notes |
|---|---|---|---|
| `link_from_resource` | `_0x47` | `link-from` | |
| `link_to_resource` | `_0x4a` | `link-to` | |
| `scene_link` | `_0x47` | `link-from` | shares `_0x47` with `link_from_resource` |

## 5. Products — code is **not** a reliable discriminator

`icon` correlates with product *kind* but is reused across unrelated products, so switch on
`product_identifier` / `name`, never on the code alone.

| Product element | `name` (as seen) | `icon` | Our key |
|---|---|---|---|
| `product_dataline` | LK FUGA Tryk 2 tast | `_0x85` | `product-button` |
| `product_dataline` | Lampeudtag | `_0x86` | `product-lamp` |
| `product_dataline` | Stikkontakt | `_0x88` | `product-socket` |
| `product_dataline` | PIR / Lux-Temp sensor | `_0x83` | `product-sensor` |
| `product_airlink` | Tryk 2 tast | `_0x85` | `product-button` |
| `product_airlink` | Dimmer Universal / Lampeudtag | `_0x86` | `product-lamp` |
| `product_rs485_led_dimmer` | IHC LED Dimmer 2 kanaler | `_0x86` | `rs485-module` |
| `s0_device` | (S0 meter) | `_0x99` | `product-s0` |

RS-485 dimmer internals: `rs485_led_dimmer_channel` = `_0x86`; `rs485_led_dimmer_error_state_*`
(loadfailure / overheating / overcurrent / overvoltage) all = `_0x36`.

## 6. Elements with no `icon` attribute (structural / config)

These never carry `icon` and render from the DTD default (`_0x0`, **except `resource_scene` →
`_0x89`**). No app glyph is needed for pure config/metadata nodes:

`enum_definitions`, `enum_definition`, `enum_value`, `dataline_input`, `dataline_output`,
`dataline_input_modules`, `dataline_output_modules`, `documentation_modules`, `scenes`,
`scene_relay`, `light_indication`, `dimmer_settings`, `dimmer_setting_*`, `airlink_*`
(`airlink_relay`, `airlink_dimming`, `airlink_input`, `airlink_dimmer_increase/decrease`),
`installer_info`, `customer_info`, `project_info`, `modified`, `utcs_project`.

(The `kW`/`kWh`/`W`/`Wh` energy element tags also carry no `icon`, but they *do* get a glyph —
`var-energy` — so they live in §3b, not here.)

---

## 7. Text-only rendering — Unicode stand-ins

Plain-text surfaces cannot embed the SVGs: `.txt` report exports, console/CLI tree dumps, log and
diff output, clipboard "copy as text", and any report variant that must survive without a styled
`<svg>` sprite. This section maps each asset onto a **1–3 character** stand-in keyed on the same
`Our key` used by §1–§6, so a text renderer can reuse the element→key resolution unchanged and
only swap the final lookup table.

Two candidate columns, because no single character is simultaneously exact, monochrome and
single-width for every glyph:

- **Text** — BMP, text-presentation, monospace-safe. Renders monochrome at a predictable width in
  terminals, monospace fonts and `<pre>`. **Use this by default.**
- **Emoji** — the exact-likeness alternative where one exists. Renders in colour, is usually
  double-width, and is absent from many monospace fonts. Use only when the output surface is known
  to be proportional and emoji-capable.

**Fidelity** — `exact`: the character *is* the shape the SVG draws; `close`: same subject, drawn
differently; `tag`: a mnemonic that only reads with the legend (§7.4).

### 7.1 The mapping

| Our key | SVG draws | Text | Emoji | Fidelity |
|---|---|:--:|:--:|---|
| **§1 Structure & sections** | | | | |
| `locality` | two overlapping rounded cards | `⧉` | | exact (U+29C9 *two joined squares*) |
| `fb-lk` | closed rounded box | `▢` | | exact |
| `fb-editable` | open-top "U" box | `⊔` | | exact |
| `section-input` | arrow entering a box | `⇥` | | close |
| `section-output` | box with arrow entering from the right | `⇤` | | close |
| `section-settings` | pencil (barrel + nib) | `✎` | `✏️` | exact |
| `section-internal-vars` | wrench (ring + toothed shaft) | `⚙` | `🔧` | close (gear ≠ wrench) |
| *(containers `programs`, `conditions`)* | — no asset | | | — |
| **§2 Programs & logic** | | | | |
| `prog-program` | banner → stem → diamond node | `◆` | | **tag** |
| `prog-subprogram` | same + elbow connector | `↳◆` | | **tag** |
| `event-group` | two offset exclamation marks | `!!` | `‼️` | exact — see §7.3 on `‼` |
| `event` | filled bar over a dot | `!` | | exact |
| `command-group` | two overlapping checkmarks | `✓✓` | | exact |
| `command` | a checkmark | `✓` | | exact (prefer U+2713 over `✔`) |
| `condition` | hook + dot = question mark | `?` | | exact |
| `cond-and` | an ampersand | `∧` | | exact shape is `&`; prefer `∧` — see §7.3 |
| `cond-or` | `>` + underbar + a "1" = IEC ≥1 | `∨` | | exact shape is `≥1`; prefer `∨` — see §7.3 |
| **§3a Resources with a code** | | | | |
| `pin-in` | right arrow, filled head | `→` | | exact |
| `pin-out` | left arrow, filled head | `←` | | exact |
| `var-flag` | pole + filled pennant | `⚑` | `🚩` | exact |
| `var-enum` | 2 slanted verticals × 2 horizontals | `#` | | exact |
| `var-timer` | outlined hourglass with end bars | `⧖` | `⌛` | exact |
| `var-date` | page + header rule + two tabs | `▤` | `📅` | close (Text) / exact (Emoji) |
| `var-time` | clock face + hands | `◷` | `🕐` | close (Text) / exact (Emoji) |
| `var-weekday` | 4 stacked bars + a digit "7" | `≣7` | | exact (`≣` = four bars) |
| `var-timer-duration` | stopwatch (crown, lugs, hand) | `⏱` | | exact |
| `var-holiday` | parasol (canopy + pole + base) | `☂` | `⛱` | exact |
| **§3b Resources with no code** | | | | |
| `var-integer` | the letter **N** | `ℕ` | | exact |
| `var-decimal` | letter **F** + dot | `0.0` | | close (`𝔽` U+1D53D is exact but non-BMP) |
| `var-counter` | odometer window, two dividers | `123` | `🔢` | **tag** |
| `var-temperature` | thermometer (tube + bulb) | `℃` | `🌡` | close (Text) / exact (Emoji) |
| `var-humidity` | a droplet | `RH` | `💧` | **tag** (Text) / exact (Emoji) |
| `var-illuminance` | disc + 8 rays = sun | `☼` | `☀️` | exact |
| `var-light-level` | light bulb (glass, neck, base) | `☼%` | `💡` | **tag** (Text) / exact (Emoji) |
| `scenario` | square inside a square | `⧈` | | exact (U+29C8 *squared square*) |
| `var-energy` | filled lightning bolt | `↯` | `⚡` | close (Text) / exact (Emoji) |
| **§4 Links** | | | | |
| `link-from` | short left arrow, outline head | `⇠` | | close — see §7.3 on arrows |
| `link-to` | short right arrow, outline head | `⇢` | | close — see §7.3 on arrows |
| **§5 Products** | | | | |
| `product-button` | hand pressing a button | `☟` | `👆` | close |
| `product-lamp` | pendant lamp (cord, shade, glow) | `▽` | `💡` | **tag** (Text) / close (Emoji) |
| `product-socket` | socket body + prongs + cord | `⊓` | `🔌` | **tag** (Text) / exact (Emoji) |
| `product-sensor` | the word **AUTO** on a rail | `◉` | | **tag** — a 3-char field cannot hold a wordmark |
| `product-s0` | the letters **S0** | `S0` | | exact |
| `rs485-module` | box with 3 + 3 terminal stubs | `▥` | | close |
| **Non-report** | | | | |
| `breakpoint` | ring + slash | `⊘` | `🚫` | exact — simulation UI only, never in a report |
| `openvisual-logo` | house + "IHC" / "OpenVisual" wordmarks | — | | print the product name instead |

`toolbar_*.svg` are chrome, not document content, and need no text form.

### 7.2 Layout

Render the stand-in in its **own fixed-width column**, padded to 3, before the label — never
inline in the prose. That is what keeps the multi-character entries (`✓✓`, `≣7`, `↳◆`, `☼%`,
`123`) distinguishable from their 1-character prefixes (`✓`, `◆`, `☼`), and what keeps the
punctuation entries from being read as part of the text next to them:

```
▢   Kip med timer
 ⇥  Input
  → Kip                    Tænd/sluk. (Udfyldes af installatøren)
 ⇥  Indstillinger
  ⧖ Timer               = 00:03:00,000
 ↳◆ Programmer
  ◆ Kip
  !! Hændelser
   ! Kip -> ON
  ∧ Betingelser
   ? Udgang              = OFF
  ✓✓ Kommandoer
   ✓ Udgang              = ON
```

Indentation already carries the tree depth, so the stand-in only has to carry *type*.

### 7.3 Collisions to design around

Several icons want the same scarce characters, and some characters collide with the report's own
text. Measured against the function-block documentation oracle
(`tests/testdata/reports/functionblockdokumentation.html`, ~1 100 icon instances):

- **`≥1` is unusable for `cond-or`.** Condition rows contain literal comparison operators —
  `<`, `>=`, `<>` occur 56× — so a `≥1` group marker sits directly above text like
  `System tid >= Tænd-tidspunkt`. Use `∨`, and `∧` for `cond-and` to keep the pair symmetric.
  (`&` is the exact shape for `cond-and` and is safe — 0 literal `&` in the text — but pairing
  `&` with `∨` reads worse than `∧`/`∨`.)
- **Four assets want arrows.** Reserve the solid arrows for pins (`→` `←`, by far the more common)
  and give links the dashed pair (`⇢` `⇠`); the SVGs make the same distinction with filled vs
  outline arrowheads. Note that event rows print a literal ASCII `->`, which rhymes with `→` but
  is a different character, so the two never actually merge.
- **`#` for `var-enum`** collides with 2 literal `#` in note text. Low severity given a separate
  icon column; swap to `≡` if the surface has no column separation.
- **Two "light" assets.** `var-illuminance` (lux) is the exact `☼`; `var-light-level` (%) then has
  to take `☼%` in text, or `💡` where emoji are allowed.
- **Verified free of collisions** in that oracle: `?`, `!`, `&`, `*`, `✓` — zero literal
  occurrences in visible text.
- **`‼` (U+203C) is emoji-presentation by default** and renders as a colour glyph or tofu in many
  monospace fonts, so the Text column uses `!!`. The same caution applies to `✔` (U+2714) — use
  `✓` (U+2713), which is text-presentation.

### 7.4 What still needs a legend

Six keys are mnemonics rather than likenesses and do not read on sight: `prog-program`,
`prog-subprogram`, `var-counter`, `product-lamp`, `product-socket`, `product-sensor`. The first two
matter most — they are the 8th and 4th most frequent icons in a function-block report (≈12 % of all
instances) — because the SVG is a vertical composite (banner → stem → decision node) that no
character sequence resembles. A text report that uses stand-ins should print a legend, in the
report's own language, for at least these six.

Everything else — ≈87 % of icon instances, and 8 keys alone (`command`, `command-group`,
`condition`, `cond-and`, `event`, `event-group`, `pin-in`, `pin-out`) covering ≈78 % of a
function-block report — reads without one.

### 7.5 What to omit entirely

The section and group markers sit on a row whose label already names them (`Input`, `Output`,
`Indstillinger`, `Interne variable`, `Kommandoer`, `Hændelser`, `Programmer`), so a text renderer
may drop `section-*`, `command-group`, `event-group` and `prog-subprogram`-as-container without
losing information. **The two exceptions are `cond-and` and `cond-or`**: the label is just
`Betingelser` in both cases, and the icon is the only thing carrying the group's logic method — it
must survive into the text form.
