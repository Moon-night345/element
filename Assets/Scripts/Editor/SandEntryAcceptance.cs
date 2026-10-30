using System;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class SandEntryAcceptance
    {
        static void Require(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        public static void AnalysisBranches()
        {
            var catalog = AgentCatalog.Load();
            var cases = new[] {
                new[] { "item_wind_desert_carbonaceous_deposit_block", "sand_carbon_confirmed" },
                new[] { "item_wind_desert_weathered_mineral_crust_block", "sand_material_analysis" },
                new[] { "item_wind_desert_sandstone_block", "sand_material_analysis" },
                new[] { "item_wind_desert_carbonate_rock_block", "sand_ember_thermal_hypothesis" }
            };
            foreach (var row in cases)
            {
                var state = new GameState { currentRegion = "sand_desert" }; state.AddItem(row[0]);
                if (row[1] == "sand_ember_thermal_hypothesis") { state.AddVisitedRegion("ember_rift"); state.AddChemistryKnowledge("thermal:resource_carbonate_rock"); }
                Require(new WorldChemistryService(state).AnalyzeSandBlock(row[0]).success && state.CountItem(row[0]) == 0, "real source terrain analysis branch");
                var agent = new AgentController(state, catalog);
                var decision = agent.Publish("MATERIAL_ANALYZED", row[0], 10000, evidence: new AgentEvent { behavior_profile = "sand_desert" });
                Require(decision.reaction_id == row[1] && agent.Queue.Current != null && !state.agent.sand.cross_region_link && !state.agent.sand.mature, "analysis branch invented confirmed link");
                Require(!agent.Queue.Current.text.Contains("CaCO") && !agent.Queue.Current.text.Contains("解锁"), "invented exact identity / device unlock");
                if (row[1] == "sand_ember_thermal_hypothesis") Require(decision.action == "HINT", "thermal hypothesis became fact");
            }
        }
        public static void LaddersAndFailures()
        {
            var catalog = AgentCatalog.Load(); var state = new GameState { currentRegion = "sand_desert" };
            var agent = new AgentController(state, catalog); string sample = WorldChemistryService.SandBlocks[0];
            Action drain = () => { while (agent.Queue.Current != null) agent.Queue.Advance(5, false); };
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 1000, analyzable: true).reaction_id == "sand_unknown_material_observed", "first unknown"); drain();
            agent.Publish("MATERIAL_COLLECTED", sample, 2000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 29999, analyzable: true).reason == "sand_collection_observed", "30sec gate bypass");
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 30000, analyzable: true).reaction_id == "sand_analysis_hint_level_1", "30sec boundary"); drain();
            agent.Publish("MATERIAL_COLLECTED", sample, 31000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 61000, analyzable: true).reaction_id == "sand_analysis_hint_level_2", "threshold6"); drain();
            agent.Publish("MATERIAL_COLLECTED", sample, 62000, analyzable: true); agent.Publish("MATERIAL_COLLECTED", sample, 63000, analyzable: true);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 92000, analyzable: true).reaction_id == "sand_analysis_hint_level_3" &&
                state.agent.sand.analysis_hint_level == 3 && !agent.Queue.Current.text.Contains("NaCl") && !agent.Queue.Current.text.Contains("CaCO"), "threshold9 / leaked composition"); drain();
            Require(agent.Publish("EXPERIMENT_FAILED", "fixture", 93000, evidence: new AgentEvent { behavior_profile = "sand_desert", failure_reason = "missing_input" }).Silent &&
                state.agent.sand.failure_count == 0, "unapproved reason counted");
            long now = 100000;
            for (int i = 1; i <= 7; i++)
            {
                // Formal event fixture only. Source explicitly has no live Sand failure publisher.
                var decision = agent.Publish("EXPERIMENT_FAILED", "fixture", now, evidence: new AgentEvent { behavior_profile = "sand_desert", failure_reason = "wrong_processing_path" }); now += 31000;
                if (i == 1) Require(decision.reaction_id == "sand_failure_observed", "first failure");
                if (i == 3 || i == 5 || i == 7) Require(decision.reaction_id == "sand_failure_hint_level_" + ((i - 1) / 2), "failure ladder"); drain();
            }
            Require(state.agent.sand.failure_count == 7 && state.agent.sand.failure_hint_level == 3, "failure state");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u21_ladder_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(state, out reason), "ladder save " + reason); var loaded = repo.Read();
            Require(loaded.success && loaded.state.agent.sand.failure_hint_level == 3 && loaded.state.agent.sand.unknown_count == 9, "ladder disk lost");
            loaded.state.agent.sand.failure_count = -1; Require(!repo.Write(loaded.state, out reason), "negative failure count committed");
            loaded = repo.Read(); loaded.state.agent.sand.analysis_hint_level = 4; Require(!repo.Write(loaded.state, out reason), "illegal analysis ladder committed");
            state.AddItem(sample); Require(new WorldChemistryService(state).AnalyzeSandBlock(sample).success, "progress analysis");
            Require(agent.Publish("MATERIAL_ANALYZED", sample, now, uiExplains: true).reason == "ui_already_explains" &&
                state.agent.sand.unknown_count == 0 && state.agent.sand.failure_count == 0 && state.agent.sand.last_progress_ms == now, "progress reset under UI suppression");
            var cross = agent.Publish("EXPERIMENT_FAILED", "fixture", now + 1000, evidence: new AgentEvent { behavior_profile = "ember_rift", failure_reason = "wrong_processing_path" });
            Require(state.agent.sand.failure_count == 0, "other profile failure counted");
        }
        public static void HypothesisAndConfirmation()
        {
            var catalog = AgentCatalog.Load(); const string sample = "item_wind_desert_carbonate_rock_block";
            Require(WorldChemistryService.SandBlocks.All(id => catalog.analyses.Any(a => a.item_id == id && a.region_id == "sand_desert")), "source sample export");
            var state = new GameState { currentRegion = "sand_desert" };
            state.AddVisitedRegion("crystal_tundra"); state.IdentifySample("resource_carbonate_rock"); state.AddItem(sample);
            var agent = new AgentController(state, catalog); var world = new WorldChemistryService(state);
            Require(agent.Publish("MATERIAL_COLLECTED", sample, 10000, analyzable: true).reaction_id == "sand_cross_region_hypothesis" &&
                state.agent.sand.hypothesis_pending && !state.agent.sand.cross_region_link && !state.agent.sand.mature && !state.HasIdentifiedSample(sample), "hypothesis created gameplay fact / linkage");
            Require(!agent.Queue.Current.text.Contains("CaCO") && !agent.Queue.Current.text.Contains("确认"), "hypothesis leaked exact composition");
            Require(agent.Publish("MATERIAL_ANALYZED", sample, 11000).reason == "knowledge_not_discovered" && !state.agent.sand.analysis_completed, "forged analysis");
            world.GameplayEventPublished += e => { if (e.success) agent.Publish("MATERIAL_ANALYZED", sample, 20000, evidence: new AgentEvent { behavior_profile = "sand_desert" }); };
            Require(world.AnalyzeSandBlock(sample).success && state.CountItem(sample) == 0 && state.agent.sand.analysis_completed &&
                state.agent.sand.cross_region_link && state.agent.sand.mature && !state.agent.sand.hypothesis_pending && agent.Queue.Current.reaction_id == "sand_crystal_recall_confirmed", "real destructive analysis did not confirm link");
            Require(agent.Publish("REGION_ENTERED", "sand_desert", 30000).reason == "sand_guidance_mature", "mature reentry");
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine("Logs", "u21_sand_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(state, out reason), "sand snapshot " + reason); var loaded = repo.Read();
            Require(loaded.success && loaded.state.agent.sand.cross_region_link && loaded.state.agent.sand.mature, "sand snapshot lost");
            loaded.state.agent.sand.analysis_completed = false; Require(!repo.Write(loaded.state, out reason), "invalid link/maturity saved");
            var generic = new GameState { currentRegion = "sand_desert" }; generic.AddItem(sample);
            var genericAgent = new AgentController(generic, catalog);
            Require(genericAgent.Publish("MATERIAL_COLLECTED", sample, 10000, analyzable: true).reaction_id == "sand_unknown_material_observed", "invented old knowledge");
            Require(new WorldChemistryService(generic).AnalyzeSandBlock(sample).success, "generic analysis");
            var decision = genericAgent.Publish("MATERIAL_ANALYZED", sample, 20000);
            Require(decision.reaction_id == "sand_carbonate_confirmed" && !generic.agent.sand.cross_region_link && !generic.agent.sand.mature,
                "analysis alone invented cross-region link");
            Require(!new DeterministicAgentResponseProvider(catalog).Response(decision, "sand_desert").text.Contains("CaCO"), "generic carbonate became exact compound");
            string before = JsonUtility.ToJson(generic);
            Require(!new WorldChemistryService(generic).AnalyzeSandBlock(sample).success && before == JsonUtility.ToJson(generic), "missing sample changed state");
        }
        public static void Entries()
        {
            var catalog = AgentCatalog.Load();
            for (int mode = 0; mode < 5; mode++)
            {
                var state = new GameState { currentRegion = "sand_desert" };
                if (mode == 1) state.IdentifySample("resource_carbonate_rock"); // No confirmed region visit: insufficient.
                if (mode == 2) { state.IdentifySample("resource_carbonate_rock"); state.AddVisitedRegion("crystal_tundra"); }
                if (mode == 3) { state.AddChemistryKnowledge("thermal:resource_carbonate_rock"); state.AddVisitedRegion("ember_rift"); }
                if (mode == 4) { state.AddChemistryKnowledge("distillation_completed"); state.AddVisitedRegion("salt_marsh"); }
                string before = JsonUtility.ToJson(state);
                var policy = new AgentDecisionPolicy(catalog);
                var e = new AgentEvent { type = "REGION_ENTERED", region = "sand_desert", subject = "sand_desert", time_ms = 10000 };
                var d = policy.Decide(e, new AgentContext(state, catalog), state.agent, 0);
                string expected = catalog.ForRegion("sand_desert").Reaction(mode >= 2 ? "entry_recall" : "entry");
                Require(d.reaction_id == expected && before == JsonUtility.ToJson(state), "recall guard / policy mutation mode " + mode);
                var agent = new AgentController(state, catalog);
                Require(agent.Publish("REGION_ENTERED", "sand_desert", 10000).reaction_id == expected && agent.Queue.Current.text ==
                    catalog.responses.First(r => r.reaction_id == expected).text, "source text not connected");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                Require(new AgentController(restored, catalog).Publish("REGION_ENTERED", "sand_desert", 20000).reason == "already_shown", "recall entry replay");
            }
        }
    }
}
