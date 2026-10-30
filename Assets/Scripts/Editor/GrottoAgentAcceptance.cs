using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class GrottoAgentAcceptance
    {
        const string Region = FluorescentGrottoAgentBehavior.RegionId;
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        sealed class Rig
        {
            public GameState state = new GameState { currentRegion = Region };
            public AgentCatalog catalog = AgentCatalog.Load();
            public AgentController agent;
            public WorldChemistryService world;
            public DeviceRuntimeService devices;
            public bool ready = true, oxygen = true;
            public long now = 10000;
            public Rig()
            {
                state.AddVisitedRegion(Region);
                agent = new AgentController(state, catalog, () => new AgentEnvironmentFacts { ready = ready, region = state.currentRegion,
                    has_oxygen = oxygen, oxygen = catalog.grotto_authored_oxygen, light = catalog.grotto_authored_light });
                world = new WorldChemistryService(state);
                world.GameplayEventPublished += e => {
                    if (e.success && e.action_id == "analyze_grotto_block") Event("MATERIAL_ANALYZED", e.inventory_delta[0].id);
                    if (e.success && e.action_id == "organic_process") Event("WORLD_REACTION_OBSERVED", e.action_id,
                        new AgentEvent { behavior_profile = Region, observed = true, phenomenon = "organic_process_completed", gas_output = "compound_co2" });
                };
                devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                devices.GameplayEventPublished += e => {
                    if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect") Event("MATERIAL_ANALYZED", e.analyzed_sample_id);
                    if (e.event_type == "device_built" && e.device_id == FluorescentGrottoAgentBehavior.TankId) Event("DEVICE_CRAFTED", e.device_id);
                    if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase3a_store_gas") Event("WORLD_REACTION_OBSERVED", e.recipe_id,
                        new AgentEvent { behavior_profile = Region, observed = true, phenomenon = "gas_stored" });
                };
            }
            public AgentDecision Event(string type, string subject, AgentEvent evidence = null, bool ui = false)
            { now += 31000; Drain(); return agent.Publish(type, subject, now, uiExplains: ui, analyzable: true, evidence: evidence ?? new AgentEvent { behavior_profile = Region }); }
            public void Drain() { while (agent.Queue.Current != null) agent.Queue.Advance(5, false); }
        }
        public static void EnvironmentAndFacts()
        {
            var r = new Rig(); r.ready = false;
            Require(r.Event("REGION_ENTERED", Region).reason == "environment_state_unavailable" && !r.state.agent.grotto.oxygen_observed, "unready environment accepted");
            r.ready = true; r.oxygen = false;
            Require(r.Event("REGION_ENTERED", Region).Silent && !r.state.agent.grotto.oxygen_observed, "missing oxygen accepted");
            r.oxygen = true; r.state.AddVisitedRegion("ember_rift"); r.state.carbonFuelBurned = 1;
            Require(r.Event("REGION_ENTERED", Region).reaction_id == "grotto_ember_oxygen_recall" && r.state.agent.grotto.oxygen_observed, "combustion recall");
            string[] expected = { "grotto_material_analysis", "grotto_organic_confirmed", "grotto_organic_confirmed", "grotto_fermentable_confirmed" };
            for (int i = 0; i < WorldChemistryService.GrottoBlocks.Length; i++)
            {
                // Fresh region state isolates one-shot reactions while retaining real analysis service.
                var branch = new Rig(); string item = WorldChemistryService.GrottoBlocks[i];
                Require(branch.Event("MATERIAL_ANALYZED", item).reason == "knowledge_not_discovered", "forged analysis accepted");
                branch.state.AddItem(item); Require(branch.world.AnalyzeGrottoBlock(item).success && branch.state.CountItem(item) == 0, "analysis consumption");
                Require(branch.agent.Queue.Current.reaction_id == expected[i] && branch.state.agent.grotto.analysis_completed &&
                    !branch.agent.Queue.Current.text.Contains("CO₂"), "analysis branch / undiscovered exact gas leaked");
                Require(branch.Event("COMPOSITION_DISCOVERED", item).reason == "grotto_fact_already_explained", "fact difference lost");
                string before = JsonUtility.ToJson(branch.state);
                Require(!branch.world.AnalyzeGrottoBlock(item).success && before == JsonUtility.ToJson(branch.state), "failed analysis mutated state");
            }
            var ui = new Rig(); string sample = WorldChemistryService.GrottoBlocks[1]; ui.state.IdentifySample(sample);
            Require(ui.Event("MATERIAL_ANALYZED", sample, ui: true).reason == "ui_already_explains" && ui.state.agent.grotto.known_facts.Contains("organic"), "UI lost facts");
            Require(ui.Event("MATERIAL_ANALYZED", sample).reason == "grotto_fact_already_explained", "UI replay");
        }
        public static void Ladders()
        {
            var r = new Rig(); string sample = WorldChemistryService.GrottoBlocks[0];
            for (int i = 1; i <= 9; i++)
            {
                var d = r.Event("MATERIAL_COLLECTED", sample);
                if (i == 1) Require(d.reaction_id == "grotto_unknown_material_observed", "first unknown");
                if (i == 3 || i == 6 || i == 9) Require(d.reaction_id == "grotto_analysis_hint_level_" + (i / 3), "unknown ladder");
            }
            Require(r.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "missing_input" }).Silent && r.state.agent.grotto.failure_count == 0, "unapproved failure");
            for (int i = 1; i <= 6; i++)
            {
                var d = r.Event("EXPERIMENT_FAILED", "fixture", new AgentEvent { behavior_profile = Region, failure_reason = "missing_organic_material" });
                if (i == 1) Require(d.reaction_id == "grotto_process_failure_observed", "first formal failure");
                if (i % 2 == 0) Require(d.reaction_id == "grotto_failure_hint_level_" + (i / 2), "failure contract ladder");
            }
            Require(r.state.agent.grotto.failure_hint_level == 3 && r.state.agent.grotto.analysis_hint_level == 3, "ladder state");
            r.Event("REGION_ENTERED", Region); Require(r.state.agent.grotto.failure_count == 0 && r.state.agent.grotto.unknown_count == 0, "progress reset");
            var gate = new Rig(); gate.agent.Publish("MATERIAL_COLLECTED", sample, 1000, analyzable: true); gate.Drain();
            gate.agent.Publish("MATERIAL_COLLECTED", sample, 2000, analyzable: true);
            Require(gate.agent.Publish("MATERIAL_COLLECTED", sample, 29999, analyzable: true).reason == "grotto_collection_observed", "stall gate");
            Require(gate.agent.Publish("MATERIAL_COLLECTED", sample, 30000, analyzable: true).reaction_id == "grotto_analysis_hint_level_1", "30sec boundary");
        }
        public static void DetectorAndGasBranches()
        {
            var r = new Rig(); r.state.laboratoryStage = 2; r.state.AddVisitedRegion("dead_city_ruins");
            foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
            {
                var def = GameData.Devices.devices.First(d => d.id == id);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) r.state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) r.state.AddItem(q.id, q.n);
                Require(r.devices.Craft(id).success && r.devices.Place(id, "u22_" + id).success, "detector manufacture");
            }
            string sample = WorldChemistryService.GrottoBlocks[0]; r.state.AddItem(sample);
            Require(r.devices.Start("u22_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string, string> { { "sample_id", sample } }).success,
                "real detector start");
            Require(r.state.CountItem(sample) == 1 && r.state.agent.grotto.known_facts.Contains("wet_cave_material") &&
                !r.state.agent.grotto.known_facts.Contains("wet_cave_rock"), "detector borrowed destructive terrain facts");
            var gas = new Rig(); gas.state.IdentifySample("compound_co2"); gas.state.AddItem("organic_glucose");
            Require(gas.world.StartOrganicProcess().success && gas.agent.Queue.Current.text.Contains("二氧化碳"), "confirmed gas identity omitted");
            gas.state.laboratoryStage = 2; gas.state.AddChemistryKnowledge("condenser_used"); gas.state.AddItem("organic_glucose");
            Require(gas.world.StartOrganicProcess().success && gas.agent.Queue.Current.reaction_id == "grotto_condenser_tank_separation", "condenser/storage distinction");
            var stored = new Rig();
            Require(stored.Event("WORLD_REACTION_OBSERVED", "fixture", new AgentEvent { behavior_profile = Region, observed = false, phenomenon = "gas_stored" }).reason == "phenomenon_not_confirmed" &&
                !stored.state.agent.grotto.storage_understood, "unobserved storage accepted");
            Require(stored.Event("WORLD_REACTION_OBSERVED", "fixture", new AgentEvent { behavior_profile = Region, observed = true, phenomenon = "gas_stored" }).reaction_id == "grotto_gas_stored", "stored celebration contract");
        }
        public static void RealProcessAndStorage()
        {
            var r = new Rig(); r.Event("REGION_ENTERED", Region);
            string sample = WorldChemistryService.GrottoBlocks[3]; r.state.AddItem(sample); r.world.AnalyzeGrottoBlock(sample);
            int count = r.state.agent.grotto.gas_count;
            Require(!r.world.StartOrganicProcess().success && count == r.state.agent.grotto.gas_count, "failed process became observation");
            r.state.AddItem("organic_glucose", 3); Require(r.world.StartOrganicProcess().success && r.state.CountGeneratedGas("compound_co2") == 1 &&
                r.state.CountItem("organic_glucose") == 2 && r.agent.Queue.Current.reaction_id == "grotto_gas_produced", "real organic delivery");
            Require(!r.agent.Queue.Current.text.Contains("二氧化碳") && !r.state.agent.grotto.mature, "unconfirmed gas identity / premature maturity");
            Require(r.world.StartOrganicProcess().success && r.agent.Queue.Current.reaction_id == "grotto_gas_tank_hint_2", "no progression ladder");
            r.state.laboratoryStage = 2;
            Require(r.world.StartOrganicProcess().success && r.agent.Queue.Current.reaction_id == "grotto_gas_tank_hint_3", "progression ladder");
            var def = GameData.Devices.devices.First(d => d.id == FluorescentGrottoAgentBehavior.TankId);
            // Manufacturing ingredients and Stage are explicit fixtures; craft/place/start are real services.
            var bench = GameData.Devices.devices.First(d => d.id == "device_chemical_workbench");
            if (!string.IsNullOrEmpty(bench.crafting.unlock_region)) r.state.AddVisitedRegion(bench.crafting.unlock_region);
            foreach (var q in bench.crafting.inputs) r.state.AddItem(q.id, q.n);
            Require(r.devices.Craft(bench.id).success, "workbench prerequisite");
            if (!string.IsNullOrEmpty(def.crafting.unlock_region)) r.state.AddVisitedRegion(def.crafting.unlock_region);
            foreach (var q in def.crafting.inputs) r.state.AddItem(q.id, q.n);
            Require(r.devices.Craft(def.id).success && r.devices.Place(def.id, "u22_tank").success, "tank craft/place");
            Require(r.state.agent.grotto.mature && r.state.agent.grotto.storage_understood, "source five-condition maturity");
            Require(r.devices.Start("u22_tank", "recipe_phase3a_store_gas", null).success && r.state.CountStoredGas("compound_co2") == 1 &&
                r.state.CountGeneratedGas("compound_co2") == 2, "actual gas storage conservation");
            Require(r.Event("REGION_ENTERED", Region).reason == "grotto_guidance_mature", "mature reentry");
            string path = Path.GetFullPath(Path.Combine("Logs", "u22_save_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true); Require(repo.Write(r.state, out reason), reason);
            var restored = repo.Read(); Require(restored.success && restored.state.agent.grotto.mature && restored.state.CountStoredGas("compound_co2") == 1, "disk recovery");
            restored.state.agent.grotto.oxygen_observed = false; Require(!repo.Write(restored.state, out reason), "invalid maturity committed");
            restored = repo.Read(); restored.state.agent.grotto.failure_hint_level = 4; Require(!repo.Write(restored.state, out reason), "invalid ladder committed");
            var old = new GameState(); old.agent.grotto = null; new AgentController(old, r.catalog); Require(old.agent.grotto != null, "old save default");
        }
    }
}
