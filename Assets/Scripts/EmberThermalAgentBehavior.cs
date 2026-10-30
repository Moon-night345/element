using System;
using System.Linq;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class EmberAttention
    {
        public string[] known_facts = new string[0];
        public bool analysis_completed, temperature_observed, temperature_knowledge, thermal_completed, condensation_used, mature;
        public long last_progress_ms;
        public int unknown_material_count, analysis_hint_level;
        public int failure_count, failure_hint_level;
        public bool condenser_available;
        public bool combustion_observed;
    }
    // Incremental Ember adapter; formal failure/capability branches still pending.
    public static class EmberThermalAgentBehavior
    {
        public const string RegionId = "ember_rift";
        public const string CondenserId = "device_industrial_condenser";
        static bool Available(AgentEvent e, AgentContext c)
        { return c.crafted_devices.Contains(CondenserId) && ((e.type == "DEVICE_CRAFTED" && e.subject == CondenserId) ||
            ((e.type == "CAPABILITY_AVAILABLE" || e.type == "CAPABILITY_ACQUIRED") && e.subject == "CONDENSATION" && e.available)); }
        static bool Failure(AgentEvent e, RegionAgentBehaviorProfile p)
        { return e.type == "EXPERIMENT_FAILED" && e.behavior_profile == RegionId && p.failure_reasons.Contains(e.failure_reason); }
        static bool Entry(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "REGION_ENTERED" && e.subject == RegionId && c.region == RegionId && c.environment.ready &&
            c.environment.region == RegionId && c.environment.has_temperature && !float.IsNaN(c.environment.temperature) &&
            !float.IsInfinity(c.environment.temperature) && c.environment.temperature >= p.minimum_high_temperature; }
        static bool Unknown(AgentEvent e, AgentContext c, RegionAgentBehaviorProfile p)
        { return e.type == "MATERIAL_COLLECTED" && e.analysis_available && p.sample_ids.Contains(e.subject) &&
            !c.known_materials.Contains(e.subject) && (e.behavior_profile == RegionId ||
            (string.IsNullOrEmpty(e.behavior_profile) && (c.region == RegionId || e.region == RegionId))); }
        static int HintLevel(AgentEvent e, AgentPersistentState state, EmberAttention next, RegionAgentBehaviorProfile p)
        {
            var before = state.ember ?? new EmberAttention();
            if (!state.shown.Contains(p.Reaction("unknown_material")) || e.time_ms < before.last_progress_ms ||
                e.time_ms - before.last_progress_ms < p.Hint("stall_min_duration_msec", 30000)) return 0;
            for (int level = 3; level >= 1; level--)
                if (next.unknown_material_count >= p.Hint("analysis_hint_level_" + level + "_threshold", level * 3)) return level;
            return 0;
        }
        public static AgentDecision DecideWithState(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            bool entry = e.type == "REGION_ENTERED" && e.subject == RegionId;
            bool unknown = Unknown(e, c, p);
            bool available = Available(e, c);
            if (e.behavior_profile != RegionId && !entry && !unknown && !available) return null;
            if (state.ember != null && state.ember.mature) return new AgentDecision { reason = "ember_guidance_mature", profile_id = RegionId };
            if (available) return new AgentDecision { action = "HINT", priority = 20, profile_id = RegionId,
                reaction_id = p.Reaction("capability_available"), cooldown_key = p.Reaction("capability_available"), reason = "condenser_progression_confirmed" };
            if (Failure(e, p))
            {
                int level = 0;
                for (int rung = 3; rung >= 1; rung--) if (next.ember.failure_count >= p.Hint("failure_hint_level_" + rung + "_threshold", rung * 2 + 1)) { level = rung; break; }
                bool first = next.ember.failure_count == 1;
                if (!first && level == 0) return new AgentDecision { reason = "ember_failure_observed_without_escalation", profile_id = RegionId };
                string id = p.Reaction(first ? "failure_first" : "failure_hint_" + level);
                return new AgentDecision { action = first ? "FOLLOW_UP" : "HINT", priority = first ? 40 : 20, profile_id = RegionId,
                    reaction_id = id, cooldown_key = id, cooldown_ms = first ? 0 : 30000, reason = "ember_formal_failure_confirmed" };
            }
            if (unknown)
            {
                bool first = !state.shown.Contains(p.Reaction("unknown_material"));
                int level = HintLevel(e, state, next.ember, p);
                if (!first && level == 0) return new AgentDecision { reason = "ember_collection_observed", profile_id = RegionId };
                string id = p.Reaction(first ? "unknown_material" : "hint_" + level);
                return new AgentDecision { action = first ? "FOLLOW_UP" : "HINT", reaction_id = id, cooldown_key = id,
                    priority = first ? 40 : 20, cooldown_ms = first ? 0 : 30000, profile_id = RegionId,
                    reason = first ? "first_unknown_ember_material" : "ember_analysis_stall_level_" + level };
            }
            if (entry) return Entry(e, c, p) ? new AgentDecision { action = p.entry_action, reaction_id = p.entry_reaction,
                cooldown_key = p.entry_reaction, priority = 40, profile_id = RegionId, reason = "confirmed_high_temperature_entry" } :
                new AgentDecision { reason = "environment_state_unavailable", profile_id = RegionId };
            if (e.type == "EXPERIMENT_FAILED") return new AgentDecision { reason = "ember_failure_not_formal", profile_id = RegionId };
            return Decide(e, c, state);
        }
        public static void UpdateAttention(AgentEvent e, AgentContext c, AgentPersistentState state, AgentPersistentState next, RegionAgentBehaviorProfile p)
        {
            var s = next.ember ?? (next.ember = new EmberAttention()); bool progress = false;
            if (Failure(e, p))
            {
                s.failure_count = s.failure_count == int.MaxValue ? int.MaxValue : s.failure_count + 1;
                for (int level = 3; level >= 1; level--) if (s.failure_count >= p.Hint("failure_hint_level_" + level + "_threshold", level * 2 + 1))
                { s.failure_hint_level = Math.Max(s.failure_hint_level, level); break; }
            }
            if (Available(e, c)) { s.condenser_available = true; progress = true; }
            if (Unknown(e, c, p))
            {
                s.unknown_material_count = s.unknown_material_count == int.MaxValue ? int.MaxValue : s.unknown_material_count + 1;
                s.analysis_hint_level = Math.Max(s.analysis_hint_level, HintLevel(e, state, s, p));
            }
            if (Entry(e, c, p)) { s.temperature_observed = true; progress = true; }
            if (e.behavior_profile == RegionId)
            {
                if (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED")
                {
                    var analysis = c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject);
                    if (analysis != null && analysis.facts.Length > 0)
                    {
                        s.analysis_completed = true; progress = true;
                        s.known_facts = s.known_facts.Concat(analysis.facts).Distinct().ToArray();
                    }
                }
                // Reuse the same confirmed-result guard as presentation; UI suppression must not discard progress.
                var confirmed = Decide(e, c, state);
                if (confirmed != null && !confirmed.Silent)
                {
                    progress = true;
                    if (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED")
                    { s.analysis_completed = true; s.known_facts = s.known_facts.Concat(confirmed.confirmed_facts).Distinct().ToArray(); }
                    if (e.phenomenon == "thermal_carbonate_processed") { s.temperature_knowledge = true; s.thermal_completed = true; }
                    if (e.phenomenon == "vapor_condensed") s.condensation_used = true;
                    if (e.phenomenon == "carbon_fuel_used") s.combustion_observed = true;
                }
            }
            if (progress) { s.last_progress_ms = e.time_ms; s.unknown_material_count = 0; s.failure_count = 0; }
            if (p.mature_conditions.Length > 0 && p.mature_conditions.All(k => k == "ember_analysis_completed" ? s.analysis_completed :
                k == "ember_temperature_knowledge" ? s.temperature_knowledge : k == "ember_thermal_process_completed" ? s.thermal_completed :
                k == "ember_condensation_used" && s.condensation_used)) s.mature = true;
        }
        public static AgentDecision Decide(AgentEvent e, AgentContext c, AgentPersistentState state)
        {
            if (e.behavior_profile == RegionId && (e.type == "MATERIAL_ANALYZED" || e.type == "COMPOSITION_DISCOVERED"))
            {
                var known = c.confirmed_analyses.FirstOrDefault(a => a.region_id == RegionId && a.item_id == e.subject);
                if (known == null || known.facts.Length == 0) return new AgentDecision { reason = "knowledge_not_discovered", profile_id = RegionId };
                var facts = known.facts.Except((state.ember ?? new EmberAttention()).known_facts).ToArray();
                if (facts.Length == 0) return new AgentDecision { reason = "ember_fact_already_explained", profile_id = RegionId };
                string id = facts.Contains("ion_so4") ? "ember_sulfate_confirmed" : facts.Contains("element_s") ? "ember_sulfur_confirmed" :
                    facts.Intersect(new[] { "high_temperature_mineral", "heat_resistant", "combustion_residue", "carbonaceous_residue" }).Any() ?
                    "ember_heat_property_confirmed" : "ember_material_analysis";
                bool exact = id == "ember_sulfate_confirmed" || id == "ember_sulfur_confirmed";
                return new AgentDecision { action = exact ? "EXPLAIN" : "FOLLOW_UP", priority = exact ? 60 : 40,
                    reaction_id = id, cooldown_key = id, profile_id = RegionId, reason = "ember_analysis_confirmed", confirmed_facts = facts.ToArray() };
            }
            if (e.behavior_profile != RegionId || e.type != "WORLD_REACTION_OBSERVED") return null;
            var silent = new AgentDecision { reason = "ember_processing_not_confirmed", profile_id = RegionId };
            if (!e.observed) return silent;
            string reaction, action; int priority;
            if (e.phenomenon == "carbon_fuel_used")
            {
                if (c.carbon_fuel_burned <= 0) return silent;
                reaction = "ember_combustion_observed"; action = "FOLLOW_UP"; priority = 40;
            }
            else if (e.phenomenon == "thermal_carbonate_processed")
            {
                if (!c.known_chemistry.Contains("thermal:resource_carbonate_rock") ||
                    !new[] { "compound_cao", "compound_co2" }.All(id => e.outputs.Contains(id) && c.owned_items.Contains(id))) return silent;
                bool linked = state.salt != null && (state.salt.distillation_used || state.salt.separation_used);
                reaction = linked ? "ember_salt_heat_link" : "ember_thermal_decomposition_confirmed";
                action = "EXPLAIN"; priority = 60;
            }
            else if (e.phenomenon == "vapor_condensed")
            {
                if (!c.known_chemistry.Contains("condenser_used") || !e.outputs.Contains("compound_ethanol") || !c.owned_items.Contains("compound_ethanol")) return silent;
                reaction = "ember_condensation_completed"; action = "CELEBRATE"; priority = 80;
            }
            else return new AgentDecision { reason = "ember_branch_not_migrated", profile_id = RegionId };
            return new AgentDecision { action = action, reaction_id = reaction, cooldown_key = reaction, priority = priority,
                profile_id = RegionId, reason = "ember_delivered_processing_confirmed" };
        }
    }
}
