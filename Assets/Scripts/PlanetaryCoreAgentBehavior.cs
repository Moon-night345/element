using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class PlanetaryAttention
    {
        public int failure_count;
        public bool analysis_confirmed, condition_selected, category_selected, device_used, mature;
    }
    [Serializable] public sealed class PlanetaryEventFacts
    {
        public string[] condition_fields = new string[0], confirmed_hazard_fields = new string[0];
        public AgentPair[] categories = new AgentPair[0];
        public string device_id = "";
        public bool analyzed, ordinary_success, multi_system_breakthrough, large_volume_liquid, intermediate_liquid_storage_needed, energy_shortage, synthesis_energy_required;
    }
    [Serializable] public sealed class PlanetaryDecisionFacts
    {
        public string knowledge_status = "", semantic_role = "", current_problem_category = "", capability_category = "", recall_region_id = "", device_id = "", failure_reason_confirmed = "";
        public bool causality_confirmed, player_autonomy_window, player_owns_solution, full_solution_disclosed, is_reaction_device, auto_unlock;
    }
    // Read-only final-region guidance. No route solver, unlock or gameplay state reference.
    public static class PlanetaryCoreAgentBehavior
    {
        public const string RegionId = "planetary_core", TankId = "device_large_industrial_liquid_tank", ChargerId = "device_synthesis_charging_station";
        static bool Applies(AgentEvent e, AgentContext c)
        { return e != null && (string.IsNullOrEmpty(e.behavior_profile) || e.behavior_profile == RegionId) && (c.region == RegionId || e.region == RegionId || e.behavior_profile == RegionId); }
        static PlanetaryEventFacts Facts(AgentEvent e) { return e.planetary ?? new PlanetaryEventFacts(); }
        static string Phenomenon(AgentEvent e) { return string.IsNullOrEmpty(e.phenomenon) ? e.subject : e.phenomenon; }
        static bool Process(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "WORLD_REACTION_OBSERVED" && p.process_phenomena.Contains(Phenomenon(e)); }
        static bool Analysis(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED") && p.sample_ids.Contains(e.subject) && c.confirmed_analyses.Any(a => a.region_id == RegionId && a.item_id == e.subject && a.facts.Length > 0); }
        static bool Unknown(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "MATERIAL_COLLECTED" && p.sample_ids.Contains(e.subject) && !Facts(e).analyzed; }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static string Category(PlanetaryEventFacts f, RegionAgentBehaviorProfile p)
        {
            foreach (string field in p.category_fields)
            {
                var row = (f.categories ?? new AgentPair[0]).FirstOrDefault(x => x.key == field && !string.IsNullOrEmpty(x.value));
                if (row != null) return row.value.ToUpperInvariant();
            }
            return "";
        }
        static string Recall(AgentContext c, AgentCatalog cat, string category)
        { var row = cat.planetary_recall_groups.FirstOrDefault(g => g.key == category); return row == null ? "" : row.values.FirstOrDefault(c.known_regions.Contains) ?? ""; }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return; var p = cat.ForRegion(RegionId); var f = Facts(e); var s = next.planetary;
            if (Analysis(e, c, p)) s.analysis_confirmed = true;
            if (Process(e, p))
            {
                s.condition_selected |= (f.condition_fields ?? new string[0]).Intersect(p.condition_fields).Any();
                s.category_selected |= Category(f, p).Length > 0; s.device_used |= !string.IsNullOrEmpty(f.device_id);
            }
            if (Failure(e, p) && s.failure_count < int.MaxValue) s.failure_count++;
            s.mature |= s.analysis_confirmed && s.condition_selected && s.category_selected && s.device_used;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return null; var p = cat.ForRegion(RegionId); var f = Facts(e);
            bool process = Process(e, p), breakthrough = process && f.multi_system_breakthrough, failure = Failure(e, p), unknown = Unknown(e, p);
            if (state.planetary.mature && !unknown && !failure && !breakthrough) return Silent("final_guidance_mature");
            string key, reason, action = "FOLLOW_UP"; int priority = 40;
            var facts = new PlanetaryDecisionFacts(); string[] confirmed = new string[0];
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
            {
                bool hazard = e.danger || (f.confirmed_hazard_fields ?? new string[0]).Intersect(p.hazard_fields).Any() || (c.environment.ready && c.environment.region == RegionId && (c.environment.danger || c.environment.hazard));
                key = hazard ? "danger" : "entry"; reason = hazard ? "authoritative_final_hazard" : "quiet_final_entry";
                if (hazard) { action = "WARN"; priority = 100; facts.knowledge_status = "OBSERVED_FACT"; facts.semantic_role = "ENVIRONMENT_CONDITION"; }
                else { facts.semantic_role = "REGION_OBSERVATION"; facts.player_autonomy_window = true; }
            }
            else if (unknown) return Silent("unknown_identity_requires_analysis");
            else if (Analysis(e, c, p))
            {
                key = "analysis"; reason = "authoritative_analysis_only"; action = "EXPLAIN"; priority = 60; facts.knowledge_status = "OBSERVED_FACT";
                confirmed = c.confirmed_analyses.First(a => a.region_id == RegionId && a.item_id == e.subject).facts.ToArray();
            }
            else if (failure)
            {
                int count = next.planetary.failure_count;
                if (count <= p.Hint("first_failure_silent_threshold", 1)) return Silent("first_failure_observe_only");
                facts.failure_reason_confirmed = e.failure_reason;
                if (count == p.Hint("observe_threshold", 2)) { key = "failure_observe"; reason = "repeated_result_observed"; }
                else if (count == p.Hint("condition_threshold", 3)) { key = "failure_condition"; reason = "current_condition_check"; action = "HINT"; priority = 20; }
                else
                {
                    key = "failure_device"; reason = "current_capability_check"; action = "HINT"; priority = 20;
                    var mapping = cat.planetary_failure_categories.FirstOrDefault(v => v.key == e.failure_reason);
                    facts.capability_category = mapping == null ? "PROCESS" : mapping.value; facts.recall_region_id = Recall(c, cat, facts.capability_category);
                }
            }
            else if (process && (f.large_volume_liquid || f.intermediate_liquid_storage_needed || Phenomenon(e) == "bulk_liquid_storage_needed"))
            {
                facts.knowledge_status = "OBSERVED_FACT"; facts.capability_category = "LIQUID_STORAGE_BUFFERING"; facts.device_id = TankId; facts.semantic_role = "STORAGE_BUFFERING";
                bool visible = c.lab_stage >= 3 && c.known_regions.Contains(RegionId) && (c.crafted_devices.Contains(TankId) || c.liquid_tank_progression_available);
                bool hint = visible && state.planetary.failure_count >= p.Hint("device_capability_threshold", 4);
                key = hint ? "liquid_tank" : "storage_need"; reason = hint ? "liquid_storage_device_capability" : "storage_capability_need_only";
                if (hint) { action = "HINT"; priority = 20; }
            }
            else if (process && (f.energy_shortage || f.synthesis_energy_required))
            {
                if (!c.crafted_devices.Contains(ChargerId)) return Silent("charger_not_available");
                key = "charger"; reason = "formal_energy_need"; action = "HINT"; priority = 20; facts.knowledge_status = "OBSERVED_FACT"; facts.device_id = ChargerId; facts.semantic_role = "ENERGY_RESTORE";
            }
            else if (breakthrough) { key = "breakthrough"; reason = "player_route_verified"; action = "CELEBRATE"; priority = 80; facts.knowledge_status = "OBSERVED_FACT"; facts.player_owns_solution = true; }
            else if (process)
            {
                if (f.ordinary_success) return Silent("ordinary_success_restraint");
                string category = Category(f, p); if (category.Length == 0) return Silent("no_authoritative_next_category");
                facts.knowledge_status = "OBSERVED_FACT"; facts.current_problem_category = category;
                string recall = Recall(c, cat, category);
                if (recall.Length > 0 && !state.shown.Contains(p.Reaction("recall"))) { key = "recall"; reason = "single_relevant_recall"; facts.recall_region_id = recall; }
                else { key = "category"; reason = "current_problem_category_only"; }
            }
            else return Silent("player_autonomy_window");
            string id = p.Reaction(key); return new AgentDecision { action = action, priority = priority, reaction_id = id, cooldown_key = id, profile_id = RegionId, reason = reason, planetary = facts, confirmed_facts = confirmed };
        }
        static AgentDecision Silent(string reason) { return new AgentDecision { reason = reason, profile_id = RegionId }; }
        public static AgentEvent StorageEvent(ElementalStarSea.Data.DeviceGameplayEvent e)
        {
            if (e.device_id != TankId || !(e.items ?? new ElementalStarSea.Data.RecipeQuantity[0]).Any(q => q.n > 0)) return null;
            bool stored = e.event_type == "device_storage_deposited", reused = e.event_type == "device_storage_withdrawn";
            if (!stored && !reused) return null;
            return new AgentEvent { behavior_profile = RegionId, phenomenon = stored ? "bulk_liquid_stored" : "bulk_liquid_reused", observed = true,
                planetary = new PlanetaryEventFacts { device_id = TankId, ordinary_success = true, categories = new[] { new AgentPair { key = "process_category", value = "LIQUID_STORAGE_BUFFERING" } } } };
        }
    }
}
