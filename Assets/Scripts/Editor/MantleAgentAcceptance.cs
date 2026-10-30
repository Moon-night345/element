using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class MantleAgentAcceptance
    {
        public static void LoadedSolutions()
        {
            string[] modes = { "distill", "concentrate", "neutralize" };
            string[] outputs = { "compound_distilled_water", "compound_concentrated_salt_solution", "compound_neutralized_salt_solution" };
            for (int i = 0; i < modes.Length; i++)
            {
                var s = new GameState { currentRegion = "mantle_flows" }; var world = new WorldChemistryService(s);
                if (world.ProcessLoadedSolution(modes[i]).success) throw new InvalidOperationException("missing loaded solution accepted");
                s.AddItem("compound_loaded_absorption_solution"); string before = JsonUtility.ToJson(s);
                if (world.ProcessLoadedSolution("invalid").success || before != JsonUtility.ToJson(s)) throw new InvalidOperationException("invalid loaded mode consumed inventory");
                if (!world.ProcessLoadedSolution(modes[i]).success || s.CountItem("compound_loaded_absorption_solution") != 0 || s.CountItem(outputs[i]) != 1 ||
                    !s.HasChemistryKnowledge("purification_discovery:" + modes[i]) || s.HasChemistryKnowledge("industrial_crystallizer_need_discovered")) throw new InvalidOperationException("loaded mode yield/invented unlock");
            }
            var state = new GameState { currentRegion = "mantle_flows", laboratoryStage = 3 }; var cat = AgentCatalog.Load(); state.AddVisitedRegion(state.currentRegion);
            var a = new AgentController(state, cat); var w = new WorldChemistryService(state); var d = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes); long now = 10000;
            w.GameplayEventPublished += e => { if (e.success && e.source_event == "loaded_solution_processed") a.Publish("WORLD_REACTION_OBSERVED", e.action_id, now += 31000, evidence: new AgentEvent { phenomenon = "loaded_solution_processed" }); };
            state.AddItem("compound_loaded_absorption_solution"); w.ProcessLoadedSolution("concentrate");
            if (!state.agent.mantle.industrial_chain_observed || state.agent.shown.Contains(cat.ForRegion(state.currentRegion).Reaction("crystallizer"))) throw new InvalidOperationException("missing progression generated device hint");
            // Actual prior natural process establishes the formal device-need knowledge; traversal is a fixture.
            state.currentRegion = "geode_abyss"; state.AddVisitedRegion(state.currentRegion); state.AddItem("resource_mineral_solution");
            if (!w.NaturalCrystallize("resource_mineral_solution", 0.9f).success) throw new InvalidOperationException("formal crystallizer need prerequisite");
            state.currentRegion = "mantle_flows"; int motherBefore = state.CountItem("compound_mother_liquor");
            foreach (string id in new[] { "device_chemical_workbench", "device_industrial_crystallizer" })
            {
                var def = d.Devices.First(x => x.id == id); state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                if (!d.Craft(id).success || !d.Place(id, id).success) throw new InvalidOperationException("loaded crystallizer manufacture");
            }
            state.AddItem("compound_loaded_absorption_solution");
            foreach (string bad in new[] { "NaN", "Infinity" })
            {
                if (d.Start("device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize", new System.Collections.Generic.Dictionary<string,string> { { "solution_id", "compound_loaded_absorption_solution" }, { "concentration", bad }, { "temperature", "0.1" } }).success || state.CountItem("compound_loaded_absorption_solution") != 1) throw new InvalidOperationException("non-finite crystallization consumed input");
                if (d.Start("device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize", new System.Collections.Generic.Dictionary<string,string> { { "solution_id", "compound_loaded_absorption_solution" }, { "concentration", "0.9" }, { "temperature", bad } }).success || state.CountItem("compound_loaded_absorption_solution") != 1) throw new InvalidOperationException("non-finite cooling consumed input");
            }
            if (!d.Start("device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize", new System.Collections.Generic.Dictionary<string,string> { { "solution_id", "compound_loaded_absorption_solution" }, { "concentration", "0.9" }, { "temperature", "0.35" } }).success || state.CountItem("compound_concentrated_salt_solution") != 1) throw new InvalidOperationException("explicit loaded input ignored");
            d.Advance(10); if (!d.Collect("device_industrial_crystallizer").success || state.CountItem("compound_loaded_absorption_solution") != 0 || state.CountItem("resource_high_purity_crystal") != 1 || state.CountItem("compound_mother_liquor") != motherBefore + 1)
                throw new InvalidOperationException("loaded crystallization actual yield");
        }
        public static void IndustrialDeviceAndSelection()
        {
            var cat = AgentCatalog.Load(); var s = new GameState { currentRegion = "mantle_flows", laboratoryStage = 3 }; s.AddVisitedRegion(s.currentRegion); s.AddChemistryKnowledge("industrial_crystallizer_need_discovered");
            var a = new AgentController(s, cat); var world = new WorldChemistryService(s); var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes); long now = 10000;
            world.GameplayEventPublished += e => { if (e.success && e.source_event == "extreme_heat_processed") a.Publish("WORLD_REACTION_OBSERVED", e.action_id, now += 31000,
                evidence: new AgentEvent { phenomenon = e.source_event, controlled_extreme_heat = true, process_input = e.subject_id }); };
            devices.GameplayEventPublished += e => { if (e.event_type == "device_output_collected" && e.recipe_id == "recipe_phase4a_industrial_crystallize")
                a.Publish("WORLD_REACTION_OBSERVED", e.recipe_id, now += 31000, evidence: new AgentEvent { phenomenon = "industrial_crystallization_completed" }); };
            foreach (string id in new[] { "device_chemical_workbench", "device_industrial_crystallizer" })
            {
                var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("actual crystallizer manufacture");
            }
            s.AddItem("element_s"); if (!world.ExtremeHeatProcess("element_s", 0.92f).success) throw new InvalidOperationException("actual thermal prerequisite");
            s.AddItem("compound_concentrated_salt_solution");
            if (devices.Start("device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize", new System.Collections.Generic.Dictionary<string,string> { { "concentration", "0.95" }, { "temperature", "1" } }).success || s.CountItem("compound_concentrated_salt_solution") != 1) throw new InvalidOperationException("cooling failure consumed solution");
            if (!devices.Start("device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize", new System.Collections.Generic.Dictionary<string,string> { { "concentration", "0.95" }, { "temperature", "0.1" } }).success || s.agent.mantle.mature) throw new InvalidOperationException("premature industrial maturity");
            devices.Advance(10); if (s.agent.mantle.crystallization_observed || s.CountItem("resource_high_purity_crystal") != 0) throw new InvalidOperationException("buffer not delivery");
            if (!devices.Collect("device_industrial_crystallizer").success || s.CountItem("resource_high_purity_crystal") != 1 || s.CountItem("compound_mother_liquor") != 1 || !s.agent.mantle.mature)
                throw new InvalidOperationException("actual industrial delivery/mature");
            for (int mode = 0; mode < 6; mode++)
            {
                var c = new GameState { currentRegion = "mantle_flows" };
                var e = new AgentEvent { phenomenon = "extreme_heat_processed", controlled_extreme_heat = true, process_input = "element_s" };
                string key;
                if (mode == 0) { c.AddVisitedRegion("ember_rift"); c.AddChemistryKnowledge("thermal:resource_carbonate_rock"); key = "ember_recall"; }
                else if (mode == 1) { var row = cat.analyses.First(r => r.region_id == "radiation_belt" && r.facts.Contains("strong_oxidizing")); c.AddVisitedRegion(row.region_id); c.IdentifySample(row.item_id); c.AddChemistryKnowledge("ph_discovery:" + row.item_id); key = "oxidation_recall"; }
                else if (mode == 2) { c.AddCraftedDevice("device_large_industrial_reactor"); e.processing_type = "bulk_reaction"; key = "reactor"; }
                else if (mode == 3) { c.AddCraftedDevice("device_small_chemical_furnace"); key = "furnace"; }
                else if (mode == 4) { c.AddCraftedDevice("device_industrial_condenser"); e.output_phase = "vapor"; e.condensable = true; key = "condenser"; }
                else { c.AddCraftedDevice("device_industrial_absorption_tower"); e.process_output = "compound_so2"; e.gas_treatment_required = true; key = "absorption"; }
                var d = new AgentController(c, cat).Publish("WORLD_REACTION_OBSERVED", "formal_contract_fixture", 10000, evidence: e);
                if (d.reaction_id != cat.ForRegion(c.currentRegion).Reaction(key)) throw new InvalidOperationException("mantle reuse/recall source selection " + mode);
            }
        }
        public static void ThermalContracts()
        {
            var cat = AgentCatalog.Load(); var s = new GameState { currentRegion = "mantle_flows", laboratoryStage = 3 }; s.AddVisitedRegion(s.currentRegion);
            var a = new AgentController(s, cat); var world = new WorldChemistryService(s); long now = 10000;
            world.GameplayEventPublished += e => { if (e.success && e.source_event == "extreme_heat_processed") a.Publish("WORLD_REACTION_OBSERVED", e.action_id, now += 31000,
                evidence: new AgentEvent { behavior_profile = s.currentRegion, phenomenon = "extreme_heat_processed", controlled_extreme_heat = true, process_input = e.subject_id }); };
            a.Publish("WORLD_REACTION_OBSERVED", "contract", now, evidence: new AgentEvent { phenomenon = "extreme_heat_processed" });
            if (s.agent.mantle.thermal_processed) throw new InvalidOperationException("uncontrolled heat counted as process");
            s.AddItem("element_s"); string before = JsonUtility.ToJson(s);
            foreach (float invalid in new[] { 0.89f, float.NaN, float.PositiveInfinity }) if (world.ExtremeHeatProcess("element_s", invalid).success || before != JsonUtility.ToJson(s)) throw new InvalidOperationException("invalid thermal condition mutation");
            if (!world.ExtremeHeatProcess("element_s", 0.9f).success || s.CountItem("element_s") != 0 || s.CountGeneratedGas("compound_so2") != 1 || s.CountItem("compound_so2") != 0 || !s.agent.mantle.thermal_processed || s.agent.mantle.mature)
                throw new InvalidOperationException("sulfur real process/buffer/maturity");
            s.AddItem("ion_so4"); before = JsonUtility.ToJson(s);
            if (world.ExtremeHeatProcess("ion_so4", 0.92f).success || before != JsonUtility.ToJson(s)) throw new InvalidOperationException("missing water consumed sulfate");
            s.AddItem("compound_h2o");
            if (!world.ExtremeHeatProcess("ion_so4", 0.92f).success || s.CountItem("compound_h2so4") != 1 || s.CountItem("ion_so4") != 0 || s.CountItem("compound_h2o") != 0) throw new InvalidOperationException("actual sulfate process conservation");
            // Formal crystallization event contract only; actual device chain remains next.
            a.Publish("WORLD_REACTION_OBSERVED", "contract", now += 31000, evidence: new AgentEvent { phenomenon = "industrial_crystallization_completed" });
            if (!s.agent.mantle.mature || a.Publish("REGION_ENTERED", s.currentRegion, now += 31000).reason != "mantle_guidance_mature") throw new InvalidOperationException("five-condition maturity");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u27_process_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            if (!repo.Write(s, out error) || !repo.Read().state.agent.mantle.mature) throw new InvalidOperationException("mantle process disk " + error);
            var loaded = repo.Read(); loaded.state.agent.mantle.crystallization_observed = false; if (repo.Write(loaded.state, out error)) throw new InvalidOperationException("invalid mantle mature saved");
            var overrideState = new GameState { currentRegion = s.currentRegion };
            var overrideAgent = new AgentController(overrideState, cat, () => new AgentEnvironmentFacts { ready = true, region = s.currentRegion, has_temperature = true, temperature = 0.95f });
            if (overrideAgent.Publish("REGION_ENTERED", s.currentRegion, 10000, evidence: new AgentEvent { has_temperature = true, temperature = 0.1f }).action != "FOLLOW_UP") throw new InvalidOperationException("explicit temperature failed to override environment");
            var failures = new GameState { currentRegion = s.currentRegion }; var f = new AgentController(failures, cat);
            for (int i = 1; i <= 6; i++) { while (f.Queue.Current != null) f.Queue.Advance(5, false); f.Publish("EXPERIMENT_FAILED", "fixture", i * 31000, evidence: new AgentEvent { behavior_profile = s.currentRegion, failure_reason = "wrong_device" }); }
            if (failures.agent.mantle.failure_count != 6 || !failures.agent.shown.Contains(cat.ForRegion(s.currentRegion).Reaction("failure_3"))) throw new InvalidOperationException("mantle formal failure ladder");
        }
        public static void EntryAndDetection()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("mantle_flows");
            foreach (float temperature in new[] { 0.1f, p.minimum_heat_temperature, cat.mantle_authored_temperature_floor, float.NaN, float.PositiveInfinity })
            {
                var s = new GameState { currentRegion = "mantle_flows" };
                var env = new AgentEnvironmentFacts { ready = true, region = s.currentRegion, has_temperature = true, temperature = temperature };
                string before = JsonUtility.ToJson(s);
                var context = new AgentContext(s, cat, env); env.temperature = 1f;
                var d = new AgentDecisionPolicy(cat).Decide(new AgentEvent { type = "REGION_ENTERED", subject = s.currentRegion, region = s.currentRegion, time_ms = 10000 }, context, s.agent, 0);
                bool hot = !float.IsNaN(temperature) && !float.IsInfinity(temperature) && temperature >= p.minimum_heat_temperature;
                if (d.action != (hot ? "WARN" : "FOLLOW_UP") || before != JsonUtility.ToJson(s)) throw new InvalidOperationException("mantle temperature/source isolation");
            }
            foreach (var row in cat.analyses.Where(r => r.region_id == "mantle_flows"))
            {
                var s = new GameState { currentRegion = "mantle_flows", laboratoryStage = 3 }; s.AddItem(row.item_id);
                var a = new AgentController(s, cat); var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                s.IdentifySample(row.item_id);
                if (a.Publish("MATERIAL_ANALYZED", row.item_id, 10000).reason != "mantle_analysis_not_authoritative") throw new InvalidOperationException("ordinary identity inferred mantle composition");
                foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
                {
                    var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                    foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                    if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("actual mantle detector manufacture");
                }
                devices.GameplayEventPublished += e => { if (e.event_type == "device_processing_started") a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 20000); };
                if (!devices.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string,string> { { "sample_id", row.item_id } }).success || s.CountItem(row.item_id) != 1)
                    throw new InvalidOperationException("actual mantle analysis consumed sample");
                if (a.Queue.Current == null || a.Queue.Current.reaction_id != "mantle_material_analysis" || s.agent.mantle.sulfur_observed != row.facts.Intersect(p.sulfur_facts).Any()) throw new InvalidOperationException("mantle analysis branch/facts");
                if (a.Queue.Current.text.Contains("硫酸根") || a.Queue.Current.text.Contains("SO4")) throw new InvalidOperationException("invented sulfate from sulfur property");
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u27_analysis_" + Guid.NewGuid().ToString("N"), "save.json")), error;
                var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
                if (!repo.Write(s, out error) || repo.Read().state.agent.mantle.sulfur_observed != s.agent.mantle.sulfur_observed) throw new InvalidOperationException("mantle disk snapshot " + error);
            }
        }
    }
}
