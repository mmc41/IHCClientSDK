# IHC OpenVisual — icon selection reference (`.vis`/`.ifb` element → `Assets/*.svg`)

Companion to [`icons_design.md`](./icons_design.md), which defines our SVG glyph set (semantic
keys like `fb-lk.svg`). This doc maps each `.vis`/`.ifb` XML element — and the vendor
`icon="_0xNN"` code it may carry — onto the SVG a GUI editor should render for it.

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
