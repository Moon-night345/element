// 元素星海·裂核 — 化学反应引擎(纯逻辑,可脱离场景测试)
// 语义与 Godot reaction_service.gd 一致:选中的物质多重集必须与配方 reactants 精确相等。
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    public static class ChemistryEngine
    {
        static Dictionary<string, MaterialDef> _materials = new Dictionary<string, MaterialDef>();
        static Dictionary<string, ReactionRecipe> _recipes = new Dictionary<string, ReactionRecipe>();
        static List<ReactionRecipe> _recipeList = new List<ReactionRecipe>();

        public static void Load(ChemistryDatabase db)
        {
            _materials.Clear(); _recipes.Clear(); _recipeList.Clear();
            if (db == null) return;
            foreach (var m in db.materials) _materials[m.id] = m;
            foreach (var r in db.reactions) { _recipes[r.id] = r; _recipeList.Add(r); }
        }

        public static MaterialDef Material(string id)
        {
            MaterialDef m;
            return _materials.TryGetValue(id, out m) ? m : null;
        }

        public static IList<ReactionRecipe> Recipes { get { return _recipeList; } }

        public static ReactionRecipe Recipe(string id)
        {
            ReactionRecipe r;
            return _recipes.TryGetValue(id, out r) ? r : null;
        }

        /// <summary>物质显示名:中文名 + 符号。</summary>
        public static string DisplayName(string materialId)
        {
            var m = Material(materialId);
            if (m == null) return materialId;
            return m.name;
        }

        /// <summary>
        /// 尝试将选中物质多重集匹配到唯一配方。
        /// 精确匹配:每种物质数量一致才算命中,与 Godot 合成语义相同。
        /// </summary>
        public static ReactionRecipe MatchExact(IEnumerable<string> selectedIds)
        {
            var counts = CountMultiset(selectedIds);
            if (counts.Count == 0) return null;
            foreach (var r in _recipeList)
            {
                if (r.reactants.Length != counts.Count) continue;
                bool allSame = true;
                foreach (var q in r.reactants)
                {
                    int have;
                    if (!counts.TryGetValue(q.id, out have) || have != q.n) { allSame = false; break; }
                }
                if (allSame) return r;
            }
            return null;
        }

        /// <summary>库存是否足够支付配方 reactants。</summary>
        public static bool CanAfford(IInventoryView inv, ReactionRecipe r)
        {
            foreach (var q in r.reactants)
                if (inv.CountItem(q.id) < q.n) return false;
            return true;
        }

        /// <summary>与 Godot FullReactionService._energy_cost 完全一致。</summary>
        public static float EnergyCost(ReactionRecipe recipe)
        {
            if (recipe == null) return 0f;
            if (recipe.id == "reaction_h2_synthesis") return 10f;
            return 5f + (recipe.tags != null ? recipe.tags.Length : 0) * 3f +
                   (recipe.reactants != null ? recipe.reactants.Length : 0);
        }

        static Dictionary<string, int> CountMultiset(IEnumerable<string> ids)
        {
            var dict = new Dictionary<string, int>();
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                int c;
                dict[id] = (dict.TryGetValue(id, out c) ? c : 0) + 1;
            }
            return dict;
        }
    }

    /// <summary>库存只读视图(引擎不依赖具体存档类)。</summary>
    public interface IInventoryView
    {
        int CountItem(string materialId);
    }
}
