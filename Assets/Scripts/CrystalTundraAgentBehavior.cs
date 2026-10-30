using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class CrystalTundraAttention
    {
        public int unknown_material_count, analysis_hint_level;
        public string[] known_facts = new string[0];
        public bool analysis_completed, carbonate_known, analyzer_available, analyzer_used;
        public bool temperature_observed, crystallization_observed, mature;
        public long last_progress_ms;
    }

    // Interprets detached confirmed facts only; no gameplay services or state writes.
    public static class CrystalTundraAgentBehavior
    {
        public const string RegionId = "crystal_tundra", DeviceId = "device_multi_chemical_analyzer", CapabilityId = "PRECISE_ANALYSIS";
        public static bool Applies(AgentEvent e, AgentContext c)
        {
            if (e == null) return false;
            if (!string.IsNullOrEmpty(e.behavior_profile)) return e.behavior_profile == RegionId;
            return c.region == RegionId || e.region == RegionId ||
                (e.type == "DEVICE_CRAFTED" && e.subject == DeviceId) ||
                ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.subject == CapabilityId);
        }
        static bool Analysis(AgentEvent e) { return e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED"; }
        static string[] Facts(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        {
            var row = c.confirmed_analyses.FirstOrDefault(a => a.item_id == e.subject && p.sample_ids.Contains(a.item_id));
            return row == null ? new string[0] : row.facts;
        }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject); }
        static bool AnalyzerAvailable(AgentEvent e, AgentContext c)
        {
            return c.crafted_devices.Contains(DeviceId) && ((e.type == "DEVICE_CRAFTED" && e.subject == DeviceId) ||
                ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.subject == CapabilityId && e.available));
        }
        static bool Phenomenon(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        {
            // No live publisher for these source gameplay phenomena yet. Require both event and a
            // committed chemistry snapshot contract; an arbitrary observed=true must not reveal them.
            return e.observed && p.phenomena.Contains(e.phenomenon) && c.known_chemistry.Contains(e.phenomenon + ":" + e.subject);
        }
        public static void UpdateAttention(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            if (!Applies(e, c)) return;
            if (next.crystal == null) next.crystal = new CrystalTundraAttention();
            var s = next.crystal;
            bool progress = e.type == "REGION_ENTERED" && e.subject == RegionId && c.region == RegionId &&
                c.environment.ready && c.environment.region == RegionId;
            if (Unknown(e, c, p)) s.unknown_material_count = s.unknown_material_count == int.MaxValue ? int.MaxValue : s.unknown_material_count + 1;
            else if (Analysis(e))
            {
                var facts = Facts(e, c, p);
                if (facts.Length > 0)
                {
                    s.analysis_completed = true; progress = true;
                    s.known_facts = s.known_facts.Concat(facts).Distinct().ToArray();
                    if (facts.Intersect(p.carbonate_facts).Any()) s.carbonate_known = true;
                    if (e.analyzer_used && c.analyzer_samples.Contains(e.subject) && c.crafted_devices.Contains(DeviceId)) s.analyzer_used = true;
                }
            }
            else if (e.type == "WORLD_REACTION_OBSERVED" && Phenomenon(e, c, p))
            {
                if (e.phenomenon == "temperature_state_changed") s.temperature_observed = true;
                if (e.phenomenon == "crystallization_state_changed") s.crystallization_observed = true;
                progress = true;
            }
            else if (AnalyzerAvailable(e, c)) { s.analyzer_available = true; progress = true; }
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_material_count = 0; }
            if (p.mature_conditions.Length > 0 && p.mature_conditions.All(k =>
                k == "crystal_analysis_completed" ? s.analysis_completed : k == "crystal_analyzer_used" ? s.analyzer_used :
                k == "crystal_carbonate_known" && s.carbonate_known)) s.mature = true;
        }
        static AgentDecision Silent(string reason) { return new AgentDecision { reason = reason, profile_id = RegionId }; }
        static AgentDecision Say(RegionAgentBehaviorProfile p, string key, string action, int priority, string reason, string[] facts = null, bool once = true, int cooldown = 0)
        {
            var id = p.Reaction(key);
            if (string.IsNullOrEmpty(id)) return Silent("missing_profile_reaction");
            return new AgentDecision { action = action, reaction_id = id, cooldown_key = id, priority = priority,
                reason = reason, profile_id = RegionId, confirmed_facts = facts ?? new string[0], once = once, cooldown_ms = cooldown };
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            if (!Applies(e, c)) return null;
            var before = state.crystal ?? new CrystalTundraAttention(); var s = next.crystal;
            if (before.mature) return Silent("crystal_guidance_mature");
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
            {
                var env = c.environment;
                if (c.region != RegionId || !env.ready || env.region != RegionId || !env.has_temperature ||
                    float.IsNaN(env.temperature) || float.IsInfinity(env.temperature) || env.temperature > p.maximum_cold_temperature)
                    return Silent("environment_state_unavailable");
                return Say(p, "entry", p.entry_action, 40, "first_crystal_tundra_observation");
            }
            if (Unknown(e, c, p))
            {
                if (!state.shown.Contains(p.Reaction("unknown_material"))) return Say(p, "unknown_material", "FOLLOW_UP", 40, "first_unknown_crystal_material");
                for (int level = 3; level >= 1; level--)
                    if (s.unknown_material_count >= p.Hint("analysis_hint_level_" + level + "_threshold", level * 3) &&
                        e.time_ms >= before.last_progress_ms && e.time_ms - before.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000))
                    {
                        s.analysis_hint_level = Math.Max(s.analysis_hint_level, level);
                        return Say(p, "hint_" + level, "HINT", 20, "crystal_analysis_stall_level_" + level, once: true, cooldown: 30000);
                    }
                return Silent("crystal_collection_observed");
            }
            if (Analysis(e))
            {
                var facts = Facts(e, c, p);
                if (facts.Length == 0) return Silent("knowledge_not_discovered");
                var discovered = facts.Except(before.known_facts).ToArray();
                if (discovered.Length == 0) return Silent("crystal_fact_already_explained");
                return discovered.Intersect(p.carbonate_facts).Any() ?
                    Say(p, "carbonate_confirmed", "EXPLAIN", 60, "carbonate_knowledge_confirmed", discovered) :
                    Say(p, "analysis", "FOLLOW_UP", 40, "crystal_material_fact_confirmed", discovered);
            }
            if (e.type == "WORLD_REACTION_OBSERVED")
            {
                if (!e.observed) return Silent("phenomenon_not_confirmed");
                if (!p.phenomena.Contains(e.phenomenon)) return Silent("unsupported_crystal_phenomenon");
                if (!Phenomenon(e, c, p)) return Silent("phenomenon_not_confirmed");
                return e.phenomenon == "crystallization_state_changed" ? Say(p, "crystallization", "CELEBRATE", 80, "crystallization_confirmed") :
                    Say(p, "temperature", "EXPLAIN", 60, "temperature_change_confirmed");
            }
            if (AnalyzerAvailable(e, c)) return Say(p, "capability_available", "HINT", 20, "analyzer_progression_confirmed");
            return Silent("crystal_exploration_silence");
        }
    }
}
