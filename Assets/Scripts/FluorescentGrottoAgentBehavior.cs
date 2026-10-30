using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class GrottoAttention
    {
        public int unknown_count, analysis_hint_level, gas_count, gas_hint_level, failure_count, failure_hint_level;
        public string[] known_facts = new string[0];
        public bool oxygen_observed, analysis_completed, organic_completed, gas_observed, storage_understood, mature;
        public long last_progress_ms;
    }
    // Presentation only: reads detached context and committed gameplay events.
    public static class FluorescentGrottoAgentBehavior
    {
        public const string RegionId = "fluorescent_grotto", TankId = "device_high_pressure_gas_tank";
        static bool Tank(AgentEvent e, AgentContext c)
        {
            return ((e.type == "DEVICE_CRAFTED" && e.subject == TankId && c.crafted_devices.Contains(TankId)) ||
                ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.subject == "GAS_HANDLING" &&
                    (c.gas_storage_progression_available || c.crafted_devices.Contains(TankId))));
        }
        static bool Applies(AgentEvent e, AgentContext c)
        {
            if (!string.IsNullOrEmpty(e.behavior_profile) && e.behavior_profile != RegionId) return false;
            return e.behavior_profile == RegionId || e.region == RegionId || c.region == RegionId || Tank(e, c);
        }
        static bool Environment(AgentContext c)
        { return c.region == RegionId && c.environment.ready && c.environment.region == RegionId && c.environment.has_oxygen && !float.IsNaN(c.environment.oxygen) && !float.IsInfinity(c.environment.oxygen); }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject); }
        static string[] Facts(AgentEvent e, AgentContext c)
        {
            if (e.type != "MATERIAL_ANALYZED" && e.type != "COMPOSITION_DISCOVERED") return new string[0];
            var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject);
            return row == null ? new string[0] : row.facts;
        }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p)
        { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static bool Phenomenon(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && e.observed; }
        static bool Condenser(AgentContext c) { return c.known_chemistry.Contains("condenser_used"); }
        static int Increment(int n) { return n == int.MaxValue ? n : n + 1; }
        static int Rung(int count, string prefix, RegionAgentBehaviorProfile p, int step, int offset = 0)
        { for (int level = 3; level >= 1; level--) if (count >= p.Hint(prefix + level + "_threshold", level * step + offset)) return level; return 0; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return;
            var p = catalog.ForRegion(RegionId); var s = next.grotto; bool progress = false;
            if (e.type == "REGION_ENTERED" && e.subject == RegionId && Environment(c)) { s.oxygen_observed = true; progress = true; }
            else if (Unknown(e, c, p)) s.unknown_count = Increment(s.unknown_count);
            else if (Facts(e, c).Length > 0) { s.analysis_completed = true; s.known_facts = s.known_facts.Union(Facts(e, c)).ToArray(); progress = true; }
            else if (Phenomenon(e) && e.phenomenon == "organic_process_completed")
            { s.organic_completed = true; s.gas_observed = !string.IsNullOrEmpty(e.gas_output); s.gas_count = Increment(s.gas_count); progress = true; }
            else if ((Phenomenon(e) && e.phenomenon == "gas_stored") || Tank(e, c)) { s.storage_understood = true; progress = true; }
            else if (Failure(e, p)) s.failure_count = Increment(s.failure_count);
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_count = 0; s.failure_count = 0; }
            if (s.oxygen_observed && s.analysis_completed && s.organic_completed && s.gas_observed && s.storage_understood) s.mature = true;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return null;
            var p = catalog.ForRegion(RegionId); var s = next.grotto;
            Func<string, AgentDecision> quiet = reason => new AgentDecision { reason = reason, profile_id = RegionId };
            if (state.grotto.mature) return quiet("grotto_guidance_mature");
            string key, action = "FOLLOW_UP"; int priority = 40, cooldown = 0; string[] facts = new string[0];
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
            {
                if (!Environment(c)) return quiet("environment_state_unavailable");
                key = c.known_regions.Contains("ember_rift") && c.carbon_fuel_burned > 0 ? "ember_oxygen_recall" : "entry";
            }
            else if (Unknown(e, c, p))
            {
                if (!state.shown.Contains(p.Reaction("unknown_material"))) key = "unknown_material";
                else
                {
                    int level = e.time_ms >= state.grotto.last_progress_ms && e.time_ms - state.grotto.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000) ? Rung(s.unknown_count, "analysis_hint_level_", p, 3) : 0;
                    if (level == 0) return quiet("grotto_collection_observed");
                    s.analysis_hint_level = Math.Max(s.analysis_hint_level, level); key = "hint_" + level; action = "HINT"; priority = 20; cooldown = 30000;
                }
            }
            else if (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED")
            {
                facts = Facts(e, c); if (facts.Length == 0) return quiet("knowledge_not_discovered");
                facts = facts.Except(state.grotto.known_facts).ToArray(); if (facts.Length == 0) return quiet("grotto_fact_already_explained");
                if (facts.Intersect(p.fermentable_facts).Any()) key = "fermentable_confirmed";
                else if (facts.Intersect(p.organic_facts).Any()) key = "organic_confirmed";
                else key = "analysis";
                if (key != "analysis") { action = "EXPLAIN"; priority = 60; }
            }
            else if (e.type == "WORLD_REACTION_OBSERVED")
            {
                if (!e.observed) return quiet("phenomenon_not_confirmed");
                if (e.phenomenon == "gas_stored") { key = "gas_stored"; action = "CELEBRATE"; priority = 80; }
                else if (e.phenomenon == "organic_process_completed")
                {
                    if (s.gas_count <= 1)
                    {
                        key = "gas_produced";
                        var group = catalog.fact_groups.First(g => g.key == "carbon_dioxide").values;
                        if (e.gas_output == "compound_co2" && (c.known_materials.Intersect(group).Any() || c.confirmed_analyses.Any(a => a.facts.Intersect(group).Any()))) facts = new[] { "compound_co2" };
                    }
                    else
                    {
                        action = "HINT"; priority = 20;
                        if (Condenser(c) && c.gas_storage_progression_available) key = "condenser_separation";
                        else { s.gas_hint_level = c.gas_storage_progression_available && s.gas_count >= p.Hint("gas_hint_level_3_threshold", 3) ? 3 : 2; key = "gas_hint_" + s.gas_hint_level; cooldown = 30000; }
                    }
                }
                else return quiet("unsupported_grotto_phenomenon");
            }
            else if (Tank(e, c)) { key = Condenser(c) ? "condenser_separation" : "gas_hint_3"; action = "HINT"; priority = 20; }
            else if (Failure(e, p))
            {
                if (s.failure_count == 1) key = "failure_first";
                else
                {
                    int level = Rung(s.failure_count, "failure_hint_level_", p, 2);
                    if (level == 0) return quiet("grotto_failure_without_escalation");
                    s.failure_hint_level = Math.Max(s.failure_hint_level, level); key = "failure_hint_" + level; action = "HINT"; priority = 20; cooldown = 30000;
                }
            }
            else return quiet("grotto_exploration_silence");
            string id = p.Reaction(key);
            return new AgentDecision { reaction_id = id, cooldown_key = id, cooldown_ms = cooldown, action = action, priority = priority, profile_id = RegionId, confirmed_facts = facts, reason = "grotto_confirmed_context" };
        }
    }
}
