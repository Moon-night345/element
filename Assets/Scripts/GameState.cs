// 元素星海·裂核 — 游戏状态与存档(JsonUtility 序列化,并行数组避免字典)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ElementalStarSea.Data
{
    [Serializable]
    public class GameState : IReactionState
    {
        // ---- 主线进度 ----
        public int questStep;                 // 0-based,指向 QuestChainDatabase.steps
        public string[] calmedShards = new string[0];
        public string[] reconstructedMemories = new string[0];
        public string[] discoveredReactions = new string[0];
        public string[] discoveredLanguages = new string[0];
        public string[] visitedRegions = new[] { "prologue_lab" };
        public int laboratoryStage;
        public string[] craftedDeviceIds = new string[0];
        public DeviceRuntimeContract[] deviceInstances = new DeviceRuntimeContract[0];
        public string[] laboratoryCapabilities = new string[0];
        public string[] laboratoryMilestones = new string[0];
        public string[] ownedToolIds = new[] { "tool_geological_sampling_pickaxe" };
        public string equippedToolId = "tool_geological_sampling_pickaxe";

        // ---- Phase 2-4 化学运行态（并行数组，保持 JsonUtility 可存档） ----
        public string[] identifiedSampleIds = new string[0];
        public string[] chemistryKnowledgeIds = new string[0];
        public long carbonFuelBurned;
        public string[] generatedGasIds = new string[0];
        public int[] generatedGasCounts = new int[0];
        public string[] storedGasIds = new string[0];
        public int[] storedGasCounts = new int[0];
        public string[] vaporStreamIds = new string[0];
        public int[] vaporStreamCounts = new int[0];

        // ---- 玩家 ----
        public float hp = 100f;
        public float energy = 100f;
        public float radiation;
        public float radiationProtection;
        public int radiationMedkits;
        public string[] unlockedAbilityIds = new string[0];
        public bool HasAbility(string id)
        {
            return Array.IndexOf(unlockedAbilityIds ?? new string[0], id) >= 0 ||
                (id == "ability_h2_hover" && Array.IndexOf(discoveredReactions ?? new string[0], "reaction_h2_synthesis") >= 0);
        }
        public void GrantAbility(string id)
        {
            if (string.IsNullOrEmpty(id) || Array.IndexOf(unlockedAbilityIds ?? new string[0], id) >= 0) return;
            unlockedAbilityIds = Append(unlockedAbilityIds ?? new string[0], id);
        }
        public string[] toolResistanceIds = new string[0];
        public float[] toolResistanceValues = new float[0];
        public string currentRegion = "prologue_lab";
        public string endingChoice = "";      // "" / "sealed" / "fused"
        public string[] coreReactionChainProgress = new string[0];
        public bool coreTestPassed;
        public int terrainSeed = 1;
        public int terrainGeneratorVersion;
        public TerrainCellChange[] terrainChanges = new TerrainCellChange[0];
        public ResourceSourceState[] resourceSources = new ResourceSourceState[0];
        public WorldDropState[] worldDrops = new WorldDropState[0];
        public CombatState combat = new CombatState();
        public ElementalStarSea.Agent.AgentPersistentState agent = new ElementalStarSea.Agent.AgentPersistentState();
        public bool hasPlayerPosition;
        public float playerWorldX, playerWorldY;

        // ---- 库存(并行数组) ----
        public string[] inventoryIds = new string[0];
        public int[] inventoryCounts = new int[0];

        // ---- 库存操作 ----
        Dictionary<string, int> _invCache;

        Dictionary<string, int> Inv()
        {
            if (_invCache == null)
            {
                _invCache = new Dictionary<string, int>();
                for (int i = 0; i < inventoryIds.Length && i < inventoryCounts.Length; i++)
                    _invCache[inventoryIds[i]] = inventoryCounts[i];
            }
            return _invCache;
        }

        void FlushInv()
        {
            inventoryIds = new List<string>(Inv().Keys).ToArray();
            inventoryCounts = new List<string>(Inv().Keys).Select(k => Inv()[k]).ToArray();
        }

        public int CountItem(string materialId)
        {
            int c;
            return Inv().TryGetValue(materialId, out c) ? c : 0;
        }

        public void AddItem(string materialId, int count = 1)
        {
            if (string.IsNullOrEmpty(materialId) || count <= 0) return;
            Inv()[materialId] = CountItem(materialId) + count;
            FlushInv();
        }

        public bool RemoveItems(IEnumerable<RecipeQuantity> quantities)
        {
            foreach (var q in quantities)
                if (CountItem(q.id) < q.n) return false;
            foreach (var q in quantities)
            {
                int left = CountItem(q.id) - q.n;
                if (left <= 0) Inv().Remove(q.id); else Inv()[q.id] = left;
            }
            FlushInv();
            return true;
        }

        public float SynthesisEnergy { get { return energy; } }

        /// <summary>库存增量与能量在验证完成后一次提交，失败时状态完全不变。</summary>
        public bool TryCommitReaction(IEnumerable<InventoryDeltaEntry> delta, float energyCost)
        {
            if (delta == null || float.IsNaN(energyCost) || float.IsInfinity(energyCost) ||
                energyCost < 0f || energy < energyCost) return false;
            if (!TryApplyInventoryDelta(delta)) return false;
            energy -= energyCost;
            return true;
        }

        public bool TryApplyInventoryDelta(IEnumerable<InventoryDeltaEntry> delta)
        {
            if (delta == null) return false;
            var next = new Dictionary<string, int>(Inv());
            try
            {
                foreach (var entry in delta)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.id) || entry.change == 0) return false;
                    int current;
                    next.TryGetValue(entry.id, out current);
                    int value = checked(current + entry.change);
                    if (value < 0) return false;
                    if (value == 0) next.Remove(entry.id); else next[entry.id] = value;
                }
            }
            catch (OverflowException) { return false; }
            _invCache = next;
            FlushInv();
            return true;
        }

        public List<string> OwnedMaterialIds()
        {
            return new List<string>(Inv().Keys);
        }

        // ---- 集合辅助 ----
        public bool HasCalmed(string shardId) { return Array.IndexOf(calmedShards, shardId) >= 0; }
        public bool HasMemory(string memoryId) { return Array.IndexOf(reconstructedMemories, memoryId) >= 0; }
        public bool HasReaction(string reactionId) { return Array.IndexOf(discoveredReactions, reactionId) >= 0; }
        public bool HasLanguage(string languageId) { return Array.IndexOf(discoveredLanguages ?? new string[0], languageId) >= 0; }
        public bool HasVisitedRegion(string regionId)
        {
            return regionId == currentRegion || Array.IndexOf(visitedRegions ?? new string[0], regionId) >= 0;
        }
        public bool HasCraftedDevice(string deviceId)
        {
            return Array.IndexOf(craftedDeviceIds ?? new string[0], deviceId) >= 0;
        }

        public void AddCalmed(string shardId)
        {
            if (HasCalmed(shardId)) return;
            calmedShards = Append(calmedShards, shardId);
        }

        public void AddMemory(string memoryId)
        {
            if (HasMemory(memoryId)) return;
            reconstructedMemories = Append(reconstructedMemories, memoryId);
        }

        public void AddReaction(string reactionId)
        {
            if (HasReaction(reactionId)) return;
            discoveredReactions = Append(discoveredReactions, reactionId);
        }

        public void AddLanguage(string languageId)
        {
            if (string.IsNullOrEmpty(languageId) || HasLanguage(languageId)) return;
            discoveredLanguages = Append(discoveredLanguages, languageId);
        }

        public void AddVisitedRegion(string regionId)
        {
            // Region entry sets currentRegion before recording discovery. HasVisitedRegion
            // includes currentRegion, so using it here would lose every new visit on departure.
            if (string.IsNullOrEmpty(regionId) || Array.IndexOf(visitedRegions ?? new string[0], regionId) >= 0) return;
            visitedRegions = Append(visitedRegions ?? new string[0], regionId);
        }

        public void AddCraftedDevice(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId) || HasCraftedDevice(deviceId)) return;
            craftedDeviceIds = Append(craftedDeviceIds ?? new string[0], deviceId);
        }

        public bool HasIdentifiedSample(string sampleId)
        {
            return Array.IndexOf(identifiedSampleIds ?? new string[0], sampleId) >= 0;
        }

        public bool HasLaboratoryCapability(string capabilityId)
        {
            return Array.IndexOf(laboratoryCapabilities ?? new string[0], capabilityId) >= 0;
        }

        public bool AddLaboratoryCapability(string capabilityId)
        {
            if (string.IsNullOrEmpty(capabilityId) || HasLaboratoryCapability(capabilityId)) return false;
            laboratoryCapabilities = Append(laboratoryCapabilities ?? new string[0], capabilityId);
            return true;
        }

        public bool HasLaboratoryMilestone(string milestoneId)
        {
            return Array.IndexOf(laboratoryMilestones ?? new string[0], milestoneId) >= 0;
        }

        public bool AddLaboratoryMilestone(string milestoneId)
        {
            if (string.IsNullOrEmpty(milestoneId) || HasLaboratoryMilestone(milestoneId)) return false;
            laboratoryMilestones = Append(laboratoryMilestones ?? new string[0], milestoneId);
            return true;
        }

        public bool HasOwnedTool(string toolId)
        {
            if (toolId == "tool_geological_sampling_pickaxe") return true;
            return Array.IndexOf(ownedToolIds ?? new string[0], toolId) >= 0;
        }

        public bool AddOwnedTool(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || HasOwnedTool(toolId)) return false;
            ownedToolIds = Append(ownedToolIds ?? new string[0], toolId);
            return true;
        }

        public float ToolResistance(string resistanceId)
        {
            var ids = toolResistanceIds ?? new string[0];
            var values = toolResistanceValues ?? new float[0];
            for (int i = 0; i < ids.Length && i < values.Length; i++)
                if (ids[i] == resistanceId) return values[i];
            return 0f;
        }

        public void SetToolResistance(string resistanceId, float value)
        {
            if (string.IsNullOrEmpty(resistanceId) || float.IsNaN(value) || float.IsInfinity(value)) return;
            value = Math.Max(0f, value);
            var ids = new List<string>(toolResistanceIds ?? new string[0]);
            var values = new List<float>(toolResistanceValues ?? new float[0]);
            while (values.Count < ids.Count) values.Add(0f);
            int index = ids.IndexOf(resistanceId);
            if (index < 0) { ids.Add(resistanceId); values.Add(value); }
            else values[index] = Math.Max(values[index], value);
            toolResistanceIds = ids.ToArray();
            toolResistanceValues = values.ToArray();
        }

        public void IdentifySample(string sampleId)
        {
            if (string.IsNullOrEmpty(sampleId) || HasIdentifiedSample(sampleId)) return;
            identifiedSampleIds = Append(identifiedSampleIds ?? new string[0], sampleId);
        }

        public bool HasChemistryKnowledge(string knowledgeId)
        {
            return Array.IndexOf(chemistryKnowledgeIds ?? new string[0], knowledgeId) >= 0;
        }

        public void AddChemistryKnowledge(string knowledgeId)
        {
            if (string.IsNullOrEmpty(knowledgeId) || HasChemistryKnowledge(knowledgeId)) return;
            chemistryKnowledgeIds = Append(chemistryKnowledgeIds ?? new string[0], knowledgeId);
        }

        public int CountGeneratedGas(string gasId) { return CountParallel(generatedGasIds, generatedGasCounts, gasId); }
        public int CountStoredGas(string gasId) { return CountParallel(storedGasIds, storedGasCounts, gasId); }
        public int CountVapor(string vaporId) { return CountParallel(vaporStreamIds, vaporStreamCounts, vaporId); }

        public bool ChangeGeneratedGas(string gasId, int change)
        {
            return ChangeParallel(ref generatedGasIds, ref generatedGasCounts, gasId, change);
        }

        public bool ChangeStoredGas(string gasId, int change)
        {
            return ChangeParallel(ref storedGasIds, ref storedGasCounts, gasId, change);
        }

        public bool ChangeVapor(string vaporId, int change)
        {
            return ChangeParallel(ref vaporStreamIds, ref vaporStreamCounts, vaporId, change);
        }

        static int CountParallel(string[] ids, int[] counts, string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            ids = ids ?? new string[0]; counts = counts ?? new int[0];
            for (int i = 0; i < ids.Length && i < counts.Length; i++)
                if (ids[i] == id) return counts[i];
            return 0;
        }

        static bool ChangeParallel(ref string[] ids, ref int[] counts, string id, int change)
        {
            if (string.IsNullOrEmpty(id) || change == 0) return false;
            var next = new Dictionary<string, int>();
            ids = ids ?? new string[0]; counts = counts ?? new int[0];
            for (int i = 0; i < ids.Length && i < counts.Length; i++) next[ids[i]] = counts[i];
            int current; next.TryGetValue(id, out current);
            int value;
            try { value = checked(current + change); }
            catch (OverflowException) { return false; }
            if (value < 0) return false;
            if (value == 0) next.Remove(id); else next[id] = value;
            ids = next.Keys.ToArray();
            counts = next.Keys.Select(key => next[key]).ToArray();
            return true;
        }

        public DeviceRuntimeContract DeviceInstance(string instanceId)
        {
            return (deviceInstances ?? new DeviceRuntimeContract[0])
                .FirstOrDefault(value => value != null && value.instance_id == instanceId);
        }

        public void UpsertDeviceInstance(DeviceRuntimeContract state)
        {
            if (state == null || string.IsNullOrEmpty(state.instance_id)) return;
            var values = new List<DeviceRuntimeContract>(deviceInstances ?? new DeviceRuntimeContract[0]);
            int index = values.FindIndex(value => value != null && value.instance_id == state.instance_id);
            if (index >= 0) values[index] = state; else values.Add(state);
            deviceInstances = values.ToArray();
        }

        static string[] Append(string[] source, string value)
        {
            var list = new List<string>(source) { value };
            return list.ToArray();
        }

        // ---- 存档 ----
        /// <summary>冒烟测试等场景用独立存档文件名,避免清掉真实进度。</summary>
        public static string SaveFileNameOverride = "";
        internal static string SceneProbeSaveDirectory = "";
        static string SaveRoot { get { return string.IsNullOrEmpty(SceneProbeSaveDirectory) ? Application.persistentDataPath : SceneProbeSaveDirectory; } }
        public static string CurrentSaveSlot { get; private set; } = "legacy";
        public static bool SaveWritesBlocked { get; private set; }
        public static string LastSaveStatus { get; private set; } = "";
        public static event Action<string> SaveFailed;

        public static void SelectSaveSlot(string id)
        {
            if (string.IsNullOrEmpty(id) || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,80}$"))
                throw new ArgumentException("Invalid save slot ID");
            CurrentSaveSlot = id; SaveWritesBlocked = false; LastSaveStatus = "";
        }

        public static void BeginNewWorld()
        {
            SelectSaveSlot("world_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
        }

        static string SavePath
        {
            get
            {
                if (!string.IsNullOrEmpty(SaveFileNameOverride))
                {
                    if (Path.GetFileName(SaveFileNameOverride) != SaveFileNameOverride || SaveFileNameOverride.Contains(".."))
                        throw new ArgumentException("Invalid save filename override");
                    return Path.Combine(string.IsNullOrEmpty(SceneProbeSaveDirectory) ? Application.persistentDataPath : SceneProbeSaveDirectory,
                        SaveFileNameOverride);
                }
                return CurrentSaveSlot == "legacy" ? Path.Combine(SaveRoot, "elemental_sea_save.json") :
                    Path.Combine(SaveRoot, "saves", CurrentSaveSlot, "save.json");
            }
        }

        public void Save()
        { TrySave(); }

        public bool TrySave()
        {
            try
            {
                if (SaveWritesBlocked) throw new IOException("Existing save is damaged; choose a different world before writing.");
                string reason;
                if (!new SaveRepository(SavePath, CurrentSaveSlot).Write(this, out reason)) throw new IOException(reason);
                LastSaveStatus = "saved";
                return true;
            }
            catch (Exception e)
            {
                LastSaveStatus = e.Message;
                Debug.LogError("[GameState] 存档失败: " + e.Message);
                if (SaveFailed != null) SaveFailed(e.Message);
                return false;
            }
        }

        public static GameState LoadOrNew()
        {
            try
            {
                var result = new SaveRepository(SavePath, CurrentSaveSlot).Read();
                if (result.success)
                {
                    SaveWritesBlocked = false;
                    LastSaveStatus = result.recovery != "" ? "recovered_" + result.recovery : result.legacy ? "legacy_loaded" : "loaded";
                    return result.state;
                }
                if (!result.exists) { SaveWritesBlocked = false; LastSaveStatus = "new_world"; return new GameState(); }
                SaveWritesBlocked = true;
                LastSaveStatus = result.reason;
                Debug.LogWarning("[GameState] 无有效存档或备份，已阻止覆盖: " + result.reason);
            }
            catch (Exception e)
            {
                SaveWritesBlocked = true; LastSaveStatus = e.Message;
                Debug.LogWarning("[GameState] 读档失败，已阻止覆盖: " + e.Message);
            }
            return new GameState();
        }

        public static bool HasSave()
        {
            return new SaveRepository(SavePath, CurrentSaveSlot).Read().exists;
        }

        public static void DeleteSave()
        {
            try { new SaveRepository(SavePath, CurrentSaveSlot).Delete(); SaveWritesBlocked = false; }
            catch (Exception e) { Debug.LogError("[GameState] 删除存档失败: " + e.Message); }
        }

        public static SaveSlotInfo[] ListSaveSlots()
        {
            var rows = new List<SaveSlotInfo>();
            Action<string, string> add = (id, path) => {
                var result = new SaveRepository(path, id).Read();
                if (!result.exists) return;
                rows.Add(new SaveSlotInfo { id = id, loadable = result.success,
                    region = result.success ? result.state.currentRegion : "不可读取",
                    stage = result.success ? result.state.laboratoryStage : 0,
                    status = !result.success ? result.reason : result.recovery != "" ? "从备份恢复" : result.legacy ? "兼容旧存档" : "正常" });
            };
            add("legacy", Path.Combine(SaveRoot, "elemental_sea_save.json"));
            var root = Path.Combine(SaveRoot, "saves");
            if (Directory.Exists(root))
                foreach (var directory in Directory.GetDirectories(root).OrderByDescending(p => p))
                {
                    string id = Path.GetFileName(directory);
                    if (System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-zA-Z0-9_-]{1,80}$")) add(id, Path.Combine(directory, "save.json"));
                }
            return rows.ToArray();
        }
    }
}
