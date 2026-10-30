# -*- coding: utf-8 -*-
"""Deterministically export current Godot catalogs into Unity JSON.

The exporter reads the Godot source of truth and never writes into that repo.
Use --check in CI to detect stale Unity data without changing files.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import subprocess
import sys
from pathlib import Path


UNITY_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_GODOT = Path.home() / "Documents" / "GodotProjects" / "Astral-Element-Sea"
DEFAULT_OUT = UNITY_ROOT / "Assets" / "Resources" / "Data"
SCHEMA_VERSION = 2

SYMBOLS = {
    "element_h": "H", "element_o": "O", "element_c": "C", "element_n": "N",
    "element_cl": "Cl", "element_s": "S", "element_na": "Na", "element_k": "K",
    "element_ca": "Ca", "element_ba": "Ba", "element_cu": "Cu", "element_fe": "Fe",
    "element_zn": "Zn", "element_ag": "Ag", "ion_no3": "NO₃⁻", "ion_so4": "SO₄²⁻",
    "ion_oh": "OH⁻", "ion_co3": "CO₃²⁻", "ion_nh4": "NH₄⁺", "compound_h2": "H₂",
    "compound_o2": "O₂", "compound_h2o": "H₂O", "compound_co2": "CO₂",
    "compound_co": "CO", "compound_ch4": "CH₄", "compound_c2h2": "C₂H₂",
    "compound_hcl": "HCl", "compound_naoh": "NaOH", "compound_h2so4": "H₂SO₄",
    "compound_hno3": "HNO₃", "compound_nh3": "NH₃", "compound_nacl": "NaCl",
    "compound_fe2o3": "Fe₂O₃", "compound_cuo": "CuO", "compound_zno": "ZnO",
    "compound_zncl2": "ZnCl₂", "compound_agno3": "AgNO₃", "compound_agcl": "AgCl",
    "compound_baso4": "BaSO₄", "compound_cuso4": "CuSO₄", "compound_caoh2": "Ca(OH)₂",
    "compound_n2": "N₂", "compound_no": "NO", "compound_no2": "NO₂",
    "compound_nh4cl": "NH₄Cl", "compound_so2": "SO₂", "compound_h2o2": "H₂O₂",
    "compound_cl2": "Cl₂", "compound_kmno4": "KMnO₄", "compound_ethanol": "C₂H₅OH",
    "compound_caco3": "CaCO₃", "compound_cao": "CaO", "resource_mno4": "MnO₄ 矿块",
    "organic_glucose": "葡萄糖 C₆H₁₂O₆",
}

SUPPLEMENT = {
    "ui.objective.calm_shards": "平息 3 个区域碎片(用正确的反应语言回应谜题)",
    "ui.objective.lab_stage3": "发现 5 种新反应,深化化学研究",
    "ui.objective.descend_memories": "乘电梯下潜至深层区域,重构 8 段记忆",
    "ui.objective.core_ending": "抵达行星核心的裂核,做出最终抉择",
}


def read(path: Path) -> str:
    if not path.is_file():
        raise FileNotFoundError(path)
    return path.read_text(encoding="utf-8")


def quoted_ids(text: str) -> list[str]:
    return re.findall(r'&"([^"]+)"', text)


def extract_const_array(source: str, name: str) -> list[str]:
    match = re.search(rf"const {re.escape(name)}[^=]*= \[(.*?)\n\]", source, re.S)
    if not match:
        raise ValueError(f"Unable to parse {name}")
    return quoted_ids(match.group(1))


def block_between(source: str, start: str, end: str) -> str:
    begin = source.find(start)
    if begin < 0:
        raise ValueError(f"Missing block start: {start}")
    finish = source.find(end, begin + len(start))
    if finish < 0:
        raise ValueError(f"Missing block end after: {start}")
    return source[begin:finish]


def parse_localization(godot: Path) -> dict[str, str]:
    result: dict[str, str] = {}
    with (godot / "localization" / "zh_CN.csv").open(encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            key = (row.get("keys") or "").strip()
            if key:
                result[key] = (row.get("zh_CN") or "").strip()
    result.update(SUPPLEMENT)
    return result


def add_language_display_names(localization: dict[str, str], panel_source: str) -> None:
    block = block_between(panel_source, "func _display_language", "\n\treturn String(names.get")
    for language_id, display_name in re.findall(r'&"([^"]+)":\s*"([^"]+)"', block):
        localization.setdefault(f"content.codex.{language_id}.name", display_name)


def parse_reaction_specs(world_source: str, localization: dict[str, str]) -> tuple[list[dict], list[dict]]:
    block = block_between(world_source, "_reaction_specs_cache = [", "\n\t]")
    qty_pattern = re.compile(r'&"([^"]+)":\s*(\d+)')
    recipes: list[dict] = []
    material_ids: list[str] = []
    for line in block.splitlines():
        id_match = re.search(r'"id": &"([^"]+)"', line)
        if not id_match:
            continue
        branch_match = re.search(r'"branch": &"([^"]*)"', line)
        reactants_match = re.search(r'"reactants": \{([^}]*)\}', line)
        products_match = re.search(r'"products": \{([^}]*)\}', line)
        tags_match = re.search(r'"tags": \[([^]]*)\]', line)
        any_tags_match = re.search(r'"any_tags": \[([^]]*)\]', line)
        language_match = re.search(r'"language": &"([^"]*)"', line)
        if not all((branch_match, reactants_match, products_match, tags_match, language_match)):
            raise ValueError(f"Incomplete reaction spec row: {line}")
        recipe_id = id_match.group(1)
        recipe = {
            "id": recipe_id,
            "branch": branch_match.group(1),
            "reactants": [{"id": item, "n": int(count)} for item, count in qty_pattern.findall(reactants_match.group(1))],
            "products": [{"id": item, "n": int(count)} for item, count in qty_pattern.findall(products_match.group(1))],
            "tags": quoted_ids(tags_match.group(1)),
            "any_tags": quoted_ids(any_tags_match.group(1)) if any_tags_match else [],
            "language": language_match.group(1),
            "title": localization.get(f"content.reaction.{recipe_id}.title", recipe_id),
        }
        recipes.append(recipe)
        for quantity in recipe["reactants"] + recipe["products"]:
            if quantity["id"] not in material_ids:
                material_ids.append(quantity["id"])
    if len(recipes) != 40:
        raise ValueError(f"Expected 40 reaction specs, got {len(recipes)}")
    elements = extract_const_array(world_source, "ELEMENT_IDS")
    ions = extract_const_array(world_source, "POLYATOMIC_IDS")
    ordered = elements + ions + [item for item in material_ids if item not in elements and item not in ions]

    def material_name(material_id: str) -> str:
        for key in (f"content.codex.{material_id}.name", f"content.material.{material_id}.name"):
            if localization.get(key):
                return localization[key]
        return SYMBOLS.get(material_id, material_id)

    materials = [{
        "id": item,
        "kind": "element" if item.startswith("element_") else "ion" if item.startswith("ion_") else "compound",
        "symbol": SYMBOLS.get(item, item),
        "name": material_name(item),
    } for item in ordered]
    return recipes, materials


def parse_reaction_content(source: str) -> list[dict]:
    pattern = re.compile(
        r'_unit\(&"([^"]+)", \[([^]]*)\], \[([^]]*)\], \[([^]]*)\], '
        r'&"([^"]+)", &"([^"]+)", &"([^"]+)", \[([^]]*)\], \[([^]]*)\]\)'
    )
    result = []
    for match in pattern.finditer(source):
        recipe_id, environments, nodes, tags, obstacle, shard, memory, routes, alternates = match.groups()
        result.append({
            "id": recipe_id,
            "source_environments": quoted_ids(environments),
            "source_nodes": quoted_ids(nodes),
            "required_tags": quoted_ids(tags),
            "obstacle_id": obstacle,
            "shard_id": shard,
            "memory_id": memory,
            "route_nodes": quoted_ids(routes),
            "alternate_reaction_ids": quoted_ids(alternates),
        })
    if len(result) != 40:
        raise ValueError(f"Expected 40 reaction content units, got {len(result)}")
    return result


def parse_regions(world_source: str) -> dict:
    surface = extract_const_array(world_source, "SURFACE_ENVIRONMENTS")
    underground = extract_const_array(world_source, "UNDERGROUND_ENVIRONMENTS")
    definitions = block_between(world_source, "_environment_definitions_cache = {", "\n\t}")
    requirement_block = block_between(world_source, "const MINING_ENVIRONMENT_REQUIREMENTS", "\n}")
    requirements: dict[str, list[dict]] = {}
    for line in requirement_block.splitlines():
        match = re.search(r'&"([^"]+)": \{([^}]*)\}', line)
        if match:
            requirements[match.group(1)] = [
                {"id": key, "minimum": float(value)}
                for key, value in re.findall(r'&"([^"]+)":\s*([0-9.]+)', match.group(2))
            ]
    regions = []
    for line in definitions.splitlines():
        match = re.search(r'&"([^"]+)": \{(.*)\},?$', line.strip())
        if not match:
            continue
        region_id, body = match.groups()
        kind = re.search(r'"kind": "([^"]+)"', body)
        depth = re.search(r'"depth": (\d+)', body)
        radiation = re.search(r'"radiation": ([0-9.]+)', body)
        tags = re.search(r'"tags": \[([^]]*)\]', body)
        resources = re.search(r'"resources": \[([^]]*)\]', body)
        shard = re.search(r'"shard": &"([^"]+)"', body)
        if not all((kind, depth, radiation, tags, resources, shard)):
            raise ValueError(f"Incomplete region row: {line}")
        regions.append({
            "id": region_id,
            "name_key": f"environment.{region_id}.name",
            "kind": kind.group(1),
            "depth": int(depth.group(1)),
            "radiation": float(radiation.group(1)),
            "tags": quoted_ids(tags.group(1)),
            "resources": quoted_ids(resources.group(1)),
            "shard": shard.group(1),
            "mining_requirements": requirements.get(region_id, []),
        })
    expected = ["prologue_lab"] + surface + underground
    if [region["id"] for region in regions] != expected:
        raise ValueError("Region definitions do not match authoritative order")
    return {"order": {"surface": surface, "underground": underground}, "regions": regions}


def parse_devices(source: str) -> list[dict]:
    constants = dict(re.findall(r'const (DEVICE_[A-Z0-9_]+) := &"([^"]+)"', source))
    rows = block_between(source, "const _FORMAL_ROWS := [", "\n]")
    craft_rows = block_between(source, "const _CRAFTING_META := {", "\n}")
    craft_by_token: dict[str, dict] = {}
    for line in craft_rows.splitlines():
        token_match = re.match(r'\s*(DEVICE_[A-Z0-9_]+): \{', line)
        if not token_match:
            continue
        token = token_match.group(1)
        inputs_match = re.search(r'"inputs": \{([^}]*)\}', line)
        value = lambda key, default="": (re.search(rf'"{key}": &"([^"]*)"', line) or [None, default])[1]
        craft_by_token[token] = {
            "crafting_station": value("crafting_station"),
            "unlock_region": value("unlock_region"),
            "unlock_condition_id": value("unlock_condition_id"),
            "unlock_lab_stage": int((re.search(r'"unlock_lab_stage": (\d+)', line) or [None, "0"])[1]),
            "inputs": [{"id": item, "n": int(count)} for item, count in re.findall(
                r'&"([^"]+)":\s*(\d+)', inputs_match.group(1) if inputs_match else "")],
        }
    devices = []
    for line in rows.splitlines():
        slot_match = re.search(r'"slot": (\d+)', line)
        token_match = re.search(r'"device_id": (DEVICE_[A-Z0-9_]+)', line)
        if not slot_match or not token_match:
            continue
        token = token_match.group(1)
        scalar = lambda key, default="": (re.search(rf'"{key}": "([^"]*)"', line) or [None, default])[1]
        integer = lambda key, default=0: int((re.search(rf'"{key}": (\d+)', line) or [None, str(default)])[1])
        recipes_match = re.search(r'"recipe_ids": \[([^]]*)\]', line)
        footprint = re.search(r'"footprint_tiles": Vector2i\((\d+), (\d+)\)', line)
        status_token = (re.search(r'"resource_status": ([A-Z_]+)', line) or [None, "STATUS_READY"])[1]
        devices.append({
            "slot": int(slot_match.group(1)), "id": constants[token],
            "display_name": scalar("display_name"), "purpose": scalar("purpose"),
            "stage": integer("stage"), "processing_type": scalar("processing_type"),
            "processing_scale": scalar("processing_scale"),
            "resource_status": status_token.removeprefix("STATUS_"),
            "world_sprite": scalar("world_sprite"),
            "recipe_ids": quoted_ids(recipes_match.group(1)) if recipes_match else [],
            "input_slots": integer("input_slots"), "output_slots": integer("output_slots"),
            "storage_capacity": integer("storage_capacity"),
            "footprint_width": int(footprint.group(1)) if footprint else 1,
            "footprint_height": int(footprint.group(2)) if footprint else 1,
            "crafting": craft_by_token.get(token, {}),
        })
    if len(devices) != 15 or [item["slot"] for item in devices] != list(range(1, 16)):
        raise ValueError(f"Expected formal device slots 1..15, got {[item['slot'] for item in devices]}")
    return devices


def constant_symbols(*sources: tuple[str, str]) -> dict[str, object]:
    symbols: dict[str, object] = {}
    for prefix, source in sources:
        for name, value in re.findall(r'const ([A-Z0-9_]+) := &"([^"]*)"', source):
            symbols[name] = value
            if prefix:
                symbols[f"{prefix}.{name}"] = value
        for name, value in re.findall(r'const ([A-Z0-9_]+) := (\d+(?:\.\d+)?)', source):
            symbols[name] = float(value) if "." in value else int(value)
            if prefix:
                symbols[f"{prefix}.{name}"] = symbols[name]
    # Resolve simple aliases after all literal constants are known.
    for prefix, source in sources:
        for name, target in re.findall(r'const ([A-Z0-9_]+) := ([A-Z][A-Z0-9_.]+)', source):
            if target in symbols:
                symbols[name] = symbols[target]
                if prefix:
                    symbols[f"{prefix}.{name}"] = symbols[target]
    return symbols


def balanced_value(source: str, field: str) -> str:
    match = re.search(rf'"{re.escape(field)}"\s*:\s*', source)
    if not match:
        return ""
    start = match.end()
    pairs = {"{": "}", "[": "]", "(": ")"}
    stack: list[str] = []
    quoted = False
    index = start
    while index < len(source):
        char = source[index]
        if char == '"' and (index == 0 or source[index - 1] != "\\"):
            quoted = not quoted
        elif not quoted:
            if char in pairs:
                stack.append(pairs[char])
            elif stack and char == stack[-1]:
                stack.pop()
            elif char == "," and not stack:
                break
        index += 1
    return source[start:index].strip()


def resolve_scalar(token: str, symbols: dict[str, object]) -> object:
    token = token.strip()
    quoted = re.fullmatch(r'&?"([^"]*)"', token)
    if quoted:
        return quoted.group(1)
    if token in ("true", "false"):
        return token == "true"
    if re.fullmatch(r'-?\d+', token):
        return int(token)
    if re.fullmatch(r'-?\d+\.\d+', token):
        return float(token)
    if token in symbols:
        return symbols[token]
    raise ValueError(f"Unresolved Godot scalar: {token}")


def split_top_level(source: str) -> list[str]:
    result: list[str] = []
    start = 0
    stack: list[str] = []
    pairs = {"{": "}", "[": "]", "(": ")"}
    quoted = False
    for index, char in enumerate(source):
        if char == '"' and (index == 0 or source[index - 1] != "\\"):
            quoted = not quoted
        elif not quoted:
            if char in pairs:
                stack.append(pairs[char])
            elif stack and char == stack[-1]:
                stack.pop()
            elif char == "," and not stack:
                result.append(source[start:index].strip())
                start = index + 1
    tail = source[start:].strip()
    if tail:
        result.append(tail)
    return result


def parse_godot_map(token: str, symbols: dict[str, object]) -> list[dict]:
    token = token.strip()
    if not token or token == "{}":
        return []
    if not (token.startswith("{") and token.endswith("}")):
        raise ValueError(f"Expected Godot map, got: {token}")
    result = []
    for entry in split_top_level(token[1:-1]):
        key, value = entry.split(":", 1)
        result.append({"id": str(resolve_scalar(key, symbols)), "n": int(resolve_scalar(value, symbols))})
    return result


def parse_condition_map(token: str, symbols: dict[str, object]) -> list[dict]:
    token = token.strip()
    if not token or token == "{}":
        return []
    result = []
    for entry in split_top_level(token[1:-1]):
        key, value = entry.split(":", 1)
        result.append({"id": str(resolve_scalar(key, symbols)), "value": str(resolve_scalar(value, symbols))})
    return result


def processing_recipe_blocks(source: str) -> list[str]:
    marker = "ProcessingRecipe.new({"
    result: list[str] = []
    cursor = 0
    while True:
        begin = source.find(marker, cursor)
        if begin < 0:
            break
        brace = source.find("{", begin)
        depth = 0
        quoted = False
        end = brace
        while end < len(source):
            char = source[end]
            if char == '"' and (end == 0 or source[end - 1] != "\\"):
                quoted = not quoted
            elif not quoted:
                if char == "{": depth += 1
                elif char == "}":
                    depth -= 1
                    if depth == 0:
                        result.append(source[brace:end + 1])
                        cursor = end + 1
                        break
            end += 1
        else:
            raise ValueError("Unclosed ProcessingRecipe.new block")
    return result


def parse_processing_recipes(recipe_source: str, device_source: str, chemistry_source: str) -> list[dict]:
    symbols = constant_symbols(
        ("Phase2RecipeCatalog", recipe_source),
        ("FormalDeviceCatalog", device_source),
        ("Phase2ChemistryService", chemistry_source),
    )
    result = []
    for block in processing_recipe_blocks(recipe_source):
        recipe_id = str(resolve_scalar(balanced_value(block, "recipe_id"), symbols))
        output_items = parse_godot_map(balanced_value(block, "output_items"), symbols)
        if not output_items:
            # Godot marks these rows as processing_design_tbd; they are not executable.
            continue
        alternate_sets = []
        alternate_token = balanced_value(block, "input_item_sets")
        if alternate_token:
            for value in split_top_level(alternate_token[1:-1]):
                alternate_sets.append({"items": parse_godot_map(value, symbols)})
        tags_token = balanced_value(block, "progression_tags")
        result.append({
            "id": recipe_id,
            "device_id": str(resolve_scalar(balanced_value(block, "device_id"), symbols)),
            "device_type": str(resolve_scalar(balanced_value(block, "device_type"), symbols)),
            "input_items": parse_godot_map(balanced_value(block, "input_items"), symbols),
            "process_inputs": parse_godot_map(balanced_value(block, "process_inputs"), symbols),
            "alternate_input_sets": alternate_sets,
            "output_items": output_items,
            "processing_time": float(resolve_scalar(balanced_value(block, "processing_time"), symbols)) if balanced_value(block, "processing_time") else 0.0,
            "required_stage": int(resolve_scalar(balanced_value(block, "required_progression_stage"), symbols)) if balanced_value(block, "required_progression_stage") else 0,
            "required_conditions": parse_condition_map(balanced_value(block, "required_conditions"), symbols),
            "progression_tags": quoted_ids(tags_token),
        })
    ids = [item["id"] for item in result]
    if len(ids) != len(set(ids)):
        raise ValueError("Duplicate processing recipe IDs")
    return result


def git_value(repository: Path, *arguments: str) -> str:
    return subprocess.check_output(["git", "-C", str(repository), *arguments], text=True).strip()


def serialize(value: object) -> str:
    return json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + "\n"


def parse_ore_profiles(source: str) -> list[dict]:
    constants = dict(re.findall(r'^const (\w+) := &"([^\"]+)"$', source, re.M))
    body = source.split('static func _ore_profile(ore_id: StringName) -> Dictionary:', 1)[1].split('\nstatic func ', 1)[0]
    rows = []
    for symbol, metal, oxide, grade, gangue in re.findall(r'(\w+): \{"metal": &"([^\"]+)", "oxide": &"([^\"]+)", "grade": ([0-9.]+), "gangue": &"([^\"]+)"\}', body):
        rows.append(dict(id=constants[symbol], metal=metal, oxide=oxide, grade=float(grade), gangue=gangue))
    if len(rows) != 8: raise ValueError('Ore profile source changed; audit required')
    return rows


def build_outputs(godot: Path, out: Path) -> dict[str, object]:
    world_path = godot / "src" / "world" / "world_content_catalog.gd"
    content_path = godot / "src" / "world" / "reaction_content_catalog.gd"
    devices_path = godot / "src" / "progression" / "formal_device_catalog.gd"
    processing_path = godot / "src" / "progression" / "phase2_recipe_catalog.gd"
    phase2_chemistry_path = godot / "src" / "chemistry" / "phase2_chemistry_service.gd"
    language_panel_path = godot / "src" / "ui" / "gameplay" / "world_systems_panel.gd"
    localization_path = godot / "localization" / "zh_CN.csv"
    world_source = read(world_path)
    localization = parse_localization(godot)
    add_language_display_names(localization, read(language_panel_path))
    recipes, materials = parse_reaction_specs(world_source, localization)
    reaction_content = parse_reaction_content(read(content_path))
    content_by_id = {item["id"]: item for item in reaction_content}
    for recipe in recipes:
        content = content_by_id[recipe["id"]]
        recipe["shard_id"] = content["shard_id"]
        recipe["memory_id"] = content["memory_id"]
    regions = parse_regions(world_source)
    device_source = read(devices_path)
    devices = parse_devices(device_source)
    processing_recipes = parse_processing_recipes(
        read(processing_path), device_source, read(phase2_chemistry_path))
    recipe_by_id = {item["id"]: item for item in recipes}
    reactions_meta = {"reactions": [{
        "id": item["id"], "branch": recipe_by_id[item["id"]]["branch"],
        "title_key": f"content.reaction.{item['id']}.title",
        "lesson_key": f"content.reaction.{item['id']}.lesson",
        "source_environments": item["source_environments"], "shard_id": item["shard_id"],
        "memory_id": item["memory_id"], "route_nodes": item["route_nodes"],
    } for item in reaction_content]}
    outputs: dict[str, object] = {
        "regions.json": regions,
        "chemistry.json": {"materials": materials, "reactions": recipes},
        "reactions.json": reactions_meta,
        "reaction_content.json": {"reactions": reaction_content},
        "devices.json": {"devices": devices},
        "processing_recipes.json": {"recipes": processing_recipes},
        "ore_profiles.json": {"ores": parse_ore_profiles(read(phase2_chemistry_path))},
        "localization_zh.json": {"entries": [{"key": key, "text": value} for key, value in sorted(localization.items())]},
    }
    memories_path = out / "memories.json"
    if memories_path.is_file():
        outputs["memories.json"] = json.loads(memories_path.read_text(encoding="utf-8"))
    source_files = [str(path.relative_to(godot)).replace("\\", "/") for path in
                    (world_path, content_path, devices_path, processing_path, phase2_chemistry_path,
                     localization_path, language_panel_path)]
    outputs["migration_manifest.json"] = {
        "schema_version": SCHEMA_VERSION,
        "source_commit": git_value(godot, "rev-parse", "HEAD"),
        "source_commit_time": git_value(godot, "show", "-s", "--format=%cI", "HEAD"),
        "source_files": source_files,
        "counts": {"regions": len(regions["regions"]), "reaction_specs": len(recipes),
                   "reaction_content_units": len(reaction_content), "formal_devices": len(devices),
                   "processing_recipes": len(processing_recipes),
                   "materials": len(materials), "localization_entries": len(localization)},
    }
    return outputs


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot", type=Path, default=DEFAULT_GODOT)
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    godot, out = args.godot.resolve(), args.out.resolve()
    if not (godot / "project.godot").is_file():
        raise FileNotFoundError(f"Godot project not found: {godot}")
    out.mkdir(parents=True, exist_ok=True)
    outputs = build_outputs(godot, out)
    stale: list[str] = []
    for name, value in outputs.items():
        target, rendered = out / name, serialize(value)
        if args.check:
            if not target.is_file() or target.read_text(encoding="utf-8") != rendered:
                stale.append(name)
        else:
            target.write_text(rendered, encoding="utf-8", newline="\n")
            print(f"wrote {name}")
    if stale:
        print("stale authoritative exports: " + ", ".join(stale), file=sys.stderr)
        return 1
    print("authoritative export counts: " + json.dumps(
        outputs["migration_manifest.json"]["counts"], ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
