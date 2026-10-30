// Godot WorldContentCatalog / RegionRouteContract / SurvivalState 的 Unity 等价运行时。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    [Serializable]
    public sealed class RegionAccessResult
    {
        public bool allowed;
        public string reason = "";
        public string region_id = "";
    }

    [Serializable]
    public sealed class MiningAccessResult
    {
        public bool allowed;
        public string reason = "";
        public string environment_type = "";
        public float required_resistance;
        public float actual_resistance;
    }

    public sealed class RegionEnvironmentService
    {
        static readonly Dictionary<string, string> FailureReasons = new Dictionary<string, string>
        {
            { "heat", "heat_resistance_too_low" }, { "cold", "cold_resistance_too_low" },
            { "corrosion", "corrosion_resistance_too_low" },
            { "radiation", "radiation_resistance_too_low" },
            { "electrical", "electrical_resistance_too_low" }
        };

        readonly GameState _state;
        readonly Dictionary<string, RegionDef> _regions;
        readonly MechanicalToolService _tools;

        public RegionEnvironmentService(GameState state, RegionsDatabase regions)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _regions = (regions != null && regions.regions != null ? regions.regions : new RegionDef[0])
                .ToDictionary(value => value.id, value => value);
            _tools = new MechanicalToolService(_state);
        }

        public float EffectiveToolResistance(string id)
        {
            float baseline = _tools.Resistance(id);
            float persisted = _state.ToolResistance(id);
            if (id == "corrosion" && _state.HasLaboratoryMilestone("corrosion_pickaxe_upgraded"))
                persisted = Math.Max(persisted, 0.30f);
            return Math.Max(baseline, persisted);
        }

        public MiningAccessResult MiningAccess(string regionId)
        {
            RegionDef region;
            if (!_regions.TryGetValue(regionId ?? "", out region))
                return MiningFail("unknown_region", "", 0f, 0f);
            foreach (var requirement in region.mining_requirements ?? new ConditionThreshold[0])
            {
                if (requirement == null || string.IsNullOrEmpty(requirement.id)) continue;
                float actual = EffectiveToolResistance(requirement.id);
                if (actual + 0.0001f < requirement.minimum)
                {
                    string reason;
                    if (!FailureReasons.TryGetValue(requirement.id, out reason)) reason = "environment_resistance_too_low";
                    return MiningFail(reason, requirement.id, requirement.minimum, actual);
                }
            }
            return new MiningAccessResult { allowed = true };
        }

        public RegionAccessResult RegionAccess(string regionId)
        {
            RegionDef region;
            if (!_regions.TryGetValue(regionId ?? "", out region)) return AccessFail(regionId, "unknown_region");
            if (regionId == _state.currentRegion) return AccessFail(regionId, "already_here");
            if (region.depth >= 5 && _state.radiationProtection < 0.35f)
                return AccessFail(regionId, "radiation_protection_required");
            if (regionId == "core_shell" && _state.laboratoryStage < 3)
                return AccessFail(regionId, "laboratory_stage_locked");
            if (regionId == "planetary_core")
            {
                if (_state.laboratoryStage < 3) return AccessFail(regionId, "laboratory_stage_locked");
                if (!FinalCorePreparationReady()) return AccessFail(regionId, "final_core_preparation_required");
            }
            return new RegionAccessResult { allowed = true, region_id = regionId };
        }

        public bool UpgradeRadiationProtection()
        {
            if (_state.radiationProtection >= 0.8f) return false;
            var cost = new[] { new RecipeQuantity { id = "resource_stone", n = 3 },
                               new RecipeQuantity { id = "resource_ore", n = 1 } };
            if (!_state.RemoveItems(cost)) return false;
            _state.radiationProtection = Math.Min(0.8f, _state.radiationProtection + 0.2f);
            return true;
        }

        public float TickExposure(RegionDef region, float deltaSeconds, float protectiveFactor = 0f)
        {
            if (region == null || !SurvivalRuntimeService.Finite(deltaSeconds) || deltaSeconds <= 0 ||
                !SurvivalRuntimeService.Finite(region.radiation) || !SurvivalRuntimeService.Finite(protectiveFactor) ||
                !SurvivalRuntimeService.Finite(_state.hp) || !SurvivalRuntimeService.Finite(_state.radiation) || !SurvivalRuntimeService.Finite(_state.radiationProtection)) return 0;
            var survival = new SurvivalRuntimeService(_state);
            float damage = survival.Tick(deltaSeconds, region.radiation, protectiveFactor);
            if (region.id == "rust_jungle")
            {
                float multiplier = EffectiveToolResistance("corrosion") >= 0.30f ? 0.35f : 1f;
                float corrosion = 1.2f * deltaSeconds * multiplier;
                if (survival.ApplyDamage(corrosion)) damage += corrosion;
            }
            return damage;
        }

        public bool FinalCorePreparationReady()
        {
            return FinalCorePreparationStatus().Values.All(value => value);
        }

        public Dictionary<string, bool> FinalCorePreparationStatus()
        {
            // WorldSession.final_core_preparation_status: successful storage grants logistics;
            // mere tank ownership and ordinary thermal processing are not sufficient.
            return new Dictionary<string, bool>
            {
                { "purification_material", _state.HasChemistryKnowledge("purification_material_prepared") },
                { "gas_absorption", HasKnowledgePrefix("absorption:") },
                { "high_purity_material", HasKnowledgePrefix("industrial_crystallization:") },
                { "mother_liquor_recycled", _state.HasChemistryKnowledge("mother_liquor_recycled") },
                { "metallurgy", HasKnowledgePrefix("metal_recovery:") },
                { "electrochemistry", _state.HasChemistryKnowledge("electrolysis_completed") },
                { "high_pressure_recipe_completed", _state.HasChemistryKnowledge("pressure:reaction_nh3_from_n2") },
                { "catalyst_used_correctly", _state.HasChemistryKnowledge("catalyst:material_iron") },
                { "logistics", _state.HasChemistryKnowledge("bulk_liquid_stored") || _state.HasChemistryKnowledge("bulk_liquid_reused") },
                { "device_12_operational", HasOperationalDevice("device_high_pressure_catalytic_reactor") },
                { "device_15_operational", HasOperationalDevice("device_large_industrial_liquid_tank") }
            };
        }

        bool HasOperationalDevice(string id)
        {
            return (_state.deviceInstances ?? new DeviceRuntimeContract[0]).Any(value => value != null &&
                value.device_id == id && value.is_unlocked && value.is_placed);
        }

        bool HasKnowledgePrefix(string prefix)
        {
            return (_state.chemistryKnowledgeIds ?? new string[0]).Any(value =>
                value != null && value.StartsWith(prefix, StringComparison.Ordinal));
        }

        static RegionAccessResult AccessFail(string region, string reason)
        {
            return new RegionAccessResult { allowed = false, reason = reason, region_id = region ?? "" };
        }

        static MiningAccessResult MiningFail(string reason, string type, float required, float actual)
        {
            return new MiningAccessResult { allowed = false, reason = reason, environment_type = type,
                required_resistance = required, actual_resistance = actual };
        }
    }
}
