using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class TerrainAcceptance
    {
        static TerrainCatalog Catalog { get { return JsonUtility.FromJson<TerrainCatalog>(Resources.Load<TextAsset>("Data/terrain_catalog").text); } }
        static TerrainRuntimeService Create(GameState state)
        {
            var world = new TerrainRuntimeService(state, GameData.Regions, Catalog);
            world.AddPatch("topsoil_caves", 0, 0, 16, 8);
            world.RestoreChanges();
            return world;
        }
        public static void ConservationAndRestore()
        {
            var state = new GameState();
            var world = Create(state);
            var cell = new Vector2Int(2, -1);
            var player = new Vector2(2.5f, 1.2f);
            Require(world.MiningStatus(cell, player).duration == 0.3f, "source soil hardness");
            for (int i = 0; i < 10; i++)
            {
                Require(world.Mine(cell, player).success && state.CountItem("resource_soil") == 1, "mine quantity");
                string snapshot = JsonUtility.ToJson(state);
                Require(!world.Mine(cell, player).success && JsonUtility.ToJson(state) == snapshot, "repeat mine changed state");
                var restored = JsonUtility.FromJson<GameState>(snapshot);
                var loaded = Create(restored);
                Require(!loaded.Solid(cell) && restored.terrainChanges.Length == 1, "mined delta lost");
                Require(loaded.Place(cell, "resource_soil", player).success && restored.CountItem("resource_soil") == 0 &&
                    restored.terrainChanges.Length == 0, "restored terrain did not conserve/canonicalize");
                state = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(restored));
                world = Create(state);
            }
            state.AddItem("resource_stone");
            Require(world.Mine(cell, player).success && world.Place(cell, "resource_stone", player).success, "different block placement");
            Require(world.BlockAt(cell) == "stone", "placed identity lost");
            var save = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            var afterLoad = Create(save);
            Require(afterLoad.BlockAt(cell) == "stone" && afterLoad.Mine(cell, player).success && save.CountItem("resource_stone") == 1,
                "placed block identity changed after save");
            Require(Catalog.blocks.Length == 64 && Catalog.blocks.Any(b => b.id == "dead_city_copper_bearing_debris"), "source catalog missing identities");
        }
        public static void FailureAndTiming()
        {
            var state = new GameState();
            var world = Create(state);
            state.AddItem("resource_soil", 10);
            var player = new Vector2(2.5f, 1.2f);
            Action<Vector2Int, string, string> rejected = (cell, item, reason) => {
                string before = JsonUtility.ToJson(state);
                var result = world.Place(cell, item, player);
                Require(!result.success && result.reason == reason && JsonUtility.ToJson(state) == before, "placement guard " + reason);
            };
            rejected(new Vector2Int(-1, 0), "resource_soil", "out_of_bounds");
            rejected(new Vector2Int(2, 0), "compound_h2o", "item_not_placeable");
            rejected(new Vector2Int(2, -1), "resource_soil", "occupied");
            rejected(new Vector2Int(5, 3), "resource_soil", "no_adjacent_support");
            rejected(new Vector2Int(15, 0), "resource_soil", "out_of_range");
            rejected(new Vector2Int(2, 0), "resource_soil", "player_overlap");
            world.Protect(new Rect(6, -1, 1, 2));
            rejected(new Vector2Int(6, 0), "resource_soil", "protected_anchor");
            Require(world.Mine(new Vector2Int(6, -1), player).reason == "protected_block", "protected mining");
            Require(world.Mine(new Vector2Int(2, -8), new Vector2(2.5f, -5)).reason == "protected_block", "slice foundation protection");
            Require(world.MiningStatus(new Vector2Int(2, -6), new Vector2(2.5f, -3)).reason == "mining_power_too_low", "power gate");
            state.deviceInstances = new[] { new DeviceRuntimeContract { instance_id = "guard", is_placed = true, world_x = 7.5f, world_y = 0.5f } };
            rejected(new Vector2Int(7, 0), "resource_soil", "device_overlap");
            var overflow = new GameState(); overflow.AddItem("resource_soil", int.MaxValue);
            var full = Create(overflow);
            Require(full.Mine(new Vector2Int(2, -1), player).reason == "inventory_rejected" && full.Solid(new Vector2Int(2, -1)), "overflow destroyed block");
            var late = new TerrainRuntimeService(state, GameData.Regions, Catalog);
            late.AddPatch("planetary_core", 0, 0, 16, 8); late.RestoreChanges();
            state.AddItem("element_fe", 6); state.AddItem("element_cu", 3);
            var tools = new MechanicalToolService(state);
            Require(tools.Craft(MechanicalToolService.ReinforcedRecipe, true).success &&
                tools.Equip(MechanicalToolService.ReinforcedTool).success, "late tool setup");
            var lateCell = late.Cells.First(c => !late.IsProtected(c.Key) && late.Definition(c.Value).power <= 3).Key;
            var low = late.MiningStatus(lateCell, TerrainRuntimeService.Center(lateCell) + Vector2.up * 2);
            Require(!low.success && low.reason == "heat_resistance_too_low", "late mining bypassed environment gate");
            var tracker = new TerrainMiningProgress();
            tracker.Begin(new Vector2Int(1, 1), 1f);
            Require(!tracker.Advance(0.4f), "instant mining");
            tracker.Begin(new Vector2Int(1, 1), 1f);
            Require(Math.Abs(tracker.Ratio - 0.4f) < 0.001f, "same target progress reset");
            tracker.Begin(new Vector2Int(2, 1), 1f);
            Require(tracker.Ratio == 0 && tracker.Advance(1f), "target switch/timing");
            tracker.Cancel(); Require(!tracker.active && !tracker.Advance(1f), "cancel completion");
            var bad = new GameState { terrainChanges = new[] { new TerrainCellChange { x = 9999, y = 0, block_id = "soil" } } };
            bool rejectedSave = false;
            try { Create(bad); } catch (InvalidOperationException) { rejectedSave = true; }
            Require(rejectedSave, "corrupt terrain delta silently accepted");
        }
        public static void Collision()
        {
            var state = new GameState(); var world = Create(state);
            var start = new Vector2(2.5f, 1.2f);
            bool x, y;
            var landed = world.Move(start + Vector2.up * 10, Vector2.down * 20, out x, out y);
            Require(y && landed.y >= 1.199f && landed.y < 1.31f, "high-speed floor tunnelling");
            state.AddItem("resource_soil", 3);
            Require(world.Place(new Vector2Int(4, 0), "resource_soil", start).success, "wall setup");
            var wall = world.Move(start, Vector2.right * 10, out x, out y);
            Require(x && wall.x < 3.7f, "horizontal wall tunnelling");
            var under = new Vector2(4.5f, 1.2f);
            Require(!world.PlacementStatus(new Vector2Int(4, 1), "resource_soil", under).success, "player entombed");
            var seedA = Create(new GameState { terrainSeed = 517 });
            var seedB = Create(new GameState { terrainSeed = 517 });
            Require(seedA.Cells.All(c => seedB.BlockAt(c.Key) == c.Value), "non deterministic slice terrain");
            world.AddPatch("fossil_strata", 0, -40, 16, 8);
            Require(world.RegionAt(new Vector2Int(2, -6)) == "topsoil_caves" &&
                world.RegionAt(new Vector2Int(2, -46)) == "fossil_strata", "digging switched to wrong depth region");
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
