using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class MineAttention
    {
        public int unknown_count, analysis_hint_level, failure_count, failure_hint_level;
        public string[] known_facts = new string[0], analyzed_ids = new string[0], displacement_ids = new string[0];
        public bool analysis_completed, metallurgy_observed, material_difference, displacement_observed, precipitation_observed, mature;
        public long last_progress_ms;
    }
    public static class AbandonedMineAgentBehavior
    {
        public const string RegionId = "abandoned_mine", Furnace = "device_small_chemical_furnace", Reactor = "device_large_industrial_reactor", Filter = "device_industrial_solid_liquid_filter";
        static bool Applies(AgentEvent e, AgentContext c)
        { return string.IsNullOrEmpty(e.behavior_profile) ? e.region == RegionId || c.region == RegionId : e.behavior_profile == RegionId; }
        static bool Analysis(AgentEvent e, RegionAgentBehaviorProfile p) { return (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED") && p.sample_ids.Contains(e.subject); }
        static AgentAnalysis Row(AgentEvent e, AgentContext c) { return c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject); }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject); }
        static bool Metal(AgentEvent e) { return e.type == "MATERIAL_RECOVERED" && e.source_event == "metal_recovered"; }
        static bool Displacement(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && e.phenomenon == "metal_displacement"; }
        static bool Precipitation(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && (e.phenomenon == "precipitation_test" || e.phenomenon == "first_industrial_precipitation_completed"); }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static int Increment(int n) { return n == int.MaxValue ? n : n + 1; }
        static int Rung(int count, RegionAgentBehaviorProfile p, string prefix, int step)
        { for (int level = 3; level >= 1; level--) if (count >= p.Hint(prefix + level + "_threshold", level * step)) return level; return 0; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return;
            var s = next.mine; var p = catalog.ForRegion(RegionId); bool progress = e.type == "REGION_ENTERED" && e.subject == RegionId;
            if (Unknown(e, c, p)) s.unknown_count = Increment(s.unknown_count);
            else if (Analysis(e, p))
            {
                var row = Row(e, c);
                if (row != null && row.facts.Length > 0)
                { s.analysis_completed = true; s.known_facts = s.known_facts.Union(row.facts).ToArray(); s.analyzed_ids = s.analyzed_ids.Union(new[] { e.subject }).ToArray(); s.material_difference = s.analyzed_ids.Length >= 2; progress = true; }
            }
            else if (Metal(e)) { s.metallurgy_observed = true; progress = true; }
            else if (Displacement(e))
            {
                s.displacement_observed = true;
                string id = string.IsNullOrEmpty(e.discovery) ? ">" + e.failure_reason : e.discovery;
                s.displacement_ids = s.displacement_ids.Union(new[] { id }).ToArray(); s.material_difference |= s.displacement_ids.Length >= 2; progress = true;
            }
            else if (Precipitation(e)) { s.precipitation_observed = true; progress = true; }
            else if (Failure(e, p)) s.failure_count = Increment(s.failure_count);
            if (progress) { s.unknown_count = 0; s.failure_count = 0; s.last_progress_ms = e.time_ms; }
            if (s.analysis_completed && s.metallurgy_observed && s.material_difference) s.mature = true;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return null;
            var p = catalog.ForRegion(RegionId); var s = next.mine;
            Func<string, AgentDecision> quiet = reason => new AgentDecision { reason = reason, profile_id = RegionId };
            if (state.mine.mature) return quiet("mine_guidance_mature");
            string key, action = "FOLLOW_UP"; int priority = 40, cooldown = 0; string[] facts = new string[0];
            if (e.type == "REGION_ENTERED" && e.subject == RegionId) key = "entry";
            else if (Unknown(e, c, p))
            {
                if (!state.shown.Contains(p.Reaction("unknown_ore"))) key = "unknown_ore";
                else
                {
                    int level = e.time_ms >= state.mine.last_progress_ms && e.time_ms - state.mine.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000) ? Rung(s.unknown_count, p, "analysis_hint_level_", 3) : 0;
                    if (level == 0) return quiet("mine_collection_observed");
                    s.analysis_hint_level = Math.Max(s.analysis_hint_level, level); key = "hint_" + level; action = "HINT"; priority = 20; cooldown = 30000;
                }
            }
            else if (Analysis(e, p))
            {
                var row = Row(e, c); if (row == null || row.facts.Length == 0) return quiet("mine_identity_not_authoritative");
                facts = row.facts.Except(state.mine.known_facts).ToArray(); if (facts.Length == 0) return quiet("mine_analysis_already_explained");
                action = "EXPLAIN"; priority = 60;
                if (!state.shown.Contains(p.Reaction("identity"))) key = "identity";
                else if (row.processing_device == Reactor && c.lab_stage >= 2 && c.known_regions.Contains(RegionId)) { key = "reactor_need"; action = "HINT"; priority = 20; }
                else if (row.processing_device == Furnace && (c.crafted_devices.Contains(Furnace) || c.lab_stage >= 1)) { key = "furnace_reuse"; action = "HINT"; priority = 20; }
                else key = string.IsNullOrEmpty(row.processing_device) ? "analysis" : "property_process";
            }
            else if (Metal(e)) { key = "metallurgy"; action = "CELEBRATE"; priority = 80; }
            else if (Displacement(e))
            {
                if (!e.result_success && e.failure_reason == "no_reaction") key = "no_displacement";
                else if (state.mine.displacement_ids.Length > 0) { key = "displacement_compare"; action = "EXPLAIN"; priority = 60; }
                else key = "displacement";
            }
            else if (Precipitation(e))
            {
                Func<string, bool> unshown = k => !state.shown.Contains(p.Reaction(k));
                if (c.known_regions.Contains("salt_marsh") && c.known_chemistry.Contains("precipitate_observed") && unshown("salt_recall")) key = "salt_recall";
                else if (c.known_regions.Contains("geode_abyss") && c.known_chemistry.Any(k => k.StartsWith("natural_crystallization:", StringComparison.Ordinal)) && unshown("precipitation_guard")) { key = "precipitation_guard"; action = "EXPLAIN"; priority = 60; }
                else if ((c.crafted_devices.Contains(Filter) || c.known_chemistry.Contains("filtration_completed")) && unshown("filter_reuse")) { key = "filter_reuse"; action = "HINT"; priority = 20; }
                else key = "precipitation";
            }
            else if (Failure(e, p))
            {
                if (s.failure_count == 1) key = "failure_first";
                else { int level = Rung(s.failure_count, p, "failure_hint_level_", 2); if (level == 0) return quiet("mine_failure_without_escalation"); s.failure_hint_level = Math.Max(s.failure_hint_level, level); key = "failure_hint_" + level; action = "HINT"; priority = 20; cooldown = 30000; }
            }
            else return quiet("mine_exploration_silence");
            string id = p.Reaction(key);
            return new AgentDecision { action = action, reaction_id = id, priority = priority, cooldown_key = id, cooldown_ms = cooldown, confirmed_facts = facts, profile_id = RegionId, reason = "mine_confirmed_context" };
        }
    }
}
