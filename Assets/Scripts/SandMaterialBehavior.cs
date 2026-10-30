using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class SandAttention
    {
        public int unknown_count;
        public int analysis_hint_level, failure_count, failure_hint_level;
        public long last_progress_ms;
        public bool analysis_completed, hypothesis_pending, cross_region_link, mature;
    }
    public static class SandMaterialBehavior
    {
        static bool Applies(AgentEvent e, AgentContext c)
        { return e.behavior_profile == "sand_desert" || (string.IsNullOrEmpty(e.behavior_profile) && (e.region == "sand_desert" || c.region == "sand_desert")); }
        static bool Crystal(AgentContext c, AgentCatalog catalog)
        { return CrossRegionRecall.HasRegionFact(c, catalog, "crystal_tundra", "carbonate") || CrossRegionRecall.HasRegionFact(c, catalog, "crystal_tundra", "calcium"); }
        static bool Analysis(AgentEvent e) { return e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED"; }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p)
        { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == "sand_desert" && p.failure_reasons.Contains(e.failure_reason); }
        static int AnalysisLevel(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog catalog)
        {
            var p = catalog.ForRegion("sand_desert"); var s = next.sand;
            if ((Crystal(c, catalog) && !s.hypothesis_pending) || !next.shown.Contains(p.Reaction("unknown_material")) ||
                e.time_ms < s.last_progress_ms || e.time_ms - s.last_progress_ms < p.Hint("stall_min_duration_msec", 30000)) return 0;
            for (int level = 3; level >= 1; level--) if (s.unknown_count >= p.Hint("analysis_hint_level_" + level + "_threshold", level * 3)) return level;
            return 0;
        }
        static string[] Facts(AgentEvent e, AgentContext c)
        { var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == "sand_desert" && a.item_id == e.subject); return row == null ? new string[0] : row.facts; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return;
            var s = next.sand;
            if (e.type == "MATERIAL_COLLECTED" && e.analysis_available && catalog.ForRegion("sand_desert").sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject))
            {
                s.unknown_count = s.unknown_count == int.MaxValue ? int.MaxValue : s.unknown_count + 1;
                if (!s.mature) s.analysis_hint_level = Math.Max(s.analysis_hint_level, AnalysisLevel(e, c, next, catalog));
            }
            var p = catalog.ForRegion("sand_desert");
            if (Failure(e, p))
            {
                s.failure_count = s.failure_count == int.MaxValue ? int.MaxValue : s.failure_count + 1;
                for (int level = s.mature ? 0 : 3; level >= 1; level--) if (s.failure_count >= p.Hint("failure_hint_level_" + level + "_threshold", level * 2 + 1))
                { s.failure_hint_level = Math.Max(s.failure_hint_level, level); break; }
            }
            bool progress = e.type == "REGION_ENTERED" && e.subject == "sand_desert";
            if (Analysis(e) && Facts(e, c).Length > 0) { s.analysis_completed = true; progress = true; }
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_count = 0; s.failure_count = 0; }
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog catalog)
        {
            if (!Applies(e, c)) return null;
            if (state.sand.mature) return new AgentDecision { reason = "sand_guidance_mature", profile_id = "sand_desert" };
            var p = catalog.ForRegion("sand_desert"); string key, action; int priority, cooldown = 0; string[] facts = new string[0];
            if (e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) && !c.known_materials.Contains(e.subject))
            {
                if (Crystal(c, catalog) && !state.sand.hypothesis_pending)
                { next.sand.hypothesis_pending = true; key = "cross_region_hypothesis"; }
                else if (!state.shown.Contains(p.Reaction("unknown_material"))) key = "unknown_material";
                else
                {
                    int level = AnalysisLevel(e, c, next, catalog);
                    if (level == 0) return new AgentDecision { reason = "sand_collection_observed", profile_id = "sand_desert" };
                    key = "hint_" + level; cooldown = 30000;
                }
                action = cooldown == 0 ? "FOLLOW_UP" : "HINT"; priority = cooldown == 0 ? 40 : 20;
            }
            else if (Analysis(e))
            {
                facts = Facts(e, c);
                if (facts.Length == 0) return new AgentDecision { reason = "knowledge_not_discovered", profile_id = "sand_desert" };
                action = "EXPLAIN"; priority = 60;
                if (facts.Intersect(p.carbonate_facts).Any())
                {
                    if (Crystal(c, catalog)) { next.sand.cross_region_link = true; next.sand.hypothesis_pending = false; next.sand.mature = next.sand.analysis_completed; key = "crystal_recall_confirmed"; }
                    else if (c.known_regions.Contains("ember_rift") && c.known_chemistry.Contains("thermal:resource_carbonate_rock"))
                    { key = "ember_thermal_hypothesis"; action = "HINT"; priority = 20; }
                    else key = "carbonate_confirmed";
                }
                else if (facts.Intersect(p.carbon_facts).Any()) key = "carbon_confirmed";
                else if (facts.Intersect(p.calcium_facts).Any()) key = "calcium_confirmed";
                else { key = "analysis"; action = "FOLLOW_UP"; priority = 40; }
            }
            else if (Failure(e, p))
            {
                if (next.sand.failure_count == 1) key = "failure_first";
                else
                {
                    int level = 0;
                    for (int rung = 3; rung >= 1; rung--) if (next.sand.failure_count >= p.Hint("failure_hint_level_" + rung + "_threshold", rung * 2 + 1)) { level = rung; break; }
                    if (level == 0) return new AgentDecision { reason = "sand_failure_observed_without_escalation", profile_id = "sand_desert" };
                    key = "failure_hint_" + level; cooldown = 30000;
                }
                action = cooldown == 0 ? "FOLLOW_UP" : "HINT"; priority = cooldown == 0 ? 40 : 20;
            }
            else if (e.type == "REGION_ENTERED") return null; // Existing entry/recall adapter owns entry.
            else return new AgentDecision { reason = "sand_exploration_silence", profile_id = "sand_desert" };
            string id = p.Reaction(key);
            return new AgentDecision { action = action, priority = priority, reaction_id = id, cooldown_key = id, cooldown_ms = cooldown, profile_id = "sand_desert", confirmed_facts = facts,
                reason = key == "cross_region_hypothesis" ? "sand_unconfirmed_similarity_hypothesis" : "sand_confirmed_context" };
        }
    }
}
