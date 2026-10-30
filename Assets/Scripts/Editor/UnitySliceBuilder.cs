// 元素星海·裂核 — 编辑器工具:场景生成 + 无头冒烟测试
// 运行:
//   Unity.exe -batchmode -quit -projectPath <proj> -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.BuildAll -logFile <log>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using ElementalStarSea.Data;
using ElementalStarSea.Game;

namespace ElementalStarSea.EditorTools
{
    public static class UnitySliceBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string RequiredUnityVersion = "2022.3.62f3c1";
        const string DefaultWindowsBuildPath = "Builds/Windows/ElementalStarSea.exe";

        [MenuItem("ElementalStarSea/生成主场景")]
        public static void BuildMainScene()
        {
            if (!Directory.Exists("Assets/Scenes")) Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Game");
            go.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Slice] 主场景已生成: " + ScenePath);
        }

        [MenuItem("ElementalStarSea/配置正式构建场景")]
        public static void ConfigureBuildSettings()
        {
            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("正式主场景不存在", ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] 正式构建场景已配置: " + ScenePath);
        }

        /// <summary>
        /// 可重复 Windows x64 构建入口。可通过 ELEMENTAL_BUILD_OUTPUT 覆盖输出 exe 路径。
        /// 命令行: Unity.exe -batchmode -quit -projectPath &lt;proj&gt; -executeMethod
        /// ElementalStarSea.EditorTools.UnitySliceBuilder.BuildWindows64 -logFile &lt;log&gt;
        /// </summary>
        [MenuItem("ElementalStarSea/构建 Windows x64")]
        public static void BuildWindows64()
        {
            try
            {
                if (Application.unityVersion != RequiredUnityVersion)
                    throw new InvalidOperationException(
                        $"Unity 版本不匹配: required={RequiredUnityVersion}, actual={Application.unityVersion}");

                ConfigureBuildSettings();

                var configuredOutput = Environment.GetEnvironmentVariable("ELEMENTAL_BUILD_OUTPUT");
                var outputPath = Path.GetFullPath(
                    string.IsNullOrWhiteSpace(configuredOutput)
                        ? DefaultWindowsBuildPath
                        : configuredOutput);
                var outputDirectory = Path.GetDirectoryName(outputPath);
                if (string.IsNullOrEmpty(outputDirectory))
                    throw new InvalidOperationException("无法确定构建输出目录: " + outputPath);
                Directory.CreateDirectory(outputDirectory);

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                var succeeded = summary.result == BuildResult.Succeeded;
                var message = $"[Build] result={summary.result}, output={outputPath}, " +
                              $"size={summary.totalSize}, errors={summary.totalErrors}, " +
                              $"warnings={summary.totalWarnings}, duration={summary.totalTime}";

                if (succeeded)
                    Debug.Log(message);
                else
                    Debug.LogError(message);

                if (Application.isBatchMode)
                    EditorApplication.Exit(succeeded ? 0 : 1);
                else if (!succeeded)
                    throw new InvalidOperationException(message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }

        /// <summary>启动入口:等场景加载完成后自动进入 Play 模式。
        /// 命令行: Unity.exe -projectPath &lt;proj&gt; -executeMethod ...UnitySliceBuilder.LaunchAndPlay</summary>
        public static void LaunchAndPlay()
        {
            EditorApplication.update += WaitForSceneAndPlay;
        }

        static void WaitForSceneAndPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorApplication.update -= WaitForSceneAndPlay;
                EditorApplication.isPlaying = true;
                Debug.Log("[Slice] 场景就绪,自动进入 Play 模式");
            }
        }

        /// <summary>批量入口:建场景 → 冒烟测试 → 退出码 0/1。</summary>
        public static void BuildAll()
        {
            BuildMainScene();
            bool ok = RunSmokeTest();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        [MenuItem("ElementalStarSea/运行冒烟测试")]
        public static bool RunSmokeTest()
        {
            int failed = 0;
            Action<string> check = (name) => Debug.Log("[PASS] " + name);
            Action<string> fail = (name) => { Debug.LogError("[FAIL] " + name); failed++; };

            // ---- 1. 数据加载 ----
            try
            {
                GameData.LoadAll();
                if (GameData.Shards.shards.Length != 16) throw new Exception("shards != 16");
                if (GameData.Memories.memories.Length != 16) throw new Exception("memories != 16");
                if (GameData.Regions.regions.Length != 17) throw new Exception("regions != 17 (1 实验室+7 地表+9 地下)");
                if (GameData.QuestChain.steps.Length != 12) throw new Exception("quest steps != 12");
                if (GameData.Endings.@sealed.Length == 0 || GameData.Endings.fused.Length == 0)
                    throw new Exception("endings empty");
                check("核心数据加载(16 碎片/16 记忆/17 区域/12 任务步)");
            }
            catch (Exception e) { fail("核心数据加载: " + e.Message); }

            // ---- 2. 化学引擎 ----
            try
            {
                var chem = JsonUtility.FromJson<ChemistryDatabase>(
                    File.ReadAllText("Assets/Resources/Data/chemistry.json"));
                ChemistryEngine.Load(chem);
                if (chem.materials.Length != 54) throw new Exception("materials != 54, got " + chem.materials.Length);
                if (chem.reactions.Length != 40) throw new Exception("recipes != 40");
                check("化学数据加载(54 物质/40 反应)");
            }
            catch (Exception e) { fail("化学数据加载: " + e.Message); }

            // ---- 3. 精确配比匹配(与 Godot 语义一致) ----
            try
            {
                var water = ChemistryEngine.MatchExact(new[] { "element_h", "element_h", "element_o" });
                if (water == null || water.id != "reaction_water_synthesis")
                    throw new Exception("2H+O 应命中水合成, got " + (water != null ? water.id : "null"));
                var none = ChemistryEngine.MatchExact(new[] { "element_h", "element_o" });
                if (none != null) throw new Exception("1H+1O 不应命中任何配方");
                var wrongRatio = ChemistryEngine.MatchExact(new[] { "element_h", "element_h", "element_h" });
                if (wrongRatio != null && wrongRatio.id != "reaction_nh3")
                    throw new Exception("3H 不应命中 NH₃(缺 N)");
                check("反应精确配比匹配(2H+O→H₂O / 1H+1O→null)");
            }
            catch (Exception e) { fail("反应匹配: " + e.Message); }

            // ---- 4. 本地化 ----
            try
            {
                var loc = JsonUtility.FromJson<LocalizationDatabase>(
                    File.ReadAllText("Assets/Resources/Data/localization_zh.json"));
                Localization.Load(loc);
                if (Localization.Tr("ui.main_menu.title") != "元素星海·裂核")
                    throw new Exception("标题词条不匹配: " + Localization.Tr("ui.main_menu.title"));
                foreach (var step in GameData.QuestChain.steps)
                    if (Localization.Tr(step.text_key) == step.text_key)
                        throw new Exception("任务键缺失: " + step.text_key);
                foreach (var shard in GameData.Shards.shards)
                {
                    var langKey = "content.codex." + shard.language + ".name";
                    if (Localization.Tr(langKey) == langKey)
                        throw new Exception("语言词条缺失: " + langKey);
                }
                check("本地化完整性(标题/12 任务键/16 语言键)");
            }
            catch (Exception e) { fail("本地化: " + e.Message); }

            // ---- 5. 库存与存档往返(用隔离存档文件,不碰真实进度) ----
            try
            {
                GameState.SaveFileNameOverride = "smoke_test_save.json";
                var state = new GameState();
                state.AddItem("element_h", 2);
                state.AddItem("element_o", 1);
                if (state.CountItem("element_h") != 2) throw new Exception("AddItem/Count 失败");
                var recipe = ChemistryEngine.MatchExact(new[] { "element_h", "element_h", "element_o" });
                if (!ChemistryEngine.CanAfford(state, recipe)) throw new Exception("CanAfford 失败");
                state.RemoveItems(recipe.reactants);
                if (state.CountItem("element_h") != 0 || state.CountItem("element_o") != 0)
                    throw new Exception("RemoveItems 失败");
                state.AddItem("compound_h2o", 1);
                state.questStep = 5;
                state.Save();
                var loaded = GameState.LoadOrNew();
                if (loaded.questStep != 5 || loaded.CountItem("compound_h2o") != 1)
                    throw new Exception("存档往返失败");
                GameState.DeleteSave();
                GameState.SaveFileNameOverride = "";
                check("库存操作与存档往返");
            }
            catch (Exception e) { fail("库存/存档: " + e.Message); GameState.SaveFileNameOverride = ""; }

            // ---- 6. 记忆与区域关联完整性 ----
            try
            {
                var regionIds = GameData.Regions.regions.Select(r => r.id).ToHashSet();
                foreach (var m in GameData.Memories.memories)
                    if (!regionIds.Contains(m.region))
                        throw new Exception("记忆区域不存在: " + m.region);
                var shardIds = GameData.Shards.shards.Select(s => s.id).ToHashSet();
                foreach (var r in GameData.Regions.regions)
                    if (!string.IsNullOrEmpty(r.shard) && !shardIds.Contains(r.shard))
                        throw new Exception("区域碎片不存在: " + r.shard);
                check("记忆-区域 / 区域-碎片 关联完整");
            }
            catch (Exception e) { fail("关联完整性: " + e.Message); }

            // ---- 7. Godot 权威导出清单 ----
            try
            {
                var manifest = GameData.Manifest;
                if (manifest == null || manifest.counts == null) throw new Exception("migration manifest missing");
                if (manifest.schema_version != 2) throw new Exception("schema version != 2");
                if (manifest.counts.regions != 17 || manifest.counts.reaction_specs != 40 ||
                    manifest.counts.reaction_content_units != 40 || manifest.counts.formal_devices != 15 ||
                    manifest.counts.processing_recipes != 33)
                    throw new Exception("authoritative counts mismatch");
                if (string.IsNullOrWhiteSpace(manifest.source_commit)) throw new Exception("source commit missing");
                check("Godot 权威导出清单(schema 2 / 17 区域 / 40 反应 / 15 设备)");
            }
            catch (Exception e) { fail("权威导出清单: " + e.Message); }

            // ---- 8. 跨目录 ID 与引用完整性 ----
            try
            {
                var chem = JsonUtility.FromJson<ChemistryDatabase>(
                    File.ReadAllText("Assets/Resources/Data/chemistry.json"));
                var loc = JsonUtility.FromJson<LocalizationDatabase>(
                    File.ReadAllText("Assets/Resources/Data/localization_zh.json"));
                var errors = AuthoritativeDataValidator.Validate(
                    GameData.Manifest, GameData.Regions, chem, GameData.Reactions,
                    GameData.ReactionContent, GameData.Devices, GameData.ProcessingRecipes, GameData.Shards,
                    GameData.Memories, loc);
                if (errors.Count > 0) throw new Exception(string.Join(" | ", errors));
                check("权威目录跨引用完整性(区域/反应/内容/碎片/记忆/设备/本地化)");
            }
            catch (Exception e) { fail("权威目录引用: " + e.Message); }

            // ---- 9. 正式反应闭环：失败零副作用 / 条件 / 原子提交 / 事件 ----
            try
            {
                var state = new GameState();
                state.AddItem("element_c", 1);
                state.AddItem("element_o", 2);
                var service = new ReactionService(state);
                var events = new List<ReactionGameplayEvent>();
                service.GameplayEventPublished += events.Add;

                var missingEnvironment = service.Execute("reaction_co2", new[] { "wet" });
                if (missingEnvironment.failure != ReactionFailure.MissingEnvironment)
                    throw new Exception("CO2 missing environment failure mismatch");
                if (state.CountItem("element_c") != 1 || state.CountItem("element_o") != 2 || state.energy != 100f)
                    throw new Exception("precondition failure mutated state");
                if (events.Count != 1 || events[0].event_type != "experiment_failed" ||
                    events[0].reason != "missing_environment")
                    throw new Exception("formal failure event missing");

                var success = service.Execute("reaction_co2", new[] { "heat" });
                if (!success.success || success.kind != ReactionResultKind.Success)
                    throw new Exception("CO2 conditioned reaction did not succeed");
                if (state.CountItem("element_c") != 0 || state.CountItem("element_o") != 0 ||
                    state.CountItem("compound_co2") != 1 || state.energy != 90f)
                    throw new Exception("atomic inventory/energy commit mismatch");
                if (!state.HasReaction("reaction_co2") || !state.HasLanguage("language_smother_cover"))
                    throw new Exception("reaction/language discovery missing");
                if (events.Count != 2 || events[1].event_type != "reaction_committed" ||
                    events[1].inventory_delta.Length != 3)
                    throw new Exception("formal success event mismatch");
                events[1].inventory_delta[0].change = -999;
                if (success.gameplay_event.inventory_delta[0].change == -999)
                    throw new Exception("published event is not a defensive snapshot");

                var nitrogenState = new GameState();
                nitrogenState.AddItem("element_n", 2);
                var nitrogen = new ReactionService(nitrogenState);
                if (nitrogen.Preview("reaction_n2", new string[0]).failure != ReactionFailure.MissingEnvironment)
                    throw new Exception("any_tags empty should fail");
                if (!nitrogen.Preview("reaction_n2", new[] { "cold" }).success)
                    throw new Exception("any_tags cold alternative should pass");

                var lowEnergy = new GameState { energy = 9f };
                lowEnergy.AddItem("element_h", 2);
                var energyFailure = new ReactionService(lowEnergy).Execute("reaction_h2_synthesis", new string[0]);
                if (energyFailure.failure != ReactionFailure.InsufficientEnergy ||
                    lowEnergy.CountItem("element_h") != 2 || lowEnergy.energy != 9f)
                    throw new Exception("insufficient energy failure is not atomic");
                check("正式反应闭环(条件/任选条件/失败零副作用/原子提交/成功失败事件)");
            }
            catch (Exception e) { fail("正式反应闭环: " + e.Message); }

            // ---- 10. 正式设备闭环：制造 / 放置 / 预留 / 加工 / 领取 / 存档 ----
            try
            {
                GameState.SaveFileNameOverride = "device_runtime_smoke_save.json";
                GameState.DeleteSave();
                var state = new GameState { currentRegion = "dead_city_ruins", laboratoryStage = 0 };
                state.AddItem("material_iron", 12);
                state.AddItem("material_copper", 2);
                state.AddItem("material_glass", 2);
                state.AddItem("material_refractory", 4);
                state.AddItem("material_carbon", 2);
                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                var deviceEvents = new List<DeviceGameplayEvent>();
                devices.GameplayEventPublished += deviceEvents.Add;

                if (!devices.Craft("device_chemical_workbench").success)
                    throw new Exception("workbench craft failed");
                if (!devices.Craft("device_small_chemical_furnace").success)
                    throw new Exception("furnace craft failed");
                var placed = devices.Place("device_small_chemical_furnace", "smoke_furnace");
                if (!placed.success || placed.state.state != DeviceProcessingState.Idle)
                    throw new Exception("furnace placement failed");

                state.AddItem("resource_carbonate_rock", 1);
                var started = devices.Start("smoke_furnace", "recipe_phase3a_thermal_carbonate");
                if (!started.success || state.CountItem("resource_carbonate_rock") != 0 ||
                    state.CountGeneratedGas("compound_co2") != 1)
                    throw new Exception("specialized thermal carbonate route failed");
                devices.Advance(1f);
                if (!devices.Collect("smoke_furnace").success ||
                    state.CountItem("compound_cao") != 1 || state.CountItem("compound_co2") != 1)
                    throw new Exception("specialized thermal outputs failed");

                var locked = new DeviceRuntimeService(new GameState(), GameData.Devices, GameData.ProcessingRecipes)
                    .Craft("device_multi_chemical_analyzer");
                if (locked.success || locked.reason != "laboratory_stage_locked")
                    throw new Exception("laboratory stage lock failed");

                var conditionState = new GameState { laboratoryStage = 3, currentRegion = "core_shell" };
                conditionState.AddCraftedDevice("device_high_pressure_catalytic_reactor");
                conditionState.AddItem("compound_co2", 1);
                conditionState.AddItem("compound_h2", 1);
                conditionState.AddItem("material_silver_catalytic", 1);
                var conditionDevices = new DeviceRuntimeService(conditionState, GameData.Devices, GameData.ProcessingRecipes);
                conditionDevices.GameplayEventPublished += deviceEvents.Add;
                if (!conditionDevices.Place("device_high_pressure_catalytic_reactor", "smoke_pressure").success)
                    throw new Exception("high pressure reactor placement failed");
                var missingCondition = conditionDevices.Start("smoke_pressure", "recipe_phase4b_device12_co2_h2_conversion");
                if (missingCondition.success || !missingCondition.reason.StartsWith("condition_missing:") ||
                    conditionState.CountItem("compound_co2") != 1)
                    throw new Exception("device condition failure mutated inputs");
                var conditions = new Dictionary<string, string>
                {
                    { "pressure", "PRESSURE_HIGH" },
                    { "temperature_condition", "HIGH" },
                    { "internal_process", "true" }
                };
                var conditioned = conditionDevices.Start("smoke_pressure",
                    "recipe_phase4b_device12_co2_h2_conversion", conditions);
                if (!conditioned.success || conditionState.CountItem("compound_co2") != 0 ||
                    conditionState.CountItem("compound_h2") != 0 ||
                    conditionState.CountItem("material_silver_catalytic") != 1)
                    throw new Exception("condition/process-input contract failed");
                conditionState.Save();
                var restored = GameState.LoadOrNew();
                var restoredDevices = new DeviceRuntimeService(restored, GameData.Devices, GameData.ProcessingRecipes);
                var restoredState = restored.DeviceInstance("smoke_pressure");
                if (restoredState == null || restoredState.state != DeviceProcessingState.Processing)
                    throw new Exception("processing state save roundtrip failed");
                if (restoredDevices.Advance(4f) != 1 ||
                    restored.DeviceInstance("smoke_pressure").state != DeviceProcessingState.OutputReady)
                    throw new Exception("processing completion failed");
                if (restored.CountItem("compound_reduced_carbon_liquid") != 0)
                    throw new Exception("buffered output leaked before collection");
                if (!restoredDevices.Collect("smoke_pressure").success ||
                    restored.CountItem("compound_reduced_carbon_liquid") != 1 ||
                    restored.DeviceInstance("smoke_pressure").state != DeviceProcessingState.Idle)
                    throw new Exception("output collection failed");
                if (!deviceEvents.Any(value => value.event_type == "device_built") ||
                    !deviceEvents.Any(value => value.event_type == "device_processing_started"))
                    throw new Exception("device gameplay events missing");
                GameState.DeleteSave();
                GameState.SaveFileNameOverride = "";
                check("正式设备闭环(制造/放置/输入预留/加工/输出缓冲领取/存档)");
            }
            catch (Exception e)
            {
                GameState.DeleteSave();
                GameState.SaveFileNameOverride = "";
                fail("正式设备闭环: " + e.Message);
            }

            // ---- 11. Godot 专属设备化学语义：非消耗输入 / 内部气相 / 双产物 / 催化剂 ----
            try
            {
                var state = new GameState { laboratoryStage = 3, currentRegion = "magnetic_plateau" };
                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                var specializedCount = GameData.ProcessingRecipes.recipes.Count(value =>
                    (value.progression_tags ?? new string[0]).Contains("runtime_service_only"));
                if (specializedCount != 18) throw new Exception("specialized recipe count != 18");

                string[] deviceIds = {
                    "device_multi_chemical_analyzer", "device_industrial_solid_liquid_filter",
                    "device_industrial_distiller", "device_industrial_condenser",
                    "device_high_pressure_gas_tank", "device_small_chemical_furnace",
                    "device_industrial_electrolysis_cell", "device_industrial_absorption_tower",
                    "device_industrial_crystallizer", "device_high_pressure_catalytic_reactor"
                };
                foreach (var id in deviceIds)
                {
                    state.AddCraftedDevice(id);
                    if (!devices.Place(id, "u5_" + id).success) throw new Exception("place failed: " + id);
                }

                state.AddItem("resource_mineral_water", 1);
                CompleteDevice(devices, "u5_device_multi_chemical_analyzer", "recipe_phase2_detect", null);
                if (!state.HasIdentifiedSample("resource_mineral_water") || state.CountItem("resource_mineral_water") != 1)
                    throw new Exception("analyzer non-consuming knowledge writeback failed");

                state.AddItem("compound_mixed_reaction_slurry", 1);
                CompleteDevice(devices, "u5_device_industrial_solid_liquid_filter", "recipe_phase2_filter", null);
                if (state.CountItem("compound_precipitate_solid") != 1 ||
                    state.CountItem("compound_neutralized_salt_solution") != 1)
                    throw new Exception("filter two-output contract failed");
                CompleteDevice(devices, "u5_device_industrial_distiller", "recipe_phase2_distill", null);
                if (state.CountItem("compound_distilled_water") != 1 ||
                    state.CountItem("compound_concentrated_salt_solution") != 1)
                    throw new Exception("distiller two-output contract failed");

                state.ChangeVapor("compound_ethanol", 1);
                CompleteDevice(devices, "u5_device_industrial_condenser", "recipe_phase3a_condense_vapor", null);
                if (state.CountVapor("compound_ethanol") != 0 || state.CountItem("compound_ethanol") != 1)
                    throw new Exception("condenser vapor contract failed");

                state.ChangeGeneratedGas("compound_co2", 1);
                CompleteDevice(devices, "u5_device_high_pressure_gas_tank", "recipe_phase3a_store_gas", null);
                if (state.CountGeneratedGas("compound_co2") != 0 || state.CountStoredGas("compound_co2") != 1)
                    throw new Exception("gas tank internal storage contract failed");

                state.AddItem("item_abandoned_mine_iron_bearing_ore_block", 1);
                var analysisGate = devices.Start("u5_device_small_chemical_furnace", "recipe_abandoned_mine_iron_ore");
                if (analysisGate.success || analysisGate.reason != "analysis_required")
                    throw new Exception("furnace analysis gate failed");
                state.IdentifySample("item_abandoned_mine_iron_bearing_ore_block");
                CompleteDevice(devices, "u5_device_small_chemical_furnace", "recipe_abandoned_mine_iron_ore", null);
                if (state.CountItem("material_iron") != 1) throw new Exception("iron furnace route failed");

                state.AddItem("compound_h2o", 1);
                CompleteDevice(devices, "u5_device_industrial_electrolysis_cell", "recipe_phase3b_electrolyze", null);
                if (state.CountItem("compound_h2") != 1 || state.CountItem("compound_o2") != 1)
                    throw new Exception("electrolysis route failed");

                state.ChangeStoredGas("compound_polluted_gas", 1);
                state.AddItem("resource_alkaline_solution", 1);
                CompleteDevice(devices, "u5_device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null);
                if (state.CountItem("compound_treated_gas") != 1 ||
                    state.CountItem("compound_loaded_absorption_solution") != 1)
                    throw new Exception("absorption route failed");

                var crystalConditions = new Dictionary<string, string> {
                    { "concentration", "0.95" }, { "temperature", "0.10" }
                };
                CompleteDevice(devices, "u5_device_industrial_crystallizer",
                    "recipe_phase4a_industrial_crystallize", crystalConditions);
                if (state.CountItem("resource_high_purity_crystal") != 1 ||
                    state.CountItem("compound_mother_liquor") != 1)
                    throw new Exception("industrial crystallization route failed");

                state.AddItem("compound_n2", 1);
                state.AddItem("compound_h2", 2); // electrolysis 已提供 1，共 3
                var ironBefore = state.CountItem("material_iron");
                var pressureConditions = new Dictionary<string, string> {
                    { "pressure", "PRESSURE_HIGH" }, { "catalyst", "material_iron" }
                };
                CompleteDevice(devices, "u5_device_high_pressure_catalytic_reactor",
                    "recipe_phase4b_high_pressure_nh3", pressureConditions);
                if (state.CountItem("compound_nh3") != 2 || state.CountItem("material_iron") != ironBefore)
                    throw new Exception("high-pressure catalyst contract failed");

                check("专属设备化学(18 路由注册/检测/过滤/蒸馏/冷凝/储气/冶炼/电解/吸收/结晶/高压催化)");
            }
            catch (Exception e) { fail("专属设备化学: " + e.Message); }

            // ---- 12. 上游世界化学与 Device 15 玩家可达性 ----
            try
            {
                var state = new GameState { laboratoryStage = 3, currentRegion = "fluorescent_grotto" };
                var world = new WorldChemistryService(state);
                state.AddItem("organic_glucose", 1);
                if (!world.StartOrganicProcess().success || state.CountItem("compound_ethanol") != 1 ||
                    state.CountGeneratedGas("compound_co2") != 1 || state.CountVapor("compound_ethanol") != 1)
                    throw new Exception("organic process/vapor reachability failed");

                state.currentRegion = "abandoned_mine";
                state.AddItem("item_abandoned_mine_barium_bearing_mineral_block", 1);
                if (world.PrepareBariumSolution().reason != "analysis_required")
                    throw new Exception("barium analysis gate failed");
                state.IdentifySample("item_abandoned_mine_barium_bearing_mineral_block");
                if (!world.PrepareBariumSolution().success || state.CountItem("compound_barium_process_solution") != 1)
                    throw new Exception("barium process solution failed");

                state.currentRegion = "ember_rift";
                state.AddItem("item_ember_rift_sulfur_bearing_mineral_block", 1);
                state.IdentifySample("item_ember_rift_sulfur_bearing_mineral_block");
                if (!world.PrepareSulfateSolution().success || state.CountItem("compound_sulfate_process_solution") != 1)
                    throw new Exception("sulfate process solution failed");

                state.currentRegion = "radiation_belt";
                state.AddItem("item_radiation_belt_gas_bearing_deposit_block", 1);
                if (!world.ProcessGasBearingDeposit().success ||
                    state.CountGeneratedGas("compound_contaminated_gas_stream") != 1)
                    throw new Exception("gas bearing deposit process failed");

                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                foreach (var id in new[] { "device_high_pressure_gas_tank", "device_industrial_absorption_tower",
                                           "device_large_industrial_liquid_tank" })
                {
                    state.AddCraftedDevice(id);
                    if (!devices.Place(id, "u6_" + id, state.currentRegion, 12f, -3f).success)
                        throw new Exception("U6 place failed: " + id);
                }
                CompleteDevice(devices, "u6_device_high_pressure_gas_tank", "recipe_phase3a_store_gas",
                    new Dictionary<string, string> { { "gas_id", "compound_contaminated_gas_stream" } });
                state.AddItem("resource_alkaline_solution", 1);
                CompleteDevice(devices, "u6_device_industrial_absorption_tower", "recipe_phase4a_absorb_gas",
                    new Dictionary<string, string> { { "gas_id", "compound_contaminated_gas_stream" } });
                if (state.CountItem("compound_purified_gas_stream") != 1 ||
                    state.CountItem("compound_contaminated_absorbent") != 1)
                    throw new Exception("contaminated gas end-to-end route failed");

                var tank = state.DeviceInstance("u6_device_large_industrial_liquid_tank");
                if (tank.storage_capacity != 20 || tank.region_id != "radiation_belt" ||
                    tank.world_x != 12f || tank.world_y != -3f)
                    throw new Exception("placed storage/world contract failed");
                state.AddItem("compound_distilled_water", 12);
                state.AddItem("compound_mother_liquor", 1);
                state.AddItem("material_iron", 1);
                if (devices.Store(tank.instance_id, "material_iron").reason != "invalid_physical_state")
                    throw new Exception("liquid physical state guard failed");
                for (int i = 0; i < 10; i++)
                    if (!devices.Store(tank.instance_id, "compound_distilled_water").success)
                        throw new Exception("liquid store failed at " + i);
                if (devices.Store(tank.instance_id, "compound_mother_liquor").reason != "incompatible_stored_item")
                    throw new Exception("single-liquid compatibility failed");
                if (devices.Store(tank.instance_id, "compound_distilled_water", 11).reason != "storage_capacity_exceeded")
                    throw new Exception("liquid capacity guard failed");
                if (!devices.Withdraw(tank.instance_id).success ||
                    state.CountItem("compound_distilled_water") != 3 || tank.stored_amount != 9)
                    throw new Exception("liquid withdrawal conservation failed");

                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                var restoredTank = restored.DeviceInstance(tank.instance_id);
                if (restoredTank == null || restoredTank.stored_amount != 9 ||
                    restoredTank.stored_item_id != "compound_distilled_water" ||
                    restored.CountStoredGas("compound_contaminated_gas_stream") != 0 ||
                    !restored.HasChemistryKnowledge("bulk_liquid_reused"))
                    throw new Exception("world chemistry/storage save roundtrip failed");

                check("世界化学链路(发酵蒸汽/钡硫工艺液/含气沉积/污染气吸收/Device15 液体守恒与存档)");
            }
            catch (Exception e) { fail("世界化学与液体储罐: " + e.Message); }

            // ---- 13. 实验室 Stage 0→3：腐蚀里程碑 + 九项成功事件能力（禁止阶段注入） ----
            try
            {
                var state = new GameState();
                var progression = new LaboratoryProgressionService(state);
                state.AddCraftedDevice("device_multi_chemical_analyzer");
                state.AddVisitedRegion("geode_abyss");
                if (state.laboratoryStage != 0 || state.laboratoryCapabilities.Length != 0)
                    throw new Exception("ownership/region visit incorrectly granted progression");

                progression.RecordPickaxeRepaired();
                progression.RecordCorrosionGateDiscovered();
                state.AddItem("resource_rust_metal_residue", 1);
                state.AddItem("resource_chloride_deposit", 1);
                if (!progression.RecordCorrosionRawMaterials() ||
                    !progression.ProcessCorrosionBase().success ||
                    !progression.ProcessCorrosionCoating().success ||
                    !progression.UpgradeCorrosionPickaxe().success || state.laboratoryStage != 1)
                    throw new Exception("Stage 0→1 corrosion milestone route failed");

                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                devices.GameplayEventPublished += value => progression.RecordDeviceEvent(value);
                foreach (var id in new[] { "device_industrial_solid_liquid_filter", "device_industrial_distiller" })
                    state.AddCraftedDevice(id);
                foreach (var id in new[] { "device_multi_chemical_analyzer", "device_industrial_solid_liquid_filter",
                                           "device_industrial_distiller" })
                    if (!devices.Place(id, "u7_" + id).success) throw new Exception("Stage 1 place failed: " + id);
                state.AddItem("resource_mineral_water", 1);
                CompleteDevice(devices, "u7_device_multi_chemical_analyzer", "recipe_phase2_detect", null);
                state.AddItem("compound_mixed_reaction_slurry", 1);
                CompleteDevice(devices, "u7_device_industrial_solid_liquid_filter", "recipe_phase2_filter", null);
                CompleteDevice(devices, "u7_device_industrial_distiller", "recipe_phase2_distill", null);
                if (state.laboratoryStage != 2 ||
                    !LaboratoryProgressionService.Stage1To2Capabilities.All(state.HasLaboratoryCapability))
                    throw new Exception("Stage 1→2 capability contract failed");

                foreach (var id in new[] { "device_small_chemical_furnace", "device_high_pressure_gas_tank",
                                           "device_industrial_condenser", "device_large_industrial_reactor",
                                           "device_industrial_electrolysis_cell" })
                {
                    state.AddCraftedDevice(id);
                    if (!devices.Place(id, "u7_" + id).success) throw new Exception("Stage 2 place failed: " + id);
                }
                state.AddItem("resource_carbonate_rock", 1);
                CompleteDevice(devices, "u7_device_small_chemical_furnace", "recipe_phase3a_thermal_carbonate", null);
                state.ChangeGeneratedGas("compound_co2", 1);
                CompleteDevice(devices, "u7_device_high_pressure_gas_tank", "recipe_phase3a_store_gas", null);
                state.ChangeVapor("compound_ethanol", 1);
                CompleteDevice(devices, "u7_device_industrial_condenser", "recipe_phase3a_condense_vapor", null);
                state.AddItem("compound_fe2o3", 1);
                state.AddItem("element_c", 1);
                CompleteDevice(devices, "u7_device_large_industrial_reactor", "recipe_phase3b_reactor_batch", null);
                state.currentRegion = "magnetic_plateau";
                state.AddItem("compound_h2o", 1);
                CompleteDevice(devices, "u7_device_industrial_electrolysis_cell", "recipe_phase3b_electrolyze", null);

                state.currentRegion = "geode_abyss";
                var world = new WorldChemistryService(state);
                world.GameplayEventPublished += value => progression.RecordWorldChemistryEvent(value);
                state.AddItem("resource_mineral_solution", 1);
                if (!world.NaturalCrystallize("resource_mineral_solution", 0.90f).success)
                    throw new Exception("natural crystallization route failed");
                if (state.laboratoryStage != 3 ||
                    !LaboratoryProgressionService.Stage2To3Capabilities.All(state.HasLaboratoryCapability))
                    throw new Exception("Stage 2→3 nine-capability contract failed");

                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                if (restored.laboratoryStage != 3 ||
                    !restored.HasLaboratoryMilestone("first_corrosion_material") ||
                    !LaboratoryProgressionService.Stage2To3Capabilities.All(restored.HasLaboratoryCapability))
                    throw new Exception("laboratory progression save roundtrip failed");
                check("实验室进阶闭环(新存档 Stage 0→1 腐蚀路线/Stage 1→2 三能力/Stage 2→3 九能力/存档)");
            }
            catch (Exception e) { fail("实验室 Stage 0→3 进阶: " + e.Message); }

            // ---- 14. 17 区域环境：采矿抗性 / 深层通行 / 生存暴露 / 存档 ----
            try
            {
                var state = new GameState();
                var environment = new RegionEnvironmentService(state, GameData.Regions);
                if (GameData.Regions.regions.Length != 17)
                    throw new Exception("formal region count != 17");
                var validKeys = new[] { "heat", "cold", "corrosion", "radiation", "electrical" };
                foreach (var region in GameData.Regions.regions)
                    foreach (var requirement in region.mining_requirements ?? new ConditionThreshold[0])
                        if (!validKeys.Contains(requirement.id) || requirement.minimum < 0f)
                            throw new Exception("invalid mining contract: " + region.id + "/" + requirement.id);

                if (!environment.MiningAccess("dead_city_ruins").allowed)
                    throw new Exception("no-requirement region blocked");
                var rustBlocked = environment.MiningAccess("rust_jungle");
                if (rustBlocked.allowed || rustBlocked.reason != "corrosion_resistance_too_low" ||
                    Math.Abs(rustBlocked.required_resistance - 0.30f) > 0.0001f ||
                    Math.Abs(rustBlocked.actual_resistance - 0.20f) > 0.0001f)
                    throw new Exception("rust mining gate mismatch");
                state.AddLaboratoryMilestone("corrosion_pickaxe_upgraded");
                if (!environment.MiningAccess("rust_jungle").allowed)
                    throw new Exception("corrosion upgrade did not open rust mining");
                if (environment.MiningAccess("crystal_tundra").reason != "cold_resistance_too_low" ||
                    environment.MiningAccess("magnetic_plateau").reason != "electrical_resistance_too_low" ||
                    environment.MiningAccess("radiation_belt").reason != "radiation_resistance_too_low")
                    throw new Exception("distinct environment failure reasons mismatch");

                if (environment.RegionAccess("abandoned_mine").reason != "radiation_protection_required")
                    throw new Exception("depth 5 radiation route gate missing");
                state.radiationProtection = 0.35f;
                if (!environment.RegionAccess("abandoned_mine").allowed)
                    throw new Exception("radiation route gate did not open at 35%");
                if (environment.RegionAccess("core_shell").reason != "laboratory_stage_locked")
                    throw new Exception("core shell Stage 3 gate missing");
                state.laboratoryStage = 3;
                if (!environment.RegionAccess("core_shell").allowed ||
                    environment.RegionAccess("planetary_core").reason != "final_core_preparation_required")
                    throw new Exception("core access contracts mismatch");

                state.currentRegion = "rust_jungle";
                state.hp = 100f;
                state.radiation = 0f;
                var rust = GameData.Regions.regions.First(value => value.id == "rust_jungle");
                float damage = environment.TickExposure(rust, 10f);
                if (damage <= 0f || state.hp >= 100f || state.radiation <= 0f)
                    throw new Exception("rust exposure did not affect survival state");

                var upgradeState = new GameState();
                upgradeState.AddItem("resource_stone", 12);
                upgradeState.AddItem("resource_ore", 4);
                var upgrades = new RegionEnvironmentService(upgradeState, GameData.Regions);
                for (int i = 0; i < 4; i++)
                    if (!upgrades.UpgradeRadiationProtection()) throw new Exception("radiation upgrade failed at " + i);
                if (Math.Abs(upgradeState.radiationProtection - 0.8f) > 0.0001f ||
                    upgrades.UpgradeRadiationProtection() || upgradeState.CountItem("resource_stone") != 0 ||
                    upgradeState.CountItem("resource_ore") != 0)
                    throw new Exception("radiation upgrade cost/cap mismatch");

                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                var restoredEnvironment = new RegionEnvironmentService(restored, GameData.Regions);
                if (Math.Abs(restored.radiationProtection - 0.35f) > 0.0001f ||
                    !restoredEnvironment.MiningAccess("rust_jungle").allowed ||
                    !restoredEnvironment.RegionAccess("core_shell").allowed)
                    throw new Exception("environment contract save roundtrip failed");
                check("17区域环境合同(五类采矿抗性/深层辐射门/Stage3核心门/生存暴露/防护升级/存档)");
            }
            catch (Exception e) { fail("17 区域环境与通行: " + e.Message); }

            // ---- 15. 三档正式镐具与中后期采矿可达性 ----
            try
            {
                var state = new GameState();
                var tools = new MechanicalToolService(state);
                var environment = new RegionEnvironmentService(state, GameData.Regions);
                var baseStats = tools.EquippedStats;
                if (baseStats.id != MechanicalToolService.BaseTool || baseStats.mining_power != 1 ||
                    Math.Abs(baseStats.mining_speed - 1.0f) > 0.0001f ||
                    Math.Abs(baseStats.heat - 0.20f) > 0.0001f || Math.Abs(baseStats.radiation - 0.15f) > 0.0001f)
                    throw new Exception("base pickaxe stats mismatch");

                state.AddItem("element_fe", 3);
                var beforeIron = state.CountItem("element_fe");
                if (tools.Craft(MechanicalToolService.ImprovedRecipe, true).reason != "missing_elements" ||
                    state.CountItem("element_fe") != beforeIron || state.HasOwnedTool(MechanicalToolService.ImprovedTool))
                    throw new Exception("improved pickaxe failure mutated inventory");
                state.AddItem("element_fe", 1);
                if (!tools.Craft(MechanicalToolService.ImprovedRecipe, true).success ||
                    state.CountItem("element_fe") != 0 || state.equippedToolId != MechanicalToolService.BaseTool)
                    throw new Exception("improved pickaxe craft contract failed");
                if (!tools.Equip(MechanicalToolService.ImprovedTool).success)
                    throw new Exception("improved pickaxe equip failed");
                var improved = tools.EquippedStats;
                if (improved.mining_power != 2 || Math.Abs(improved.mining_speed - 1.35f) > 0.0001f ||
                    !environment.MiningAccess("crystal_tundra").allowed ||
                    !environment.MiningAccess("salt_marsh").allowed ||
                    !environment.MiningAccess("ember_rift").allowed ||
                    !environment.MiningAccess("magnetic_plateau").allowed ||
                    environment.MiningAccess("radiation_belt").reason != "radiation_resistance_too_low")
                    throw new Exception("improved pickaxe region reachability mismatch");

                state.AddItem("element_fe", 6);
                state.AddItem("element_cu", 3);
                if (!tools.Craft(MechanicalToolService.ReinforcedRecipe, true).success ||
                    !tools.Equip(MechanicalToolService.ReinforcedTool).success)
                    throw new Exception("reinforced pickaxe craft/equip failed");
                var reinforced = tools.EquippedStats;
                if (reinforced.mining_power != 3 || Math.Abs(reinforced.mining_speed - 1.70f) > 0.0001f ||
                    !environment.MiningAccess("radiation_belt").allowed ||
                    !environment.MiningAccess("mantle_flows").allowed ||
                    !environment.MiningAccess("core_shell").allowed)
                    throw new Exception("reinforced pickaxe late-region reachability mismatch");
                if (environment.MiningAccess("planetary_core").allowed)
                    throw new Exception("tier 3 incorrectly bypassed planetary 0.80 mining requirement");

                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                var restoredTools = new MechanicalToolService(restored);
                var restoredEnvironment = new RegionEnvironmentService(restored, GameData.Regions);
                if (restoredTools.EquippedStats.id != MechanicalToolService.ReinforcedTool ||
                    !restored.HasOwnedTool(MechanicalToolService.ImprovedTool) ||
                    !restored.HasOwnedTool(MechanicalToolService.ReinforcedTool) ||
                    !restoredEnvironment.MiningAccess("core_shell").allowed)
                    throw new Exception("tool progression save roundtrip failed");
                check("机械镐具闭环(基础/改良铁镐/复合强化镐/原子制造/装备/中后期区域抗性/存档)");
            }
            catch (Exception e) { fail("机械镐具与区域可达性: " + e.Message); }

            try
            {
                var state = new GameState();
                var world = new WorldChemistryService(state);
                foreach (var row in WorldChemistryService.DeadCityRecovery)
                {
                    state.AddItem(row.Key);
                    var before = JsonUtility.ToJson(state);
                    if (world.RecoverDeadCityBlock(row.Key).success || JsonUtility.ToJson(state) != before)
                        throw new Exception("unanalyzed recovery mutated state");
                    int outputBefore = state.CountItem(row.Value);
                    if (!world.AnalyzeDeadCityBlock(row.Key).success || state.CountItem(row.Key) != 1 ||
                        !world.RecoverDeadCityBlock(row.Key).success || state.CountItem(row.Key) != 0 ||
                        state.CountItem(row.Value) != outputBefore + 1)
                        throw new Exception("dead city recovery mismatch: " + row.Key);
                }
                if (world.RecoverDeadCityBlock("resource_stone").success)
                    throw new Exception("DESIGN_TBD stone gained invented output");
                // Construct a real first workbench using recovered materials, not owned-device injection.
                foreach (var pair in new[] { new KeyValuePair<string,int>("item_dead_city_iron_bearing_debris_block", 5),
                    new KeyValuePair<string,int>("item_dead_city_copper_bearing_debris_block", 1),
                    new KeyValuePair<string,int>("item_dead_city_glass_bearing_debris_block", 1) })
                {
                    state.AddItem(pair.Key, pair.Value);
                    for (int i = 0; i < pair.Value; i++)
                        if (!world.RecoverDeadCityBlock(pair.Key).success) throw new Exception("repeat recovery failed");
                }
                state.visitedRegions = new[] { "prologue_lab", "dead_city_ruins" };
                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                if (!devices.Craft("device_chemical_workbench").success || !devices.Place("device_chemical_workbench").success)
                    throw new Exception("recovered materials cannot bootstrap workbench");
                check("U10 上游材料（死城6种方块/分析/守恒回收/正式工作台制造）");
            }
            catch (Exception e) { fail("U10 上游材料: " + e.Message); }

            try
            {
                var state = new GameState { laboratoryStage = 3, radiationProtection = 0.4f };
                var world = new WorldChemistryService(state);
                var environment = new RegionEnvironmentService(state, GameData.Regions);
                if (world.RecycleMotherLiquor().success || environment.FinalCorePreparationReady())
                    throw new Exception("empty state incorrectly ready");
                state.AddItem("resource_contaminated_mineral");
                var before = JsonUtility.ToJson(state);
                if (world.PurifyContamination("resource_contaminated_mineral", 0.94f).success || JsonUtility.ToJson(state) != before)
                    throw new Exception("failed purification mutated state");
                if (!world.PurifyContamination("resource_contaminated_mineral", 0.72f).success ||
                    !world.PretreatIronOre().success) throw new Exception("purification / pretreatment failed");
                state.AddItem("element_c");
                if (!world.SmeltIron().success || !environment.FinalCorePreparationStatus()["metallurgy"])
                    throw new Exception("real metallurgy not recorded");
                var devices = new DeviceRuntimeService(state, GameData.Devices, GameData.ProcessingRecipes);
                // U9 boundary fixture: Stage 3 and earlier devices. U10 operations are real transactions.
                foreach (var id in new[] { "device_chemical_workbench", "device_industrial_absorption_tower",
                    "device_industrial_crystallizer", "device_industrial_electrolysis_cell", "device_high_pressure_catalytic_reactor" })
                {
                    state.AddCraftedDevice(id);
                    if (!devices.Place(id, "u10_" + id).success) throw new Exception("fixture device placement failed");
                }
                if (devices.Craft("device_large_industrial_liquid_tank").success)
                    throw new Exception("direct region-gated tank craft bypassed");
                var kitRecipe = GameData.ProcessingRecipes.recipes.First(r => r.id == "recipe_phase4b_liquid_tank_kit");
                foreach (var input in kitRecipe.input_items) state.AddItem(input.id, input.n);
                CompleteDevice(devices, "u10_device_chemical_workbench", kitRecipe.id, null);
                if (!devices.Craft("device_large_industrial_liquid_tank").success ||
                    state.CountItem("item_large_industrial_liquid_tank_kit") != 0 ||
                    !devices.Place("device_large_industrial_liquid_tank", "u10_tank").success)
                    throw new Exception("Godot Phase4b kit route before planetary core failed");
                state.ChangeStoredGas("compound_polluted_gas", 1);
                state.AddItem("resource_alkaline_solution");
                CompleteDevice(devices, "u10_device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null);
                state.AddItem("compound_concentrated_salt_solution");
                CompleteDevice(devices, "u10_device_industrial_crystallizer", "recipe_phase4a_industrial_crystallize",
                    new Dictionary<string, string> { { "concentration", "0.95" }, { "temperature", "0.10" } });
                if (!world.RecycleMotherLiquor().success || state.CountItem("compound_mother_liquor") != 0)
                    throw new Exception("mother liquor recycle failed");
                state.currentRegion = "magnetic_plateau";
                state.AddItem("compound_h2o");
                CompleteDevice(devices, "u10_device_industrial_electrolysis_cell", "recipe_phase3b_electrolyze", null);
                state.AddItem("compound_n2"); state.AddItem("compound_h2", 2); state.AddItem("material_iron");
                CompleteDevice(devices, "u10_device_high_pressure_catalytic_reactor", "recipe_phase4b_high_pressure_nh3",
                    new Dictionary<string, string> { { "pressure", "PRESSURE_HIGH" }, { "catalyst", "material_iron" } });
                if (environment.FinalCorePreparationReady()) throw new Exception("tank ownership incorrectly counted as logistics");
                if (!devices.Store("u10_tank", "compound_concentrated_salt_solution").success ||
                    state.HasChemistryKnowledge("bulk_liquid_reused") || !environment.FinalCorePreparationReady() ||
                    !environment.RegionAccess("planetary_core").allowed)
                    throw new Exception("storage-only logistics / final core access failed");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                if (!new RegionEnvironmentService(restored, GameData.Regions).FinalCorePreparationReady())
                    throw new Exception("preparation save roundtrip failed");
                // Only the nine authored preparation evidence groups are prerequisites.
                // Additional observations (e.g. ore pretreatment) must not become new gates.
                foreach (var knowledge in state.chemistryKnowledgeIds.Where(k =>
                    new[] { "purification_material_prepared", "mother_liquor_recycled", "electrolysis_completed", "pressure:reaction_nh3_from_n2", "catalyst:material_iron", "bulk_liquid_stored", "bulk_liquid_reused" }.Contains(k) ||
                    k.StartsWith("absorption:", StringComparison.Ordinal) || k.StartsWith("industrial_crystallization:", StringComparison.Ordinal) || k.StartsWith("metal_recovery:", StringComparison.Ordinal)))
                {
                    var missing = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                    missing.chemistryKnowledgeIds = missing.chemistryKnowledgeIds.Where(k => k != knowledge).ToArray();
                    if (new RegionEnvironmentService(missing, GameData.Regions).FinalCorePreparationReady())
                        throw new Exception("missing preparation evidence accepted: " + knowledge);
                }
                foreach (var id in new[] { "device_high_pressure_catalytic_reactor", "device_large_industrial_liquid_tank" })
                {
                    var missing = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                    missing.deviceInstances.First(d => d.device_id == id).is_placed = false;
                    if (new RegionEnvironmentService(missing, GameData.Regions).FinalCorePreparationReady())
                        throw new Exception("unplaced final device accepted");
                }
                restored.chemistryKnowledgeIds = restored.chemistryKnowledgeIds.Where(k => !k.StartsWith("metal_recovery:")).ToArray();
                restored.AddChemistryKnowledge("thermal:resource_carbonate_rock");
                if (new RegionEnvironmentService(restored, GameData.Regions).FinalCorePreparationReady())
                    throw new Exception("ordinary thermal processing incorrectly counted as metallurgy");
                check("U10 核心准备（净化/冶金/母液/11条件/储罐套件破循环/通行/存档）");
            }
            catch (Exception e) { fail("U10 核心准备: " + e.Message); }

            try
            {
                var state = new GameState();
                var core = new CoreProgressionService(state);
                var reactions = new ReactionService(state);
                if (core.ChooseEnding("sealed") || core.AttemptCoreTest()) throw new Exception("early ending accepted");
                state.AddReaction("reaction_n2");
                if (core.CompletedSteps != 0) throw new Exception("discovery backfilled chain");
                if (reactions.Execute("reaction_n2", new[] { "electric" }).success || core.CompletedSteps != 0)
                    throw new Exception("failed reaction advanced chain");
                core.RecordCommittedReaction("reaction_no");
                if (core.CompletedSteps != 0) throw new Exception("out-of-order reaction advanced chain");
                foreach (var id in CoreProgressionService.ReactionChain)
                {
                    var recipe = ChemistryEngine.Recipe(id);
                    foreach (var q in recipe.reactants) state.AddItem(q.id, q.n);
                    state.energy = 100f;
                    if (!reactions.Execute(id, recipe.tags.Concat(recipe.any_tags ?? new string[0])).success)
                        throw new Exception("chain commit failed: " + id);
                }
                state.currentRegion = "planetary_core";
                foreach (var memory in GameData.Memories.memories.Take(8)) state.AddMemory(memory.id);
                foreach (var shard in GameData.Shards.shards.Take(7)) state.AddCalmed(shard.id);
                if (core.AttemptCoreTest()) throw new Exception("seven keys accepted");
                state.AddCalmed(GameData.Shards.shards[7].id);
                var eightMemories = state.reconstructedMemories;
                state.reconstructedMemories = eightMemories.Take(7).ToArray();
                if (core.AttemptCoreTest()) throw new Exception("seven memories accepted");
                state.reconstructedMemories = eightMemories;
                if (!core.AttemptCoreTest() || core.AttemptCoreTest()) throw new Exception("core test transition failed");
                var restored = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
                foreach (var choice in new[] { "sealed", "fused" })
                {
                    var branch = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(restored));
                    var ending = new CoreProgressionService(branch);
                    if (ending.ChooseEnding("invalid") || !ending.ChooseEnding(choice) || ending.ChooseEnding(choice) ||
                        JsonUtility.FromJson<GameState>(JsonUtility.ToJson(branch)).endingChoice != choice)
                        throw new Exception("ending branch / persistence / duplicate guard failed");
                }
                check("U10 终局（5步真实反应/失败与乱序/8记忆8密钥/双结局/存档）");
            }
            catch (Exception e) { fail("U10 终局: " + e.Message); }

            try
            {
                PlayerRouteAcceptance.Run();
                check("U11 新状态到 Stage 3（真实制造11设备/中和沉淀过滤蒸馏/无阶段与设备注入）");
            }
            catch (Exception e) { fail("U11 新状态服务路线: " + e.Message); }

            try { TerrainAcceptance.ConservationAndRestore(); check("U12 地形守恒/十轮挖放/身份与增量读档"); }
            catch (Exception e) { fail("U12 地形守恒: " + e.Message); }
            try { TerrainAcceptance.FailureAndTiming(); check("U12 保护/放置拒绝/抗性强度/溢出/计时/坏增量"); }
            catch (Exception e) { fail("U12 地形失败合同: " + e.Message); }
            try { TerrainAcceptance.Collision(); check("U12 格子碰撞/高速防穿透/确定性"); }
            catch (Exception e) { fail("U12 格子碰撞: " + e.Message); }

            try { SaveAcceptance.LegacyAndAtomic(); check("U13 旧档兼容/校验封装/中断原子写入/临时文件隔离"); }
            catch (Exception e) { fail("U13 原子存档: " + e.Message); }
            try { SaveAcceptance.BackupRecovery(); check("U13 双备份/校验损坏/回退/坏档保留/未来版本保护"); }
            catch (Exception e) { fail("U13 存档恢复: " + e.Message); }
            try { SaveAcceptance.SlotIsolation(); check("U13 多世界隔离/新建不覆盖/槽ID验证"); }
            catch (Exception e) { fail("U13 世界槽: " + e.Message); }

            try { ResourceAcceptance.FiniteSources(); check("U14 有限采集/17区域正式资源/失败不扣容量/存档恢复"); }
            catch (Exception e) { fail("U14 有限采集: " + e.Message); }
            try { ResourceAcceptance.DropLifecycle(); check("U14 实体掉落/拾取守恒/磁盘恢复/重复与溢出防护/源范围"); }
            catch (Exception e) { fail("U14 掉落闭环: " + e.Message); }

            try { SnapshotSaveAcceptance.Lifecycle(); check("U15 不可变快照/连续保存/历史路由/两级回退/旧档保留"); }
            catch (Exception e) { fail("U15 快照生命周期: " + e.Message); }
            try { SnapshotSaveAcceptance.InterruptionAndVersion(); check("U15 中断重试/临时文件隔离/未来格式保护/首次迁移回退"); }
            catch (Exception e) { fail("U15 快照失败合同: " + e.Message); }
            try { SnapshotSaveAcceptance.RealPersistentDirectory(); check("U15 本机正式存档目录七次连续提交与读取"); }
            catch (Exception e) { fail("U15 正式目录: " + e.Message); }

            try { AgentAcceptance.ProfileLookupAndEntries(); check("U16 17区域Profile路由/14入场/3明确generic/Gameplay只读"); }
            catch (Exception e) { fail("U16 Profile与入场: " + e.Message); }
            try { AgentAcceptance.KnowledgeAndMilestones(); check("U16 知识保护/真实分析/制造里程碑/成熟抑制/未迁移分支边界"); }
            catch (Exception e) { fail("U16 知识与里程碑: " + e.Message); }
            try { AgentAcceptance.BudgetsQueueAndRecall(); check("U16 预算/优先抢占/暂停恢复/事件去重/只读回忆查询"); }
            catch (Exception e) { fail("U16 预算与队列: " + e.Message); }
            try { AgentAcceptance.Persistence(); check("U16 Agent快照/重载抑制/冷却/非法状态拒绝"); }
            catch (Exception e) { fail("U16 Agent持久化: " + e.Message); }

            try { RustAgentAcceptance.HazardAndStall(); check("U17 腐蚀确认/重复抑制/3与6次梯度/30秒停滞/只读"); }
            catch (Exception e) { fail("U17 腐蚀与停滞: " + e.Message); }
            try { RustAgentAcceptance.AnalysisAndKnowledgeGuard(); check("U17 真扫描/动态铁氯文本/知识保护/腐蚀解释/失败不变"); }
            catch (Exception e) { fail("U17 分析与知识: " + e.Message); }
            try { RustAgentAcceptance.CapabilitiesMaturityAndDanger(); check("U17 真防腐制造升级/跨区能力/成熟抑制/新危险合同"); }
            catch (Exception e) { fail("U17 能力与成熟: " + e.Message); }
            try { RustAgentAcceptance.FailureWorldReactionAndPersistence(); check("U17 失败梯度/正式反应确认/进展重置/专属存档"); }
            catch (Exception e) { fail("U17 失败与持久化: " + e.Message); }

            try { CrystalAgentAcceptance.EntryAndStall(); check("U18 晶体低温快照/未知保护/3-6-9次与30秒门/只读"); }
            catch (Exception e) { fail("U18 入场与梯度: " + e.Message); }
            try { CrystalAgentAcceptance.KnowledgeAndActualAnalyzer(); check("U18 实际检测仪制造放置分析/精确与通用碳酸盐/成熟抑制"); }
            catch (Exception e) { fail("U18 分析与成熟: " + e.Message); }
            try { CrystalAgentAcceptance.PhenomenaAndSuppression(); check("U18 现象双证据合同/未发布保护/UI抑制/跨区路由"); }
            catch (Exception e) { fail("U18 现象与抑制: " + e.Message); }
            try { CrystalAgentAcceptance.PersistenceAndFailure(); check("U18 专属快照恢复/非法成熟拒绝/旧档默认/分析失败保护"); }
            catch (Exception e) { fail("U18 持久化与失败: " + e.Message); }

            try { SaltAgentAcceptance.EntryAndLadder(); check("U19 溶液快照确认/未知保护/3-6-9与30秒梯度/UI抑制"); }
            catch (Exception e) { fail("U19 入场与梯度: " + e.Message); }
            try { SaltAgentAcceptance.AnalysisAndPh(); check("U19 真实检测/酸碱文本/组成与pH确认/源缺口保护"); }
            catch (Exception e) { fail("U19 分析与pH: " + e.Message); }
            try { SaltAgentAcceptance.ChemistryAndSeparation(); check("U19 实际中和沉淀/二次分离需要/过滤蒸馏领取后确认/成熟"); }
            catch (Exception e) { fail("U19 化学与分离: " + e.Message); }
            try { SaltAgentAcceptance.FailureAndPersistence(); check("U19 失败伪造保护/专属快照恢复/旧档/预算/跨区"); }
            catch (Exception e) { fail("U19 失败与持久化: " + e.Message); }

            try { EmberThermalAcceptance.Processing(); check("U20A 真实热处理冷凝交付/知识保护/只读/快照抑制/盐碱回忆"); }
            catch (Exception e) { fail("U20A 热处理: " + e.Message); }
            try { EmberThermalAcceptance.Analysis(); check("U20B 四种余烬样本真实检测/精确硫保护/不虚构硫酸根/重复及快照抑制"); }
            catch (Exception e) { fail("U20B 分析: " + e.Message); }
            try { EmberThermalAcceptance.AttentionAndEntry(); check("U20C 高温入场确认/专属事实与成熟门/磁盘恢复/非法成熟拒绝/旧档默认"); }
            catch (Exception e) { fail("U20C 专属状态: " + e.Message); }
            try { EmberThermalAcceptance.UnknownLadder(); check("U20D 未知材料3-6-9与30秒门/知识保护/进展重置/UI与跨区保护/梯度磁盘恢复"); }
            catch (Exception e) { fail("U20D 未知提示: " + e.Message); }
            try { EmberThermalAcceptance.CapabilityAndFailures(); check("U20E 冷凝器真实制造能力/伪造保护/正式失败合同1-3-5-7/进展重置"); }
            catch (Exception e) { fail("U20E 能力与失败: " + e.Message); }
            try { EmberThermalAcceptance.Fuel(); check("U20F 碳燃料真实消费/次数/失败不提交/不凭空加能量/恢复/观察"); }
            catch (Exception e) { fail("U20F 燃料: " + e.Message); }
            try { EmberThermalAcceptance.FactDeltas(); check("U20G 逐事实差集/旧精确事实不复用/UI抑制记忆/重载保护/重复分析进展"); }
            catch (Exception e) { fail("U20G 事实差集: " + e.Message); }
            try { SandEntryAcceptance.Entries(); check("U21 风蚀沙漠入场回忆/地区与知识双门/三种旧知识/源文本/恢复抑制"); }
            catch (Exception e) { fail("U21 入场回忆: " + e.Message); }
            try { SandEntryAcceptance.HypothesisAndConfirmation(); check("U21 未知假说/伪造保护/真实消耗分析/跨区关联成熟/动态文本/磁盘恢复"); }
            catch (Exception e) { fail("U21 假说与确认: " + e.Message); }
            try { SandEntryAcceptance.LaddersAndFailures(); check("U21 3-6-9停滞/30秒边界/正式失败1-3-5-7合同/白名单/进展重置/梯度存档"); }
            catch (Exception e) { fail("U21 梯度与失败: " + e.Message); }
            try { SandEntryAcceptance.AnalysisBranches(); check("U21 四类地形实际分析/碳性质与一般分支/余烬温度假说/不虚构确认关联"); }
            catch (Exception e) { fail("U21 分析分支: " + e.Message); }
            try { GrottoAgentAcceptance.EnvironmentAndFacts(); check("U22 含氧快照/余烬回忆/四样本真实分析/逐事实保护/UI抑制"); }
            catch (Exception e) { fail("U22 环境与分析: " + e.Message); }
            try { GrottoAgentAcceptance.Ladders(); check("U22 3-6-9停滞/30秒门/1-2-4-6正式失败合同/进展重置"); }
            catch (Exception e) { fail("U22 梯度: " + e.Message); }
            try { GrottoAgentAcceptance.RealProcessAndStorage(); check("U22 实际发酵产气/身份保护/储气梯度/实际储罐/成熟与磁盘恢复"); }
            catch (Exception e) { fail("U22 发酵与储气: " + e.Message); }
            try { GrottoAgentAcceptance.DetectorAndGasBranches(); check("U22 实际检测仪独立记录/不消耗样本/气体精确身份门/冷凝区别/未观察保护"); }
            catch (Exception e) { fail("U22 检测与产气分支: " + e.Message); }
            try { GeodeAgentAcceptance.DetectorFacts(); check("U23 四样本真实检测/通用识别拒绝/逐事实去重/无虚构纯度"); }
            catch (Exception e) { fail("U23 检测: " + e.Message); }
            try { GeodeAgentAcceptance.NaturalProcessAndSave(); check("U23 真实自然结晶/有限浓度边界/物料守恒/四门成熟/磁盘恢复"); }
            catch (Exception e) { fail("U23 结晶: " + e.Message); }
            try { GeodeAgentAcceptance.ComparisonAndContracts(); check("U23 跨区比较优先级/条件假说/3-6-9与1-2-4-6/未来纯度合同/旧档"); }
            catch (Exception e) { fail("U23 比较与合同: " + e.Message); }
            try { MineAgentAcceptance.IdentificationAndProcessing(); check("U24 权威八矿档案/六矿识别预处理/四金属真实还原/地形与设备方向"); }
            catch (Exception e) { fail("U24 矿物处理: " + e.Message); }
            try { MineAgentAcceptance.DisplacementPrecipitationAndSave(); check("U24 真实置换/无反应不扣料/沉淀跨区比较/成熟与磁盘恢复"); }
            catch (Exception e) { fail("U24 置换沉淀: " + e.Message); }
            try { MineAgentAcceptance.LaddersAndGuards(); check("U24 未知保护/3-6-9与1-2-4-6/只读策略/进展重置/旧档"); }
            catch (Exception e) { fail("U24 梯度保护: " + e.Message); }
            try { MineAgentAcceptance.RealDetectorAndBatch(); check("U24 四种真实检测/组成来源/工业批量沉淀制造与领取后观察"); }
            catch (Exception e) { fail("U24 设备分析与工业沉淀: " + e.Message); }
            try { MagneticEntryAcceptance.Entries(); check("U25 权威电活动入场/边界与无效快照/源文本/只读与重复抑制"); }
            catch (Exception e) { fail("U25 电活动入场: " + e.Message); }
            try { MagneticEntryAcceptance.ActualAnalyses(); check("U25 四样本真实检测/不消费/逐事实保护/性质不冒充过程/快照恢复"); }
            catch (Exception e) { fail("U25 磁暴分析: " + e.Message); }
            try { MagneticEntryAcceptance.Ladders(); check("U25 源提示映射/3-6-9与30秒/正式失败白名单/1-2-4-6/梯度磁盘恢复"); }
            catch (Exception e) { fail("U25 磁暴梯度: " + e.Message); }
            try { MagneticEntryAcceptance.Electrolysis(); check("U25 真实电解制造/领取守恒/四门成熟/磁盘恢复/矿井与气体设备比较"); }
            catch (Exception e) { fail("U25 电解与成熟: " + e.Message); }
            try { RadiationAgentAcceptance.EnvironmentAndAnalyses(); check("U26 风险快照边界/四样本真实检测/污染与氧化分离/磁盘快照"); }
            catch (Exception e) { fail("U26 环境与分析: " + e.Message); }
            try { RadiationAgentAcceptance.ProcessAndMaturity(); check("U26 真实检测/产气/净化/失败无提交/四门成熟/磁盘恢复/吸收合同"); }
            catch (Exception e) { fail("U26 净化与成熟: " + e.Message); }
            try { RadiationAgentAcceptance.ActualStorageAbsorption(); check("U26 实际储罐/吸收塔制造/储气与吸收剂守恒/失败保护/领取后观察"); }
            catch (Exception e) { fail("U26 储气吸收: " + e.Message); }
            try { RadiationAgentAcceptance.ScansAndPresence(); check("U26 三资源实际扫描/组成来源/不消耗与无pH/污染字段存在性/显式风险"); }
            catch (Exception e) { fail("U26 扫描与字段: " + e.Message); }
            try { MantleAgentAcceptance.EntryAndDetection(); check("U27 温度下限/边界/只读快照/四类真实检测/硫不冒充硫酸根/磁盘恢复"); }
            catch (Exception e) { fail("U27 入场与检测: " + e.Message); }
            try { MantleAgentAcceptance.ThermalContracts(); check("U27 两条真实极热路线/失败零提交/显式温度/五门成熟合同/磁盘恢复"); }
            catch (Exception e) { fail("U27 极热与成熟: " + e.Message); }
            try { MantleAgentAcceptance.IndustrialDeviceAndSelection(); check("U27 实际工业结晶制造/冷却失败/领取成熟/六类源回忆与设备选择"); }
            catch (Exception e) { fail("U27 工业结晶与选择: " + e.Message); }
            try { MantleAgentAcceptance.LoadedSolutions(); check("U27 载液三处理/不伪造解锁/实际载液结晶选材/有限条件与边界/守恒"); }
            catch (Exception e) { fail("U27 载液处理: " + e.Message); }
            try { CoreShellAgentAcceptance.EntryAndDetection(); check("U28 压力来源/环境设备分离/四类实际检测/知识守卫"); }
            catch (Exception e) { fail("U28 入场检测: " + e.Message); }
            try { CoreShellAgentAcceptance.ContractsAndPersistence(); check("U28 多条件字段存在性/铁银区别/回忆优先级/失败阶梯/多步合同/磁盘恢复"); }
            catch (Exception e) { fail("U28 事件合同: " + e.Message); }
            try { CoreShellAgentAcceptance.ActualPressureRoutes(); check("U28 实际合成氨与银催化路线/条件失败/缓冲领取/催化剂守恒/事件缺口不伪造"); }
            catch (Exception e) { fail("U28 高压路线: " + e.Message); }
            try { PlanetaryAgentAcceptance.EntryAndDetection(); check("U29 权威危险/自主观察/四类实际检测/知识守卫"); }
            catch (Exception e) { fail("U29 入场检测: " + e.Message); }
            try { PlanetaryAgentAcceptance.FailuresStorageAndEnergy(); check("U29 首次失败静默/阶梯/储液非反应语义/解锁门槛/实际充能设备复用"); }
            catch (Exception e) { fail("U29 失败与设备: " + e.Message); }
            try { PlanetaryAgentAcceptance.RecallMatureAndDisk(); check("U29 十一类单次回忆/分类优先级/成熟例外/玩家路线归属/磁盘恢复"); }
            catch (Exception e) { fail("U29 回忆与成熟: " + e.Message); }
            try { PlanetaryAgentAcceptance.ActualStorage(); check("U29 实际储罐制造/存取守恒/普通成功静默/不伪造条件与终局/跨区归属"); }
            catch (Exception e) { fail("U29 实际储液: " + e.Message); }
            try { CombatAcceptance.SourceAndCombat(); check("U30 十五类权威敌人/单位/生成/追击/距离边界/冷却/鼠标攻击/一次奖励"); }
            catch (Exception e) { fail("U30 战斗合同: " + e.Message); }
            try { CombatAcceptance.PersistenceAndRejection(); check("U30 战斗快照/受伤冷却/死亡不重复奖励/非法存档拒绝/旧档初始化"); }
            catch (Exception e) { fail("U30 战斗存档: " + e.Message); }
            try { SurvivalAcceptance.RulesAndProtection(); check("U31 生存有限值/生命辐射边界/附加防护/阈值/腐蚀不重复扣血"); }
            catch (Exception e) { fail("U31 生存规则: " + e.Message); }
            try { SurvivalAcceptance.MedkitAndDisk(); check("U31 医疗包独立计数/准确效果/缺失零提交/磁盘恢复/非法计数拒绝"); }
            catch (Exception e) { fail("U31 医疗包存档: " + e.Message); }
            try { PlayerMotorAcceptance.Motion(); check("U32 源运动参数/跳跃/悬浮分时能耗/时长/终速/交互锁"); }
            catch (Exception e) { fail("U32 移动: " + e.Message); }
            try { PlayerMotorAcceptance.UnlockAndSave(); check("U32 实际氢反应成功解锁/失败不授予/物品不冒充/存档与旧进度兼容"); }
            catch (Exception e) { fail("U32 能力: " + e.Message); }
            Debug.Log(failed == 0 ? "[Slice] 冒烟测试全部通过 ✔" : "[Slice] 冒烟测试失败 " + failed + " 项 ✘");
            return failed == 0;
        }

        public static void ValidateMigration()
        {
            EditorApplication.Exit(RunSmokeTest() ? 0 : 1);
        }

        static void CompleteDevice(DeviceRuntimeService devices, string instanceId, string recipeId,
            IDictionary<string, string> conditions)
        {
            var started = devices.Start(instanceId, recipeId, conditions);
            if (!started.success) throw new Exception(recipeId + " start failed: " + started.reason);
            devices.Advance(10f);
            var collected = devices.Collect(instanceId);
            if (!collected.success) throw new Exception(recipeId + " collect failed: " + collected.reason);
        }
    }
}
