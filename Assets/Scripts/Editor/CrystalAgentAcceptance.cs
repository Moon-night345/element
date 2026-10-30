using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class CrystalAgentAcceptance
    {
        const string Region = CrystalTundraAgentBehavior.RegionId, Sample = "resource_carbonate_rock";
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static string Gameplay(GameState state)
        { var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); copy.agent = null; return JsonUtility.ToJson(copy); }
        sealed class Rig
        {
            public readonly GameState state = new GameState { currentRegion = Region };
            public readonly AgentCatalog catalog = AgentCatalog.Load();
            public readonly AgentController agent;
            public readonly DeviceRuntimeService devices;
            public bool ready = true, hasTemperature = true;
            public float temperature = 0.12f;
            public long now = 10000;
            public string lastSample = "";
            public Rig()
            {
                state.AddVisitedRegion(Region);
                agent = new AgentController(state, catalog, () => new AgentEnvironmentFacts { region = state.currentRegion, ready = ready,
                    has_temperature = hasTemperature, temperature = temperature });
                devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                devices.GameplayEventPublished += e => {
                    if (e.event_type == "device_built" && e.device_id == CrystalTundraAgentBehavior.DeviceId)
                        Event("DEVICE_CRAFTED", e.device_id, new AgentEvent { behavior_profile = Region });
                    if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect")
                    {
                        lastSample = e.analyzed_sample_id;
                        Event("MATERIAL_ANALYZED", e.analyzed_sample_id, new AgentEvent { behavior_profile = Region, analyzer_used = true });
                    }
                };
            }
            public AgentDecision Event(string type, string subject = Sample, AgentEvent evidence = null, bool analyzable = false, bool ui = false)
            { return agent.Publish(type, subject, now++, ui, analyzable, evidence: evidence); }
            public void Drain() { for (int i = 0; agent.Queue.Current != null && i < 100; i++) agent.Queue.Advance(5, false); }
            public void PrepareAnalyzer()
            {
                state.currentRegion = "prologue_lab";
                // Raw material/stage prerequisites are explicit test fixtures, not a player-route claim.
                var def = devices.Devices.First(d => d.id == CrystalTundraAgentBehavior.DeviceId);
                state.laboratoryStage = Math.Max(def.stage, def.crafting.unlock_lab_stage);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) state.AddVisitedRegion(def.crafting.unlock_region);
                state.AddCraftedDevice("device_chemical_workbench");
                foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                now += 5000;
                Require(devices.Craft(def.id).success && devices.Place(def.id, "u18_analyzer").success, "actual analyzer craft/place fixture");
                Drain();
            }
            public void Detect(string id)
            {
                now += 5000;
                Require(devices.Start("u18_analyzer", "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", id } }).success, "real analyzer start");
                devices.Advance(10);
                Require(devices.Collect("u18_analyzer").success, "real analyzer collect");
            }
        }
        public static void EntryAndStall()
        {
            var r = new Rig(); r.ready = false;
            Require(r.Event("REGION_ENTERED", Region).reason == "environment_state_unavailable", "unready cold entry accepted");
            r.ready = true; r.hasTemperature = false;
            Require(r.Event("REGION_ENTERED", Region).Silent, "missing temperature accepted");
            r.hasTemperature = true; r.temperature = float.NaN;
            Require(r.Event("REGION_ENTERED", Region).Silent, "NaN temperature accepted");
            r.temperature = 0.26f; Require(r.Event("REGION_ENTERED", Region).Silent, "warm entry accepted");
            r.temperature = r.catalog.crystal_authored_temperature;
            string before = Gameplay(r.state);
            Require(r.Event("REGION_ENTERED", Region).reaction_id == "crystal_tundra_first_entry" && Gameplay(r.state) == before, "confirmed entry / readonly");
            Require(r.Event("REGION_ENTERED", Region).reason == "already_shown", "entry repeated");
            r.Drain();
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "crystal_unknown_material_observed", "unknown material");
            Require(!r.agent.Queue.Current.text.Contains("碳酸"), "unknown sample leaked composition"); r.Drain();
            r.Event("MATERIAL_COLLECTED", analyzable: true);
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).Silent, "hint before 30 seconds");
            r.state.agent.crystal.unknown_material_count = 2; r.now = r.state.agent.crystal.last_progress_ms + 30000;
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "crystal_analysis_hint_level_1", "threshold3 at30seconds"); r.Drain();
            r.state.agent.crystal.unknown_material_count = 5; r.now += 31000;
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "crystal_analysis_hint_level_2", "threshold6"); r.Drain();
            r.state.agent.crystal.unknown_material_count = 8; r.now += 31000;
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "crystal_analysis_hint_level_3" && r.state.agent.crystal.analysis_hint_level == 3, "threshold9");
            Require(!r.agent.Queue.Current.text.Contains("CaCO") && !r.agent.Queue.Current.text.Contains("碳酸钙"), "ladder leaked answer");
        }
        public static void KnowledgeAndActualAnalyzer()
        {
            var r = new Rig(); string before = Gameplay(r.state);
            Require(r.Event("MATERIAL_ANALYZED", evidence: new AgentEvent { analyzer_used = true }).reason == "knowledge_not_discovered" &&
                !r.state.agent.crystal.analyzer_used && Gameplay(r.state) == before, "forged analysis / use / mutation");
            r.PrepareAnalyzer();
            Require(r.state.agent.crystal.analyzer_available && r.state.agent.shown.Contains("crystal_analyzer_available"), "cross-region built event not connected");
            r.state.AddItem("resource_frozen_water"); r.Detect("resource_frozen_water");
            Require(r.lastSample == "resource_frozen_water" && r.agent.Queue.Current.reaction_id == "crystal_material_analysis" &&
                !r.agent.Queue.Current.text.Contains("碳酸") && !r.state.agent.crystal.mature, "noncarbonate scan leaked identity / matured"); r.Drain();
            r.state.AddItem(Sample); int count = r.state.CountItem(Sample); r.Detect(Sample);
            Require(r.lastSample == Sample && r.state.CountItem(Sample) == count && r.state.HasIdentifiedSample(Sample) &&
                r.agent.Queue.Current.reaction_id == "crystal_carbonate_confirmed" && r.agent.Queue.Current.text.Contains("CaCO₃") &&
                r.state.agent.crystal.mature && r.state.agent.crystal.analyzer_used, "actual precise analyzer confirmation / no consumption / maturity");
            r.Drain(); r.state.currentRegion = Region;
            Require(r.Event("REGION_ENTERED", Region).reason == "crystal_guidance_mature", "mature reentry spam");
            var generic = new Rig(); const string block = "item_crystal_tundra_carbonate_rock_block";
            generic.state.IdentifySample(block); before = Gameplay(generic.state);
            Require(generic.Event("MATERIAL_ANALYZED", block).reaction_id == "crystal_carbonate_confirmed" &&
                generic.agent.Queue.Current.text.Contains("碳酸盐") && !generic.agent.Queue.Current.text.Contains("CaCO") &&
                !generic.state.agent.crystal.analyzer_used && !generic.state.agent.crystal.mature && Gameplay(generic.state) == before,
                "general carbonate facts upgraded to exact formula / fictional analyzer use");
            Require(generic.Event("MATERIAL_ANALYZED", block).reason == "crystal_fact_already_explained", "repeated fact");
        }
        public static void PhenomenaAndSuppression()
        {
            var r = new Rig();
            Require(r.Event("DEVICE_CRAFTED", CrystalTundraAgentBehavior.DeviceId).Silent && !r.state.agent.crystal.analyzer_available, "forged crafted event");
            Require(r.Event("WORLD_REACTION_OBSERVED", evidence: new AgentEvent { observed = true, phenomenon = "temperature_state_changed" }).reason == "phenomenon_not_confirmed" &&
                !r.state.agent.crystal.temperature_observed, "observed flag invented temperature gameplay");
            Require(r.Event("WORLD_REACTION_OBSERVED", evidence: new AgentEvent { observed = true, phenomenon = "dissolution_state_changed" }).reason == "unsupported_crystal_phenomenon", "invented dissolution");
            // Snapshot contracts below are fixtures only, not claims of a real live publisher.
            r.state.AddChemistryKnowledge("temperature_state_changed:" + Sample);
            string before = Gameplay(r.state);
            Require(r.Event("WORLD_REACTION_OBSERVED", evidence: new AgentEvent { observed = true, phenomenon = "temperature_state_changed" }).reaction_id == "crystal_temperature_observed" &&
                r.state.agent.crystal.temperature_observed && Gameplay(r.state) == before, "confirmed phenomenon contract"); r.Drain();
            r.state.AddChemistryKnowledge("crystallization_state_changed:" + Sample);
            Require(r.Event("WORLD_REACTION_OBSERVED", evidence: new AgentEvent { observed = true, phenomenon = "crystallization_state_changed" }).action == "CELEBRATE", "crystallization contract");
            var ui = new Rig();
            Require(ui.Event("MATERIAL_COLLECTED", analyzable: true, ui: true).reason == "ui_already_explains" && ui.state.agent.crystal.unknown_material_count == 1 && ui.agent.Queue.Current == null,
                "UI suppression discarded attention / duplicate dialogue");
            var budget = new Rig(); budget.state.AddCraftedDevice(CrystalTundraAgentBehavior.DeviceId);
            budget.state.agent.last_dialogue_ms = budget.now - 1;
            Require(budget.Event("DEVICE_CRAFTED", CrystalTundraAgentBehavior.DeviceId).reason == "global_cooldown" &&
                budget.state.agent.crystal.analyzer_available && budget.agent.Queue.Current == null, "analyzer hint bypassed budget / discarded confirmed availability");
            budget.now += 3000;
            Require(budget.Event("DEVICE_CRAFTED", CrystalTundraAgentBehavior.DeviceId).reaction_id == "crystal_analyzer_available", "analyzer budget boundary");
            ui.state.currentRegion = "rust_jungle"; ui.state.IdentifySample(Sample);
            Require(ui.Event("MATERIAL_ANALYZED", evidence: new AgentEvent { behavior_profile = Region }).reaction_id == "crystal_carbonate_confirmed" &&
                ui.state.agent.rust.known_properties.Length == 0, "tagged cross-region event swallowed by Rust adapter");
        }
        public static void PersistenceAndFailure()
        {
            var r = new Rig(); r.PrepareAnalyzer(); r.state.AddItem(Sample); r.Detect(Sample);
            string path = Path.GetFullPath(Path.Combine("Logs", "u18_crystal_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(r.state, out reason), "save " + reason);
            var loaded = repo.Read();
            Require(loaded.success && JsonUtility.ToJson(loaded.state.agent.crystal) == JsonUtility.ToJson(r.state.agent.crystal), "crystal snapshot lost");
            Require(new AgentController(loaded.state, r.catalog).Publish("MATERIAL_COLLECTED", Sample, r.now + 1000, analyzable: true,
                evidence: new AgentEvent { behavior_profile = Region }).reason == "crystal_guidance_mature", "loaded maturity lost");
            loaded.state.agent.crystal.analyzer_used = false;
            Require(!repo.Write(loaded.state, out reason) && repo.Read().success, "invalid mature state committed");
            var fresh = new Rig(); fresh.PrepareAnalyzer(); string before = Gameplay(fresh.state);
            Require(!fresh.devices.Start("u18_analyzer", "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", Sample } }).success &&
                fresh.lastSample == "" && !fresh.state.agent.crystal.analysis_completed && !fresh.state.agent.crystal.mature &&
                fresh.state.CountItem(Sample) == 0 && !fresh.state.HasIdentifiedSample(Sample), "failed scan emitted success");
            // Device failure-state bookkeeping may change; policy alone must never change gameplay.
            before = Gameplay(fresh.state);
            fresh.Event("ANALYSIS_FAILED", Sample);
            Require(Gameplay(fresh.state) == before, "failed analysis Agent mutated gameplay");
            fresh.state.agent.crystal = null;
            new AgentController(fresh.state, fresh.catalog);
            Require(fresh.state.agent.crystal != null && !fresh.state.agent.crystal.mature, "old-save default broken");
        }
    }
}
