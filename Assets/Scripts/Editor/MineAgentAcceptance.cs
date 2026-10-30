using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class MineAgentAcceptance
    {
        const string Region = AbandonedMineAgentBehavior.RegionId;
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        sealed class Rig
        {
            public GameState state = new GameState { currentRegion = Region, laboratoryStage = 2 };
            public AgentCatalog catalog = AgentCatalog.Load(); public AgentController agent; public WorldChemistryService world; public long now = 10000;
            public Rig()
            {
                state.AddVisitedRegion(Region); agent = new AgentController(state, catalog); world = new WorldChemistryService(state);
                world.GameplayEventPublished += e => {
                    if (e.action_id == "metal_displacement") Event("WORLD_REACTION_OBSERVED", e.action_id, new AgentEvent { behavior_profile = Region, phenomenon = "metal_displacement", result_success = e.success, discovery = e.discovery, failure_reason = e.reason });
                    else if (e.success && e.action_id == "identify_ore") Event("MATERIAL_ANALYZED", e.subject_id);
                    else if (e.success && e.action_id == "analyze_mine_block") Event("MATERIAL_ANALYZED", e.inventory_delta[0].id);
                    else if (e.success && e.source_event == "metal_recovered") Event("MATERIAL_RECOVERED", e.metal, new AgentEvent { behavior_profile = Region, source_event = "metal_recovered" });
                    else if (e.success && e.source_event == "precipitation_test") Event("WORLD_REACTION_OBSERVED", e.subject_id, new AgentEvent { behavior_profile = Region, phenomenon = "precipitation_test" });
                };
            }
            public AgentDecision Event(string type, string subject, AgentEvent evidence = null, bool ui = false)
            { now += 31000; while (agent.Queue.Current != null) agent.Queue.Advance(5, false); return agent.Publish(type, subject, now, uiExplains: ui, analyzable: true, evidence: evidence ?? new AgentEvent { behavior_profile = Region }); }
        }
        public static void IdentificationAndProcessing()
        {
            Require(GameData.OreProfiles.ores.Length == 8, "authoritative ore count");
            foreach (var profile in GameData.OreProfiles.ores.Take(6))
            {
                var r = new Rig(); string before = JsonUtility.ToJson(r.state);
                Require(!r.world.IdentifyOre(profile.id).success && before == JsonUtility.ToJson(r.state), "missing ore mutated");
                r.state.AddItem(profile.id);
                Require(r.world.IdentifyOre(profile.id).success && r.state.CountItem(profile.id) == 1 && r.state.agent.mine.known_facts.Contains(profile.metal), "ore identity/consumption");
                Require(r.Event("MATERIAL_ANALYZED", profile.id).reason == "mine_analysis_already_explained", "identity repeated");
                Require(r.world.PretreatOre(profile.id).success && r.state.CountItem(profile.id) == 0 && r.state.CountItem(profile.oxide) == 1, "pretreatment conservation");
                if (!GameData.OreProfiles.ores.Take(4).Contains(profile)) continue;
                before = JsonUtility.ToJson(r.state); Require(!r.world.SmeltOre(profile.metal).success && before == JsonUtility.ToJson(r.state), "missing carbon mutated");
                r.state.AddItem("element_c"); Require(r.world.SmeltOre(profile.metal).success && r.state.CountItem(profile.oxide) == 0 && r.state.CountItem(profile.metal) == 1 &&
                    r.state.agent.mine.metallurgy_observed && r.state.CountItem("element_c") == 0, "smelt actual recovery");
            }
            var mapping = new Rig();
            foreach (string sample in new[] { WorldChemistryService.MineBlocks[2], WorldChemistryService.MineBlocks[0], WorldChemistryService.MineBlocks[4] })
            { mapping.state.AddItem(sample); Require(mapping.world.AnalyzeMineBlock(sample).success && mapping.state.CountItem(sample) == 0, "terrain analysis"); }
            Require(mapping.state.agent.shown.Contains("mine_ore_identity_confirmed") && mapping.state.agent.shown.Contains("mine_furnace_reuse") && mapping.state.agent.shown.Contains("mine_reactor_need"), "source formal processing mapping");
            Require(mapping.state.agent.mine.material_difference && !mapping.state.agent.mine.mature, "analysis only maturity");
        }
        public static void DisplacementPrecipitationAndSave()
        {
            var r = new Rig(); r.state.AddItem("element_cu"); r.state.AddItem("compound_fe2o3");
            var no = r.world.MetalDisplacement("element_cu", "compound_fe2o3");
            Require(!no.success && no.reason == "no_reaction" && r.state.CountItem("element_cu") == 1 && r.state.CountItem("compound_fe2o3") == 1 &&
                r.state.chemistryKnowledgeIds.Contains("no_reaction:element_cu>compound_fe2o3") && r.agent.Queue.Current.reaction_id == "mine_no_displacement_observed", "no-reaction contract");
            r.state.AddItem("element_zn"); r.state.AddItem("compound_cuso4");
            Require(r.world.MetalDisplacement("element_zn", "compound_cuso4").success && r.state.CountItem("element_cu") == 2 && r.state.CountItem("compound_znso4") == 1 &&
                r.state.agent.mine.material_difference && r.agent.Queue.Current.reaction_id == "mine_displacement_compared", "actual displacement comparison");
            r.state.AddVisitedRegion("salt_marsh"); r.state.AddChemistryKnowledge("precipitate_observed");
            r.state.AddVisitedRegion("geode_abyss"); r.state.AddChemistryKnowledge("natural_crystallization:resource_mineral_solution");
            r.state.AddCraftedDevice(AbandonedMineAgentBehavior.Filter);
            foreach (string reaction in new[] { "mine_salt_precipitation_recall", "mine_precipitation_crystallization_guard", "mine_filter_reuse", "mine_precipitation_observed" })
            {
                r.state.AddItem("element_ba"); r.state.AddItem("ion_so4");
                Require(r.world.PrecipitationTest("element_ba", "ion_so4").success && r.agent.Queue.Current.reaction_id == reaction, "precipitation branch " + reaction);
            }
            string before = JsonUtility.ToJson(r.state); Require(!r.world.PrecipitationTest("element_fe", "element_cl").success && before == JsonUtility.ToJson(r.state), "unsupported precipitation mutated");
            r.state.AddItem("resource_iron_bearing_ore"); r.world.IdentifyOre("resource_iron_bearing_ore");
            r.state.AddItem("resource_copper_bearing_ore"); r.world.IdentifyOre("resource_copper_bearing_ore");
            r.world.PretreatIronOre(); r.state.AddItem("element_c");
            Require(r.world.SmeltIron().success && r.state.agent.mine.mature, "three-condition maturity");
            Require(r.Event("REGION_ENTERED", Region).reason == "mine_guidance_mature", "mature reentry");
            string path = Path.GetFullPath(Path.Combine("Logs", "u24_save_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(r.state, out reason), reason);
            var loaded = repo.Read(); Require(loaded.success && loaded.state.agent.mine.mature && loaded.state.agent.mine.displacement_ids.Length == 2 && loaded.state.CountItem("compound_baso4") == 4, "mine recovery");
            loaded.state.agent.mine.metallurgy_observed = false; Require(!repo.Write(loaded.state, out reason), "invalid maturity saved");
        }
        public static void RealDetectorAndBatch()
        {
            var r = new Rig(); var devices = new DeviceRuntimeService(r.state, GameData.Devices, GameData.ProcessingRecipes);
            devices.GameplayEventPublished += e => {
                if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect") r.Event("MATERIAL_ANALYZED", e.analyzed_sample_id);
                if (e.event_type == "device_output_collected" && e.recipe_id == "barium_sulfate_batch_precipitation")
                    r.Event("WORLD_REACTION_OBSERVED", e.recipe_id, new AgentEvent { behavior_profile = Region, phenomenon = "first_industrial_precipitation_completed" });
            };
            foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId, AbandonedMineAgentBehavior.Reactor })
            {
                var def = GameData.Devices.devices.First(d => d.id == id);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) r.state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) r.state.AddItem(q.id, q.n);
                Require(devices.Craft(id).success && devices.Place(id, "u24_" + id).success, "actual device manufacture " + id);
            }
            foreach (var row in r.catalog.mine_detector_analyses)
            {
                r.state.AddItem(row.item_id);
                Require(devices.Start("u24_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string, string> { { "sample_id", row.item_id } }).success,
                    "mine actual detector start");
                devices.Advance(10); Require(devices.Collect("u24_" + CrystalTundraAgentBehavior.DeviceId).success, "mine detector collection");
                Require(r.state.CountItem(row.item_id) == 1 && row.facts.All(r.state.agent.mine.known_facts.Contains), "detector consumption/source fact mismatch");
            }
            r.state.AddItem("compound_barium_process_solution", 5); r.state.AddItem("compound_sulfate_process_solution", 5);
            Require(devices.Start("u24_" + AbandonedMineAgentBehavior.Reactor, "barium_sulfate_batch_precipitation", null).success &&
                !r.state.agent.mine.precipitation_observed && r.state.CountItem("compound_barium_process_solution") == 0, "premature industrial observation");
            devices.Advance(10); Require(!r.state.agent.mine.precipitation_observed, "buffer treated as delivery");
            Require(devices.Collect("u24_" + AbandonedMineAgentBehavior.Reactor).success && r.state.agent.mine.precipitation_observed &&
                r.state.CountItem("compound_barium_sulfate_reaction_slurry") == 5, "actual industrial delivery observation");
        }
        public static void LaddersAndGuards()
        {
            var r = new Rig(); string sample = WorldChemistryService.MineBlocks[0];
            Require(r.Event("MATERIAL_ANALYZED", sample).reason == "mine_identity_not_authoritative", "invented identity");
            for (int i = 1; i <= 9; i++)
            {
                var d = r.Event("MATERIAL_COLLECTED", sample);
                if (i == 1) Require(d.reaction_id == "mine_unknown_ore_observed", "unknown ore");
                if (i % 3 == 0) Require(d.reaction_id == "mine_analysis_hint_" + i / 3, "stall ladder");
            }
            Require(r.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "missing_input" }).Silent && r.state.agent.mine.failure_count == 0, "wrong formal failure");
            for (int i = 1; i <= 6; i++)
            {
                var d = r.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "wrong_device" });
                if (i == 1) Require(d.reaction_id == "mine_failure_observed", "first formal failure");
                if (i % 2 == 0) Require(d.reaction_id == "mine_failure_hint_" + i / 2, "failure ladder");
            }
            r.Event("REGION_ENTERED", Region); Require(r.state.agent.mine.failure_count == 0 && r.state.agent.mine.unknown_count == 0, "progress reset");
            r.state.AddItem(sample); r.world.AnalyzeMineBlock(sample);
            string before = JsonUtility.ToJson(r.state);
            new AgentDecisionPolicy(r.catalog).Decide(new AgentEvent { type = "MATERIAL_ANALYZED", subject = sample, region = Region, time_ms = r.now }, new AgentContext(r.state, r.catalog), r.state.agent, 0);
            Require(before == JsonUtility.ToJson(r.state), "policy mutated gameplay or state input");
            var old = new GameState(); old.agent.mine = null; new AgentController(old, r.catalog); Require(old.agent.mine != null, "old save migration");
        }
    }
}
