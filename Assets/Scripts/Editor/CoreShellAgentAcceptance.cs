using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class CoreShellAgentAcceptance
    {
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static GameState State() { var s = new GameState { currentRegion = "core_shell", laboratoryStage = 3 }; s.AddVisitedRegion(s.currentRegion); return s; }
        static void Craft(DeviceRuntimeService d, GameState s, string id)
        {
            var def = d.Devices.First(x => x.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
            foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
            Require(d.Craft(id).success && d.Place(id, id).success, "actual manufacture " + id);
        }
        static CoreShellEventFacts Formal() { return new CoreShellEventFacts { has_pressure = true, has_temperature_condition = true, has_catalyst_consumed = true,
            catalyst_id = "material_silver_catalytic", device_id = "device_high_pressure_catalytic_reactor", temperature_condition = "HIGH" }; }
        static AgentEvent Success(CoreShellEventFacts facts) { return new AgentEvent { phenomenon = "high_pressure_catalytic_conversion_completed", behavior_profile = "core_shell", core = facts }; }
        public static void EntryAndDetection()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("core_shell");
            Require(p.sample_ids.Length == 4 && p.formal_catalyst == "material_silver_catalytic" && cat.core_authored_pressure == .88f, "source profile contracts");
            foreach (float pressure in new[] { 0f, -.1f, .88f, float.NaN, float.PositiveInfinity })
            {
                var s = State(); var env = new AgentEnvironmentFacts { region = s.currentRegion, ready = true, has_pressure = true, pressure = pressure };
                var c = new AgentContext(s, cat, env); env.pressure = 1f; string before = JsonUtility.ToJson(s);
                var d = new AgentDecisionPolicy(cat).Decide(new AgentEvent { type = "REGION_ENTERED", subject = s.currentRegion, region = s.currentRegion, time_ms = 10000 }, c, s.agent, 0);
                bool present = !float.IsNaN(pressure) && !float.IsInfinity(pressure) && pressure > 0;
                Require(d.action == (present ? "WARN" : "FOLLOW_UP") && d.core.environment_pressure == (present ? "PRESENT" : "") && !d.core.device_condition_satisfied && before == JsonUtility.ToJson(s), "pressure isolation/finite/no device implication");
                Require(!d.next_state.core_shell.pressure_observed, "entry invented process pressure evidence");
            }
            var explicitState = State();
            var explicitEvent = new AgentEvent { type = "REGION_ENTERED", subject = "core_shell", region = "core_shell", time_ms = 10000,
                core = new CoreShellEventFacts { pressure_facts = new[] { new CorePressureFact { field = "pressure_hazard", flag = true }, new CorePressureFact { field = "pressure_state", text = "SOURCE_PRESSURE" } } } };
            Require(new AgentDecisionPolicy(cat).Decide(explicitEvent, new AgentContext(explicitState, cat), explicitState.agent, 0).core.environment_pressure == "SOURCE_PRESSURE", "pressure source field priority");
            foreach (var row in cat.analyses.Where(r => r.region_id == "core_shell"))
            {
                var s = State(); s.AddItem(row.item_id); var a = new AgentController(s, cat); var d = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                Require(a.Publish("MATERIAL_COLLECTED", row.item_id, 10000).reaction_id == p.Reaction("unknown_material"), "unknown material guard");
                s.IdentifySample(row.item_id);
                Require(a.Publish("MATERIAL_ANALYZED", row.item_id, 20000).reason == "core_analysis_not_authoritative", "identity-only inferred core composition");
                Craft(d, s, "device_chemical_workbench"); Craft(d, s, CrystalTundraAgentBehavior.DeviceId);
                d.GameplayEventPublished += e => { if (e.event_type == "device_processing_started") a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 40000); };
                Require(d.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", row.item_id } }).success && s.CountItem(row.item_id) == 1, "real detector consumed sample");
                Require(a.Queue.Current != null && a.Queue.Current.reaction_id == p.Reaction("analysis") && !s.agent.core_shell.catalysis_observed && !s.agent.core_shell.multi_step_observed, "analysis inflated process progress");
                var context = new AgentContext(s, cat); Require(context.confirmed_analyses.Single(r => r.item_id == row.item_id).facts.SequenceEqual(row.facts), "detector source facts");
            }
        }
        public static void ContractsAndPersistence()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("core_shell"); var s = State(); var a = new AgentController(s, cat); long time = 10000;
            Func<string,AgentEvent,AgentDecision> publish = (type, evidence) => {
                while (a.Queue.Current != null) a.Queue.Advance(100, false);
                return a.Publish(type, "contract_fixture", time += 31000, evidence: evidence);
            };
            var needs = new AgentEvent { phenomenon = "advanced_reaction_need_identified", core = new CoreShellEventFacts { pressure_required = true } };
            s.laboratoryStage = 2; Require(publish("WORLD_REACTION_OBSERVED", needs).reason == "device12_progression_not_available", "device hint bypassed stage");
            s.laboratoryStage = 3; s.visitedRegions = new string[0]; Require(publish("WORLD_REACTION_OBSERVED", needs).Silent, "device hint bypassed visited region");
            s.AddVisitedRegion("core_shell"); Require(publish("WORLD_REACTION_OBSERVED", needs).reaction_id == p.Reaction("device12") && !s.agent.core_shell.device12_observed && !s.HasCraftedDevice("device_high_pressure_catalytic_reactor"), "hint auto-unlocked device");
            for (int i = 1; i <= 6; i++)
            {
                var result = publish("EXPERIMENT_FAILED", new AgentEvent { behavior_profile = "core_shell", failure_reason = "high_pressure_required" });
                string key = i == 1 ? "failure" : i < 4 ? "failure_1" : i < 6 ? "failure_2" : "failure_3";
                if (i == 1 || i == 2 || i == 4 || i == 6) Require(result.reaction_id == p.Reaction(key), "failure ladder " + i);
            }
            publish("EXPERIMENT_FAILED", new AgentEvent { failure_reason = "high_pressure_required" });
            publish("EXPERIMENT_FAILED", new AgentEvent { behavior_profile = "core_shell", failure_reason = "unrecognized" });
            Require(s.agent.core_shell.failure_count == 6, "untagged/unsupported failure counted");
            var f = Formal(); var missing = Formal(); missing.has_temperature_condition = false;
            a.Publish("WORLD_REACTION_OBSERVED", "missing_temperature_fixture", time += 31000, uiExplains: true, evidence: Success(missing)); Require(!s.agent.core_shell.multi_condition_observed && !s.agent.core_shell.mature, "missing temperature invented matrix");
            s.AddVisitedRegion("mantle_flows"); s.AddChemistryKnowledge("extreme_heat:element_s");
            var recall = publish("WORLD_REACTION_OBSERVED", Success(f)); Require(recall.reaction_id == p.Reaction("mantle_recall") && !recall.core.environment_pressure_satisfies_device, "mantle recall/matrix separation");
            Require(publish("WORLD_REACTION_OBSERVED", Success(f)).reaction_id == p.Reaction("multi_condition"), "matrix priority");
            Require(publish("WORLD_REACTION_OBSERVED", Success(f)).reaction_id == p.Reaction("catalyst"), "catalyst priority");
            Require(publish("WORLD_REACTION_OBSERVED", Success(f)).reaction_id == p.Reaction("completed") && !s.agent.core_shell.mature, "success without multi-step matured");
            var step = publish("WORLD_REACTION_OBSERVED", new AgentEvent { core = new CoreShellEventFacts { has_reaction_step_completed = true } });
            Require(step.reaction_id == p.Reaction("multi_step") && s.agent.core_shell.mature && !step.core.identity_confirmed && !step.core.full_chain_disclosed, "presence-only multi-step contract");
            // Godot maturity remains sticky even after a later success replaces the current flags.
            Require(publish("WORLD_REACTION_OBSERVED", Success(new CoreShellEventFacts())).reason == "core_guidance_mature" && s.agent.core_shell.mature && !s.agent.core_shell.multi_condition_observed, "sticky mature/source replacement semantics");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u28_contract_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(s, out error), "core save " + error); var read = repo.Read(); Require(read.success && read.state.agent.core_shell.mature && read.state.agent.core_shell.failure_count == 6, "core restart persistence");
            Require(new AgentController(read.state, cat).Publish("REGION_ENTERED", "core_shell", time + 31000).reason == "core_guidance_mature", "mature restart replay");
            s.agent.core_shell.failure_count = -1; Require(!repo.Write(s, out error), "negative failure counter accepted");
            var ui = State(); var ctrl = new AgentController(ui, cat); ctrl.Publish("WORLD_REACTION_OBSERVED", "fixture", 10000, uiExplains: true, evidence: Success(Formal()));
            Require(ui.agent.core_shell.multi_condition_observed && ui.agent.shown.Length == 0, "UI suppression lost attention");
            var iron = Formal(); iron.catalyst_id = "material_iron"; iron.has_catalyst_consumed = false;
            ctrl.Publish("WORLD_REACTION_OBSERVED", "fixture", 41000, evidence: Success(iron)); Require(!ui.agent.core_shell.multi_condition_observed && !ui.agent.core_shell.catalysis_observed, "iron promoted to silver");
            iron.has_catalyst_consumed = true; ctrl.Publish("WORLD_REACTION_OBSERVED", "fixture", 72000, evidence: Success(iron)); Require(ui.agent.core_shell.multi_condition_observed && !ui.agent.core_shell.catalysis_observed, "present false consumption discarded");
            var cross = State(); cross.agent.mantle = new MantleAttention { mature = true, heat_observed = true, sulfur_observed = true, thermal_processed = true, industrial_chain_observed = true, crystallization_observed = true };
            Require(new AgentController(cross, cat).Publish("REGION_ENTERED", "mantle_flows", 10000, evidence: new AgentEvent { behavior_profile = "mantle_flows" }).reason == "mantle_guidance_mature" && !cross.agent.core_shell.pressure_observed,
                "current Core Shell swallowed explicitly owned prior-region event");
        }
        public static void ActualPressureRoutes()
        {
            foreach (bool conversion in new[] { false, true })
            {
                var s = State(); var cat = AgentCatalog.Load(); var a = new AgentController(s, cat); var d = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                Craft(d, s, "device_chemical_workbench"); Craft(d, s, "device_high_pressure_catalytic_reactor");
                long now = 10000;
                d.GameplayEventPublished += e => { var evidence = CoreShellAgentBehavior.DeviceDelivery(e, GameData.ProcessingRecipes); if (evidence != null) a.Publish("WORLD_REACTION_OBSERVED", e.recipe_id, now += 31000, evidence: evidence); };
                string recipe = conversion ? "recipe_phase4b_device12_co2_h2_conversion" : "recipe_phase4b_high_pressure_nh3";
                string input = conversion ? "compound_co2" : "compound_n2", catalyst = conversion ? "material_silver_catalytic" : "material_iron";
                s.AddItem(input); s.AddItem("compound_h2", conversion ? 1 : 3); s.AddItem(catalyst);
                Require(!d.Start("device_high_pressure_catalytic_reactor", recipe).success && s.CountItem(input) == 1 && !s.agent.core_shell.pressure_observed, "missing conditions consumed input/published success");
                var conditions = new Dictionary<string,string> { { "pressure", "PRESSURE_HIGH" }, { "temperature_condition", "HIGH" }, { "internal_process", "true" }, { "catalyst", "material_iron" } };
                Require(d.Start("device_high_pressure_catalytic_reactor", recipe, conditions).success && !s.agent.core_shell.pressure_observed, "processing start counted as delivery");
                d.Advance(10); Require(!s.agent.core_shell.pressure_observed, "buffer counted as delivery");
                Require(d.Collect("device_high_pressure_catalytic_reactor").success && s.CountItem(catalyst) == 1 && s.CountItem(conversion ? "compound_reduced_carbon_liquid" : "compound_nh3") == (conversion ? 1 : 2), "actual pressure output/catalyst conservation");
                var att = s.agent.core_shell;
                Require(att.pressure_observed && att.catalysis_observed == conversion && att.multi_condition_observed == conversion && att.device12_observed == conversion && !att.multi_step_observed && !att.mature, "real route invented temperature/silver/multi-step");
            }
        }
    }
}
