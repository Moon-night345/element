using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class MagneticEntryAcceptance
    {
        public static void Electrolysis()
        {
            var cat = AgentCatalog.Load(); var s = new GameState { currentRegion = "magnetic_plateau", laboratoryStage = 2 }; s.AddVisitedRegion(s.currentRegion);
            var a = new AgentController(s, cat); var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
            foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId, "device_industrial_electrolysis_cell" })
            {
                var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("actual electrolysis manufacture");
            }
            long now = 10000;
            devices.GameplayEventPublished += e => {
                now += 31000;
                if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect") a.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, now);
                if (e.event_type == "device_output_collected" && (e.recipe_id == "recipe_phase3b_electrolyze" || e.recipe_id == "recipe_phase4b_silver_catalytic_refine")) a.Publish("WORLD_REACTION_OBSERVED", e.recipe_id, now,
                    outputs: e.items.Where(q => q.n > 0).Select(q => q.id).ToArray(), evidence: new AgentEvent { behavior_profile = s.currentRegion, observed = true,
                        phenomenon = e.recipe_id == "recipe_phase3b_electrolyze" ? "electrolysis_completed" : "silver_catalytic_material_refined" });
            };
            string item = cat.analyses.First(x => x.region_id == s.currentRegion).item_id; s.AddItem(item);
            if (!devices.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string,string> { { "sample_id", item } }).success) throw new InvalidOperationException("analysis prerequisite");
            devices.Advance(10); devices.Collect(CrystalTundraAgentBehavior.DeviceId);
            if (a.Publish("WORLD_REACTION_OBSERVED", "fixture", now + 1000, evidence: new AgentEvent { observed = false, phenomenon = "electrolysis_completed" }).reaction_id == "magnetic_electrolysis_completed" || s.agent.magnetic.electrolysis_observed) throw new InvalidOperationException("unconfirmed process accepted");
            s.AddItem("compound_h2o");
            if (!devices.Start("device_industrial_electrolysis_cell", "recipe_phase3b_electrolyze", null).success || s.agent.magnetic.electrolysis_observed) throw new InvalidOperationException("premature electrolysis observation");
            devices.Advance(10); if (s.agent.magnetic.mature) throw new InvalidOperationException("buffered output counted");
            if (!devices.Collect("device_industrial_electrolysis_cell").success || s.CountItem("compound_h2") != 1 || s.CountItem("compound_o2") != 1 || !s.agent.magnetic.mature)
                throw new InvalidOperationException("real delivered electrolysis/maturity");
            if (a.Publish("REGION_ENTERED", s.currentRegion, now + 31000).reason != "magnetic_guidance_mature") throw new InvalidOperationException("mature entry replay");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u25_process_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            if (!repo.Write(s, out error) || !repo.Read().state.agent.magnetic.mature) throw new InvalidOperationException("process disk state " + error);
            var loaded = repo.Read(); loaded.state.agent.magnetic.electrolysis_observed = false; if (repo.Write(loaded.state, out error)) throw new InvalidOperationException("invalid maturity accepted");
            s.laboratoryStage = 3; s.AddItem("compound_preprocessed_silver_bearing_material");
            if (devices.Start("device_industrial_electrolysis_cell", "recipe_phase4b_silver_catalytic_refine", null).success || s.CountItem("compound_preprocessed_silver_bearing_material") != 1) throw new InvalidOperationException("silver controlled-current gate");
            if (!devices.Start("device_industrial_electrolysis_cell", "recipe_phase4b_silver_catalytic_refine", new System.Collections.Generic.Dictionary<string,string> { { "controlled_current", "True" } }).success) throw new InvalidOperationException("actual silver refinement");
            devices.Advance(10); if (!devices.Collect("device_industrial_electrolysis_cell").success || s.CountItem("material_silver_catalytic") != 1 || !s.agent.magnetic.mature) throw new InvalidOperationException("silver delivery/mature suppression");
            foreach (string unresolved in cat.magnetic_unresolved_sample_ids)
            {
                var uncertain = new GameState { currentRegion = "magnetic_plateau" }; uncertain.IdentifySample(unresolved);
                if (new AgentController(uncertain, cat).Publish("MATERIAL_ANALYZED", unresolved, 10000).reason != "magnetic_identity_not_authoritative") throw new InvalidOperationException("unresolved identity inferred from name");
            }
            for (int mode = 0; mode < 4; mode++)
            {
                var c = new GameState { currentRegion = "magnetic_plateau" };
                if (mode == 1) { c.AddVisitedRegion("abandoned_mine"); c.AddChemistryKnowledge("no_reaction:fixture"); }
                if (mode == 2) c.AddCraftedDevice("device_high_pressure_gas_tank");
                if (mode == 3) c.AddCraftedDevice("device_industrial_condenser");
                var decision = new AgentController(c, cat).Publish("WORLD_REACTION_OBSERVED", "contract_fixture", 10000, outputs: new[] { "compound_h2" },
                    evidence: new AgentEvent { observed = true, phenomenon = "electrolysis_completed", condensable = mode == 3 });
                string expected = new[] { "magnetic_electrolysis_completed", "magnetic_mine_metal_recall", "magnetic_gas_tank_reuse", "magnetic_condenser_reuse" }[mode];
                if (decision.reaction_id != expected || c.agent.magnetic.mature) throw new InvalidOperationException("electrolysis comparison/maturity gate " + mode);
            }
        }
        public static void Ladders()
        {
            var cat = AgentCatalog.Load(); var s = new GameState { currentRegion = "magnetic_plateau" };
            var a = new AgentController(s, cat); string item = cat.analyses.First(x => x.region_id == "magnetic_plateau").item_id;
            Action drain = () => { while (a.Queue.Current != null) a.Queue.Advance(5, false); };
            for (int i = 1; i <= 9; i++)
            {
                var d = a.Publish("MATERIAL_COLLECTED", item, i * 31000, analyzable: true); drain();
                string expected = i == 1 ? "magnetic_unknown_material" : i == 3 ? "magnetic_analyzer_reuse" : i == 6 ? "magnetic_failure_hint_2" : i == 9 ? "magnetic_failure_hint_3" : "";
                if (expected != "" && d.reaction_id != expected) throw new InvalidOperationException("magnetic source unknown ladder " + i);
            }
            if (!a.Publish("EXPERIMENT_FAILED", "fixture", 300000, evidence: new AgentEvent { behavior_profile = "magnetic_plateau", failure_reason = "missing_input" }).Silent || s.agent.magnetic.failure_count != 0)
                throw new InvalidOperationException("nonformal failure counted");
            string reason = cat.ForRegion("magnetic_plateau").failure_reasons.First();
            for (int i = 1; i <= 6; i++) { a.Publish("EXPERIMENT_FAILED", "fixture", 310000 + i * 31000, evidence: new AgentEvent { behavior_profile = "magnetic_plateau", failure_reason = reason }); drain(); }
            if (s.agent.magnetic.failure_count != 6 || s.agent.magnetic.failure_hint_level != 3) throw new InvalidOperationException("formal failure contract ladder");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u25_ladder_" + Guid.NewGuid().ToString("N"), "save.json")), error;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            if (!repo.Write(s, out error)) throw new InvalidOperationException(error);
            var loaded = repo.Read(); if (!loaded.success || loaded.state.agent.magnetic.analysis_hint_level != 3 || loaded.state.agent.magnetic.failure_count != 6) throw new InvalidOperationException("ladder disk restore");
            loaded.state.agent.magnetic.failure_hint_level = 4; if (repo.Write(loaded.state, out error)) throw new InvalidOperationException("invalid ladder persisted");
            a.Publish("REGION_ENTERED", s.currentRegion, 600000); if (s.agent.magnetic.unknown_count != 0 || s.agent.magnetic.failure_count != 0) throw new InvalidOperationException("entry did not reset progress");
            var boundary = new GameState { currentRegion = s.currentRegion }; var b = new AgentController(boundary, cat);
            b.Publish("MATERIAL_COLLECTED", item, 1000, analyzable: true); while (b.Queue.Current != null) b.Queue.Advance(5, false);
            b.Publish("MATERIAL_COLLECTED", item, 2000, analyzable: true);
            if (b.Publish("MATERIAL_COLLECTED", item, 29999, analyzable: true).reason != "magnetic_collection_observed" ||
                b.Publish("MATERIAL_COLLECTED", item, 30000, analyzable: true).reaction_id != "magnetic_analyzer_reuse") throw new InvalidOperationException("30-second boundary");
        }
        public static void ActualAnalyses()
        {
            var cat = AgentCatalog.Load();
            foreach (var row in cat.analyses.Where(a => a.region_id == "magnetic_plateau"))
            {
                var s = new GameState { currentRegion = "magnetic_plateau", laboratoryStage = 2 }; s.AddVisitedRegion(s.currentRegion); s.AddItem(row.item_id);
                var agent = new AgentController(s, cat, () => new AgentEnvironmentFacts { ready = true, region = s.currentRegion, has_electrical_activity = true, electrical_activity = cat.magnetic_authored_activity });
                if (agent.Publish("MATERIAL_ANALYZED", row.item_id, 10000).reason != "magnetic_identity_not_authoritative") throw new InvalidOperationException("forged magnetic analysis");
                var devices = new DeviceRuntimeService(s, GameData.Devices, GameData.ProcessingRecipes);
                foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
                {
                    var def = devices.Devices.First(d => d.id == id); s.AddVisitedRegion(def.crafting.unlock_region);
                    foreach (var q in def.crafting.inputs) s.AddItem(q.id, q.n);
                    if (!devices.Craft(id).success || !devices.Place(id, id).success) throw new InvalidOperationException("magnetic detector craft");
                }
                devices.GameplayEventPublished += e => { if (e.event_type == "device_processing_started") agent.Publish("MATERIAL_ANALYZED", e.analyzed_sample_id, 20000); };
                if (!devices.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new System.Collections.Generic.Dictionary<string,string> { { "sample_id", row.item_id } }).success)
                    throw new InvalidOperationException("magnetic real detection");
                string expected = row.facts.Contains("redox_active") ? "magnetic_redox_property" : row.facts.Contains("nitrate_electrolyte_context") ? "magnetic_electrolyzer_guidance" : "magnetic_material_analysis";
                if (s.CountItem(row.item_id) != 1 || !s.agent.magnetic.analysis_completed || agent.Queue.Current == null || agent.Queue.Current.reaction_id != expected)
                    throw new InvalidOperationException("magnetic actual analysis branch");
                if (agent.Publish("COMPOSITION_DISCOVERED", row.item_id, 30000).reason != "magnetic_analysis_already_explained") throw new InvalidOperationException("magnetic fact replay");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(s));
                if (new AgentController(restored, cat).Publish("MATERIAL_ANALYZED", row.item_id, 40000).reason != "magnetic_analysis_already_explained") throw new InvalidOperationException("magnetic snapshot lost facts");
            }
        }
        public static void Entries()
        {
            var catalog = AgentCatalog.Load(); var p = catalog.ForRegion("magnetic_plateau");
            for (int mode = 0; mode < 7; mode++)
            {
                var state = new GameState { currentRegion = "magnetic_plateau" }; state.AddVisitedRegion(state.currentRegion);
                var env = new AgentEnvironmentFacts { region = state.currentRegion, ready = true, has_electrical_activity = true,
                    electrical_activity = mode == 0 ? catalog.magnetic_authored_activity : mode == 1 ? p.minimum_electrical_activity : 0.1f };
                if (mode == 2) env.ready = false;
                if (mode == 3) env.has_electrical_activity = false;
                if (mode == 4) env.region = "ember_rift";
                if (mode == 5) env.electrical_activity = float.NaN;
                if (mode == 6) env.electrical_activity = float.PositiveInfinity;
                var context = new AgentContext(state, catalog, env); env.electrical_activity = 1f;
                string before = JsonUtility.ToJson(state);
                var decision = new AgentDecisionPolicy(catalog).Decide(new AgentEvent { type = "REGION_ENTERED", subject = state.currentRegion, region = state.currentRegion, time_ms = 10000 }, context, state.agent, 0);
                string expected = p.Reaction(mode <= 1 ? "environment_electricity" : "entry");
                if (decision.reaction_id != expected || before != JsonUtility.ToJson(state)) throw new InvalidOperationException("magnetic snapshot guard/mutation mode " + mode);
                var agent = new AgentController(state, catalog, () => context.environment);
                if (agent.Publish("REGION_ENTERED", state.currentRegion, 10000).reaction_id != expected || agent.Queue.Current.text != catalog.responses.First(r => r.reaction_id == expected).text)
                    throw new InvalidOperationException("magnetic source text not connected");
                if (agent.Publish("REGION_ENTERED", state.currentRegion, 20000).reason != "already_shown") throw new InvalidOperationException("entry replay");
            }
        }
    }
}
