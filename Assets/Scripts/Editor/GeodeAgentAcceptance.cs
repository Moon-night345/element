using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class GeodeAgentAcceptance
    {
        const string Region = GeodeAbyssAgentBehavior.RegionId;
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        sealed class Rig
        {
            public GameState state = new GameState { currentRegion = Region, laboratoryStage = 2 };
            public AgentCatalog catalog = AgentCatalog.Load();
            public AgentController agent; public WorldChemistryService world; public DeviceRuntimeService devices; public long now = 10000;
            public Rig()
            {
                state.AddVisitedRegion(Region); agent = new AgentController(state, catalog);
                world = new WorldChemistryService(state); devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                world.GameplayEventPublished += e => { if (e.success && e.action_id == "natural_crystallization") Event("WORLD_REACTION_OBSERVED", e.action_id,
                    new AgentEvent { behavior_profile = Region, observed = true, phenomenon = "natural_crystallization_completed", has_concentration = e.has_concentration, concentration = e.concentration }); };
                devices.GameplayEventPublished += e => { if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect") Event("MATERIAL_ANALYZED", e.analyzed_sample_id); };
            }
            public AgentDecision Event(string type, string subject, AgentEvent evidence = null, bool ui = false)
            { now += 31000; while (agent.Queue.Current != null) agent.Queue.Advance(5, false); return agent.Publish(type, subject, now, uiExplains: ui, analyzable: true, evidence: evidence ?? new AgentEvent { behavior_profile = Region }); }
            public void Detector()
            {
                foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
                {
                    var def = GameData.Devices.devices.First(d => d.id == id);
                    if (!string.IsNullOrEmpty(def.crafting.unlock_region)) state.AddVisitedRegion(def.crafting.unlock_region);
                    foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                    Require(devices.Craft(id).success && devices.Place(id, "u23_" + id).success, "real detector manufacture");
                }
            }
        }
        public static void DetectorFacts()
        {
            string[] expected = { "geode_material_analysis", "geode_crystal_confirmed", "geode_crystal_confirmed", "geode_solution_confirmed" };
            for (int i = 0; i < WorldChemistryService.GeodeBlocks.Length; i++)
            {
                var r = new Rig(); string sample = WorldChemistryService.GeodeBlocks[i]; r.state.AddItem(sample);
                Require(r.Event("MATERIAL_ANALYZED", sample).reason == "knowledge_not_discovered", "forged analysis accepted");
                r.state.IdentifySample(sample); Require(r.Event("MATERIAL_ANALYZED", sample).reason == "knowledge_not_discovered", "generic identity became detector facts");
                r.Detector(); Require(r.devices.Start("u23_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string, string> { { "sample_id", sample } }).success,
                    "detector start");
                Require(r.state.CountItem(sample) == 1 && r.state.agent.geode.analysis_completed && r.agent.Queue.Current.reaction_id == expected[i] &&
                    !r.state.agent.geode.purity_observed && !r.agent.Queue.Current.text.Contains("99"), "wrong branch/consumption/invented purity");
                Require(r.Event("COMPOSITION_DISCOVERED", sample).reason == "geode_fact_already_explained", "fact delta replay");
            }
        }
        public static void NaturalProcessAndSave()
        {
            var r = new Rig(); r.Event("REGION_ENTERED", Region); r.Detector();
            string sample = WorldChemistryService.GeodeBlocks[3]; r.state.AddItem(sample);
            Require(r.devices.Start("u23_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string, string> { { "sample_id", sample } }).success, "analysis");
            r.state.AddItem("resource_mineral_solution", 2); string before = JsonUtility.ToJson(r.state);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, 0.79f }) Require(!r.world.NaturalCrystallize("resource_mineral_solution", invalid).success && before == JsonUtility.ToJson(r.state), "invalid concentration mutation");
            Require(r.world.NaturalCrystallize("resource_mineral_solution", 0.8f).success && r.state.CountItem("resource_mineral_solution") == 1 &&
                r.state.CountItem("resource_crystal") == 1 && r.state.CountItem("compound_mother_liquor") == 1, "natural process conservation");
            Require(r.state.agent.geode.mature && r.state.agent.geode.condition_compared && !r.state.agent.geode.purity_observed &&
                r.agent.Queue.Current.reaction_id == "geode_natural_crystallization_observed", "concentration/maturity/purity boundary");
            Require(r.Event("REGION_ENTERED", Region).reason == "geode_guidance_mature", "mature replay");
            string path = Path.GetFullPath(Path.Combine("Logs", "u23_save_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(r.state, out reason), reason);
            var loaded = repo.Read(); Require(loaded.success && loaded.state.agent.geode.mature && !loaded.state.agent.geode.purity_observed, "disk recovery");
            loaded.state.agent.geode.condition_compared = false; Require(!repo.Write(loaded.state, out reason), "invalid mature persisted");
            loaded = repo.Read(); loaded.state.agent.geode.failure_count = -1; Require(!repo.Write(loaded.state, out reason), "negative count persisted");
        }
        public static void ComparisonAndContracts()
        {
            var r = new Rig(); var observation = new AgentEvent { behavior_profile = Region, observed = true, phenomenon = "natural_crystallization_completed" };
            r.state.AddVisitedRegion("crystal_tundra"); r.state.AddChemistryKnowledge("crystal_tundra_crystallization_observed");
            r.state.AddVisitedRegion("salt_marsh"); r.state.AddChemistryKnowledge("precipitate_observed"); r.state.AddChemistryKnowledge("condenser_used"); r.state.AddChemistryKnowledge("industrial_crystallizer_need_discovered");
            foreach (string reaction in new[] { "geode_tundra_crystallization_recall", "geode_natural_crystallization_observed", "geode_precipitation_separation", "geode_condenser_separation", "geode_crystallizer_foreshadow", "geode_condition_hypothesis" })
                Require(r.Event("WORLD_REACTION_OBSERVED", "fixture", observation).reaction_id == reaction, "comparison order " + reaction);
            Require(!r.state.agent.geode.mature && !r.state.agent.geode.condition_compared && !r.state.agent.geode.purity_observed, "absent concentration/purity manufactured");
            Require(r.Event("WORLD_REACTION_OBSERVED", "fixture", observation).reason == "repeated_geode_crystallization", "repeated observation");
            var ladder = new Rig(); string sample = WorldChemistryService.GeodeBlocks[0];
            for (int i = 1; i <= 9; i++)
            {
                var d = ladder.Event("MATERIAL_COLLECTED", sample);
                if (i == 1) Require(d.reaction_id == "geode_unknown_material_observed", "first unknown");
                if (i % 3 == 0) Require(d.reaction_id == "geode_analysis_hint_level_" + i / 3, "analysis ladder");
            }
            Require(ladder.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "missing_input" }).Silent && ladder.state.agent.geode.failure_count == 0, "unapproved failure counted");
            for (int i = 1; i <= 6; i++)
            {
                var d = ladder.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "insufficient_concentration" });
                if (i == 1) Require(d.reaction_id == "geode_failure_observed", "first failure");
                if (i % 2 == 0) Require(d.reaction_id == "geode_failure_hint_level_" + i / 2, "failure ladder");
            }
            // Future authoritative-field contract only. Source currently publishes no purity value.
            Require(ladder.Event("MATERIAL_ANALYZED", "fixture", new AgentEvent { behavior_profile = Region, has_purity = true, purity = 0.9f }).reaction_id == "geode_purity_observed" &&
                ladder.state.agent.geode.purity_observed && !ladder.state.agent.geode.analysis_completed, "explicit purity contract");
            var old = new GameState(); old.agent.geode = null; new AgentController(old, r.catalog); Require(old.agent.geode != null, "old save default");
        }
    }
}
