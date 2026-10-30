// Godot Phase2ChemistryService / WorldSession 专属设备路由的 Unity 等价层。
// 这里只生成原子执行计划；库存提交和设备状态机仍由 DeviceRuntimeService 统一负责。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ElementalStarSea.Data
{
    internal sealed class SpecializedDevicePlan
    {
        public bool success;
        public bool conditionFailure;
        public string reason = "";
        public RecipeQuantity[] consumed = new RecipeQuantity[0];
        public RecipeQuantity[] processInputs = new RecipeQuantity[0];
        public RecipeQuantity[] outputs = new RecipeQuantity[0];
        public Action applyState;
        public string analyzed_sample_id = "";
    }

    internal static class SpecializedDeviceChemistryService
    {
        const int GasTankCapacity = 20;

        static readonly HashSet<string> LiquidIds = new HashSet<string>
        {
            "compound_reduced_carbon_liquid", "compound_neutralized_salt_solution",
            "compound_concentrated_salt_solution", "compound_distilled_water",
            "compound_loaded_absorption_solution", "compound_mother_liquor",
            "resource_acidic_solution", "resource_alkaline_solution", "resource_mineral_solution",
            "compound_h2so4", "compound_h2o", "compound_cuso4", "compound_znso4"
        };

        static readonly string[] SupportedStoredGases =
        {
            "compound_co2", "compound_co", "compound_o2", "compound_so2",
            "compound_polluted_gas", "compound_contaminated_gas_stream"
        };

        public static bool IsLiquid(string itemId) { return LiquidIds.Contains(itemId ?? ""); }

        static readonly HashSet<string> SampleProfiles = new HashSet<string>
        {
            "resource_frozen_water", "resource_mineral_water", "resource_carbonate_rock",
            "resource_carbonate_deposit", "resource_acidic_solution", "resource_alkaline_solution",
            "resource_fossil", "resource_carbonaceous_material",
            "item_abandoned_mine_barium_bearing_mineral_block",
            "item_abandoned_mine_iron_bearing_ore_block", "item_abandoned_mine_copper_bearing_ore_block",
            "item_abandoned_mine_silver_bearing_ore_block",
            "item_ember_rift_scorched_rock_block", "item_ember_rift_sulfur_bearing_mineral_block",
            "item_ember_rift_ash_deposit_block", "item_ember_rift_carbonaceous_residue_block",
            "item_fungal_cave_wet_rock_block", "item_fungal_cave_fungal_biomass_block",
            "item_fungal_cave_organic_deposit_block", "item_fungal_cave_fermentation_feedstock_block",
            "item_geode_abyss_host_rock_block", "item_geode_abyss_crystal_bearing_rock_block",
            "item_geode_abyss_natural_crystal_deposit_block", "item_geode_abyss_mineral_solution_deposit_block",
            "item_magnetic_plateau_magnetized_rock_block", "item_magnetic_plateau_conductive_mineral_block",
            "item_magnetic_plateau_silver_bearing_mineral_block", "item_magnetic_plateau_nitrate_deposit_block",
            "item_radiation_belt_irradiated_rock_block", "item_radiation_belt_oxidized_mineral_block",
            "item_radiation_belt_contaminated_deposit_block", "item_radiation_belt_gas_bearing_deposit_block",
            "item_mantle_flow_high_temp_rock_block", "item_mantle_flow_sulfur_bearing_mineral_block",
            "item_mantle_flow_heat_resistant_mineral_block", "item_mantle_flow_mineral_deposit_block",
            "item_core_shell_dense_rock_block", "item_core_shell_high_strength_mineral_block",
            "item_core_shell_electroactive_mineral_block", "item_core_shell_core_mineral_deposit_block",
            "item_planetary_core_structural_rock_block", "item_planetary_core_heat_pressure_altered_mineral_block",
            "item_planetary_core_conductive_core_deposit_block", "item_planetary_core_high_energy_mineral_deposit_block"
        };

        public static SpecializedDevicePlan Build(GameState state, ProcessingRecipeDef recipe,
            IDictionary<string, string> conditions)
        {
            var c = conditions ?? new Dictionary<string, string>();
            switch (recipe.id)
            {
                case "recipe_phase2_detect": return Detect(state, c);
                case "recipe_phase2_filter":
                    return InventoryPlan(state, Q("compound_mixed_reaction_slurry"),
                        new[] { Q("compound_precipitate_solid"), Q("compound_neutralized_salt_solution") },
                        "missing_mixture", () => state.AddChemistryKnowledge("filtration_completed"));
                case "recipe_phase2_distill":
                    return InventoryPlan(state, Q("compound_neutralized_salt_solution"),
                        new[] { Q("compound_distilled_water"), Q("compound_concentrated_salt_solution") },
                        "missing_solution", () => { state.AddChemistryKnowledge("distillation_completed"); state.AddChemistryKnowledge("water_recovered"); });
                case "recipe_phase3a_condense_vapor": return Condense(state);
                case "recipe_phase3a_store_gas": return StoreGas(state, c);
                case "recipe_phase3a_thermal_carbonate":
                    return InventoryPlan(state, Q("resource_carbonate_rock"),
                        new[] { Q("compound_cao"), Q("compound_co2") }, "missing_carbonate",
                        () => { state.AddChemistryKnowledge("thermal:resource_carbonate_rock"); state.ChangeGeneratedGas("compound_co2", 1); });
                case "recipe_abandoned_mine_iron_ore":
                    return AnalysedInput(state, "item_abandoned_mine_iron_bearing_ore_block", "material_iron");
                case "recipe_abandoned_mine_copper_ore":
                    return AnalysedInput(state, "item_abandoned_mine_copper_bearing_ore_block", "material_copper");
                case "recipe_phase4b_core_high_strength_preprocess":
                    return AnalysedInput(state, "item_core_shell_high_strength_mineral_block", "compound_conditioned_high_strength_mineral");
                case "recipe_phase4b_silver_bearing_preprocess": return SilverPreprocess(state);
                case "recipe_phase3b_electrolyze": return Electrolyze(state);
                case "recipe_phase4b_silver_catalytic_refine": return SilverRefine(state, c);
                case "recipe_phase3b_reactor_batch":
                    return InventoryPlan(state, new[] { Q("compound_fe2o3"), Q("element_c") },
                        new[] { Q("element_fe") }, "missing_reactant",
                        () => state.AddChemistryKnowledge("reaction_kettle_batch:reaction_fe_refine"));
                case "barium_sulfate_batch_precipitation":
                    return InventoryPlan(state,
                        new[] { Q("compound_barium_process_solution", 5), Q("compound_sulfate_process_solution", 5) },
                        new[] { Q("compound_barium_sulfate_reaction_slurry", 5) }, "missing_process_solution",
                        () => state.AddChemistryKnowledge("barium_sulfate_precipitation_observed"));
                case "recipe_phase4b_high_strength_conditioning":
                    return InventoryPlan(state, Q("compound_conditioned_high_strength_mineral"),
                        new[] { Q("material_high_strength") }, "missing_input",
                        () => state.AddChemistryKnowledge("high_strength_material_produced"));
                case "recipe_phase4a_absorb_gas": return AbsorbGas(state, c);
                case "recipe_phase4a_industrial_crystallize": return Crystallize(state, c);
                case "recipe_phase4b_high_pressure_nh3": return HighPressureNh3(state, c);
                default: return Fail("specialized_runtime_required", true);
            }
        }

        static SpecializedDevicePlan Detect(GameState state, IDictionary<string, string> conditions)
        {
            string sample;
            conditions.TryGetValue("sample_id", out sample);
            if (string.IsNullOrEmpty(sample))
                sample = state.OwnedMaterialIds().FirstOrDefault(id => SampleProfiles.Contains(id) && !state.HasIdentifiedSample(id))
                    ?? state.OwnedMaterialIds().FirstOrDefault(SampleProfiles.Contains);
            if (string.IsNullOrEmpty(sample) || state.CountItem(sample) <= 0) return Fail("missing_sample");
            if (!SampleProfiles.Contains(sample)) return Fail("unknown_sample");
            var plan = Ok(new RecipeQuantity[0], new RecipeQuantity[0], new RecipeQuantity[0],
                () => { state.IdentifySample(sample); state.AddChemistryKnowledge("ph_discovery:" + sample); });
            plan.analyzed_sample_id = sample;
            return plan;
        }

        static SpecializedDevicePlan Condense(GameState state)
        {
            const string vapor = "compound_ethanol";
            if (state.CountVapor(vapor) <= 0) return Fail("no_vapor_stream");
            return Ok(new RecipeQuantity[0], new RecipeQuantity[0], new[] { Q(vapor) },
                () => { state.ChangeVapor(vapor, -1); state.AddChemistryKnowledge("condenser_used"); });
        }

        static SpecializedDevicePlan StoreGas(GameState state, IDictionary<string, string> conditions)
        {
            string gas;
            conditions.TryGetValue("gas_id", out gas);
            if (string.IsNullOrEmpty(gas)) gas = SupportedStoredGases.FirstOrDefault(id => state.CountGeneratedGas(id) > 0);
            if (string.IsNullOrEmpty(gas) || !SupportedStoredGases.Contains(gas)) return Fail("unsupported_gas");
            if (state.CountGeneratedGas(gas) <= 0) return Fail("gas_not_produced");
            var storedIds = (state.storedGasIds ?? new string[0]).Where(id => state.CountStoredGas(id) > 0).ToArray();
            if (storedIds.Length > 0 && storedIds[0] != gas) return Fail("incompatible_gas");
            if ((state.storedGasCounts ?? new int[0]).Sum() >= GasTankCapacity) return Fail("gas_tank_full", true);
            return Ok(new RecipeQuantity[0], new RecipeQuantity[0], new RecipeQuantity[0],
                () => { state.ChangeGeneratedGas(gas, -1); state.ChangeStoredGas(gas, 1); });
        }

        static SpecializedDevicePlan AnalysedInput(GameState state, string input, string output)
        {
            if (state.CountItem(input) <= 0) return Fail("missing_input");
            if (!state.HasIdentifiedSample(input)) return Fail("analysis_required", true);
            return Ok(new[] { Q(input) }, new RecipeQuantity[0], new[] { Q(output) },
                () => state.AddChemistryKnowledge("thermal:" + input));
        }

        static SpecializedDevicePlan SilverPreprocess(GameState state)
        {
            var source = state.CountItem("item_abandoned_mine_silver_bearing_ore_block") > 0
                ? "item_abandoned_mine_silver_bearing_ore_block"
                : "item_magnetic_plateau_silver_bearing_mineral_block";
            return AnalysedInput(state, source, "compound_preprocessed_silver_bearing_material");
        }

        static SpecializedDevicePlan Electrolyze(GameState state)
        {
            if (state.currentRegion != "magnetic_plateau") return Fail("magnetic_plateau_required", true);
            return InventoryPlan(state, Q("compound_h2o"),
                new[] { Q("compound_h2"), Q("compound_o2") }, "missing_electrolyte",
                () => state.AddChemistryKnowledge("electrolysis_completed"));
        }

        static SpecializedDevicePlan SilverRefine(GameState state, IDictionary<string, string> conditions)
        {
            if (state.currentRegion != "magnetic_plateau") return Fail("magnetic_plateau_required", true);
            string controlled;
            if (!conditions.TryGetValue("controlled_current", out controlled) || !IsTrue(controlled))
                return Fail("controlled_current_required", true);
            return InventoryPlan(state, Q("compound_preprocessed_silver_bearing_material"),
                new[] { Q("material_silver_catalytic") }, "missing_input",
                () => state.AddChemistryKnowledge("silver_catalytic_refined"));
        }

        static SpecializedDevicePlan AbsorbGas(GameState state, IDictionary<string, string> conditions)
        {
            string gas;
            conditions.TryGetValue("gas_id", out gas);
            if (string.IsNullOrEmpty(gas))
                gas = new[] { "compound_polluted_gas", "compound_so2", "compound_contaminated_gas_stream" }
                    .FirstOrDefault(id => state.CountStoredGas(id) > 0);
            if (gas != "compound_polluted_gas" && gas != "compound_so2" && gas != "compound_contaminated_gas_stream")
                return Fail("unsupported_gas");
            if (state.CountStoredGas(gas) <= 0) return Fail("gas_not_stored");
            if (state.CountItem("resource_alkaline_solution") <= 0) return Fail("missing_absorbent");
            bool contaminated = gas == "compound_contaminated_gas_stream";
            return Ok(new[] { Q("resource_alkaline_solution") }, new RecipeQuantity[0],
                contaminated
                    ? new[] { Q("compound_purified_gas_stream"), Q("compound_contaminated_absorbent") }
                    : new[] { Q("compound_treated_gas"), Q("compound_loaded_absorption_solution") },
                () => { state.ChangeStoredGas(gas, -1); state.AddChemistryKnowledge("absorption:" + gas); });
        }

        static SpecializedDevicePlan Crystallize(GameState state, IDictionary<string, string> conditions)
        {
            float concentration, temperature;
            if (!TryFloat(conditions, "concentration", out concentration) || float.IsNaN(concentration) || float.IsInfinity(concentration) || concentration < 0.90f)
                return Fail("insufficient_concentration", true);
            if (!TryFloat(conditions, "temperature", out temperature) || float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature > 0.35f)
                return Fail("insufficient_cooling", true);
            string solution;
            if (!conditions.TryGetValue("solution_id", out solution)) solution = "compound_concentrated_salt_solution";
            if (solution != "compound_concentrated_salt_solution" && solution != "compound_loaded_absorption_solution") return Fail("unsupported_industrial_solution");
            return InventoryPlan(state, Q(solution),
                new[] { Q("resource_high_purity_crystal"), Q("compound_mother_liquor") }, "missing_solution",
                () => state.AddChemistryKnowledge("industrial_crystallization:" + solution));
        }

        static SpecializedDevicePlan HighPressureNh3(GameState state, IDictionary<string, string> conditions)
        {
            string pressure, catalyst, purity;
            conditions.TryGetValue("pressure", out pressure);
            conditions.TryGetValue("catalyst", out catalyst);
            conditions.TryGetValue("purity", out purity);
            if (!string.IsNullOrEmpty(purity) && (purity == "RAW" || purity == "CONTAMINATED"))
                return Fail("input_not_purified", true);
            if (pressure != "PRESSURE_HIGH" && pressure != "PRESSURE_EXTREME")
                return Fail("high_pressure_required", true);
            if (catalyst != "material_iron") return Fail("invalid_catalyst", true);
            if (state.CountItem("material_iron") <= 0) return Fail("catalyst_required");
            if (state.CountItem("compound_n2") < 1 || state.CountItem("compound_h2") < 3)
                return Fail("missing_reactant");
            return Ok(new[] { Q("compound_n2"), Q("compound_h2", 3) }, new[] { Q("material_iron") },
                new[] { Q("compound_nh3", 2) },
                () => { state.AddChemistryKnowledge("pressure:reaction_nh3_from_n2"); state.AddChemistryKnowledge("catalyst:material_iron"); });
        }

        static SpecializedDevicePlan InventoryPlan(GameState state, RecipeQuantity input,
            RecipeQuantity[] outputs, string missingReason, Action applyState)
        {
            return InventoryPlan(state, new[] { input }, outputs, missingReason, applyState);
        }

        static SpecializedDevicePlan InventoryPlan(GameState state, RecipeQuantity[] inputs,
            RecipeQuantity[] outputs, string missingReason, Action applyState)
        {
            if (inputs.Any(value => state.CountItem(value.id) < value.n)) return Fail(missingReason);
            return Ok(inputs, new RecipeQuantity[0], outputs, applyState);
        }

        static SpecializedDevicePlan Ok(RecipeQuantity[] consumed, RecipeQuantity[] process,
            RecipeQuantity[] outputs, Action applyState)
        {
            return new SpecializedDevicePlan { success = true, consumed = consumed, processInputs = process,
                outputs = outputs, applyState = applyState };
        }

        static SpecializedDevicePlan Fail(string reason, bool condition = false)
        {
            return new SpecializedDevicePlan { success = false, reason = reason, conditionFailure = condition };
        }

        static RecipeQuantity Q(string id, int n = 1) { return new RecipeQuantity { id = id, n = n }; }
        static bool IsTrue(string value) { return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase); }
        static bool TryFloat(IDictionary<string, string> values, string key, out float result)
        {
            string text;
            result = 0f;
            return values.TryGetValue(key, out text) &&
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}
