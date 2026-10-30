"""Read-only, fail-closed extraction of Godot Agent profile constants and literal text."""
import argparse
import hashlib
import json
import re
from pathlib import Path
from export_chem_data import DEFAULT_GODOT, DEFAULT_OUT, parse_regions, parse_ore_profiles, read


class LiteralParser:
    def __init__(self, source, symbols):
        token = r'\s+|\#[^\n]*|&?"(?:\\.|[^"\\])*"|-?\d+(?:\.\d+)?|[A-Za-z_][A-Za-z_0-9.]*|[{}\[\]:,]'
        self.tokens = []
        position = 0
        while position < len(source):
            match = re.compile(token).match(source, position)
            if not match:
                raise ValueError("Unsupported profile syntax: " + source[position:position + 50])
            value = match.group()
            position = match.end()
            if not value.isspace() and not value.startswith("#"):
                self.tokens.append(value)
        self.index, self.symbols = 0, symbols

    def take(self):
        value = self.tokens[self.index]
        self.index += 1
        return value

    def value(self):
        value = self.take()
        if value in ("{", "["):
            closing = "}" if value == "{" else "]"
            result = {} if value == "{" else []
            while self.tokens[self.index] != closing:
                key = self.value()
                if value == "{":
                    if self.take() != ":": raise ValueError("Expected colon")
                    if key in result: raise ValueError("Duplicate profile key: " + key)
                    result[key] = self.value()
                else:
                    result.append(key)
                if self.tokens[self.index] == ",": self.take()
                elif self.tokens[self.index] != closing: raise ValueError("Expected comma")
            self.take()
            return result
        if value.startswith(('"', '&"')): return json.loads(value.lstrip("&"))
        if value in ("true", "false"): return value == "true"
        if re.fullmatch(r'-?\d+(?:\.\d+)?', value): return float(value) if "." in value else int(value)
        if value not in self.symbols: raise ValueError("Unresolved profile constant: " + value)
        return self.symbols[value]


def export(root):
    sources = {p.relative_to(root).as_posix(): read(p) for p in (root / "src").rglob("*.gd")}
    symbols = {}
    for source in sources.values():
        name = re.search(r'^class_name (\w+)', source, re.M)
        if not name: continue
        for key, literal in re.findall(r'^const (\w+)(?:\s*:\s*[^=\n]+)?\s*(?::=|=)\s*(&?"(?:\\.|[^"\\])*"|\d+)\s*$', source, re.M):
            symbols[name[1] + "." + key] = json.loads(literal.lstrip("&"))
    profile_path = "src/agent/region_agent_behavior_profile.gd"
    body = sources[profile_path].split("static func _definitions() -> Dictionary:", 1)[1].split("return", 1)[1].split("}.duplicate(true)", 1)[0] + "}"
    definitions = LiteralParser(body, symbols).value()
    response_path = "src/agent/deterministic_agent_response_provider.gd"
    responses = [{"reaction_id": key, "text": json.loads(text)} for key, text in re.findall(
        r'^\t\t&"([^\"]+)":\s*\n\t\t\treturn ("(?:\\.|[^"\\])*")\s*$', sources[response_path], re.M)]
    response_ids = {r["reaction_id"] for r in responses}
    profiles = []
    for region, definition in definitions.items():
        entry, budget = definition["entry_reaction"], definition["speech_budget"]
        if entry["reaction_id"] not in response_ids: raise ValueError("Entry has no literal source text: " + region)
        profiles.append(dict(region_id=region, entry_action=entry["action"], entry_reaction=entry["reaction_id"],
            global_cooldown_ms=budget["global_short_cooldown_msec"], exempt_actions=budget.get("budget_exempt_actions", []),
            exempt_reactions=budget.get("budget_exempt_reactions", []),
            hints=[dict(key=k, value=v) for k, v in definition.get("hint_ladder", {}).items()],
            material_events=definition.get("material_topics", {}).get("events", []),
            hazard_events=definition.get("hazard_topics", {}).get("events", []),
            failure_reasons=definition.get("failure_topics", {}).get("reasons", []) if region in ("core_shell", "planetary_core") else definition.get("hazard_topics", {}).get("failure_reasons", []),
            mature_conditions=definition.get("guidance_mature_conditions", []),
            sample_ids=definition.get("ore_topics", definition.get("material_topics", {})).get("sample_ids", []),
            carbonate_facts=definition.get("material_topics", {}).get("carbonate_facts", []),
            solution_facts=definition.get("material_topics", {}).get("solution_facts", []),
            carbon_facts=definition.get("material_topics", {}).get("carbon_facts", []),
            calcium_facts=definition.get("material_topics", {}).get("calcium_facts", []),
            organic_facts=definition.get("material_topics", {}).get("organic_facts", []),
            fermentable_facts=definition.get("material_topics", {}).get("fermentable_facts", []),
            crystal_facts=definition.get("material_topics", {}).get("crystal_facts", []),
            phenomena=definition.get("environment_topics", {}).get("phenomena", []),
            maximum_cold_temperature=definition.get("environment_topics", {}).get("maximum_cold_temperature", -1),
            minimum_solution_availability=definition.get("environment_topics", {}).get("minimum_solution_availability", -1),
            minimum_high_temperature=definition.get("environment_topics", {}).get("minimum_high_temperature", -1),
            minimum_electrical_activity=definition.get("electricity_topics", {}).get("minimum_authored_activity", 0.5),
            contamination_facts=definition.get("contamination_topics", {}).get("fact_ids", []),
            oxidation_properties=definition.get("oxidation_topics", {}).get("property_ids", []),
            purity_phenomena=definition.get("purity_topics", {}).get("phenomena", []),
            absorption_phenomena=definition.get("absorption_topics", {}).get("phenomena", []),
            oxidation_phenomena=definition.get("oxidation_topics", {}).get("phenomena", []),
            minimum_heat_temperature=definition.get("heat_topics", {}).get("minimum_authored_temperature", 0.9),
            sulfur_facts=definition.get("sulfur_topics", {}).get("protected_ids", []),
            pressure_success_phenomena=definition.get("pressure_topics", {}).get("success_phenomena", []),
            formal_catalyst=definition.get("catalysis_topics", {}).get("formal_catalyst", ""),
            pressure_fields=definition.get("environment_topics", {}).get("pressure_fields", []),
            condition_fields=definition.get("condition_topics", {}).get("authoritative_fields", []),
            category_fields=definition.get("process_chain_topics", {}).get("category_fields", []),
            process_phenomena=definition.get("process_chain_topics", {}).get("phenomena", []),
            hazard_fields=definition.get("hazard_topics", {}).get("fields", []),
            reactions=[dict(key=k, value=v) for k, v in definition["reaction_ids"].items()],
            visuals=[dict(key=k, value=v) for k, v in definition.get("visual_semantic_mapping", {}).items()],
            definition_json=json.dumps(definition, ensure_ascii=False, sort_keys=True)))
    regions = parse_regions(sources["src/world/world_content_catalog.gd"])
    adapters = []
    for path, source in sources.items():
        if path.startswith("src/agent/") and path.endswith("_agent_behavior.gd"):
            region = re.search(r'const REGION_ID := &"([^\"]+)"', source)
            if not region: raise ValueError("Adapter has no authoritative REGION_ID: " + path)
            adapters.append(dict(region_id=region[1], source=path))
    if len(profiles) != 14 or len(adapters) != 13 or len(regions["regions"]) != 17:
        raise ValueError("Agent source coverage changed; audit required")
    analysis_path = "src/progression/scrap_catalog.gd"
    analyses = []
    for item, facts in re.findall(r'&"(item_dead_city_[a-z_]+_block)":\s*\n\s*return \{[^\n]*?"composition": (\[[^\]]*\])', sources[analysis_path]):
        analyses.append(dict(item_id=item, region_id="dead_city_ruins", facts=LiteralParser(facts, symbols).value()))
    if len(analyses) != 6: raise ValueError("Expected six explicit dead-city analysis contracts")
    chemical_path = "src/world/chemical_resource_catalog.gd"
    compositions = sources[chemical_path].split("const COMPOSITIONS: Dictionary =", 1)[1].split("\n}", 1)[0] + "}"
    chemical_facts = LiteralParser(compositions, symbols).value()
    for item in ("resource_rust_metal_residue", "resource_chloride_deposit"):
        analyses.append(dict(item_id=item, region_id="rust_jungle", facts=chemical_facts[item]))
    for item in definitions["crystal_tundra"]["material_topics"]["sample_ids"]:
        if item in chemical_facts:
            facts = chemical_facts[item]
        else:
            match = re.search(r'&"' + re.escape(item) + r'":\s*\n\s*return \{[^\n]*?"composition": (\[[^\]]*\])', sources[analysis_path])
            if not match: raise ValueError("Missing crystal source analysis: " + item)
            facts = LiteralParser(match[1], symbols).value()
        analyses.append(dict(item_id=item, region_id="crystal_tundra", facts=facts))
    crystal_body = sources[chemical_path].split('elif biome_id == &"crystal_tundra":', 1)[1].split("\n\telif", 1)[0]
    cold_temperature = re.findall(r'profile\["temperature"\] = ([0-9.]+)', crystal_body)
    if len(cold_temperature) != 1: raise ValueError("Crystal authored temperature changed")
    ember_body = sources[chemical_path].split('elif biome_id == &"ember_rift":', 1)[1].split("\n\telif", 1)[0]
    ember_temperature = re.findall(r'profile\["temperature"\] = ([0-9.]+)', ember_body)
    if len(ember_temperature) != 1: raise ValueError("Ember authored temperature changed")
    crystal_text_body = sources[response_path].split("func _crystal_carbonate_text", 1)[1].split("\nfunc ", 1)[0]
    crystal_texts = re.findall(r'\breturn ("(?:\\.|[^"\\])*")', crystal_text_body)
    if len(crystal_texts) != 2: raise ValueError("Crystal dynamic response changed")
    salt_samples = definitions["salt_marsh"]["material_topics"]["sample_ids"]
    salt_terrain_match = re.search(r'(&"item_salt_alkali_mudflat[^\n]+):\s*\n\s*var salt_profile[^\n]+\n\s*return \{[^\n]*?"composition": (\[[^\]]*\])', sources[analysis_path])
    if not salt_terrain_match: raise ValueError("Salt terrain analysis contract changed")
    salt_terrain_ids = re.findall(r'&"([^\"]+)"', salt_terrain_match[1])
    if set(salt_terrain_ids) != {s for s in salt_samples if s.startswith("item_")}:
        raise ValueError("Salt terrain identities changed")
    salt_unresolved = []
    for item in salt_samples:
        if item in chemical_facts: facts = chemical_facts[item]
        elif item in salt_terrain_ids: facts = LiteralParser(salt_terrain_match[2], symbols).value()
        elif item == symbols["Phase2ChemistryService.NEUTRALIZED_SALT_SOLUTION"]:
            # Source profile lists this item but neither composition catalog nor _sample_info
            # provides an analysis. Do not invent NaCl or a neutral pH for its Agent record.
            salt_unresolved.append(item)
            continue
        else: raise ValueError("Unexpected unresolved Salt analysis: " + item)
        analyses.append(dict(item_id=item, region_id="salt_marsh", facts=facts))
    phase2_path = "src/chemistry/phase2_chemistry_service.gd"
    for symbol in ("EMBER_SCORCHED_ROCK", "EMBER_SULFUR_MINERAL", "EMBER_ASH_DEPOSIT", "EMBER_CARBONACEOUS_RESIDUE"):
        match = re.search(r'^\t\t' + symbol + r': return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError("Ember sample definition changed: " + symbol)
        facts = LiteralParser(match[1], symbols).value()
        facts.append(LiteralParser(match[2], symbols).value())
        analyses.append(dict(item_id=symbols["Phase2ChemistryService." + symbol], region_id="ember_rift", facts=list(dict.fromkeys(facts))))
    magnetic_source = sources['src/agent/magnetic_plateau_agent_behavior.gd']
    magnetic_samples = '[' + magnetic_source.split('static func is_profile_material', 1)[1].split('return', 1)[1].split('[', 1)[1].split(']', 1)[0] + ']'
    next(p for p in profiles if p['region_id'] == 'magnetic_plateau')['sample_ids'] = LiteralParser(magnetic_samples, symbols).value()
    for symbol in ('MAGNETIC_MAGNETIZED_ROCK', 'MAGNETIC_CONDUCTIVE_MINERAL', 'MAGNETIC_SILVER_MINERAL', 'MAGNETIC_NITRATE_DEPOSIT'):
        match = re.search(r'^\t\t' + symbol + r': return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError('Magnetic detector contract changed: ' + symbol)
        facts = LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]
        analyses.append(dict(item_id=symbols['Phase2ChemistryService.' + symbol], region_id='magnetic_plateau', facts=facts))
    magnetic_unresolved = [item for item in next(p for p in profiles if p['region_id'] == 'magnetic_plateau')['sample_ids'] if not any(a['item_id'] == item for a in analyses)]
    if len(magnetic_unresolved) != 6 or any(chemical_facts.get(item) for item in magnetic_unresolved): raise ValueError('Magnetic unresolved identity sources changed; audit required')
    for item in definitions['radiation_belt']['material_topics']['sample_ids']:
        match = re.search(r'^\t\t&"' + re.escape(item) + r'": return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError('Radiation detector source changed: ' + item)
        analyses.append(dict(item_id=item, region_id='radiation_belt', facts=LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]))
    for item in ('resource_contaminated_mineral', 'resource_contaminated_solution', 'resource_oxidized_residue'):
        if item not in chemical_facts: raise ValueError('Missing radiation scan composition: ' + item)
        analyses.append(dict(item_id=item, region_id='radiation_belt', facts=chemical_facts[item], requires_scan=True))
    for item in definitions['mantle_flows']['material_topics']['sample_ids']:
        match = re.search(r'^\t\t&"' + re.escape(item) + r'": return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError('Mantle detector source changed: ' + item)
        analyses.append(dict(item_id=item, region_id='mantle_flows', facts=LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]))
    mantle_body = sources[chemical_path].split('elif layer_id == &"mantle_flows":', 1)[1].split('\n\telif', 1)[0]
    mantle_floor = re.findall(r'profile\["temperature"\] = maxf\(float\(profile\["temperature"\]\), ([0-9.]+)\)', mantle_body)
    if len(mantle_floor) != 1: raise ValueError('Mantle authored thermal floor changed')
    for item in definitions['core_shell']['material_topics']['sample_ids']:
        match = re.search(r'^\t\t&"' + re.escape(item) + r'": return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError('Core Shell detector source changed: ' + item)
        analyses.append(dict(item_id=item, region_id='core_shell', facts=list(dict.fromkeys(LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]))))
    core_body = sources[chemical_path].split('elif layer_id == &"core_shell":', 1)[1].split('\n\telif', 1)[0]
    core_pressure = re.findall(r'profile\["pressure"\] = ([0-9.]+)', core_body)
    if len(core_pressure) != 1: raise ValueError('Core Shell authored pressure changed')
    for item in definitions['planetary_core']['material_topics']['sample_ids']:
        match = re.search(r'^\t\t&"' + re.escape(item) + r'": return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError('Planetary Core detector source changed: ' + item)
        analyses.append(dict(item_id=item, region_id='planetary_core', facts=list(dict.fromkeys(LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]))))
    planetary_source = sources['src/agent/planetary_core_agent_behavior.gd']
    recall_body = planetary_source.split('static func _select_one_recall', 1)[1].split('\n\nstatic func', 1)[0]
    planetary_recalls = []
    for keys, values in re.findall(r'((?:&"[^\"]+"(?:, )?)+): candidates = (\[[^\]]*\])', recall_body):
        for key in re.findall(r'&"([^\"]+)"', keys):
            planetary_recalls.append(dict(key=key, values=LiteralParser(values, symbols).value()))
    if len(planetary_recalls) != 11: raise ValueError('Planetary recall mapping changed; audit required')
    failure_body = planetary_source.split('static func _capability_category_for_reason', 1)[1].split('\n\nstatic func', 1)[0]
    planetary_failure_categories = [dict(key=k, value=v) for k, v in re.findall(r'&"([^\"]+)": return &"([^\"]+)"', failure_body)]
    if len(planetary_failure_categories) != 5 or 'return &"PROCESS"' not in failure_body: raise ValueError('Planetary failure category mapping changed')
    salt_ph = []
    for symbol in ("ACIDIC_SOLUTION", "ALKALINE_SOLUTION"):
        match = re.search(r'^\t\t' + symbol + r': return \{[^\n]*"ph": (PH_\w+)', sources[phase2_path], re.M)
        if not match: raise ValueError("Salt pH contract changed")
        salt_ph.append(dict(key=symbols["Phase2ChemistryService." + symbol], value=symbols["Phase2ChemistryService." + match[1]]))
    salt_body = sources[chemical_path].split('elif biome_id == &"salt_marsh":', 1)[1].split("\n\telif", 1)[0]
    salt_availability = re.findall(r'profile\["solution_availability"\] = ([0-9.]+)', salt_body)
    if len(salt_availability) != 1: raise ValueError("Salt environment changed")
    def source_returns(function, count):
        body = sources[response_path].split("func " + function, 1)[1].split("\nfunc ", 1)[0]
        values = re.findall(r'\breturn ("(?:\\.|[^"\\])*")', body)
        if len(values) != count: raise ValueError("Dynamic response changed: " + function)
        return [json.loads(v) for v in values]
    salt_solution_texts = source_returns("_salt_solution_text", 7)
    sand_texts = []
    for topic, count in (("carbon", 2), ("calcium", 2), ("carbonate", 3)):
        values = source_returns("_sand_" + topic + "_text", count)
        keys = ("exact", "general") if count == 2 else ("exact", "ion", "general")
        sand_texts.extend(dict(key=topic + "_" + key, value=value) for key, value in zip(keys, values))
    for item in definitions["sand_desert"]["material_topics"]["sample_ids"]:
        match = re.search(r'&"' + re.escape(item) + r'":\s*\n\s*return \{[^\n]*?"composition": (\[[^\]]*\])', sources[analysis_path])
        if not match: raise ValueError("Missing Sand composition: " + item)
        analyses.append(dict(item_id=item, region_id="sand_desert", facts=LiteralParser(match[1], symbols).value()))
    for item in definitions["fluorescent_grotto"]["material_topics"]["sample_ids"]:
        match = re.search(r'&"' + re.escape(item) + r'":\s*\n\s*return \{[^\n]*?"composition": (\[[^\]]*\])', sources[analysis_path])
        if not match: raise ValueError("Missing Grotto composition: " + item)
        analyses.append(dict(item_id=item, region_id="fluorescent_grotto", facts=LiteralParser(match[1], symbols).value()))
    grotto_detector_analyses = []
    for symbol in ("FUNGAL_WET_ROCK", "FUNGAL_BIOMASS", "FUNGAL_ORGANIC_DEPOSIT", "FUNGAL_FERMENTATION_FEEDSTOCK"):
        match = re.search(r'^\t\t' + symbol + r': return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not match: raise ValueError("Grotto detector definition changed: " + symbol)
        facts = LiteralParser(match[1], symbols).value() + [LiteralParser(match[2], symbols).value()]
        grotto_detector_analyses.append(dict(item_id=symbols["Phase2ChemistryService." + symbol], region_id="fluorescent_grotto", facts=list(dict.fromkeys(facts))))
    geode_detector_analyses = []
    for symbol in ("GEODE_HOST_ROCK", "GEODE_CRYSTAL_BEARING_ROCK", "GEODE_NATURAL_CRYSTAL_DEPOSIT", "GEODE_MINERAL_SOLUTION_DEPOSIT"):
        item = symbols["Phase2ChemistryService." + symbol]
        detector = re.search(r'^\t\t' + symbol + r': return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M)
        if not detector: raise ValueError("Missing Geode detector contract: " + item)
        facts = LiteralParser(detector[1], symbols).value() + [LiteralParser(detector[2], symbols).value()]
        analyses.append(dict(item_id=item, region_id="geode_abyss", facts=list(dict.fromkeys(facts))))
        geode_detector_analyses.append(dict(item_id=item, region_id="geode_abyss", facts=list(dict.fromkeys(facts))))
    mine_detector_analyses = []
    mine_contract = sources[analysis_path].split('&"item_abandoned_mine_iron_bearing_ore_block",', 1)[1].split('\n\t\t&"item_crystal', 1)[0]
    if 'String(item_id).replace("item_", "").replace("_block", "")' not in mine_contract: raise ValueError('Mine terrain analysis changed')
    processing = {}
    for item, device_symbol in re.findall(r'(?:if|elif) item_id == &"([^\"]+)":\s*\n\s*processing_status = &"READY"; processing_device = (\w+\.\w+)', mine_contract):
        processing[item] = symbols[device_symbol]
    if len(processing) != 4: raise ValueError('Mine processing mapping changed')
    ore_profiles = {p['id']: p for p in parse_ore_profiles(sources[phase2_path])}
    for item in definitions['abandoned_mine']['ore_topics']['sample_ids']:
        device = processing.get(item, '')
        facts = [item.removeprefix('item_').removesuffix('_block')] if item.startswith('item_') else [ore_profiles[item]['metal']]
        analyses.append(dict(item_id=item, region_id='abandoned_mine', facts=facts, processing_device=device))
        for token, composition, prop in re.findall(r'^\t\t([^:\n]+): return \{"composition_ids": (\[[^\]]*\])[^\n]*"property": (&"[^\"]+")', sources[phase2_path], re.M):
            candidate = symbols.get('Phase2ChemistryService.' + token, token.lstrip('&').strip('"'))
            if candidate == item:
                detector_facts = LiteralParser(composition, symbols).value() + [LiteralParser(prop, symbols).value()]
                mine_detector_analyses.append(dict(item_id=item, region_id='abandoned_mine', facts=list(dict.fromkeys(detector_facts)), processing_device=device))
    if len(mine_detector_analyses) != 4: raise ValueError('Mine detector source coverage changed')
    magnetic_body = sources[chemical_path].split('elif biome_id == &"magnetic_plateau":', 1)[1].split('\n\tif layer_id', 1)[0]
    magnetic_activity = re.findall(r'profile\["electrical_activity"\] = ([0-9.]+)', magnetic_body)
    if len(magnetic_activity) != 1: raise ValueError('Magnetic authored environment changed')
    grotto_body = sources[chemical_path].split('if layer_id == &"fluorescent_grotto":', 1)[1].split("\n\telif", 1)[0]
    grotto_oxygen = re.findall(r'profile\["oxygen_level"\] = minf\(float\(profile\["oxygen_level"\]\), ([0-9.]+)\)', grotto_body)
    grotto_light = re.findall(r'profile\["light_level"\] = ([0-9.]+)', grotto_body)
    if len(grotto_oxygen) != 1 or len(grotto_light) != 1: raise ValueError("Grotto environment source changed")
    grotto_material_texts = source_returns("_grotto_material_text", 5)
    grotto_gas_texts = source_returns("_grotto_gas_text", 2)
    salt_ph_texts = source_returns("_salt_ph_text", 4)
    rust_texts = re.findall(r'\breturn ("(?:\\.|[^"\\])*")', sources[response_path].split("func _rust_analysis_text", 1)[1])
    if len(rust_texts) != 4: raise ValueError("Rust dynamic response changed; audit required")
    recall = sources["src/agent/cross_region_recall.gd"].split("const FACT_GROUPS :=", 1)[1].split("\n}", 1)[0] + "}"
    groups = LiteralParser(recall, symbols).value()
    paths = sorted(p for p in sources if p.startswith("src/agent/")) + ["src/world/world_content_catalog.gd", analysis_path, chemical_path, phase2_path]
    return dict(schema_version=1, profiles=profiles, responses=responses, adapters=adapters,
        analyses=analyses, fact_groups=[dict(key=k, values=v) for k, v in groups.items()],
        rust_analysis_texts=[dict(key=k, value=json.loads(v)) for k, v in zip(("iron_chloride", "iron", "chloride", "default"), rust_texts)],
        crystal_carbonate_texts=[dict(key=k, value=json.loads(v)) for k, v in zip(("exact", "general"), crystal_texts)],
        crystal_authored_temperature=float(cold_temperature[0]),
        sand_analysis_texts=sand_texts,
        grotto_authored_oxygen=float(grotto_oxygen[0]), grotto_authored_light=float(grotto_light[0]),
        grotto_detector_analyses=grotto_detector_analyses,
        geode_detector_analyses=geode_detector_analyses,
        mine_detector_analyses=mine_detector_analyses,
        magnetic_authored_activity=float(magnetic_activity[0]),
        magnetic_unresolved_sample_ids=magnetic_unresolved,
        mantle_authored_temperature_floor=float(mantle_floor[0]),
        core_authored_pressure=float(core_pressure[0]),
        planetary_recall_groups=planetary_recalls, planetary_failure_categories=planetary_failure_categories,
        mantle_analysis_texts=[dict(key=k, value=v) for k, v in zip(('sulfate', 'sulfur', 'default'), source_returns('_mantle_analysis_text', 3))],
        magnetic_analysis_texts=[dict(key=k, value=v) for k, v in zip(('nitrate', 'ammonium', 'silver', 'copper', 'default'), source_returns('_magnetic_analysis_text', 5))],
        grotto_material_texts=[dict(key=k, value=v) for k, v in zip(("compound_co2", "element_o", "element_h", "element_c", "default"), grotto_material_texts)],
        grotto_gas_texts=[dict(key=k, value=v) for k, v in zip(("confirmed", "unknown"), grotto_gas_texts)],
        ember_authored_temperature=float(ember_temperature[0]),
        salt_authored_solution_availability=float(salt_availability[0]),
        salt_unresolved_analysis_ids=salt_unresolved, salt_sample_ph=salt_ph,
        salt_solution_texts=[dict(key=k, value=v) for k, v in zip(("compound_hcl", "compound_naoh", "ion_oh", "chloride", "element_na", "element_k", "default"), salt_solution_texts)],
        salt_ph_texts=[dict(key=k, value=v) for k, v in zip(("ACIDIC", "ALKALINE", "NEUTRAL", "default"), salt_ph_texts)],
        generic_regions=[r["id"] for r in regions["regions"] if r["id"] not in definitions],
        source_hashes=[dict(path=p, sha256=hashlib.sha256(sources[p].encode()).hexdigest()) for p in paths])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot", type=Path, default=DEFAULT_GODOT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    data = export(args.godot)
    payload = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    output = DEFAULT_OUT / "agent_catalog.json"
    if args.check:
        if not output.is_file() or read(output) != payload: raise SystemExit("Agent export is stale")
    else: output.write_text(payload, encoding="utf-8")
    print("agent catalog:", len(data["profiles"]), "profiles;", len(data["adapters"]), "source adapters; generic:", data["generic_regions"])


if __name__ == "__main__": main()
