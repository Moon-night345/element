using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class RadiationAgentAcceptance
    {
        static string Gameplay(GameState s) { var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(s)); copy.agent = null; return JsonUtility.ToJson(copy); }
        public static void ScansAndPresence()
        {
            var cat = AgentCatalog.Load();
            foreach (string item in WorldChemistryService.RadiationSamples)
            {
                var s = new GameState { currentRegion = "radiation_belt" }; var world = new WorldChemistryService(s);
                string before = Gameplay(s); if (world.AnalyzeRadiationSample(item).success || before != Gameplay(s)) throw new InvalidOperationException("missing scan sample changed gameplay");
                s.AddItem(item); s.IdentifySample(item);
                if (new AgentContext(s, cat).confirmed_analyses.Any(r => r.item_id == item)) throw new InvalidOperationException("ordinary identity became scan facts");
                if (!world.AnalyzeRadiationSample(item).success || s.CountItem(item) != 1 || s.HasChemistryKnowledge("ph_discovery:" + item)) throw new InvalidOperationException("scan cost/pH invented");
                var row = new AgentContext(s, cat).confirmed_analyses.Single(r => r.item_id == item);
                if (!row.facts.SequenceEqual(cat.analyses.Single(r => r.item_id == item).facts)) throw new InvalidOperationException("scan source composition mismatch");
                var agent = new AgentController(s, cat); agent.Publish("COMPOSITION_DISCOVERED", item, 10000);
                if (s.agent.radiation.contamination_analyzed || s.agent.radiation.oxidation_observed) throw new InvalidOperationException("scan name inferred contamination/oxidation");
            }
            foreach (bool identityField in new[] { false, true })
            {
                var s = new GameState { currentRegion = "radiation_belt" }; var agent = new AgentController(s, cat);
                var decision = agent.Publish("MATERIAL_ANALYZED", "explicit_field_contract", 10000, evidence: new AgentEvent { has_contamination_record = !identityField, has_contaminated_identity = identityField });
                if (!s.agent.radiation.contamination_analyzed || decision.reaction_id != cat.ForRegion(s.currentRegion).Reaction("contamination")) throw new InvalidOperationException("source presence contract lost");
            }
            var hazard = new GameState { currentRegion = "radiation_belt" };
            if (new AgentController(hazard, cat).Publish("REGION_ENTERED", hazard.currentRegion, 10000, evidence: new AgentEvent { danger = true }).action != "WARN") throw new InvalidOperationException("explicit hazard lost in normalization");
        }
        public static void ActualStorageAbsorption()
        {
            var s = new GameState { currentRegion = "radiation_belt", laboratoryStage = 3 }; var cat = AgentCatalog.Load(); var a = new AgentController(s, cat);
            var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes); long now = 10000;
            foreach (string id in new[] { "device_chemical_workbench", "device_high_pressure_gas_tank", "device_industrial_absorption_tower" })
            {
                var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("actual gas device manufacture");
            }
            devices.GameplayEventPublished += e => { if (e.event_type == "device_output_collected" && e.recipe_id == "recipe_phase4a_absorb_gas")
                a.Publish("WORLD_REACTION_OBSERVED", e.recipe_id, now += 31000, evidence: new AgentEvent { behavior_profile = s.currentRegion, phenomenon = "gas_absorbed", has_gas = true }); };
            var world = new WorldChemistryService(s); s.AddItem("item_radiation_belt_gas_bearing_deposit_block");
            if (!world.ProcessGasBearingDeposit().success) throw new InvalidOperationException("actual contaminated gas production");
            if (!devices.Start("device_high_pressure_gas_tank", "recipe_phase3a_store_gas", new System.Collections.Generic.Dictionary<string,string> { { "gas_id", "compound_contaminated_gas_stream" } }).success ||
                s.CountGeneratedGas("compound_contaminated_gas_stream") != 0 || s.CountStoredGas("compound_contaminated_gas_stream") != 1) throw new InvalidOperationException("real gas storage conservation");
            if (devices.Start("device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null).success || s.CountItem("compound_purified_gas_stream") != 0) throw new InvalidOperationException("missing absorbent accepted");
            if (s.CountStoredGas("compound_contaminated_gas_stream") != 1 || s.agent.radiation.purification_verified) throw new InvalidOperationException("failed absorption consumed gas");
            s.AddItem("resource_alkaline_solution");
            if (!devices.Start("device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null).success || s.CountStoredGas("compound_contaminated_gas_stream") != 0 || s.CountItem("resource_alkaline_solution") != 0 || s.agent.radiation.purification_verified)
                throw new InvalidOperationException("absorption reservation/premature observation");
            devices.Advance(10); if (s.agent.radiation.purification_verified) throw new InvalidOperationException("buffer treated as delivery");
            if (!devices.Collect("device_industrial_absorption_tower").success || s.CountItem("compound_purified_gas_stream") != 1 || s.CountItem("compound_contaminated_absorbent") != 1 ||
                !s.agent.radiation.purification_verified || !s.agent.radiation.gas_handled || s.agent.radiation.mature) throw new InvalidOperationException("absorption delivery/source precedence");
        }
        public static void ProcessAndMaturity()
        {
            var cat = AgentCatalog.Load(); var s = new GameState { currentRegion = "radiation_belt", laboratoryStage = 2 }; var a = new AgentController(s, cat);
            var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes); long now = 10000;
            foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
            {
                var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("radiation process detector manufacture");
            }
            devices.GameplayEventPublished += e => { if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect") {
                a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, now += 31000); a.Publish("MATERIAL_PROPERTY_DISCOVERED", e.analyzed_sample_id, now += 31000); } };
            string item = cat.analyses.First(r => r.region_id == s.currentRegion && r.facts.Contains("irradiated")).item_id; s.AddItem(item);
            if (!devices.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string,string> { { "sample_id", item } }).success) throw new InvalidOperationException("radiation confirmed analysis");
            if (!s.agent.radiation.contamination_analyzed || !s.agent.radiation.oxidation_observed || s.agent.radiation.mature) throw new InvalidOperationException("analysis/process maturity conflated");
            var world = new WorldChemistryService(s);
            world.GameplayEventPublished += e => { if (!e.success) return;
                if (e.action_id == "process_gas_bearing_deposit") a.Publish("WORLD_REACTION_OBSERVED", e.action_id, now += 31000, evidence: new AgentEvent { behavior_profile = s.currentRegion, has_gas = true, gas_output = "compound_contaminated_gas_stream" });
                if (e.action_id == "purify_contamination") a.Publish("WORLD_REACTION_OBSERVED", e.action_id, now += 31000, evidence: new AgentEvent { behavior_profile = s.currentRegion, phenomenon = "contamination_purified" }); };
            s.AddItem("item_radiation_belt_gas_bearing_deposit_block");
            if (!world.ProcessGasBearingDeposit().success || !s.agent.radiation.gas_handled || s.agent.radiation.purification_verified || s.agent.radiation.mature) throw new InvalidOperationException("gas falsely purified");
            s.AddItem("resource_contaminated_mineral");
            if (world.PurifyContamination("resource_contaminated_mineral", 1f).success || s.CountItem("resource_contaminated_mineral") != 1 || s.agent.radiation.purification_verified) throw new InvalidOperationException("failed purification mutated");
            if (!world.PurifyContamination("resource_contaminated_mineral", 0.5f).success || !s.agent.radiation.mature || s.CountItem("resource_iron_bearing_ore") != 1) throw new InvalidOperationException("actual purification maturity");
            if (a.Publish("REGION_ENTERED", s.currentRegion, now += 31000).reason != "radiation_guidance_mature") throw new InvalidOperationException("mature revisit");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u26_process_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            if (!repo.Write(s, out error) || !repo.Read().state.agent.radiation.mature) throw new InvalidOperationException("radiation process disk " + error);
            var loaded = repo.Read(); loaded.state.agent.radiation.gas_handled = false; if (repo.Write(loaded.state, out error)) throw new InvalidOperationException("invalid radiation mature saved");
            foreach (bool tower in new[] { false, true })
            {
                var c = new GameState { currentRegion = "radiation_belt" }; if (tower) c.AddCraftedDevice("device_industrial_absorption_tower");
                var decision = new AgentController(c, cat).Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 10000, evidence: new AgentEvent { phenomenon = "gas_absorbed" });
                if (decision.reaction_id != cat.ForRegion(c.currentRegion).Reaction(tower ? "absorption" : "tower") || !c.agent.radiation.purification_verified || c.agent.radiation.mature)
                    throw new InvalidOperationException("absorption source precedence/maturity gate");
            }
            var gasResult = new GameState { currentRegion = "radiation_belt" };
            var gasDecision = new AgentController(gasResult, cat).Publish("WORLD_REACTION_OBSERVED", "source_result_fixture", 10000,
                evidence: new AgentEvent { phenomenon = "gas_absorbed", has_gas = true });
            if (gasDecision.reaction_id != cat.ForRegion(gasResult.currentRegion).Reaction("gas") || !gasResult.agent.radiation.gas_handled || !gasResult.agent.radiation.purification_verified)
                throw new InvalidOperationException("source absorption gas field precedence");
        }
        public static void EnvironmentAndAnalyses()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("radiation_belt");
            foreach (float value in new[] { 0f, 0.5f, float.NaN, float.PositiveInfinity })
            {
                var s = new GameState { currentRegion = "radiation_belt" };
                var a = new AgentController(s, cat, () => new AgentEnvironmentFacts { region = s.currentRegion, ready = true, has_radiation = true, radiation = value });
                string before = Gameplay(s);
                var d = a.Publish("REGION_ENTERED", s.currentRegion, 10000);
                if (d.action != (value == 0.5f ? "WARN" : "FOLLOW_UP") || Gameplay(s) != before) throw new InvalidOperationException("radiation hazard gate");
            }
            foreach (var row in cat.analyses.Where(r => r.region_id == "radiation_belt" && !r.requires_scan))
            {
                var s = new GameState { currentRegion = "radiation_belt", laboratoryStage = 2 }; s.AddItem(row.item_id);
                var a = new AgentController(s, cat); var dev = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                if (a.Publish("MATERIAL_ANALYZED", row.item_id, 10000).reason != "radiation_fact_not_confirmed") throw new InvalidOperationException("forged contamination");
                foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
                {
                    var def = dev.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                    foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                    if (!dev.Craft(id).success || !dev.Place(id, id).success) throw new InvalidOperationException("radiation real detector manufacture");
                }
                dev.GameplayEventPublished += e => { if (e.event_type == "device_processing_started") a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 20000); };
                if (!dev.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string,string> { { "sample_id", row.item_id } }).success)
                    throw new InvalidOperationException("radiation real detection");
                bool contaminated = row.facts.Intersect(p.contamination_facts).Any();
                if (s.CountItem(row.item_id) != 1 || s.agent.radiation.contamination_analyzed != contaminated || s.agent.radiation.oxidation_observed)
                    throw new InvalidOperationException("contamination/oxidation source distinction");
                if (contaminated && (a.Queue.Current == null || a.Queue.Current.reaction_id != p.Reaction("contamination"))) throw new InvalidOperationException("source contamination response");
                a.Publish("MATERIAL_PROPERTY_DISCOVERED", row.item_id, 30000);
                if (s.agent.radiation.oxidation_observed != row.facts.Intersect(p.oxidation_properties).Any()) throw new InvalidOperationException("oxidation property confirmation");
                string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u26_analysis_" + Guid.NewGuid().ToString("N"), "save.json")), error;
                var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
                if (!repo.Write(s, out error) || !repo.Read().success) throw new InvalidOperationException("radiation disk snapshot " + error);
                var loaded = repo.Read(); loaded.state.agent.radiation.failure_count = -1; if (repo.Write(loaded.state, out error)) throw new InvalidOperationException("negative radiation counter persisted");
            }
        }
    }
}
