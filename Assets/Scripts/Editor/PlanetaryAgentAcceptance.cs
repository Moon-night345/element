using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;
namespace ElementalStarSea.EditorTools
{
    public static class PlanetaryAgentAcceptance
    {
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
        static GameState State() { var s = new GameState { currentRegion = "planetary_core", laboratoryStage = 3 }; s.AddVisitedRegion(s.currentRegion); return s; }
        static AgentEvent Process(PlanetaryEventFacts facts, string phenomenon = "final_process_step_observed")
        { return new AgentEvent { phenomenon = phenomenon, behavior_profile = "planetary_core", planetary = facts }; }
        static void Drain(AgentController a) { while (a.Queue.Current != null) a.Queue.Advance(100, false); }
        static void Craft(DeviceRuntimeService d, GameState s, string id)
        {
            var def = d.Devices.First(v => v.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
            foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
            Require(d.Craft(id).success && d.Place(id, id).success, "actual manufacture " + id);
        }
        public static void EntryAndDetection()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("planetary_core");
            Require(p.sample_ids.Length == 4 && p.condition_fields.Length == 8 && cat.planetary_recall_groups.Length == 11 && GameData.Devices.devices.Any(d => d.id == PlanetaryCoreAgentBehavior.ChargerId), "source catalogs");
            foreach (bool hazard in new[] { false, true })
            {
                var s = State(); var env = new AgentEnvironmentFacts { ready = true, region = s.currentRegion, hazard = hazard };
                string before = JsonUtility.ToJson(s); var c = new AgentContext(s, cat, env); env.hazard = !hazard;
                var result = new AgentDecisionPolicy(cat).Decide(new AgentEvent { type = "REGION_ENTERED", subject = s.currentRegion, region = s.currentRegion, time_ms = 10000 }, c, s.agent, 0);
                Require(result.action == (hazard ? "WARN" : "FOLLOW_UP") && before == JsonUtility.ToJson(s) && !result.next_state.planetary.condition_selected, "hazard source snapshot/autonomy");
                var explicitEvent = new AgentEvent { type = "REGION_ENTERED", subject = s.currentRegion, region = s.currentRegion, time_ms = 10000,
                    planetary = new PlanetaryEventFacts { confirmed_hazard_fields = new[] { hazard ? "pressure_hazard" : "invented_hazard" } } };
                Require(new AgentDecisionPolicy(cat).Decide(explicitEvent, new AgentContext(s, cat), s.agent, 0).action == (hazard ? "WARN" : "FOLLOW_UP"), "hazard whitelist");
            }
            foreach (var row in cat.analyses.Where(r => r.region_id == "planetary_core"))
            {
                var s = State(); s.AddItem(row.item_id); var a = new AgentController(s, cat); var d = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                Require(a.Publish("MATERIAL_COLLECTED", row.item_id, 10000).reason == "unknown_identity_requires_analysis" && s.agent.shown.Length == 0, "unknown disclosed identity");
                s.IdentifySample(row.item_id); Require(a.Publish("MATERIAL_ANALYZED", row.item_id, 20000).Silent && !s.agent.planetary.analysis_confirmed, "identity-only inferred analysis");
                Craft(d, s, "device_chemical_workbench"); Craft(d, s, CrystalTundraAgentBehavior.DeviceId);
                d.GameplayEventPublished += e => { if (e.event_type == "device_processing_started") a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 40000); };
                Require(d.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", row.item_id } }).success && s.CountItem(row.item_id) == 1, "actual detection conservation");
                Require(s.agent.planetary.analysis_confirmed && !s.agent.planetary.condition_selected && !s.agent.planetary.mature && a.Queue.Current != null && a.Queue.Current.reaction_id == p.Reaction("analysis"), "analysis inflated process progress");
                Require(new AgentContext(s, cat).confirmed_analyses.Single(r => r.item_id == row.item_id).facts.SequenceEqual(row.facts), "confirmed source composition");
            }
        }
        public static void FailuresStorageAndEnergy()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("planetary_core"); var s = State(); var a = new AgentController(s, cat); long time = 10000;
            Func<string,AgentEvent,AgentDecision> publish = (type, ev) => { Drain(a); return a.Publish(type, "contract_fixture", time += 51000, evidence: ev); };
            var need = Process(new PlanetaryEventFacts(), "bulk_liquid_storage_needed");
            var result = publish("WORLD_REACTION_OBSERVED", need);
            Require(result.reaction_id == p.Reaction("storage_need") && result.planetary.semantic_role == "STORAGE_BUFFERING" && !result.planetary.is_reaction_device && !result.planetary.auto_unlock && !s.HasCraftedDevice(PlanetaryCoreAgentBehavior.TankId), "storage hint fabricated reaction/unlock");
            s.AddVisitedRegion("core_shell");
            for (int i = 1; i <= 4; i++)
            {
                result = publish("EXPERIMENT_FAILED", new AgentEvent { behavior_profile = "planetary_core", failure_reason = "missing_pressure" });
                if (i == 1) Require(result.reason == "first_failure_observe_only", "first failure disclosed hint");
                else Require(result.reaction_id == p.Reaction(i == 2 ? "failure_observe" : i == 3 ? "failure_condition" : "failure_device"), "failure ladder " + i);
                if (i == 4) Require(result.planetary.capability_category == "PRESSURE" && result.planetary.recall_region_id == "core_shell" && !result.planetary.full_solution_disclosed, "failure single relevant recall");
            }
            publish("EXPERIMENT_FAILED", new AgentEvent { failure_reason = "missing_pressure" }); publish("EXPERIMENT_FAILED", new AgentEvent { behavior_profile = "planetary_core", failure_reason = "invalid_reason" });
            Require(s.agent.planetary.failure_count == 4, "unsupported failure counted");
            s.laboratoryStage = 2; Require(publish("WORLD_REACTION_OBSERVED", need).Silent, "repeat storage observation unexpectedly bypassed once budget");
            s.laboratoryStage = 3; result = publish("WORLD_REACTION_OBSERVED", need);
            Require(result.reaction_id == p.Reaction("liquid_tank") && result.planetary.device_id == PlanetaryCoreAgentBehavior.TankId && !s.HasCraftedDevice(PlanetaryCoreAgentBehavior.TankId), "liquid tank escalation/unlock");
            var energy = Process(new PlanetaryEventFacts { energy_shortage = true });
            Require(publish("WORLD_REACTION_OBSERVED", energy).reason == "charger_not_available", "invented charger ownership");
            var d = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes); Craft(d, s, "device_chemical_workbench"); Craft(d, s, PlanetaryCoreAgentBehavior.ChargerId);
            result = publish("WORLD_REACTION_OBSERVED", energy); Require(result.reaction_id == p.Reaction("charger") && result.planetary.semantic_role == "ENERGY_RESTORE", "actual charger reuse");
            // Failure exception remains usable after maturity; current scope is an explicit contract fixture.
            s.agent.planetary.analysis_confirmed = s.agent.planetary.condition_selected = s.agent.planetary.category_selected = s.agent.planetary.device_used = s.agent.planetary.mature = true;
            publish("EXPERIMENT_FAILED", new AgentEvent { behavior_profile = "planetary_core", failure_reason = "wrong_sequence" }); Require(s.agent.planetary.failure_count == 5, "mature ignored supported failure");
        }
        public static void RecallMatureAndDisk()
        {
            var cat = AgentCatalog.Load(); var p = cat.ForRegion("planetary_core");
            foreach (var group in cat.planetary_recall_groups)
            {
                var s = State(); foreach (string r in group.values.Reverse()) s.AddVisitedRegion(r);
                var a = new AgentController(s, cat); var f = new PlanetaryEventFacts { categories = new[] { new AgentPair { key = "process_category", value = group.key.ToLowerInvariant() } } };
                var result = a.Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 10000, evidence: Process(f));
                Require(result.reaction_id == p.Reaction("recall") && result.planetary.recall_region_id == group.values[0] && result.planetary.current_problem_category == group.key && !result.planetary.full_solution_disclosed, "recall source priority " + group.key);
                Drain(a); result = a.Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 61000, evidence: Process(f)); Require(result.reaction_id == p.Reaction("category") && result.planetary.recall_region_id.Length == 0, "recall repeated/full route disclosed");
            }
            var state = State(); var controller = new AgentController(state, cat);
            var fields = new PlanetaryEventFacts { device_id = PlanetaryCoreAgentBehavior.TankId, ordinary_success = true, condition_fields = new[] { "phase" }, categories = new[] { new AgentPair { key = "process_category", value = "thermal" }, new AgentPair { key = "next_problem_category", value = "pressure" } } };
            var result0 = controller.Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 10000, evidence: Process(fields));
            Require(result0.reason == "ordinary_success_restraint" && state.agent.planetary.condition_selected && state.agent.planetary.category_selected && state.agent.planetary.device_used && !state.agent.planetary.mature, "ordinary success lost attention/invented analysis");
            var sample = cat.analyses.First(r => r.region_id == "planetary_core").item_id; state.IdentifySample(sample); state.AddChemistryKnowledge("ph_discovery:" + sample);
            // Confirmed analysis fixture here; actual manufacturing/detection is checked separately.
            controller.Publish("MATERIAL_ANALYZED", sample, 61000, uiExplains: true); Require(state.agent.planetary.mature && state.agent.shown.Length == 0, "four-gate mature/UI attention");
            Require(controller.Publish("REGION_ENTERED", "planetary_core", 112000, evidence: new AgentEvent { danger = true }).reason == "final_guidance_mature", "mature entry differs from source restraint");
            Require(controller.Publish("MATERIAL_COLLECTED", p.sample_ids[1], 163000).reason == "unknown_identity_requires_analysis", "mature unknown exception");
            var breakthrough = controller.Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 214000, evidence: Process(new PlanetaryEventFacts { multi_system_breakthrough = true }));
            Require(breakthrough.reaction_id == p.Reaction("breakthrough") && breakthrough.planetary.player_owns_solution && !state.coreTestPassed, "guidance breakthrough auto-completed game");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u29_contract_" + Guid.NewGuid().ToString("N"), "save.json")), error; var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(state, out error), "planetary save " + error); var read = repo.Read(); Require(read.success && read.state.agent.planetary.mature && read.state.agent.shown.Contains(p.Reaction("breakthrough")), "planetary disk persistence");
            Require(new AgentController(read.state, cat).Publish("REGION_ENTERED", "planetary_core", 265000).reason == "final_guidance_mature", "restart guidance replay");
            state.agent.planetary.condition_selected = false; Require(!repo.Write(state, out error), "invalid mature saved");
            var precedence = State(); var pc = new AgentController(precedence, cat); fields.ordinary_success = false; fields.device_id = ""; fields.condition_fields = new[] { "invented_condition" };
            var pd = pc.Publish("WORLD_REACTION_OBSERVED", "fixture", 10000, evidence: Process(fields)); Require(pd.planetary.current_problem_category == "PRESSURE" && !precedence.agent.planetary.condition_selected, "category field priority/unrecognized condition");
        }
        public static void ActualStorage()
        {
            var s = State(); var cat = AgentCatalog.Load(); var a = new AgentController(s, cat); var d = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes); long time = 10000;
            Craft(d, s, "device_chemical_workbench"); Craft(d, s, PlanetaryCoreAgentBehavior.TankId);
            d.GameplayEventPublished += e => { var facts = PlanetaryCoreAgentBehavior.StorageEvent(e); if (facts != null) a.Publish("WORLD_REACTION_OBSERVED", e.event_type, time += 51000, evidence: facts); };
            const string liquid = "compound_reduced_carbon_liquid"; s.AddItem(liquid, 2); int before = s.CountItem(liquid);
            Require(!d.Store(PlanetaryCoreAgentBehavior.TankId, "material_iron").success && !s.agent.planetary.device_used, "non-liquid produced storage observation");
            Require(d.Store(PlanetaryCoreAgentBehavior.TankId, liquid).success && s.CountItem(liquid) == before - 1 && s.agent.planetary.device_used && s.agent.planetary.category_selected && !s.agent.planetary.condition_selected && !s.agent.planetary.mature && a.Queue.Current == null, "actual store conservation/quiet/category");
            Require(!d.Store(PlanetaryCoreAgentBehavior.TankId, "compound_h2o").success && s.CountItem(liquid) == before - 1, "incompatible storage mutated liquid");
            Require(d.Withdraw(PlanetaryCoreAgentBehavior.TankId).success && s.CountItem(liquid) == before && s.DeviceInstance(PlanetaryCoreAgentBehavior.TankId).stored_amount == 0 && a.Queue.Current == null && s.HasChemistryKnowledge("bulk_liquid_reused"), "actual withdraw conservation/quiet");
            Require(!d.Withdraw(PlanetaryCoreAgentBehavior.TankId).success && s.CountItem(liquid) == before && !s.coreTestPassed, "empty withdraw duplicated/finished game");
            var cross = new GameState { currentRegion = "core_shell" }; var ctrl = new AgentController(cross, cat);
            ctrl.Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 10000, evidence: Process(new PlanetaryEventFacts { ordinary_success = true, device_id = PlanetaryCoreAgentBehavior.TankId, categories = new[] { new AgentPair { key = "process_category", value = "LIQUID_STORAGE_BUFFERING" } } }));
            Require(cross.agent.planetary.device_used && !cross.agent.core_shell.pressure_observed, "cross-region producer ownership");
        }
    }
}
