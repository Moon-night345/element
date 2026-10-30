"""Read-only Godot mining/identity export. Does not run world generation."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from export_chem_data import DEFAULT_GODOT, DEFAULT_OUT, read


def export(root):
    paths = ["src/mining/mining_catalog.gd", "src/mining/biome_block_catalog.gd",
             "src/world/region_resource_catalog.gd", "src/content/item_contract_catalog.gd"]
    mining, biome, resources, items = [read(root / path) for path in paths]
    specs = []
    for material, body in re.findall(r"\n\t([1-4]): \{(.*?)\n\t\},", mining, re.S):
        def field(name):
            return re.search(r'"' + name + r'"\s*:\s*&?"([^\"]+)"', body).group(1)
        specs.append(dict(id=field("id"), item_id=field("drop"), region_id="", material=int(material),
                          hardness=float(re.search(r'"base_hardness": ([\d.]+)', body).group(1)),
                          power=int(re.search(r'"required_mining_power": (\d+)', body).group(1)),
                          icon_x=0, icon_y=int(material) - 1, placement_material=int(material)))
    if len(specs) != 4:
        raise ValueError("Expected four legacy MiningCatalog blocks")
    bands = {region: int(band) for region, band in re.findall(
        r'&"([a-z_]+)": \{[^\n]*?"progression_band": (\d+)', biome)}
    role_order = ["surface", "subsurface", "deep", "ore", "rare"]
    registered_roles = {}
    dead_specs = biome.split("const DEAD_CITY_ROLE_SPECS", 1)[1].split("\n}", 1)[0]
    for role, identity in re.findall(r'&"([a-z]+)": \{"identity": &"([a-z_]+)"', dead_specs):
        registered_roles[identity] = role
    for name in ("core_identity_ids", "planetary_identity_ids"):
        body = biome.split("var " + name + " := {", 1)[1].split("}", 1)[0]
        for role, identity in re.findall(r'&"([a-z]+)": &"([a-z_]+)"', body):
            # _ensure_biomes assigns duplicate IDs in ROLE_ORDER: the last role wins lookup.
            registered_roles[identity] = role
    hardness = dict(re.findall(r'&"(surface|subsurface|deep|ore|rare)": \{"hardness": ([\d.]+)', biome))
    power_by_band = {}
    for band, body in re.findall(r'\n\t([0-2]): \{([^\n]+)\}', biome):
        power_by_band[int(band)] = {key: int(value) for key, value in re.findall(r'&"([a-z]+)": (\d+)', body)}
    material_map = {}
    material_body = items.split("static func terrain_material_for", 1)[1].split("static func", 1)[0]
    for identity, material in re.findall(r'&"([a-z_]+)": return TerrainModel\.([A-Z_]+)', material_body):
        material_map[identity] = dict(SOIL=1, STONE=2, ORE=3, DENSE_ROCK=4)[material]
    block_source = resources.split("const _BLOCKS: Dictionary = {", 1)[1].split("\n}", 1)[0]
    for region, body in re.findall(r'&"([a-z_]+)": \[(.*?)\n\t\]', block_source, re.S):
        for row in re.findall(r'\{[^\n]+\}', body):
            identity = re.search(r'"block_id": &"([a-z_]+)"', row).group(1)
            item = re.search(r'"item_id": &"([a-z_]+)"', row).group(1)
            icon = re.search(r'"icon": Vector2i\((\d+),\s*(\d+)\)', row)
            x, y = map(int, icon.groups())
            # Mirrors BiomeBlockCatalog._ensure_biomes authored alias registration.
            role = registered_roles.get(identity, role_order[min(y, len(role_order) - 1)])
            power = power_by_band[bands[region]][role]
            if region == "dead_city_ruins" and role == "rare": power = 1
            specs.append(dict(id=identity, item_id=item, region_id=region,
                              material=material_map.get(identity, [1, 2, 4, 3, 3][min(y, 4)]),
                              placement_material=material_map.get(identity, 0),
                              hardness=float(hardness[role]), power=power, icon_x=x, icon_y=y))
    if len(bands) != 17 or len({b["id"] for b in specs}) != len(specs):
        raise ValueError("Invalid terrain source catalog")
    return dict(schema_version=1, source_hashes=[dict(path=p, sha256=hashlib.sha256(read(root/p).encode()).hexdigest())
                                               for p in paths], blocks=specs)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot", type=Path, default=DEFAULT_GODOT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    data = export(args.godot)
    output = DEFAULT_OUT / "terrain_catalog.json"
    payload = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if args.check:
        if not output.is_file() or output.read_text(encoding="utf-8") != payload:
            raise SystemExit("Terrain export is stale")
    else:
        output.write_text(payload, encoding="utf-8")
    print("terrain catalog:", len(data["blocks"]), "identities; 4 legacy + authored region identities")


if __name__ == "__main__":
    main()
