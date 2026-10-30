using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class RustJungleAttention
    {
        public int exposure_count, stall_hint_level, failed_attempts;
        public string[] known_properties = new string[0];
        public bool analysis_completed, corrosion_understood, resistance_acquired, mature;
        public long last_progress_ms;
    }
    // Detached facts supplied by the existing gameplay/runtime owner, never derived from a region name.
    public sealed class AgentEnvironmentFacts
    {
        public string region;
        public bool ready, corrosion_active, new_high_danger;
        public int corrosion_resistance_level;
        public bool resistance_available;
        public bool has_temperature;
        public float temperature;
        public bool has_solution_availability;
        public float solution_availability;
        public bool has_oxygen;
        public bool has_electrical_activity;
        public float electrical_activity;
        public bool has_radiation;
        public float radiation;
        public float oxygen, light;
        public bool has_pressure;
        public float pressure;
        public string pressure_state = "";
        public bool danger, hazard;
    }
    public static class RustJungleAgentBehavior
    {
        public const string RegionId = "rust_jungle", CapabilityId = "CORROSION_RESISTANCE";
        public static bool Applies(AgentEvent e, AgentContext context)
        {
            if (e != null && !string.IsNullOrEmpty(e.behavior_profile) && e.behavior_profile != RegionId) return false;
            return e != null && (context.region == RegionId || e.region == RegionId || e.behavior_profile == RegionId ||
                ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.capability == CapabilityId));
        }
        static string[] Facts(AgentEvent e, AgentContext context)
        {
            var known = context.confirmed_analyses.FirstOrDefault(a => a.item_id == e.subject);
            return known == null ? new string[0] : known.facts;
        }
        static bool Acquired(AgentEvent e, AgentContext context)
        { return e.capability == CapabilityId && e.level >= 1 && context.environment.corrosion_resistance_level >= 1; }
        static bool Available(AgentEvent e, AgentContext context)
        { return e.capability == CapabilityId && e.available && context.environment.resistance_available; }
        static bool Observed(AgentEvent e, AgentContext context)
        { return e.observed && context.known_reactions.Contains(e.subject); }
        static bool HighDanger(AgentEvent e, AgentContext context)
        { return e.new_high_danger && context.environment.new_high_danger; }
        static bool Mature(RegionAgentBehaviorProfile profile, RustJungleAttention s)
        {
            return profile.mature_conditions.Length > 0 && profile.mature_conditions.All(c =>
                c == "rust_corrosion_understood" ? s.corrosion_understood : c == "rust_analysis_completed" ? s.analysis_completed :
                c == "rust_resistance_acquired" && s.resistance_acquired);
        }
        public static void UpdateAttention(AgentEvent e, AgentContext context, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile profile)
        {
            if (!Applies(e, context)) return;
            if (next.rust == null) next.rust = new RustJungleAttention();
            var s = next.rust;
            if (profile.hazard_events.Contains(e.type) && e.type != "EXPERIMENT_FAILED" && e.corrosion_active && context.environment.corrosion_active)
                s.exposure_count = s.exposure_count == int.MaxValue ? int.MaxValue : s.exposure_count + 1;
            if (e.type == "EXPERIMENT_FAILED" && e.corrosion_relevant)
                s.failed_attempts = s.failed_attempts == int.MaxValue ? int.MaxValue : s.failed_attempts + 1;
            bool material = profile.material_events.Contains(e.type);
            var facts = material ? Facts(e, context) : new string[0];
            if (facts.Length > 0)
            { s.analysis_completed = true; s.known_properties = s.known_properties.Concat(facts).Distinct().ToArray(); }
            if (e.type == "CAPABILITY_ACQUIRED" && Acquired(e, context)) s.resistance_acquired = true;
            bool progress = e.type == "REGION_ENTERED" || (material && facts.Length > 0) ||
                (e.type == "WORLD_REACTION_OBSERVED" && Observed(e, context)) ||
                (e.type == "CAPABILITY_AVAILABLE" && Available(e, context)) || (e.type == "CAPABILITY_ACQUIRED" && Acquired(e, context));
            if (progress || (e.hazard_reduced && context.environment.corrosion_resistance_level >= 1))
            { s.last_progress_ms = e.time_ms; s.exposure_count = 0; s.failed_attempts = 0; }
            if (Mature(profile, s)) s.mature = true;
        }
        static AgentDecision Silent(string reason) { return new AgentDecision { reason = reason, profile_id = RegionId }; }
        static AgentDecision Say(RegionAgentBehaviorProfile p, string key, string action, int priority, string reason,
            bool once = true, int cooldown = 0, string[] facts = null)
        {
            string id = p.Reaction(key);
            if (id == "") return Silent("missing_profile_reaction");
            return new AgentDecision { action = action, reaction_id = id, cooldown_key = id, priority = priority, reason = reason,
                once = once, cooldown_ms = cooldown, profile_id = RegionId, confirmed_facts = facts ?? new string[0] };
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext context, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            if (!Applies(e, context)) return null;
            var before = state.rust ?? new RustJungleAttention();
            var after = next.rust;
            bool highDanger = HighDanger(e, context);
            bool stalled = e.time_ms >= before.last_progress_ms && e.time_ms - before.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000);
            if (before.mature && !highDanger) return Silent("rust_guidance_mature");
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
                return !context.environment.ready || context.environment.region != RegionId ? Silent("environment_state_unavailable") :
                    Say(p, "entry", p.entry_action, 40, "first_rust_jungle_observation");
            if (e.type == "ENVIRONMENT_EXPOSURE_CHANGED") return Silent("environment_observation_recorded");
            if (p.hazard_events.Contains(e.type) && e.type != "EXPERIMENT_FAILED")
            {
                if (!e.corrosion_active || !context.environment.corrosion_active) return Silent("corrosion_not_active");
                if (Math.Max(context.environment.corrosion_resistance_level, before.resistance_acquired ? 1 : 0) >= 1 && !highDanger)
                    return Silent("post_resistance_normal_corrosion");
                if (highDanger) return Say(p, "advanced_hazard", "WARN", 100, "new_corrosion_danger", false, 30000);
                if (!state.shown.Contains(p.Reaction("first_hazard")))
                { after.stall_hint_level = Math.Max(1, after.stall_hint_level); return Say(p, "first_hazard", "HINT", 20, "first_confirmed_corrosion"); }
                if (after.exposure_count >= p.Hint("exposure_level_3_threshold", 6) && stalled)
                { after.stall_hint_level = 3; return Say(p, "hazard_hint_3", "HINT", 20, "rust_corrosion_stall_level_3"); }
                if (after.exposure_count >= p.Hint("exposure_level_2_threshold", 3) && stalled)
                { after.stall_hint_level = 2; return Say(p, "hazard_hint_2", "HINT", 20, "rust_corrosion_stall_level_2"); }
                return Silent("repeated_corrosion_suppressed");
            }
            if (p.material_events.Contains(e.type))
            {
                var facts = Facts(e, context);
                if (facts.Length == 0) return Silent("knowledge_not_discovered");
                var added = facts.Except(before.known_properties).ToArray();
                if (added.Length == 0) return Silent("property_already_explained");
                if (state.shown.Contains(p.Reaction("first_hazard")) && !before.corrosion_understood)
                { after.corrosion_understood = true; return Say(p, "hazard_explained", "EXPLAIN", 60, "corrosion_mechanism_confirmed", facts: added); }
                return Say(p, "material_analysis", "EXPLAIN", 60, "rust_material_fact_confirmed", facts: added);
            }
            if (e.type == "WORLD_REACTION_OBSERVED")
            {
                if (!Observed(e, context)) return Silent("world_reaction_not_confirmed");
                if (e.related_hazard && state.shown.Contains(p.Reaction("first_hazard"))) return Silent("corrosion_reaction_already_observed");
                return Say(p, "world_reaction", "FOLLOW_UP", 40, "confirmed_world_reaction");
            }
            if (e.type == "CAPABILITY_AVAILABLE") return Available(e, context) ?
                Say(p, "capability_available", "HINT", 20, "confirmed_capability_available") : Silent("capability_not_available");
            if (e.type == "CAPABILITY_ACQUIRED")
            {
                if (!Acquired(e, context)) return Silent("capability_not_acquired");
                after.resistance_acquired = true; if (Mature(p, after)) after.mature = true;
                return Say(p, "capability_acquired", "CELEBRATE", 80, "confirmed_resistance_acquired");
            }
            if (e.type == "EXPERIMENT_FAILED" && e.corrosion_relevant)
                return after.failed_attempts >= p.Hint("failure_threshold", 3) && stalled ?
                    Say(p, "repeated_failure", "HINT", 20, "repeated_informative_failure") : Say(p, "failed_attempt", "FOLLOW_UP", 40, "informative_failure");
            return Silent("rust_exploration_silence");
        }
    }
}
