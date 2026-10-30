using System;
using System.Linq;
namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class CoreShellAttention
    {
        public bool pressure_observed, multi_condition_observed, catalysis_observed, device12_observed, multi_step_observed, mature;
        public int failure_count;
    }
    // Presence and value are distinct: a present false catalyst_consumed still belongs to the matrix.
    [Serializable] public sealed class CorePressureFact
    {
        public string field = "", text = "";
        public bool flag, has_number;
        public float number;
    }
    [Serializable] public sealed class CoreShellEventFacts
    {
        public CorePressureFact[] pressure_facts = new CorePressureFact[0];
        public bool has_pressure, has_pressure_condition, has_temperature_condition, has_catalyst_consumed;
        public bool formal_catalyst_confirmed, catalyst_consumed, pressure_required, catalyst_required, analyzed;
        public bool has_processing_stage, has_intermediate_product, has_reaction_step_completed;
        public string catalyst_id = "", device_id = "", temperature_condition = "";
        public string processing_stage = "", intermediate_product = "";
    }
    [Serializable] public sealed class CoreShellDecisionFacts
    {
        public string knowledge_status = "", semantic_role = "", environment_pressure = "", missing_condition_confirmed = "";
        public bool causality_confirmed, device_condition_satisfied, environment_pressure_satisfies_device;
        public bool multi_condition_confirmed, catalyst_confirmed, catalyst_consumed, identity_confirmed, full_chain_disclosed, ordinary_reactor_obsolete;
        public string device_id = "";
    }
    // Read-only source adapter. Contract events do not manufacture missing gameplay publishers.
    public static class CoreShellAgentBehavior
    {
        public const string RegionId = "core_shell";
        // Only an actual collected output is completion. Fields come from enforced recipe contracts.
        // No failure or multi-step publication is fabricated to make attention mature.
        public static AgentEvent DeviceDelivery(ElementalStarSea.Data.DeviceGameplayEvent e, ElementalStarSea.Data.ProcessingRecipesDatabase recipes)
        {
            if (e.event_type != "device_output_collected" || e.device_id != "device_high_pressure_catalytic_reactor") return null;
            bool conversion = e.recipe_id == "recipe_phase4b_device12_co2_h2_conversion";
            bool ammonia = e.recipe_id == "recipe_phase4b_high_pressure_nh3";
            if (!conversion && !ammonia) return null;
            var recipe = recipes.recipes.FirstOrDefault(r => r.id == e.recipe_id && r.device_id == e.device_id);
            if (recipe == null) return null;
            var pressure = (recipe.required_conditions ?? new ElementalStarSea.Data.ConditionValue[0]).FirstOrDefault(v => v.id == "pressure");
            if (pressure == null) return null;
            var f = new CoreShellEventFacts { has_pressure = true, has_catalyst_consumed = true, catalyst_consumed = false,
                pressure_facts = new[] { new CorePressureFact { field = "pressure", text = pressure.value } } };
            if (conversion)
            {
                var temp = recipe.required_conditions.FirstOrDefault(v => v.id == "temperature_condition");
                if (temp == null || !(recipe.process_inputs ?? new ElementalStarSea.Data.RecipeQuantity[0]).Any(q => q.id == "material_silver_catalytic" && q.n > 0)) return null;
                f.has_temperature_condition = true; f.temperature_condition = temp.value; f.catalyst_id = "material_silver_catalytic"; f.device_id = e.device_id;
            }
            else
            {
                var catalyst = recipe.required_conditions.FirstOrDefault(v => v.id == "catalyst"); if (catalyst == null) return null;
                f.catalyst_id = catalyst.value; // NH3 has no formal temperature or source device12 observation.
            }
            return new AgentEvent { behavior_profile = RegionId, phenomenon = conversion ? "high_pressure_catalytic_conversion_completed" : "high_pressure_reaction_completed", observed = true, core = f };
        }
        static bool Applies(AgentEvent e, AgentContext c)
        {
            // Preserve explicit producer ownership across regions, as other Unity adapters do.
            if (e == null || (!string.IsNullOrEmpty(e.behavior_profile) && e.behavior_profile != RegionId)) return false;
            return c.region == RegionId || e.region == RegionId || e.behavior_profile == RegionId;
        }
        static string Phenomenon(AgentEvent e) { return string.IsNullOrEmpty(e.phenomenon) ? e.subject : e.phenomenon; }
        static bool Pressure(AgentEvent e) { return e.type == "WORLD_REACTION_OBSERVED" && new[] { "pressure_state_changed", "high_pressure_condition" }.Contains(Phenomenon(e)); }
        static bool Success(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "WORLD_REACTION_OBSERVED" && p.pressure_success_phenomena.Contains(Phenomenon(e)); }
        static bool Catalyst(CoreShellEventFacts f, RegionAgentBehaviorProfile p) { return f.formal_catalyst_confirmed || f.catalyst_id == p.formal_catalyst; }
        static bool Matrix(AgentEvent e, CoreShellEventFacts f, RegionAgentBehaviorProfile p)
        { return (f.has_pressure || f.has_pressure_condition || f.pressure_facts.Any(v => v.field == "pressure")) && (f.has_temperature_condition || e.has_temperature) && (Catalyst(f, p) || f.has_catalyst_consumed); }
        static bool MultiStep(AgentEvent e, CoreShellEventFacts f) { return e.type == "WORLD_REACTION_OBSERVED" && (f.has_processing_stage || f.has_intermediate_product || f.has_reaction_step_completed); }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p) { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static bool Analysis(AgentEvent e, RegionAgentBehaviorProfile p) { return (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED") && p.sample_ids.Contains(e.subject); }
        static string FormalPressure(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        {
            var f = e.core ?? new CoreShellEventFacts();
            foreach (var field in p.pressure_fields)
            {
                var v = f.pressure_facts.FirstOrDefault(x => x.field == field);
                if (v == null) continue;
                if (!string.IsNullOrEmpty(v.text)) return v.text;
                if (v.flag || (v.has_number && !float.IsNaN(v.number) && !float.IsInfinity(v.number) && v.number > 0f)) return "PRESENT";
            }
            var env = c.environment;
            if (!env.ready || env.region != RegionId) return "";
            if (!string.IsNullOrEmpty(env.pressure_state)) return env.pressure_state;
            return env.has_pressure && !float.IsNaN(env.pressure) && !float.IsInfinity(env.pressure) && env.pressure > 0f ? "PRESENT" : "";
        }
        public static void Update(AgentEvent e, AgentContext c, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return; var p = cat.ForRegion(RegionId); var f = e.core ?? new CoreShellEventFacts(); var s = next.core_shell;
            if (Pressure(e)) s.pressure_observed = true;
            if (Success(e, p))
            {
                s.pressure_observed = true;
                // Deliberate assignment, matching source: success evidence can replace prior observations.
                s.multi_condition_observed = Matrix(e, f, p); s.catalysis_observed = Catalyst(f, p);
                s.device12_observed = f.device_id == "device_high_pressure_catalytic_reactor";
            }
            if (MultiStep(e, f)) s.multi_step_observed = true;
            if (Failure(e, p) && s.failure_count < int.MaxValue) s.failure_count++;
            s.mature |= s.pressure_observed && s.multi_condition_observed && s.catalysis_observed && s.device12_observed && s.multi_step_observed;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, AgentCatalog cat)
        {
            if (!Applies(e, c)) return null; var p = cat.ForRegion(RegionId); var f = e.core ?? new CoreShellEventFacts();
            var fact = new CoreShellDecisionFacts(); string key, reason, action = "FOLLOW_UP"; int priority = 40;
            if (state.core_shell.mature) return new AgentDecision { reason = "core_guidance_mature", profile_id = RegionId };
            string[] facts = new string[0];
            if (e.type == "REGION_ENTERED" && e.subject == RegionId)
            {
                key = "entry"; fact.environment_pressure = FormalPressure(e, c, p);
                bool formal = fact.environment_pressure.Length > 0;
                fact.knowledge_status = formal ? "OBSERVED_FACT" : "OBSERVATION"; fact.semantic_role = formal ? "ENVIRONMENT_CONDITION" : "REGION_OBSERVATION";
                reason = formal ? "authoritative_core_pressure_condition" : "core_shell_region_observation_only";
                if (formal) { action = "WARN"; priority = 100; }
            }
            else if (e.type == "MATERIAL_COLLECTED" && !f.analyzed && !c.known_materials.Contains(e.subject) && p.sample_ids.Contains(e.subject))
            { key = "unknown_material"; reason = "unknown_core_material"; fact.knowledge_status = "UNKNOWN"; }
            else if (Analysis(e, p))
            {
                var row = c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject);
                if (row == null || row.facts.Length == 0) return new AgentDecision { reason = "core_analysis_not_authoritative", profile_id = RegionId };
                facts = row.facts.ToArray(); key = "analysis"; reason = "core_material_analysis_confirmed"; action = "EXPLAIN"; priority = 60; fact.knowledge_status = "OBSERVED_FACT";
            }
            else if (Pressure(e)) { key = "pressure"; reason = "pressure_effect_observed"; fact.knowledge_status = "OBSERVATION"; fact.semantic_role = "PROCESS_CONDITION"; }
            else if (Success(e, p))
            {
                fact.knowledge_status = "OBSERVED_FACT"; fact.semantic_role = "PROCESS_CONDITION";
                fact.multi_condition_confirmed = Matrix(e, f, p); fact.catalyst_confirmed = Catalyst(f, p); fact.catalyst_consumed = f.catalyst_consumed;
                if (c.known_regions.Contains("mantle_flows") && c.known_chemistry.Any(k => k.StartsWith("extreme_heat:", StringComparison.Ordinal)) && !state.shown.Contains(p.Reaction("mantle_recall"))) { key = "mantle_recall"; reason = "mantle_multi_condition_recall"; }
                else if (fact.multi_condition_confirmed && !state.shown.Contains(p.Reaction("multi_condition"))) { key = "multi_condition"; reason = "formal_multi_condition_matrix"; action = "EXPLAIN"; priority = 60; }
                else if (fact.catalyst_confirmed && !state.shown.Contains(p.Reaction("catalyst"))) { key = "catalyst"; reason = "formal_catalyst_semantics"; action = "EXPLAIN"; priority = 60; }
                else { key = "completed"; reason = "high_pressure_catalysis_completed"; action = "CELEBRATE"; priority = 80; }
            }
            else if (MultiStep(e, f)) { key = "multi_step"; reason = "current_reaction_step_only"; action = "EXPLAIN"; priority = 60; fact.knowledge_status = "OBSERVATION"; fact.identity_confirmed = f.analyzed; }
            else if (e.type == "WORLD_REACTION_OBSERVED" && Phenomenon(e) == "advanced_reaction_need_identified")
            {
                if (!f.pressure_required && !f.catalyst_required) return new AgentDecision { reason = "no_advanced_condition_requirement", profile_id = RegionId };
                if (c.lab_stage < 3 || !c.known_regions.Contains(RegionId)) return new AgentDecision { reason = "device12_progression_not_available", profile_id = RegionId };
                key = "device12"; reason = "advanced_condition_device_capability"; action = "HINT"; priority = 20;
                fact.knowledge_status = "PROCESS_REQUIREMENT"; fact.causality_confirmed = true; fact.device_id = "device_high_pressure_catalytic_reactor";
            }
            else if (Failure(e, p))
            {
                int n = next.core_shell.failure_count;
                key = n <= 1 ? "failure" : n >= p.Hint("failure_hint_level_3_threshold", 6) ? "failure_3" : n >= p.Hint("failure_hint_level_2_threshold", 4) ? "failure_2" : "failure_1";
                reason = n <= 1 ? "core_failure_observed" : "core_failure_hint"; fact.missing_condition_confirmed = e.failure_reason;
                if (n > 1) { action = "HINT"; priority = 20; }
            }
            else return new AgentDecision { reason = "core_shell_exploration_silence", profile_id = RegionId };
            string id = p.Reaction(key); return new AgentDecision { action = action, priority = priority, reaction_id = id, cooldown_key = id, profile_id = RegionId, reason = reason, core = fact, confirmed_facts = facts };
        }
    }
}
