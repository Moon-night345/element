using System;
using System.Linq;
using UnityEngine;
namespace ElementalStarSea.Data
{
    [Serializable] public sealed class EnemyDefinition
    {
        public string region_id, kind, drop, shape, body, accent;
        public float health, speed, damage, radiation, scale;
    }
    [Serializable] public sealed class CombatCatalog
    {
        public int schema_version, pixels_per_unit;
        public float attack_interval, stop_distance, attack_range, reach, pointer_radius, click_damage, spawn_offset, radiation_speed, radiation_damage;
        public EnemyDefinition[] enemies;
        public static CombatCatalog Load()
        {
            var c = JsonUtility.FromJson<CombatCatalog>(Resources.Load<TextAsset>("Data/combat_catalog").text);
            if (c.schema_version != 1 || c.enemies.Length != 15 || c.pixels_per_unit != 32) throw new InvalidOperationException("Unsupported combat source catalog");
            return c;
        }
    }
    [Serializable] public sealed class CombatEnemyState
    {
        public string id, kind, drop, origin_region;
        public float x, y, health, max_health, radiation_factor, move_speed, attack_damage, attack_interval, attack_cooldown;
        public bool alive;
    }
    [Serializable] public sealed class CombatState
    {
        public long next_entity_id = 1;
        public CombatEnemyState[] entities = new CombatEnemyState[0];
    }
    public sealed class CombatRuntimeService
    {
        readonly GameState state;
        public readonly CombatCatalog Catalog;
        public CombatRuntimeService(GameState state, CombatCatalog catalog)
        { this.state = state; Catalog = catalog; if (state.combat == null) state.combat = new CombatState(); }
        static bool Finite(float n) { return !float.IsNaN(n) && !float.IsInfinity(n); }
        static bool Finite(Vector2 p) { return Finite(p.x) && Finite(p.y); }
        public CombatEnemyState[] Alive { get { return state.combat.entities.Where(e => e.alive).ToArray(); } }
        // Godot creates a new encounter only while binding/entering a world with no living enemy.
        public string EnsureEncounter(string region, Vector2 regionSpawn)
        {
            var def = Catalog.enemies.FirstOrDefault(d => d.region_id == region);
            if (def == null || Alive.Length != 0 || !Finite(regionSpawn) || state.combat.next_entity_id == long.MaxValue) return "";
            string id = "enemy_" + state.combat.next_entity_id.ToString("D4", System.Globalization.CultureInfo.InvariantCulture); state.combat.next_entity_id++;
            var pos = regionSpawn + Vector2.right * (Catalog.spawn_offset / Catalog.pixels_per_unit);
            state.combat.entities = state.combat.entities.Concat(new[] { new CombatEnemyState { id = id, kind = def.kind, drop = def.drop, origin_region = region,
                x = pos.x, y = pos.y, health = def.health, max_health = def.health, radiation_factor = def.radiation,
                move_speed = def.speed / Catalog.pixels_per_unit, attack_damage = def.damage, attack_interval = Catalog.attack_interval, alive = true } }).ToArray();
            return id;
        }
        public string EnemyAtPointer(Vector2 player, Vector2 pointer)
        {
            if (!Finite(player) || !Finite(pointer)) return "";
            string nearest = ""; float distance = Catalog.pointer_radius / Catalog.pixels_per_unit;
            foreach (var e in Alive)
            {
                var pos = new Vector2(e.x, e.y);
                if (Vector2.Distance(player, pos) > Catalog.reach / Catalog.pixels_per_unit) continue;
                float d = Vector2.Distance(pointer, pos); if (d <= distance) { nearest = e.id; distance = d; }
            }
            return nearest;
        }
        public bool ClickAttack(Vector2 player, Vector2 pointer, out bool killed, out bool rewarded)
        {
            killed = rewarded = false; string id = EnemyAtPointer(player, pointer);
            return id.Length > 0 && Damage(id, Catalog.click_damage, out killed, out rewarded);
        }
        public bool Damage(string id, float amount, out bool killed, out bool rewarded)
        {
            killed = rewarded = false; var e = state.combat.entities.FirstOrDefault(x => x.id == id);
            if (e == null || !e.alive || !Finite(amount) || amount <= 0) return false;
            e.health = Mathf.Max(0, e.health - amount);
            if (e.health <= 0)
            {
                e.alive = false; killed = true;
                // Source awards directly to inventory once, even if a full inventory rejects the delta.
                rewarded = state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(e.drop, 1) });
            }
            return true;
        }
        public float Tick(Vector2 target, float delta, bool runtimeReady, bool paused)
        {
            if (!runtimeReady || paused || !Finite(delta) || delta <= 0 || !Finite(target)) return 0;
            float dealt = 0;
            foreach (var e in Alive)
            {
                e.attack_cooldown = Mathf.Max(0, e.attack_cooldown - delta);
                var pos = new Vector2(e.x, e.y);
                if (Vector2.Distance(pos, target) > Catalog.stop_distance / Catalog.pixels_per_unit)
                {
                    float speed = Mathf.Max(0, e.move_speed) + e.radiation_factor * Catalog.radiation_speed / Catalog.pixels_per_unit;
                    pos = Vector2.MoveTowards(pos, target, speed * delta); e.x = pos.x; e.y = pos.y;
                }
                if (Vector2.Distance(pos, target) > Catalog.attack_range / Catalog.pixels_per_unit || e.attack_cooldown > 0) continue;
                e.attack_cooldown = e.attack_interval;
                float damage = e.attack_damage + e.radiation_factor * Catalog.radiation_damage;
                if (damage > 0 && Finite(damage)) { state.hp = Mathf.Max(0, state.hp - damage); dealt += damage; }
            }
            return dealt;
        }
        public static bool Valid(CombatState combat)
        {
            if (combat == null) return true; // old saves initialize when the service binds
            if (combat.next_entity_id < 1 || combat.entities == null || combat.entities.Any(e => e == null) || combat.entities.Select(e => e.id).Distinct().Count() != combat.entities.Length) return false;
            foreach (var e in combat.entities)
            {
                long number;
                if (string.IsNullOrEmpty(e.id) || !e.id.StartsWith("enemy_", StringComparison.Ordinal) || !long.TryParse(e.id.Substring(6), out number) || number < 1 || number >= combat.next_entity_id ||
                    string.IsNullOrEmpty(e.kind) || string.IsNullOrEmpty(e.drop) || !Finite(e.x) || !Finite(e.y) || !Finite(e.health) || !Finite(e.max_health) || e.max_health <= 0 || e.health < 0 || e.health > e.max_health || e.alive != (e.health > 0) ||
                    !Finite(e.radiation_factor) || e.radiation_factor < 0 || !Finite(e.move_speed) || e.move_speed < 0 || !Finite(e.attack_damage) || e.attack_damage < 0 || !Finite(e.attack_interval) || e.attack_interval < .1f || !Finite(e.attack_cooldown) || e.attack_cooldown < 0) return false;
            }
            return true;
        }
    }
}
