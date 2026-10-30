// Godot MechanicalToolCatalog 与三档正式采样镐的 Unity 等价层。
using System;
using System.Collections.Generic;

namespace ElementalStarSea.Data
{
    [Serializable]
    public sealed class MechanicalToolStats
    {
        public string id;
        public int mining_power;
        public float mining_speed;
        public float heat;
        public float cold;
        public float corrosion;
        public float radiation;
        public float electrical;
    }

    [Serializable]
    public sealed class MechanicalToolResult
    {
        public bool success;
        public string reason = "";
        public string tool_id = "";
    }

    public sealed class MechanicalToolService
    {
        public const string BaseTool = "tool_geological_sampling_pickaxe";
        public const string ImprovedTool = "tool_improved_iron_pickaxe";
        public const string ReinforcedTool = "tool_reinforced_composite_pickaxe";
        public const string ImprovedRecipe = "recipe_improved_iron_pickaxe";
        public const string ReinforcedRecipe = "recipe_reinforced_composite_pickaxe";

        static readonly Dictionary<string, MechanicalToolStats> Tools = new Dictionary<string, MechanicalToolStats>
        {
            { BaseTool, S(BaseTool, 1, 1.00f, 0.20f, 0.20f, 0.20f, 0.15f, 0.15f) },
            { ImprovedTool, S(ImprovedTool, 2, 1.35f, 0.45f, 0.25f, 0.45f, 0.40f, 0.40f) },
            { ReinforcedTool, S(ReinforcedTool, 3, 1.70f, 0.70f, 0.70f, 0.70f, 0.65f, 0.65f) }
        };

        readonly GameState _state;

        public MechanicalToolService(GameState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(_state.equippedToolId) || !Tools.ContainsKey(_state.equippedToolId))
                _state.equippedToolId = BaseTool;
        }

        public MechanicalToolStats EquippedStats
        {
            get { return Clone(Tools.ContainsKey(_state.equippedToolId) ? Tools[_state.equippedToolId] : Tools[BaseTool]); }
        }

        public MechanicalToolResult Craft(string recipeId, bool atWorkstation)
        {
            if (!atWorkstation) return Fail("workstation_required");
            string output;
            RecipeQuantity[] ingredients;
            if (recipeId == ImprovedRecipe)
            {
                output = ImprovedTool;
                ingredients = new[] { Q("element_fe", 4) };
            }
            else if (recipeId == ReinforcedRecipe)
            {
                output = ReinforcedTool;
                ingredients = new[] { Q("element_fe", 6), Q("element_cu", 3) };
            }
            else return Fail("invalid_recipe");
            if (_state.HasOwnedTool(output)) return Fail("already_owned", output);
            if (!_state.RemoveItems(ingredients)) return Fail("missing_elements", output);
            _state.AddOwnedTool(output);
            return Ok(output);
        }

        public MechanicalToolResult Equip(string toolId)
        {
            if (!Tools.ContainsKey(toolId ?? "")) return Fail("invalid_tool", toolId);
            if (!_state.HasOwnedTool(toolId)) return Fail("tool_not_owned", toolId);
            _state.equippedToolId = toolId;
            return Ok(toolId);
        }

        public float Resistance(string id)
        {
            var tool = EquippedStats;
            switch (id)
            {
                case "heat": return tool.heat;
                case "cold": return tool.cold;
                case "corrosion": return tool.corrosion;
                case "radiation": return tool.radiation;
                case "electrical": return tool.electrical;
                default: return 0f;
            }
        }

        static MechanicalToolStats S(string id, int power, float speed, float heat, float cold,
            float corrosion, float radiation, float electrical)
        {
            return new MechanicalToolStats { id = id, mining_power = power, mining_speed = speed,
                heat = heat, cold = cold, corrosion = corrosion, radiation = radiation, electrical = electrical };
        }

        static MechanicalToolStats Clone(MechanicalToolStats value)
        {
            return S(value.id, value.mining_power, value.mining_speed, value.heat, value.cold,
                value.corrosion, value.radiation, value.electrical);
        }

        static RecipeQuantity Q(string id, int amount) { return new RecipeQuantity { id = id, n = amount }; }
        static MechanicalToolResult Ok(string tool) { return new MechanicalToolResult { success = true, tool_id = tool }; }
        static MechanicalToolResult Fail(string reason, string tool = "")
        {
            return new MechanicalToolResult { success = false, reason = reason, tool_id = tool ?? "" };
        }
    }
}
