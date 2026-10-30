// 元素星海·裂核 — Unity 数据模型
// 与 unity_adapter/data/*.json 一一对应的 C# 数据类。
// 使用 JsonUtility.FromJson<T>(json) 直接反序列化。
//
// 注意:JsonUtility 不支持 Dictionary,故嵌套结构一律用数组。

using System;

namespace ElementalStarSea.Data
{
    // ───────────────────────── shards.json ─────────────────────────

    [Serializable]
    public class ShardsDatabase
    {
        public ShardDef[] shards;
    }

    [Serializable]
    public class ShardDef
    {
        public string id;          // "shard_cleanser"
        public string key;         // "cleanser"
        public string name_key;    // 本地化键 → "清洗者"
        public ShardDialogue dialogue;
        public string language;    // 该碎片对应的化学语言词
        public int depth_tier;     // 0=地表 1=浅层 2=深层
    }

    [Serializable]
    public class ShardDialogue
    {
        public string persona;     // 自我介绍
        public string prompt;      // 它被困在什么指令里(谜题)
        public string accepted;    // 回答正确
        public string rejected;    // 回答错误(软失败)
        public string calmed;      // 平息
        public string hint;        // 更直白的提示
    }

    // ───────────────────────── memories.json ───────────────────────

    [Serializable]
    public class MemoriesDatabase
    {
        public MemoryDef[] memories;
    }

    [Serializable]
    public class MemoryDef
    {
        public string id;          // "memory_01"
        public string region;      // "dead_city_ruins"
        public string title_key;   // 本地化键 → 标题
        public string body_key;    // 本地化键 → 正文
        public string[] fragments; // 残片序列(本地化键)
    }

    // ───────────────────────── endings.json ────────────────────────

    [Serializable]
    public class EndingsDatabase
    {
        public string[] core_truth; // 核心真相 4 句
        public string[] @sealed;    // 封核结局演出(sealed 是 C# 关键字,@ 转义后字段名仍为 "sealed")
        public string[] fused;      // 融合结局演出
    }

    // ───────────────────────── regions.json ────────────────────────

    [Serializable]
    public class RegionsDatabase
    {
        public RegionDef[] regions;
        public RegionOrder order;
    }

    [Serializable]
    public class RegionOrder
    {
        public string[] surface;     // 7 个地表区域(从西到东)
        public string[] underground; // 9 个地下层(从浅到深)
    }

    [Serializable]
    public class RegionDef
    {
        public string id;        // "rust_jungle"
        public string name_key;  // 本地化键 → "锈蚀丛林"
        public string kind;      // "surface" / "underground"
        public int depth;        // 0=地表,1-9=下潜深度
        public float radiation;  // 辐射强度
        public string[] tags;    // 环境条件标签
        public string[] resources;
        public string shard;     // 该区域的碎片 id
        public ConditionThreshold[] mining_requirements;
    }

    // ───────────────────────── reactions.json ──────────────────────

    [Serializable]
    public class ReactionsDatabase
    {
        public ReactionDef[] reactions;
    }

    [Serializable]
    public class ReactionDef
    {
        public string id;       // "reaction_h2_synthesis"
        public string branch;   // "A"-"H"(化学分支)
        public string title_key;
        public string lesson_key;
        public string[] source_environments;
        public string shard_id; // 关联碎片
        public string memory_id;
        public string[] route_nodes;
    }

    // ───────────────────────── quest_chain.json ────────────────────

    [Serializable]
    public class QuestChainDatabase
    {
        public QuestStep[] steps;
    }

    [Serializable]
    public class QuestStep
    {
        public string id;      // "awaken"
        public string text_key; // 本地化键 → 目标文案
    }
}
