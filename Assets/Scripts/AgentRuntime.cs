using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.Agent
{
    [Serializable] public sealed class AgentPair { public string key, value; }
    [Serializable] public sealed class AgentIntPair { public string key; public int value; }
    [Serializable] public sealed class AgentFactGroup { public string key; public string[] values; }
    [Serializable] public sealed class AgentAnalysis { public string item_id, region_id, processing_device; public string[] facts; public bool requires_scan; }
    [Serializable] public sealed class AgentResponse { public string reaction_id, text; }
    [Serializable] public sealed class RegionAgentBehaviorProfile
    {
        public string region_id, entry_action, entry_reaction, definition_json;
        public int global_cooldown_ms;
        public string[] exempt_actions, exempt_reactions;
        public AgentPair[] reactions, visuals;
        public AgentIntPair[] hints;
        public string[] material_events, hazard_events, mature_conditions;
        public string[] sample_ids, carbonate_facts, phenomena;
        public string[] solution_facts;
        public string[] carbon_facts, calcium_facts;
        public string[] organic_facts, fermentable_facts;
        public string[] crystal_facts;
        public string[] failure_reasons;
        public float maximum_cold_temperature;
        public float minimum_high_temperature;
        public float minimum_electrical_activity;
        public string[] contamination_facts, oxidation_properties;
        public string[] purity_phenomena, absorption_phenomena, oxidation_phenomena;
        public float minimum_heat_temperature;
        public string[] sulfur_facts;
        public string[] pressure_success_phenomena, pressure_fields;
        public string formal_catalyst;
        public string[] condition_fields, category_fields, process_phenomena, hazard_fields;
        public float minimum_solution_availability;
        public int Hint(string key, int fallback)
        { var row = (hints ?? new AgentIntPair[0]).FirstOrDefault(p => p.key == key); return row == null ? fallback : row.value; }
        public string Reaction(string key, string fallback = "")
        { var row = (reactions ?? new AgentPair[0]).FirstOrDefault(p => p.key == key); return row == null ? fallback : row.value; }
    }
    [Serializable] public sealed class AgentCatalog
    {
        public int schema_version;
        public RegionAgentBehaviorProfile[] profiles;
        public string[] generic_regions;
        public AgentResponse[] responses;
        public AgentAnalysis[] analyses;
        public AgentAnalysis[] grotto_detector_analyses;
        public AgentAnalysis[] geode_detector_analyses;
        public AgentAnalysis[] mine_detector_analyses;
        public AgentFactGroup[] fact_groups;
        public AgentPair[] rust_analysis_texts;
        public AgentPair[] crystal_carbonate_texts;
        public AgentPair[] sand_analysis_texts;
        public AgentPair[] grotto_material_texts, grotto_gas_texts;
        public float grotto_authored_oxygen, grotto_authored_light;
        public float magnetic_authored_activity;
        public AgentPair[] magnetic_analysis_texts;
        public string[] magnetic_unresolved_sample_ids;
        public float mantle_authored_temperature_floor;
        public float core_authored_pressure;
        public AgentFactGroup[] planetary_recall_groups;
        public AgentPair[] planetary_failure_categories;
        public AgentPair[] mantle_analysis_texts;
        public float crystal_authored_temperature;
        public float ember_authored_temperature;
        public float salt_authored_solution_availability;
        public AgentPair[] salt_solution_texts, salt_ph_texts, salt_sample_ph;
        public string[] salt_unresolved_analysis_ids;
        public RegionAgentBehaviorProfile ForRegion(string id) { return profiles.FirstOrDefault(p => p.region_id == id); }
        public static AgentCatalog Load()
        {
            var catalog = JsonUtility.FromJson<AgentCatalog>(Resources.Load<TextAsset>("Data/agent_catalog").text);
            if (catalog.schema_version != 1 || catalog.profiles.Length != 14 || catalog.generic_regions.Length != 3)
                throw new InvalidOperationException("Unsupported Agent source catalog");
            return catalog;
        }
    }
    [Serializable] public sealed class AgentCooldown { public string key; public long time_ms; }
    [Serializable] public sealed class AgentPersistentState
    {
        public long event_order, clock_ms, last_dialogue_ms = -100000;
        public string[] shown = new string[0], recent_event_ids = new string[0], seen_materials = new string[0];
        public AgentCooldown[] cooldowns = new AgentCooldown[0];
        public bool pending_analysis, bootstrap_complete;
        public int repeated_mining, analysis_failures, unknown_hint_level;
        public RustJungleAttention rust = new RustJungleAttention();
        public CrystalTundraAttention crystal = new CrystalTundraAttention();
        public SaltMarshAttention salt = new SaltMarshAttention();
        public EmberAttention ember = new EmberAttention();
        public SandAttention sand = new SandAttention();
        public GrottoAttention grotto = new GrottoAttention();
        public GeodeAttention geode = new GeodeAttention();
        public MineAttention mine = new MineAttention();
        public MagneticAttention magnetic = new MagneticAttention();
        public RadiationAttention radiation = new RadiationAttention();
        public MantleAttention mantle = new MantleAttention();
        public CoreShellAttention core_shell = new CoreShellAttention();
        public PlanetaryAttention planetary = new PlanetaryAttention();
    }
    public sealed class AgentEvent
    {
        public string type, region, subject, event_id;
        public long order, time_ms;
        public bool ui_already_explains, danger, analysis_available;
        public string behavior_profile = "", capability = "";
        public int level;
        public bool available, corrosion_relevant, observed, related_hazard, hazard_reduced, new_high_danger;
        public bool corrosion_active = true;
        public bool analyzer_used;
        public string phenomenon = "";
        public string gas_output = "";
        public bool has_concentration, has_purity;
        public float concentration, purity;
        public string source_event = "", discovery = "";
        public bool result_success;
        public bool condensable;
        public bool has_gas;
        public bool has_contamination_record, has_contaminated_identity;
        public bool has_temperature, controlled_extreme_heat, industrial_crystallizer_needed;
        public float temperature;
        public string process_input = "";
        public string processing_type = "thermal", process_output = "", output_phase = "";
        public bool industrial_scale_required, gas_treatment_required;
        public string failure_reason = "";
        public string[] outputs = new string[0];
        public CoreShellEventFacts core;
        public PlanetaryEventFacts planetary;
    }
    // Detached snapshots only. Policy/provider never receive GameState or gameplay services.
    public sealed class AgentContext
    {
        public readonly string region;
        public readonly string[] known_materials, known_regions, crafted_devices, owned_items;
        public readonly string[] known_reactions;
        public readonly long carbon_fuel_burned;
        public readonly bool gas_storage_progression_available;
        public readonly bool liquid_tank_progression_available;
        public readonly int lab_stage;
        public readonly string[] known_chemistry, analyzer_samples;
        public readonly AgentEnvironmentFacts environment;
        public readonly AgentAnalysis[] confirmed_analyses;
        public readonly AgentPair[] confirmed_ph;
        public AgentContext(GameState state, AgentCatalog catalog, AgentEnvironmentFacts environment = null)
        {
            region = state.currentRegion;
            carbon_fuel_burned = state.carbonFuelBurned;
            lab_stage = state.laboratoryStage;
            gas_storage_progression_available = state.laboratoryStage >= 2 && (state.visitedRegions ?? new string[0]).Contains("fluorescent_grotto");
            liquid_tank_progression_available = state.laboratoryStage >= 3 && (state.visitedRegions ?? new string[0]).Contains("planetary_core");
            known_materials = (state.identifiedSampleIds ?? new string[0]).ToArray();
            known_regions = (state.visitedRegions ?? new string[0]).ToArray();
            crafted_devices = (state.craftedDeviceIds ?? new string[0]).ToArray();
            owned_items = (state.inventoryIds ?? new string[0]).ToArray();
            known_reactions = (state.discoveredReactions ?? new string[0]).ToArray();
            known_chemistry = (state.chemistryKnowledgeIds ?? new string[0]).ToArray();
            analyzer_samples = known_chemistry.Where(k => k.StartsWith("ph_discovery:", StringComparison.Ordinal)).Select(k => k.Substring(13)).ToArray();
            this.environment = environment == null ? new AgentEnvironmentFacts() : new AgentEnvironmentFacts {
                region = environment.region, ready = environment.ready, corrosion_active = environment.corrosion_active,
                corrosion_resistance_level = environment.corrosion_resistance_level, resistance_available = environment.resistance_available,
                new_high_danger = environment.new_high_danger, has_temperature = environment.has_temperature, temperature = environment.temperature,
                has_solution_availability = environment.has_solution_availability, solution_availability = environment.solution_availability,
                has_oxygen = environment.has_oxygen, oxygen = environment.oxygen, light = environment.light,
                has_electrical_activity = environment.has_electrical_activity, electrical_activity = environment.electrical_activity,
                has_radiation = environment.has_radiation, radiation = environment.radiation,
                has_pressure = environment.has_pressure, pressure = environment.pressure, pressure_state = environment.pressure_state,
                danger = environment.danger, hazard = environment.hazard };
            confirmed_analyses = catalog.analyses.Where(a => known_materials.Contains(a.item_id) && (a.region_id != "geode_abyss" || analyzer_samples.Contains(a.item_id)) &&
                (a.region_id != "magnetic_plateau" || analyzer_samples.Contains(a.item_id)) &&
                (a.region_id != "mantle_flows" || analyzer_samples.Contains(a.item_id)) &&
                (a.region_id != "core_shell" || analyzer_samples.Contains(a.item_id)) &&
                (a.region_id != "planetary_core" || analyzer_samples.Contains(a.item_id)) &&
                (a.region_id != "radiation_belt" || (a.requires_scan ? known_chemistry.Contains("resource_scan:" + a.item_id) : analyzer_samples.Contains(a.item_id))) &&
                (a.region_id != "abandoned_mine" || a.item_id.StartsWith("item_", StringComparison.Ordinal) || known_chemistry.Contains("ore_identified:" + a.item_id))).Select(a =>
                a.region_id == "fluorescent_grotto" && analyzer_samples.Contains(a.item_id) ? catalog.grotto_detector_analyses.First(d => d.item_id == a.item_id) :
                a.region_id == "geode_abyss" && analyzer_samples.Contains(a.item_id) ? catalog.geode_detector_analyses.First(d => d.item_id == a.item_id) :
                a.region_id == "abandoned_mine" && analyzer_samples.Contains(a.item_id) ? catalog.mine_detector_analyses.FirstOrDefault(d => d.item_id == a.item_id) ?? a : a).Select(a =>
                new AgentAnalysis { item_id = a.item_id, region_id = a.region_id, processing_device = a.processing_device, facts = a.facts.ToArray(), requires_scan = a.requires_scan }).ToArray();
            confirmed_ph = (catalog.salt_sample_ph ?? new AgentPair[0]).Where(p => known_materials.Contains(p.key) && analyzer_samples.Contains(p.key) &&
                crafted_devices.Contains(CrystalTundraAgentBehavior.DeviceId)).Select(p => new AgentPair { key = p.key, value = p.value }).ToArray();
        }
    }
    public static class CrossRegionRecall
    {
        public static bool HasRelevantOldKnowledge(AgentContext c, AgentCatalog catalog)
        {
            return HasRegionFact(c, catalog, "crystal_tundra", "carbonate") || HasRegionFact(c, catalog, "crystal_tundra", "calcium") ||
                (System.Array.IndexOf(c.known_regions, "ember_rift") >= 0 && System.Array.IndexOf(c.known_chemistry, "thermal:resource_carbonate_rock") >= 0) ||
                (System.Array.IndexOf(c.known_regions, "salt_marsh") >= 0 &&
                    (System.Array.IndexOf(c.known_chemistry, "filtration_completed") >= 0 || System.Array.IndexOf(c.known_chemistry, "distillation_completed") >= 0));
        }
        public static bool HasRegionFact(AgentContext context, AgentCatalog catalog, string region, string group)
        {
            if (!context.known_regions.Contains(region)) return false;
            var definition = catalog.fact_groups.FirstOrDefault(g => g.key == group);
            return definition != null && context.confirmed_analyses.Any(a => a.region_id == region && a.facts.Intersect(definition.values).Any());
        }
    }
    public sealed class AgentDecision
    {
        public string action = "SILENT", reaction_id = "", reason = "default_silence", cooldown_key = "";
        public int priority, cooldown_ms;
        public bool once = true;
        public string profile_id = "";
        public string[] confirmed_facts = new string[0];
        public string confirmed_ph = "";
        public CoreShellDecisionFacts core;
        public PlanetaryDecisionFacts planetary;
        public AgentPersistentState next_state;
        public bool Silent { get { return action == "SILENT"; } }
    }
    public sealed class AgentDecisionPolicy
    {
        readonly AgentCatalog catalog;
        public AgentDecisionPolicy(AgentCatalog catalog) { this.catalog = catalog; }
        static AgentDecision Say(string action, string id, int priority, string reason, bool once = true, int cooldown = 0, string key = "")
        { return new AgentDecision { action = action, reaction_id = id, priority = priority, reason = reason, once = once, cooldown_ms = cooldown, cooldown_key = key == "" ? id : key }; }
        public AgentDecision Decide(AgentEvent e, AgentContext context, AgentPersistentState state, int currentPriority)
        {
            var next = JsonUtility.FromJson<AgentPersistentState>(JsonUtility.ToJson(state));
            if (e == null) return new AgentDecision { reason = "invalid_event", next_state = next };
            bool known = context.known_materials.Contains(e.subject);
            if (e.type == "MATERIAL_COLLECTED")
            {
                if (!next.seen_materials.Contains(e.subject))
                { next.seen_materials = next.seen_materials.Concat(new[] { e.subject }).ToArray(); next.repeated_mining = 0; }
                if (e.analysis_available && !known) next.pending_analysis = true;
            }
            else if (e.type == "MATERIAL_MINED") next.repeated_mining++;
            else if (e.type == "ANALYSIS_FAILED") next.analysis_failures++;
            else if (e.type == "MATERIAL_ANALYZED" && known) { next.pending_analysis = false; next.analysis_failures = 0; next.repeated_mining = 0; }
            else if (new[] { "PLAYER_AWAKENED", "REGION_ENTERED", "MATERIAL_RECOVERED", "RECIPE_AVAILABLE", "DEVICE_CRAFTED", "DEVICE_PLACED" }.Contains(e.type))
                next.repeated_mining = 0;
            var profile = catalog.ForRegion(e.region);
            var rustProfile = catalog.ForRegion(RustJungleAgentBehavior.RegionId);
            RustJungleAgentBehavior.UpdateAttention(e, context, state, next, rustProfile);
            var crystalProfile = catalog.ForRegion(CrystalTundraAgentBehavior.RegionId);
            CrystalTundraAgentBehavior.UpdateAttention(e, context, state, next, crystalProfile);
            var saltProfile = catalog.ForRegion(SaltMarshAgentBehavior.RegionId);
            SaltMarshAgentBehavior.UpdateAttention(e, context, state, next, saltProfile);
            EmberThermalAgentBehavior.UpdateAttention(e, context, state, next, catalog.ForRegion("ember_rift"));
            SandMaterialBehavior.Update(e, context, next, catalog);
            FluorescentGrottoAgentBehavior.Update(e, context, state, next, catalog);
            GeodeAbyssAgentBehavior.Update(e, context, next, catalog);
            AbandonedMineAgentBehavior.Update(e, context, next, catalog);
            MagneticMaterialBehavior.Update(e, context, next, catalog);
            RadiationBeltAgentBehavior.Update(e, context, next, catalog);
            MantleFlowAgentBehavior.Update(e, context, next, catalog);
            CoreShellAgentBehavior.Update(e, context, next, catalog);
            PlanetaryCoreAgentBehavior.Update(e, context, next, catalog);
            AgentDecision candidate;
            if (e.ui_already_explains) candidate = new AgentDecision { reason = "ui_already_explains" };
            else if ((candidate = PlanetaryCoreAgentBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = CoreShellAgentBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = MantleFlowAgentBehavior.Decide(e, context, state, catalog)) != null) { }
            else if ((candidate = RadiationBeltAgentBehavior.Decide(e, context, state, catalog)) != null) { }
            else if ((candidate = MagneticMaterialBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = MagneticPlateauEntryBehavior.Decide(e, context, catalog)) != null) { }
            else if ((candidate = AbandonedMineAgentBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = GeodeAbyssAgentBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = FluorescentGrottoAgentBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = SandMaterialBehavior.Decide(e, context, state, next, catalog)) != null) { }
            else if ((candidate = SandDesertEntryBehavior.Decide(e, context, catalog)) != null) { }
            else if ((candidate = EmberThermalAgentBehavior.DecideWithState(e, context, state, next, catalog.ForRegion("ember_rift"))) != null) { }
            else if ((candidate = RustJungleAgentBehavior.Decide(e, context, state, next, rustProfile)) != null) { }
            else if ((candidate = CrystalTundraAgentBehavior.Decide(e, context, state, next, crystalProfile)) != null) { }
            else if ((candidate = SaltMarshAgentBehavior.Decide(e, context, state, next, saltProfile)) != null) { }
            else if (e.danger || e.type == "EXPERIMENT_FAILED")
                candidate = Say("WARN", "experiment_failed_feedback", 100, "warning_event", false, 5000);
            else if (e.type == "DEVICE_CRAFTED" && e.subject == "device_chemical_workbench" && context.crafted_devices.Contains(e.subject))
            { next.bootstrap_complete = true; candidate = Say("CELEBRATE", "workbench_crafted", 80, "bootstrap_milestone"); }
            else if ((next.bootstrap_complete || context.crafted_devices.Contains("device_chemical_workbench")) &&
                (profile == null || e.region == "dead_city_ruins"))
            { next.bootstrap_complete = true; candidate = new AgentDecision { reason = "bootstrap_guidance_complete" }; }
            else if (e.type == "REGION_ENTERED" && profile != null && e.subject == e.region && context.region == e.region && context.known_regions.Contains(e.region))
                candidate = Say(profile.entry_action, profile.entry_reaction, 40, "profile_entry");
            else if (e.type == "PLAYER_AWAKENED") candidate = Say("HINT", "awakened_greeting", 20, "first_awakening");
            else if (e.type == "MATERIAL_ANALYZED" && known && (e.region == "dead_city_ruins" || profile == null))
            {
                var analysis = context.confirmed_analyses.FirstOrDefault(a => a.item_id == e.subject);
                if (analysis == null || analysis.facts.Length == 0) candidate = new AgentDecision { reason = "confirmed_facts_unavailable" };
                else
                {
                    string id = analysis.facts.Contains("iron_bearing") ? "first_iron_analysis" : analysis.facts.Contains("cu_bearing") ? "first_copper_analysis" :
                        analysis.facts.Contains("silica_rich") ? "first_glass_analysis" : "first_material_analysis";
                    candidate = Say("EXPLAIN", id, 60, "composition_confirmed", key: "analysis_explanation");
                }
            }
            else if (e.type == "MATERIAL_ANALYZED" && !known) candidate = new AgentDecision { reason = "unconfirmed_knowledge" };
            else if (profile != null && e.region != "dead_city_ruins")
                candidate = new AgentDecision { reason = "dedicated_branch_not_migrated" };
            else if (e.type == "ANALYSIS_FAILED" && next.analysis_failures == 1)
                candidate = Say("FOLLOW_UP", "analysis_failed_follow_up", 40, "first_analysis_failure");
            else if (e.type == "ANALYSIS_FAILED" && next.analysis_failures >= 3)
                candidate = Say("HINT", "analysis_failed_hint", 20, "repeated_analysis_failure");
            else if (e.type == "MATERIAL_COLLECTED" && e.analysis_available && !known)
            { next.unknown_hint_level = Math.Max(2, next.unknown_hint_level); candidate = Say("HINT", "unknown_material_hint", 20, "unknown_analyzable_material"); }
            else if (e.type == "MATERIAL_MINED" && next.repeated_mining >= 4 && next.pending_analysis)
            { next.unknown_hint_level = 3; candidate = Say("HINT", "stall_analysis_hint", 20, "stall_threshold_reached", false, 30000); }
            else if (e.type == "MATERIAL_RECOVERED" && e.outputs.Length > 0 && e.outputs.All(id => context.owned_items.Contains(id)))
            {
                string id = "first_material_recovery";
                if (state.shown.Contains(id))
                    id = e.outputs.Contains("material_iron") ? "first_iron_material" : e.outputs.Contains("material_copper") ? "first_copper_material" :
                        e.outputs.Contains("material_glass") ? "first_glass_material" : "";
                candidate = id == "" ? new AgentDecision() : Say("FOLLOW_UP", id, 40, "recovery_confirmed", key: "material_recovery");
            }
            else candidate = new AgentDecision();
            candidate.next_state = next;
            if (candidate.Silent) return candidate;
            if (!string.IsNullOrEmpty(candidate.profile_id)) profile = catalog.ForRegion(candidate.profile_id) ?? profile;
            string suppressed = "";
            var last = state.cooldowns.FirstOrDefault(c => c.key == candidate.cooldown_key);
            if (candidate.once && state.shown.Contains(candidate.reaction_id)) suppressed = "already_shown";
            else if (last != null && candidate.cooldown_ms > 0 && e.time_ms >= last.time_ms && e.time_ms - last.time_ms < candidate.cooldown_ms) suppressed = "cooldown";
            else if (!(profile == null ? new[] { "WARN", "EXPLAIN", "CELEBRATE", "FOLLOW_UP" } : profile.exempt_actions).Contains(candidate.action) &&
                !(profile == null ? new[] { "workbench_recipe_available" } : profile.exempt_reactions).Contains(candidate.reaction_id))
            {
                if (e.time_ms >= state.last_dialogue_ms && e.time_ms - state.last_dialogue_ms < (profile == null ? 2500 : profile.global_cooldown_ms)) suppressed = "global_cooldown";
                else if (currentPriority >= candidate.priority) suppressed = "dialogue_busy";
            }
            return suppressed == "" ? candidate : new AgentDecision { reason = suppressed, next_state = next };
        }
    }
    public sealed class AgentDialogue
    {
        public string reaction_id, text, action, visual;
        public int priority;
        public float remaining = 4.5f;
    }
    public sealed class AgentDialogueQueue
    {
        public AgentDialogue Current { get; private set; }
        readonly List<AgentDialogue> pending = new List<AgentDialogue>();
        public int PendingCount { get { return pending.Count; } }
        public void Enqueue(AgentDialogue dialogue)
        {
            if (dialogue == null || string.IsNullOrEmpty(dialogue.text)) return;
            if (Current == null) { Current = dialogue; return; }
            if (dialogue.priority >= 50 && dialogue.priority > Current.priority)
            { pending.Insert(0, Current); Current = dialogue; return; }
            pending.Add(dialogue);
            var sorted = pending.OrderByDescending(d => d.priority).ToArray(); pending.Clear(); pending.AddRange(sorted);
        }
        public void Advance(float seconds, bool paused)
        {
            if (paused || Current == null || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            Current.remaining -= seconds;
            if (Current.remaining > 0) return;
            Current = pending.FirstOrDefault(); if (Current != null) pending.RemoveAt(0);
        }
    }
    public sealed class DeterministicAgentResponseProvider
    {
        readonly AgentCatalog catalog;
        public DeterministicAgentResponseProvider(AgentCatalog catalog) { this.catalog = catalog; }
        public AgentDialogue Response(AgentDecision decision, string region)
        {
            if (decision.Silent) return null;
            var response = catalog.responses.FirstOrDefault(r => r.reaction_id == decision.reaction_id);
            string text = response == null ? "" : response.text;
            if (decision.reaction_id == "rust_material_analysis" && decision.confirmed_facts.Length > 0)
            {
                var facts = decision.confirmed_facts;
                bool iron = facts.Intersect(new[] { "element_fe", "iron_bearing", "compound_fe2o3" }).Any();
                bool chloride = facts.Intersect(new[] { "element_cl", "chloride_associated", "compound_hcl" }).Any();
                string key = iron && chloride ? "iron_chloride" : iron ? "iron" : chloride ? "chloride" : "default";
                text = catalog.rust_analysis_texts.First(p => p.key == key).value;
            }
            if (decision.reaction_id == "crystal_carbonate_confirmed" && decision.confirmed_facts.Length > 0)
                text = catalog.crystal_carbonate_texts.First(p => p.key == (decision.confirmed_facts.Contains("compound_caco3") ? "exact" : "general")).value;
            if (decision.reaction_id == "salt_solution_identity_confirmed" && decision.confirmed_facts.Length > 0)
            {
                var f = decision.confirmed_facts;
                string key = f.Contains("compound_hcl") ? "compound_hcl" : f.Contains("compound_naoh") ? "compound_naoh" :
                    f.Contains("ion_oh") ? "ion_oh" : f.Contains("element_cl") || f.Contains("chloride_related") ? "chloride" :
                    f.Contains("element_na") ? "element_na" : f.Contains("element_k") ? "element_k" : "default";
                text = catalog.salt_solution_texts.First(p => p.key == key).value;
            }
            if (decision.reaction_id == "salt_ph_confirmed" && decision.confirmed_ph != "")
                text = catalog.salt_ph_texts.First(p => p.key == decision.confirmed_ph).value;
            if (new[] { "sand_carbon_confirmed", "sand_calcium_confirmed", "sand_carbonate_confirmed" }.Contains(decision.reaction_id))
            {
                var f = decision.confirmed_facts;
                string topic = decision.reaction_id == "sand_carbon_confirmed" ? "carbon" : decision.reaction_id == "sand_calcium_confirmed" ? "calcium" : "carbonate";
                string exact = topic == "carbon" ? "element_c" : topic == "calcium" ? "element_ca" : "compound_caco3";
                string key = topic + "_" + (f.Contains(exact) ? "exact" : topic == "carbonate" && f.Contains("ion_co3") ? "ion" : "general");
                text = catalog.sand_analysis_texts.First(p => p.key == key).value;
            }
            var profile = catalog.ForRegion(decision.profile_id == "" ? region : decision.profile_id);
            var visual = profile == null ? null : profile.visuals.FirstOrDefault(v => v.key == decision.reaction_id);
            if (decision.reaction_id == "grotto_material_analysis")
            {
                string key = new[] { "compound_co2", "element_o", "element_h", "element_c" }.FirstOrDefault(decision.confirmed_facts.Contains) ?? "default";
                text = catalog.grotto_material_texts.First(p => p.key == key).value;
            }
            if (decision.reaction_id == "grotto_gas_produced") text = catalog.grotto_gas_texts.First(p => p.key == (decision.confirmed_facts.Contains("compound_co2") ? "confirmed" : "unknown")).value;
            if (decision.reaction_id == "magnetic_material_analysis")
            {
                var f = decision.confirmed_facts;
                string key = f.Contains("ion_no3") || f.Contains("nitrate_related") ? "nitrate" : f.Contains("ion_nh4") || f.Contains("ammonium_related") ? "ammonium" :
                    f.Contains("element_ag") || f.Contains("silver_bearing") ? "silver" : f.Contains("element_cu") || f.Contains("copper_bearing") ? "copper" : "default";
                text = catalog.magnetic_analysis_texts.First(p => p.key == key).value;
            }
            if (decision.reaction_id == "mantle_material_analysis")
            {
                var f = decision.confirmed_facts;
                string key = f.Contains("ion_so4") || f.Contains("sulfate_related") ? "sulfate" : f.Intersect(new[] { "element_s", "sulfur_related", "sulfur_bearing" }).Any() ? "sulfur" : "default";
                text = catalog.mantle_analysis_texts.First(p => p.key == key).value;
            }
            if (string.IsNullOrEmpty(text)) return null; // Other dynamic responses remain unported.
            return new AgentDialogue { reaction_id = decision.reaction_id, text = text, action = decision.action, priority = decision.priority,
                visual = visual != null ? visual.value : decision.action == "WARN" ? "ALERT" : decision.action == "CELEBRATE" ? "HAPPY" : "SPEAKING" };
        }
    }
    public sealed class AgentController
    {
        readonly GameState state;
        readonly AgentCatalog catalog;
        readonly AgentDecisionPolicy policy;
        readonly DeterministicAgentResponseProvider provider;
        readonly Func<AgentEnvironmentFacts> environmentSource;
        public readonly AgentDialogueQueue Queue = new AgentDialogueQueue();
        public AgentController(GameState state, AgentCatalog catalog, Func<AgentEnvironmentFacts> environmentSource = null)
        {
            this.state = state; this.catalog = catalog;
            if (state.agent == null) state.agent = new AgentPersistentState();
            if (state.agent.rust == null) state.agent.rust = new RustJungleAttention();
            if (state.agent.crystal == null) state.agent.crystal = new CrystalTundraAttention();
            if (state.agent.salt == null) state.agent.salt = new SaltMarshAttention();
            if (state.agent.ember == null) state.agent.ember = new EmberAttention();
            if (state.agent.sand == null) state.agent.sand = new SandAttention();
            if (state.agent.grotto == null) state.agent.grotto = new GrottoAttention();
            if (state.agent.geode == null) state.agent.geode = new GeodeAttention();
            if (state.agent.mine == null) state.agent.mine = new MineAttention();
            if (state.agent.magnetic == null) state.agent.magnetic = new MagneticAttention();
            if (state.agent.radiation == null) state.agent.radiation = new RadiationAttention();
            if (state.agent.mantle == null) state.agent.mantle = new MantleAttention();
            if (state.agent.core_shell == null) state.agent.core_shell = new CoreShellAttention();
            if (state.agent.planetary == null) state.agent.planetary = new PlanetaryAttention();
            this.environmentSource = environmentSource;
            policy = new AgentDecisionPolicy(catalog); provider = new DeterministicAgentResponseProvider(catalog);
        }
        public AgentDecision Publish(string type, string subject, long time, bool uiExplains = false, bool analyzable = false, string[] outputs = null, AgentEvent evidence = null)
        {
            long order = checked(state.agent.event_order + 1);
            return Observe(new AgentEvent { type = type, subject = subject, region = state.currentRegion, order = order,
                event_id = "agent_event_" + order + "_" + type + "_" + subject, time_ms = Math.Max(time, state.agent.clock_ms),
                ui_already_explains = uiExplains, analysis_available = analyzable, outputs = outputs == null ? new string[0] : outputs.ToArray(), danger = evidence != null && evidence.danger,
                behavior_profile = evidence == null ? "" : evidence.behavior_profile, capability = evidence == null ? "" : evidence.capability,
                level = evidence == null ? 0 : evidence.level, available = evidence != null && evidence.available,
                corrosion_relevant = evidence != null && evidence.corrosion_relevant, corrosion_active = evidence == null || evidence.corrosion_active,
                observed = evidence != null && evidence.observed, related_hazard = evidence != null && evidence.related_hazard,
                hazard_reduced = evidence != null && evidence.hazard_reduced, new_high_danger = evidence != null && evidence.new_high_danger,
                analyzer_used = evidence != null && evidence.analyzer_used, phenomenon = evidence == null ? "" : evidence.phenomenon,
                failure_reason = evidence == null ? "" : evidence.failure_reason, gas_output = evidence == null ? "" : evidence.gas_output,
                has_concentration = evidence != null && evidence.has_concentration, concentration = evidence == null ? 0f : evidence.concentration,
                has_purity = evidence != null && evidence.has_purity, purity = evidence == null ? 0f : evidence.purity,
                source_event = evidence == null ? "" : evidence.source_event, discovery = evidence == null ? "" : evidence.discovery,
                result_success = evidence != null && evidence.result_success, condensable = evidence != null && evidence.condensable, has_gas = evidence != null && evidence.has_gas,
                has_contamination_record = evidence != null && evidence.has_contamination_record, has_contaminated_identity = evidence != null && evidence.has_contaminated_identity,
                has_temperature = evidence != null && evidence.has_temperature, temperature = evidence == null ? 0f : evidence.temperature,
                controlled_extreme_heat = evidence != null && evidence.controlled_extreme_heat, industrial_crystallizer_needed = evidence != null && evidence.industrial_crystallizer_needed,
                process_input = evidence == null ? "" : evidence.process_input,
                processing_type = evidence == null ? "thermal" : evidence.processing_type, process_output = evidence == null ? "" : evidence.process_output,
                output_phase = evidence == null ? "" : evidence.output_phase, industrial_scale_required = evidence != null && evidence.industrial_scale_required,
                gas_treatment_required = evidence != null && evidence.gas_treatment_required,
                core = evidence == null || evidence.core == null ? null : JsonUtility.FromJson<CoreShellEventFacts>(JsonUtility.ToJson(evidence.core)),
                planetary = evidence == null || evidence.planetary == null ? null : JsonUtility.FromJson<PlanetaryEventFacts>(JsonUtility.ToJson(evidence.planetary)) });
        }
        public AgentDecision Observe(AgentEvent e)
        {
            if (e == null || string.IsNullOrEmpty(e.event_id) || e.time_ms < 0 || e.order < 0)
                return new AgentDecision { reason = "invalid_event" };
            if (state.agent.recent_event_ids.Contains(e.event_id)) return new AgentDecision { reason = "duplicate_event" };
            var context = new AgentContext(state, catalog, environmentSource == null ? null : environmentSource());
            var decision = policy.Decide(e, context, state.agent, Queue.Current == null ? 0 : Queue.Current.priority);
            state.agent = decision.next_state;
            state.agent.event_order = Math.Max(state.agent.event_order, e.order);
            state.agent.clock_ms = Math.Max(state.agent.clock_ms, e.time_ms);
            state.agent.recent_event_ids = state.agent.recent_event_ids.Concat(new[] { e.event_id }).Reverse().Take(64).Reverse().ToArray();
            var response = provider.Response(decision, e.region);
            if (response == null) return decision;
            if (!state.agent.shown.Contains(decision.reaction_id)) state.agent.shown = state.agent.shown.Concat(new[] { decision.reaction_id }).ToArray();
            state.agent.cooldowns = state.agent.cooldowns.Where(c => c.key != decision.cooldown_key)
                .Concat(new[] { new AgentCooldown { key = decision.cooldown_key, time_ms = e.time_ms } }).ToArray();
            state.agent.last_dialogue_ms = e.time_ms;
            Queue.Enqueue(response);
            return decision;
        }
    }
}
