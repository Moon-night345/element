// Godot Phase2ChemistryService 中非设备的上游世界化学动作。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    [Serializable]
    public sealed class WorldChemistryResult
    {
        public bool success;
        public string reason;
        public string action_id;
        public int heat_units;
        public bool has_concentration;
        public float concentration;
        public string subject_id = "", discovery = "", metal = "", source_event = "";
        public InventoryDeltaEntry[] inventory_delta = new InventoryDeltaEntry[0];
    }

    public sealed class WorldChemistryService
    {
        readonly GameState _state;
        // ScrapCatalog.terrain_analysis_for_item / ItemContractCatalog._dead_city_recovery_for.
        public static readonly Dictionary<string, string> DeadCityRecovery = new Dictionary<string, string>
        {
            { "item_dead_city_surface_debris_block", "material_refractory" },
            { "item_dead_city_masonry_block", "material_refractory" },
            { "item_dead_city_carbonaceous_debris_block", "material_carbon" },
            { "item_dead_city_iron_bearing_debris_block", "material_iron" },
            { "item_dead_city_copper_bearing_debris_block", "material_copper" },
            { "item_dead_city_glass_bearing_debris_block", "material_glass" }
        };
        public event Action<WorldChemistryResult> GameplayEventPublished;

        public WorldChemistryService(GameState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public WorldChemistryResult StartOrganicProcess()
        {
            if (_state.currentRegion != "fluorescent_grotto") return Fail("organic_process", "fluorescent_grotto_required");
            if (_state.CountItem("organic_glucose") <= 0) return Fail("organic_process", "missing_organic_material");
            // fluorescent_grotto 带 low_oxygen：Godot 产率为 ethanol×1 + CO2×1。
            var delta = new[] { D("organic_glucose", -1), D("compound_ethanol", 1), D("compound_co2", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("organic_process", "inventory_rejected");
            _state.ChangeGeneratedGas("compound_co2", 1);
            // 玩家入口把紧邻的 register_vapor 调用合并到一次“发酵并导流”交互中；
            // 化学产率仍与 Godot start_organic_process + register_vapor(ethanol, 1) 一致。
            _state.ChangeVapor("compound_ethanol", 1);
            _state.AddChemistryKnowledge("organic_process_completed");
            _state.AddChemistryKnowledge("oxygen_condition:LIMITED");
            return Ok("organic_process", delta);
        }

        // Phase2ChemistryService.neutralize / precipitate: canonical Phase 2 solution chain.
        public WorldChemistryResult NeutralizeSolutions()
        {
            if (_state.CountItem("resource_acidic_solution") <= 0 || _state.CountItem("resource_alkaline_solution") <= 0)
                return Fail("neutralize_solutions", "missing_reactant");
            var delta = new[] { D("resource_acidic_solution", -1), D("resource_alkaline_solution", -1),
                D("compound_neutralized_salt_solution", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("neutralize_solutions", "inventory_rejected");
            _state.AddChemistryKnowledge("salt_marsh_acid_pool_01_neutralized");
            return Ok("neutralize_solutions", delta);
        }

        public WorldChemistryResult PrecipitateSolution()
        {
            if (_state.CountItem("compound_neutralized_salt_solution") <= 0 || _state.CountItem("resource_alkaline_solution") <= 0)
                return Fail("precipitate_solution", "missing_reactant");
            var delta = new[] { D("compound_neutralized_salt_solution", -1), D("resource_alkaline_solution", -1),
                D("compound_mixed_reaction_slurry", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("precipitate_solution", "inventory_rejected");
            _state.AddChemistryKnowledge("precipitate_observed");
            return Ok("precipitate_solution", delta);
        }

        public WorldChemistryResult UseCarbonFuel(string input)
        {
            if (input != "resource_carbonaceous_material" && input != "material_carbon") return Fail("use_carbon_fuel", "invalid_fuel");
            if (_state.CountItem(input) <= 0) return Fail("use_carbon_fuel", "missing_fuel");
            if (_state.carbonFuelBurned == long.MaxValue) return Fail("use_carbon_fuel", "counter_overflow");
            var delta = new[] { D(input, -1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("use_carbon_fuel", "inventory_rejected");
            _state.carbonFuelBurned++;
            return Ok("use_carbon_fuel", delta, 1);
        }

        public static readonly string[] SandBlocks = { "item_wind_desert_sandstone_block", "item_wind_desert_carbonate_rock_block",
            "item_wind_desert_carbonaceous_deposit_block", "item_wind_desert_weathered_mineral_crust_block" };
        public WorldChemistryResult AnalyzeSandBlock(string input)
        {
            if (Array.IndexOf(SandBlocks, input) < 0) return Fail("analyze_sand_block", "not_terrain_block");
            if (_state.CountItem(input) <= 0) return Fail("analyze_sand_block", "missing_block_item");
            var delta = new[] { D(input, -1) }; // Source terrain analysis is destructive outside dead-city special case.
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("analyze_sand_block", "inventory_commit_failed");
            _state.IdentifySample(input); // Existing known-analysis index represents confirmed composition, not Device05 use/pH.
            return Ok("analyze_sand_block", delta);
        }

        public static readonly string[] GrottoBlocks = { "item_fungal_cave_wet_rock_block", "item_fungal_cave_fungal_biomass_block",
            "item_fungal_cave_organic_deposit_block", "item_fungal_cave_fermentation_feedstock_block" };
        public WorldChemistryResult AnalyzeGrottoBlock(string input)
        {
            if (Array.IndexOf(GrottoBlocks, input) < 0) return Fail("analyze_grotto_block", "not_terrain_block");
            if (_state.CountItem(input) <= 0) return Fail("analyze_grotto_block", "missing_block_item");
            var delta = new[] { D(input, -1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("analyze_grotto_block", "inventory_commit_failed");
            _state.IdentifySample(input);
            return Ok("analyze_grotto_block", delta);
        }

        public static readonly string[] GeodeBlocks = { "item_geode_abyss_host_rock_block", "item_geode_abyss_crystal_bearing_rock_block",
            "item_geode_abyss_natural_crystal_deposit_block", "item_geode_abyss_mineral_solution_deposit_block" };
        public WorldChemistryResult PrepareBariumSolution()
        {
            const string input = "item_abandoned_mine_barium_bearing_mineral_block";
            if (_state.CountItem(input) <= 0) return Fail("prepare_barium_solution", "missing_barium_mineral");
            if (!_state.HasIdentifiedSample(input)) return Fail("prepare_barium_solution", "analysis_required");
            var delta = new[] { D(input, -1), D("compound_barium_process_solution", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("prepare_barium_solution", "inventory_rejected");
            return Ok("prepare_barium_solution", delta);
        }

        public WorldChemistryResult PrepareSulfateSolution()
        {
            const string input = "item_ember_rift_sulfur_bearing_mineral_block";
            if (_state.CountItem(input) <= 0) return Fail("prepare_sulfate_solution", "missing_sulfate_source");
            if (!_state.HasIdentifiedSample(input)) return Fail("prepare_sulfate_solution", "analysis_required");
            var delta = new[] { D(input, -1), D("compound_sulfate_process_solution", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("prepare_sulfate_solution", "inventory_rejected");
            return Ok("prepare_sulfate_solution", delta);
        }

        public WorldChemistryResult ProcessGasBearingDeposit()
        {
            const string input = "item_radiation_belt_gas_bearing_deposit_block";
            const string gas = "compound_contaminated_gas_stream";
            if (_state.CountItem(input) <= 0) return Fail("process_gas_bearing_deposit", "missing_gas_bearing_deposit");
            var delta = new[] { D(input, -1), D(gas, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("process_gas_bearing_deposit", "inventory_rejected");
            _state.ChangeGeneratedGas(gas, 1);
            _state.AddChemistryKnowledge("gas_bearing_deposit_processed");
            return Ok("process_gas_bearing_deposit", delta);
        }

        public WorldChemistryResult NaturalCrystallize(string solutionId, float concentration)
        {
            if (_state.currentRegion != "geode_abyss")
                return Fail("natural_crystallization", "geode_abyss_required");
            if (solutionId != "compound_concentrated_salt_solution" && solutionId != "resource_mineral_solution")
                return Fail("natural_crystallization", "unsupported_solution");
            if (float.IsNaN(concentration) || float.IsInfinity(concentration) || concentration < 0.80f)
                return Fail("natural_crystallization", "insufficient_concentration");
            if (_state.CountItem(solutionId) <= 0)
                return Fail("natural_crystallization", "missing_solution");
            var delta = new[] { D(solutionId, -1), D("resource_crystal", 1), D("compound_mother_liquor", 1) };
            if (!_state.TryApplyInventoryDelta(delta))
                return Fail("natural_crystallization", "inventory_rejected");
            _state.AddChemistryKnowledge("natural_crystallization:" + solutionId);
            _state.AddChemistryKnowledge("industrial_crystallizer_need_discovered");
            return Ok("natural_crystallization", delta, hasConcentration: true, concentration: concentration);
        }

        public WorldChemistryResult AnalyzeDeadCityBlock(string input)
        {
            if (!DeadCityRecovery.ContainsKey(input ?? "")) return Fail("analyze_terrain_block", "analysis_design_tbd");
            if (_state.CountItem(input) <= 0) return Fail("analyze_terrain_block", "missing_block_item");
            _state.IdentifySample(input);
            return Ok("analyze_terrain_block", new InventoryDeltaEntry[0]);
        }

        // WorldSession.scan_chemical_resource: direct environmental sample scan,
        // not an invented Device 05 unlock or a material-producing recipe.
        public WorldChemistryResult AnalyzeRustSample(string input)
        {
            if (_state.currentRegion != "rust_jungle") return Fail("analyze_rust_sample", "rust_jungle_required");
            if (input != "resource_rust_metal_residue" && input != "resource_chloride_deposit")
                return Fail("analyze_rust_sample", "analysis_design_tbd");
            if (_state.CountItem(input) <= 0) return Fail("analyze_rust_sample", "missing_sample");
            _state.IdentifySample(input);
            return Ok("analyze_rust_sample", new InventoryDeltaEntry[0]);
        }

        public static readonly string[] RadiationSamples = { "resource_contaminated_mineral", "resource_contaminated_solution", "resource_oxidized_residue" };
        public WorldChemistryResult AnalyzeRadiationSample(string input)
        {
            if (_state.currentRegion != "radiation_belt") return Fail("scan_radiation_sample", "radiation_belt_required");
            if (Array.IndexOf(RadiationSamples, input) < 0) return Fail("scan_radiation_sample", "analysis_not_authored");
            if (_state.CountItem(input) <= 0) return Fail("scan_radiation_sample", "missing_sample");
            _state.IdentifySample(input); _state.AddChemistryKnowledge("resource_scan:" + input);
            return Send(new WorldChemistryResult { success = true, action_id = "scan_radiation_sample", subject_id = input, source_event = "radiation_sample_scanned" });
        }

        public WorldChemistryResult RecoverDeadCityBlock(string input)
        {
            string output;
            if (!DeadCityRecovery.TryGetValue(input ?? "", out output)) return Fail("recover_terrain_block", "recovery_design_tbd");
            if (!_state.HasIdentifiedSample(input)) return Fail("recover_terrain_block", "analysis_required");
            if (_state.CountItem(input) <= 0) return Fail("recover_terrain_block", "missing_block_item");
            var delta = new[] { D(input, -1), D(output, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("recover_terrain_block", "inventory_rejected");
            return Ok("recover_terrain_block", delta);
        }

        // Phase2ChemistryService.purify_contamination: no invented reagent cost.
        public WorldChemistryResult PurifyContamination(string input, float ambientRadiation)
        {
            string output;
            switch (input)
            {
                case "resource_contaminated_mineral": output = "resource_iron_bearing_ore"; break;
                case "resource_contaminated_solution": output = "resource_mineral_solution"; break;
                case "resource_oxidized_residue": output = "resource_rust_metal_residue"; break;
                default: return Fail("purify_contamination", "unsupported_contamination");
            }
            if (_state.CountItem(input) <= 0) return Fail("purify_contamination", "missing_contaminated_resource");
            if (float.IsNaN(ambientRadiation) || float.IsInfinity(ambientRadiation) || ambientRadiation > 0.85f)
                return Fail("purify_contamination", "radiation_too_high");
            var delta = new[] { D(input, -1), D(output, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("purify_contamination", "inventory_rejected");
            _state.AddChemistryKnowledge("purification_material_prepared");
            return Ok("purify_contamination", delta);
        }

        public WorldChemistryResult ExtremeHeatProcess(string input, float temperature)
        {
            if (_state.currentRegion != "mantle_flows" || float.IsNaN(temperature) || float.IsInfinity(temperature) || temperature < 0.90f) return Fail("extreme_heat_process", "extreme_heat_required");
            if (input != "element_s" && input != "ion_so4") return Fail("extreme_heat_process", "unsupported_extreme_heat_feedstock");
            if (input == "ion_so4" && _state.CountItem("compound_h2o") <= 0) return Fail("extreme_heat_process", "missing_water");
            if (_state.CountItem(input) <= 0) return Fail("extreme_heat_process", "missing_reactant");
            if (input == "element_s" && _state.CountGeneratedGas("compound_so2") == int.MaxValue) return Fail("extreme_heat_process", "counter_overflow");
            var delta = input == "element_s" ? new[] { D(input, -1) } : new[] { D(input, -1), D("compound_h2o", -1), D("compound_h2so4", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("extreme_heat_process", "missing_reactant");
            if (input == "element_s") _state.ChangeGeneratedGas("compound_so2", 1);
            _state.AddChemistryKnowledge("extreme_heat:" + input);
            return Send(new WorldChemistryResult { success = true, action_id = "extreme_heat_process", subject_id = input, source_event = "extreme_heat_processed", inventory_delta = delta });
        }

        public WorldChemistryResult ProcessLoadedSolution(string mode)
        {
            const string input = "compound_loaded_absorption_solution";
            if (_state.CountItem(input) <= 0) return Fail("process_loaded_solution", "missing_loaded_solution");
            if (mode != "distill" && mode != "concentrate" && mode != "neutralize") return Fail("process_loaded_solution", "unsupported_loaded_solution_process");
            string output = mode == "distill" ? "compound_distilled_water" : mode == "concentrate" ? "compound_concentrated_salt_solution" : "compound_neutralized_salt_solution";
            var delta = new[] { D(input, -1), D(output, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("process_loaded_solution", "inventory_rejected");
            _state.AddChemistryKnowledge("purification_discovery:" + mode);
            return Send(new WorldChemistryResult { success = true, action_id = "process_loaded_solution", source_event = "loaded_solution_processed", subject_id = mode, inventory_delta = delta });
        }

        public WorldChemistryResult RecycleMotherLiquor()
        {
            if (_state.CountItem("compound_mother_liquor") <= 0)
                return Fail("recycle_mother_liquor", "missing_mother_liquor");
            var delta = new[] { D("compound_mother_liquor", -1), D("compound_concentrated_salt_solution", 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("recycle_mother_liquor", "inventory_rejected");
            _state.AddChemistryKnowledge("mother_liquor_recycled");
            return Ok("recycle_mother_liquor", delta);
        }

        public static readonly string[] MineBlocks = { "item_abandoned_mine_iron_bearing_ore_block", "item_abandoned_mine_copper_bearing_ore_block",
            "item_abandoned_mine_zinc_bearing_ore_block", "item_abandoned_mine_silver_bearing_ore_block", "item_abandoned_mine_barium_bearing_mineral_block", "item_abandoned_mine_nitrate_deposit_block" };
        public WorldChemistryResult AnalyzeMineBlock(string input)
        {
            if (Array.IndexOf(MineBlocks, input) < 0) return Fail("analyze_mine_block", "not_terrain_block");
            if (_state.CountItem(input) <= 0) return Fail("analyze_mine_block", "missing_block_item");
            var delta = new[] { D(input, -1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("analyze_mine_block", "inventory_commit_failed");
            _state.IdentifySample(input);
            return Send(new WorldChemistryResult { success = true, action_id = "analyze_mine_block", subject_id = input, source_event = "terrain_block_analyzed", inventory_delta = delta });
        }
        OreProfile Ore(string id) { return GameData.OreProfiles.ores.FirstOrDefault(p => p.id == id); }
        public WorldChemistryResult IdentifyOre(string ore)
        {
            var profile = Ore(ore);
            if (profile == null) return Fail("identify_ore", "unknown_ore");
            if (_state.CountItem(ore) <= 0) return Fail("identify_ore", "missing_ore");
            _state.IdentifySample(ore); _state.AddChemistryKnowledge("ore_identified:" + ore);
            return Send(new WorldChemistryResult { success = true, action_id = "identify_ore", subject_id = ore, metal = profile.metal, source_event = "ore_identified" });
        }
        public WorldChemistryResult PretreatOre(string ore)
        {
            var profile = Ore(ore); if (profile == null) return Fail("pretreat_ore", "unknown_ore");
            if (_state.CountItem(ore) <= 0) return Fail("pretreat_ore", "missing_ore");
            var delta = new[] { D(ore, -1), D(profile.oxide, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("pretreat_ore", "inventory_rejected");
            _state.AddChemistryKnowledge("ore_pretreated:" + ore);
            return Ok("pretreat_ore", delta);
        }
        public WorldChemistryResult SmeltOre(string metalOrOre, bool raw = false)
        {
            var profile = GameData.OreProfiles.ores.Take(4).FirstOrDefault(p => p.metal == metalOrOre) ?? Ore(metalOrOre);
            if (profile == null) return Fail("smelt_ore", "unsupported_metal");
            string input = raw && (Ore(metalOrOre) != null || _state.CountItem(metalOrOre) > 0) ? metalOrOre : profile.oxide;
            if (raw && _state.CountItem(input) <= 0) return Fail("smelt_ore", "ore_pretreatment_required");
            if (_state.CountItem(input) <= 0 || _state.CountItem("element_c") <= 0) return Fail("smelt_ore", "missing_reactant");
            var delta = new[] { D(input, -1), D("element_c", -1), D(profile.metal, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("smelt_ore", "inventory_rejected");
            _state.AddChemistryKnowledge("metal_recovery:" + profile.metal);
            return Send(new WorldChemistryResult { success = true, action_id = "smelt_ore", subject_id = metalOrOre, metal = profile.metal, source_event = "metal_recovered", inventory_delta = delta });
        }
        public WorldChemistryResult PretreatIronOre() { return PretreatOre("resource_iron_bearing_ore"); }
        public WorldChemistryResult SmeltIron() { return SmeltOre("element_fe"); }
        public WorldChemistryResult MetalDisplacement(string active, string target)
        {
            var activity = new Dictionary<string, int> { { "element_zn", 3 }, { "element_fe", 2 }, { "element_cu", 1 }, { "element_ag", 0 } };
            string metal = target == "compound_cuso4" ? "element_cu" : target == "compound_agno3" ? "element_ag" : target == "compound_fe2o3" ? "element_fe" : (target ?? "").Replace("ion_", "element_");
            if (!activity.ContainsKey(active ?? "") || !activity.ContainsKey(metal)) return Fail("metal_displacement", "unsupported_displacement");
            string key = active + ">" + target;
            if (activity[active] <= activity[metal])
            {
                _state.AddChemistryKnowledge("no_reaction:" + key);
                return Send(new WorldChemistryResult { success = false, reason = "no_reaction", action_id = "metal_displacement", source_event = "metal_displacement", discovery = key });
            }
            if (_state.CountItem(active) <= 0 || _state.CountItem(target) <= 0) return Fail("metal_displacement", "missing_reactant");
            string salt = target == "compound_cuso4" && active == "element_zn" ? "compound_znso4" : "compound_" + active.Replace("element_", "") + "cl2";
            var delta = new[] { D(active, -1), D(target, -1), D(metal, 1), D(salt, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("metal_displacement", "inventory_rejected");
            _state.AddChemistryKnowledge("displacement:" + key);
            return Send(new WorldChemistryResult { success = true, action_id = "metal_displacement", source_event = "metal_displacement", discovery = key, metal = metal, inventory_delta = delta });
        }
        public WorldChemistryResult PrecipitationTest(string cation, string anion)
        {
            string product = cation == "element_ba" && anion == "ion_so4" ? "compound_baso4" :
                cation == "element_ag" && (anion == "element_cl" || anion == "compound_hcl") ? "compound_agcl" :
                cation == "element_cu" && anion == "ion_so4" ? "compound_cuso4" : "";
            if (product == "") return Fail("precipitation_test", "no_precipitate");
            if (_state.CountItem(cation) <= 0 || _state.CountItem(anion) <= 0) return Fail("precipitation_test", "missing_reactant");
            var delta = new[] { D(cation, -1), D(anion, -1), D(product, 1) };
            if (!_state.TryApplyInventoryDelta(delta)) return Fail("precipitation_test", "inventory_rejected");
            _state.AddChemistryKnowledge("precipitation_test:" + product);
            return Send(new WorldChemistryResult { success = true, action_id = "precipitation_test", source_event = "precipitation_test", subject_id = product, inventory_delta = delta });
        }

        WorldChemistryResult Send(WorldChemistryResult result) { Publish(result); return result; }

        WorldChemistryResult Ok(string actionId, InventoryDeltaEntry[] delta, int heatUnits = 0, bool hasConcentration = false, float concentration = 0f)
        {
            var result = new WorldChemistryResult { success = true, reason = "", action_id = actionId,
                inventory_delta = Clone(delta), heat_units = heatUnits, has_concentration = hasConcentration, concentration = concentration };
            Publish(result);
            return result;
        }

        WorldChemistryResult Fail(string actionId, string reason)
        {
            var result = new WorldChemistryResult { success = false, reason = reason, action_id = actionId };
            Publish(result);
            return result;
        }

        void Publish(WorldChemistryResult result)
        {
            var handler = GameplayEventPublished;
            if (handler == null) return;
            foreach (Action<WorldChemistryResult> subscriber in handler.GetInvocationList())
                subscriber(new WorldChemistryResult { success = result.success, reason = result.reason,
                    action_id = result.action_id, inventory_delta = Clone(result.inventory_delta), heat_units = result.heat_units,
                    has_concentration = result.has_concentration, concentration = result.concentration, subject_id = result.subject_id,
                    discovery = result.discovery, metal = result.metal, source_event = result.source_event });
        }

        static InventoryDeltaEntry D(string id, int change) { return new InventoryDeltaEntry(id, change); }
        static InventoryDeltaEntry[] Clone(IEnumerable<InventoryDeltaEntry> rows)
        {
            var result = new List<InventoryDeltaEntry>();
            foreach (var row in rows ?? new InventoryDeltaEntry[0]) result.Add(D(row.id, row.change));
            return result.ToArray();
        }
    }
}
