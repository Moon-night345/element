// 元素星海·裂核 — 化学数据模型(chemistry.json / localization_zh.json)
// 与 tools/export_chem_data.py 的导出结构一一对应,JsonUtility 直接反序列化。
using System;

namespace ElementalStarSea.Data
{
    [Serializable]
    public class ChemistryDatabase
    {
        public MaterialDef[] materials;
        public ReactionRecipe[] reactions;
    }

    [Serializable]
    public class MaterialDef
    {
        public string id;      // "element_h" / "compound_h2o" / "ion_oh"
        public string kind;    // "element" / "ion" / "compound"
        public string symbol;  // "H" / "H₂O"
        public string name;    // "氢"
    }

    [Serializable]
    public class ReactionRecipe
    {
        public string id;              // "reaction_water_synthesis"
        public string branch;          // "A"-"H" 化学分支
        public RecipeQuantity[] reactants;
        public RecipeQuantity[] products;
        public string[] tags;            // 必须全部满足的条件
        public string[] any_tags;        // 至少满足一个的条件
        public string language;        // 反应语言(碎片谜底)
        public string shard_id;
        public string memory_id;
        public string title;
    }

    [Serializable]
    public class RecipeQuantity
    {
        public string id;
        public int n;
    }

    [Serializable]
    public class LocalizationDatabase
    {
        public LocEntry[] entries;
    }

    [Serializable]
    public class LocEntry
    {
        public string key;
        public string text;
    }
}
