using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class MagneticAttention
    {
        public string[] known_facts = new string[0];
        public int unknown_count, analysis_hint_level, failure_count, failure_hint_level;
        public bool electricity_observed, analysis_completed, material_condition_linked;
        public bool electrolysis_observed, mature;
        public long last_progress_ms;
    }
    public static class MagneticMaterialBehavior
    {
        static bool Applies(AgentEvent e, AgentContext c) { return string.IsNullOrEmpty(e.behavior_profile) ? c.region == "magnetic_plateau" || e.region == "magnetic_plateau" : e.behavior_profile == "magnetic_plateau"; }
        static bool Electricity(AgentContext c, AgentCatalog cat) { var v = c.environment; return v.ready && v.region == "magnetic_plateau" && v.has_electrical_activity && !float.IsInfinity(v.electrical_activity) && !float.IsNaN(v.electrical_activity) && v.electrical_activity >= cat.ForRegion("magnetic_plateau").minimum_electrical_activity; }
        static string[] Facts(AgentEvent e, AgentContext c) { var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == "magnetic_plateau" && a.item_id == e.subject); return row == null ? new string[0] : row.facts; }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p) { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && !c.known_materials.Contains(e.subject) && p.sample_ids.Contains(e.subject); }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == "magnetic_plateau" && p.failure_reasons.Contains(e.failure_reason); }
        static int Increment(int n) { return n == int.MaxValue ? n : n + 1; }
        static bool Process(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && e.observed && (e.phenomenon == "electrolysis_completed" || e.phenomenon == "silver_catalytic_material_refined"); }
        static int Rung(int count, RegionAgentBehaviorProfile p, string prefix, int step) { for (int i = 3; i >= 1; i--) if (count >= p.Hint(prefix + i + "_threshold", i * step)) return i; return 0; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return; var s = next.magnetic;
            var p = cat.ForRegion("magnetic_plateau");
            s.electricity_observed |= Electricity(c, cat);
            if (Unknown(e, c, p)) s.unknown_count = Increment(s.unknown_count);
            if (Failure(e, p)) s.failure_count = Increment(s.failure_count);
            if (e.type == "REGION_ENTERED" && e.subject == "magnetic_plateau") { s.last_progress_ms = e.time_ms; s.unknown_count = 0; s.failure_count = 0; }
            if (Process(e))
            {
                s.electricity_observed = true; s.electrolysis_observed = true; s.material_condition_linked = true;
                s.last_progress_ms = e.time_ms; s.unknown_count = 0; s.failure_count = 0;
                s.mature |= s.analysis_completed; return;
            }
            if (e.type != "MATERIAL_ANALYZED" && e.type != "COMPOSITION_DISCOVERED") return;
            var f = Facts(e, c); if (f.Length == 0) return;
            s.analysis_completed = true; s.known_facts = s.known_facts.Union(f).ToArray(); s.last_progress_ms = e.time_ms;
            s.unknown_count = 0; s.failure_count = 0;
            if (f.Intersect(new[] { "conductive_mineral", "redox_active", "nitrate_electrolyte_context" }).Any()) s.material_condition_linked = s.electricity_observed;
            s.mature |= s.electricity_observed && s.analysis_completed && s.electrolysis_observed && s.material_condition_linked;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return null;
            var p = cat.ForRegion("magnetic_plateau"); var s = next.magnetic;
            if (state.magnetic.mature) return new AgentDecision { reason = "magnetic_guidance_mature", profile_id = "magnetic_plateau" };
            if (Process(e) || (e.type == "DEVICE_CRAFTED" && e.subject == "device_industrial_electrolysis_cell" && c.crafted_devices.Contains(e.subject)))
            {
                string topic = "electrolyzer_reuse", processAction = "FOLLOW_UP"; int priority = 40;
                if (Process(e))
                {
                    bool gas = e.outputs.Intersect(new[] { "compound_h2", "compound_o2", "compound_cl2", "compound_nh3" }).Any();
                    if (c.known_regions.Contains("abandoned_mine") && c.known_chemistry.Any(k => k.StartsWith("displacement:", StringComparison.Ordinal) || k.StartsWith("no_reaction:", StringComparison.Ordinal)) && !state.shown.Contains(p.Reaction("mine_recall"))) topic = "mine_recall";
                    else if (gas && c.crafted_devices.Contains("device_high_pressure_gas_tank") && !state.shown.Contains(p.Reaction("gas_tank_reuse"))) { topic = "gas_tank_reuse"; processAction = "HINT"; priority = 20; }
                    else if (gas && e.condensable && c.crafted_devices.Contains("device_industrial_condenser")) { topic = "condenser_reuse"; processAction = "HINT"; priority = 20; }
                    else if (!state.magnetic.electrolysis_observed) { topic = "electrolysis_completed"; processAction = "CELEBRATE"; priority = 80; }
                    else return new AgentDecision { reason = "repeated_electrolysis", profile_id = "magnetic_plateau" };
                }
                string reaction = p.Reaction(topic);
                return new AgentDecision { action = processAction, priority = priority, reaction_id = reaction, cooldown_key = reaction, profile_id = "magnetic_plateau", reason = "formal_controlled_electrolysis" };
            }
            if (Unknown(e, c, p) || Failure(e, p))
            {
                bool unknown = Unknown(e, c, p); string topic; int cooldown = 0;
                string ladderAction = "FOLLOW_UP";
                if (unknown && !state.shown.Contains(p.Reaction("unknown_material"))) topic = "unknown_material";
                else if (!unknown && s.failure_count == 1) topic = "failure_first";
                else
                {
                    int level = unknown ? (e.time_ms >= state.magnetic.last_progress_ms && e.time_ms - state.magnetic.last_progress_ms >= p.Hint("stall_min_duration_msec", 30000) ? Rung(s.unknown_count, p, "analysis_hint_level_", 3) : 0) : Rung(s.failure_count, p, "failure_hint_level_", 2);
                    if (level == 0) return new AgentDecision { reason = unknown ? "magnetic_collection_observed" : "magnetic_failure_without_escalation" };
                    if (unknown) s.analysis_hint_level = Math.Max(s.analysis_hint_level, level); else s.failure_hint_level = Math.Max(s.failure_hint_level, level);
                    topic = unknown && level == 1 ? "analyzer_reuse" : "failure_hint_" + level; ladderAction = "HINT"; cooldown = 30000;
                }
                string reaction = p.Reaction(topic);
                return new AgentDecision { action = ladderAction, priority = ladderAction == "HINT" ? 20 : 40, reaction_id = reaction, cooldown_key = reaction, cooldown_ms = cooldown, profile_id = "magnetic_plateau", reason = "magnetic_stall_or_formal_failure" };
            }
            if (e.type == "EXPERIMENT_FAILED") return new AgentDecision { reason = "magnetic_failure_not_formal" };
            if (e.type != "MATERIAL_ANALYZED" && e.type != "COMPOSITION_DISCOVERED") return null;
            var f = Facts(e, c); if (f.Length == 0) return new AgentDecision { reason = "magnetic_identity_not_authoritative" };
            f = f.Except(state.magnetic.known_facts).ToArray(); if (f.Length == 0) return new AgentDecision { reason = "magnetic_analysis_already_explained" };
            string key = "analysis", action = "EXPLAIN";
            if (f.Contains("redox_active")) key = "redox_property";
            else if (f.Intersect(new[] { "conductive_mineral", "nitrate_electrolyte_context" }).Any() && (state.magnetic.electricity_observed || Electricity(c, cat)))
            {
                key = c.crafted_devices.Contains("device_industrial_electrolysis_cell") ? "electrolyzer_reuse" : c.lab_stage >= 2 && c.known_regions.Contains("magnetic_plateau") ? "electrolyzer_guidance" : "condition_hypothesis";
                if (key != "condition_hypothesis") action = "HINT";
            }
            string id = cat.ForRegion("magnetic_plateau").Reaction(key);
            return new AgentDecision { action = action, priority = action == "HINT" ? 20 : 60, reaction_id = id, cooldown_key = id, profile_id = "magnetic_plateau", confirmed_facts = f, reason = "magnetic_confirmed_property_not_process" };
        }
    }
}
