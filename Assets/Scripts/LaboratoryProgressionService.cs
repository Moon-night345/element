// Godot LaboratoryProgressState / UndergroundProgressState 的 Unity 等价进阶层。
// 阶段只由明确里程碑和成功的化学事件推进，不由任务步数、区域访问或设备所有权推断。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    [Serializable]
    public sealed class LaboratoryProgressionResult
    {
        public bool success;
        public string reason = "";
        public int laboratory_stage;
        public string capability_id = "";
    }

    public sealed class LaboratoryProgressionService
    {
        public const string PreciseAnalysis = "PRECISE_ANALYSIS";
        public const string Distillation = "DISTILLATION";
        public const string SolidLiquidFiltration = "SOLID_LIQUID_FILTRATION";
        public const string ThermalProcessing = "THERMAL_PROCESSING";
        public const string GasHandling = "GAS_HANDLING";
        public const string Condensation = "CONDENSATION";
        public const string NaturalCrystallization = "NATURAL_CRYSTALLIZATION";
        public const string BatchReaction = "BATCH_REACTION";
        public const string Electrochemistry = "ELECTROCHEMISTRY";

        public static readonly string[] Stage1To2Capabilities =
        {
            PreciseAnalysis, Distillation, SolidLiquidFiltration
        };

        public static readonly string[] Stage2To3Capabilities =
        {
            PreciseAnalysis, Distillation, SolidLiquidFiltration, ThermalProcessing,
            GasHandling, Condensation, NaturalCrystallization, BatchReaction, Electrochemistry
        };

        readonly GameState _state;

        public LaboratoryProgressionService(GameState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            // Godot WorldSession 新世界初始化时直接解锁这两台 Stage 0 装置。
            _state.AddLaboratoryMilestone("stage0_workbench_unlocked");
            _state.AddLaboratoryMilestone("stage0_reaction_bench_unlocked");
        }

        public bool RecordPickaxeRepaired()
        {
            return _state.AddLaboratoryMilestone("geological_pickaxe_repaired");
        }

        public bool RecordCorrosionGateDiscovered()
        {
            return _state.AddLaboratoryMilestone("corrosion_gate_discovered");
        }

        public bool RecordCorrosionRawMaterials()
        {
            bool hasFormalPair = _state.CountItem("element_fe") > 0 && _state.CountItem("element_cl") > 0;
            bool hasWorldPair = _state.CountItem("resource_rust_metal_residue") > 0 &&
                                _state.CountItem("resource_chloride_deposit") > 0;
            if (!hasFormalPair && !hasWorldPair) return false;
            return _state.AddLaboratoryMilestone("corrosion_raw_materials_collected");
        }

        public LaboratoryProgressionResult ProcessCorrosionBase()
        {
            InventoryDeltaEntry[] delta;
            if (_state.CountItem("element_fe") > 0 && _state.CountItem("element_cl") > 0)
                delta = new[] { D("element_fe", -1), D("element_cl", -1), D("processed_corrosion_base", 1) };
            else if (_state.CountItem("resource_rust_metal_residue") > 0 &&
                     _state.CountItem("resource_chloride_deposit") > 0)
                delta = new[] { D("resource_rust_metal_residue", -1), D("resource_chloride_deposit", -1),
                    D("processed_corrosion_base", 1) };
            else return Fail("missing_corrosion_raw_materials");

            if (!_state.TryApplyInventoryDelta(delta)) return Fail("inventory_rejected");
            _state.AddLaboratoryMilestone("corrosion_raw_materials_collected");
            _state.AddLaboratoryMilestone("corrosion_base_processed");
            return Ok();
        }

        public LaboratoryProgressionResult ProcessCorrosionCoating()
        {
            if (_state.CountItem("processed_corrosion_base") <= 0) return Fail("missing_corrosion_base");
            if (!_state.TryApplyInventoryDelta(new[] { D("processed_corrosion_base", -1),
                    D("processed_corrosion_coating", 1) })) return Fail("inventory_rejected");
            _state.AddLaboratoryMilestone("corrosion_coating_processed");
            return Ok();
        }

        public LaboratoryProgressionResult UpgradeCorrosionPickaxe()
        {
            if (_state.currentRegion != "prologue_lab") return Fail("laboratory_required");
            if (!_state.HasLaboratoryMilestone("stage0_workbench_unlocked")) return Fail("workbench_locked");
            if (!_state.HasLaboratoryMilestone("geological_pickaxe_repaired")) return Fail("pickaxe_not_repaired");
            if (!_state.HasLaboratoryMilestone("corrosion_gate_discovered")) return Fail("corrosion_gate_not_discovered");
            if (!_state.HasLaboratoryMilestone("corrosion_raw_materials_collected")) return Fail("raw_materials_not_discovered");
            if (_state.HasLaboratoryMilestone("corrosion_pickaxe_upgraded")) return Fail("already_upgraded");
            if (_state.CountItem("processed_corrosion_coating") <= 0) return Fail("missing_upgrade_material");
            if (!_state.TryApplyInventoryDelta(new[] { D("processed_corrosion_coating", -1) }))
                return Fail("inventory_rejected");

            _state.AddLaboratoryMilestone("corrosion_pickaxe_upgraded");
            _state.AddLaboratoryMilestone("first_corrosion_material");
            _state.SetToolResistance("corrosion", 0.30f);
            _state.laboratoryStage = Math.Max(_state.laboratoryStage, 1);
            return Ok();
        }

        public bool RecordDeviceEvent(DeviceGameplayEvent gameplayEvent)
        {
            if (gameplayEvent == null || gameplayEvent.event_type != "device_output_collected") return false;
            string capability = CapabilityForRecipe(gameplayEvent.recipe_id);
            return !string.IsNullOrEmpty(capability) && AcquireCapability(capability);
        }

        public bool RecordWorldChemistryEvent(WorldChemistryResult gameplayEvent)
        {
            if (gameplayEvent == null || !gameplayEvent.success) return false;
            if (gameplayEvent.action_id == "natural_crystallization")
                return AcquireCapability(NaturalCrystallization);
            return false;
        }

        public bool AcquireCapability(string capabilityId)
        {
            if (!_state.AddLaboratoryCapability(capabilityId)) return false;
            EvaluateTransitions();
            return true;
        }

        public void EvaluateTransitions()
        {
            if (_state.laboratoryStage == 1 && Stage1To2Capabilities.All(_state.HasLaboratoryCapability))
                _state.laboratoryStage = 2;
            if (_state.laboratoryStage == 2 && Stage2To3Capabilities.All(_state.HasLaboratoryCapability))
                _state.laboratoryStage = 3;
        }

        static string CapabilityForRecipe(string recipeId)
        {
            switch (recipeId)
            {
                case "recipe_phase2_detect": return PreciseAnalysis;
                case "recipe_phase2_filter": return SolidLiquidFiltration;
                case "recipe_phase2_distill": return Distillation;
                case "recipe_phase3a_thermal_carbonate": return ThermalProcessing;
                case "recipe_phase3a_store_gas": return GasHandling;
                case "recipe_phase3a_condense_vapor": return Condensation;
                case "recipe_phase3b_reactor_batch":
                case "barium_sulfate_batch_precipitation": return BatchReaction;
                case "recipe_phase3b_electrolyze": return Electrochemistry;
                default: return "";
            }
        }

        LaboratoryProgressionResult Ok()
        {
            return new LaboratoryProgressionResult { success = true, laboratory_stage = _state.laboratoryStage };
        }

        LaboratoryProgressionResult Fail(string reason)
        {
            return new LaboratoryProgressionResult { success = false, reason = reason,
                laboratory_stage = _state.laboratoryStage };
        }

        static InventoryDeltaEntry D(string id, int change) { return new InventoryDeltaEntry(id, change); }
    }
}
