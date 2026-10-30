using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class EmberThermalAcceptance
    {
        static void Require(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        public static void FactDeltas()
        {
            var catalog = AgentCatalog.Load(); var sample = catalog.analyses.First(a => a.region_id == "ember_rift" && a.facts.Contains("element_s"));
            var state = new GameState { currentRegion = "ember_rift" };
            state.IdentifySample(sample.item_id);
            // A partially known composition is an explicit attention fixture, not invented gameplay knowledge.
            state.agent.ember.known_facts = new[] { "element_s" };
            var agent = new AgentController(state, catalog); var tag = new AgentEvent { behavior_profile = "ember_rift" };
            var delta = agent.Publish("MATERIAL_ANALYZED", sample.item_id, 10000, evidence: tag);
            Require(delta.reaction_id == "ember_material_analysis" && !delta.confirmed_facts.Contains("element_s") &&
                delta.confirmed_facts.SequenceEqual(sample.facts.Where(f => f != "element_s")), "old sulfur selected exact explanation again");
            Require(agent.Publish("COMPOSITION_DISCOVERED", sample.item_id, 11000, evidence: tag).reason == "ember_fact_already_explained" &&
                state.agent.ember.last_progress_ms == 11000, "repeated confirmed analysis did not retain source progress semantics");
            var ui = new GameState { currentRegion = "ember_rift" }; ui.IdentifySample(sample.item_id);
            var uiAgent = new AgentController(ui, catalog);
            Require(uiAgent.Publish("MATERIAL_ANALYZED", sample.item_id, 10000, uiExplains: true, evidence: tag).reason == "ui_already_explains" &&
                ui.agent.ember.known_facts.Contains("element_s"), "UI suppression lost facts");
            Require(uiAgent.Publish("MATERIAL_ANALYZED", sample.item_id, 20000, evidence: tag).reason == "ember_fact_already_explained", "UI suppressed fact replayed");
            var clone = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(ui));
            Require(new AgentController(clone, catalog).Publish("MATERIAL_ANALYZED", sample.item_id, 30000, evidence: tag).reason == "ember_fact_already_explained", "delta memory lost on reload");
            var unknown = new GameState { currentRegion = "ember_rift" };
            Require(new AgentController(unknown, catalog).Publish("MATERIAL_ANALYZED", sample.item_id, 10000, evidence: tag).reason == "knowledge_not_discovered" &&
                unknown.agent.ember.known_facts.Length == 0, "unidentified sample created facts");
        }
        public static void Fuel()
        {
            var state = new GameState { currentRegion = "ember_rift" }; var world = new WorldChemistryService(state);
            int commits = 0; world.GameplayEventPublished += e => { if (e.success && e.action_id == "use_carbon_fuel") { Require(e.heat_units == 1, "source heat metadata missing from event"); commits++; } };
            Require(!world.UseCarbonFuel("element_c").success && !world.UseCarbonFuel("material_carbon").success && commits == 0, "invalid/missing fuel succeeded");
            float energy = state.energy; state.AddItem("material_carbon"); state.AddItem("resource_carbonaceous_material");
            Require(world.UseCarbonFuel("material_carbon").success && world.UseCarbonFuel("resource_carbonaceous_material").success &&
                state.carbonFuelBurned == 2 && state.CountItem("material_carbon") == 0 && state.CountItem("resource_carbonaceous_material") == 0 && state.energy == energy && commits == 2, "fuel cost/counter/energy mismatch");
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            Require(restored.carbonFuelBurned == 2, "fuel snapshot lost");
            state.AddItem("material_carbon"); Require(world.UseCarbonFuel("material_carbon").heat_units == 1, "source heat return metadata");
            var agent = new AgentController(state, AgentCatalog.Load());
            Require(agent.Publish("WORLD_REACTION_OBSERVED", "fuel", 10000, evidence: new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "carbon_fuel_used" }).reaction_id == "ember_combustion_observed" && state.agent.ember.combustion_observed, "committed fuel observation");
        }
        public static void CapabilityAndFailures()
        {
            var catalog = AgentCatalog.Load(); var state = new GameState { currentRegion = "prologue_lab", laboratoryStage = 2 };
            var agent = new AgentController(state, catalog); long now = 10000;
            var tag = new AgentEvent { behavior_profile = "ember_rift" };
            Require(agent.Publish("DEVICE_CRAFTED", EmberThermalAgentBehavior.CondenserId, now++, evidence: tag).Silent && !state.agent.ember.condenser_available, "forged condenser");
            state.AddCraftedDevice("device_chemical_workbench"); state.AddVisitedRegion("ember_rift");
            var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
            var def = devices.Devices.First(d => d.id == EmberThermalAgentBehavior.CondenserId);
            foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n); // Manufacturing fixture.
            devices.GameplayEventPublished += e => { if (e.event_type == "device_built") agent.Publish("DEVICE_CRAFTED", e.device_id, now++, evidence: tag); };
            Require(devices.Craft(def.id).success && state.agent.ember.condenser_available && state.agent.shown.Contains("ember_condenser_available"), "real capability");
            while (agent.Queue.Current != null) agent.Queue.Advance(5, false);
            Require(agent.Publish("EXPERIMENT_FAILED", "x", now++, evidence: new AgentEvent { behavior_profile = "ember_rift", failure_reason = "missing_input" }).Silent && state.agent.ember.failure_count == 0, "unapproved failure mapped");
            for (int i = 1; i <= 7; i++)
            {
                now += 31000;
                var d = agent.Publish("EXPERIMENT_FAILED", "contract_fixture", now++, evidence: new AgentEvent { behavior_profile = "ember_rift", failure_reason = "insufficient_temperature" });
                if (i == 1) Require(d.reaction_id == "ember_processing_failure_observed", "first formal failure");
                if (i == 3 || i == 5 || i == 7) Require(d.reaction_id == "ember_failure_hint_level_" + ((i - 1) / 2), "formal failure ladder");
                while (agent.Queue.Current != null) agent.Queue.Advance(5, false);
            }
            Require(state.agent.ember.failure_count == 7 && state.agent.ember.failure_hint_level == 3, "failure state");
            string sample = catalog.ForRegion("ember_rift").sample_ids[0]; state.IdentifySample(sample);
            agent.Publish("MATERIAL_ANALYZED", sample, now++, uiExplains: true, evidence: tag);
            Require(state.agent.ember.failure_count == 0, "progress did not reset failure count");
        }
        public static void UnknownLadder()
        {
            var catalog = AgentCatalog.Load(); var state = new GameState { currentRegion = "ember_rift" };
            var agent = new AgentController(state, catalog);
            string sample = catalog.ForRegion("ember_rift").sample_ids[0];
            Require(agent.Publish("MATERIAL_COLLECTED", "not_a_profile_sample", 1, analyzable: true).Silent && state.agent.ember.unknown_material_count == 0, "unrelated sample counted");
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 1000, analyzable: true).reaction_id == "ember_unknown_material_observed", "first unknown");
            Require(!agent.Queue.Current.text.Contains("硫") && !agent.Queue.Current.text.Contains("SO"), "unknown knowledge leak");
            agent.Queue.Advance(5, false);
            agent.Publish("MATERIAL_COLLECTED", sample, 2000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 29999, analyzable: true).reason == "ember_collection_observed", "30 second gate bypass");
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 30000, analyzable: true).reaction_id == "ember_analysis_hint_level_1", "first threshold/time boundary");
            agent.Queue.Advance(5, false);
            agent.Publish("MATERIAL_COLLECTED", sample, 31000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 61000, analyzable: true).reaction_id == "ember_analysis_hint_level_2", "threshold6");
            agent.Queue.Advance(5, false);
            agent.Publish("MATERIAL_COLLECTED", sample, 62000, analyzable: true);
            agent.Publish("MATERIAL_COLLECTED", sample, 63000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 92000, analyzable: true).reaction_id == "ember_analysis_hint_level_3" && state.agent.ember.analysis_hint_level == 3, "threshold9");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u20d_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(state, out reason), "ladder snapshot " + reason); var loaded = repo.Read();
            Require(loaded.success && loaded.state.agent.ember.unknown_material_count == 9 && loaded.state.agent.ember.analysis_hint_level == 3, "ladder snapshot mismatch");
            loaded.state.agent.ember.analysis_hint_level = 4; Require(!repo.Write(loaded.state, out reason), "invalid hint level committed");
            state.IdentifySample(sample);
            agent.Publish("MATERIAL_ANALYZED", sample, 93000, uiExplains: true, evidence: new AgentEvent { behavior_profile = "ember_rift" });
            Require(state.agent.ember.unknown_material_count == 0 && state.agent.ember.last_progress_ms == 93000, "confirmed progress not reset under UI suppression");
            agent.Publish("MATERIAL_COLLECTED", sample, 94000, analyzable: true);
            Require(state.agent.ember.unknown_material_count == 0, "known sample counted as unknown");
            var other = catalog.ForRegion("ember_rift").sample_ids[1];
            agent.Publish("MATERIAL_COLLECTED", other, 95000, uiExplains: true, analyzable: true);
            Require(state.agent.ember.unknown_material_count == 1, "UI suppression discarded attempt");
            agent.Publish("MATERIAL_COLLECTED", other, 96000, analyzable: true, evidence: new AgentEvent { behavior_profile = "salt_marsh" });
            Require(state.agent.ember.unknown_material_count == 1, "other profile event counted");
        }
        public static void AttentionAndEntry()
        {
            var catalog = AgentCatalog.Load(); var state = new GameState { currentRegion = "ember_rift" };
            var env = new AgentEnvironmentFacts { region = "ember_rift", ready = false, has_temperature = true, temperature = catalog.ember_authored_temperature };
            var agent = new AgentController(state, catalog, () => env);
            Require(agent.Publish("REGION_ENTERED", "ember_rift", 1000).reason == "environment_state_unavailable" && !state.agent.ember.temperature_observed, "unready entry changed attention");
            env.ready = true; env.temperature = float.NaN;
            Require(agent.Publish("REGION_ENTERED", "ember_rift", 2000).Silent, "nonfinite entry");
            env.temperature = 0.84f; Require(agent.Publish("REGION_ENTERED", "ember_rift", 3000).Silent, "insufficient temperature");
            env.temperature = catalog.ember_authored_temperature;
            Require(agent.Publish("REGION_ENTERED", "ember_rift", 4000).reaction_id == "ember_rift_first_entry" && state.agent.ember.temperature_observed, "confirmed heat entry");
            // Explicit committed-result snapshot fixtures; real processing/detection is tested separately in U20A/B.
            string sample = catalog.analyses.First(a => a.region_id == "ember_rift").item_id;
            state.IdentifySample(sample);
            var tag = new AgentEvent { behavior_profile = "ember_rift" };
            agent.Publish("MATERIAL_ANALYZED", sample, 5000, uiExplains: true, evidence: tag);
            Require(state.agent.ember.analysis_completed && state.agent.ember.known_facts.Length > 0 && !state.agent.ember.mature, "UI discarded confirmed analysis / premature maturity");
            state.AddChemistryKnowledge("thermal:resource_carbonate_rock"); state.AddItem("compound_cao"); state.AddItem("compound_co2");
            agent.Publish("WORLD_REACTION_OBSERVED", "thermal", 6000, outputs: new[] { "compound_cao", "compound_co2" }, evidence:
                new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "thermal_carbonate_processed" });
            Require(state.agent.ember.temperature_knowledge && state.agent.ember.thermal_completed && !state.agent.ember.mature, "thermal maturity gate");
            state.AddChemistryKnowledge("condenser_used"); state.AddItem("compound_ethanol");
            Require(agent.Publish("WORLD_REACTION_OBSERVED", "condense", 7000, outputs: new[] { "compound_ethanol" }, evidence:
                new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "vapor_condensed" }).reaction_id == "ember_condensation_completed" && state.agent.ember.mature, "mature final event lost");
            Require(agent.Publish("REGION_ENTERED", "ember_rift", 8000).reason == "ember_guidance_mature", "mature replay");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u20c_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(state, out reason), "snapshot write " + reason); var loaded = repo.Read();
            Require(loaded.success && JsonUtility.ToJson(loaded.state.agent.ember) == JsonUtility.ToJson(state.agent.ember), "attention disk mismatch");
            loaded.state.agent.ember.condensation_used = false;
            Require(!repo.Write(loaded.state, out reason) && repo.Read().success, "illegal maturity accepted");
            var old = new GameState(); old.agent.ember = null; new AgentController(old, catalog);
            Require(old.agent.ember != null && !old.agent.ember.mature, "old state default");
        }
        public static void Analysis()
        {
            var catalog = AgentCatalog.Load();
            var samples = catalog.analyses.Where(a => a.region_id == "ember_rift").ToArray();
            Require(samples.Length == 4 && !samples.Any(a => a.facts.Contains("ion_so4")), "source sample identity / invented sulfate");
            foreach (var sample in samples)
            {
                var state = new GameState { currentRegion = "ember_rift", laboratoryStage = 2 };
                var agent = new AgentController(state, catalog);
                var evidence = new AgentEvent { behavior_profile = "ember_rift", analyzer_used = true };
                Require(agent.Publish("MATERIAL_ANALYZED", sample.item_id, 10000, evidence: evidence).reason == "knowledge_not_discovered", "forged analysis");
                // Explicit manufacturing/feedstock fixtures, not a player acquisition route.
                state.AddCraftedDevice("device_chemical_workbench"); state.AddVisitedRegion("crystal_tundra");
                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                var def = devices.Devices.First(d => d.id == CrystalTundraAgentBehavior.DeviceId);
                foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                Require(devices.Craft(def.id).success && devices.Place(def.id, "analyzer").success, "analyzer fixture");
                state.AddItem(sample.item_id); AgentDecision decision = null;
                devices.GameplayEventPublished += e => {
                    if (e.event_type == "device_processing_started") decision = agent.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 15000, evidence: evidence);
                };
                Require(devices.Start("analyzer", "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample.item_id } }).success, "actual detection");
                Require(decision != null && !decision.Silent && state.CountItem(sample.item_id) == 1, "analysis not routed / consumed sample");
                var dialogue = agent.Queue.Current;
                Require(dialogue != null && !dialogue.text.Contains("硫酸盐"), "invented sulfate identity");
                Require(sample.facts.Contains("element_s") ? decision.reaction_id == "ember_sulfur_confirmed" : !dialogue.text.Contains("硫"), "exact sulfur guard");
                string before = JsonUtility.ToJson(state);
                var policy = new AgentDecisionPolicy(catalog);
                policy.Decide(new AgentEvent { type = "MATERIAL_ANALYZED", subject = sample.item_id, region = "ember_rift", behavior_profile = "ember_rift", time_ms = 16000 },
                    new AgentContext(state, catalog), state.agent, 0);
                Require(JsonUtility.ToJson(state) == before, "policy mutated gameplay/state input");
                Require(agent.Publish("MATERIAL_ANALYZED", sample.item_id, 20000, evidence: evidence).reason == "ember_fact_already_explained", "repeat scan spam");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                Require(new AgentController(restored, catalog).Publish("MATERIAL_ANALYZED", sample.item_id, 25000, evidence: evidence).reason == "ember_fact_already_explained", "snapshot replay");
            }
        }
        public static void Processing()
        {
            var state = new GameState { currentRegion = "ember_rift", laboratoryStage = 2 };
            var catalog = AgentCatalog.Load(); var agent = new AgentController(state, catalog);
            var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
            long now = 10000; int delivered = 0;
            devices.GameplayEventPublished += e => {
                if (e.event_type != "device_output_collected") return;
                string phenomenon = e.recipe_id == "recipe_phase3a_thermal_carbonate" ? "thermal_carbonate_processed" :
                    e.recipe_id == "recipe_phase3a_condense_vapor" ? "vapor_condensed" : "";
                if (phenomenon == "") return;
                var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); copy.agent = null;
                string before = JsonUtility.ToJson(copy);
                var decision = agent.Publish("WORLD_REACTION_OBSERVED", e.recipe_id, now++, outputs: e.items.Select(q => q.id).ToArray(),
                    evidence: new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = phenomenon });
                copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); copy.agent = null;
                Require(before == JsonUtility.ToJson(copy) && !decision.Silent, "delivered processing / gameplay guard"); delivered++;
            };
            Require(agent.Publish("WORLD_REACTION_OBSERVED", "fake", now++, evidence: new AgentEvent {
                behavior_profile = "ember_rift", observed = true, phenomenon = "thermal_carbonate_processed" }).Silent, "forged thermal result");
            // Explicit manufacturing and feedstock fixtures, not a player route.
            state.AddCraftedDevice("device_chemical_workbench");
            foreach (string recipeId in new[] { "recipe_phase3a_thermal_carbonate", "recipe_phase3a_condense_vapor" })
            {
                var recipe = GameData.ProcessingRecipes.recipes.First(r => r.id == recipeId);
                var def = devices.Devices.First(d => d.id == recipe.device_id);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                Require(devices.Craft(def.id).success && devices.Place(def.id, def.id).success, "craft fixture");
                if (recipeId == "recipe_phase3a_thermal_carbonate") state.AddItem("resource_carbonate_rock");
                else state.ChangeVapor("compound_ethanol", 1);
                var conditions = (recipe.required_conditions ?? new ConditionValue[0]).ToDictionary(c => c.id, c => c.value);
                int previous = delivered;
                Require(devices.Start(def.id, recipeId, conditions).success && delivered == previous, "start counted as delivered");
                devices.Advance(10); Require(delivered == previous && devices.Collect(def.id).success && delivered == previous + 1, "collection bridge");
            }
            Require(state.agent.shown.Contains("ember_thermal_decomposition_confirmed") && state.agent.shown.Contains("ember_condensation_completed"), "missing source responses");
            var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            var loaded = new AgentController(restored, catalog);
            Require(loaded.Publish("WORLD_REACTION_OBSERVED", "again", now++, outputs: new[] { "compound_ethanol" }, evidence:
                new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "vapor_condensed" }).reason == "already_shown", "snapshot replay");
            state.agent.salt.separation_used = true; // Recall fixture only, no false claim of a full salt route.
            Require(agent.Publish("WORLD_REACTION_OBSERVED", "linked", now++, outputs: new[] { "compound_cao", "compound_co2" }, evidence:
                new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "thermal_carbonate_processed" }).reaction_id == "ember_salt_heat_link", "confirmed recall link");
        }
    }
}
