using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class SaltMarshAttention
    {
        public int unknown_material_count, analysis_hint_level;
        public string[] known_facts = new string[0];
        public bool analysis_completed, solution_property_known, distiller_available;
        public bool distillation_used, filter_need_observed, filtration_used, separation_used;
        public bool ph_observed, neutralization_observed, precipitation_observed, mature;
        public long last_progress_ms;
    }

    // A thin, read-only adapter: all chemical identities and results must be in confirmed snapshots.
    public static class SaltMarshAgentBehavior
    {
        public const string RegionId = "salt_marsh", DeviceId = "device_industrial_distiller", CapabilityId = "DISTILLATION";
        public const string FilterId = "device_industrial_solid_liquid_filter", FilterCapabilityId = "SOLID_LIQUID_FILTRATION";
        public static bool Applies(AgentEvent e, AgentContext c)
        {
            if (e == null) return false;
            if (!string.IsNullOrEmpty(e.behavior_profile)) return e.behavior_profile == RegionId;
            return c.region == RegionId || e.region == RegionId ||
                ((e.type == "DEVICE_CRAFTED" || e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") &&
                new[] { DeviceId, CapabilityId, FilterId, FilterCapabilityId }.Contains(e.subject));
        }
        static bool Analysis(AgentEvent e) { return e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED"; }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject); }
        static string[] Facts(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        {
            var row = c.confirmed_analyses.FirstOrDefault(a => a.item_id == e.subject && p.sample_ids.Contains(a.item_id));
            return row == null ? new string[0] : row.facts;
        }
        static bool Available(AgentEvent e, AgentContext c)
        { return c.crafted_devices.Contains(DeviceId) && ((e.type == "DEVICE_CRAFTED" && e.subject == DeviceId) ||
            ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.subject == CapabilityId && e.available)); }
        static string Ph(AgentEvent e, AgentContext c)
        { var row = c.confirmed_ph.FirstOrDefault(p => p.key == e.subject); return row == null ? "" : row.value; }
        static bool Observed(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        {
            if (!e.observed || !p.phenomena.Contains(e.phenomenon)) return false;
            switch (e.phenomenon)
            {
                case "ph_measured": return Ph(e, c) != "";
                case "neutralization_completed": return c.known_chemistry.Contains("salt_marsh_acid_pool_01_neutralized");
                case "precipitate_observed": return c.known_chemistry.Contains("precipitate_observed");
                // The live bridge emits these only AFTER successful output collection, not when
                // processing starts (where the existing service already records chemistry knowledge).
                case "filtration_completed": return c.known_chemistry.Contains("filtration_completed") &&
                    new[] { "compound_precipitate_solid", "compound_neutralized_salt_solution" }.All(id => e.outputs.Contains(id) && c.owned_items.Contains(id));
                case "distillation_completed": return c.known_chemistry.Contains("distillation_completed") &&
                    new[] { "compound_distilled_water", "compound_concentrated_salt_solution" }.All(id => e.outputs.Contains(id) && c.owned_items.Contains(id));
                default: return false;
            }
        }
        public static void UpdateAttention(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            if (!Applies(e, c)) return;
            if (next.salt == null) next.salt = new SaltMarshAttention();
            var s = next.salt;
            bool progress = e.type == "REGION_ENTERED" && e.subject == RegionId && c.region == RegionId && c.environment.ready && c.environment.region == RegionId;
            if (Unknown(e, c, p)) s.unknown_material_count = s.unknown_material_count == int.MaxValue ? int.MaxValue : s.unknown_material_count + 1;
            else if (Analysis(e))
            {
                var facts = Facts(e, c, p);
                if (facts.Length > 0)
                {
                    s.analysis_completed = true; progress = true;
                    s.known_facts = s.known_facts.Concat(facts).Distinct().ToArray();
                    if (facts.Intersect(p.solution_facts).Any()) s.solution_property_known = true;
                }
            }
            else if (e.type == "WORLD_REACTION_OBSERVED" && Observed(e, c, p))
            {
                switch (e.phenomenon)
                {
                    case "ph_measured": s.ph_observed = true; break;
                    case "neutralization_completed": s.neutralization_observed = true; s.solution_property_known = true; break;
                    case "precipitate_observed": s.precipitation_observed = true; s.filter_need_observed = true; break;
                    case "filtration_completed": s.filtration_used = true; s.separation_used = true; break;
                    case "distillation_completed": s.distillation_used = true; s.separation_used = true; break;
                }
                progress = true;
            }
            else if (Available(e, c)) { s.distiller_available = true; progress = true; }
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_material_count = 0; }
            if (p.mature_conditions.Length > 0 && p.mature_conditions.All(k => k == "salt_analysis_completed" ? s.analysis_completed :
                k == "salt_solution_property_known" ? s.solution_property_known : k == "salt_separation_used" && s.separation_used)) s.mature = true;
        }
        static AgentDecision Silent(string reason) { return new AgentDecision { reason = reason, profile_id = RegionId }; }
        static AgentDecision Say(RegionAgentBehaviorProfile p, string key, string action, int priority, string reason, string[] facts = null, int cooldown = 0)
        {
            string id = p.Reaction(key);
            if (string.IsNullOrEmpty(id)) return Silent("missing_profile_reaction");
            return new AgentDecision { action = action, reaction_id = id, cooldown_key = id, profile_id = RegionId,
                priority = priority, reason = reason, confirmed_facts = facts ?? new string[0], cooldown_ms = cooldown };
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            if (!Applies(e, c)) return null;
            var before = state.salt ?? new SaltMarshAttention(); var s = next.salt;
            if (before.mature) return Silent("salt_guidance_mature");
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
            {
                var env = c.environment;
                if (c.region != RegionId || !env.ready || env.region != RegionId || !env.has_solution_availability ||
                    float.IsNaN(env.solution_availability) || float.IsInfinity(env.solution_availability) || env.solution_availability < p.minimum_solution_availability)
                    return Silent("environment_state_unavailable");
                return Say(p, "entry", p.entry_action, 40, "first_salt_marsh_observation");
            }
            if (Unknown(e, c, p))
            {
                if (!state.shown.Contains(p.Reaction("unknown_material"))) return Say(p, "unknown_material", "FOLLOW_UP", 40, "first_unknown_salt_material");
                for (int level = 3; level >= 1; level--)
                    if (s.unknown_material_count >= p.Hint("analysis_hint_level_" + level + "_threshold", level * 3) &&
                        e.time_ms >= before.last_progress_ms && e.time_ms - before.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000))
                    {
                        s.analysis_hint_level = Math.Max(s.analysis_hint_level, level);
                        return Say(p, "hint_" + level, "HINT", 20, "salt_analysis_stall_level_" + level, cooldown: 30000);
                    }
                return Silent("salt_collection_observed");
            }
            if (Analysis(e))
            {
                var facts = Facts(e, c, p);
                if (facts.Length == 0) return Silent("knowledge_not_discovered");
                var discovered = facts.Except(before.known_facts).ToArray();
                if (discovered.Length == 0) return Silent("salt_fact_already_explained");
                return discovered.Intersect(p.solution_facts).Any() ? Say(p, "solution_confirmed", "EXPLAIN", 60, "salt_solution_knowledge_confirmed", discovered) :
                    Say(p, "analysis", "FOLLOW_UP", 40, "salt_material_fact_confirmed", discovered);
            }
            if (e.type == "WORLD_REACTION_OBSERVED")
            {
                if (!e.observed) return Silent("phenomenon_not_confirmed");
                if (!p.phenomena.Contains(e.phenomenon)) return Silent("unsupported_salt_phenomenon");
                if (!Observed(e, c, p)) return Silent("phenomenon_not_confirmed");
                switch (e.phenomenon)
                {
                    case "ph_measured": var ph = Say(p, "ph", "EXPLAIN", 60, "ph_measurement_confirmed"); ph.confirmed_ph = Ph(e, c); return ph;
                    case "neutralization_completed": return Say(p, "neutralization", "EXPLAIN", 60, "neutralization_confirmed");
                    case "precipitate_observed": return state.shown.Contains(p.Reaction("precipitation")) ?
                        Say(p, "filter_need", "HINT", 20, "solid_liquid_separation_need_confirmed") : Say(p, "precipitation", "FOLLOW_UP", 40, "precipitate_confirmed");
                    case "filtration_completed": return Say(p, "filtration", "CELEBRATE", 80, "filtration_confirmed");
                    case "distillation_completed": return Say(p, "distillation", "CELEBRATE", 80, "distillation_confirmed");
                }
            }
            if (Available(e, c)) return Say(p, "capability_available", "HINT", 20, "distiller_progression_confirmed");
            return Silent("salt_exploration_silence");
        }
    }
}
