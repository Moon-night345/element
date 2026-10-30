using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class RadiationAttention
    {
        public int unknown_count, failure_count;
        public bool contamination_analyzed, oxidation_observed;
        public bool gas_handled, purification_verified, mature;
    }
    // Read-only source profile adapter; gas field presence preserves source branch precedence.
    public static class RadiationBeltAgentBehavior
    {
        static bool Applies(AgentEvent e, AgentContext c) { return string.IsNullOrEmpty(e.behavior_profile) ? e.region == "radiation_belt" || c.region == "radiation_belt" : e.behavior_profile == "radiation_belt"; }
        static string[] Facts(AgentEvent e, AgentContext c) { var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == "radiation_belt" && a.item_id == e.subject); return row == null ? new string[0] : row.facts; }
        static bool Analysis(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p) { return (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED") && (e.has_contamination_record || e.has_contaminated_identity || Facts(e, c).Intersect(p.contamination_facts).Any()); }
        static bool Oxidation(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p) { return (e.type == "MATERIAL_PROPERTY_DISCOVERED" && Facts(e, c).Intersect(p.oxidation_properties).Any()) || (e.type == "WORLD_REACTION_OBSERVED" && p.oxidation_phenomena.Contains(e.phenomenon)); }
        static bool Gas(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && (e.has_gas || !string.IsNullOrEmpty(e.gas_output)); }
        static bool Purification(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "WORLD_REACTION_OBSERVED" && p.purity_phenomena.Contains(e.phenomenon); }
        static bool Absorption(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "WORLD_REACTION_OBSERVED" && p.absorption_phenomena.Contains(e.phenomenon); }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p) { return e.type == "MATERIAL_COLLECTED" && !c.known_materials.Contains(e.subject) && p.sample_ids.Contains(e.subject); }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return; var s = next.radiation; var p = cat.ForRegion("radiation_belt");
            if (Unknown(e, c, p)) s.unknown_count = s.unknown_count == int.MaxValue ? s.unknown_count : s.unknown_count + 1;
            if (Analysis(e, c, p)) s.contamination_analyzed = true;
            if (Oxidation(e, c, p)) s.oxidation_observed = true;
            if (Gas(e)) s.gas_handled = true;
            if (Purification(e, p)) s.purification_verified = true;
            s.mature |= s.contamination_analyzed && s.oxidation_observed && s.gas_handled && s.purification_verified;
            if (e.type == "EXPERIMENT_FAILED") s.failure_count = s.failure_count == int.MaxValue ? s.failure_count : s.failure_count + 1;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentCatalog cat)
        {
            if (!Applies(e, c)) return null; var p = cat.ForRegion("radiation_belt"); string key, action = "FOLLOW_UP"; int priority = 40;
            if (state.radiation.mature) return new AgentDecision { reason = "radiation_guidance_mature", profile_id = "radiation_belt" };
            if (e.type == "REGION_ENTERED" && e.subject == "radiation_belt")
            {
                key = "entry"; var env = c.environment;
                if (e.danger || (env.ready && env.region == "radiation_belt" && env.has_radiation && !float.IsNaN(env.radiation) && !float.IsInfinity(env.radiation) && env.radiation > 0f)) { action = "WARN"; priority = 100; }
            }
            else if (Unknown(e, c, p)) key = "unknown_material";
            else if (Analysis(e, c, p)) { key = "contamination"; action = "EXPLAIN"; priority = 60; }
            else if (Oxidation(e, c, p)) { key = "oxidation"; action = "EXPLAIN"; priority = 60; }
            else if (Gas(e)) { key = c.crafted_devices.Contains("device_high_pressure_gas_tank") ? "gas_tank" : "gas"; if (key == "gas_tank") { action = "HINT"; priority = 20; } }
            else if (Absorption(e, p)) { bool tower = c.crafted_devices.Contains("device_industrial_absorption_tower"); key = tower ? "absorption" : "tower"; action = tower ? "CELEBRATE" : "HINT"; priority = tower ? 80 : 20; }
            else if (Purification(e, p)) { key = "purified"; action = "CELEBRATE"; priority = 80; }
            else if (e.type == "EXPERIMENT_FAILED") key = "failure";
            else if (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED" || e.type == "MATERIAL_PROPERTY_DISCOVERED") return new AgentDecision { reason = "radiation_fact_not_confirmed", profile_id = "radiation_belt" };
            else return null;
            string id = p.Reaction(key);
            return new AgentDecision { action = action, priority = priority, reaction_id = id, cooldown_key = id, profile_id = "radiation_belt", reason = "radiation_authoritative_observation" };
        }
    }
}
