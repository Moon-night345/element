using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class MantleAttention { public bool heat_observed, sulfur_observed, thermal_processed, industrial_chain_observed, crystallization_observed, mature; public int failure_count; }
    // U27 entry and confirmed analysis; no process completion is inferred from heat.
    public static class MantleFlowAgentBehavior
    {
        static bool Applies(AgentEvent e, AgentContext c) { return string.IsNullOrEmpty(e.behavior_profile) ? c.region == "mantle_flows" || e.region == "mantle_flows" : e.behavior_profile == "mantle_flows"; }
        static bool Heat(AgentEvent e, AgentContext c, AgentCatalog cat) { var env = c.environment; float t = e.has_temperature ? e.temperature : env.temperature; return (e.has_temperature || (env.ready && env.region == "mantle_flows" && env.has_temperature)) && !float.IsNaN(t) && !float.IsInfinity(t) && t >= cat.ForRegion("mantle_flows").minimum_heat_temperature; }
        static bool Thermal(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && e.phenomenon == "extreme_heat_processed" && e.controlled_extreme_heat; }
        static bool Need(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && (e.industrial_crystallizer_needed || e.phenomenon == "loaded_solution_processed"); }
        static bool Crystal(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && e.phenomenon == "industrial_crystallization_completed"; }
        static bool Failure(AgentEvent e, AgentCatalog cat) { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == "mantle_flows" && cat.ForRegion("mantle_flows").failure_reasons.Contains(e.failure_reason); }
        static bool Analysis(AgentEvent e, AgentCatalog cat) { return (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED") && cat.ForRegion("mantle_flows").sample_ids.Contains(e.subject); }
        static string[] Facts(AgentEvent e, AgentContext c) { var row = c.confirmed_analyses.FirstOrDefault(r => r.region_id == "mantle_flows" && r.item_id == e.subject); return row == null ? new string[0] : row.facts; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return;
            next.mantle.heat_observed |= Heat(e, c, cat);
            if (Analysis(e, cat)) next.mantle.sulfur_observed |= Facts(e, c).Intersect(cat.ForRegion("mantle_flows").sulfur_facts).Any();
            var s = next.mantle;
            if (Thermal(e)) { s.heat_observed = true; s.thermal_processed = true; s.industrial_chain_observed = true; s.sulfur_observed |= e.process_input == "element_s" || e.process_input == "ion_so4"; }
            if (Need(e)) s.industrial_chain_observed = true;
            if (Crystal(e)) { s.industrial_chain_observed = true; s.crystallization_observed = true; }
            if (Failure(e, cat)) s.failure_count = s.failure_count == int.MaxValue ? s.failure_count : s.failure_count + 1;
            s.mature |= s.heat_observed && s.thermal_processed && s.sulfur_observed && s.industrial_chain_observed && s.crystallization_observed;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentCatalog cat)
        {
            if (!Applies(e, c)) return null; var p = cat.ForRegion("mantle_flows"); string key, action = "FOLLOW_UP"; int priority = 40; string[] facts = new string[0];
            if (state.mantle.mature) return new AgentDecision { reason = "mantle_guidance_mature", profile_id = "mantle_flows" };
            if (e.type == "REGION_ENTERED" && e.subject == "mantle_flows") { key = "entry"; if (Heat(e, c, cat)) { action = "WARN"; priority = 100; } }
            else if (e.type == "MATERIAL_COLLECTED" && !c.known_materials.Contains(e.subject) && p.sample_ids.Contains(e.subject)) key = "unknown_material";
            else if (Analysis(e, cat))
            {
                facts = Facts(e, c); if (facts.Length == 0) return new AgentDecision { reason = "mantle_analysis_not_authoritative", profile_id = "mantle_flows" };
                key = "analysis"; action = "EXPLAIN"; priority = 60;
            }
            else if (Thermal(e))
            {
                if (c.known_regions.Contains("ember_rift") && c.known_chemistry.Contains("thermal:resource_carbonate_rock") && !state.shown.Contains(p.Reaction("ember_recall"))) key = "ember_recall";
                else if (new[] { "radiation_belt", "ember_rift", "rust_jungle" }.Any(region => CrossRegionRecall.HasRegionFact(c, cat, region, "oxidation")) && !state.shown.Contains(p.Reaction("oxidation_recall"))) key = "oxidation_recall";
                else if (e.processing_type == "bulk_reaction" && c.crafted_devices.Contains("device_large_industrial_reactor")) { key = "reactor"; action = "HINT"; priority = 20; }
                else if (e.processing_type == "thermal" && c.crafted_devices.Contains("device_small_chemical_furnace") && !e.industrial_scale_required) { key = "furnace"; action = "HINT"; priority = 20; }
                else if ((e.output_phase == "gas" || e.output_phase == "vapor") && e.condensable && c.crafted_devices.Contains("device_industrial_condenser")) { key = "condenser"; action = "HINT"; priority = 20; }
                else if (!string.IsNullOrEmpty(e.process_output) && e.gas_treatment_required && c.crafted_devices.Contains("device_industrial_absorption_tower")) { key = "absorption"; action = "HINT"; priority = 20; }
                else { key = "thermal"; action = "EXPLAIN"; priority = 60; }
            }
            else if (Need(e))
            {
                if (c.known_regions.Contains("geode_abyss") && c.known_chemistry.Any(k => k.StartsWith("natural_crystallization:", StringComparison.Ordinal)) && !state.shown.Contains(p.Reaction("geode_recall"))) key = "geode_recall";
                else if (c.lab_stage >= 3 && c.known_regions.Contains("mantle_flows") && c.known_chemistry.Contains("industrial_crystallizer_need_discovered")) { key = "crystallizer"; action = "HINT"; priority = 20; }
                else return new AgentDecision { reason = "crystallizer_progression_not_available", profile_id = "mantle_flows" };
            }
            else if (Crystal(e)) { key = "crystallized"; action = "CELEBRATE"; priority = 80; }
            else if (Failure(e, cat))
            {
                int count = state.mantle.failure_count == int.MaxValue ? int.MaxValue : state.mantle.failure_count + 1;
                key = count <= 1 ? "failure" : count >= 6 ? "failure_3" : count >= 4 ? "failure_2" : "failure_1";
                if (count > 1) { action = "HINT"; priority = 20; }
            }
            else return null;
            string id = p.Reaction(key); return new AgentDecision { action = action, priority = priority, reaction_id = id, cooldown_key = id, profile_id = "mantle_flows", confirmed_facts = facts, reason = "mantle_confirmed_environment_or_analysis" };
        }
    }
}
