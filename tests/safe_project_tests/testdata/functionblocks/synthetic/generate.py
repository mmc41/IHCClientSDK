#!/usr/bin/env python3
# -*- coding: utf-8 -*-
#
# Self-contained, reproducible generator for the synthetic IHC Visual FunctionBlock (.ifb) oracle
# files that live next to it.  Run from anywhere with:  python generate.py
# It reads the SDK's own canonical DTD blocks (ihcclient/src/vis/schema/CanonicalDtdBlocks.dtd) so
# the inline DTD in each oracle is byte-identical to what the engine emits, and writes
# ISO-8859-1 / CRLF / no-trailing-newline bytes matching the vendor .ifb format.
# See README.md for the coverage map.  Regenerate after changing CanonicalDtdBlocks.dtd.
#
#!/usr/bin/env python3
"""
Generator for synthetic IHC Visual FunctionBlock (.ifb) oracle files.

These are INVENTED function blocks that reproduce the exact .ifb
byte format of the vendor install files, to act as byte/structure oracles for the
future FunctionBlockDefinitionBuilder ("a builder is a code-authored CatalogReader").

Byte contract (verified against vendor 1.1.01.ifb):
  - prolog <?xml version="1.0" encoding="ISO-8859-1"?>
  - <!DOCTYPE functionblock[ ... ]>   (NO space before '[')
  - CRLF line endings, NO trailing newline
  - ISO-8859-1 (Latin-1) single-byte encoding
  - DTD lines 3-space indent (verbatim from the SDK's CanonicalDtdBlocks.dtd -> byte identical)
  - body 2-space indent per nesting level; empty elements self-close as ` />`
  - element id = (counter << 8) | typeCode ; counter increments +1 in document pre-order
  - vendor-style LEAN body: attributes equal to their DTD default are omitted (the reader
    re-materializes them via DtdProcessing.Parse)
"""
import os, re

_HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(_HERE, "..", "..", "..", "..", ".."))
DTD_SRC = os.path.join(REPO, "ihcclient", "src", "vis", "schema", "CanonicalDtdBlocks.dtd")
OUT_DIR = _HERE

# Element type-code low byte (from ihcclient/src/vis/schema/TypeCode.cs)
TYPECODE = {
    "functionblock": 0x28, "inputs": 0x23, "outputs": 0x24, "settings": 0x25,
    "internalsettings": 0x29, "programs": 0x26, "program_simple": 0x1e, "program_sub": 0x1f,
    "events": 0x64, "event": 0xc8, "event_power": 0xc8, "actions": 0x66,
    "conditions": 0x65, "condition": 0xc9, "action": 0xca,
    "resource_input": 0x11, "resource_output": 0x12, "resource_scene": 0x4a,
    "resource_timer": 0x10, "resource_enum": 0x0f, "resource_date": 0x0e,
    "enum_definition": 0x47, "enum_value": 0x48, "scenes": 0x49,
    "resource_flag": 0x0a, "resource_integer": 0x0b, "resource_counter": 0x0c,
    "resource_time": 0x0d, "resource_light_level": 0x13, "resource_temperature": 0x14,
    "resource_floating_point": 0x15, "resource_light": 0x16, "resource_timertime": 0x17,
    "resource_weekday": 0x09, "resource_holiday": 0x20, "resource_humidity_level": 0x27,
}

# ---- load byte-exact canonical DTD blocks, keyed by tag -----------------------------------
def load_dtd_blocks(path):
    text = open(path, encoding="latin-1").read().replace("\r\n", "\n").replace("\r", "\n")
    blocks, tag, cur = {}, None, []
    for line in text.split("\n"):
        if "<!ELEMENT " in line:
            if tag is not None:
                blocks[tag] = "\n".join(cur).rstrip("\n")
            m = re.search(r"<!ELEMENT\s+(\S+)\s", line)
            tag, cur = m.group(1), [line]
        elif tag is not None:
            cur.append(line)
    if tag is not None:
        blocks[tag] = "\n".join(cur).rstrip("\n")
    return blocks

DTD = load_dtd_blocks(DTD_SRC)

# ---- element model -----------------------------------------------------------------------
class Ref:
    """Marker: an attribute value that is an IDREF to another element (resolved post-id-assign)."""
    def __init__(self, target): self.target = target

class E:
    def __init__(self, tag, attrs=None, children=None):
        self.tag = tag
        self.attrs = list(attrs or [])       # list of (name, value|Ref)
        self.children = list(children or [])
        self.id = None                        # assigned token string

def hextoken(value):
    return "_0x" + format(value, "x")         # lowercase, leading zeros stripped

def assign_ids(root, start_counter):
    counter = [start_counter]
    def walk(e):
        code = TYPECODE[e.tag]
        e.id = hextoken((counter[0] << 8) | code)
        counter[0] += 1
        for c in e.children:
            walk(c)
    walk(root)

def esc(s):
    s = (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
          .replace('"', "&quot;").replace("'", "&apos;"))
    return s.replace("\r", "&#xD;").replace("\n", "&#xA;").replace("\t", "&#x9;")

def emit(e, depth, out):
    ind = "  " * depth
    parts = [f' id="{e.id}"']
    for name, val in e.attrs:
        v = val.target.id if isinstance(val, Ref) else esc(str(val))
        parts.append(f' {name}="{v}"')
    a = "".join(parts)
    if e.children:
        out.append(f"{ind}<{e.tag}{a}>")
        for c in e.children:
            emit(c, depth + 1, out)
        out.append(f"{ind}</{e.tag}>")
    else:
        out.append(f"{ind}<{e.tag}{a} />")

def dtd_order(root):
    seen = []
    def walk(e):
        if e.tag not in seen:
            seen.append(e.tag)
        for c in e.children:
            walk(c)
    walk(root)
    hoist = [t for t in ("enum_definition", "enum_value") if t in seen]
    return hoist + [t for t in seen if t not in hoist]

def build_ifb(root, start_counter=0x51):
    assign_ids(root, start_counter)
    lines = ['<?xml version="1.0" encoding="ISO-8859-1"?>', "<!DOCTYPE functionblock["]
    for tag in dtd_order(root):
        if tag not in DTD:
            raise SystemExit(f"No canonical DTD block for tag '{tag}'")
        lines.extend(DTD[tag].split("\n"))
    lines.append("]>")
    emit(root, 0, lines)
    return "\r\n".join(lines).encode("latin-1")   # CRLF, no trailing newline, ISO-8859-1

def write(name, root, start_counter=0x51):
    data = build_ifb(root, start_counter)
    path = os.path.join(OUT_DIR, name)
    with open(path, "wb") as f:
        f.write(data)
    print(f"wrote {name}: {len(data)} bytes, {data.count(b'\r\n')} CRLF, "
          f"trailing_newline={data.endswith(b'\n')}")

# ==========================================================================================
# Shorthand builders for the fixed vendor program-graph skeleton
# ==========================================================================================
def container(tag, name, icon, note, *children):
    return E(tag, [("name", name), ("icon", icon), ("note", note)], list(children))

def res_input(name, icon="_0x36", note=""):
    a = [("name", name), ("icon", icon)]
    if note: a.append(("note", note))
    return E("resource_input", a)

def res_output(name, icon="_0x39", note=""):
    a = [("name", name), ("icon", icon)]
    if note: a.append(("note", note))
    return E("resource_output", a)

def res_scene(name, note="", extra=None):
    a = [("name", name)]
    if note: a.append(("note", note))
    a += list(extra or [])
    return E("resource_scene", a)

def res_timer(name, h, m, s, ms, icon="_0x43", backup=False):
    a = [("name", name)]
    if backup: a.append(("backup", "yes"))
    a += [("icon", icon), ("hour", str(h)), ("minute", str(m)), ("second", str(s)), ("millisecond", str(ms))]
    return E("resource_timer", a)

def event(name, link1, method, note="", link2=None):
    a = [("name", name), ("icon", "_0xc")]
    if note: a.append(("note", note))
    a.append(("link1", Ref(link1)))
    if link2 is not None: a.append(("link2", Ref(link2)))
    a.append(("method", method))
    return E("event", a)

def action(name, link1, method, note="", link2=None):
    a = [("name", name), ("icon", "_0x9")]
    if note: a.append(("note", note))
    a.append(("link1", Ref(link1)))
    if link2 is not None: a.append(("link2", Ref(link2)))
    a.append(("method", method))
    return E("action", a)

def condition(name, link1, method, note="", link2=None, operand=None):
    a = [("name", name), ("icon", "_0x1a")]
    if note: a.append(("note", note))
    a.append(("link1", Ref(link1)))
    if link2 is not None: a.append(("link2", Ref(link2)))
    a.append(("method", method))
    ch = [operand] if operand is not None else []
    return E("condition", a, ch)

def conditions(*conds, ctype=None):
    a = [("name", "Betingelser"), ("icon", "_0x16"), ("note", "Betingelser der testes logisk")]
    if ctype: a.append(("type", ctype))
    return E("conditions", a, list(conds))

def actions_true(*acts):
    # Branch name pinned by the committed FbProgramBuilder docstring; note is an original paraphrase.
    return E("actions", [("name", "Kommandoer ved betingelser sande"), ("icon", "_0x8"),
                         ("note", "Kommandoer der udføres når betingelserne er opfyldt"), ("type", "_0x1")], list(acts))

def actions_false(*acts):
    return E("actions", [("name", "Kommandoer ved betingelser falske"), ("icon", "_0x8"),
                         ("note", "Kommandoer der udføres når betingelserne ikke er opfyldt")], list(acts))

def program_sub(conds, whentrue, whenfalse):
    return E("program_sub", [("name", "Under program"), ("icon", "_0x7")], [conds, whentrue, whenfalse])

def root_actions(*children):
    return E("actions", [("name", "Kommandoer"), ("icon", "_0x8"),
                         ("note", "Kommandoer der udføres når en hændelse indtræffer"), ("type", "_0x2")],
             list(children))

def events(*evs):
    return E("events", [("name", "Hændelser"), ("icon", "_0xb"), ("note", "Hændelser der udløser programmet")], list(evs))

def program_simple(name, ev, acts):
    return E("program_simple", [("name", name), ("icon", "_0x7")], [ev, acts])


# ---- extra resource / event helpers (enum, date, flag, power event) ----------------------
def event_power(name, note=""):
    a = [("name", name), ("icon", "_0xc")]
    if note:
        a.append(("note", note))
    return E("event_power", a)

def res_date(name, y, m, d, icon="_0x29", backup=False, note=""):
    a = [("name", name)]
    if backup:
        a.append(("backup", "yes"))
    a.append(("icon", icon))
    if note:
        a.append(("note", note))
    a += [("year", str(y)), ("month", str(m)), ("day", str(d))]
    return E("resource_date", a)

def res_flag(name, inivalue=None, icon="_0x33", note="", backup=False):
    a = [("name", name)]
    if backup:
        a.append(("backup", "yes"))
    a.append(("icon", icon))
    if note:
        a.append(("note", note))
    if inivalue:
        a.append(("inivalue", inivalue))
    return E("resource_flag", a)

def enum_def(name, *values):
    return E("enum_definition", [("name", name)], list(values))

def enum_val(name, index=None):
    a = [("name", name)]
    if index is not None:
        a.append(("index", str(index)))
    return E("enum_value", a)

def res_enum(name, typedef, inivalue, icon="_0x22", note="", backup=False):
    a = [("name", name), ("typedef", Ref(typedef)), ("inivalue", Ref(inivalue))]
    if backup:
        a.append(("backup", "yes"))
    a.append(("icon", icon))
    if note:
        a.append(("note", note))
    return E("resource_enum", a)


# ==========================================================================================
# File 1 — synthetic_fb01_toggle.ifb : the comprehensive core block.
# Covers: full master identity (VendorMaster/Locked/MasterProgrammer/MasterDate/Note multiline+Latin1),
#         2 inputs, 2 outputs, timer setting (TimerHms), internal timer, deep program graph
#         (program_sub, conditions(and), true/false branches, leaf action in root actions),
#         link1+method wiring AND link2 wiring, multiple program_simple.
# ==========================================================================================
push     = res_input("Tryk",       note="Aktivér for at skifte udgangen.\r\n(Udfyldes af montøren)")
forceoff = res_input("Tvangssluk", note="Tvinger udgangen slukket.")
lamp     = res_output("Lampe",   note="Tilsluttes en lampe eller stikkontakt.")
onpulse  = res_output("ON puls", note="Kort puls når udgangen tændes.")
autooff  = res_timer("Sluktimer", 0, 5, 0, 0)
debounce = res_timer("Afvisningstid", 0, 0, 0, 200)

inputs   = container("inputs",  "Input",         "_0x4",  "Variablene i denne gruppering er indgange til blokken", push, forceoff)
outputs  = container("outputs", "Output",        "_0x14", "Variablene i denne gruppering er udgange fra blokken", lamp, onpulse)
settings = container("settings","Indstillinger", "_0xd",  "Indstillinger som brugeren kan ændre", autooff)
internal = container("internalsettings", "Interne variable", "_0x13", "Private variable til blokkens eget brug", debounce)

p1 = program_simple("Skift",
    events(event("%P -> ON", push, "_0xa", note="Start når %P skifter til ON")),
    root_actions(
        program_sub(
            conditions(condition("%P = OFF", lamp, "_0x14", note="Betingelse: %P er slukket")),
            actions_true(
                action("%P = ON", lamp,    "_0xa", note="Tænder udgangen"),
                action("%P = ON", onpulse, "_0xa", note="Afgiver ON-puls"),
            ),
            actions_false(
                action("%P = OFF", lamp, "_0x14", note="Slukker udgangen"),
                action("%P = %S", lamp, "_0x1e", note="Sætter %P lig med %S", link2=onpulse),
            ),
        ),
        action("Aktivér nedtælling på %P", autooff, "_0xbe", note="Starter sluktimeren"),
    ),
)
p2 = program_simple("Tvangssluk",
    events(event("%P -> ON", forceoff, "_0xa", note="Start når %P skifter til ON")),
    root_actions(action("%P = OFF", lamp, "_0x14", note="Slukker udgangen")),
)
programs = E("programs", [("name", "Programmer"), ("icon", "_0x19"),
                          ("note", "Gruppering af blokkens programmer")], [p1, p2])

toggle = E("functionblock", [
    ("name", "9.1.01.a. Toggle lamp"),
    ("master_schneider_electric", "yes"),
    ("master_type", "9.1.01"),
    ("master_version", "a"),
    ("master_name", "Toggle lamp"),
    ("master_programmer", "Morten Christensen"),
    ("master_date_year", "2026"), ("master_date_month", "2"), ("master_date_day", "1"),
    ("locked", "yes"),
    ("icon", "_0xe"),
    ("note", '9.1.01.a. Toggle lamp\r\n\r\nSyntetisk demoblok til afprøvning. Tryk på %P for at tænde '
             'eller slukke udgangen.\r\nMarkér linjen og tryk "F1" for at se en beskrivelse.'),
], [inputs, outputs, settings, internal, programs])

write("synthetic_fb01_toggle.ifb", toggle)

# ==========================================================================================
# File 2 — synthetic_fb02_scene.ifb : scenes + backup + attribute escape hatch + empty container + leaf-only programs.
# Covers: AddOutput("resource_scene", …) (two scenes), a scene-recall action, Backup on a setting,
#         .Attribute escape hatch (hide_dialog / note-2 on a scene), an EMPTY internalsettings container,
#         VendorMaster=false (master_schneider_electric omitted), multiple leaf-only programs.
# ==========================================================================================
trigger  = res_input("Udløser", note="Aktiverer scenariefremkaldelsen.")
relay    = res_output("Relæ", note="Slutter eller bryder belastningen.")
scene_on = res_scene("Scenarie tændt", note="Fremkaldes når udgangen tændes.", extra=[("hide_dialog", "yes")])
scene_off = res_scene("Scenarie slukket", note="Fremkaldes når udgangen slukkes.", extra=[("note-2", "Vises kun for avancerede brugere")])
hold      = res_timer("Holdetid", 0, 2, 30, 0, backup=True)

inputs2   = container("inputs",  "Input",         "_0x4",  "Indgange til scenarieblokken", trigger)
outputs2  = container("outputs", "Output",        "_0x14", "Udgange og scenarier fra blokken", relay, scene_on, scene_off)
settings2 = container("settings","Indstillinger", "_0xd",  "Indstillinger som brugeren kan ændre", hold)
internal2 = container("internalsettings", "Interne variable", "_0x13", "Private variable til blokkens eget brug")  # empty → self-closing

p_on = program_simple("Fremkald tændt",
    events(event("%P -> ON", trigger, "_0xa", note="Start når %P skifter til ON")),
    root_actions(
        action("%P = ON", relay, "_0xa", note="Tænder relæet"),
        action("Fremkald %P", scene_on, "_0xa", note="Fremkalder scenariet"),
    ),
)
p_off = program_simple("Fremkald slukket",
    events(event("%P -> OFF", trigger, "_0x14", note="Start når %P skifter til OFF")),
    root_actions(
        action("%P = OFF", relay, "_0x14", note="Slukker relæet"),
        action("Fremkald %P", scene_off, "_0xa", note="Fremkalder scenariet"),
    ),
)
programs2 = E("programs", [("name", "Programmer"), ("icon", "_0x19"),
                           ("note", "Gruppering af blokkens programmer")], [p_on, p_off])

scene = E("functionblock", [
    ("name", "9.1.02.a. Scene recall"),
    ("master_type", "9.1.02"), ("master_version", "a"), ("master_name", "Scene recall"),
    ("master_programmer", "Morten Christensen"),
    ("master_date_year", "2026"), ("master_date_month", "3"), ("master_date_day", "12"),
    ("locked", "yes"),
    ("icon", "_0xe"),
    ("note", "9.1.02.a. Scene recall\r\n\r\nFremkalder et scenarie når udløseren aktiveres."),
], [inputs2, outputs2, settings2, internal2, programs2])

write("synthetic_fb02_scene.ifb", scene)

# ==========================================================================================
# File 3 — synthetic_fb03_mode.ifb : enum definitions + resource_enum + embedded enum operand + nested sub + empty branch.
# Covers: top-level enum_definition with two enum_value children (one indexed, one index-0 elided),
#         a resource_enum setting (Enum(typedef,inivalue) + Backup), an embedded resource_enum operand inside a
#         condition (AddEnumOperand → condition link2), a nested program_sub inside a true branch, and a
#         self-closing (empty) false branch.
# ==========================================================================================
auto    = enum_val("Automatik", index=1)
manuel  = enum_val("Manuel")                       # index 0 → elided
funcsel = enum_def("Funktionsvalg", auto, manuel)

indgang = res_input("Indgang", note="Styreindgang til blokken.")
udgang  = res_output("Udgang", note="Følger eller kipper afhængigt af tilstanden.")
sel     = res_enum("Funktionsvalg", typedef=funcsel, inivalue=manuel, backup=True, icon="_0x22",
                   note="Vælg mellem automatisk og manuel styring.")

inputs3   = container("inputs",  "Input",         "_0x4",  "Indgange til vælgerblokken", indgang)
outputs3  = container("outputs", "Output",        "_0x14", "Udgange fra vælgerblokken", udgang)
settings3 = container("settings","Indstillinger", "_0xd",  "Indstillinger som brugeren kan ændre", sel)
internal3 = container("internalsettings", "Interne variable", "_0x13", "Private variable til blokkens eget brug")  # empty

operand = res_enum("Enumerator", typedef=funcsel, inivalue=auto, icon="_0x22")
inner_sub = program_sub(
    conditions(condition("%P = ON", indgang, "_0xa", note="Betingelse: %P er tændt")),
    actions_true(action("Kip %P", udgang, "_0x23", note="Skifter %P til modsat værdi")),
    actions_false(),                                # empty false branch → self-closing actions
)
outer_sub = program_sub(
    conditions(condition("%P = %S", sel, "_0x1e", note="Betingelse: valgt tilstand er %S",
                         link2=operand, operand=operand)),
    actions_true(inner_sub),                        # nested program_sub inside the true branch
    actions_false(action("%P = %S", udgang, "_0x1e", note="Sætter %P lig med %S", link2=indgang)),
)
p_sel = program_simple("Vælg funktion",
    events(event("%P bliver ændret", indgang, "_0x96", note="Start når %P skifter værdi")),
    root_actions(outer_sub),
)
programs3 = E("programs", [("name", "Programmer"), ("icon", "_0x19"),
                           ("note", "Gruppering af blokkens programmer")], [p_sel])

mode = E("functionblock", [
    ("name", "9.2.01.a. Mode selector"),
    ("master_schneider_electric", "yes"),
    ("master_type", "9.2.01"), ("master_version", "a"), ("master_name", "Mode selector"),
    ("master_programmer", "Morten Christensen"),
    ("master_date_year", "2026"), ("master_date_month", "4"), ("master_date_day", "3"),
    ("locked", "yes"),
    ("icon", "_0xe"),
    ("note", "9.2.01.a. Mode selector\r\n\r\nKan fungere som enten kip- eller følgefunktion afhængigt af valgt tilstand."),
], [funcsel, inputs3, outputs3, settings3, internal3, programs3])

write("synthetic_fb03_mode.ifb", mode)

# ==========================================================================================
# File 4 — synthetic_fb04_holiday.ifb : date variables + power event + or-conditions + flag with inivalue.
# Covers: resource_date settings (DateYmd + Backup) and an internal resource_date, a resource_flag with a
#         non-default Inivalue, a power-up trigger (event_power / AddPowerEvent), a conditions list with
#         type="or" and two conditions, and multiple programs.
# ==========================================================================================
aktiver   = res_input("Aktivér", note="Slår ferieprogrammet til.")
aktiv     = res_output("Aktiv", note="Er tændt i ferieperioden.")
startdato = res_date("Startdato", 2026, 6, 1,  backup=True, note="Ferieperiodens første dag.")
stopdato  = res_date("Stopdato",  2026, 8, 31, backup=True, note="Ferieperiodens sidste dag.")
bemyndiget = res_flag("Bemyndiget", inivalue="on", note="Angiver om ferieprogrammet er tilladt.")
dagsdato  = res_date("Dags dato", 2000, 1, 1, note="Opdateres løbende af controlleren.")

inputs4   = container("inputs",  "Input",         "_0x4",  "Indgange til ferieblokken", aktiver)
outputs4  = container("outputs", "Output",        "_0x14", "Udgange fra ferieblokken", aktiv)
settings4 = container("settings","Indstillinger", "_0xd",  "Indstillinger som brugeren kan ændre", startdato, stopdato, bemyndiget)
internal4 = container("internalsettings", "Interne variable", "_0x13", "Private variable til blokkens eget brug", dagsdato)

p_boot = program_simple("Opstart",
    E("events", [("name", "Hændelser"), ("icon", "_0xb"), ("note", "Hændelser der udløser programmet")],
      [event_power("Opstart", note="Start programmet når controlleren tændes")]),
    root_actions(action("%P = OFF", aktiv, "_0x14", note="Nulstiller udgangen ved opstart")),
)
p_check = program_simple("Kontrollér periode",
    events(event("%P -> ON", aktiver, "_0xa", note="Start når %P skifter til ON")),
    root_actions(program_sub(
        conditions(
            condition("%P = ON", bemyndiget, "_0xa", note="Betingelse: programmet er bemyndiget"),
            condition("%P bliver ændret", aktiver, "_0x96", note="Betingelse: indgangen er ændret"),
            ctype="or",
        ),
        actions_true(action("%P = ON", aktiv, "_0xa", note="Tænder udgangen")),
        actions_false(action("%P = OFF", aktiv, "_0x14", note="Slukker udgangen")),
    )),
)
programs4 = E("programs", [("name", "Programmer"), ("icon", "_0x19"),
                           ("note", "Gruppering af blokkens programmer")], [p_boot, p_check])

holiday = E("functionblock", [
    ("name", "9.3.01.a. Holiday schedule"),
    ("master_type", "9.3.01"), ("master_version", "a"), ("master_name", "Holiday schedule"),
    ("master_programmer", "Morten Christensen"),
    ("master_date_year", "2026"), ("master_date_month", "5"), ("master_date_day", "20"),
    ("locked", "yes"),
    ("icon", "_0xe"),
    ("note", "9.3.01.a. Holiday schedule\r\n\r\nHolder en udgang tændt i en defineret ferieperiode."),
], [inputs4, outputs4, settings4, internal4, programs4])

write("synthetic_fb04_holiday.ifb", holiday)

# ==========================================================================================
# File 6 — synthetic_fb06_sensor.ifb : value-type breadth + DisplayName override + value-type input.
# Covers: DisplayName override (block name attr differs from the composed "{type}.{ver}. {name}"),
#         AddInput with an explicit value-type tag (a resource_temperature pin under inputs),
#         a spread of registry value families with heterogeneous attribute shapes
#         (resource_temperature/counter/integer without icon, resource_weekday/time with icon),
#         Inivalue on a float / weekday enum / integer, and a link2 wired to a value operand.
# ==========================================================================================
nulstil = res_input("Nulstil", note="Nulstiller alarmen.")
maalt   = E("resource_temperature", [("name", "Målt temperatur"), ("note", "Aktuelt målt temperatur."), ("inivalue", "20.00")])
alarm   = res_output("Alarm", note="Aktiv når grænseværdien overskrides.")
graense = E("resource_temperature", [("name", "Grænseværdi"), ("note", "Temperaturgrænse for alarm."), ("inivalue", "21.50")])
udloes  = E("resource_counter", [("name", "Udløsninger"), ("note", "Antal gange alarmen er udløst."), ("inivalue", "5")])
ugedag  = E("resource_weekday", [("name", "Aktiv ugedag"), ("icon", "_0x2c"), ("note", "Ugedag hvor blokken overvåger."), ("inivalue", "friday")])
taerskel = E("resource_integer", [("name", "Tærskel"), ("note", "Heltalstærskel for gentagne alarmer."), ("inivalue", "10")])
forsink = E("resource_time", [("name", "Forsinkelse"), ("icon", "_0x2f"), ("note", "Forsinkelse før alarmen udløses."),
            ("hour", "0"), ("minute", "0"), ("second", "30")])

inputs6   = container("inputs",  "Input",         "_0x4",  "Indgange til sensorpanelet", nulstil, maalt)
outputs6  = container("outputs", "Output",        "_0x14", "Udgange fra sensorpanelet", alarm)
settings6 = container("settings","Indstillinger", "_0xd",  "Indstillinger som brugeren kan ændre", graense, udloes, ugedag, taerskel)
internal6 = container("internalsettings", "Interne variable", "_0x13", "Private variable til blokkens eget brug", forsink)

p_watch = program_simple("Overvågning",
    events(event("%P bliver ændret", maalt, "_0x96", note="Start når den målte temperatur ændres")),
    root_actions(program_sub(
        conditions(condition("%P > %S", maalt, "_0x64", note="Betingelse: målt temperatur over grænseværdien",
                             link2=graense)),
        actions_true(
            action("%P = ON", alarm, "_0xa", note="Udløser alarmen"),
            action("Tæl %P op", udloes, "_0xbf", note="Tæller antal udløsninger"),
        ),
        actions_false(action("%P = OFF", alarm, "_0x14", note="Rydder alarmen")),
    )),
)
programs6 = E("programs", [("name", "Programmer"), ("icon", "_0x19"),
                           ("note", "Gruppering af blokkens programmer")], [p_watch])

sensor = E("functionblock", [
    ("name", "Sensorpanel (udvidet)"),   # DisplayName override — NOT the composed "9.4.01.a. Sensor panel"
    ("master_type", "9.4.01"), ("master_version", "a"), ("master_name", "Sensor panel"),
    ("master_programmer", "Morten Christensen"),
    ("master_date_year", "2026"), ("master_date_month", "6"), ("master_date_day", "30"),
    ("locked", "yes"),
    ("icon", "_0xe"),
    ("note", "Sensorpanel (udvidet)\r\n\r\nOvervåger en temperatur og udløser en alarm når grænseværdien overskrides."),
], [inputs6, outputs6, settings6, internal6, programs6])

write("synthetic_fb06_sensor.ifb", sensor)

# ==========================================================================================
# File 5 — synthetic_fb05_empty.ifb : the empty "Tom blok" scaffold (peer of Data/fb.def).
# Covers: AsEmptyTemplate — five containers in fixed order + one empty program_simple
#         (events+actions), explicit conventional container icons, block icon _0xf.
# ==========================================================================================
empty = E("functionblock", [("name", "Tomt demoblok"), ("icon", "_0xf")], [
    E("inputs",           [("name", "Input"),            ("icon", "_0x4"),  ("note", "Indgange til funktionsblokken")]),
    E("outputs",          [("name", "Output"),           ("icon", "_0x14"), ("note", "Udgange fra funktionsblokken")]),
    E("settings",         [("name", "Indstillinger"),    ("icon", "_0xd"),  ("note", "Indstillinger som brugeren kan ændre")]),
    E("internalsettings", [("name", "Interne variable"), ("icon", "_0x13"), ("note", "Private variable til blokkens eget brug")]),
    E("programs",         [("name", "Programmer"),       ("icon", "_0x19"), ("note", "Gruppering af blokkens programmer")], [
        E("program_simple", [("name", "Program"), ("icon", "_0x7")], [
            E("events",  [("name", "Hændelser"), ("icon", "_0xb"), ("note", "Hændelser der udløser programmet")]),
            E("actions", [("name", "Kommandoer"), ("icon", "_0x8"), ("note", "Kommandoer der udføres"), ("type", "_0x2")]),
        ]),
    ]),
])
write("synthetic_fb05_empty.ifb", empty)
