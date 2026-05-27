"""
Extracts the gameplay data tables from voidborne-flowchart-v3.html into JSON files
under Design Documents/GameDesign/data/. This is the single source of truth for the
Unity bulk ScriptableObject generator.

Re-run whenever the HTML design doc changes.

What it extracts:
  - D                — main item dictionary (sources, machines, components, products)
  - BUILD_MATERIALS  — building material registry (stone, wood, iron, ...)
  - FORM_TEMPLATES   — block form variants (cube, slab, panel, stairs, door)
  - DECO_BLOCKS      — decorative blocks per archetype
  - SYNERGY_ALTS     — cross-archetype alternate recipes
  - NPC_DATA         — bosses, fodder enemies, wildlife, named NPCs, traders
  - All *_IDS sets   — category memberships (FOOD/POWER/WEAPON/ARMOR/...)

Output files:
  data/items.json      — full D dict (after building blocks + deco blocks generated and synergy alts applied)
  data/build_materials.json
  data/form_templates.json
  data/deco_blocks.json
  data/synergy_alts.json
  data/categories.json — { "food": [...], "power": [...], "weapon": [...], ... }
  data/npcs.json
  data/summary.json    — counts + sanity-check fields
"""

import ast
import json
import re
from pathlib import Path

HERE = Path(__file__).resolve().parent
DATA = HERE.parent / "data"
HTML = HERE.parent.parent / "voidborne-flowchart-v3.html"

DATA.mkdir(parents=True, exist_ok=True)


def js_object_to_python(text: str) -> str:
    """Convert a JS object/array literal source string into a Python literal source
    string parsable by ast.literal_eval.

    Handles:
      - Single-line // comments
      - Multi-line /* ... */ comments
      - true/false/null -> True/False/None
      - Bare identifier keys -> quoted "key"
      - Trailing commas in arrays and dicts (ast.literal_eval allows them)

    Limitations:
      - Does NOT handle template literals, expressions, or function calls.
        Caller must pre-strip those.
    """
    # 1) Strip block comments first (so they don't confuse line-comment regex).
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.DOTALL)

    # 2) Strip line comments — only outside string literals. We do a simple
    #    state machine to avoid clobbering URLs in strings.
    out = []
    i = 0
    in_str = False
    quote = ""
    while i < len(text):
        c = text[i]
        if in_str:
            if c == "\\" and i + 1 < len(text):
                out.append(c)
                out.append(text[i + 1])
                i += 2
                continue
            if c == quote:
                in_str = False
            out.append(c)
            i += 1
            continue
        if c in ('"', "'"):
            in_str = True
            quote = c
            out.append(c)
            i += 1
            continue
        if c == "/" and i + 1 < len(text) and text[i + 1] == "/":
            # skip to end of line
            j = text.find("\n", i)
            if j == -1:
                break
            i = j
            continue
        out.append(c)
        i += 1
    text = "".join(out)

    # 3) Replace JS literals with Python equivalents.
    text = re.sub(r"\btrue\b", "True", text)
    text = re.sub(r"\bfalse\b", "False", text)
    text = re.sub(r"\bnull\b", "None", text)
    text = re.sub(r"\bundefined\b", "None", text)

    # 4) Quote bare identifier keys. Match "{key:" or ",key:" or whitespace
    #    surrounding the key. We only do this outside strings — but since we
    #    already stripped comments and the regex requires a leading punctuator,
    #    the false-positive rate inside strings is acceptable for our data.
    text = re.sub(
        r"([{,]\s*)([A-Za-z_$][\w$]*)(\s*:)",
        r'\1"\2"\3',
        text,
    )

    # 5) Convert single-quoted strings to double-quoted (ast.literal_eval
    #    handles both, but JS strings sometimes embed unescaped " inside ',
    #    which would break a naive swap — so we leave them alone).
    return text


def slice_declaration(html: str, name: str) -> str:
    """Find `const NAME = ...;` and return the value source between `=` and the
    matching closing brace/bracket followed by `;`.

    Uses simple bracket counting (not aware of strings beyond the outer match).
    """
    pattern = rf"const\s+{re.escape(name)}\s*="
    m = re.search(pattern, html)
    if not m:
        raise ValueError(f"Could not find `const {name} = ...`")
    start = m.end()
    # Skip whitespace
    while start < len(html) and html[start] in " \t\n":
        start += 1
    if html[start] not in "{[":
        raise ValueError(f"`const {name} = ...` does not start with a brace/bracket")
    opener = html[start]
    closer = "}" if opener == "{" else "]"
    depth = 0
    in_str = False
    quote = ""
    i = start
    while i < len(html):
        c = html[i]
        if in_str:
            if c == "\\" and i + 1 < len(html):
                i += 2
                continue
            if c == quote:
                in_str = False
            i += 1
            continue
        # Skip line comments
        if c == "/" and i + 1 < len(html) and html[i + 1] == "/":
            j = html.find("\n", i)
            if j == -1:
                break
            i = j
            continue
        # Skip block comments
        if c == "/" and i + 1 < len(html) and html[i + 1] == "*":
            j = html.find("*/", i + 2)
            if j == -1:
                break
            i = j + 2
            continue
        if c in ('"', "'"):
            in_str = True
            quote = c
            i += 1
            continue
        if c == opener:
            depth += 1
        elif c == closer:
            depth -= 1
            if depth == 0:
                return html[start : i + 1]
        i += 1
    raise ValueError(f"Unterminated `{name}` declaration")


def parse_js_data(html: str, name: str):
    """Slice + convert + eval a top-level JS const declaration."""
    raw = slice_declaration(html, name)
    pythonised = js_object_to_python(raw)
    try:
        return ast.literal_eval(pythonised)
    except Exception as exc:  # pragma: no cover - debugging aid
        # Dump the converted source so we can inspect what failed.
        dump_path = DATA / f"_debug_{name}.py"
        dump_path.write_text(pythonised, encoding="utf-8")
        raise RuntimeError(
            f"ast.literal_eval failed for {name}: {exc}. "
            f"Converted source dumped to {dump_path}"
        ) from exc


def parse_id_set(html: str, name: str) -> list:
    """Parse `const X_IDS = new Set([ 'a', 'b', ... ]);`."""
    pattern = rf"const\s+{re.escape(name)}\s*=\s*new\s+Set\s*\("
    m = re.search(pattern, html)
    if not m:
        raise ValueError(f"Could not find `const {name} = new Set(...)`")
    # Find the array literal after the `(`.
    start = m.end()
    while start < len(html) and html[start] in " \t\n":
        start += 1
    if html[start] != "[":
        raise ValueError(f"`const {name} = new Set(...)` does not contain an array")
    depth = 0
    in_str = False
    quote = ""
    i = start
    while i < len(html):
        c = html[i]
        if in_str:
            if c == "\\" and i + 1 < len(html):
                i += 2
                continue
            if c == quote:
                in_str = False
            i += 1
            continue
        if c == "/" and i + 1 < len(html) and html[i + 1] == "/":
            j = html.find("\n", i)
            if j == -1:
                break
            i = j
            continue
        if c in ('"', "'"):
            in_str = True
            quote = c
            i += 1
            continue
        if c == "[":
            depth += 1
        elif c == "]":
            depth -= 1
            if depth == 0:
                raw = html[start : i + 1]
                pythonised = js_object_to_python(raw)
                return list(ast.literal_eval(pythonised))
        i += 1
    raise ValueError(f"Unterminated `{name}` array")


def replicate_build_block_loop(d: dict, build_materials: list, form_templates: list):
    """Mirror the JS loop at HTML lines 3271-3312 that auto-generates building blocks."""
    for m in build_materials:
        for f in form_templates:
            block_id = f"{m['id']}_{f['form']}"
            qty = max(1, round(m["baseQty"] * f["mult"]))
            inputs = [{"id": m["base"], "qty": qty}]
            for ex in f.get("extras", []):
                inputs.append(ex)
            recipes = [{"via": m["via"], "inputs": inputs, "notes": f.get("notes")}]

            # Multi-recipe alts for cubes/slabs
            if f["form"] in ("cube", "slab"):
                if m["via"] != "workbench":
                    recipes.append(
                        {
                            "via": "workbench",
                            "inputs": [{"id": m["base"], "qty": qty + 1}],
                            "notes": "Hand-shaped — wasteful.",
                        }
                    )
            # Special-case cube alts (mirrors the JS exactly)
            if f["form"] == "cube" and m["id"] == "stone":
                recipes.append({"via": "crusher", "inputs": [{"id": "stone", "qty": 2}], "notes": "Crushed-stone block — rough finish."})
            if f["form"] == "cube" and m["id"] == "wood":
                recipes.append({"via": "workbench", "inputs": [{"id": "wood", "qty": 2}], "notes": "From raw wood."})
            if f["form"] == "cube" and m["id"] == "brick":
                recipes.append({"via": "workbench", "inputs": [{"id": "raw_soil", "qty": 3}, {"id": "water", "qty": 1}, {"id": "charcoal", "qty": 1}], "notes": "Sun-dried adobe — no kiln needed."})
            if f["form"] == "cube" and m["id"] == "iron":
                recipes.append({"via": "forge", "inputs": [{"id": "iron_ingot", "qty": 3}], "notes": "Forge-cast."})
            if f["form"] == "cube" and m["id"] == "glass":
                recipes.append({"via": "furnace", "inputs": [{"id": "silica_sand", "qty": 4}], "notes": "Direct cast — skips the plate."})

            d[block_id] = {
                "kind": "product",
                "name": f"{m['label']} {f['label']}",
                "role": f"building — {f['form']}",
                "recipes": recipes,
                "_build": True,
                "_buildColor": m["color"],
            }


def add_deco_blocks(d: dict, deco_blocks: dict):
    """Mirror the JS loop at HTML lines 3540-3542."""
    for block_id, n in deco_blocks.items():
        d[block_id] = {
            "kind": "product",
            "name": n["name"],
            "role": n["role"],
            "recipes": n["recipes"],
            "_build": True,
            "_buildColor": "build",
            "_deco": True,
        }


def normalize_recipes(d: dict):
    """Mirror the JS normalize loop at HTML lines 3544-3552."""
    for n in d.values():
        if "recipes" not in n and "inputs" in n:
            n["recipes"] = [
                {"via": n.get("via"), "inputs": n["inputs"], "notes": n.get("notes")}
            ]
        if "recipes" not in n and n.get("kind") not in ("source", "machine"):
            n["recipes"] = []


def apply_synergy_alts(d: dict, synergy_alts: list):
    """Mirror the JS loop at HTML lines 3581-3584."""
    for alt in synergy_alts:
        target = d.get(alt["id"])
        if target and target.get("recipes") is not None:
            target["recipes"].append(alt["recipe"])


def categorise_items(d: dict, id_sets: dict) -> dict:
    """Build a flat { item_id: [category, ...] } reverse index plus the
    forward { category: [item_id, ...] } map."""
    forward = {cat: sorted(ids) for cat, ids in id_sets.items()}
    return forward


def main() -> None:
    if not HTML.exists():
        raise SystemExit(f"HTML not found: {HTML}")

    html = HTML.read_text(encoding="utf-8")

    print("Parsing const D ...")
    d = parse_js_data(html, "D")
    print(f"  -> {len(d)} initial items")

    print("Parsing BUILD_MATERIALS / FORM_TEMPLATES ...")
    build_materials = parse_js_data(html, "BUILD_MATERIALS")
    form_templates = parse_js_data(html, "FORM_TEMPLATES")

    print("Generating building blocks ...")
    replicate_build_block_loop(d, build_materials, form_templates)
    print(f"  -> {len(d)} items after building blocks")

    print("Parsing DECO_BLOCKS ...")
    deco_blocks = parse_js_data(html, "DECO_BLOCKS")
    add_deco_blocks(d, deco_blocks)
    print(f"  -> {len(d)} items after deco blocks")

    print("Normalising recipes ...")
    normalize_recipes(d)

    print("Parsing SYNERGY_ALTS ...")
    synergy_alts = parse_js_data(html, "SYNERGY_ALTS")
    apply_synergy_alts(d, synergy_alts)

    print("Parsing category ID sets ...")
    id_set_names = [
        "FOOD_IDS", "POWER_IDS", "WEAPON_IDS", "AMMO_IDS", "ARMOR_IDS",
        "BEAST_IDS", "ROBOT_IDS", "DEFENSE_IDS", "TARGET_IDS", "SPIRIT_IDS",
        "SCHOLAR_IDS", "VEHICLE_IDS", "AUTOMATION_IDS", "LOOP_IDS", "GADGET_IDS",
    ]
    id_sets = {}
    for n in id_set_names:
        try:
            id_sets[n.lower().replace("_ids", "")] = parse_id_set(html, n)
        except Exception as e:
            print(f"  ! {n}: {e}")
    # Derived sets
    id_sets["build"] = sorted([k for k, v in d.items() if v.get("_build")])
    id_sets["deco"] = sorted([k for k, v in d.items() if v.get("_deco")])

    print("Parsing NPC_DATA ...")
    npc_data = parse_js_data(html, "NPC_DATA")
    print(f"  -> {len(npc_data)} NPCs")

    # Write outputs
    def write(name: str, payload) -> None:
        path = DATA / name
        path.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"  wrote {path.relative_to(DATA.parent.parent)} ({len(json.dumps(payload))} bytes)")

    print("\nWriting JSON outputs ...")
    write("items.json", d)
    write("build_materials.json", build_materials)
    write("form_templates.json", form_templates)
    write("deco_blocks.json", deco_blocks)
    write("synergy_alts.json", synergy_alts)
    write("categories.json", id_sets)
    write("npcs.json", npc_data)

    # Summary / sanity check
    by_kind = {}
    by_src = {}
    for k, v in d.items():
        by_kind[v.get("kind", "?")] = by_kind.get(v.get("kind", "?"), 0) + 1
        if v.get("kind") == "source":
            by_src[v.get("src", "?")] = by_src.get(v.get("src", "?"), 0) + 1

    npc_by_cat = {}
    for n in npc_data:
        cat = n.get("cat", "?")
        npc_by_cat[cat] = npc_by_cat.get(cat, 0) + 1

    summary = {
        "total_items": len(d),
        "items_by_kind": by_kind,
        "sources_by_src": by_src,
        "build_materials": len(build_materials),
        "form_templates": len(form_templates),
        "deco_blocks": len(deco_blocks),
        "synergy_alts": len(synergy_alts),
        "category_counts": {k: len(v) for k, v in id_sets.items()},
        "npc_total": len(npc_data),
        "npc_by_category": npc_by_cat,
    }
    write("summary.json", summary)

    print("\nSummary:")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
