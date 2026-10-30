"""Extract existing Godot combat and creature contracts without editing the source."""
import argparse
import hashlib
import json
import re
from export_chem_data import DEFAULT_GODOT, DEFAULT_OUT, parse_regions, read
from export_agent_catalog import LiteralParser


def export(root):
    paths = ['src/combat/combat_model.gd', 'src/world/world_content_catalog.gd',
             'src/world/environment_art_catalog.gd', 'src/world/sandbox_world.gd', 'src/session/world_session.gd']
    sources = {p: read(root / p) for p in paths}
    world = sources[paths[1]]
    body = world.split('static func enemy_definition', 1)[1].split('_enemy_definitions_cache = ', 1)[1].split('\n\treturn', 1)[0]
    enemies = LiteralParser(body, {}).value()
    regions = parse_regions(world)['regions']
    if len(enemies) != 15 or set(enemies) != {r['id'] for r in regions} - {'prologue_lab', 'planetary_core'}:
        raise ValueError('Combat region coverage changed')
    art = {}
    for kind, shape, color, accent, scale in re.findall(r'&"([^\"]+)": _creature\(&"([^\"]+)", Color\("#([0-9a-f]+)"\), Color\("#([0-9a-f]+)"\), ([0-9.]+)\)', sources[paths[2]]):
        art[kind] = dict(shape=shape, body='#' + color, accent='#' + accent, scale=float(scale))
    if len(art) != 15: raise ValueError('Creature source art changed')
    for region, enemy in enemies.items():
        enemy.update(region_id=region, radiation=next(r['radiation'] for r in regions if r['id'] == region), **art[enemy['kind']])
    combat, sandbox, session = sources[paths[0]], sources[paths[3]], sources[paths[4]]
    # These numeric rules are intentionally guarded until any source change is reviewed.
    rules = [(combat, 'const DEFAULT_ATTACK_INTERVAL := 0.9'), (combat, 'distance > 56.0'),
             (combat, '* 24.0'), (combat, 'attack_range := 72.0'), (sandbox, 'const MINING_REACH := 160.0'),
             (sandbox, 'nearest_pointer_distance := 36.0'), (sandbox, 'attack_enemy(enemy_id, 12.0)'),
             (sandbox, '+ Vector2(560, 0)'), (session, '* 4.0'), (session, 'inventory.apply_delta({drop: 1})')]
    if not all(token in source for source, token in rules): raise ValueError('Combat numeric contract changed')
    return dict(schema_version=1, pixels_per_unit=32, attack_interval=.9, stop_distance=56, attack_range=72,
                reach=160, pointer_radius=36, click_damage=12, spawn_offset=560, radiation_speed=24, radiation_damage=4,
                enemies=list(enemies.values()), source_hashes=[dict(path=p, sha256=hashlib.sha256(sources[p].encode()).hexdigest()) for p in paths])


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--check', action='store_true'); args = parser.parse_args()
    payload = json.dumps(export(DEFAULT_GODOT), ensure_ascii=False, indent=2) + '\n'
    output = DEFAULT_OUT / 'combat_catalog.json'
    if args.check:
        if not output.is_file() or read(output) != payload: raise SystemExit('Combat catalog is stale')
    else: output.write_text(payload, encoding='utf-8')
    print('combat catalog: 15 authoritative enemies, source combat constants and creature art')


if __name__ == '__main__': main()
