using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace ElementalStarSea.Game
{
    public partial class GameUI
    {
        static readonly Dictionary<string,string> RecipeNames = new Dictionary<string,string> {
            {"recipe_phase4b_device12_co2_h2_conversion","高压催化转化"}, {"recipe_stage0_furnace_kit","制作小型熔炉套件"},
            {"recipe_stage0_charging_station_kit","制作充能台套件"}, {"recipe_stage0_reaction_bench_kit","制作反应台套件"},
            {"recipe_phase2_detector_kit","制作检测仪套件"}, {"recipe_phase2_filter_kit","制作过滤器套件"}, {"recipe_phase2_distiller_kit","制作蒸馏器套件"},
            {"recipe_phase2_detect","检测样本"}, {"recipe_phase2_filter","过滤混合物"}, {"recipe_phase2_distill","蒸馏溶液"},
            {"recipe_phase3a_condenser_kit","制作冷凝器套件"}, {"recipe_phase3a_gas_tank_kit","制作储气罐套件"},
            {"recipe_phase3a_condense_vapor","冷凝蒸气"}, {"recipe_phase3a_store_gas","储存气体"}, {"recipe_phase3a_thermal_carbonate","热解碳酸盐"},
            {"recipe_abandoned_mine_iron_ore","预处理含铁矿物"}, {"recipe_abandoned_mine_copper_ore","预处理含铜矿物"},
            {"recipe_phase4b_core_high_strength_preprocess","预处理高强度矿物"}, {"recipe_phase4b_silver_bearing_preprocess","预处理含银矿物"},
            {"recipe_phase3b_electrolysis_cell_kit","制作电解槽套件"}, {"recipe_phase3b_reactor_kit","制作工业反应釜套件"},
            {"recipe_phase3b_electrolyze","工业电解"}, {"recipe_phase4b_silver_catalytic_refine","精炼银催化材料"},
            {"recipe_phase3b_reactor_batch","批量还原反应"}, {"barium_sulfate_batch_precipitation","硫酸钡批量沉淀"},
            {"recipe_phase4b_high_strength_conditioning","制备高强度材料"}, {"recipe_phase4a_absorption_tower_kit","制作吸收塔套件"},
            {"recipe_phase4a_absorb_gas","吸收处理气体"}, {"recipe_phase4a_crystallizer_kit","制作结晶器套件"},
            {"recipe_phase4a_industrial_crystallize","工业结晶"}, {"recipe_phase4b_high_pressure_reactor_kit","制作高压反应器套件"},
            {"recipe_phase4b_high_pressure_nh3","高压合成氨"}, {"recipe_phase4b_liquid_tank_kit","制作大型储液罐套件"}
        };
        public static string RecipeLabel(string id) { string name; return RecipeNames.TryGetValue(id ?? "", out name) ? name : "设备工艺"; }
        static string DeviceStateLabel(Data.DeviceProcessingState state)
        {
            switch(state) { case Data.DeviceProcessingState.Processing: return "加工中"; case Data.DeviceProcessingState.OutputReady: return "产物待领取";
                case Data.DeviceProcessingState.ConditionMissing: return "条件不足"; case Data.DeviceProcessingState.InvalidInput: return "原料不适用";
                case Data.DeviceProcessingState.Error: return "操作未完成"; case Data.DeviceProcessingState.Cancelled: return "已取消"; default: return "待机"; }
        }
        static string DeviceFailureLabel(string reason)
        {
            string[] parts = (reason ?? "").Split(':');
            switch(parts[0]) {
                case "laboratory_stage_locked": return "实验室阶段不足，请先推进实验室升级";
                case "missing_crafting_input": case "missing_input": case "missing_reactant": return "原料不足，请先收集所需材料";
                case "missing_process_input": return "缺少工艺材料" + (parts.Length > 1 ? "：" + Data.ChemistryEngine.DisplayName(parts[1]) : "");
                case "unlock_region_required": return "需先探索对应区域"; case "workbench_required": return "请先制造基础工作台";
                case "already_processing": return "设备正在加工，请稍候"; case "output_not_collected": return "请先领取上一批产物";
                case "output_not_ready": return "产物尚未完成"; case "device_not_built": case "device_not_operational": return "请先制造并部署设备";
                case "storage_capacity_exceeded": return "储罐已满"; case "stored_item_unavailable": return "储罐中没有足够物质";
                case "incompatible_stored_item": return "请先取出储罐内的另一种物质"; case "invalid_physical_state": return "该储罐只能存放液体";
                case "high_pressure_required": return "需要高压条件"; case "catalyst_required": return "缺少催化材料";
                case "condition_missing": return "当前工艺条件未满足"; case "inventory_rejected": return "背包无法接收本次物质";
                default: return "请检查所需材料、实验室阶段及设备条件";
            }
        }
        Text MakeScrollText(GameObject parent, string name, Vector2 position, float width, float height, int fontSize)
        {
            var viewport = new GameObject(name + "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent.transform, false); var vr = viewport.GetComponent<RectTransform>(); vr.anchoredPosition = position; vr.sizeDelta = new Vector2(width, height);
            viewport.GetComponent<Image>().color = new Color(0,0,0,.08f);
            var text = MakePanelText(viewport, name, Vector2.zero, width - 16, height, TextAnchor.UpperLeft, fontSize);
            var tr = text.rectTransform; tr.anchorMin = new Vector2(0,1); tr.anchorMax = new Vector2(1,1); tr.pivot = new Vector2(.5f,1); tr.sizeDelta = new Vector2(-16,0); tr.anchoredPosition = new Vector2(0,-4);
            text.raycastTarget = false; text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.content = tr; scroll.viewport = vr; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            return text;
        }
    }
}
