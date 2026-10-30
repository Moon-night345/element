using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Agent;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class SaltAgentAcceptance
    {
        const string Region = SaltMarshAgentBehavior.RegionId, Acid = "resource_acidic_solution", Base = "resource_alkaline_solution";
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        static string Gameplay(GameState state)
        { var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); copy.agent = null; return JsonUtility.ToJson(copy); }
        sealed class Rig
        {
            public readonly GameState state = new GameState { currentRegion = Region };
            public readonly AgentCatalog catalog = AgentCatalog.Load();
            public readonly AgentController agent;
            public readonly DeviceRuntimeService devices;
            public readonly WorldChemistryService world;
            public bool ready = true, hasSolution = true;
            public float solution = 0.90f;
            public long now = 10000;
            public Rig()
            {
                state.AddVisitedRegion(Region);
                agent = new AgentController(state, catalog, () => new AgentEnvironmentFacts { region = state.currentRegion, ready = ready,
                    has_solution_availability = hasSolution, solution_availability = solution });
                devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                world = new WorldChemistryService(state);
                devices.GameplayEventPublished += e => {
                    if (e.event_type == "device_built" && (e.device_id == SaltMarshAgentBehavior.DeviceId || e.device_id == SaltMarshAgentBehavior.FilterId))
                        Event("DEVICE_CRAFTED", e.device_id, new AgentEvent { behavior_profile = Region });
                    if (e.event_type == "device_processing_started" && e.recipe_id == "recipe_phase2_detect")
                    {
                        Event("MATERIAL_ANALYZED", e.analyzed_sample_id, new AgentEvent { behavior_profile = Region, analyzer_used = true });
                        Observation("ph_measured", e.analyzed_sample_id);
                    }
                    if (e.event_type == "device_output_collected")
                    {
                        string[] outputs = e.items.Where(q => q.n > 0).Select(q => q.id).ToArray();
                        if (e.recipe_id == "recipe_phase2_filter") Observation("filtration_completed", e.recipe_id, outputs);
                        if (e.recipe_id == "recipe_phase2_distill") Observation("distillation_completed", e.recipe_id, outputs);
                    }
                };
                world.GameplayEventPublished += e => {
                    if (!e.success) return;
                    if (e.action_id == "neutralize_solutions") Observation("neutralization_completed", e.action_id);
                    if (e.action_id == "precipitate_solution") Observation("precipitate_observed", e.action_id);
                };
            }
            public AgentDecision Event(string type, string subject = Acid, AgentEvent evidence = null, bool analyzable = false, bool ui = false, string[] outputs = null)
            {
                string before = Gameplay(state);
                var result = agent.Publish(type, subject, now++, ui, analyzable, outputs, evidence);
                Require(Gameplay(state) == before, "Agent mutated gameplay for " + type);
                return result;
            }
            public AgentDecision Observation(string phenomenon, string subject = "solution", string[] outputs = null)
            { return Event("WORLD_REACTION_OBSERVED", subject, new AgentEvent { behavior_profile = Region, observed = true, phenomenon = phenomenon }, outputs: outputs); }
            public void Drain() { for (int i = 0; agent.Queue.Current != null && i < 100; i++) agent.Queue.Advance(5, false); }
            public void Gather(string id, int count = 1)
            {
                state.currentRegion = Region; string reason;
                for (int i = 0; i < count; i++) Require(new FiniteResourceService(state, GameData.Regions).GatherRegion(Region, id, out reason), "finite gather " + reason);
            }
            public void Prepare(string device, bool unlockFixture = true)
            {
                // Manufacturing raw materials / Stage / unlock visits are explicit fixtures, not a player route.
                state.currentRegion = "prologue_lab"; var def = devices.Devices.First(d => d.id == device);
                state.laboratoryStage = Math.Max(def.stage, def.crafting.unlock_lab_stage);
                state.AddCraftedDevice("device_chemical_workbench");
                if (unlockFixture && !string.IsNullOrEmpty(def.crafting.unlock_region)) state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) state.AddItem(q.id, q.n);
                Drain(); now += 5000;
                Require(devices.Craft(device).success && devices.Place(device, device).success, "real craft/place " + device);
                Drain(); now += 5000;
            }
            public void Detect(string id)
            {
                Require(devices.Start(CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", id } }).success, "actual detection");
                devices.Advance(10); Require(devices.Collect(CrystalTundraAgentBehavior.DeviceId).success, "detection collection");
            }
        }
        public static void EntryAndLadder()
        {
            var r = new Rig(); r.ready = false;
            Require(r.Event("REGION_ENTERED", Region).reason == "environment_state_unavailable", "unready entry");
            r.ready = true; r.hasSolution = false; Require(r.Event("REGION_ENTERED", Region).Silent, "missing solution snapshot");
            r.hasSolution = true; r.solution = float.PositiveInfinity; Require(r.Event("REGION_ENTERED", Region).Silent, "nonfinite solution");
            r.solution = 0.74f; Require(r.Event("REGION_ENTERED", Region).Silent, "insufficient solution");
            r.solution = r.catalog.salt_authored_solution_availability;
            Require(r.Event("REGION_ENTERED", Region).reaction_id == "salt_marsh_first_entry" && !r.agent.Queue.Current.text.Contains("碱性"), "entry / knowledge guard");
            Require(r.Event("REGION_ENTERED", Region).reason == "already_shown", "entry replay"); r.Drain();
            Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "salt_unknown_material_observed" && !r.agent.Queue.Current.text.Contains("盐酸"), "unknown identity leak"); r.Drain();
            r.Event("MATERIAL_COLLECTED", analyzable: true); Require(r.Event("MATERIAL_COLLECTED", analyzable: true).Silent, "premature stall hint");
            for (int level = 1; level <= 3; level++)
            {
                r.state.agent.salt.unknown_material_count = level * 3 - 1;
                r.now = r.state.agent.salt.last_progress_ms + level * 31000;
                Require(r.Event("MATERIAL_COLLECTED", analyzable: true).reaction_id == "salt_analysis_hint_level_" + level, "ladder " + level);
                Require(!r.agent.Queue.Current.text.Contains("NaCl") && !r.agent.Queue.Current.text.Contains("氢氧根"), "hint leaked chemistry"); r.Drain();
            }
            var ui = new Rig();
            Require(ui.Event("MATERIAL_COLLECTED", analyzable: true, ui: true).reason == "ui_already_explains" && ui.state.agent.salt.unknown_material_count == 1 && ui.agent.Queue.Current == null, "UI duplicate suppression");
        }
        public static void AnalysisAndPh()
        {
            var r = new Rig(); Require(r.Event("MATERIAL_ANALYZED").reason == "knowledge_not_discovered", "forged composition");
            Require(r.Observation("ph_measured", Acid).reason == "phenomenon_not_confirmed" && !r.state.agent.salt.ph_observed, "forged pH");
            r.Gather(Acid); r.Prepare(CrystalTundraAgentBehavior.DeviceId); r.Detect(Acid);
            Require(r.state.agent.salt.analysis_completed && r.state.agent.salt.solution_property_known && r.state.agent.salt.ph_observed &&
                !r.state.agent.salt.mature && r.state.CountItem(Acid) == 1 && r.agent.Queue.Current.text.Contains("盐酸"), "real acid scan / consumption / maturity");
            r.agent.Queue.Advance(5, false);
            Require(r.agent.Queue.Current.reaction_id == "salt_ph_confirmed" && r.agent.Queue.Current.text.Contains("偏酸"), "dynamic confirmed acidic pH text");
            var alkaline = new Rig(); alkaline.Gather(Base); alkaline.Prepare(CrystalTundraAgentBehavior.DeviceId); alkaline.Detect(Base);
            Require(alkaline.agent.Queue.Current.text.Contains("氢氧化钠"), "exact base identity"); alkaline.agent.Queue.Advance(5, false);
            Require(alkaline.agent.Queue.Current.text.Contains("偏碱"), "confirmed base pH");
            var generic = new Rig(); const string block = "item_salt_alkali_mudflat_salt_deposit_block";
            generic.state.IdentifySample(block); Require(generic.Event("MATERIAL_ANALYZED", block).reaction_id == "salt_solution_identity_confirmed" &&
                !generic.agent.Queue.Current.text.Contains("氢氧化钠") && !generic.agent.Queue.Current.text.Contains("氯化物"), "generic salt facts became exact compounds");
            Require(generic.Event("MATERIAL_ANALYZED", block).reason == "salt_fact_already_explained", "fact repeated");
            Require(generic.catalog.salt_unresolved_analysis_ids.SequenceEqual(new[] { "compound_neutralized_salt_solution" }) &&
                generic.Event("MATERIAL_ANALYZED", "compound_neutralized_salt_solution").reason == "knowledge_not_discovered", "source gap invented composition");
        }
        public static void ChemistryAndSeparation()
        {
            var r = new Rig();
            r.Gather(Acid, 2); r.Gather(Base, 4); r.Prepare(CrystalTundraAgentBehavior.DeviceId); r.Detect(Acid); r.Drain(); r.now += 5000;
            Require(r.world.NeutralizeSolutions().success && r.state.agent.salt.neutralization_observed &&
                r.state.agent.shown.Contains("salt_neutralization_observed"), "real neutralization event"); r.Drain(); r.now += 5000;
            Require(r.world.PrecipitateSolution().success && r.state.agent.salt.precipitation_observed && r.state.agent.salt.filter_need_observed &&
                r.agent.Queue.Current.reaction_id == "salt_precipitate_observed", "real precipitation event");
            Require(!r.agent.Queue.Current.text.Contains("解锁"), "precipitation invented filter unlock"); r.Drain(); r.now += 5000;
            Require(r.world.NeutralizeSolutions().success, "second real neutralization"); r.Drain(); r.now += 5000;
            Require(r.world.PrecipitateSolution().success && r.agent.Queue.Current.reaction_id == "salt_filter_need_observed", "second precipitation separation need"); r.Drain();
            Require(r.devices.Craft(SaltMarshAgentBehavior.FilterId).reason == "unlock_region_required", "filter unlocked before fossil_strata");
            r.Prepare(SaltMarshAgentBehavior.FilterId);
            Require(!r.state.agent.salt.separation_used && r.devices.Start(SaltMarshAgentBehavior.FilterId, "recipe_phase2_filter").success &&
                !r.state.agent.salt.separation_used && !r.state.agent.salt.mature, "start counted as separation");
            Require(r.Observation("filtration_completed", "recipe_phase2_filter").reason == "phenomenon_not_confirmed", "premature chemistry flag celebrated without delivered outputs");
            r.devices.Advance(10); Require(!r.state.agent.salt.separation_used, "output ready counted as collection");
            Require(r.devices.Collect(SaltMarshAgentBehavior.FilterId).success && r.state.agent.salt.filtration_used && r.state.agent.salt.mature &&
                r.agent.Queue.Current.reaction_id == "salt_filtration_completed", "real filter collection / maturity"); r.Drain();
            r.state.currentRegion = Region; Require(r.Event("REGION_ENTERED", Region).reason == "salt_guidance_mature", "mature reentry spam");
            var d = new Rig(); d.Gather(Acid); d.Gather(Base); Require(d.world.NeutralizeSolutions().success, "distill solution preparation"); d.Drain();
            d.Prepare(SaltMarshAgentBehavior.DeviceId);
            Require(d.state.agent.salt.distiller_available && d.state.agent.shown.Contains("salt_distiller_available"), "real distiller availability");
            Require(d.devices.Start(SaltMarshAgentBehavior.DeviceId, "recipe_phase2_distill").success && !d.state.agent.salt.distillation_used, "early distillation completion");
            d.devices.Advance(10); Require(d.devices.Collect(SaltMarshAgentBehavior.DeviceId).success && d.state.agent.salt.distillation_used &&
                !d.state.agent.salt.mature && d.agent.Queue.Current.reaction_id == "salt_distillation_completed", "real distill / maturity without analysis");
            d.Prepare(CrystalTundraAgentBehavior.DeviceId); d.Gather(Base); d.Detect(Base);
            Require(d.state.agent.salt.mature, "analysis-after-separation failed to mature");
        }
        public static void FailureAndPersistence()
        {
            var r = new Rig();
            Require(!r.world.NeutralizeSolutions().success && !r.world.PrecipitateSolution().success && !r.state.agent.salt.neutralization_observed &&
                !r.state.agent.salt.precipitation_observed, "failure published success");
            foreach (var p in new[] { "neutralization_completed", "precipitate_observed", "filtration_completed", "distillation_completed" })
                Require(r.Observation(p).reason == "phenomenon_not_confirmed", "forged observation " + p);
            Require(r.Observation("dissolution_state_changed").reason == "unsupported_salt_phenomenon", "invented phenomenon");
            Require(r.Event("DEVICE_CRAFTED", SaltMarshAgentBehavior.DeviceId).Silent && !r.state.agent.salt.distiller_available, "forged distiller craft");
            r.Gather(Acid); r.Gather(Base, 2); r.Prepare(CrystalTundraAgentBehavior.DeviceId); r.Detect(Acid); r.Drain();
            Require(r.world.NeutralizeSolutions().success, "persistence route neutralization"); r.Drain(); r.Prepare(SaltMarshAgentBehavior.DeviceId);
            Require(r.devices.Start(SaltMarshAgentBehavior.DeviceId, "recipe_phase2_distill").success, "persistence distill start");
            r.devices.Advance(10); Require(r.devices.Collect(SaltMarshAgentBehavior.DeviceId).success && r.state.agent.salt.mature, "persistence mature route");
            string path = Path.GetFullPath(Path.Combine("Logs", "u19_salt_" + Guid.NewGuid().ToString("N"), "save.json")), reason;
            var repo = new SaveRepository(path, "test", forceSnapshotHistory: true);
            Require(repo.Write(r.state, out reason), "salt save " + reason); var loaded = repo.Read();
            Require(loaded.success && JsonUtility.ToJson(loaded.state.agent.salt) == JsonUtility.ToJson(r.state.agent.salt), "salt snapshot lost");
            Require(new AgentController(loaded.state, r.catalog).Publish("REGION_ENTERED", Region, r.now + 1000, evidence: new AgentEvent { behavior_profile = Region }).reason == "salt_guidance_mature", "reload mature replay");
            loaded.state.agent.salt.analysis_completed = false;
            Require(!repo.Write(loaded.state, out reason) && repo.Read().success, "illegal maturity committed");
            var old = new GameState(); old.agent.salt = null; new AgentController(old, r.catalog); Require(old.agent.salt != null && !old.agent.salt.mature, "old snapshot default");
            var budget = new Rig(); budget.state.AddCraftedDevice(SaltMarshAgentBehavior.DeviceId); budget.state.agent.last_dialogue_ms = budget.now - 1;
            Require(budget.Event("DEVICE_CRAFTED", SaltMarshAgentBehavior.DeviceId).reason == "global_cooldown" && budget.state.agent.salt.distiller_available, "budget bypass / discarded availability");
            budget.now += 3000; Require(budget.Event("DEVICE_CRAFTED", SaltMarshAgentBehavior.DeviceId).reaction_id == "salt_distiller_available", "budget expiry");
            var cross = new Rig(); cross.state.currentRegion = "crystal_tundra"; cross.state.IdentifySample(Acid);
            Require(cross.Event("MATERIAL_ANALYZED", Acid, new AgentEvent { behavior_profile = Region }).reaction_id == "salt_solution_identity_confirmed" &&
                !cross.state.agent.crystal.analysis_completed, "tagged event swallowed by Crystal adapter");
        }
    }
}
