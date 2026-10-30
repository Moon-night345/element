using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    // Service integration, not a GUI/traversal playthrough. Raw inputs now use finite
    // regional sources and real terrain drops; never inject inventory, stages or devices.
    public static class PlayerRouteAcceptance
    {
        public static void Run()
        {
            var state = new GameState();
            var world = new WorldChemistryService(state);
            var progression = new LaboratoryProgressionService(state);
            var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
            var environment = new RegionEnvironmentService(state, GameData.Regions);
            var finite = new FiniteResourceService(state, GameData.Regions);
            var drops = new WorldDropService(state);
            var terrain = new TerrainRuntimeService(state, GameData.Regions,
                JsonUtility.FromJson<TerrainCatalog>(Resources.Load<TextAsset>("Data/terrain_catalog").text));
            terrain.AddPatch("dead_city_ruins", 40, 0, 80, 8); terrain.RestoreChanges();
            devices.GameplayEventPublished += e => progression.RecordDeviceEvent(e);
            world.GameplayEventPublished += e => progression.RecordWorldChemistryEvent(e);

            Action<string> visit = region => {
                if (state.currentRegion == region) return;
                Require(environment.RegionAccess(region).allowed, "visit blocked: " + region);
                state.currentRegion = region;
                state.AddVisitedRegion(region);
            };
            Action<string, string, int> gather = (region, item, count) => {
                visit(region);
                var def = GameData.Regions.regions.First(r => r.id == region);
                Require(def.resources.Contains(item), "not an authored raw source: " + region + "/" + item);
                string reason;
                for (int i = 0; i < count; i++) Require(finite.GatherRegion(region, item, out reason), "finite source " + region + ": " + reason);
            };
            Action<string, int> recover = (output, count) => {
                visit("dead_city_ruins");
                string block = WorldChemistryService.DeadCityRecovery.First(p => p.Value == output).Key;
                for (int i = 0; i < count; i++)
                {
                    var candidates = terrain.Cells.Where(p => terrain.Definition(p.Value).item_id == block &&
                        terrain.MiningStatus(p.Key, TerrainRuntimeService.Center(p.Key) + Vector2.up * 1.7f).success).ToArray();
                    Require(candidates.Length > 0, "finite terrain exhausted: " + block);
                    var cell = candidates[0].Key;
                    Require(terrain.MineToDrop(cell, TerrainRuntimeService.Center(cell) + Vector2.up * 1.7f).success, "mine recovery input");
                    var drop = state.worldDrops.Last();
                    string reason;
                    Require(drops.Pickup(drop.instance_id, TerrainRuntimeService.Center(cell) + Vector2.up, out reason), "pickup recovery input " + reason);
                }
                Require(world.AnalyzeDeadCityBlock(block).success, "analyze dead city block");
                for (int i = 0; i < count; i++) Require(world.RecoverDeadCityBlock(block).success, "recover " + output);
            };
            Action<string> craft = id => {
                var def = devices.Devices.First(d => d.id == id);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) visit(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs)
                    if (state.CountItem(q.id) < q.n) recover(q.id, q.n - state.CountItem(q.id));
                var built = devices.Craft(id);
                Require(built.success, "craft " + id + ": " + built.reason + " inventory=" +
                    string.Join(",", def.crafting.inputs.Select(q => q.id + "=" + state.CountItem(q.id) + "/" + q.n).ToArray()));
                Require(devices.Place(id, id, state.currentRegion).success, "place " + id);
            };

            var before = JsonUtility.ToJson(state);
            Require(!world.NeutralizeSolutions().success && !world.PrecipitateSolution().success &&
                JsonUtility.ToJson(state) == before, "missing reagents mutated state");
            progression.RecordPickaxeRepaired();
            gather("rust_jungle", "resource_rust_metal_residue", 1);
            gather("rust_jungle", "resource_chloride_deposit", 1);
            progression.RecordCorrosionGateDiscovered();
            progression.RecordCorrosionRawMaterials();
            visit("prologue_lab");
            Require(progression.ProcessCorrosionBase().success && progression.ProcessCorrosionCoating().success &&
                progression.UpgradeCorrosionPickaxe().success && state.laboratoryStage == 1, "Stage 0 -> 1");

            // Carbon debris is power 2. Reach the authored Fe sampler with radiation
            // protection and actually craft the improved pick before furnace inputs.
            visit("dead_city_ruins");
            string sourceReason;
            for (int i = 0; i < 6; i++) Require(finite.Capture("slice_ore:dead_city_ruins:resource_stone", "resource_stone", 6, out sourceReason), "finite stone");
            for (int i = 0; i < 2; i++) Require(finite.Capture("slice_ore:dead_city_ruins:resource_ore", "resource_ore", 6, out sourceReason), "finite ore");
            Require(environment.UpgradeRadiationProtection() && environment.UpgradeRadiationProtection(), "deep access protection");
            gather("abandoned_mine", "element_fe", 4);
            visit("prologue_lab");
            var tools = new MechanicalToolService(state);
            Require(tools.Craft(MechanicalToolService.ImprovedRecipe, true).success && tools.Equip(MechanicalToolService.ImprovedTool).success,
                "finite Fe -> improved pickaxe");

            craft("device_chemical_workbench");
            craft("device_small_chemical_furnace");
            craft("device_synthesis_charging_station");
            craft("device_basic_reaction_bench");
            craft("device_multi_chemical_analyzer");
            craft("device_industrial_solid_liquid_filter");
            craft("device_industrial_distiller");
            gather("crystal_tundra", "resource_mineral_water", 1);
            Complete(devices, "device_multi_chemical_analyzer", "recipe_phase2_detect");
            gather("salt_marsh", "resource_acidic_solution", 1);
            gather("salt_marsh", "resource_alkaline_solution", 2);
            Require(world.NeutralizeSolutions().success && world.PrecipitateSolution().success, "slurry player source");
            Require(state.CountItem("resource_acidic_solution") == 0 && state.CountItem("resource_alkaline_solution") == 0 &&
                state.CountItem("compound_mixed_reaction_slurry") == 1, "upstream conservation");
            Complete(devices, "device_industrial_solid_liquid_filter", "recipe_phase2_filter");
            Complete(devices, "device_industrial_distiller", "recipe_phase2_distill");
            Require(state.laboratoryStage == 2, "Stage 1 -> 2");

            visit("dead_city_ruins");
            craft("device_industrial_condenser");
            craft("device_high_pressure_gas_tank");
            craft("device_large_industrial_reactor");
            craft("device_industrial_electrolysis_cell");
            gather("fossil_strata", "resource_carbonate_rock", 1);
            Complete(devices, "device_small_chemical_furnace", "recipe_phase3a_thermal_carbonate");
            Complete(devices, "device_high_pressure_gas_tank", "recipe_phase3a_store_gas");
            gather("fluorescent_grotto", "organic_glucose", 1);
            Require(world.StartOrganicProcess().success, "organic process");
            Complete(devices, "device_industrial_condenser", "recipe_phase3a_condense_vapor");
            gather("abandoned_mine", "resource_iron_bearing_ore", 1);
            Require(world.PretreatIronOre().success, "ore pretreatment");
            gather("ember_rift", "element_c", 1);
            Complete(devices, "device_large_industrial_reactor", "recipe_phase3b_reactor_batch");
            gather("prologue_lab", "element_h", 2);
            gather("dead_city_ruins", "element_o", 1);
            var reactions = new ReactionService(state);
            visit("salt_marsh");
            Require(reactions.Execute("reaction_water_synthesis", GameData.Regions.regions.First(r => r.id == state.currentRegion).tags).success,
                "water synthesis in an authored wet region");
            visit("magnetic_plateau");
            Complete(devices, "device_industrial_electrolysis_cell", "recipe_phase3b_electrolyze");
            visit("geode_abyss");
            Require(world.NaturalCrystallize("compound_concentrated_salt_solution", 0.90f).success, "natural crystallization");
            Require(state.laboratoryStage == 3 && LaboratoryProgressionService.Stage2To3Capabilities.All(state.HasLaboratoryCapability),
                "Stage 2 -> 3");
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            Require(restored.laboratoryStage == 3 && restored.craftedDeviceIds.Length == 11 &&
                restored.deviceInstances.Length == 11 && restored.CountItem("compound_mother_liquor") == 1 &&
                restored.HasVisitedRegion("rust_jungle") && restored.HasVisitedRegion("crystal_tundra") &&
                restored.visitedRegions.Contains("salt_marsh"),
                "whole service route save roundtrip");
            Require(state.resourceSources.Length > 0 && state.terrainChanges.Length > 0 && state.worldDrops.Length == 0, "route bypassed finite world");
            Debug.Log("[RouteAcceptance] new state -> finite sources + terrain/drop pickup -> 11 devices crafted/placed -> Stage 3 PASS; no raw inventory injection; UI/traversal NOT_VERIFIED");
        }

        static void Complete(DeviceRuntimeService devices, string id, string recipe)
        {
            var result = devices.Start(id, recipe);
            Require(result.success, recipe + ": " + result.reason);
            devices.Advance(10f);
            Require(devices.Collect(id).success, "collect " + recipe);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
