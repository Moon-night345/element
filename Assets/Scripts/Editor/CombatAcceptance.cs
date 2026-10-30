using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class CombatAcceptance
    {
        static void Require(bool valid, string why) { if (!valid) throw new InvalidOperationException(why); }
        public static void SourceAndCombat()
        {
            var cat = CombatCatalog.Load(); Require(cat.enemies.Length == 15 && cat.enemies.All(d => GameData.Regions.regions.Any(r => r.id == d.region_id)), "authoritative enemy coverage");
            foreach (var def in cat.enemies)
            {
                var s = new GameState(); var c = new CombatRuntimeService(s, cat);
                Require(c.EnsureEncounter("prologue_lab", Vector2.zero) == "" && c.EnsureEncounter("planetary_core", Vector2.zero) == "", "excluded region spawned enemy");
                string id = c.EnsureEncounter(def.region_id, Vector2.zero); var e = c.Alive.Single();
                Require(id == "enemy_0001" && e.kind == def.kind && e.health == def.health && e.x == 17.5f && e.radiation_factor == def.radiation && e.move_speed == def.speed / 32, "source enemy/units " + def.region_id);
                Require(c.EnsureEncounter(def.region_id, Vector2.zero) == "" && s.combat.entities.Length == 1, "duplicate live encounter");
                string before = JsonUtility.ToJson(s);
                foreach (float bad in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) Require(c.Tick(Vector2.zero, bad, true, false) == 0 && before == JsonUtility.ToJson(s), "invalid tick mutated state");
                Require(c.Tick(Vector2.zero, .1f, false, false) == 0 && c.Tick(Vector2.zero, .1f, true, true) == 0 && before == JsonUtility.ToJson(s), "unready/paused combat advanced");
                c.Tick(Vector2.zero, .1f, true, false); Require(Mathf.Abs(e.x - (17.5f - (def.speed + def.radiation * 24) / 32 * .1f)) < .0001f && s.hp == 100, "pursuit units/radiation speed");
                e.x = 56 / 32f; e.y = 0; float damage = c.Tick(Vector2.zero, .1f, true, false);
                Require(Mathf.Abs(damage - (def.damage + def.radiation * 4)) < .0001f && e.x == 56 / 32f && e.attack_cooldown == .9f, "attack/stop threshold/radiation damage");
                float hp = s.hp; c.Tick(Vector2.zero, .4f, true, false); Require(s.hp == hp && Mathf.Abs(e.attack_cooldown - .5f) < .0001f, "attack cooldown ignored");
                c.Tick(Vector2.zero, .5f, true, false); Require(s.hp < hp, "cooldown expiry failed");
                e.x = 72 / 32f; e.move_speed = 0; e.radiation_factor = 0; e.attack_cooldown = 0; hp = s.hp;
                c.Tick(Vector2.zero, .01f, true, false); Require(s.hp < hp, "inclusive attack range boundary");
                e.x += .001f; e.attack_cooldown = 0; hp = s.hp; c.Tick(Vector2.zero, .01f, true, false); Require(s.hp == hp, "outside attack range hit");
                e.x = 5f; e.y = 0; bool killed, rewarded;
                Require(c.EnemyAtPointer(Vector2.zero, new Vector2(6.125f, 0)) == id && c.EnemyAtPointer(Vector2.zero, new Vector2(6.126f, 0)) == "", "pointer radius boundary");
                Require(!c.ClickAttack(new Vector2(-.001f, 0), new Vector2(e.x, e.y), out killed, out rewarded), "out of reach attack");
                int hits = Mathf.CeilToInt(def.health / 12);
                for (int i = 0; i < hits; i++) Require(c.ClickAttack(Vector2.zero, new Vector2(e.x, e.y), out killed, out rewarded) && killed == (i == hits - 1) && rewarded == killed, "12-point click/kill/drop " + i);
                Require(s.CountItem(def.drop) == 1 && !c.Damage(id, 12, out killed, out rewarded) && s.CountItem(def.drop) == 1 && !e.alive, "dead enemy duplicate loot");
                Require(c.EnsureEncounter(def.region_id, Vector2.zero) == "enemy_0002", "reentry encounter counter");
                Require(CombatRuntimeService.Valid(s.combat), "normal combat save validation");
            }
        }
        public static void PersistenceAndRejection()
        {
            var s = new GameState(); var c = new CombatRuntimeService(s, CombatCatalog.Load()); c.EnsureEncounter("dead_city_ruins", Vector2.zero);
            var e = c.Alive.Single(); e.x = 1; c.Tick(Vector2.zero, .1f, true, false); bool killed, rewarded; c.Damage(e.id, 12, out killed, out rewarded);
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u30_combat_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(s, out error), "combat save " + error);
            var read = repo.Read(); Require(read.success && read.state.combat.entities.Single().health == 16 && read.state.combat.entities.Single().attack_cooldown == .9f && read.state.combat.next_entity_id == 2, "combat disk health/cooldown/counter");
            var restored = new CombatRuntimeService(read.state, c.Catalog); restored.Damage(e.id, 100, out killed, out rewarded); Require(killed && rewarded && read.state.CountItem(e.drop) == 1, "restored kill reward");
            Require(repo.Write(read.state, out error), "dead enemy save"); var dead = repo.Read().state; Require(!new CombatRuntimeService(dead, c.Catalog).Damage(e.id, 100, out killed, out rewarded) && dead.CountItem(e.drop) == 1, "dead persisted loot duplicate");
            e.attack_cooldown = float.NaN; Require(!repo.Write(s, out error), "non-finite combat save accepted"); e.attack_cooldown = 0; s.combat.next_entity_id = 1; Require(!repo.Write(s, out error), "colliding next id saved");
            var full = new GameState(); var fc = new CombatRuntimeService(full, c.Catalog); fc.EnsureEncounter("dead_city_ruins", Vector2.zero); var fe = fc.Alive.Single(); full.AddItem(fe.drop, int.MaxValue);
            Require(fc.Damage(fe.id, 100, out killed, out rewarded) && killed && !rewarded && full.CountItem(fe.drop) == int.MaxValue, "full inventory overflow reward");
            Require(!fc.Damage(fe.id, 100, out killed, out rewarded), "full inventory kill retried");
            var old = new GameState { combat = null }; new CombatRuntimeService(old, c.Catalog); Require(old.combat.entities.Length == 0 && old.combat.next_entity_id == 1, "legacy empty combat initialization");
        }
    }
}
