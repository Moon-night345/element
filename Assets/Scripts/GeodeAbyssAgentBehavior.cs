using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class GeodeAttention
    {
        public int unknown_count, analysis_hint_level, failure_count, failure_hint_level;
        public string[] known_facts = new string[0];
        public bool crystal_observed, analysis_completed, crystallization_observed, condition_compared, purity_observed, mature;
        public long last_progress_ms;
    }
    public static class GeodeAbyssAgentBehavior
    {
        public const string RegionId = "geode_abyss";
        static bool Applies(AgentEvent e, AgentContext c)
        { return string.IsNullOrEmpty(e.behavior_profile) ? e.region == RegionId || c.region == RegionId : e.behavior_profile == RegionId; }
        static bool Analysis(AgentEvent e) { return e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED"; }
        static string[] Facts(AgentEvent e, AgentContext c)
        { var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject); return row == null ? new string[0] : row.facts; }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject); }
        static bool Crystallization(AgentEvent e)
        { return e.type == "WORLD_REACTION_OBSERVED" && e.observed && e.phenomenon == "natural_crystallization_completed"; }
        static bool Purity(AgentEvent e) { return Analysis(e) && e.has_purity && !float.IsNaN(e.purity) && !float.IsInfinity(e.purity); }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p)
        { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static int Increment(int n) { return n == int.MaxValue ? n : n + 1; }
        static int Rung(int count, RegionAgentBehaviorProfile p, string prefix, int step)
        { for (int level = 3; level >= 1; level--) if (count >= p.Hint(prefix + level + "_threshold", level * step)) return level; return 0; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return;
            var s = next.geode; var p = catalog.ForRegion(RegionId); bool progress = false;
            if (e.type == "REGION_ENTERED" && e.subject == RegionId) { s.crystal_observed = true; progress = true; }
            else if (Unknown(e, c, p)) s.unknown_count = Increment(s.unknown_count);
            else if (Analysis(e))
            {
                var facts = Facts(e, c);
                if (facts.Length > 0) { s.analysis_completed = true; s.known_facts = s.known_facts.Union(facts).ToArray(); s.crystal_observed |= facts.Intersect(p.crystal_facts).Any(); progress = true; }
                if (Purity(e)) { s.purity_observed = true; progress = true; }
            }
            else if (Crystallization(e))
            { s.crystallization_observed = true; s.crystal_observed = true; s.condition_compared = e.has_concentration && !float.IsNaN(e.concentration) && !float.IsInfinity(e.concentration); progress = true; }
            else if (Failure(e, p)) s.failure_count = Increment(s.failure_count);
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_count = 0; s.failure_count = 0; }
            if (s.crystal_observed && s.analysis_completed && s.crystallization_observed && s.condition_compared) s.mature = true;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return null;
            var p = catalog.ForRegion(RegionId); var s = next.geode;
            Func<string, AgentDecision> quiet = reason => new AgentDecision { reason = reason, profile_id = RegionId };
            if (state.geode.mature) return quiet("geode_guidance_mature");
            string key, action = "FOLLOW_UP"; int priority = 40, cooldown = 0; string[] facts = new string[0];
            if (e.type == "REGION_ENTERED" && e.subject == RegionId) key = "entry";
            else if (Unknown(e, c, p))
            {
                if (!state.shown.Contains(p.Reaction("unknown_material"))) key = "unknown_material";
                else
                {
                    int level = e.time_ms >= state.geode.last_progress_ms && e.time_ms - state.geode.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000) ? Rung(s.unknown_count, p, "analysis_hint_level_", 3) : 0;
                    if (level == 0) return quiet("geode_collection_observed");
                    s.analysis_hint_level = Math.Max(s.analysis_hint_level, level); key = "hint_" + level; action = "HINT"; priority = 20; cooldown = 30000;
                }
            }
            else if (Analysis(e))
            {
                if (Purity(e)) { key = "purity"; action = "CELEBRATE"; priority = 80; }
                else
                {
                    facts = Facts(e, c); if (facts.Length == 0) return quiet("knowledge_not_discovered");
                    facts = facts.Except(state.geode.known_facts).ToArray(); if (facts.Length == 0) return quiet("geode_fact_already_explained");
                    key = facts.Intersect(p.solution_facts).Any() ? "solution_confirmed" : facts.Intersect(p.crystal_facts).Any() ? "crystal_confirmed" : "analysis";
                    if (key != "analysis") { action = "EXPLAIN"; priority = 60; }
                }
            }
            else if (Crystallization(e))
            {
                Func<string, bool> unshown = k => !state.shown.Contains(p.Reaction(k));
                if (c.known_regions.Contains("crystal_tundra") && c.known_chemistry.Contains("crystal_tundra_crystallization_observed") && unshown("tundra_recall")) key = "tundra_recall";
                else if (unshown("crystallization")) key = "crystallization";
                else if (c.known_regions.Contains("salt_marsh") && c.known_chemistry.Contains("precipitate_observed") && unshown("precipitation_separation")) { key = "precipitation_separation"; action = "EXPLAIN"; priority = 60; }
                else if (c.known_chemistry.Contains("condenser_used") && unshown("condenser_separation")) { key = "condenser_separation"; action = "EXPLAIN"; priority = 60; }
                else if (c.known_chemistry.Contains("industrial_crystallizer_need_discovered") && unshown("crystallizer_foreshadow")) { key = "crystallizer_foreshadow"; action = "HINT"; priority = 20; }
                else if (unshown("condition_hypothesis")) { key = "condition_hypothesis"; action = "HINT"; priority = 20; }
                else return quiet("repeated_geode_crystallization");
            }
            else if (Failure(e, p))
            {
                if (s.failure_count == 1) key = "failure_first";
                else
                {
                    int level = Rung(s.failure_count, p, "failure_hint_level_", 2); if (level == 0) return quiet("geode_failure_without_escalation");
                    s.failure_hint_level = Math.Max(s.failure_hint_level, level); key = "failure_hint_" + level; action = "HINT"; priority = 20; cooldown = 30000;
                }
            }
            else return quiet("geode_exploration_silence");
            string id = p.Reaction(key);
            return new AgentDecision { action = action, reaction_id = id, priority = priority, cooldown_key = id, cooldown_ms = cooldown, confirmed_facts = facts, profile_id = RegionId, reason = "geode_confirmed_context" };
        }
    }
}
