import hashlib
import json
import re
import sys
from export_chem_data import DEFAULT_GODOT, DEFAULT_OUT, read

paths = ['content/prologue/ability_h2_hover.tres', 'content/prologue/reaction_h2_synthesis.tres', 'src/player/player_motor.gd']
sources = {p: read(DEFAULT_GODOT / p) for p in paths}
data = dict(schema_version=1, pixels_per_unit=32, ability_id='ability_h2_hover', reaction_id='reaction_h2_synthesis')
for field in ('duration_seconds', 'gravity_multiplier', 'energy_cost_per_second', 'ground_speed', 'jump_velocity', 'gravity', 'terminal_velocity'):
    values = re.findall(r'^' + field + r' = ([0-9.]+)$', sources[paths[0]], re.M)
    if len(values) != 1: raise ValueError('Player ability source changed: ' + field)
    data[field] = float(values[0])
if 'ability_reward_id = &"ability_h2_hover"' not in sources[paths[1]]: raise ValueError('Hover reward source changed')
data['source_hashes'] = [dict(path=p, sha256=hashlib.sha256(sources[p].encode()).hexdigest()) for p in paths]
payload = json.dumps(data, ensure_ascii=False, indent=2) + '\n'
out = DEFAULT_OUT / 'player_motor.json'
if '--check' in sys.argv:
    if not out.is_file() or read(out) != payload: raise SystemExit('Player motor export is stale')
else: out.write_text(payload, encoding='utf-8')
print('player motor: authored H2 hover parameters and reward contract')
