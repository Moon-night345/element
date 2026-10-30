using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;
using ElementalStarSea.Agent;

namespace ElementalStarSea.EditorTools
{
    public static class RustAgentAcceptance
    {
        sealed class Rig
        {
            public readonly GameState state = new GameState { currentRegion = "rust_jungle" };
            public readonly AgentCatalog catalog = AgentCatalog.Load();
            public readonly AgentController agent;
            public readonly WorldChemistryService world;
            public readonly LaboratoryProgressionService progression;
            public bool ready = true, advanced;
            public Rig()
            {
                state.AddVisitedRegion(state.currentRegion);
                progression = new LaboratoryProgressionService(state);
                world = new WorldChemistryService(state);
                agent = new AgentController(state, catalog, Environment);
            }
            public AgentEnvironmentFacts Environment()
            {
                float resistance = new RegionEnvironmentService(state, GameData.Regions).EffectiveToolResistance("corrosion");
                float required = GameData.Regions.regions.First(r => r.id == "rust_jungle").mining_requirements.First(c => c.id == "corrosion").minimum;
                int level = resistance + 0.0001f >= required ? 1 : 0;
                return new AgentEnvironmentFacts { region = state.currentRegion, ready = ready,
                    corrosion_active = state.currentRegion == "rust_jungle" && (level == 0 || advanced), corrosion_resistance_level = level,
                    resistance_available = state.HasLaboratoryMilestone("corrosion_coating_processed") && state.CountItem("processed_corrosion_coating") > 0 && level == 0,
                    new_high_danger = advanced };
            }
            public AgentDecision Event(string type, long time, string subject = "corrosion", AgentEvent facts = null)
            { return agent.Publish(type, subject, time, evidence: facts); }
            public void Drain() { for (int i = 0; agent.Queue.Current != null && i < 100; i++) agent.Queue.Advance(5, false); }
            public void Gather(string id)
            {
                string reason; Require(new FiniteResourceService(state, GameData.Regions).GatherRegion("rust_jungle", id, out reason), "finite gathering " + reason);
            }
        }
        static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        static string Gameplay(GameState state)
        { var clone = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); clone.agent = null; return JsonUtility.ToJson(clone); }
        static AgentEvent Capability(bool available = false, int level = 0)
        { return new AgentEvent { behavior_profile = "rust_jungle", capability = RustJungleAgentBehavior.CapabilityId, available = available, level = level }; }

        public static void HazardAndStall()
        {
            var r = new Rig { ready = false };
            Require(r.Event("REGION_ENTERED", 0, "rust_jungle").reason == "environment_state_unavailable", "unready environment spoke");
            r.ready = true;
            Require(r.Event("REGION_ENTERED", 1, "rust_jungle").reaction_id == "rust_jungle_first_entry", "entry not routed to adapter"); r.Drain();
            string before = Gameplay(r.state);
            Require(r.Event("CORROSION_EVENT", 3001).reaction_id == "rust_first_corrosion_discovery", "first corrosion missing"); r.Drain();
            Require(r.Event("CORROSION_EVENT", 6001).reason == "repeated_corrosion_suppressed", "repeat spam");
            Require(r.Event("CORROSION_EVENT", 9001).Silent && r.state.agent.rust.stall_hint_level == 1, "hint ignored 30s gate");
            Require(r.Event("CORROSION_EVENT", 30001).reaction_id == "rust_corrosion_hint_level_2", "level2 not reached"); r.Drain();
            r.Event("CORROSION_EVENT", 33001);
            Require(r.Event("CORROSION_EVENT", 36001).reaction_id == "rust_corrosion_hint_level_3" && r.state.agent.rust.stall_hint_level == 3, "level3 not reached");
            Require(Gameplay(r.state) == before, "hazard hints mutated gameplay");
            var quiet = new Rig();
            Require(quiet.Event("ENVIRONMENT_EXPOSURE_CHANGED", 10).reason == "environment_observation_recorded" && quiet.state.agent.rust.exposure_count == 0,
                "passive environment observation became hazard attempt");
            Require(quiet.Event("CORROSION_EVENT", 20, facts: new AgentEvent { corrosion_active = false }).reason == "corrosion_not_active", "inactive corrosion warned");
        }
        public static void AnalysisAndKnowledgeGuard()
        {
            var r = new Rig(); const string sample = "resource_rust_metal_residue";
            r.Gather(sample);
            string before = Gameplay(r.state);
            Require(r.Event("MATERIAL_ANALYZED", 100, sample).reason == "knowledge_not_discovered" && Gameplay(r.state) == before, "forged composition accepted");
            Require(r.world.AnalyzeRustSample(sample).success && r.state.CountItem(sample) == 1, "scan changed inventory / failed");
            before = Gameplay(r.state);
            Require(r.Event("MATERIAL_ANALYZED", 200, sample).reaction_id == "rust_material_analysis" && r.agent.Queue.Current.text.Contains("含铁"), "dynamic confirmed-iron response");
            Require(r.Event("MATERIAL_ANALYZED", 300, sample).reason == "property_already_explained" && Gameplay(r.state) == before, "analysis repeated / changed gameplay");
            var chloride = new Rig(); chloride.Gather("resource_chloride_deposit");
            Require(chloride.world.AnalyzeRustSample("resource_chloride_deposit").success, "chloride scan");
            chloride.Event("MATERIAL_ANALYZED", 100, "resource_chloride_deposit");
            Require(chloride.agent.Queue.Current.text.Contains("氯") && !chloride.agent.Queue.Current.text.Contains("含铁"), "chloride template leaked iron");
            var mechanism = new Rig(); mechanism.Event("CORROSION_EVENT", 3000); mechanism.Drain(); mechanism.Gather(sample);
            Require(mechanism.world.AnalyzeRustSample(sample).success, "mechanism scan");
            Require(mechanism.Event("MATERIAL_ANALYZED", 6000, sample).reaction_id == "rust_corrosion_mechanism_explained" &&
                mechanism.state.agent.rust.corrosion_understood && mechanism.state.agent.rust.analysis_completed, "observed corrosion not linked to confirmed scan");
            var invalid = new Rig(); before = Gameplay(invalid.state);
            Require(!invalid.world.AnalyzeRustSample(sample).success && !invalid.world.AnalyzeRustSample("invented").success && Gameplay(invalid.state) == before,
                "invalid scan changed gameplay");
            invalid.state.currentRegion = "prologue_lab";
            Require(!invalid.world.AnalyzeRustSample(sample).success, "remote scan accepted");
        }
        public static void CapabilitiesMaturityAndDanger()
        {
            var r = new Rig();
            Require(r.Event("CAPABILITY_AVAILABLE", 0, RustJungleAgentBehavior.CapabilityId, Capability(true)).reason == "capability_not_available" &&
                r.Event("CAPABILITY_ACQUIRED", 1, RustJungleAgentBehavior.CapabilityId, Capability(level: 1)).reason == "capability_not_acquired", "forged capability accepted");
            r.Event("CORROSION_EVENT", 3000); r.Drain();
            r.Gather("resource_rust_metal_residue"); r.Gather("resource_chloride_deposit");
            Require(r.world.AnalyzeRustSample("resource_rust_metal_residue").success, "analysis setup");
            r.Event("MATERIAL_ANALYZED", 6000, "resource_rust_metal_residue"); r.Drain();
            r.progression.RecordPickaxeRepaired(); r.progression.RecordCorrosionGateDiscovered(); r.progression.RecordCorrosionRawMaterials();
            r.state.currentRegion = "prologue_lab"; r.state.AddVisitedRegion("prologue_lab");
            Require(r.progression.ProcessCorrosionBase().success && r.progression.ProcessCorrosionCoating().success, "real corrosion processing");
            string before = Gameplay(r.state);
            Require(r.Event("CAPABILITY_AVAILABLE", 10000, RustJungleAgentBehavior.CapabilityId, Capability(true)).reaction_id == "rust_resistance_available" && Gameplay(r.state) == before,
                "cross-region availability not routed / Agent mutated coating"); r.Drain();
            Require(r.progression.UpgradeCorrosionPickaxe().success && r.state.laboratoryStage == 1, "real upgrade failed");
            before = Gameplay(r.state);
            Require(r.Event("CAPABILITY_ACQUIRED", 14000, RustJungleAgentBehavior.CapabilityId, Capability(level: 1)).reaction_id == "rust_resistance_acquired" &&
                r.state.agent.rust.mature && Gameplay(r.state) == before, "maturity / acquired celebration / readonly guard");
            r.state.currentRegion = "rust_jungle";
            Require(r.Event("REGION_ENTERED", 20000, "rust_jungle").reason == "rust_guidance_mature", "mature reentry spam");
            Require(r.Event("CORROSION_EVENT", 21000, facts: new AgentEvent { new_high_danger = true }).reason == "rust_guidance_mature", "invented advanced hazard");
            r.advanced = true;
            Require(r.Event("HAZARD_STATE_CHANGED", 22000, facts: new AgentEvent { new_high_danger = true }).reaction_id == "rust_advanced_corrosion_warning", "confirmed new danger suppressed by maturity");
            Require(r.Event("HAZARD_STATE_CHANGED", 23000, facts: new AgentEvent { new_high_danger = true }).reason == "cooldown", "advanced hazard cooldown");
            // Advanced danger above is a fixture contract; this slice publishes no such live variant.
        }
        public static void FailureWorldReactionAndPersistence()
        {
            var r = new Rig(); var failure = new AgentEvent { corrosion_relevant = true, behavior_profile = "rust_jungle" };
            Require(r.Event("EXPERIMENT_FAILED", 1000, facts: failure).reaction_id == "rust_failed_attempt_observation", "first failure"); r.Drain();
            r.Event("EXPERIMENT_FAILED", 2000, facts: failure);
            Require(r.Event("EXPERIMENT_FAILED", 30000, facts: failure).reaction_id == "rust_repeated_failure_hint", "repeated failure threshold");
            Require(r.Event("WORLD_REACTION_OBSERVED", 40000, "reaction_rust", new AgentEvent { observed = true }).reason == "world_reaction_not_confirmed", "forged reaction accepted");
            var recipe = ChemistryEngine.Recipe("reaction_rust");
            foreach (var q in recipe.reactants) r.state.AddItem(q.id, q.n); // Raw fixture inputs, not a player-route claim.
            Require(new ReactionService(r.state).Execute(recipe.id, new[] { "wet" }).success, "real rust reaction");
            Require(r.Event("WORLD_REACTION_OBSERVED", 41000, recipe.id, new AgentEvent { observed = true }).reaction_id == "rust_world_reaction_observed" &&
                r.state.agent.rust.failed_attempts == 0 && r.state.agent.rust.last_progress_ms == 41000, "confirmed progress not reset");
            string path = Path.GetFullPath(Path.Combine("Logs", "u17_rust_test_" + Guid.NewGuid().ToString("N"), "save.json"));
            var repository = new SaveRepository(path, "test", forceSnapshotHistory: true); string reason;
            Require(repository.Write(r.state, out reason), "rust save " + reason);
            var read = repository.Read(); Require(read.success && JsonUtility.ToJson(read.state.agent.rust) == JsonUtility.ToJson(r.state.agent.rust), "rust attention snapshot lost");
            read.state.agent.rust.mature = true;
            Require(!repository.Write(read.state, out reason) && repository.Read().success, "invalid maturity committed");
        }
    }
}
