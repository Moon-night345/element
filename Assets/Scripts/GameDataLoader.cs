// 元素星海·裂核 — Unity 运行时数据加载器
// 将 unity_adapter/data/*.json 放入 Unity 项目的 Assets/Resources/Data/ 后,
// 通过 GameData.LoadAll() 一次性加载全部游戏内容。
//
// 依赖:Newtonsoft.Json(Package Manager → com.unity.nuget.newtonsoft-json)
// 或使用内置 JsonUtility(需要字段名完全匹配,本文件已按此设计)。

using System;
using System.IO;
using UnityEngine;

namespace ElementalStarSea.Data
{
    public static class GameData
    {
        public static ShardsDatabase Shards { get; private set; }
        public static MemoriesDatabase Memories { get; private set; }
        public static EndingsDatabase Endings { get; private set; }
        public static RegionsDatabase Regions { get; private set; }
        public static ReactionsDatabase Reactions { get; private set; }
        public static ReactionContentDatabase ReactionContent { get; private set; }
        public static DevicesDatabase Devices { get; private set; }
        public static ProcessingRecipesDatabase ProcessingRecipes { get; private set; }
        public static MigrationManifest Manifest { get; private set; }
        public static QuestChainDatabase QuestChain { get; private set; }

        /// <summary>从 Resources/Data/ 加载全部 JSON(编辑器下也可用绝对路径)。</summary>
        public static OreProfilesDatabase OreProfiles { get; private set; }
        public static void LoadAll()
        {
            Shards   = Load<ShardsDatabase>("shards");
            Memories = Load<MemoriesDatabase>("memories");
            Endings  = Load<EndingsDatabase>("endings");
            Regions  = Load<RegionsDatabase>("regions");
            Reactions = Load<ReactionsDatabase>("reactions");
            ReactionContent = Load<ReactionContentDatabase>("reaction_content");
            Devices = Load<DevicesDatabase>("devices");
            ProcessingRecipes = Load<ProcessingRecipesDatabase>("processing_recipes");
            OreProfiles = Load<OreProfilesDatabase>("ore_profiles");
            Manifest = Load<MigrationManifest>("migration_manifest");
            QuestChain = Load<QuestChainDatabase>("quest_chain");
            Debug.Log($"[GameData] Loaded: {Shards.shards.Length} shards, " +
                      $"{Memories.memories.Length} memories, " +
                      $"{Regions.regions.Length} regions, " +
                      $"{Reactions.reactions.Length} reactions, " +
                      $"{Devices.devices.Length} devices, " +
                      $"{ProcessingRecipes.recipes.Length} processing recipes, " +
                      $"{QuestChain.steps.Length} quest steps");
        }

        static T Load<T>(string name) where T : class
        {
            // TextAsset 路径(发布版):Assets/Resources/Data/<name>.json
            var asset = Resources.Load<TextAsset>($"Data/{name}");
            if (asset != null)
            {
                var result = JsonUtility.FromJson<T>(asset.text);
                Resources.UnloadAsset(asset);
                return result;
            }
            // 编辑器回退:直接读文件系统
            var path = Path.Combine(Application.dataPath, $"unity_adapter/data/{name}.json");
            if (File.Exists(path))
                return JsonUtility.FromJson<T>(File.ReadAllText(path));
            Debug.LogError($"[GameData] 未找到 {name}.json");
            return null;
        }

    }
}
