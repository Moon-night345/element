using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class ResourceAcceptance
    {
        static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        public static void FiniteSources()
        {
            var state = new GameState { currentRegion = "rust_jungle" };
            var service = new FiniteResourceService(state, GameData.Regions);
            var item = GameData.Regions.regions.First(r => r.id == "rust_jungle").resources[0];
            string reason;
            for (int i = 0; i < 6; i++) Require(service.GatherRegion("rust_jungle", item, out reason), "gather " + reason);
            Require(!service.GatherRegion("rust_jungle", item, out reason) && reason == "source_depleted" && state.CountItem(item) == 6,
                "source overdraw / unlimited alternate entry");
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            service = new FiniteResourceService(restored, GameData.Regions);
            Require(service.RegionRemaining("rust_jungle", item) == 0 && !service.GatherRegion("rust_jungle", item, out reason), "depleted source reset");
            string before = JsonUtility.ToJson(restored);
            Require(!service.GatherRegion("planetary_core", item, out reason) && JsonUtility.ToJson(restored) == before, "remote / undeclared source accepted");
            Require(!service.GatherRegion("rust_jungle", "invented_item", out reason), "unregistered item accepted");
            Require(service.Capture("source_hydrogen_leak_01", "element_h", 2, out reason) && service.Capture("source_hydrogen_leak_01", "element_h", 2, out reason) &&
                !service.Capture("source_hydrogen_leak_01", "element_h", 2, out reason) && restored.CountItem("element_h") == 2, "hydrogen capacity/output");
            Require(!service.Capture("source_hydrogen_leak_01", "element_h", 20, out reason), "capacity changed for existing identity");
            var full = new GameState(); full.AddItem("element_h", int.MaxValue);
            service = new FiniteResourceService(full, GameData.Regions);
            before = JsonUtility.ToJson(full);
            Require(!service.Capture("h", "element_h", 2, out reason) && JsonUtility.ToJson(full) == before, "inventory failure depleted / created source");
            foreach (var region in GameData.Regions.regions)
            {
                var regional = new GameState { currentRegion = region.id };
                var gathering = new FiniteResourceService(regional, GameData.Regions);
                foreach (var resource in region.resources.Distinct())
                {
                    for (int i = 0; i < 6; i++) Require(gathering.GatherRegion(region.id, resource, out reason), "formal source inaccessible " + region.id + ":" + resource);
                    Require(!gathering.GatherRegion(region.id, resource, out reason), "formal source unlimited");
                }
            }
        }
        public static void DropLifecycle()
        {
            var state = new GameState();
            var terrain = new TerrainRuntimeService(state, GameData.Regions,
                JsonUtility.FromJson<TerrainCatalog>(Resources.Load<TextAsset>("Data/terrain_catalog").text));
            terrain.AddPatch("topsoil_caves", 0, 0, 16, 8); terrain.RestoreChanges();
            var cell = new Vector2Int(2, -1);
            Require(terrain.MineToDrop(cell, new Vector2(2.5f, 1.2f)).success && !terrain.Solid(cell) &&
                state.CountItem("resource_soil") == 0 && state.worldDrops.Length == 1, "mine bypassed drop");
            Require(!terrain.MineToDrop(cell, new Vector2(2.5f, 1.2f)).success && state.worldDrops.Length == 1, "duplicate mine");
            string directory = Path.GetFullPath(Path.Combine("Logs", "u14_resource_test_" + Guid.NewGuid().ToString("N")));
            var repository = new SaveRepository(Path.Combine(directory, "save.json"), "test");
            string reason;
            var finite = new FiniteResourceService(state, GameData.Regions);
            Require(finite.Capture("h", "element_h", 2, out reason), "source fixture");
            Require(repository.Write(state, out reason), "drop disk write " + reason);
            var read = repository.Read(); Require(read.success, "drop disk read");
            var restored = read.state;
            Require(restored.worldDrops.Length == 1 && restored.terrainChanges.Length == 1 && restored.resourceSources[0].remaining == 1, "snapshot lost resource state");
            var drops = new WorldDropService(restored);
            var drop = restored.worldDrops[0];
            string before = JsonUtility.ToJson(restored);
            Require(!drops.Pickup(drop.instance_id, new Vector2(100, 0), out reason) && JsonUtility.ToJson(restored) == before, "remote pickup mutated");
            var player = new Vector2(2.5f, 0.2f);
            restored.AddItem(drop.item_id, int.MaxValue);
            before = JsonUtility.ToJson(restored);
            Require(!drops.Pickup(drop.instance_id, player, out reason) && JsonUtility.ToJson(restored) == before, "overflow deleted drop");
            Require(restored.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(drop.item_id, -int.MaxValue) }), "fixture reset");
            Require(drops.Pickup(drop.instance_id, player, out reason) && restored.worldDrops.Length == 0 && restored.CountItem(drop.item_id) == 1 &&
                !drops.Pickup(drop.instance_id, player, out reason), "pickup once / conservation");
            Require(repository.Write(restored, out reason) && repository.Read().state.worldDrops.Length == 0, "picked drop respawned");
            restored.resourceSources = new[] { new ResourceSourceState { id = "bad", item_id = "element_h", capacity = 2, remaining = 3 } };
            Require(!repository.Write(restored, out reason), "invalid capacity save accepted");
            restored.resourceSources = new ResourceSourceState[0];
            restored.worldDrops = new[] { drop, drop };
            Require(!repository.Write(restored, out reason), "duplicate entity saved");
            var origin = new Vector2(0, TerrainRuntimeService.PlayerFootOffset);
            Require(WorldDropService.WithinPickupArea(origin, new Vector2(-22f / 32f, 1)) &&
                !WorldDropService.WithinPickupArea(origin, new Vector2(22f / 32f, 1)) &&
                !WorldDropService.WithinPickupArea(origin, new Vector2(0, -6f / 32f)) &&
                WorldDropService.WithinPickupArea(origin, new Vector2(0, 70f / 32f)), "Godot pickup rectangle boundaries");
        }
    }
}
