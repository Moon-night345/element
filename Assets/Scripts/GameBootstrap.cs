// 元素星海·裂核 — Unity 垂直切片主控
// 世界构建(8 地表 + 9 地下,程序化美术)/玩家控制/交互/碎片谜题/记忆重构/任务链/双结局。
// 数据全部来自 Resources/Data/*.json(Godot 仓库单一事实来源导出)。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using ElementalStarSea.Data;
using ElementalStarSea.Agent;

namespace ElementalStarSea.Game
{
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>跨场景标记:标题菜单完成后才构建世界。</summary>
        public static bool GameStarted;
        bool _loading;
        bool submissionKeyReview;
        int submissionKeyStep;
        static int submissionMenuPhase;
        static string submissionMenuSlot = "";
        bool _terrainSmoke;
        bool _saveSmoke;
        string _restartProbeMode = "";
        bool _rustRestartProbe;
        bool _crystalRestartProbe;
        bool _saltRestartProbe;
        float _lastSaveWarningAt = -10f;
        float _loadingT;
        // ---- 数据 ----
        GameState _state;
        QuestChainDatabase _quest;
        RegionsDatabase _regions;
        ShardsDatabase _shards;
        MemoriesDatabase _memories;
        EndingsDatabase _endings;
        ReactionService _reactionService;
        DeviceRuntimeService _deviceRuntime;
        WorldChemistryService _worldChemistry;
        LaboratoryProgressionService _laboratoryProgression;
        RegionEnvironmentService _environmentRuntime;
        MechanicalToolService _mechanicalTools;
        AgentCatalog _agentCatalog;
        AgentController _agent;
        readonly HashSet<string> _announcedAnalyses = new HashSet<string>();
        long _agentClockBase;
        float _agentClockStart;
        FiniteResourceService _resources;
        WorldDropService _drops;
        CombatRuntimeService _combat;
        bool _combatPointerCaptured;
        readonly Dictionary<string, CombatEnemyVisual> _enemyVisuals = new Dictionary<string, CombatEnemyVisual>();
        readonly Dictionary<string, GameObject> _dropVisuals = new Dictionary<string, GameObject>();
        float _hydrogenReadyAt;
        readonly HashSet<string> _deviceVisualIds = new HashSet<string>();

        readonly Dictionary<string, ShardDef> _shardById = new Dictionary<string, ShardDef>();
        readonly Dictionary<string, RegionDef> _regionById = new Dictionary<string, RegionDef>();
        readonly Dictionary<string, RegionPalette> _surfacePalettes = new Dictionary<string, RegionPalette>();

        // ---- 世界/玩家 ----
        Transform _player;
        CharacterSprite _char;
        Camera _camera;
        GameUI _ui;
        AmbientParticles _ambient;
        float _moveX;
        float _moveY;
        float _vy;
        float _hoverElapsed;
        PlayerMotorService _playerMotor;
        TerrainRuntimeService _terrain;
        readonly TerrainMiningProgress _mining = new TerrainMiningProgress();
        SpriteRenderer _terrainCursor;
        string _buildItem = "resource_soil";
        bool OnGround
        {
            get { return _terrain != null ? _terrain.Collides(TerrainRuntimeService.PlayerBounds(
                (Vector2)_player.position + Vector2.down * 0.08f)) : Mathf.Abs(_player.position.y - CurrentGroundY()) < 0.08f; }
        }

        float CurrentGroundY()
        {
            if (_player.position.y > -6f) return 1.2f;
            int layer = Mathf.Clamp(Mathf.FloorToInt((-_player.position.y - 10f) / UndergroundLayerHeight),
                                    0, _undergroundOrder.Count - 1);
            return GroundTopY - 10f - (layer + 1) * UndergroundLayerHeight + 1.2f;
        }

        // ---- 视觉动画 ----
        class Floaty { public Transform tr; public float baseY; public float phase; public float amp; }
        class Pulse { public Transform tr; public SpriteRenderer sr; public float phase; public float max; }
        readonly List<Floaty> _floaties = new List<Floaty>();
        readonly List<Pulse> _pulses = new List<Pulse>();
        class ShardTracker
        {
            public string shardId;
            public TextMesh labelTm, trustTm;
            public SpriteRenderer beamSr;
        }
        readonly List<ShardTracker> shardTrackers = new List<ShardTracker>();

        // ---- 背景画/引路人/音效 ----
        SpriteRenderer _backdrop;
        string _bgRegion = "";
        Transform _lumen;
        SpriteRenderer _lumenSr;
        Sprite[] _lumenFrames;
        float _lumenT;
        AudioSource _sfx, _amb, _steps;

        // ---- 布局常量(与 Godot EnvironmentLayoutCatalog 的 7+9 结构对应) ----
        const float SurfaceRegionWidth = 80f;
        const float UndergroundLayerHeight = 40f;
        const float GroundTopY = 0f;
        const float CameraOrthographicSize = 7f; // 14 单位高实验室内景恰好完整进入视野
        const float CameraVerticalOffset = 4.5f;

        readonly List<string> _surfaceOrder = new List<string>();      // prologue_lab 最西
        readonly List<string> _undergroundOrder = new List<string>();

        // ---- 交互 ----
        class Interactable
        {
            public Vector2 pos;
            public float radius = 3.2f;
            public Func<string> label;
            public Func<bool> requiredStep;    // 返回 false 时不出现在交互探测中
            public Action action;
            public Func<Vector2> follow;       // 跟随世界物件(空则用固定 pos)
            public Vector2 Pos { get { return follow != null ? follow() : pos; } }
        }

        readonly List<Interactable> _interactables = new List<Interactable>();

        ShardDef _activeShard;
        bool _movedOnce;
        float _autosaveTimer;

        // ───────────────────────── 生命周期 ─────────────────────────
        void Awake()
        {
            var arguments = Environment.GetCommandLineArgs();
            submissionKeyReview = arguments.Contains("--submission-key-review");
            if (submissionKeyReview)
            {
                Application.runInBackground = true; GameStarted = true;
                GameState.SaveFileNameOverride = "keyboard_review_" + Guid.NewGuid().ToString("N") + ".json";
                GameState.SceneProbeSaveDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../..", "Logs", "keyboard_review_" + Guid.NewGuid().ToString("N")));
            }
            if (arguments.Contains("--submission-menu-review"))
            {
                Application.runInBackground = true;
                if (string.IsNullOrEmpty(GameState.SceneProbeSaveDirectory))
                    GameState.SceneProbeSaveDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../..", "Logs", "submission_menu_" + Guid.NewGuid().ToString("N")));
                Debug.Log("[SUBMISSION_MENU_SCOPE] " + GameState.SceneProbeSaveDirectory);
            }
            if (arguments.Contains("--ui-review") || arguments.Contains("--submission-route-review")) Application.runInBackground = true;
            _rustRestartProbe = arguments.Contains("--u17-save-write") || arguments.Contains("--u17-save-read");
            _crystalRestartProbe = arguments.Contains("--u18-save-write") || arguments.Contains("--u18-save-read");
            _saltRestartProbe = arguments.Contains("--u19-save-write") || arguments.Contains("--u19-save-read");
            _restartProbeMode = arguments.Contains("--u15-save-write") || arguments.Contains("--u17-save-write") || arguments.Contains("--u18-save-write") || arguments.Contains("--u19-save-write") ? "write" :
                arguments.Contains("--u15-save-read") || arguments.Contains("--u17-save-read") || arguments.Contains("--u18-save-read") || arguments.Contains("--u19-save-read") ? "read" : "";
            _saveSmoke = arguments.Contains("--u13-smoke") || arguments.Contains("--u14-smoke") || arguments.Contains("--u15-smoke") || arguments.Contains("--u16-smoke") || arguments.Contains("--u17-smoke") || arguments.Contains("--u18-smoke") || arguments.Contains("--u19-smoke");
            _saveSmoke = _saveSmoke || arguments.Contains("--u20-smoke");
            _terrainSmoke = _saveSmoke || arguments.Contains("--u12-smoke") || _restartProbeMode != "" || arguments.Contains("--ui-review") || arguments.Contains("--submission-route-review");
            if (_terrainSmoke)
            {
                GameState.SaveFileNameOverride = "u12_probe_" + Guid.NewGuid().ToString("N") + ".json";
                if (_restartProbeMode != "")
                {
                    var tokenArgument = arguments.FirstOrDefault(a => a.StartsWith("--u15-token=", StringComparison.Ordinal) || a.StartsWith("--u17-token=", StringComparison.Ordinal) || a.StartsWith("--u18-token=", StringComparison.Ordinal) || a.StartsWith("--u19-token=", StringComparison.Ordinal));
                    Guid token;
                    if (tokenArgument == null || !Guid.TryParseExact(tokenArgument.Substring(tokenArgument.IndexOf('=') + 1), "N", out token))
                        throw new ArgumentException("Restart save probe requires a unique --u15-token=<32 hex GUID>");
                    GameState.SaveFileNameOverride = "u15_restart_" + token.ToString("N") + ".json";
                }
                if (Environment.GetCommandLineArgs().Contains("--u14-smoke-local-save"))
                {
                    GameState.SceneProbeSaveDirectory = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                        "..", "..", "..", "Logs", "u14_scene_save_" + Guid.NewGuid().ToString("N")));
                    Debug.Log("[U14_SAVE_SCOPE] ISOLATED_PROJECT_LOGS; ordinary persistentDataPath is NOT verified by this run");
                }
                GameStarted = true;
            }
            GameData.LoadAll();
            _quest = GameData.QuestChain;
            _regions = GameData.Regions;
            _shards = GameData.Shards;
            _memories = GameData.Memories;
            _endings = GameData.Endings;
            ChemistryEngine.Load(LoadChemistry());
            Localization.Load(LoadLocalization());

            foreach (var s in _shards.shards) _shardById[s.id] = s;
            foreach (var r in _regions.regions) _regionById[r.id] = r;
            _surfaceOrder.Add("prologue_lab");
            _surfaceOrder.AddRange(_regions.order.surface);
            _undergroundOrder.AddRange(_regions.order.underground);
            BuildPalettes();

            _state = GameState.LoadOrNew();
            _reactionService = new ReactionService(_state);
            _reactionService.GameplayEventPublished += OnReactionGameplayEvent;
            _deviceRuntime = new DeviceRuntimeService(_state, GameData.Devices, GameData.ProcessingRecipes);
            _deviceRuntime.GameplayEventPublished += OnDeviceGameplayEvent;
            _worldChemistry = new WorldChemistryService(_state);
            _worldChemistry.GameplayEventPublished += OnWorldChemistryEvent;
            _laboratoryProgression = new LaboratoryProgressionService(_state);
            _environmentRuntime = new RegionEnvironmentService(_state, _regions);
            _mechanicalTools = new MechanicalToolService(_state);
            _resources = new FiniteResourceService(_state, _regions);
            _drops = new WorldDropService(_state);

            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            _ui = uiGo.AddComponent<GameUI>();
            GameState.SaveFailed += OnSaveFailed;
            Application.wantsToQuit += SaveBeforeQuit;
            WireMenu();

            // 相机在菜单态就存在(否则 Game view 显示 No cameras rendering)
            _camera = new GameObject("MainCamera").AddComponent<Camera>();
            _camera.gameObject.tag = "MainCamera";
            _camera.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
            _camera.orthographic = true;
            _camera.orthographicSize = CameraOrthographicSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.gameObject.AddComponent<AudioListener>();   // 否则所有音效无声

            if (!GameStarted)
            {
                _ui.ShowTitle();
                if (arguments.Contains("--submission-menu-review")) StartCoroutine(RunSubmissionMenuReview());
                return;
            }

            InitGame();
            if (arguments.Contains("--submission-menu-review")) { StartCoroutine(RunSubmissionMenuReview()); return; }
            if (arguments.Contains("--submission-route-review")) { StartCoroutine(RunSubmissionRouteReview()); return; }
            if (arguments.Contains("--ui-review")) { StartCoroutine(CaptureUIReview()); return; }
            if (_restartProbeMode != "") StartCoroutine(RunSaveRestartProbe());
            else if (_terrainSmoke) StartCoroutine(RunTerrainSceneProbe());
        }

        void MenuReviewClick(string name)
        {
            _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b => b.name == name && b.gameObject.activeInHierarchy).onClick.Invoke();
        }

        System.Collections.IEnumerator RunSubmissionMenuReview()
        {
            var steps = SubmissionMenuSteps();
            while (true)
            {
                bool more;
                try { more = steps.MoveNext(); }
                catch (Exception error) { Debug.LogException(error); Debug.LogError("[SUBMISSION_MENU] FAIL"); Application.Quit(1); yield break; }
                if (!more) break;
                yield return steps.Current;
            }
        }

        System.Collections.IEnumerator SubmissionMenuSteps()
        {
            yield return null;
            if (submissionMenuPhase == 0)
            {
                MenuReviewClick("azone_zone_new"); yield return null;
                submissionMenuSlot = GameState.CurrentSaveSlot;
                if (!submissionMenuSlot.StartsWith("world_", StringComparison.Ordinal) || GameState.HasSave()) throw new InvalidOperationException("New world did not choose fresh slot");
                submissionMenuPhase = 1; MenuReviewClick("azone_zone_create");
                Debug.Log("[SUBMISSION_MENU] new-world button -> world selection -> real loading/scene reload");
            }
            else if (submissionMenuPhase == 1)
            {
                if (!GameStarted || !AtStep("awaken") || GameState.CurrentSaveSlot != submissionMenuSlot) throw new InvalidOperationException("New-world scene did not start fresh");
                ObserveAwakeningInput(true); CapturePlayerPosition();
                _ui.TogglePause(); yield return null;
                MenuReviewClick("Title"); yield return null;
                if (GameStarted || !GameState.HasSave()) throw new InvalidOperationException("Pause save/title failed");
                MenuReviewClick("azone_zone_load"); yield return null;
                if (GameState.ListSaveSlots().Length != 1) throw new InvalidOperationException("Menu slot listing leaked outside isolated root");
                MenuReviewClick("Slot_" + submissionMenuSlot); yield return null;
                submissionMenuPhase = 2; MenuReviewClick("azone_zone_create");
                Debug.Log("[SUBMISSION_MENU] pause/save/title -> real load slot button -> loading/scene reload");
            }
            else
            {
                if (!GameStarted || !AtStep("guide_contact") || GameState.CurrentSaveSlot != submissionMenuSlot || !_state.hasPlayerPosition) throw new InvalidOperationException("Loaded scene did not restore slot progress/position");
                Debug.Log("[SUBMISSION_MENU] PASS: actual title/world/loading buttons, two real scene reloads, fresh slot, pause-save-title, real slot enumeration/selection and restored progress; isolated save root, ordinary player saves untouched");
                Application.Quit(0);
            }
        }

        void WalkToSubmissionTarget(string prefix)
        {
            _ui.CloseAllPanels();
            var target = _interactables.First(i => i.label().StartsWith(prefix, StringComparison.Ordinal) && (i.requiredStep == null || i.requiredStep()));
            for (int step = 0; step < 3000 && Vector2.Distance(_player.position, target.Pos) >= target.radius * .8f; step++)
                AdvancePlayerMovement(Mathf.Sign(target.Pos.x - _player.position.x), false, false, 1 / 60f);
            if (Vector2.Distance(_player.position, target.Pos) >= target.radius) throw new InvalidOperationException("Cannot walk to " + prefix);
            target.action();
        }

        System.Collections.IEnumerator RunSubmissionRouteReview()
        {
            var route = SubmissionRouteSteps();
            while (true)
            {
                bool more;
                try { more = route.MoveNext(); }
                catch (Exception error) { Debug.LogException(error); Debug.LogError("[SUBMISSION_ROUTE] FAIL"); Application.Quit(1); yield break; }
                if (!more) break;
                yield return route.Current;
            }
            Application.Quit(0);
        }

        System.Collections.IEnumerator SubmissionRouteSteps()
        {
            yield return null;
            if (!AtStep("awaken") || _state.CountItem("element_h") != 0) throw new InvalidOperationException("Submission route must start fresh");
            ObserveAwakeningInput(true);
            WalkToSubmissionTarget("引路人终端"); _ui.AdvanceOrClose(); _ui.AdvanceOrClose();
            if (!AtStep("find_exit")) throw new InvalidOperationException("Guide dialogue did not advance");
            WalkToSubmissionTarget("地表气闸");
            if (!AtStep("repair_pickaxe")) throw new InvalidOperationException("Airlock discovery did not unlock repair");
            WalkToSubmissionTarget("手工工作台");
            if (!AtStep("capture_hydrogen")) throw new InvalidOperationException("Repair did not unlock capture");
            WalkToSubmissionTarget("氢泄漏舱");
            yield return new WaitForSecondsRealtime(.7f);
            WalkToSubmissionTarget("氢泄漏舱");
            if (!AtStep("synthesis") || _state.CountItem("element_h") != 2) throw new InvalidOperationException("Finite hydrogen source did not supply exactly two");
            WalkToSubmissionTarget("合成仪");
            ClickMineProbeOption("H", "_synOwned"); ClickMineProbeOption("H", "_synOwned");
            _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Combine").onClick.Invoke();
            if (!AtStep("demonstrate") || _state.CountItem("compound_h2") != 1 || _state.CountItem("element_h") != 0 || !_state.HasAbility("ability_h2_hover")) throw new InvalidOperationException("UI synthesis did not unlock demonstration");
            WalkToSubmissionTarget("引路人终端");
            if (!AtStep("airlock")) throw new InvalidOperationException("Guide demonstration did not unlock airlock");
            WalkToSubmissionTarget("地表气闸");
            if (!AtStep("calm_shards") || _state.currentRegion != "dead_city_ruins" || _terrain.Collides(TerrainRuntimeService.PlayerBounds(_player.position))) throw new InvalidOperationException("Airlock arrival failed");
            _ui.CloseAllPanels(); RefreshHud();
            for (int i = 0; i < 4; i++) yield return null;
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../..", "Logs"));
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, "submission_route_exit.png"));
            _ui.OnReturnLab();
            var restored = GameState.LoadOrNew();
            if (_state.currentRegion != "prologue_lab" || restored.currentRegion != "prologue_lab" || !restored.HasVisitedRegion("dead_city_ruins") || restored.CountItem("compound_h2") != 1 || !restored.HasAbility("ability_h2_hover")) throw new InvalidOperationException("Return/save restore lost route state");
            Debug.Log("[SUBMISSION_ROUTE] PASS: fresh awakening, physical fixed-step walking, guide dialogue, closed airlock discovery, actual repair/capture cooldown/two finite H, real material/Combine buttons, guide demonstration, airlock entry, return-lab callback and disk restoration; no inventory/quest injection; synthetic input not OS keyboard");
        }

        System.Collections.IEnumerator CaptureUIReview()
        {
            _ui.CloseAllPanels(); RefreshHud();
            for (int i = 0; i < 8; i++) yield return null;
            string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../..", "Logs"));
            string prefix = "ui_" + Screen.width + "x" + Screen.height;
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_hud.png"));
            yield return new WaitForSecondsRealtime(.6f);
            OpenDeviceConsole();
            for (int i = 0; i < 5; i++) yield return null;
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_devices.png"));
            yield return new WaitForSecondsRealtime(.6f);
            _ui.ShowDialogue("引路人", "先看看实验室里的设备。准备好后，我们可以分析样本、合成物质，再向外面的区域探索。", new[] { "查看当前任务", "继续探索", "了解实验设备", "稍后再说" }, option => _ui.CloseAllPanels());
            for (int i = 0; i < 5; i++) yield return null;
            _ui.AdvanceOrClose();
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_dialogue.png"));
            yield return new WaitForSecondsRealtime(.6f);
            _ui.ShowDialogue("长内容检查", string.Join("\n", Enumerable.Range(1, 28).Select(i => "第 " + i + " 条记录：比较样本与处理条件，继续探索实验室。")), Enumerable.Range(1, 8).Select(i => "检查选项 " + i + "：查看这份较长的实验记录").ToArray(), option => _ui.CloseAllPanels());
            _ui.AdvanceOrClose();
            for (int i = 0; i < 4; i++) yield return null;
            Canvas.ForceUpdateCanvases();
            var dialogRoot = _ui.GetComponentInChildren<Canvas>().transform.Find("DialoguePanel");
            var bodyScroll = dialogRoot.Find("BodyViewport").GetComponent<UnityEngine.UI.ScrollRect>();
            var optionScroll = dialogRoot.Find("OptionsViewport").GetComponent<UnityEngine.UI.ScrollRect>();
            if (bodyScroll.content.rect.height <= bodyScroll.viewport.rect.height || optionScroll.content.rect.height <= optionScroll.viewport.rect.height) throw new InvalidOperationException("Long dialogue/options did not become scrollable");
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_long_dialogue.png"));
            Debug.Log("[LONG_CONTENT] PASS: 28 dialogue lines and 8 long options remain inside independent scroll viewports");
            _ui.CloseAllPanels(); string beforeMap = JsonUtility.ToJson(_state); Vector3 beforeMapPosition = _player.position;
            _ui.OnMap();
            for (int i = 0; i < 4; i++) yield return null;
            if (!_ui.AnyPanelOpen() || _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b => b.name.StartsWith("MapNode_", StringComparison.Ordinal)) != 17) throw new InvalidOperationException("Map region coverage/lock failed");
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_map.png"));
            MenuReviewClick("MapNode_planetary_core");
            if (beforeMap != JsonUtility.ToJson(_state) || beforeMapPosition != _player.position) throw new InvalidOperationException("Map selection mutated gameplay");
            MenuReviewClick("CloseMap");
            if (_ui.AnyPanelOpen()) throw new InvalidOperationException("Map close failed");
            Debug.Log("[MAP_UI] PASS: 17 source regions, current/visited/access, selection read-only, gameplay lock and close");
            _ui.CloseAllPanels(); _ui.TogglePause();
            for (int i = 0; i < 4; i++) yield return null;
            if (!_ui.AnyPanelOpen()) throw new InvalidOperationException("UI pause did not lock gameplay");
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_pause.png"));
            _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Resume").onClick.Invoke();
            if (_ui.AnyPanelOpen()) throw new InvalidOperationException("UI resume did not close pause");
            _state.AddItem("element_h", 2); int hydrogen = _state.CountItem("compound_h2");
            _ui.ShowSynthesis(new List<string> { "element_h", "element_o", "element_c", "element_fe", "compound_h2o" }, new List<string> { "element_h", "element_h" }, 0, 40,
                () => _reactionService.Execute("reaction_h2_synthesis", new[] { "synthesis_device" }));
            for (int i = 0; i < 4; i++) yield return null;
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_synthesis.png"));
            _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Combine").onClick.Invoke();
            if (_state.CountItem("compound_h2") != hydrogen + 1) throw new InvalidOperationException("UI synthesis callback did not commit");
            _ui.CloseAllPanels();
            _ui.ShowBackpack(Enumerable.Range(1, 40).Select(i => "样本 " + i + "    ·    数量 ×" + i).ToArray());
            for (int i = 0; i < 4; i++) yield return null;
            if (!_ui.AnyPanelOpen()) throw new InvalidOperationException("UI backpack did not lock gameplay");
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_backpack.png"));
            _ui.CloseAllPanels(); _ui.TogglePause();
            _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Title" && b.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("保存")).onClick.Invoke();
            if (GameStarted) throw new InvalidOperationException("UI return to title failed");
            for (int i = 0; i < 4; i++) yield return null;
            yield return CaptureReviewFrame(System.IO.Path.Combine(folder, prefix + "_title.png"));
            Debug.Log("[UI_REVIEW] captured " + prefix + "; pause/resume, actual synthesis commit, backpack lock and save-return-title PASS; feedstock/40 inventory text rows are isolated UI fixtures"); Application.Quit(0);
        }

        System.Collections.IEnumerator CaptureReviewFrame(string path)
        {
            var canvas = _ui.GetComponentInChildren<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = _camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); yield return null;
            var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
            var previous = RenderTexture.active; _camera.targetTexture = target; _camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
            System.IO.File.WriteAllBytes(path, texture.EncodeToPNG()); Destroy(texture);
            _camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Destroy(target); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // ───────────────────────── 区域调色板 ─────────────────────────
        void BuildPalettes()
        {
            _surfacePalettes["prologue_lab"] = RegionPalette.Surface(
                new Color(0.15f, 0.21f, 0.37f), new Color(0.45f, 0.52f, 0.71f),
                new Color(0.28f, 0.33f, 0.45f), new Color(0.35f, 0.85f, 0.95f), "dust", 40, 120);
            _surfacePalettes["dead_city_ruins"] = RegionPalette.Surface(
                new Color(0.20f, 0.17f, 0.15f), new Color(0.62f, 0.52f, 0.36f),
                new Color(0.42f, 0.37f, 0.32f), new Color(1f, 0.75f, 0.40f), "dust", 40, 120);
            _surfacePalettes["rust_jungle"] = RegionPalette.Surface(
                new Color(0.27f, 0.15f, 0.12f), new Color(0.71f, 0.44f, 0.25f),
                new Color(0.48f, 0.27f, 0.18f), new Color(1f, 0.60f, 0.30f), "dust", 2, 121);
            _surfacePalettes["crystal_tundra"] = RegionPalette.Surface(
                new Color(0.10f, 0.20f, 0.34f), new Color(0.50f, 0.72f, 0.85f),
                new Color(0.37f, 0.49f, 0.59f), new Color(0.55f, 0.90f, 1f), "snow", 80, 100);
            _surfacePalettes["salt_marsh"] = RegionPalette.Surface(
                new Color(0.13f, 0.21f, 0.17f), new Color(0.49f, 0.63f, 0.49f),
                new Color(0.30f, 0.42f, 0.32f), new Color(0.60f, 1f, 0.60f), "spore", 0, 40);
            _surfacePalettes["ember_rift"] = RegionPalette.Surface(
                new Color(0.22f, 0.08f, 0.06f), new Color(0.77f, 0.34f, 0.18f),
                new Color(0.43f, 0.20f, 0.15f), new Color(1f, 0.50f, 0.30f), "ember", 120, 121);
            _surfacePalettes["sand_desert"] = RegionPalette.Surface(
                new Color(0.29f, 0.24f, 0.13f), new Color(0.85f, 0.72f, 0.47f),
                new Color(0.69f, 0.56f, 0.35f), new Color(1f, 0.85f, 0.45f), "sand", 60, 4);
            _surfacePalettes["magnetic_plateau"] = RegionPalette.Surface(
                new Color(0.15f, 0.12f, 0.25f), new Color(0.48f, 0.42f, 0.72f),
                new Color(0.31f, 0.27f, 0.41f), new Color(0.70f, 0.60f, 1f), "spark", 40, 121);
        }

        RegionPalette PaletteOf(RegionDef def)
        {
            if (def.kind != "underground")
            {
                RegionPalette p;
                return _surfacePalettes.TryGetValue(def.id, out p) ? p
                    : RegionPalette.Surface(new Color(0.2f, 0.2f, 0.3f), new Color(0.5f, 0.5f, 0.6f),
                                            new Color(0.35f, 0.35f, 0.4f), Color.cyan, "dust", 40, 120);
            }
            return RegionPalette.Underground(def.depth - 1, _undergroundOrder.Count);
        }

        // ───────────────────────── 世界构建 ─────────────────────────
        void BuildWorld()
        {
            var catalog = JsonUtility.FromJson<TerrainCatalog>(Resources.Load<TextAsset>("Data/terrain_catalog").text);
            _terrain = new TerrainRuntimeService(_state, _regions, catalog);
            // 背景画层(Godot 手绘/AI 区域背景,跟随相机,按区域切换)
            var backdropGo = new GameObject("backdrop");
            _backdrop = backdropGo.AddComponent<SpriteRenderer>();
            _backdrop.sortingOrder = -70;

            for (int i = 0; i < _surfaceOrder.Count; i++)
            {
                var def = _regionById[_surfaceOrder[i]];
                float cx = i * SurfaceRegionWidth;
                var pal = PaletteOf(def);
                int seed = i * 17 + 3;
                WorldArt.BuildSky(new Vector2(cx, GroundTopY), SurfaceRegionWidth, pal, seed);
                if (def.id == "prologue_lab")
                    BuildLabInterior(cx);   // 实验室是室内:背景画锚定世界坐标,人物走在画中地板上
                _terrain.AddPatch(def.id, Mathf.RoundToInt(cx - SurfaceRegionWidth * 0.5f), 0,
                    Mathf.RoundToInt(SurfaceRegionWidth), 8);
                if (def.id != "prologue_lab")
                    WorldArt.BuildProps(new Vector2(cx, GroundTopY), SurfaceRegionWidth, pal, seed);
                SpriteUtil.WorldLabel("label_" + def.id, new Vector2(cx, GroundTopY + 8.5f),
                    Localization.Tr(def.name_key), 22, new Color(1f, 1f, 1f, 0.8f));
                BuildRegionContents(def, new Vector2(cx, GroundTopY));
            }

            // 地下:9 层,统一竖井在东西向中心
            float midX = (_surfaceOrder.Count - 1) * SurfaceRegionWidth * 0.5f;
            float floorW = _surfaceOrder.Count * SurfaceRegionWidth;
            for (int d = 0; d < _undergroundOrder.Count; d++)
            {
                var def = _regionById[_undergroundOrder[d]];
                float y = GroundTopY - 10f - (d + 1) * UndergroundLayerHeight;
                var pal = PaletteOf(def);
                int seed = 100 + d * 19 + 5;
                WorldArt.BuildSky(new Vector2(midX, y), floorW, pal, seed);
                _terrain.AddPatch(def.id, Mathf.RoundToInt(midX - floorW * 0.5f), Mathf.RoundToInt(y), Mathf.RoundToInt(floorW), 8);
                WorldArt.BuildProps(new Vector2(midX, y), floorW, pal, seed);
                SpriteUtil.WorldLabel("label_" + def.id, new Vector2(midX, y + 8.5f),
                    Localization.Tr(def.name_key) + string.Format("(深度 {0})", def.depth),
                    20, new Color(1f, 1f, 1f, 0.55f));
                BuildRegionContents(def, new Vector2(midX, y));
            }

            BuildLabFixtures();
            BuildCoreTerminal(new Vector2(midX, CoreFloorY() + 2.2f));
            _terrain.RestoreChanges();
            new GameObject("TerrainWorld").AddComponent<TerrainWorldView>().Bind(_terrain);
            _terrainCursor = SpriteUtil.RectWorld("terrain_cursor", Vector2.zero, Vector2.one,
                new Color(0.4f, 0.9f, 1f, 0.25f), 8).GetComponent<SpriteRenderer>();
            _terrainCursor.enabled = false;
        }

        float CoreFloorY()
        {
            return GroundTopY - 10f - _undergroundOrder.Count * UndergroundLayerHeight;
        }

        // ───────────────────────── 视觉元件 ─────────────────────────
        SpriteRenderer MakeGlow(Vector2 pos, Color c, float size, float alphaMax)
        {
            var glow = new GameObject("glow");
            glow.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = glow.AddComponent<SpriteRenderer>();
            sr.sprite = TexGen.SpriteOf(TexGen.SoftCircle("glow" + c.GetHashCode(), c), 64f);
            glow.transform.localScale = new Vector3(size, size, 1f);
            sr.color = new Color(c.r, c.g, c.b, alphaMax * 0.5f);
            sr.sortingOrder = -6;
            _pulses.Add(new Pulse { tr = glow.transform, sr = sr, phase = pos.x * 0.7f, max = alphaMax });
            return sr;
        }

        SpriteRenderer MakeCrystal(Vector2 pos, Color c, float size)
        {
            var go = new GameObject("crystal");
            go.transform.position = new Vector3(pos.x, pos.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArtLib.Tile("tilemap", 67, ArtLib.TilemapCols);   // 蓝钻瓦片
            if (sr.sprite == null) sr.sprite = SpriteUtil.Square();
            go.transform.localScale = new Vector3(size / ArtLib.TileWorld, size / ArtLib.TileWorld, 1f);
            sr.color = Color.Lerp(c, Color.white, 0.25f);
            sr.sortingOrder = 3;
            _floaties.Add(new Floaty { tr = go.transform, baseY = pos.y, phase = pos.x * 1.3f, amp = 0.18f });
            return sr;
        }

        /// <summary>
        /// 实验室室内(原版做法):内景画锚定在世界坐标,随镜头 1:1 滚动。
        /// 画高 22 单位,画中地板线(约 85% 高度)精确落在行走线 y=0 上;
        /// 两幅镜像平铺盖满 80 单位区域,人物左右走动时视角在画中穿行。
        /// </summary>
        void BuildLabInterior(float cx)
        {
            const float H = 18f;                     // Cover the camera above the floor without exposing the outdoor sky.
            const float FloorLineY = 0f;             // Walkable foreground is about 74.5% down the authored image.
            float ppu = 941f / H;
            var sp = ArtLib.Single("bg_prologue_lab", ppu);
            if (sp == null) return;
            float w = sp.bounds.size.x;              // ≈24.9
            float bottomY = FloorLineY - 0.255f * H;
            for (int t = 0; t < 6; t++)
            {
                var go = new GameObject("labwall" + t);
                float x = cx - w * 3f + w * t;
                go.transform.position = new Vector3(x + w * 0.5f, bottomY + H * 0.5f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                sr.flipX = t % 2 == 1;               // 镜像平铺,接缝自然
                sr.sortingOrder = -70;
            }
            // 地板前沿:画底以下三层暖棕渐变,延续画中地板色调直到视野底
            float frontTop = bottomY;                       // ≈ -0.5
            var bandA = SpriteUtil.RectWorld("lfa", new Vector2(cx, frontTop - 0.55f),
                new Vector2(SurfaceRegionWidth + 40f, 1.1f + 40f * 0f + 0.0f + 1.1f), new Color(0.33f, 0.25f, 0.17f), -69);
            var bandB = SpriteUtil.RectWorld("lfb", new Vector2(cx, frontTop - 2.1f),
                new Vector2(SurfaceRegionWidth + 40f, 2.2f), new Color(0.24f, 0.18f, 0.12f), -69);
            var bandC = SpriteUtil.RectWorld("lfc", new Vector2(cx, frontTop - 4.3f),
                new Vector2(SurfaceRegionWidth + 40f, 3.6f), new Color(0.15f, 0.11f, 0.08f), -69);
            bandA.GetComponent<SpriteRenderer>().sortingOrder = -69;
            bandB.GetComponent<SpriteRenderer>().sortingOrder = -69;
            bandC.GetComponent<SpriteRenderer>().sortingOrder = -69;
            // 走廊地板条(原版 floor_strip):顶边与角色脚底齐平,作为行走表面
            var strip = ArtLib.Single("lab_floor_strip", 1408f / SurfaceRegionWidth);
            if (strip != null)
            {
                var go = new GameObject("labfloor");
                go.transform.position = new Vector3(cx, 0.25f - strip.bounds.size.y * 0.5f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = strip;
                sr.sortingOrder = -8;
            }
        }

        // ───────────────────────── 区域内容 ─────────────────────────
        void BuildRegionContents(RegionDef def, Vector2 center)
        {
            var pal = PaletteOf(def);

            // 碎片水晶
            if (!string.IsNullOrEmpty(def.shard) && _shardById.ContainsKey(def.shard))
            {
                var shardId = def.shard;
                var pos = center + new Vector2(10f, 0.4f);
                // 底座
                var pedestal = SpriteUtil.RectWorld("pedestal_" + shardId, pos + new Vector2(0f, 0.5f),
                    new Vector2(3.4f, 1f), new Color(0.32f, 0.26f, 0.2f), 2);
                // 主晶 + 双侧晶(水晶簇)
                MakeCrystal(pos + new Vector2(0f, 2.6f), new Color(0.4f, 0.9f, 1f), 2.6f);
                MakeCrystal(pos + new Vector2(-1.3f, 1.8f), new Color(0.4f, 0.9f, 1f), 1.4f);
                MakeCrystal(pos + new Vector2(1.3f, 1.9f), new Color(0.4f, 0.9f, 1f), 1.6f);
                MakeGlow(pos + new Vector2(0f, 2.4f), new Color(0.4f, 0.9f, 1f), 7f, 0.5f);
                // 名字+信任(金字,原版在水晶上方)
                var shardLabel = SpriteUtil.WorldLabel("shard_label_" + shardId,
                    pos + new Vector2(0f, 7.6f), Localization.Tr(_shardById[shardId].name_key),
                    22, AccentGold);
                var trustLabel = SpriteUtil.WorldLabel("shard_trust_" + shardId,
                    pos + new Vector2(0f, 6.7f), "信任 0%", 15, new Color(0.9f, 1f, 0.8f));
                var beam = SpriteUtil.RectWorld("shard_beam_" + shardId, pos + new Vector2(0f, 12f),
                    new Vector2(0.7f, 9f), new Color(0.55f, 1f, 0.6f, 0.25f), 1);
                shardTrackers.Add(new ShardTracker
                {
                    shardId = shardId,
                    labelTm = shardLabel,
                    trustTm = trustLabel,
                    beamSr = beam.GetComponent<SpriteRenderer>(),
                });
                _interactables.Add(new Interactable
                {
                    pos = pos,
                    label = () => _state.HasCalmed(shardId)
                        ? "已平息的碎片 [E]"
                        : "碎片水晶 [E] 回应它的语言",
                    action = () => TalkToShard(shardId),
                });
            }

            // 记忆终端
            var mem = _memories.memories.FirstOrDefault(m => m.region == def.id);
            if (mem != null)
            {
                var memId = mem.id;
                var pos = center + new Vector2(-12f, 2f);
                var terminal = SpriteUtil.RectWorld("memory_" + memId, pos,
                    new Vector2(1.1f, 1.8f), new Color(0.68f, 0.42f, 0.95f), 3);
                terminal.transform.rotation = Quaternion.Euler(0, 0, 45f);
                var tileSpr = ArtLib.Tile("tilemap", 67, ArtLib.TilemapCols);
                if (tileSpr != null)
                {
                    terminal.GetComponent<SpriteRenderer>().sprite = tileSpr;
                    terminal.transform.rotation = Quaternion.identity;
                    terminal.transform.localScale = new Vector3(1.25f, 1.25f, 1f);
                    terminal.GetComponent<SpriteRenderer>().color = new Color(1f, 0.62f, 1.4f);
                }
                MakeGlow(pos, new Color(0.68f, 0.42f, 0.95f), 3.6f, 0.42f);
                SpriteUtil.WorldLabel("mem_label_" + memId, pos + new Vector2(0f, 1.8f),
                    "记忆终端", 13, new Color(0.85f, 0.65f, 1f));
                _interactables.Add(new Interactable
                {
                    follow = () => terminal.transform.position,
                    label = () => _state.HasMemory(memId) ? "记忆(已重构) [E]" : "记忆终端 [E]",
                    action = () => OpenMemory(memId),
                });
            }

            // 资源采集点(实验室西侧留空,避免与工作台/氢泄漏舱重叠)
            float off = def.id == "prologue_lab" ? -32f : 20f;
            foreach (var res in def.resources)
            {
                if (def.id == "prologue_lab" ? off > 0f : off > SurfaceRegionWidth * 0.42f) break;
                var materialId = res;
                var pos = center + new Vector2(off, 0.9f);
                var node = SpriteUtil.RectWorld("res_" + def.id + "_" + materialId, pos,
                    new Vector2(1.1f, 1.1f), Color.Lerp(pal.accent, Color.white, 0.35f), 3);
                var oreSpr = ArtLib.Tile("tilemap", 68, ArtLib.TilemapCols);   // 矿石堆瓦片
                if (oreSpr != null)
                {
                    node.GetComponent<SpriteRenderer>().sprite = oreSpr;
                    node.transform.rotation = Quaternion.identity;
                    node.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                }
                MakeGlow(pos, pal.accent, 2.6f, 0.4f);
                _interactables.Add(new Interactable
                {
                    follow = () => node.transform.position,
                    label = () => "采集 " + ChemistryEngine.DisplayName(materialId) + " [E] 剩余 " + _resources.RegionRemaining(def.id, materialId),
                    action = () => CollectChemicalResource(def.id, materialId),
                });
                off += 7f;
            }

            if (def.id != "prologue_lab")
            {
                // The compact scenery preview above has room for only two deposits.
                // Expose the full authored catalog rather than silently dropping later resources.
                if ((def.resources ?? new string[0]).Length > 2)
                    AddChemistryNode("resource_catalog_" + def.id, center + new Vector2(13f, 0.8f), pal.accent,
                        () => "区域资源采样 [E] 选择全部材料", () => ShowRegionResourcePage(def, 0));
                AddTerrainResourceNode(def.id, center + new Vector2(-5f, 0.9f),
                    "resource_stone", "石材方块", pal);
                AddTerrainResourceNode(def.id, center + new Vector2(3f, 0.9f),
                    "resource_ore", "矿石方块", pal);
            }

            // 地下层的上行/下行电梯(统一竖井)
            if (def.kind == "underground")
            {
                BuildElevator(new Vector2(center.x - 32f, center.y), def.depth - 1, "上行电梯 ↑", pal);
                BuildElevator(new Vector2(center.x + 32f, center.y), def.depth + 1, "下行电梯 ↓", pal);
            }
            else if (def.id == "dead_city_ruins")
            {
                // Godot RegionRuntimeContract: dead_city_ruins ↔ topsoil_caves 是正式地表/地下入口。
                BuildElevator(center + new Vector2(-36f, 0f), 1, "地下竖井 ↓", pal);
            }

            BuildChemistryWorldActions(def, center, pal);
        }

        void BuildChemistryWorldActions(RegionDef def, Vector2 center, RegionPalette pal)
        {
            if (def.id == "rust_jungle")
                AddChemistryNode("rust_sample_scan", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => "环境样本扫描 [E] 确认已采集样本", () => {
                        var samples = new[] { "resource_rust_metal_residue", "resource_chloride_deposit" };
                        string sample = samples.FirstOrDefault(id => _state.CountItem(id) > 0 && !_state.HasIdentifiedSample(id))
                            ?? samples.FirstOrDefault(id => _state.CountItem(id) > 0) ?? "";
                        ShowWorldChemistryResult(_worldChemistry.AnalyzeRustSample(sample));
                    });
            if (def.id == "sand_desert")
                AddChemistryNode("sand_analysis", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => "地形样本分析 [E] 消耗一份待分析方块",
                    () => ShowWorldChemistryResult(_worldChemistry.AnalyzeSandBlock(WorldChemistryService.SandBlocks.FirstOrDefault(id => _state.CountItem(id) > 0 && !_state.HasIdentifiedSample(id))
                        ?? WorldChemistryService.SandBlocks.FirstOrDefault(id => _state.CountItem(id) > 0) ?? "")));
            if (def.id == "salt_marsh")
            {
                AddChemistryNode("neutralize_solutions", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => "酸碱中和 [E] 酸液 + 碱液 → 盐溶液",
                    () => ShowWorldChemistryResult(_worldChemistry.NeutralizeSolutions()));
                AddChemistryNode("precipitate_solution", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "沉淀工艺槽 [E] 盐溶液 + 碱液 → 浆料",
                    () => ShowWorldChemistryResult(_worldChemistry.PrecipitateSolution()));
            }
            if (def.id == "dead_city_ruins")
            {
                var blocks = WorldChemistryService.DeadCityRecovery.Keys.Where(id => id != "item_dead_city_masonry_block").ToArray();
                var names = new[] { "耐火废料", "含碳废料", "含铁废料", "含铜废料", "玻璃废料" };
                for (int i = 0; i < blocks.Length; i++)
                {
                    string block = blocks[i];
                    string name = names[i];
                    AddChemistryNode(block, center + new Vector2(-28f + i * 4f, 0.8f), pal.accent,
                        () => name + (_state.CountItem(block) == 0 ? " [E] 开采" :
                            !_state.HasIdentifiedSample(block) ? " [E] 分析" : " [E] 回收材料"),
                        () =>
                        {
                            if (_state.CountItem(block) == 0) MineTerrainResource(def.id, block, name);
                            else if (!_state.HasIdentifiedSample(block)) ShowWorldChemistryResult(_worldChemistry.AnalyzeDeadCityBlock(block));
                            else ShowWorldChemistryResult(_worldChemistry.RecoverDeadCityBlock(block));
                        });
                }
            }
            if (def.id == "prologue_lab" || def.id == "core_shell")
                AddChemistryNode("core_preparation_" + def.id, center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "最终核心准备 [E] 查看清单 / 母液回收", ShowCorePreparation);
            if (def.id == "radiation_belt")
            {
                AddChemistryNode("radiation_scan", center + new Vector2(-15f, 0.8f), pal.accent,
                    () => "污染资源扫描 [E] 读取已采集样本，不消耗",
                    () => ShowWorldChemistryResult(_worldChemistry.AnalyzeRadiationSample(WorldChemistryService.RadiationSamples.FirstOrDefault(id => _state.CountItem(id) > 0 && !_state.HasChemistryKnowledge("resource_scan:" + id)) ??
                        WorldChemistryService.RadiationSamples.FirstOrDefault(id => _state.CountItem(id) > 0) ?? "")));
                AddChemistryNode("contaminated_sample", center + new Vector2(-27f, 0.8f), pal.accent,
                    () => "污染矿物封存罐 [E] 剩余 " + _resources.Remaining("sealed:radiation_belt", "resource_contaminated_mineral", 4),
                    () => CaptureFiniteSource("sealed:radiation_belt", "resource_contaminated_mineral", 4));
                AddChemistryNode("purify_sample", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "污染矿物净化 [E]", () => ShowWorldChemistryResult(
                        _worldChemistry.PurifyContamination("resource_contaminated_mineral", def.radiation)));
            }
            if (def.id == "mantle_flows")
            {
                AddChemistryNode("mantle_loaded_solution", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "载液处理 [E] 蒸馏 / 浓缩 / 中和",
                    () => _ui.ShowDialogue("载液处理", "选择已有载液的处理方向，每次消耗一份。", new[] { "蒸馏", "浓缩", "中和" }, choice => {
                        _ui.CloseAllPanels(); ShowWorldChemistryResult(_worldChemistry.ProcessLoadedSolution(choice == "蒸馏" ? "distill" : choice == "浓缩" ? "concentrate" : "neutralize")); }));
                AddChemistryNode("mantle_heat_process", center + new Vector2(-15f, 0.8f), pal.accent,
                    () => "地幔极热处理 [E] 硫 / 硫酸根与水",
                    () => ShowWorldChemistryResult(_worldChemistry.ExtremeHeatProcess(_state.CountItem("element_s") > 0 ? "element_s" : "ion_so4", _agentCatalog.mantle_authored_temperature_floor)));
            }
            if (def.id == "abandoned_mine")
            {
                AddChemistryNode("iron_pretreat", center + new Vector2(-27f, 0.8f), pal.accent,
                    () => "铁矿预处理 [E] 铁矿 → 氧化铁", () => ShowWorldChemistryResult(_worldChemistry.PretreatIronOre()));
                AddChemistryNode("iron_smelt", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "冶金还原 [E] 氧化铁 + 碳 → 铁", () => ShowWorldChemistryResult(_worldChemistry.SmeltIron()));
                AddTerrainResourceNode(def.id, center + new Vector2(-7f, 0.8f), "item_abandoned_mine_iron_bearing_ore_block", "含铁矿物块", pal);
                AddTerrainResourceNode(def.id, center + new Vector2(-3f, 0.8f), "item_abandoned_mine_copper_bearing_ore_block", "含铜矿物块", pal);
            }
            if (def.id == "core_shell")
                AddTerrainResourceNode(def.id, center + new Vector2(-7f, 0.8f), "item_core_shell_high_strength_mineral_block", "高强度矿物块", pal);
            if (def.id == "fluorescent_grotto")
            {
                AddChemistryNode("grotto_analysis", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "洞穴地形样本分析 [E] 消耗一份待分析方块",
                    () => ShowWorldChemistryResult(_worldChemistry.AnalyzeGrottoBlock(
                        WorldChemistryService.GrottoBlocks.FirstOrDefault(id => _state.CountItem(id) > 0 && !_state.HasIdentifiedSample(id)) ??
                        WorldChemistryService.GrottoBlocks.FirstOrDefault(id => _state.CountItem(id) > 0) ?? "")));
                AddChemistryNode("organic_fermenter", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => "低氧有机反应床 [E] 发酵并导流蒸汽",
                    () => ShowWorldChemistryResult(_worldChemistry.StartOrganicProcess()));
            }
            else if (def.id == "abandoned_mine")
            {
                AddChemistryNode("mine_analysis", center + new Vector2(-15f, 0.8f), pal.accent,
                    () => "矿井地形分析 [E] 消耗一份方块，读取正式记录",
                    () => ShowWorldChemistryResult(_worldChemistry.AnalyzeMineBlock(WorldChemistryService.MineBlocks.FirstOrDefault(id => _state.CountItem(id) > 0 && !_state.HasIdentifiedSample(id)) ??
                        WorldChemistryService.MineBlocks.FirstOrDefault(id => _state.CountItem(id) > 0) ?? "")));
                AddChemistryNode("mine_experiments", center + new Vector2(-11f, 0.8f), pal.accent,
                    () => "矿物实验台 [E] 识别 / 预处理 / 冶炼 / 置换 / 沉淀", ShowMineExperiments);
                const string sample = "item_abandoned_mine_barium_bearing_mineral_block";
                AddChemistryNode("barium_process", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => _state.CountItem(sample) <= 0 ? "钡矿脉 [E] 采集工艺样本" :
                        (!_state.HasIdentifiedSample(sample) ? "钡矿样本待分析 [D]" : "钡工艺槽 [E] 制备工艺液"),
                    () =>
                    {
                        if (_state.CountItem(sample) <= 0)
                        {
                            MineTerrainResource(def.id, sample, "钡载矿物块");
                        }
                        else ShowWorldChemistryResult(_worldChemistry.PrepareBariumSolution());
                    });
            }
            else if (def.id == "ember_rift")
            {
                const string sample = "item_ember_rift_sulfur_bearing_mineral_block";
                AddChemistryNode("carbon_fuel", center + new Vector2(-19f, 0.8f), pal.accent,
                    () => "碳燃料观察 [E] 消耗一份碳质材料",
                    () => ShowWorldChemistryResult(_worldChemistry.UseCarbonFuel(_state.CountItem("resource_carbonaceous_material") > 0 ? "resource_carbonaceous_material" : "material_carbon")));
                AddChemistryNode("sulfate_process", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => _state.CountItem(sample) <= 0 ? "含硫矿脉 [E] 采集工艺样本" :
                        (!_state.HasIdentifiedSample(sample) ? "含硫样本待分析 [D]" : "硫酸盐工艺槽 [E] 制备工艺液"),
                    () =>
                    {
                        if (_state.CountItem(sample) <= 0)
                        {
                            MineTerrainResource(def.id, sample, "含硫矿物块");
                        }
                        else ShowWorldChemistryResult(_worldChemistry.PrepareSulfateSolution());
                    });
            }
            else if (def.id == "radiation_belt")
            {
                const string sample = "item_radiation_belt_gas_bearing_deposit_block";
                AddChemistryNode("gas_bearing_process", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => _state.CountItem(sample) <= 0 ? "含气沉积层 [E] 采集密封样本" : "气相释放台 [E] 处理含气沉积物",
                    () =>
                    {
                        if (_state.CountItem(sample) <= 0)
                        {
                            MineTerrainResource(def.id, sample, "含气沉积块");
                        }
                        else ShowWorldChemistryResult(_worldChemistry.ProcessGasBearingDeposit());
                    });
            }
            else if (def.id == "geode_abyss")
            {
                AddChemistryNode("natural_crystallization", center + new Vector2(-23f, 0.8f), pal.accent,
                    () => "天然晶簇生长槽 [E] 浓缩结晶",
                    () =>
                    {
                        string solution = _state.CountItem("compound_concentrated_salt_solution") > 0
                            ? "compound_concentrated_salt_solution" : "resource_mineral_solution";
                        ShowWorldChemistryResult(_worldChemistry.NaturalCrystallize(solution, 0.90f));
                    });
            }
        }

        void AddChemistryNode(string id, Vector2 pos, Color color, Func<string> label, Action action)
        {
            var node = SpriteUtil.RectWorld("chemistry_node_" + id, pos,
                new Vector2(.55f, .55f), Color.Lerp(color, Color.white, 0.15f), 3);
            MakeGlow(pos, color, 3.4f, 0.35f);
            SpriteUtil.WorldLabel("chemistry_label_" + id, pos + new Vector2(0f, 2.2f),
                "化学工艺节点", 13, new Color(0.9f, 0.88f, 0.72f));
            _interactables.Add(new Interactable
            {
                follow = () => node.transform.position,
                label = label,
                action = action,
            });
        }

        void AddTerrainResourceNode(string regionId, Vector2 pos, string materialId, string displayName,
            RegionPalette pal)
        {
            var node = SpriteUtil.RectWorld("terrain_resource_" + materialId, pos,
                new Vector2(1.2f, 1.2f), Color.Lerp(pal.ground, Color.white, 0.3f), 3);
            var tile = ArtLib.Tile("tilemap", materialId == "resource_stone" ? 1 : 3, ArtLib.TilemapCols);
            if (tile != null) node.GetComponent<SpriteRenderer>().sprite = tile;
            _interactables.Add(new Interactable
            {
                follow = () => node.transform.position,
                label = () => "开采 " + displayName + " [E]（有限矿源）",
                action = () => MineTerrainResource(regionId, materialId, displayName),
            });
        }

        void ShowRegionResourcePage(RegionDef region, int page)
        {
            var resources = region.resources ?? new string[0];
            int pages = Math.Max(1, (resources.Length + 2) / 3);
            int index = Math.Max(0, Math.Min(page, pages - 1));
            var visible = resources.Skip(index * 3).Take(3).ToArray();
            var options = visible.Select((id, i) => (i + 1) + ". " + ChemistryEngine.DisplayName(id) +
                " 剩余 " + _resources.RegionRemaining(region.id, id)).ToList();
            if (pages > 1) options.Add("下一页");
            options.Add("关闭");
            _ui.ShowDialogue("区域资源 " + (index + 1) + "/" + pages,
                "采集 " + Localization.Tr(region.name_key) + " 的正式资源。选择材料采集一份；下一页查看更多。",
                options.ToArray(), choice =>
                {
                    if (choice == "下一页") { ShowRegionResourcePage(region, (index + 1) % pages); return; }
                    _ui.CloseAllPanels();
                    int picked = options.IndexOf(choice);
                    if (picked >= 0 && picked < visible.Length)
                    {
                        CollectChemicalResource(region.id, visible[picked]);
                        _state.Save();
                    }
                });
        }

        void ShowMineSelection(string title, string[] ids, Action<string> select)
        {
            if (ids.Length == 0) { _ui.ShowMessage("背包没有此实验可用的材料"); return; }
            string[] labels = ids.Select((id, i) => (i + 1) + ". " + ChemistryEngine.DisplayName(id)).ToArray();
            _ui.ShowDialogue(title, "选择当前持有的实际材料；实验将使用现有库存。", labels, choice => {
                int index = Array.IndexOf(labels, choice); if (index < 0) return; _ui.CloseAllPanels(); select(ids[index]); });
        }
        void ShowMineExperiments()
        {
            _ui.ShowDialogue("矿井实验", "先识别，再比较实际处理结果。无反应也会保留记录。", new[] { "矿石识别", "矿石预处理", "氧化物冶炼", "金属置换", "沉淀实验" }, choice => {
                _ui.CloseAllPanels();
                if (choice == "矿石识别" || choice == "矿石预处理")
                    ShowMineSelection(choice, GameData.OreProfiles.ores.Select(p => p.id).Where(id => _state.CountItem(id) > 0).ToArray(),
                        id => ShowWorldChemistryResult(choice == "矿石识别" ? _worldChemistry.IdentifyOre(id) : _worldChemistry.PretreatOre(id)));
                else if (choice == "氧化物冶炼")
                    ShowMineSelection(choice, GameData.OreProfiles.ores.Take(4).Where(p => _state.CountItem(p.oxide) > 0).Select(p => p.metal).ToArray(),
                        id => ShowWorldChemistryResult(_worldChemistry.SmeltOre(id)));
                else if (choice == "金属置换")
                    ShowMineSelection("选择投入金属", new[] { "element_zn", "element_fe", "element_cu", "element_ag" }.Where(id => _state.CountItem(id) > 0).ToArray(),
                        active => ShowMineSelection("选择目标材料", new[] { "ion_zn", "ion_fe", "ion_cu", "ion_ag", "compound_cuso4", "compound_agno3", "compound_fe2o3" }.Where(id => _state.CountItem(id) > 0).ToArray(),
                            target => ShowWorldChemistryResult(_worldChemistry.MetalDisplacement(active, target))));
                else if (choice == "沉淀实验")
                    ShowMineSelection("选择金属材料", new[] { "element_ba", "element_ag", "element_cu" }.Where(id => _state.CountItem(id) > 0).ToArray(),
                        cation => ShowMineSelection("选择配对材料", new[] { "ion_so4", "element_cl", "compound_hcl" }.Where(id => _state.CountItem(id) > 0).ToArray(),
                            anion => ShowWorldChemistryResult(_worldChemistry.PrecipitationTest(cation, anion))));
            });
        }

        void ShowWorldChemistryResult(WorldChemistryResult result)
        {
            if (result.success)
            {
                var text = result.inventory_delta.Length == 0 ? result.action_id : string.Join(", ",
                    result.inventory_delta.Where(value => value.change > 0)
                        .Select(value => ChemistryEngine.DisplayName(value.id) + "×" + value.change).ToArray());
                _ui.ShowMessage("工艺完成: " + text);
                _state.Save();
            }
            else if (result.action_id == "metal_displacement" && result.reason == "no_reaction")
            { _ui.ShowMessage("未发生置换：比较结果已记录，材料未消耗"); _state.Save(); }
            else _ui.ShowMessage("工艺失败: " + result.reason);
        }

        // ───────────────────────── 电梯 ─────────────────────────
        void BuildElevator(Vector2 pos, int targetDepth, string label, RegionPalette pal)
        {
            if (_terrain != null) _terrain.Protect(new Rect(pos.x - 2f, pos.y - 2f, 4f, 5f));
            if (targetDepth < 0 || targetDepth > _undergroundOrder.Count) return;
            string targetRegion = targetDepth == 0 ? "dead_city_ruins" : _undergroundOrder[targetDepth - 1];
            var go = SpriteUtil.RectWorld("elevator_" + targetDepth + "_" + pos.x, pos,
                new Vector2(2.2f, 3f), Color.Lerp(pal.accent, Color.white, 0.4f), 2);
            MakeGlow(pos, new Color(0.5f, 1f, 0.6f), 4.5f, 0.4f);
            SpriteUtil.WorldLabel("elevator_label_" + targetDepth + "_" + pos.x,
                pos + new Vector2(0f, 2.6f), label, 13, new Color(0.6f, 1f, 0.65f));
            _interactables.Add(new Interactable
            {
                follow = () => go.transform.position,
                label = () =>
                {
                    var access = _environmentRuntime.RegionAccess(targetRegion);
                    return access.allowed ? label + " [E]" : label + " — " + EnvironmentFailureText(access.reason) + " [E]";
                },
                action = () =>
                {
                    var access = _environmentRuntime.RegionAccess(targetRegion);
                    if (!access.allowed)
                    {
                        if (access.reason == "final_core_preparation_required") ShowCorePreparation();
                        else _ui.ShowMessage("电梯锁定: " + EnvironmentFailureText(access.reason));
                        return;
                    }
                    if (!ArriveAtRegion(targetRegion, RegionSpawnPos(targetRegion))) return;
                    Sfx("wrist_device");
                    _ui.ShowMessage("电梯到达 " + (targetDepth == 0
                        ? "地表"
                        : Localization.Tr(_regionById[_undergroundOrder[targetDepth - 1]].name_key)));
                },
            });
        }

        void BuildLabFixtures()
        {
            var lab = new Vector2(0f, GroundTopY);

            void Add(string name, Vector2 offset, string artName, float artHeight, string labelText,
                     Action action, Func<bool> step)
            {
                GameObject go = null;
                if (artName == "painting")
                {
                    // 视觉已在背景画中:只放名称牌和标记
                }
                else if (!string.IsNullOrEmpty(artName))
                    go = WorldArt.ArtProp(artName, lab + offset, artHeight);
                if (go == null && artName != "painting")
                    go = SpriteUtil.RectWorld("fixture_" + name, lab + offset,
                        new Vector2(1.6f, 1.6f), new Color(0.6f, 0.7f, 0.9f, 0.9f), 3);
                // 原版同款:装置上方悬浮名称牌 + 黄色▼标记
                var shortLabel = labelText.Split(' ')[0].Replace("[E]", "").Trim();
                SpriteUtil.WorldLabel("flabel_" + name, lab + offset + new Vector2(0f, artHeight + 1.1f),
                    shortLabel, 17, new Color(0.95f, 0.92f, 0.85f));
                if (artName != "painting")
                {
                    var tri = SpriteUtil.RectWorld("ftri_" + name, lab + offset + new Vector2(0f, artHeight + 0.2f),
                        new Vector2(0.5f, 0.5f), new Color(1f, 0.85f, 0.3f), 4);
                    tri.transform.rotation = Quaternion.Euler(0, 0, 45f);
                }
                _interactables.Add(new Interactable
                {
                    pos = lab + offset,
                    label = () => labelText,
                    action = action,
                    requiredStep = step,
                });
            }

            Add("cryopod", new Vector2(-10.6f, 0f), "painting", 2.4f,
                "冷冻休眠舱 [E] 回满生命/清除辐射", () =>
            {
                _state.hp = 100f;
                _state.radiation = 0f;
                _state.energy = 100f;
                _ui.ShowMessage("休眠舱:生命回满,辐射清除,能量恢复");
                _state.Save();
            }, null);

            Add("guide", new Vector2(-14f, 0f), "lab_guide", 2.4f,
                "引路人终端 [E]", TalkToGuide, () => AtLeastStep("guide_contact"));

            Add("workbench", new Vector2(20f, 0f), "device_workbench", 1.8f,
                "手工工作台 [E] 修复地质采样镐", () =>
                {
                    _laboratoryProgression.RecordPickaxeRepaired();
                    AdvanceTo("capture_hydrogen", "已修复地质采样镐 — 去氢泄漏舱截取两份氢");
                    _state.Save();
                },
                () => AtStep("repair_pickaxe"));

            Add("reaction_bench", new Vector2(7f, 0f), "device_bench", 1.8f,
                "基础反应台 [E] 防腐材料路线", RunCorrosionRoute,
                () => _state.HasLaboratoryMilestone("corrosion_gate_discovered") ||
                      _state.HasLaboratoryMilestone("corrosion_raw_materials_collected") ||
                      _state.CountItem("processed_corrosion_base") > 0 ||
                      _state.CountItem("processed_corrosion_coating") > 0);

            Add("radiation_protection", new Vector2(31f, 0f), null, 1.6f,
                "防辐射改装台 [E] 石料×3/矿石×1", () =>
                {
                    if (_environmentRuntime.UpgradeRadiationProtection())
                    {
                        _ui.ShowMessage("辐射防护提升至 " + Mathf.RoundToInt(_state.radiationProtection * 100f) + "%");
                        _state.Save();
                    }
                    else _ui.ShowMessage(_state.radiationProtection >= 0.8f
                        ? "辐射防护已达到 80% 上限" : "需要 resource_stone×3 与 resource_ore×1");
                }, () => true);

            Add("tool_workstation", new Vector2(1f, 0f), "device_workbench", 1.8f,
                "机械工具工作站 [E] 镐具制造/装备", RunMechanicalToolWorkstation, () => true);

            Add("hydrogen", new Vector2(26f, 0f), "lab_hydrogen", 2.6f,
                "氢泄漏舱 [E] 截取 1 份氢（容量 2）", () =>
            {
                if (Time.time < _hydrogenReadyAt) { _ui.ShowMessage("泄漏舱冷却中"); return; }
                if (!CaptureFiniteSource("source_hydrogen_leak_01", "element_h", 2)) return;
                _hydrogenReadyAt = Time.time + 0.6f;
                if (AtStep("capture_hydrogen") && _resources.Remaining("source_hydrogen_leak_01", "element_h", 2) == 0)
                    AdvanceTo("synthesis", "截取了 2 份氢 — 按 C 打开合成仪,选 2 个 H 合成");
            }, () => AtLeastStep("capture_hydrogen"));

            Add("synthesis", new Vector2(13f, 0f), "device_bench", 2.2f,
                "合成仪 [C 或 E]", OpenSynthesis, () => AtLeastStep("synthesis"));

            Add("demonstrate", new Vector2(-19f, 0f), "painting", 1.6f,
                "演示反应 [E] 向引路人演示 H₂", () =>
            {
                if (_state.CountItem("compound_h2") > 0)
                    AdvanceTo("airlock", "演示成功!去东侧气闸离开实验室");
                else
                    _ui.ShowMessage("需要先持有 H₂(2 个氢合成)");
            }, () => AtStep("demonstrate"));

            Add("airlock", new Vector2(36f, 0f), "lab_airlock", 3.8f,
                "地表气闸 [E] 前往死城废墟", () =>
            {
                if (AtStep("airlock"))
                {
                    var dest = RegionSpawnPos("dead_city_ruins");
                    if (!ArriveAtRegion("dead_city_ruins", dest)) return;
                    Sfx("airlock");
                    AdvanceTo("calm_shards", "已进入死城废墟 — 用正确的反应语言平息碎片(需 3 个)");
                }
                else
                {
                    if (AtStep("find_exit")) AdvanceTo("repair_pickaxe", "气闸尚未就绪 — 请先去东侧手工工作台修复采样镐");
                    else _ui.ShowMessage("气闸尚未解锁(先完成演示)");
                }
            }, () => AtLeastStep("find_exit"));
        }

        void BuildCoreTerminal(Vector2 pos)
        {
            var core = SpriteUtil.RectWorld("core_terminal", pos, new Vector2(2f, 3.2f),
                new Color(1f, 0.42f, 0.28f), 3);
            core.transform.rotation = Quaternion.Euler(0, 0, 45f);
            MakeGlow(pos, new Color(1f, 0.45f, 0.3f), 6f, 0.55f);
            SpriteUtil.WorldLabel("core_label", pos + new Vector2(0f, 2.8f), "裂核 [E]",
                15, new Color(1f, 0.6f, 0.5f));
            _interactables.Add(new Interactable
            {
                follow = () => core.transform.position,
                label = () => "裂核 [E] 做出最终抉择",
                action = () =>
                {
                    var coreProgress = new CoreProgressionService(_state);
                    if (!coreProgress.AttemptCoreTest() && !_state.coreTestPassed)
                    {
                        ShowCorePreparation();
                        return;
                    }
                    _state.Save();
                    ShowCoreChoice();
                },
            });
        }

        // ───────────────────────── 启动流程 ─────────────────────────
        Action OnBackpackSafe;

        void WireMenu()
        {
            _ui.OnMap = () => _ui.ShowRegionMap(_surfaceOrder.Concat(_undergroundOrder).Select(id => _regionById[id]).ToArray(), _state, id => {
                if (id == _state.currentRegion) return "当前位置";
                if (_surfaceOrder.Contains(id) && id != "prologue_lab" && !AtLeastStep("calm_shards")) return "先完成实验室演示并开启气闸";
                var access = _environmentRuntime.RegionAccess(id);
                return access.allowed ? "当前进入条件已满足" : EnvironmentFailureText(access.reason);
            });
            OnBackpackSafe = () =>
            {
                var owned = _state.OwnedMaterialIds();
                owned.Sort();
                var lines = owned.Select(id =>
                {
                    var m = ChemistryEngine.Material(id);
                    return (m != null ? m.symbol : id) + "  ×" + _state.CountItem(id) +
                           "   " + (m != null ? m.name : "");
                }).ToArray();
                _ui.ShowBackpack(lines);
            };
            _ui.OnNewWorld = () => { if (!SuspendForMenu()) return; GameState.BeginNewWorld(); _ui.ShowWorldSelect(); };
            _ui.OnLoadWorld = () =>
            {
                if (!SuspendForMenu()) return;
                _ui.ShowSaveSlots(GameState.ListSaveSlots(), id =>
                {
                    GameState.SelectSaveSlot(id);
                    _ui.ShowWorldSelect();
                });
            };
            _ui.OnQuit = () => Application.Quit();
            _ui.OnBackToTitle = () => { if (SuspendForMenu()) _ui.ShowTitle(); };
            _ui.OnCreateWorld = () =>
            {
                GameState.LoadOrNew();
                if (GameState.SaveWritesBlocked)
                {
                    _ui.ShowToast("存档与备份无法读取，已保留文件：" + GameState.LastSaveStatus);
                    return;
                }
                _ui.ShowLoading("正在生成死城废墟地形");
                _loading = true;
                _loadingT = 0f;
            };
            _ui.OnSynthesisKey = () =>
            {
                if (_ui.AnyPanelOpen()) _ui.CloseAllPanels();
                else if (AtLeastStep("synthesis")) OpenSynthesis();
                else _ui.ShowMessage("合成仪尚未就绪(继续主线)");
            };
            _ui.OnBackpack = () => { if (OnBackpackSafe != null) OnBackpackSafe(); };
            _ui.OnReturnLab = () =>
            {
                var lab = RegionSpawnPos("prologue_lab");
                if (!ArriveAtRegion("prologue_lab", lab)) return;
                _ui.ShowToast("返回废墟实验室");
            };
        }

        bool SuspendForMenu()
        {
            if (GameStarted && _player != null && !GameState.SaveWritesBlocked)
            {
                CapturePlayerPosition();
                if (!_state.TrySave()) return false;
            }
            GameStarted = false;
            _ui.SetAgentDialogue("", false);
            return true;
        }

        bool SaveBeforeQuit()
        {
            if (!_terrainSmoke && GameStarted && _player != null && !GameState.SaveWritesBlocked)
            {
                CapturePlayerPosition(); return _state.TrySave();
            }
            return true;
        }

        void OnSaveFailed(string reason)
        {
            if (_ui == null || Time.unscaledTime < _lastSaveWarningAt + 5f) return;
            _lastSaveWarningAt = Time.unscaledTime;
            _ui.ShowToast("存档失败，旧进度未覆盖；请勿退出：" + reason);
        }

        void OnDestroy()
        {
            GameState.SaveFailed -= OnSaveFailed;
            Application.wantsToQuit -= SaveBeforeQuit;
        }

        void InitGame()
        {
            _ui.SetupHud();
            _state = GameState.LoadOrNew();
            if (GameState.SaveWritesBlocked)
            {
                GameStarted = false; _ui.ShowTitle();
                _ui.ShowToast("存档损坏，已保留文件。请从加载列表选择其他世界。");
                return;
            }
            string loadStatus = GameState.LastSaveStatus;
            // LoadOrNew 返回新对象；所有运行时服务必须重新绑定该存档实例。
            _reactionService = new ReactionService(_state);
            _reactionService.GameplayEventPublished += OnReactionGameplayEvent;
            _deviceRuntime = new DeviceRuntimeService(_state, GameData.Devices, GameData.ProcessingRecipes);
            _deviceRuntime.GameplayEventPublished += OnDeviceGameplayEvent;
            _worldChemistry = new WorldChemistryService(_state);
            _worldChemistry.GameplayEventPublished += OnWorldChemistryEvent;
            _laboratoryProgression = new LaboratoryProgressionService(_state);
            _environmentRuntime = new RegionEnvironmentService(_state, _regions);
            _mechanicalTools = new MechanicalToolService(_state);

            _resources = new FiniteResourceService(_state, _regions);
            _drops = new WorldDropService(_state);
            BuildWorld();
            BuildPlayer();
            _combat = new CombatRuntimeService(_state, CombatCatalog.Load());
            _playerMotor = new PlayerMotorService(PlayerMotorCatalog.Load());
            RestorePlacedDeviceVisuals();

            _ambient = AmbientParticles.Attach(_camera.transform);
            BuildLumen();
            BuildAudio();

            var spawn = RegionSpawnPos("prologue_lab");
            if (_state.hasPlayerPosition)
            {
                var saved = new Vector2(_state.playerWorldX, _state.playerWorldY);
                if (!float.IsNaN(saved.x) && !float.IsNaN(saved.y) && !float.IsInfinity(saved.x) && !float.IsInfinity(saved.y) &&
                    _terrain.InBounds(Vector2Int.FloorToInt(saved)) && !_terrain.Collides(TerrainRuntimeService.PlayerBounds(saved)))
                    spawn = saved;
            }
            _player.position = new Vector3(spawn.x, spawn.y, 0f);
            _combat.EnsureEncounter(_state.currentRegion, RegionSpawnPos(_state.currentRegion)); RefreshCombatVisuals();
            _camera.transform.position = new Vector3(spawn.x, spawn.y + CameraVerticalOffset, -10f);

            _agentCatalog = AgentCatalog.Load();
            _agent = new AgentController(_state, _agentCatalog, ReadAgentEnvironment);
            _agentClockBase = _state.agent.clock_ms;
            _agentClockStart = Time.realtimeSinceStartup;
            _announcedAnalyses.Clear();
            foreach (var id in _state.identifiedSampleIds ?? new string[0]) _announcedAnalyses.Add(id);
            if (_state.questStep == 0) PublishAgent("PLAYER_AWAKENED", "player");
            RefreshWorldDrops(false);
            RefreshHud();
            _ui.ShowToast(Localization.Tr("ui.main_menu.title") + "  —  按 H 查看操作说明");
            if (loadStatus.StartsWith("recovered_")) _ui.ShowToast("主存档异常，已从 " + loadStatus.Substring(10) + " 恢复进度");
            if (_state.endingChoice == "sealed" || _state.endingChoice == "fused") ShowSavedEnding();
        }

        // ───────────────────────── 引路人与音效 ─────────────────────────
        long AgentNow()
        { return _agentClockBase + (long)(Mathf.Max(0f, Time.realtimeSinceStartup - _agentClockStart) * 1000f); }

        AgentEnvironmentFacts ReadAgentEnvironment()
        {
            RegionDef rust;
            float required = _regionById.TryGetValue("rust_jungle", out rust) ? (rust.mining_requirements ?? new ConditionThreshold[0])
                .Where(c => c.id == "corrosion").Select(c => c.minimum).FirstOrDefault() : 0f;
            float actual = _environmentRuntime.EffectiveToolResistance("corrosion");
            bool protectedFromNormalCorrosion = required > 0f && actual + 0.0001f >= required;
            return new AgentEnvironmentFacts {
                region = _state.currentRegion,
                ready = _terrain != null && _player != null && RegionOf(_player.position).id == _state.currentRegion,
                corrosion_active = _state.currentRegion == "rust_jungle" && required > 0f && !protectedFromNormalCorrosion,
                corrosion_resistance_level = protectedFromNormalCorrosion ? 1 : 0,
                resistance_available = _state.HasLaboratoryMilestone("corrosion_coating_processed") &&
                    _state.CountItem("processed_corrosion_coating") > 0 && !protectedFromNormalCorrosion,
                new_high_danger = false, // No authoritative advanced-corrosion publisher in this slice.
                has_temperature = _agentCatalog != null && (_state.currentRegion == CrystalTundraAgentBehavior.RegionId || _state.currentRegion == "ember_rift" || _state.currentRegion == "mantle_flows"),
                temperature = _agentCatalog == null ? 0f : _state.currentRegion == "mantle_flows" ? _agentCatalog.mantle_authored_temperature_floor : _state.currentRegion == "ember_rift" ? _agentCatalog.ember_authored_temperature : _agentCatalog.crystal_authored_temperature,
                has_solution_availability = _agentCatalog != null && _state.currentRegion == SaltMarshAgentBehavior.RegionId,
                solution_availability = _agentCatalog == null ? 0f : _agentCatalog.salt_authored_solution_availability,
                has_oxygen = _agentCatalog != null && _state.currentRegion == FluorescentGrottoAgentBehavior.RegionId,
                oxygen = _agentCatalog == null ? 0f : _agentCatalog.grotto_authored_oxygen,
                light = _agentCatalog == null ? 0f : _agentCatalog.grotto_authored_light,
                has_electrical_activity = _agentCatalog != null && _state.currentRegion == "magnetic_plateau",
                electrical_activity = _agentCatalog == null ? 0f : _agentCatalog.magnetic_authored_activity,
                has_radiation = _regionById.ContainsKey(_state.currentRegion),
                radiation = _regionById.ContainsKey(_state.currentRegion) ? _regionById[_state.currentRegion].radiation : 0f,
                has_pressure = _agentCatalog != null && _state.currentRegion == "core_shell",
                pressure = _agentCatalog == null ? 0f : _agentCatalog.core_authored_pressure
            };
        }

        AgentDecision PublishAgent(string type, string subject, bool uiExplains = false, bool analyzable = false, string[] outputs = null, AgentEvent evidence = null)
        {
            if (_agent == null) return null;
            var decision = _agent.Publish(type, subject, AgentNow(), uiExplains, analyzable, outputs, evidence);
            if (!decision.Silent) Debug.Log("[AgentDecision] " + type + " -> " + decision.action + "/" + decision.reaction_id);
            return decision;
        }

        void PublishAgentCollected(string item)
        { PublishAgent("MATERIAL_COLLECTED", item, analyzable: _agentCatalog != null && _agentCatalog.analyses.Any(a => a.item_id == item)); }

        void PublishAgentAnalyses()
        {
            if (_agent == null) return;
            foreach (string item in _state.identifiedSampleIds ?? new string[0])
                if (_announcedAnalyses.Add(item)) PublishAgent("MATERIAL_ANALYZED", item, evidence:
                    _agentCatalog.analyses.Any(a => a.item_id == item && (a.region_id == "rust_jungle" || a.region_id == "crystal_tundra" ||
                        (a.region_id == "salt_marsh" && SaltGuidanceActive()) ||
                        (a.region_id == "ember_rift" && (_state.currentRegion == "ember_rift" || _state.agent.shown.Contains("ember_rift_first_entry"))) ||
                        (a.region_id == "sand_desert" && (_state.currentRegion == "sand_desert" || _state.agent.shown.Contains("sand_desert_first_entry") || _state.agent.shown.Contains("sand_desert_entry_recall"))) ||
                        (a.region_id == "fluorescent_grotto" && GrottoGuidanceActive()) ||
                        (a.region_id == "geode_abyss" && GeodeGuidanceActive()) ||
                        (a.region_id == "abandoned_mine" && MineGuidanceActive()))) ?
                        new AgentEvent { behavior_profile = _agentCatalog.analyses.First(a => a.item_id == item).region_id } : null);
        }

        bool RadiationGuidanceActive()
        { return _state.currentRegion == "radiation_belt" || _state.agent.shown.Contains("radiation_belt_first_entry"); }

        bool MineGuidanceActive()
        { return _state.currentRegion == AbandonedMineAgentBehavior.RegionId || _state.agent.shown.Contains("mine_first_entry"); }

        bool GeodeGuidanceActive()
        { return _state.currentRegion == GeodeAbyssAgentBehavior.RegionId || _state.agent.geode.crystal_observed; }

        bool GrottoGuidanceActive()
        { return _state.currentRegion == FluorescentGrottoAgentBehavior.RegionId || _state.agent.grotto.oxygen_observed; }

        bool SaltGuidanceActive()
        { return _state.currentRegion == SaltMarshAgentBehavior.RegionId || (_state.agent != null && _state.agent.shown.Contains("salt_marsh_first_entry")); }

        void PublishSaltObservation(string phenomenon, string subject, string[] outputs = null)
        {
            if (SaltGuidanceActive()) PublishAgent("WORLD_REACTION_OBSERVED", subject, outputs: outputs,
                evidence: new AgentEvent { behavior_profile = SaltMarshAgentBehavior.RegionId, observed = true, phenomenon = phenomenon });
        }

        void PublishCorrosionGateFailure()
        {
            var facts = ReadAgentEnvironment();
            if (facts.region == "rust_jungle" && facts.ready && facts.corrosion_active)
                PublishAgent("CORROSION_EVENT", "corrosion_gate", evidence: new AgentEvent { corrosion_active = true });
        }

        void UpdateAgent(float seconds)
        {
            if (_agent == null) return;
            bool paused = _ui.AnyPanelOpen();
            _agent.Queue.Advance(seconds, paused);
            var current = _agent.Queue.Current;
            _ui.SetAgentDialogue(current == null ? "" : current.text, !paused);
            if (_lumenSr != null)
                _lumenSr.color = current == null || paused ? Color.white : current.visual == "ALERT" ? new Color(1f, 0.65f, 0.3f) :
                    current.visual == "SCAN" ? new Color(0.55f, 1f, 1f) : current.visual == "HAPPY" ? new Color(0.65f, 1f, 0.7f) : Color.white;
        }

        void BuildLumen()
        {
            _lumenFrames = ArtLib.Frames("lumen", 30f);   // 30px → 1 单位
            var go = new GameObject("Lumen");
            _lumen = go.transform;
            _lumenSr = go.AddComponent<SpriteRenderer>();
            _lumenSr.sprite = _lumenFrames.Length > 0 ? _lumenFrames[0] : SpriteUtil.Square();
            _lumenSr.sortingOrder = 7;
        }

        void BuildAudio()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _amb = gameObject.AddComponent<AudioSource>();
            _steps = gameObject.AddComponent<AudioSource>();
            var amb = ArtLib.Clip("lab_ambience");
            if (amb != null) { _amb.clip = amb; _amb.loop = true; _amb.volume = 0.16f; _amb.Play(); }
            var step = ArtLib.Clip("footstep_stone");
            if (step != null) { _steps.clip = step; _steps.loop = true; _steps.volume = 0.30f; }
        }

        void Sfx(string name)
        {
            var clip = ArtLib.Clip(name);
            if (clip != null && _sfx != null) _sfx.PlayOneShot(clip);
        }

        static readonly Color AccentGold = new Color(0.98f, 0.8f, 0.42f);

        // ───────────────────────── 玩家 ─────────────────────────
        void BuildPlayer()
        {
            var go = new GameObject("Player");
            _player = go.transform;
            _char = CharacterSprite.Attach(go);
        }

        void LateUpdate()
        {
            if (!GameStarted || _player == null || _camera == null) return;
            // 相机平滑跟随 + 垂直抬高(人物落画面下 1/3,房间在头顶展开)+ 像素网格取整
            var target = new Vector3(_player.position.x, _player.position.y + CameraVerticalOffset, -10f);
            var p = Vector3.Lerp(_camera.transform.position, target,
                1f - Mathf.Exp(-6f * Time.deltaTime));
            p.x = Mathf.Round(p.x * 12f) / 12f;
            p.y = Mathf.Round(p.y * 12f) / 12f;
            _camera.transform.position = p;

            // 背景画:室外半视差(0.5)跟随相机;实验室用世界锚定的内景画,隐藏此层
            if (_backdrop != null)
            {
                bool inLab = _state.currentRegion == "prologue_lab";
                _backdrop.enabled = !inLab;
                if (!inLab)
                {
                    var regionDef = _regionById.ContainsKey(_state.currentRegion)
                        ? _regionById[_state.currentRegion] : _regionById[_surfaceOrder[0]];
                    float centerX;
                    int sIdx = _surfaceOrder.IndexOf(regionDef.id);
                    if (sIdx >= 0) centerX = sIdx * SurfaceRegionWidth;
                    else centerX = (_surfaceOrder.Count - 1) * SurfaceRegionWidth * 0.5f;
                    _backdrop.transform.position = new Vector3(
                        centerX + (p.x - centerX) * 0.5f, p.y, 0f);
                    var region = _state.currentRegion;
                    if (region != _bgRegion)
                    {
                        var sp = ArtLib.Single("bg_" + region, 1672f / 64f);
                        if (sp != null) { _backdrop.sprite = sp; _bgRegion = region; }
                    }
                }
            }

            // 引路人机器人伴随玩家
            if (_lumen != null)
            {
                _lumenT += Time.deltaTime * 5f;
                if (_lumenFrames.Length > 0)
                    _lumenSr.sprite = _lumenFrames[Mathf.FloorToInt(_lumenT) % _lumenFrames.Length];
                var off = new Vector3(-1.5f + Mathf.Sin(_lumenT * 0.7f) * 0.12f,
                                      0.55f + Mathf.Sin(_lumenT * 1.1f) * 0.08f, 0f);
                _lumen.position = _player.position + off;
            }
        }

        void Update()
        {
            if (_terrainSmoke) return;
            if (!GameStarted)
            {
                if (_loading)
                {
                    _loadingT += Time.unscaledDeltaTime;
                    _ui.SetLoadingProgress(Mathf.Clamp01(_loadingT / 1.4f));
                    if (_loadingT >= 1.4f)
                    {
                        _loading = false;
                        GameStarted = true;
                        _ui.HideMenu();
                        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                    }
                }
                return;
            }
            HandlePanelKeys();
            if (submissionKeyReview)
            {
                var keys = new[] { KeyCode.M, KeyCode.M, KeyCode.Tab, KeyCode.Escape, KeyCode.Escape, KeyCode.Escape };
                if (submissionKeyStep < keys.Length && Input.GetKeyDown(keys[submissionKeyStep]))
                {
                    bool expectedOpen = submissionKeyStep == 0 || submissionKeyStep == 2 || submissionKeyStep == 4;
                    if (_ui.AnyPanelOpen() != expectedOpen) { Debug.LogError("[SYSTEM_KEYS] FAIL step " + submissionKeyStep); Application.Quit(1); return; }
                    Debug.Log("[SYSTEM_KEYS] step " + submissionKeyStep + " PASS"); submissionKeyStep++;
                    if (submissionKeyStep == keys.Length) { Debug.Log("[SYSTEM_KEYS] PASS: OS keyboard M/M/TAB/ESC/ESC/ESC toggles map, backpack and pause"); Application.Quit(0); return; }
                }
            }
            HandleMovement();
            UpdateRegionAndRadiation();
            CapturePlayerPosition();
            HandleTerrainInput();
            RefreshWorldDrops(true);
            HandleInteraction();
            AnimateVisuals();
            if (!_ui.AnyPanelOpen() && _deviceRuntime.Advance(Time.deltaTime) > 0) _state.Save();
            UpdateAgent(Time.unscaledDeltaTime);
            RefreshHud();

            _autosaveTimer += Time.deltaTime;
            if (_autosaveTimer > 30f) { _autosaveTimer = 0f; _state.Save(); }
        }

        void AnimateVisuals()
        {
            float t = Time.time;
            foreach (var f in _floaties)
                f.tr.position = new Vector3(f.tr.position.x, f.baseY + Mathf.Sin(t * 1.6f + f.phase) * f.amp, 0f);
            foreach (var p in _pulses)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 2.2f + p.phase);
                p.sr.color = new Color(p.sr.color.r, p.sr.color.g, p.sr.color.b, p.max * k);
            }
            _char.Tick(_moveX, Mathf.Abs(_vy) > 0.6f);
            foreach (var st in shardTrackers)
            {
                bool calmed = _state.HasCalmed(st.shardId);
                if (st.trustTm != null)
                    st.trustTm.text = calmed ? "已平息" : "信任 0%";
                if (st.beamSr != null) st.beamSr.enabled = !calmed;
            }
        }

        void HandlePanelKeys()
        {
            if (Input.GetKeyDown(KeyCode.H)) { _ui.ToggleHelp(); return; }
            if (Input.GetKeyDown(KeyCode.M)) { if (_ui.AnyPanelOpen()) _ui.CloseAllPanels(); else _ui.OnMap(); return; }
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (_ui.AnyPanelOpen()) _ui.CloseAllPanels();
                else OpenDeviceConsole();
                return;
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (_ui.AnyPanelOpen()) _ui.CloseAllPanels();
                else if (AtLeastStep("synthesis")) OpenSynthesis();
                else _ui.ShowMessage("合成仪尚未就绪(继续主线)");
                return;
            }
            if (Input.GetKeyDown(KeyCode.T) || Input.GetKeyDown(KeyCode.Tab))
            {
                if (_ui.AnyPanelOpen()) _ui.CloseAllPanels();
                else if (OnBackpackSafe != null) OnBackpackSafe();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { if (_ui.AnyPanelOpen()) _ui.CloseAllPanels(); else _ui.TogglePause(); return; }
            if (Input.GetKeyDown(KeyCode.E) && _ui.AnyPanelOpen()) _ui.AdvanceOrClose();
        }

        void OpenDeviceConsole(string selectedDeviceId = "")
        {
            _ui.ShowDeviceConsole(_deviceRuntime.Devices, _state,
                id => _deviceRuntime.Craft(id),
                id => _deviceRuntime.Place(id, null, _state.currentRegion,
                    _player != null ? _player.position.x : 0f,
                    _player != null ? _player.position.y - TerrainRuntimeService.PlayerFootOffset : 0f),
                id => _deviceRuntime.RecipesFor(id),
                (instance, recipe, conditions) => _deviceRuntime.Start(instance, recipe, conditions),
                instance => _deviceRuntime.Collect(instance),
                (instance, item) => _deviceRuntime.Store(instance, item),
                instance => _deviceRuntime.Withdraw(instance),
                selectedDeviceId);
        }

        void HandleMovement()
        {
            if (_ui.AnyPanelOpen()) { _moveX = 0f; _vy = 0f; return; }
            // 苏醒推进必须在移动处理之前判定(AtStep 保证只触发一次)
            ObserveAwakeningInput(Input.anyKeyDown);
            _moveX = Input.GetAxisRaw("Horizontal");
            _moveY = 0f;
            AdvancePlayerMovement(_moveX, Input.GetKeyDown(KeyCode.Space), Input.GetKey(KeyCode.Space), Time.deltaTime);

            bool movingNow = Mathf.Abs(_moveX) > 0f;
            if (movingNow)
            {
                _movedOnce = true;
            }
            else _moveX = 0f;
            // 脚步声随移动启停
            if (_steps != null && _steps.clip != null)
            {
                if (movingNow && !_steps.isPlaying) _steps.Play();
                else if (!movingNow && _steps.isPlaying) _steps.Pause();
            }
        }

        void ObserveAwakeningInput(bool pressed)
        {
            if (pressed && AtStep("awaken")) AdvanceTo("guide_contact", "完成苏醒 — 接触西侧的引路人终端");
        }

        PlayerMotionFrame AdvancePlayerMovement(float axis, bool pressed, bool held, float seconds)
        {
            bool locked = _ui.AnyPanelOpen(), floor = OnGround;
            var frame = _playerMotor.Step(new Vector2(0, _vy), seconds, axis, pressed, held, floor,
                _state.HasAbility("ability_h2_hover"), _state.energy, _hoverElapsed, locked);
            if (locked) { _vy = 0; return frame; }
            if (floor && pressed) Sfx("jump");
            _vy = frame.velocity.y; _hoverElapsed = frame.hover_elapsed; _state.energy = Mathf.Max(0, _state.energy - frame.energy_spent);
            bool blockedX, blockedY;
            _player.position = _terrain.Move(_player.position, frame.velocity * seconds, out blockedX, out blockedY);
            if (blockedY) _vy = 0;
            return frame;
        }

        System.Collections.IEnumerator RunSaveRestartProbe()
        {
            yield return null;
            try
            {
                if (GameState.SaveWritesBlocked) throw new InvalidOperationException("Restart probe save cannot load");
                if (_restartProbeMode == "write")
                {
                    if (GameState.HasSave()) throw new InvalidOperationException("Restart probe token already exists; use a fresh GUID");
                    string reason;
                    if (!_resources.Capture("source_hydrogen_leak_01", "element_h", 2, out reason))
                        throw new InvalidOperationException(reason);
                    var target = _terrain.Cells.First(p => p.Key.x >= 45 && p.Key.x <= 110 && p.Key.y == -1 &&
                        _terrain.MiningStatus(p.Key, TerrainRuntimeService.Center(p.Key) + Vector2.up * 1.7f).success).Key;
                    if (!_terrain.MineToDrop(target, TerrainRuntimeService.Center(target) + Vector2.up * 1.7f).success)
                        throw new InvalidOperationException("Restart fixture mine failed");
                    for (int i = 0; i < 7; i++)
                    {
                        _state.energy = 100 - i; CapturePlayerPosition();
                        if (!_state.TrySave() || GameState.LoadOrNew().energy != _state.energy)
                            throw new InvalidOperationException("Repeated real-directory save failed: " + GameState.LastSaveStatus);
                    }
                    if (_rustRestartProbe) RunRustSceneProbe();
                    if (_crystalRestartProbe) RunCrystalSceneProbe();
                    if (_saltRestartProbe)
                    {
                        if (Environment.GetCommandLineArgs().Contains("--u20-chain-probe"))
                        {
                            RunEmberSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u22-grotto-probe")) RunGrottoSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u23-geode-probe")) RunGeodeSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u24-mine-probe")) RunMineSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u25-magnetic-probe")) RunMagneticSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u26-radiation-probe")) RunRadiationSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u27-mantle-probe")) RunMantleSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u28-core-shell-probe")) RunCoreShellSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u29-planetary-probe")) RunPlanetarySceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u30-combat-probe")) RunCombatSceneProbe();
                            if (Environment.GetCommandLineArgs().Contains("--u32-player-probe")) RunPlayerMotorSceneProbe();
                        }
                        else RunSaltSceneProbe();
                    }
                    Debug.Log("[U15_RESTART_WRITE] PASS: seven real-directory commits; inventory/source/drop/terrain/player snapshot retained for separate process");
                    if (_agent == null || !_state.agent.shown.Contains("awakened_greeting"))
                        throw new InvalidOperationException("Agent awakening not included in restart fixture");
                    Debug.Log("[U16_AGENT_RESTART_WRITE] PASS: once-only reaction and attention state committed in actual game process");
                }
                else
                {
                    int expectedHydrogen = 1 + (Environment.GetCommandLineArgs().Contains("--u30-combat-probe") ? _state.combat.entities.Count(e => !e.alive && e.drop == "element_h") : 0);
                    float expectedEnergy = Environment.GetCommandLineArgs().Contains("--u32-player-probe") ? 94 - ChemistryEngine.EnergyCost(ChemistryEngine.Recipe("reaction_h2_synthesis")) - 18 : 94;
                    if (Mathf.Abs(_state.energy - expectedEnergy) > .001f || _state.CountItem("element_h") != expectedHydrogen || _state.worldDrops.Length != 1 ||
                        _resources.Remaining("source_hydrogen_leak_01", "element_h", 2) != 1 || _dropVisuals.Count != 1 || !_state.hasPlayerPosition)
                        throw new InvalidOperationException("Cross-process state / scene entity restore failed");
                    var drop = _state.worldDrops[0];
                    if (_agent == null || !_state.agent.shown.Contains("awakened_greeting") || _agent.Queue.Current != null)
                        throw new InvalidOperationException("Restart replayed already-shown Agent awakening");
                    var cell = Vector2Int.FloorToInt(new Vector2(drop.x, drop.y));
                    if (_terrain.Solid(cell)) throw new InvalidOperationException("Cross-process terrain respawned mined cell");
                    string reason;
                    if (!_drops.Pickup(drop.instance_id, new Vector2(drop.x, drop.y + 1), out reason) ||
                        !_state.TrySave() || GameState.LoadOrNew().worldDrops.Length != 0)
                        throw new InvalidOperationException("Cross-process pickup / save failed: " + reason);
                    Debug.Log("[U15_RESTART_READ] PASS: separate process restored inventory/source/drop/terrain/player; collected drop and saved without duplication");
                    Debug.Log("[U16_AGENT_RESTART_READ] PASS: separate process restored once-state; already-shown awakening was not replayed");
                    if (Environment.GetCommandLineArgs().Contains("--u32-player-probe"))
                    {
                        if (!_state.HasAbility("ability_h2_hover") || !OnGround || _terrain.Collides(TerrainRuntimeService.PlayerBounds(_player.position))) throw new InvalidOperationException("Player ability/landing lost across process");
                        Debug.Log("[U32_PLAYER_RESTART_READ] PASS: earned H2 hover, exact reaction/hover energy and landed collision-free position restored in separate process");
                    }
                    if (_rustRestartProbe)
                    {
                        if (!_state.agent.rust.mature || !_state.agent.rust.corrosion_understood || !_state.agent.rust.resistance_acquired ||
                            !_state.agent.rust.known_properties.Contains("element_cl") || !_state.agent.shown.Contains("rust_resistance_acquired"))
                            throw new InvalidOperationException("Rust attention / maturity / properties lost across processes");
                        Debug.Log("[U17_RUST_RESTART_READ] PASS: actual completed corrosion route and Rust attention/maturity/known facts persisted across independent game processes");
                    }
                    if (_crystalRestartProbe)
                    {
                        if (!_state.agent.crystal.mature || !_state.agent.crystal.analyzer_used || !_state.agent.crystal.carbonate_known ||
                            !_state.agent.crystal.known_facts.Contains("compound_caco3") || !_state.agent.shown.Contains("crystal_carbonate_confirmed"))
                            throw new InvalidOperationException("Crystal attention / maturity / exact facts lost across processes");
                        var replay = PublishAgent("MATERIAL_ANALYZED", "resource_carbonate_rock", evidence:
                            new AgentEvent { behavior_profile = CrystalTundraAgentBehavior.RegionId, analyzer_used = true });
                        if (replay.reason != "crystal_guidance_mature") throw new InvalidOperationException("Crystal restart replayed mature guidance");
                        Debug.Log("[U18_CRYSTAL_RESTART_READ] PASS: separate process restored exact analyzed facts, analyzer use and mature suppression");
                    }
                    if (_saltRestartProbe && !Environment.GetCommandLineArgs().Contains("--u20-chain-probe"))
                    {
                        var salt = _state.agent.salt;
                        if (!salt.mature || !salt.ph_observed || !salt.neutralization_observed || !salt.precipitation_observed ||
                            !salt.filtration_used || !salt.distillation_used || !salt.known_facts.Contains("compound_hcl"))
                            throw new InvalidOperationException("Salt chemistry observations / facts / mature state lost across processes");
                        var replay = PublishAgent("MATERIAL_ANALYZED", "resource_acidic_solution", evidence:
                            new AgentEvent { behavior_profile = SaltMarshAgentBehavior.RegionId, analyzer_used = true });
                        if (replay.reason != "salt_guidance_mature") throw new InvalidOperationException("Salt restart replayed mature guidance");
                        Debug.Log("[U19_SALT_RESTART_READ] PASS: separate process restored confirmed analysis, pH, neutralization, precipitation, both separations and mature suppression");
                    }
                    if (_saltRestartProbe && Environment.GetCommandLineArgs().Contains("--u20-chain-probe"))
                    {
                        var ember = _state.agent.ember;
                        if (!ember.mature || !ember.condenser_available || !ember.thermal_completed || !ember.condensation_used || !ember.known_facts.Contains("element_s") ||
                            !ember.combustion_observed || _state.carbonFuelBurned != 1)
                            throw new InvalidOperationException("Ember cross-process attention missing");
                        if (PublishAgent("MATERIAL_ANALYZED", "item_ember_rift_sulfur_bearing_mineral_block", evidence:
                            new AgentEvent { behavior_profile = "ember_rift" }).reason != "ember_guidance_mature") throw new InvalidOperationException("Ember mature restart replay");
                        Debug.Log("[U20_EMBER_RESTART_READ] PASS: separate game process restored sulfur facts, condenser capability, thermal/condensation progress and mature silence");
                        if (Environment.GetCommandLineArgs().Contains("--u21-link-probe"))
                        {
                            if (!_state.agent.sand.mature || !_state.agent.sand.cross_region_link || _state.agent.sand.hypothesis_pending || !_state.agent.sand.analysis_completed)
                                throw new InvalidOperationException("Sand hypothesis/link state lost across processes");
                            if (PublishAgent("REGION_ENTERED", "sand_desert", evidence: new AgentEvent { behavior_profile = "sand_desert" }).reason != "sand_guidance_mature") throw new InvalidOperationException("Sand mature replay after restart");
                            Debug.Log("[U21_SAND_LINK_RESTART_READ] PASS: independent process restored analysis, confirmed linkage, cleared hypothesis and mature suppression");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u22-grotto-probe"))
                        {
                            if (!_state.agent.grotto.mature || _state.CountStoredGas("compound_co2") != 1 || !_state.agent.grotto.known_facts.Contains("fermentable_material"))
                                throw new InvalidOperationException("Grotto mature knowledge/gas lost across processes");
                            if (PublishAgent("REGION_ENTERED", FluorescentGrottoAgentBehavior.RegionId, evidence: new AgentEvent { behavior_profile = FluorescentGrottoAgentBehavior.RegionId }).reason != "grotto_guidance_mature")
                                throw new InvalidOperationException("Grotto mature reentry after restart");
                            Debug.Log("[U22_GROTTO_RESTART_READ] PASS: separate game process restored analysis, organic/gas/storage knowledge, stored gas and mature silence");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u23-geode-probe"))
                        {
                            if (!_state.agent.geode.mature || !_state.agent.geode.condition_compared || _state.agent.geode.purity_observed ||
                                !_state.agent.geode.known_facts.Contains("natural_mineral_crystal") || _state.CountItem("resource_crystal") < 1)
                                throw new InvalidOperationException("Geode mature/concentration/analysis state lost or purity invented across restart");
                            if (PublishAgent("REGION_ENTERED", GeodeAbyssAgentBehavior.RegionId, evidence: new AgentEvent { behavior_profile = GeodeAbyssAgentBehavior.RegionId }).reason != "geode_guidance_mature")
                                throw new InvalidOperationException("Geode mature reentry after restart");
                            Debug.Log("[U23_GEODE_RESTART_READ] PASS: separate game process restored confirmed crystal facts, crystallization/concentration, mature silence and absent purity");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u24-mine-probe"))
                        {
                            if (!_state.agent.mine.mature || _state.agent.mine.displacement_ids.Length < 2 || !_state.agent.mine.precipitation_observed ||
                                !_state.HasChemistryKnowledge("no_reaction:element_cu>compound_fe2o3") || _state.CountItem("compound_znso4") < 1 ||
                                _state.CountItem("compound_barium_sulfate_reaction_slurry") != 5)
                                throw new InvalidOperationException("Mine experiment/maturity state lost across processes");
                            if (PublishAgent("REGION_ENTERED", AbandonedMineAgentBehavior.RegionId, evidence: new AgentEvent { behavior_profile = AbandonedMineAgentBehavior.RegionId }).reason != "mine_guidance_mature")
                                throw new InvalidOperationException("Mine mature reentry after restart");
                            Debug.Log("[U24_MINE_RESTART_READ] PASS: separate game process restored actual identification/metallurgy/displacement/no-reaction/precipitation/mature state");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u25-magnetic-probe"))
                        {
                            if (!_state.agent.magnetic.mature || !_state.agent.magnetic.electrolysis_observed || !_state.agent.magnetic.material_condition_linked ||
                                !_state.agent.magnetic.known_facts.Contains("redox_active") || !_state.agent.shown.Contains("magnetic_mine_metal_recall") || _state.CountItem("material_silver_catalytic") < 1)
                                throw new InvalidOperationException("Magnetic analysis/process/mature state lost across processes");
                            if (PublishAgent("REGION_ENTERED", "magnetic_plateau", evidence: new AgentEvent { behavior_profile = "magnetic_plateau" }).reason != "magnetic_guidance_mature")
                                throw new InvalidOperationException("Magnetic mature restart replay");
                            Debug.Log("[U25_MAGNETIC_RESTART_READ] PASS: separate game process restored precise facts, formal electrolysis, condition link, mine recall and mature silence");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u26-radiation-probe"))
                        {
                            if (!_state.agent.radiation.mature || !_state.agent.radiation.oxidation_observed || !_state.agent.radiation.gas_handled ||
                                !_state.agent.radiation.purification_verified || _state.CountItem("compound_purified_gas_stream") != 1 || _state.CountItem("compound_contaminated_absorbent") != 1 ||
                                WorldChemistryService.RadiationSamples.Any(id => !_state.HasChemistryKnowledge("resource_scan:" + id)))
                                throw new InvalidOperationException("Radiation state/output lost across processes");
                            if (PublishAgent("REGION_ENTERED", "radiation_belt", evidence: new AgentEvent { behavior_profile = "radiation_belt" }).reason != "radiation_guidance_mature") throw new InvalidOperationException("Radiation mature restart replay");
                            Debug.Log("[U26_RADIATION_RESTART_READ] PASS: separate process restored contamination/oxidation/gas/purification, actual absorption outputs and mature silence");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u27-mantle-probe"))
                        {
                            if (!_state.agent.mantle.mature || !_state.agent.mantle.thermal_processed || !_state.agent.mantle.crystallization_observed ||
                                !_state.HasChemistryKnowledge("purification_discovery:concentrate") || _state.CountItem("resource_high_purity_crystal") != 2 ||
                                !_state.agent.shown.Contains("mantle_geode_crystallization_recall")) throw new InvalidOperationException("Mantle industrial state/output lost across processes");
                            if (PublishAgent("REGION_ENTERED", "mantle_flows", evidence: new AgentEvent { behavior_profile = "mantle_flows" }).reason != "mantle_guidance_mature") throw new InvalidOperationException("Mantle mature restart replay");
                            Debug.Log("[U27_MANTLE_RESTART_READ] PASS: separate process restored heat/sulfur/industrial/crystallization, loaded processing, outputs/recall and mature silence");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u28-core-shell-probe"))
                        {
                            var core = _state.agent.core_shell;
                            if (!core.pressure_observed || !core.multi_condition_observed || !core.catalysis_observed || !core.device12_observed || core.multi_step_observed || core.mature ||
                                _state.CountItem("compound_nh3") < 2 || _state.CountItem("compound_reduced_carbon_liquid") +
                                    (_state.deviceInstances ?? new DeviceRuntimeContract[0]).Where(d => d.device_id == PlanetaryCoreAgentBehavior.TankId && d.stored_item_id == "compound_reduced_carbon_liquid").Sum(d => d.stored_amount) < 3 ||
                                !_state.agent.shown.Contains("core_mantle_condition_recall") || !_state.agent.shown.Contains("core_catalyst_observed"))
                                throw new InvalidOperationException("Core Shell state/output/recall lost across processes or invented multi-step maturity");
                            Debug.Log("[U28_CORE_SHELL_RESTART_READ] PASS: pressure/matrix/silver/device, actual outputs and recall restored; missing multi-step publisher remains visible, mature false");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u29-planetary-probe"))
                        {
                            var planetary = _state.agent.planetary; var tank = _state.DeviceInstance("u29_scene_tank");
                            if (!planetary.analysis_confirmed || !planetary.category_selected || !planetary.device_used || planetary.condition_selected || planetary.mature ||
                                tank == null || tank.stored_item_id != "compound_reduced_carbon_liquid" || tank.stored_amount != 1 ||
                                _state.CountItem("compound_reduced_carbon_liquid") != 2 || !_state.HasChemistryKnowledge("bulk_liquid_stored") || !_state.HasChemistryKnowledge("bulk_liquid_reused") ||
                                _state.coreTestPassed || _state.agent.shown.Contains("planetary_core_breakthrough"))
                                throw new InvalidOperationException("Planetary actual analysis/storage/output lost or missing condition/breakthrough fabricated");
                            Debug.Log("[U29_PLANETARY_RESTART_READ] PASS: separate process restored analysis/category/device, tank and liquid conservation, storage discoveries; missing condition and final victory remain unclaimed");
                        }
                        if (Environment.GetCommandLineArgs().Contains("--u30-combat-probe"))
                        {
                            var dead = _state.combat.entities.Single(e => e.id == "enemy_0001"); var alive = _state.combat.entities.Single(e => e.id == "enemy_0002");
                            int loot = _state.CountItem(dead.drop); bool killed, rewarded;
                            if (dead.alive || alive.health != alive.max_health - 12 || !alive.alive || alive.attack_cooldown <= 0 ||
                                _state.combat.next_entity_id != 3 || !_enemyVisuals.ContainsKey(alive.id) ||
                                _combat.Damage(dead.id, 12, out killed, out rewarded) || _state.CountItem(dead.drop) != loot)
                                throw new InvalidOperationException("Combat health/cooldown/visual/dead reward lost across process");
                            Debug.Log("[U30_COMBAT_RESTART_READ] PASS: separate process restored dead/live identities, health, cooldown, next id and visual; dead enemy cannot award loot again");
                        }
                    }
                }
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error); Debug.LogError("[U15_RESTART_PROBE] FAIL"); Application.Quit(1);
            }
        }

        System.Collections.IEnumerator RunTerrainSceneProbe()
        {
            yield return null;
            try
            {
                if (_terrain == null || _terrain.Collides(TerrainRuntimeService.PlayerBounds(_player.position)))
                    throw new InvalidOperationException("Scene spawn overlaps terrain");
                for (int i = 0; i < 7; i++) CollectChemicalResource("prologue_lab", "element_h");
                if (_state.CountItem("element_h") != 6 || _resources.RegionRemaining("prologue_lab", "element_h") != 0 ||
                    GameState.LoadOrNew().resourceSources.Length != 1)
                    throw new InvalidOperationException("Scene collector uses wrong state / failed to persist depletion");
                var map = GameObject.Find("SolidTiles").GetComponent<UnityEngine.Tilemaps.Tilemap>();
                Vector2Int target = new Vector2Int();
                bool found = false;
                foreach (var pair in _terrain.Cells)
                {
                    if (pair.Key.x < 45 || pair.Key.x > 110 || pair.Key.y != -1) continue;
                    var def = _terrain.Definition(pair.Value);
                    var testPlayer = new Vector2(pair.Key.x - 1.5f, 1.2f);
                    if (def.placement_material <= 0 || !_terrain.MiningStatus(pair.Key, testPlayer).success) continue;
                    target = pair.Key; found = true; break;
                }
                if (!found) throw new InvalidOperationException("No reachable scene mining target");
                _player.position = new Vector2(target.x - 1.5f, 1.2f);
                string item = _terrain.Definition(_terrain.BlockAt(target)).item_id;
                int before = _state.CountItem(item);
                if (!_terrain.MineToDrop(target, _player.position).success || map.GetTile(new Vector3Int(target.x, target.y, 0)) != null)
                    throw new InvalidOperationException("Mining did not remove visible/collision model tile");
                RefreshWorldDrops(false);
                if (_state.CountItem(item) != before || _dropVisuals.Count != 1)
                    throw new InvalidOperationException("Mining bypassed physical drop / missing visual");
                CapturePlayerPosition(); _state.Save();
                var loaded = GameState.LoadOrNew();
                if (loaded.CountItem(item) != before || loaded.worldDrops.Length != 1 || !loaded.hasPlayerPosition)
                    throw new InvalidOperationException("Scene inventory / player save mismatch");
                var restored = new TerrainRuntimeService(loaded, _regions,
                    JsonUtility.FromJson<TerrainCatalog>(Resources.Load<TextAsset>("Data/terrain_catalog").text));
                foreach (var patch in _terrain.Patches) restored.AddPatch(patch.region, patch.x, patch.floor, patch.width, patch.depth);
                restored.Protect(new Rect(-40, -8, 80, 32));
                restored.RestoreChanges();
                if (restored.Solid(target)) throw new InvalidOperationException("Mined scene tile respawned on load");
                string reason;
                var loadedDrops = new WorldDropService(loaded);
                var pickupPosition = new Vector2(target.x + 0.5f, 0.2f);
                if (!loadedDrops.Pickup(loaded.worldDrops[0].instance_id, pickupPosition, out reason) ||
                    loaded.worldDrops.Length != 0 || loaded.CountItem(item) != before + 1)
                    throw new InvalidOperationException("Restored drop pickup failed: " + reason);
                _player.position = pickupPosition;
                RefreshWorldDrops(true);
                if (_state.CountItem(item) != before + 1 || _state.worldDrops.Length != 0 || _dropVisuals.Count != 0)
                    throw new InvalidOperationException("Actual scene pickup did not conserve / remove visual");
                _player.position = new Vector2(target.x - 1.5f, 1.2f);
                if (!_terrain.Place(target, item, _player.position).success || _state.CountItem(item) != before ||
                    map.GetTile(new Vector3Int(target.x, target.y, 0)) == null)
                    throw new InvalidOperationException("Scene placement / visual / quantity mismatch");
                bool hitX, hitY;
                var landed = _terrain.Move(new Vector2(target.x + 0.5f, 6), Vector2.down * 20, out hitX, out hitY);
                if (!hitY || landed.y < 1.19f) throw new InvalidOperationException("Scene floor collision failed");
                Debug.Log("[U12_SCENE_PROBE] PASS: actual world / tilemap updates / collision / isolated disk save-load / conservation; cells=" + _terrain.Cells.Count());
                Debug.Log("[U14_SCENE_PROBE] PASS: mine creates visible persistent drop / reload unpicked drop / nearby pickup / entity removed / place conserves quantity");
                if (_saveSmoke)
                {
                    string selected = "";
                    _ui.ShowSaveSlots(new[] {
                        new SaveSlotInfo { id = "probe_a", region = "prologue_lab", stage = 0, status = "正常", loadable = true },
                        new SaveSlotInfo { id = "probe_bad", region = "不可读取", status = "checksum mismatch", loadable = false }
                    }, id => selected = id);
                    var available = GameObject.Find("Slot_probe_a").GetComponent<UnityEngine.UI.Button>();
                    var damaged = GameObject.Find("Slot_probe_bad").GetComponent<UnityEngine.UI.Button>();
                    available.onClick.Invoke();
                    if (!available.interactable || damaged.interactable || selected != "probe_a")
                        throw new InvalidOperationException("World slot UI selection / damaged slot guard failed");
                    Debug.Log("[U13_SCENE_PROBE] PASS: new save envelope disk roundtrip + actual world slot buttons / callback / damaged slot disabled");
                }
                if (Environment.GetCommandLineArgs().Contains("--u15-smoke"))
                {
                    _ui.CloseAllPanels();
                    string previousSlot = GameState.CurrentSaveSlot;
                    float previousEnergy = _state.energy;
                    _state.energy = float.NaN; // Deliberate invalid-state failure; never committed.
                    Debug.Log("[U15_EXPECTED_FAILURE] injecting invalid state to verify menu / quit / toast guards");
                    _ui.OnNewWorld();
                    _ui.OnLoadWorld();
                    _ui.OnBackToTitle();
                    _terrainSmoke = false;
                    bool canQuit = SaveBeforeQuit();
                    _terrainSmoke = true;
                    var toast = GameObject.Find("Toast").GetComponent<UnityEngine.UI.Text>();
                    if (canQuit || !GameStarted || GameState.CurrentSaveSlot != previousSlot || !toast.text.Contains("存档失败"))
                        throw new InvalidOperationException("Save failure allowed world switch / quit / missing UI warning");
                    _state.energy = previousEnergy;
                    if (!_state.TrySave()) throw new InvalidOperationException("Save did not recover after invalid-state failure");
                    Debug.Log("[U15_FAILURE_UI_PROBE] PASS: actual menu callbacks preserve world / quit guard rejects failure / visible toast / retry succeeds");
                }
                if (Environment.GetCommandLineArgs().Contains("--u16-smoke")) RunAgentSceneProbe();
                if (Environment.GetCommandLineArgs().Contains("--u17-smoke")) RunRustSceneProbe();
                if (Environment.GetCommandLineArgs().Contains("--u18-smoke")) RunCrystalSceneProbe();
                if (Environment.GetCommandLineArgs().Contains("--u19-smoke")) RunSaltSceneProbe();
                if (Environment.GetCommandLineArgs().Contains("--u20-smoke")) RunEmberSceneProbe();
                GameState.DeleteSave();
                Application.Quit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("[U12_SCENE_PROBE] FAIL");
                Application.Quit(1);
            }
        }

        void CapturePlayerPosition()
        {
            if (_player == null) return;
            if (_agent != null) _state.agent.clock_ms = Math.Max(_state.agent.clock_ms, AgentNow());
            _state.hasPlayerPosition = true;
            _state.playerWorldX = _player.position.x;
            _state.playerWorldY = _player.position.y;
        }

        static string GameplaySnapshot(GameState state)
        {
            var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state));
            copy.agent = null;
            return JsonUtility.ToJson(copy);
        }

        void DrainAgentProbeQueue()
        { for (int i = 0; _agent.Queue.Current != null && i < 100; i++) _agent.Queue.Advance(5f, false); }

        void RunRustSceneProbe()
        {
            _ui.CloseAllPanels();
            _laboratoryProgression.RecordPickaxeRepaired(); // Existing repair service fixture, not a UI playthrough.
            var stone = _interactables.First(i => i.Pos.x > 120 && i.Pos.x < 200 && i.label().StartsWith("开采 石材方块", StringComparison.Ordinal));
            _player.position = new Vector2(stone.Pos.x, 1.2f);
            UpdateRegionAndRadiation(); DrainAgentProbeQueue();
            _agentClockBase += 5000; // Deterministic test-clock offset; no real player timer or system clock changes.
            int beforeStone = _state.CountItem("resource_stone");
            stone.action();
            if (!_state.agent.shown.Contains("rust_first_corrosion_discovery") || _state.CountItem("resource_stone") != beforeStone)
                throw new InvalidOperationException("Actual blocked mining did not publish confirmed corrosion / minted stone");
            foreach (string id in new[] { "resource_rust_metal_residue", "resource_chloride_deposit" })
            {
                var resource = GameObject.Find("res_rust_jungle_" + id);
                var collection = _interactables.First(i => Vector2.Distance(i.Pos, resource.transform.position) < 0.01f && i.label().StartsWith("采集", StringComparison.Ordinal));
                _player.position = new Vector2(collection.Pos.x, 1.2f);
                collection.action();
                if (_state.CountItem(id) != 1) throw new InvalidOperationException("Scene finite Rust source failed");
            }
            var scanner = _interactables.First(i => i.label().StartsWith("环境样本扫描", StringComparison.Ordinal));
            _player.position = new Vector2(scanner.Pos.x, 1.2f);
            string before = GameplaySnapshot(_state);
            if (PublishAgent("MATERIAL_ANALYZED", "resource_rust_metal_residue").reason != "knowledge_not_discovered" || GameplaySnapshot(_state) != before)
                throw new InvalidOperationException("Rust scene inferred unscanned composition");
            scanner.action();
            if (!_state.agent.rust.corrosion_understood || !_state.agent.rust.analysis_completed ||
                _agent.Queue.Current.reaction_id != "rust_corrosion_mechanism_explained")
                throw new InvalidOperationException("Actual scan callback did not confirm corrosion mechanism");
            scanner.action();
            _agent.Queue.Advance(5f, false); UpdateAgent(0);
            var text = GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>();
            if (!_state.agent.rust.known_properties.Contains("element_cl") || !text.text.Contains("氯"))
                throw new InvalidOperationException("Actual chloride scan / dynamic source text missing");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation();
            RunCorrosionRoute(); RunCorrosionRoute();
            if (!_state.agent.shown.Contains("rust_resistance_available") || _state.CountItem("processed_corrosion_coating") != 1)
                throw new InvalidOperationException("Real coating process did not publish cross-region capability");
            RunCorrosionRoute();
            if (_state.laboratoryStage != 1 || !_state.agent.rust.mature || !_state.agent.rust.resistance_acquired ||
                _agent.Queue.Current.reaction_id != "rust_resistance_acquired")
                throw new InvalidOperationException("Real upgrade / Rust celebration / maturity failed");
            DrainAgentProbeQueue(); _player.position = new Vector2(scanner.Pos.x, 1.2f); UpdateRegionAndRadiation();
            if (_agent.Queue.Current != null || !_environmentRuntime.MiningAccess("rust_jungle").allowed)
                throw new InvalidOperationException("Mature Rust entry spam / gameplay upgrade failed");
            CapturePlayerPosition();
            if (!_state.TrySave()) throw new InvalidOperationException(GameState.LastSaveStatus);
            var restored = GameState.LoadOrNew();
            if (JsonUtility.ToJson(restored.agent.rust) != JsonUtility.ToJson(_state.agent.rust))
                throw new InvalidOperationException("Rust attention / maturity real-directory snapshot mismatch");
            Debug.Log("[U17_RUST_SCENE_PROBE] PASS: actual blocked mining / finite collection / scan callbacks / dynamic text / coating and upgrade / cross-region capability / mature silence / real-directory save-load; deterministic cooldown clock offset used");
        }

        void RunCrystalSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            const string region = CrystalTundraAgentBehavior.RegionId, sample = "resource_carbonate_rock";
            var resource = GameObject.Find("chemistry_node_resource_catalog_" + region);
            if (resource == null) throw new InvalidOperationException("Actual Crystal finite resource menu missing");
            _player.position = new Vector2(resource.transform.position.x, 1.2f);
            UpdateRegionAndRadiation();
            if (!_state.agent.shown.Contains("crystal_tundra_first_entry")) throw new InvalidOperationException("Confirmed cold entry not published");
            DrainAgentProbeQueue();
            var collection = _interactables.First(i => Vector2.Distance(i.Pos, resource.transform.position) < 0.01f && i.label().StartsWith("区域资源采样", StringComparison.Ordinal));
            int count = _state.CountItem(sample); collection.action();
            var nextPage = _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b =>
                b.GetComponentInChildren<UnityEngine.UI.Text>().text == "下一页");
            nextPage.onClick.Invoke();
            var pickSample = _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b =>
                b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("3. " + ChemistryEngine.DisplayName(sample), StringComparison.Ordinal));
            pickSample.onClick.Invoke();
            if (_state.CountItem(sample) != count + 1 || _agent.Queue.Current == null ||
                _agent.Queue.Current.reaction_id != "crystal_unknown_material_observed" || _agent.Queue.Current.text.Contains("碳酸"))
                throw new InvalidOperationException("Actual finite collection leaked / failed");
            string before = GameplaySnapshot(_state);
            if (PublishAgent("MATERIAL_ANALYZED", sample, evidence: new AgentEvent { analyzer_used = true }).reason != "knowledge_not_discovered" ||
                GameplaySnapshot(_state) != before || _state.agent.crystal.mature)
                throw new InvalidOperationException("Unconfirmed scene analysis / maturity accepted");
            DrainAgentProbeQueue();
            // Visit the workbench's formal unlock region through the existing region observer.
            _player.position = RegionSpawnPos("dead_city_ruins"); UpdateRegionAndRadiation(); DrainAgentProbeQueue();
            _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation(); _agentClockBase += 5000;
            // Manufacturing materials and Stage 1 are explicit fixture prerequisites, not a player-route claim.
            _state.laboratoryStage = Math.Max(1, _state.laboratoryStage);
            foreach (string id in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId })
            {
                if (_state.HasCraftedDevice(id)) continue;
                var def = _deviceRuntime.Devices.First(d => d.id == id);
                foreach (var q in def.crafting.inputs) _state.AddItem(q.id, q.n);
                var crafted = _deviceRuntime.Craft(id);
                if (!crafted.success) throw new InvalidOperationException("Scene manufacturing fixture failed " + id + ":" + crafted.reason);
                DrainAgentProbeQueue(); _agentClockBase += 5000; // Respect the real dialogue budget between fixture crafts.
            }
            if (!_state.agent.shown.Contains("crystal_analyzer_available")) throw new InvalidOperationException("Cross-region analyzer availability missing");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var placed = _deviceRuntime.Place(CrystalTundraAgentBehavior.DeviceId, "u18_scene_analyzer", "prologue_lab", _player.position.x + 3f, 1.2f);
            if (!placed.success || !_deviceRuntime.Start(placed.instance_id, "recipe_phase2_detect",
                new Dictionary<string,string> { { "sample_id", sample } }).success)
                throw new InvalidOperationException("Actual scene analyzer failed");
            _deviceRuntime.Advance(10f);
            if (!_deviceRuntime.Collect(placed.instance_id).success) throw new InvalidOperationException("Actual scene analyzer output failed");
            UpdateAgent(0);
            var text = GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>();
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "crystal_carbonate_confirmed" || !text.text.Contains("CaCO₃") ||
                !_state.agent.crystal.analyzer_used || !_state.agent.crystal.mature || _state.CountItem(sample) != count + 1)
                throw new InvalidOperationException("Scene precise confirmation / actual HUD / maturity / zero-consumption scan failed");
            DrainAgentProbeQueue(); _player.position = new Vector2(resource.transform.position.x, 1.2f); UpdateRegionAndRadiation();
            if (_agent.Queue.Current != null) throw new InvalidOperationException("Mature Crystal entry replayed");
            CapturePlayerPosition();
            if (!_state.TrySave()) throw new InvalidOperationException(GameState.LastSaveStatus);
            var restored = GameState.LoadOrNew();
            if (JsonUtility.ToJson(restored.agent.crystal) != JsonUtility.ToJson(_state.agent.crystal))
                throw new InvalidOperationException("Crystal real-directory snapshot mismatch");
            Debug.Log("[U18_CRYSTAL_SCENE_PROBE] PASS: authored cold entry / finite collection callback / knowledge guard / real analyzer craft-place-process / cross-region evidence / exact HUD text / mature silence / real-directory save-load; raw manufacturing and Stage1 fixtures, test-clock offsets explicitly used; temperature/crystallization live publishers not tested");
        }

        void RunEmberSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            foreach (string visit in new[] { "dead_city_ruins", "crystal_tundra", "ember_rift" })
            { _player.position = RegionSpawnPos(visit); UpdateRegionAndRadiation(); DrainAgentProbeQueue(); _agentClockBase += 5000; }
            if (!_state.agent.ember.temperature_observed) throw new InvalidOperationException("Actual Ember temperature entry missing");
            string sample = _agentCatalog.ForRegion("ember_rift").sample_ids[1];
            _state.AddItem("material_carbon"); // Explicit fixture, consumed through the actual callback.
            var fuelNode = GameObject.Find("chemistry_node_carbon_fuel");
            _player.position = fuelNode.transform.position;
            _interactables.First(i => Vector2.Distance(i.Pos, fuelNode.transform.position) < 0.01f).action();
            if (_state.carbonFuelBurned != 1 || !_state.agent.ember.combustion_observed) throw new InvalidOperationException("Live fuel observation failed");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            // Explicit manufacturing/feedstock/Stage fixtures, not a complete player acquisition route.
            _state.AddItem(sample); PublishAgentCollected(sample);
            if (_agent.Queue.Current == null || _agent.Queue.Current.text.Contains("硫")) throw new InvalidOperationException("Unknown Ember sample leaked sulfur");
            DrainAgentProbeQueue(); _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation(); _state.laboratoryStage = 2;
            var recipes = new[] { "recipe_phase2_detect", "recipe_phase3a_thermal_carbonate", "recipe_phase3a_condense_vapor" };
            var deviceIds = new[] { "device_chemical_workbench" }.Concat(recipes.Select(id => GameData.ProcessingRecipes.recipes.First(r => r.id == id).device_id)).Distinct();
            foreach (string id in deviceIds)
            {
                var def = _deviceRuntime.Devices.First(d => d.id == id);
                if (!string.IsNullOrEmpty(def.crafting.unlock_region)) _state.AddVisitedRegion(def.crafting.unlock_region); // Unlock prerequisite fixture.
                foreach (var q in def.crafting.inputs) _state.AddItem(q.id, q.n);
                _agentClockBase += 5000; DrainAgentProbeQueue();
                if (!_deviceRuntime.Craft(id).success || !_deviceRuntime.Place(id, "u20_" + id).success) throw new InvalidOperationException("Ember manufacturing fixture failed " + id);
            }
            if (!_state.agent.ember.condenser_available) throw new InvalidOperationException("Live condenser bridge missing");
            _state.AddItem("resource_carbonate_rock"); _state.ChangeVapor("compound_ethanol", 1);
            foreach (string id in recipes)
            {
                DrainAgentProbeQueue(); _agentClockBase += 5000;
                var recipe = GameData.ProcessingRecipes.recipes.First(r => r.id == id);
                var conditions = (recipe.required_conditions ?? new ConditionValue[0]).ToDictionary(c => c.id, c => c.value);
                if (id == "recipe_phase2_detect") conditions["sample_id"] = sample;
                if (!_deviceRuntime.Start("u20_" + recipe.device_id, id, conditions).success) throw new InvalidOperationException("Ember actual process failed " + id);
                if (id == "recipe_phase3a_condense_vapor" && _state.agent.ember.mature) throw new InvalidOperationException("Premature mature state");
                _deviceRuntime.Advance(10);
                if (!_deviceRuntime.Collect("u20_" + recipe.device_id).success) throw new InvalidOperationException("Ember output collection failed");
                UpdateAgent(0);
                if (_agent.Queue.Current == null) throw new InvalidOperationException("Live Ember dialogue missing");
                if (id == "recipe_phase2_detect" && !GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text.Contains("硫"))
                    throw new InvalidOperationException("Actual sulfur HUD missing");
            }
            if (!_state.agent.ember.mature || !_state.agent.shown.Contains("ember_condensation_completed")) throw new InvalidOperationException("Actual Ember mature chain incomplete");
            DrainAgentProbeQueue(); _player.position = RegionSpawnPos("ember_rift"); UpdateRegionAndRadiation();
            if (_agent.Queue.Current != null) throw new InvalidOperationException("Mature Ember replay");
            CapturePlayerPosition(); if (!_state.TrySave()) throw new InvalidOperationException(GameState.LastSaveStatus);
            if (JsonUtility.ToJson(GameState.LoadOrNew().agent.ember) != JsonUtility.ToJson(_state.agent.ember)) throw new InvalidOperationException("Ember disk snapshot mismatch");
            Debug.Log("[U20_EMBER_SCENE_PROBE] PASS: live entry/carbon fuel callback/unknown guard/capability/analyzer HUD/thermal and condensation collection/mature silence/real-directory save-load; manufacturing, feedstock, Stage, positioning and clock fixtures used; formal-failure live publisher remains missing");
            if (Environment.GetCommandLineArgs().Contains("--u21-entry-probe") || Environment.GetCommandLineArgs().Contains("--u21-link-probe"))
            {
                _player.position = RegionSpawnPos("sand_desert"); UpdateRegionAndRadiation(); UpdateAgent(0);
                string expected = _agentCatalog.ForRegion("sand_desert").Reaction("entry_recall");
                if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != expected ||
                    GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agentCatalog.responses.First(r => r.reaction_id == expected).text)
                    throw new InvalidOperationException("Sand actual cross-region entry HUD missing");
                CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.shown.Contains(expected))
                    throw new InvalidOperationException("Sand entry real-directory snapshot missing");
                Debug.Log("[U21_SAND_ENTRY_PROBE] PASS: actual region entry after committed Ember thermal route / source recall HUD / real-directory snapshot; position and previous route fixtures remain explicit");
                if (Environment.GetCommandLineArgs().Contains("--u21-link-probe"))
                {
                    DrainAgentProbeQueue(); _agentClockBase += 5000;
                    // Explicit old-sample fixture, confirmed through the actual existing analyzer.
                    _state.AddItem("resource_carbonate_rock");
                    string analyzerId = "u20_" + CrystalTundraAgentBehavior.DeviceId;
                    if (!_deviceRuntime.Start(analyzerId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", "resource_carbonate_rock" } }).success)
                        throw new InvalidOperationException("Old carbonate actual analysis failed");
                    _deviceRuntime.Advance(10); if (!_deviceRuntime.Collect(analyzerId).success) throw new InvalidOperationException("Old analyzer collection failed");
                    DrainAgentProbeQueue(); _agentClockBase += 5000;
                    const string sandSample = "item_wind_desert_carbonate_rock_block";
                    _state.AddItem(sandSample); PublishAgentCollected(sandSample); UpdateAgent(0); // Explicit terrain acquisition fixture.
                    if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "sand_cross_region_hypothesis" ||
                        _state.agent.sand.cross_region_link || _state.HasIdentifiedSample(sandSample)) throw new InvalidOperationException("Sand hypothesis became fact prematurely");
                    DrainAgentProbeQueue(); _agentClockBase += 5000;
                    var node = GameObject.Find("chemistry_node_sand_analysis");
                    _player.position = node.transform.position;
                    _interactables.First(i => Vector2.Distance(i.Pos, node.transform.position) < 0.01f).action(); UpdateAgent(0);
                    if (!_state.agent.sand.mature || !_state.agent.sand.cross_region_link || _state.agent.sand.hypothesis_pending || _state.CountItem(sandSample) != 0 ||
                        _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "sand_crystal_recall_confirmed" ||
                        !GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text.Contains("碳酸盐相关性质"))
                        throw new InvalidOperationException("Actual Sand analysis callback/HUD/link failed");
                    CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.sand.mature) throw new InvalidOperationException("Sand mature disk snapshot missing");
                    Debug.Log("[U21_SAND_LINK_PROBE] PASS: real analyzer old knowledge / unconfirmed hypothesis / actual destructive terrain analysis callback / confirmed recall HUD / mature real-directory snapshot; acquisition and manufacturing fixtures explicit");
                }
            }
        }

        void RunGrottoSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos(FluorescentGrottoAgentBehavior.RegionId); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (!_state.agent.grotto.oxygen_observed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "grotto_ember_oxygen_recall")
                throw new InvalidOperationException("Grotto environment/Ember recall not connected");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string sample = WorldChemistryService.GrottoBlocks[3]; _state.AddItem(sample); PublishAgentCollected(sample); // Explicit acquisition fixture.
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "grotto_unknown_material_observed") throw new InvalidOperationException("Grotto unknown presentation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var analysis = GameObject.Find("chemistry_node_grotto_analysis");
            _player.position = analysis.transform.position;
            _interactables.First(i => Vector2.Distance(i.Pos, analysis.transform.position) < 0.01f).action(); UpdateAgent(0);
            if (_state.CountItem(sample) != 0 || !_state.agent.grotto.analysis_completed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "grotto_fermentable_confirmed")
                throw new InvalidOperationException("Grotto live analysis callback/HUD");
            if (GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Grotto analysis dialogue did not reach actual HUD");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _state.AddItem("organic_glucose"); // Explicit feedstock fixture; actual callback consumes it.
            var fermenter = GameObject.Find("chemistry_node_organic_fermenter"); _player.position = fermenter.transform.position;
            _interactables.First(i => Vector2.Distance(i.Pos, fermenter.transform.position) < 0.01f).action(); UpdateAgent(0);
            if (!_state.agent.grotto.organic_completed || !_state.agent.grotto.gas_observed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "grotto_gas_produced")
                throw new InvalidOperationException("Grotto live fermentation observation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var tank = _deviceRuntime.Devices.First(d => d.id == FluorescentGrottoAgentBehavior.TankId);
            foreach (var q in tank.crafting.inputs) _state.AddItem(q.id, q.n); // Manufacturing fixture, real craft/place/start.
            if (!string.IsNullOrEmpty(tank.crafting.unlock_region)) _state.AddVisitedRegion(tank.crafting.unlock_region);
            if (!_deviceRuntime.Craft(tank.id).success || !_deviceRuntime.Place(tank.id, "u22_scene_tank").success ||
                !_deviceRuntime.Start("u22_scene_tank", "recipe_phase3a_store_gas", null).success || _state.CountStoredGas("compound_co2") != 1 || !_state.agent.grotto.mature)
                throw new InvalidOperationException("Grotto actual tank/store/maturity");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.grotto.mature) throw new InvalidOperationException("Grotto mature disk snapshot");
            Debug.Log("[U22_GROTTO_SCENE_PROBE] PASS: actual region/recall, terrain analysis and fermentation callbacks, HUD queue, real tank craft/place/store, mature snapshot; acquisition/Stage/time/position fixtures explicit");
        }

        void RunGeodeSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos(GeodeAbyssAgentBehavior.RegionId); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (!_state.agent.geode.crystal_observed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "geode_abyss_first_entry")
                throw new InvalidOperationException("Geode live region observation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string sample = WorldChemistryService.GeodeBlocks[1]; _state.AddItem(sample); PublishAgentCollected(sample); // Explicit acquisition fixture.
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "geode_unknown_material_observed") throw new InvalidOperationException("Geode unknown sample guard");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string, string> { { "sample_id", sample } }).success)
                throw new InvalidOperationException("Geode actual detector start");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.geode.analysis_completed || _state.CountItem(sample) != 1 || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "geode_crystal_confirmed" ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Geode live detector/HUD facts");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            int crystal = _state.CountItem("resource_crystal"), mother = _state.CountItem("compound_mother_liquor");
            _state.AddItem("resource_mineral_solution"); // Explicit solution acquisition fixture.
            var node = GameObject.Find("chemistry_node_natural_crystallization"); _player.position = node.transform.position;
            _interactables.First(i => Vector2.Distance(i.Pos, node.transform.position) < 0.01f).action(); UpdateAgent(0);
            if (_state.CountItem("resource_crystal") != crystal + 1 || _state.CountItem("compound_mother_liquor") != mother + 1 || !_state.agent.geode.mature ||
                !_state.agent.geode.condition_compared || _state.agent.geode.purity_observed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "geode_natural_crystallization_observed")
                throw new InvalidOperationException("Geode live crystallization/concentration/maturity or invented purity");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.geode.mature) throw new InvalidOperationException("Geode mature disk snapshot");
            Debug.Log("[U23_GEODE_SCENE_PROBE] PASS: actual underground entry, real detector and HUD, natural crystallization callback/conservation/concentration/maturity with no purity; acquisition/Stage/position/time fixtures explicit");
        }

        void ClickMineProbeOption(string suffix, string fieldName = "_dlgOptions")
        {
            var field = typeof(GameUI).GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var root = (Transform)field.GetValue(_ui);
            // Search from the newest children: old options may be pending end-of-frame Destroy.
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var button = root.GetChild(i).GetComponent<UnityEngine.UI.Button>();
                var text = root.GetChild(i).GetComponentInChildren<UnityEngine.UI.Text>();
                if (button != null && text != null && (text.text.EndsWith(suffix, StringComparison.Ordinal) || (suffix.StartsWith("领取 ", StringComparison.Ordinal) && button.name == "Collect_" + suffix.Substring(3)))) { button.onClick.Invoke(); return; }
            }
            throw new InvalidOperationException("Actual mine dialogue button missing: " + suffix);
        }
        void RunMineSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos(AbandonedMineAgentBehavior.RegionId); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "mine_first_entry") throw new InvalidOperationException("Mine actual entry");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string block = WorldChemistryService.MineBlocks[2]; _state.AddItem(block); PublishAgentCollected(block); // Explicit raw acquisition fixture.
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "mine_unknown_ore_observed") throw new InvalidOperationException("Mine unknown guard");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var analysis = GameObject.Find("chemistry_node_mine_analysis"); _player.position = analysis.transform.position;
            _interactables.First(i => Vector2.Distance(i.Pos, analysis.transform.position) < 0.01f).action(); UpdateAgent(0);
            if (_state.CountItem(block) != 0 || !_state.agent.mine.analysis_completed || _agent.Queue.Current == null ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Mine terrain analysis callback/HUD");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string barium = WorldChemistryService.MineBlocks[4]; _state.AddItem(barium);
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string, string> { { "sample_id", barium } }).success)
                throw new InvalidOperationException("Mine live detector start");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.mine.known_facts.Contains("element_ba") || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "mine_reactor_need")
                throw new InvalidOperationException("Mine live precise analysis/formal reactor mapping");
            Action<string, string, string> experiment = (operation, first, second) => {
                _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
                var node = GameObject.Find("chemistry_node_mine_experiments"); _player.position = node.transform.position;
                if (_interactables.Count(i => Vector2.Distance(i.Pos, node.transform.position) < 0.01f) != 1) throw new InvalidOperationException("Mine experiment interaction overlaps another callback");
                _interactables.First(i => Vector2.Distance(i.Pos, node.transform.position) < 0.01f).action();
                ClickMineProbeOption(operation); ClickMineProbeOption(ChemistryEngine.DisplayName(first));
                if (second != null) ClickMineProbeOption(ChemistryEngine.DisplayName(second));
                UpdateAgent(0);
            };
            // Feedstocks are explicit fixtures; all selection buttons invoke real inventory operations.
            _state.AddItem("element_zn"); _state.AddItem("compound_cuso4");
            experiment("金属置换", "element_zn", "compound_cuso4");
            if (_state.CountItem("compound_znso4") != 1 || !_state.agent.mine.displacement_observed) throw new InvalidOperationException("Mine actual displacement UI");
            _state.AddItem("compound_fe2o3"); int copper = _state.CountItem("element_cu");
            experiment("金属置换", "element_cu", "compound_fe2o3");
            if (_state.CountItem("element_cu") != copper || !_state.HasChemistryKnowledge("no_reaction:element_cu>compound_fe2o3") || _state.agent.mine.displacement_ids.Length != 2)
                throw new InvalidOperationException("Mine actual no-reaction UI/conservation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var reactor = _deviceRuntime.Devices.First(d => d.id == AbandonedMineAgentBehavior.Reactor);
            foreach (var q in reactor.crafting.inputs) _state.AddItem(q.id, q.n); // Explicit manufacturing fixture, real craft/place/process.
            if (!_deviceRuntime.Craft(reactor.id).success || !_deviceRuntime.Place(reactor.id, "u24_scene_reactor").success) throw new InvalidOperationException("Mine real reactor manufacture");
            _state.AddItem("compound_barium_process_solution", 5); _state.AddItem("compound_sulfate_process_solution", 5);
            if (!_deviceRuntime.Start("u24_scene_reactor", "barium_sulfate_batch_precipitation", null).success || _state.agent.mine.precipitation_observed)
                throw new InvalidOperationException("Mine batch start/premature observation");
            _deviceRuntime.Advance(10); if (_state.agent.mine.precipitation_observed) throw new InvalidOperationException("Mine buffered batch treated as collected");
            if (!_deviceRuntime.Collect("u24_scene_reactor").success || !_state.agent.mine.precipitation_observed || _state.CountItem("compound_barium_sulfate_reaction_slurry") != 5)
                throw new InvalidOperationException("Mine live industrial batch delivery");
            _state.AddItem("element_ba"); _state.AddItem("ion_so4"); experiment("沉淀实验", "element_ba", "ion_so4");
            if (!_state.agent.mine.precipitation_observed || _state.CountItem("compound_baso4") != 1) throw new InvalidOperationException("Mine actual precipitation UI");
            _state.AddItem("resource_iron_bearing_ore"); experiment("矿石识别", "resource_iron_bearing_ore", null);
            if (!_state.HasChemistryKnowledge("ore_identified:resource_iron_bearing_ore") || _state.CountItem("resource_iron_bearing_ore") != 1) throw new InvalidOperationException("Mine non-destructive identification UI");
            experiment("矿石预处理", "resource_iron_bearing_ore", null);
            _state.AddItem("element_c"); experiment("氧化物冶炼", "element_fe", null);
            if (!_state.agent.mine.mature || !_state.HasChemistryKnowledge("metal_recovery:element_fe") || _state.CountItem("element_fe") < 1)
                throw new InvalidOperationException("Mine actual metallurgy/maturity UI");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.mine.mature) throw new InvalidOperationException("Mine mature disk snapshot");
            Debug.Log("[U24_MINE_SCENE_PROBE] PASS: actual region, terrain analysis/HUD, actual experiment selection buttons for identification/pretreatment/smelting/displacement/no-reaction/precipitation, conservation/maturity/save; acquisition/Stage/time/position fixtures explicit");
        }

        void RunMagneticSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("magnetic_plateau"); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (!_state.agent.magnetic.electricity_observed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "magnetic_environment_electricity")
                throw new InvalidOperationException("Magnetic actual electrical entry");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string sample = _agentCatalog.analyses.First(r => r.region_id == "magnetic_plateau" && r.facts.Contains("redox_active")).item_id;
            _state.AddItem(sample); PublishAgentCollected(sample); // Explicit acquisition fixture.
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "magnetic_unknown_material") throw new InvalidOperationException("Magnetic unknown sample presentation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample } }).success)
                throw new InvalidOperationException("Magnetic actual detector start");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.magnetic.analysis_completed || _state.agent.magnetic.electrolysis_observed || _state.agent.magnetic.mature || _state.CountItem(sample) != 1 ||
                _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "magnetic_redox_property" ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Magnetic actual analysis HUD/property-process isolation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var device = _deviceRuntime.Devices.First(d => d.id == "device_industrial_electrolysis_cell");
            foreach (var q in device.crafting.inputs) _state.AddItem(q.id, q.n); // Explicit manufacturing fixture, real craft/place/process.
            if (!_deviceRuntime.Craft(device.id).success || !_deviceRuntime.Place(device.id, "u25_scene_electrolyzer").success)
                throw new InvalidOperationException("Magnetic real device manufacture");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            int hydrogen = _state.CountItem("compound_h2"), oxygen = _state.CountItem("compound_o2"), water = _state.CountItem("compound_h2o");
            _state.AddItem("compound_h2o");
            if (!_deviceRuntime.Start("u25_scene_electrolyzer", "recipe_phase3b_electrolyze", null).success || _state.agent.magnetic.electrolysis_observed)
                throw new InvalidOperationException("Magnetic process start/premature observation");
            _deviceRuntime.Advance(10); if (_state.agent.magnetic.mature) throw new InvalidOperationException("Magnetic buffered output counted as observed");
            if (!_deviceRuntime.Collect("u25_scene_electrolyzer").success) throw new InvalidOperationException("Magnetic output collection"); UpdateAgent(0);
            if (!_state.agent.magnetic.mature || _state.CountItem("compound_h2") != hydrogen + 1 || _state.CountItem("compound_o2") != oxygen + 1 || _state.CountItem("compound_h2o") != water ||
                _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "magnetic_mine_metal_recall" ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Magnetic actual delivery/recall HUD/maturity");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _state.laboratoryStage = 3; _state.AddItem("compound_preprocessed_silver_bearing_material"); // Explicit late-stage/feedstock fixture.
            if (_deviceRuntime.Start("u25_scene_electrolyzer", "recipe_phase4b_silver_catalytic_refine", null).success) throw new InvalidOperationException("Magnetic silver current gate bypassed");
            int silver = _state.CountItem("material_silver_catalytic");
            if (!_deviceRuntime.Start("u25_scene_electrolyzer", "recipe_phase4b_silver_catalytic_refine", new Dictionary<string,string> { { "controlled_current", "True" } }).success) throw new InvalidOperationException("Magnetic silver actual refinement start");
            _deviceRuntime.Advance(10); if (!_deviceRuntime.Collect("u25_scene_electrolyzer").success || _state.CountItem("material_silver_catalytic") != silver + 1 || _agent.Queue.Current != null)
                throw new InvalidOperationException("Magnetic silver delivery or mature replay");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.magnetic.mature) throw new InvalidOperationException("Magnetic mature disk snapshot");
            Debug.Log("[U25_MAGNETIC_SCENE_PROBE] PASS: actual region/electrical snapshot, real analysis and HUD/property guard, actual electrolyzer manufacture and collection, conservation/mine recall/mature save; acquisition/Stage/position/time fixtures explicit");
        }

        void RunRadiationSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("radiation_belt"); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (_agent.Queue.Current == null || _agent.Queue.Current.action != "WARN") throw new InvalidOperationException("Radiation live hazard entry");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var scan = GameObject.Find("chemistry_node_radiation_scan"); _player.position = scan.transform.position;
            foreach (string raw in WorldChemistryService.RadiationSamples)
            {
                _state.AddItem(raw); // Explicit acquired-resource fixture; actual node scan is non-destructive.
                _interactables.Single(i => Vector2.Distance(i.Pos, scan.transform.position) < 0.01f).action();
                if (_state.CountItem(raw) != 1 || !_state.HasChemistryKnowledge("resource_scan:" + raw) || _state.agent.radiation.contamination_analyzed)
                    throw new InvalidOperationException("Radiation actual raw scan/source identity boundary");
            }
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string sample = _agentCatalog.analyses.First(r => r.region_id == "radiation_belt" && r.facts.Contains("irradiated")).item_id;
            _state.AddItem(sample); // Explicit acquisition fixture, real detector.
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample } }).success) throw new InvalidOperationException("Radiation live detector");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.radiation.contamination_analyzed || !_state.agent.radiation.oxidation_observed || _state.agent.radiation.mature || _agent.Queue.Current == null ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text) throw new InvalidOperationException("Radiation analysis/property HUD");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _state.laboratoryStage = 3; // Explicit progression/manufacturing fixtures.
            foreach (string id in new[] { "device_high_pressure_gas_tank", "device_industrial_absorption_tower" })
            {
                var def = _deviceRuntime.Devices.First(d => d.id == id); _state.AddVisitedRegion(def.crafting.unlock_region);
                foreach (var q in def.crafting.inputs) _state.AddItem(q.id, q.n);
                if (!_deviceRuntime.Craft(id).success || !_deviceRuntime.Place(id, "u26_" + id).success) throw new InvalidOperationException("Radiation real device manufacture");
            }
            _state.AddItem("item_radiation_belt_gas_bearing_deposit_block");
            if (!_worldChemistry.ProcessGasBearingDeposit().success || !_state.agent.radiation.gas_handled || _state.agent.radiation.mature) throw new InvalidOperationException("Radiation real gas observation");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            if (!_deviceRuntime.Start("u26_device_high_pressure_gas_tank", "recipe_phase3a_store_gas", new Dictionary<string,string> { { "gas_id", "compound_contaminated_gas_stream" } }).success) throw new InvalidOperationException("Radiation actual gas storage");
            if (_deviceRuntime.Start("u26_device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null).success || _state.CountStoredGas("compound_contaminated_gas_stream") != 1) throw new InvalidOperationException("Radiation missing absorbent failure");
            _state.AddItem("resource_alkaline_solution");
            if (!_deviceRuntime.Start("u26_device_industrial_absorption_tower", "recipe_phase4a_absorb_gas", null).success || _state.agent.radiation.purification_verified) throw new InvalidOperationException("Radiation premature purification");
            _deviceRuntime.Advance(10); if (_state.agent.radiation.mature) throw new InvalidOperationException("Radiation buffered output counted");
            if (!_deviceRuntime.Collect("u26_device_industrial_absorption_tower").success) throw new InvalidOperationException("Radiation actual absorption collection"); UpdateAgent(0);
            if (!_state.agent.radiation.mature || _state.CountStoredGas("compound_contaminated_gas_stream") != 0 || _state.CountItem("compound_purified_gas_stream") != 1 ||
                _state.CountItem("compound_contaminated_absorbent") != 1 || _state.CountItem("resource_alkaline_solution") != 0) throw new InvalidOperationException("Radiation actual absorption conservation/maturity");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.radiation.mature) throw new InvalidOperationException("Radiation disk snapshot");
            Debug.Log("[U26_RADIATION_SCENE_PROBE] PASS: live hazard/analysis/property HUD, actual gas manufacture/storage/absorption failure and delivery/conservation, mature save; acquisition/Stage/visit/position/time fixtures explicit");
        }

        void RunMantleSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("mantle_flows"); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (!_state.agent.mantle.heat_observed || _agent.Queue.Current == null || _agent.Queue.Current.action != "WARN") throw new InvalidOperationException("Mantle actual heat entry");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            string sample = _agentCatalog.analyses.First(r => r.region_id == "mantle_flows" && r.facts.Contains("sulfur_related")).item_id; _state.AddItem(sample); // Acquisition fixture.
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample } }).success) throw new InvalidOperationException("Mantle actual detector");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.mantle.sulfur_observed || _state.agent.mantle.thermal_processed || _agent.Queue.Current == null ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text) throw new InvalidOperationException("Mantle sulfur HUD/process distinction");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _state.AddItem("element_s"); var heat = GameObject.Find("chemistry_node_mantle_heat_process"); _player.position = heat.transform.position;
            _interactables.Single(i => Vector2.Distance(i.Pos, heat.transform.position) < 0.01f).action(); UpdateAgent(0);
            if (!_state.agent.mantle.thermal_processed || _state.CountGeneratedGas("compound_so2") != 1 || _state.agent.mantle.mature) throw new InvalidOperationException("Mantle actual extreme heat callback");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            _state.laboratoryStage = 3; _state.AddItem("compound_loaded_absorption_solution"); // Explicit progression/feedstock fixtures.
            var loaded = GameObject.Find("chemistry_node_mantle_loaded_solution"); _player.position = loaded.transform.position;
            _interactables.Single(i => Vector2.Distance(i.Pos, loaded.transform.position) < 0.01f).action(); ClickMineProbeOption("浓缩"); UpdateAgent(0);
            if (!_state.HasChemistryKnowledge("purification_discovery:concentrate") || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "mantle_geode_crystallization_recall") throw new InvalidOperationException("Mantle actual loaded button/geode recall");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var device = _deviceRuntime.Devices.First(d => d.id == "device_industrial_crystallizer"); foreach (var q in device.crafting.inputs) _state.AddItem(q.id, q.n);
            if (!_deviceRuntime.Craft(device.id).success || !_deviceRuntime.Place(device.id, "u27_scene_crystallizer", "mantle_flows", _player.position.x + 3, _player.position.y).success) throw new InvalidOperationException("Mantle actual crystallizer manufacture");
            int mother = _state.CountItem("compound_mother_liquor");
            foreach (string solution in new[] { "compound_concentrated_salt_solution", "compound_loaded_absorption_solution" })
            {
                if (solution == "compound_loaded_absorption_solution") _state.AddItem(solution);
                _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000; OpenDeviceConsole(device.id);
                ClickMineProbeOption("结晶 " + ChemistryEngine.DisplayName(solution), "_deviceActions");
                if (_state.DeviceInstance("u27_scene_crystallizer").state != DeviceProcessingState.Processing) throw new InvalidOperationException("Mantle actual crystallizer input button");
                _deviceRuntime.Advance(10);
                _ui.CloseAllPanels(); OpenDeviceConsole(device.id); // Reopen the real console after processing to show current collection actions.
                ClickMineProbeOption("领取 u27_scene_crystallizer", "_deviceActions"); _ui.CloseAllPanels(); UpdateAgent(0);
            }
            if (!_state.agent.mantle.mature || _state.CountItem("resource_high_purity_crystal") != 2 || _state.CountItem("compound_mother_liquor") != mother + 2) throw new InvalidOperationException("Mantle actual dual input delivery/mature");
            CapturePlayerPosition(); if (!_state.TrySave() || !GameState.LoadOrNew().agent.mantle.mature) throw new InvalidOperationException("Mantle disk snapshot");
            Debug.Log("[U27_MANTLE_SCENE_PROBE] PASS: actual heat/detector HUD, extreme heat node, loaded-solution button/geode recall, real crystallizer manufacture and dual input/collection buttons, conservation/mature save; raw material/Stage/position/time fixtures explicit");
        }

        void RunCoreShellSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("core_shell"); UpdateRegionAndRadiation(); UpdateAgent(0);
            if (_agent.Queue.Current == null || _agent.Queue.Current.action != "WARN" || _state.agent.core_shell.pressure_observed)
                throw new InvalidOperationException("Core Shell environmental entry invented process pressure");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            const string sample = "item_core_shell_high_strength_mineral_block";
            _state.AddItem(sample); // Acquisition fixture, not a natural traversal claim.
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample } }).success)
                throw new InvalidOperationException("Core Shell actual detector");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "core_material_analysis" || _state.agent.core_shell.multi_condition_observed)
                throw new InvalidOperationException("Core Shell analysis HUD/process separation");
            var device = _deviceRuntime.Devices.First(d => d.id == "device_high_pressure_catalytic_reactor");
            foreach (var q in device.crafting.inputs) _state.AddItem(q.id, q.n); // Manufacturing fixture.
            if (!_deviceRuntime.Craft(device.id).success || !_deviceRuntime.Place(device.id, "u28_scene_reactor", "core_shell", _player.position.x + 3, _player.position.y).success)
                throw new InvalidOperationException("Core Shell actual reactor manufacture");
            int iron = _state.CountItem("material_iron"), silver = _state.CountItem("material_silver_catalytic");
            _state.AddItem("material_iron"); _state.AddItem("material_silver_catalytic");
            for (int i = 0; i < 4; i++)
            {
                bool ammonia = i == 0; string recipe = ammonia ? "recipe_phase4b_high_pressure_nh3" : "recipe_phase4b_device12_co2_h2_conversion";
                _state.AddItem(ammonia ? "compound_n2" : "compound_co2"); _state.AddItem("compound_h2", ammonia ? 3 : 1);
                _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000; OpenDeviceConsole(device.id);
                ClickMineProbeOption("运行 " + GameUI.RecipeLabel(recipe), "_deviceActions");
                if (_state.DeviceInstance("u28_scene_reactor").state != DeviceProcessingState.Processing) throw new InvalidOperationException("Core Shell actual recipe button");
                _deviceRuntime.Advance(10); _ui.CloseAllPanels(); OpenDeviceConsole(device.id);
                ClickMineProbeOption("领取 u28_scene_reactor", "_deviceActions"); _ui.CloseAllPanels(); UpdateAgent(0);
                string expected = i == 0 ? "core_mantle_condition_recall" : i == 1 ? "core_multi_condition_reasoning" : i == 2 ? "core_catalyst_observed" : "core_high_pressure_catalysis_completed";
                if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != expected ||
                    GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                    throw new InvalidOperationException("Core Shell route priority/HUD " + i);
                if (ammonia && (_state.agent.core_shell.catalysis_observed || _state.agent.core_shell.multi_condition_observed)) throw new InvalidOperationException("NH3 iron misclassified as silver/temperature");
            }
            if (_state.CountItem("material_iron") != iron + 1 || _state.CountItem("material_silver_catalytic") != silver + 1 ||
                _state.agent.core_shell.multi_step_observed || _state.agent.core_shell.mature) throw new InvalidOperationException("Core Shell catalyst conservation or fabricated source event gap");
            _state.Save(); var read = GameState.LoadOrNew();
            if (!read.agent.core_shell.catalysis_observed || read.agent.core_shell.mature) throw new InvalidOperationException("Core Shell actual disk state");
            Debug.Log("[U28_CORE_SHELL_SCENE_PROBE] PASS: actual entry/detector/reactor manufacture, NH3 and three silver conversion buttons/collection/HUD/recall, non-consumed catalysts and saved attention; no fabricated multi-step/maturity; resource/position/clock fixtures explicit");
        }

        void RunPlanetarySceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            _player.position = RegionSpawnPos("planetary_core"); UpdateRegionAndRadiation(); UpdateAgent(0); // Position/access fixture, not a traversal claim.
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "planetary_core_first_entry" || _state.agent.planetary.condition_selected)
                throw new InvalidOperationException("Planetary entry without explicit hazard should preserve autonomy");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            const string sample = "item_planetary_core_conductive_core_deposit_block"; _state.AddItem(sample);
            if (PublishAgent("MATERIAL_COLLECTED", sample).reason != "unknown_identity_requires_analysis" || _agent.Queue.Current != null) throw new InvalidOperationException("Planetary unknown identity leaked");
            if (!_deviceRuntime.Start("u20_" + CrystalTundraAgentBehavior.DeviceId, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", sample } }).success)
                throw new InvalidOperationException("Planetary actual detector");
            _deviceRuntime.Advance(10); _deviceRuntime.Collect("u20_" + CrystalTundraAgentBehavior.DeviceId); UpdateAgent(0);
            if (!_state.agent.planetary.analysis_confirmed || _agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "planetary_core_analysis" ||
                GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>().text != "引路人：" + _agent.Queue.Current.text)
                throw new InvalidOperationException("Planetary actual analysis/HUD");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            var tank = _deviceRuntime.Devices.First(d => d.id == PlanetaryCoreAgentBehavior.TankId); foreach (var q in tank.crafting.inputs) _state.AddItem(q.id, q.n);
            if (!_deviceRuntime.Craft(tank.id).success || !_deviceRuntime.Place(tank.id, "u29_scene_tank", "planetary_core", _player.position.x + 3, _player.position.y).success)
                throw new InvalidOperationException("Planetary actual tank manufacture");
            const string liquid = "compound_reduced_carbon_liquid"; int before = _state.CountItem(liquid);
            if (before != 3) throw new InvalidOperationException("Planetary needs actual U28 product chain");
            OpenDeviceConsole(tank.id);
            ClickMineProbeOption("存入 " + ChemistryEngine.DisplayName(liquid), "_deviceActions");
            ClickMineProbeOption("存入 " + ChemistryEngine.DisplayName(liquid), "_deviceActions");
            ClickMineProbeOption("取出 " + ChemistryEngine.DisplayName(liquid), "_deviceActions"); _ui.CloseAllPanels(); UpdateAgent(0);
            var stored = _state.DeviceInstance("u29_scene_tank");
            if (stored.stored_amount != 1 || _state.CountItem(liquid) != before - 1 || !_state.agent.planetary.category_selected || !_state.agent.planetary.device_used ||
                _state.agent.planetary.condition_selected || _state.agent.planetary.mature || _agent.Queue.Current != null || _state.coreTestPassed)
                throw new InvalidOperationException("Planetary actual liquid buttons/conservation/ordinary success restraint");
            ShowCorePreparation(); ClickMineProbeOption("关闭");
            if (_state.agent.shown.Contains("planetary_core_breakthrough") || _state.coreTestPassed) throw new InvalidOperationException("Preparation UI auto-completed final route");
            _state.Save(); var read = GameState.LoadOrNew();
            if (!read.agent.planetary.analysis_confirmed || read.DeviceInstance("u29_scene_tank").stored_amount != 1 || read.agent.planetary.mature)
                throw new InvalidOperationException("Planetary actual save snapshot");
            Debug.Log("[U29_PLANETARY_SCENE_PROBE] PASS: actual entry/unknown silence/detector/HUD, tank manufacture and two store/one withdraw UI buttons using U28 actual output, preparation UI and save; ordinary successes silent, no invented condition/maturity/victory; acquisition/Stage/position/time fixtures explicit");
        }

        void RunPlayerMotorSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue();
            var candidate = _terrain.Patches.First(p => p.region == "dead_city_ruins");
            Vector2 start = new Vector2(candidate.x + candidate.width / 2f, candidate.floor + TerrainRuntimeService.PlayerFootOffset);
            if (_terrain.Collides(TerrainRuntimeService.PlayerBounds(start))) throw new InvalidOperationException("Player grounded fixture collides");
            _player.position = start; _vy = 0; _hoverElapsed = 0;
            if (!OnGround) throw new InvalidOperationException("Player fixture not grounded");
            if (_state.HasAbility("ability_h2_hover")) throw new InvalidOperationException("Player probe requires earned fresh ability");
            _state.AddItem("element_h", 2); // Feedstock fixture, real reaction commit grants the ability.
            if (!_reactionService.Execute("reaction_h2_synthesis", new[] { "synthesis_device" }).success) throw new InvalidOperationException("Player actual H2 unlock");
            float energy = _state.energy; var jump = AdvancePlayerMovement(0, true, true, 1 / 60f);
            if (jump.energy_spent != 0 || _player.position.y <= start.y || OnGround) throw new InvalidOperationException("Player Main jump/collision");
            float maximum = _player.position.y;
            for (int i = 0; i < 72; i++)
            {
                AdvancePlayerMovement(0, false, true, 1 / 60f); maximum = Mathf.Max(maximum, _player.position.y);
                if (_terrain.Collides(TerrainRuntimeService.PlayerBounds(_player.position))) throw new InvalidOperationException("Player hover crossed terrain");
            }
            if (Mathf.Abs(energy - _state.energy - 18) > .001f || maximum <= start.y + 1 || Mathf.Abs(_hoverElapsed - 1.2f) > .001f) throw new InvalidOperationException("Player Main full hover cost/time");
            for (int i = 0; i < 300 && !OnGround; i++) AdvancePlayerMovement(0, false, false, 1 / 60f);
            AdvancePlayerMovement(0, false, false, 1 / 60f);
            if (!OnGround || _hoverElapsed != 0 || _terrain.Collides(TerrainRuntimeService.PlayerBounds(_player.position))) throw new InvalidOperationException("Player Main landing/reset");
            _ui.ShowDialogue("移动锁验证", "打开面板后停止运动与能耗", new[] { "关闭" }, option => _ui.CloseAllPanels());
            Vector3 paused = _player.position; float remaining = _state.energy;
            AdvancePlayerMovement(1, true, true, .2f);
            if (_player.position != paused || _state.energy != remaining) throw new InvalidOperationException("Player Main interaction lock");
            ClickMineProbeOption("关闭"); CapturePlayerPosition(); _state.Save();
            Debug.Log("[U32_PLAYER_SCENE_PROBE] PASS: actual H2 commit unlock, shared Main input movement/Terrain.Move jump, full 1.2s hover/18 energy, no terrain penetration, landing/reset, panel lock and save; starting position/feedstock/fixed-step input fixtures explicit");
        }

        void RunCombatSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue();
            var e = _combat.Alive.Single(); string region = e.origin_region;
            if (e.id != "enemy_0001" || !_enemyVisuals.ContainsKey(e.id) || !_enemyVisuals[e.id].gameObject.activeSelf) throw new InvalidOperationException("Combat real encounter/visual missing");
            _player.position = new Vector2(e.x - 4, e.y); UpdateRegionAndRadiation(); // Position fixture; enemy remains at its real generated location.
            float beforeHp = _state.hp, beforeX = e.x;
            for (int step = 0; step < 30 && _state.hp == beforeHp; step++) TickCombat(.1f);
            if (e.x >= beforeX || _state.hp >= beforeHp || e.attack_cooldown <= 0) throw new InvalidOperationException("Combat actual pursuit/attack");
            string paused = JsonUtility.ToJson(_state.combat); float hp = _state.hp;
            _ui.ShowDialogue("战斗暂停验证", "面板打开时停止追击与攻击", new[] { "关闭" }, option => _ui.CloseAllPanels()); TickCombat(10);
            if (JsonUtility.ToJson(_state.combat) != paused || hp != _state.hp || TryCombatPrimary(new Vector2(e.x, e.y))) throw new InvalidOperationException("Combat advanced through blocking panel");
            ClickMineProbeOption("关闭");
            _player.position = new Vector2(e.x - 5.01f, e.y);
            if (TryCombatPrimary(new Vector2(e.x, e.y))) throw new InvalidOperationException("Combat out-of-reach Main attack");
            _player.position = new Vector2(e.x - 1.5f, e.y); int loot = _state.CountItem(e.drop); float health = e.health;
            if (!TryCombatPrimary(new Vector2(e.x, e.y)) || e.health != health - 12 || _enemyVisuals[e.id].transform.Find("health").localScale.x >= 1.5f)
                throw new InvalidOperationException("Combat Main click/health bar");
            while (e.alive) if (!TryCombatPrimary(new Vector2(e.x, e.y))) throw new InvalidOperationException("Combat kill callback");
            if (_state.CountItem(e.drop) != loot + 1 || _enemyVisuals[e.id].gameObject.activeSelf || TryCombatPrimary(new Vector2(e.x, e.y)) || _state.CountItem(e.drop) != loot + 1)
                throw new InvalidOperationException("Combat kill reward/visual/duplicate protection");
            _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation();
            _player.position = RegionSpawnPos(region); UpdateRegionAndRadiation();
            var next = _combat.Alive.Single(); if (next.id != "enemy_0002") throw new InvalidOperationException("Combat reentry encounter");
            _player.position = new Vector2(next.x - 1.5f, next.y); TickCombat(.1f);
            if (!TryCombatPrimary(new Vector2(next.x, next.y)) || next.health != next.max_health - 12 || next.attack_cooldown <= 0) throw new InvalidOperationException("Combat partial snapshot fixture");
            RefreshHud(); CapturePlayerPosition(); _state.Save(); var restored = GameState.LoadOrNew();
            if (restored.combat.entities.Single(x => x.id == next.id).health != next.health || restored.combat.next_entity_id != 3) throw new InvalidOperationException("Combat actual directory save");
            Debug.Log("[U30_COMBAT_SCENE_PROBE] PASS: actual encounter/visual/pursuit/damage, blocking panel pause, Main primary attack/reach/health bar, kill/direct one-time reward, reentry and wounded cooldown save; player positioning/time advancement fixtures explicit");
        }

        void RunSaltSceneProbe()
        {
            _ui.CloseAllPanels(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            const string region = SaltMarshAgentBehavior.RegionId, acid = "resource_acidic_solution", alkaline = "resource_alkaline_solution";
            var resource = GameObject.Find("chemistry_node_resource_catalog_" + region);
            if (resource == null) throw new InvalidOperationException("Salt actual resource menu missing");
            _player.position = new Vector2(resource.transform.position.x, 1.2f); UpdateRegionAndRadiation();
            if (!_state.agent.shown.Contains("salt_marsh_first_entry")) throw new InvalidOperationException("Salt confirmed entry missing");
            DrainAgentProbeQueue();
            var sampler = _interactables.First(i => Vector2.Distance(i.Pos, resource.transform.position) < 0.01f && i.label().StartsWith("区域资源采样", StringComparison.Ordinal));
            foreach (string id in new[] { acid, acid, acid, alkaline, alkaline, alkaline, alkaline })
            {
                int index = Array.IndexOf(_regionById[region].resources, id), beforeCount = _state.CountItem(id);
                sampler.action();
                for (int page = 0; page < index / 3; page++)
                    _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text == "下一页").onClick.Invoke();
                string prefix = (index % 3 + 1) + ". " + ChemistryEngine.DisplayName(id);
                _ui.GetComponentsInChildren<UnityEngine.UI.Button>().Last(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith(prefix, StringComparison.Ordinal)).onClick.Invoke();
                if (_state.CountItem(id) != beforeCount + 1) throw new InvalidOperationException("Salt finite resource button failed " + id);
            }
            if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != "salt_unknown_material_observed" || _agent.Queue.Current.text.Contains("盐酸"))
                throw new InvalidOperationException("Salt unknown sample leaked composition");
            string before = GameplaySnapshot(_state);
            if (PublishAgent("MATERIAL_ANALYZED", acid).reason != "knowledge_not_discovered" || GameplaySnapshot(_state) != before)
                throw new InvalidOperationException("Salt fake scan accepted");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            for (int cycle = 0; cycle < 2; cycle++)
            {
                foreach (string actionId in new[] { "neutralize_solutions", "precipitate_solution" })
                {
                    var node = GameObject.Find("chemistry_node_" + actionId);
                    _player.position = new Vector2(node.transform.position.x, 1.2f);
                    _interactables.First(i => Vector2.Distance(i.Pos, node.transform.position) < 0.01f).action();
                    DrainAgentProbeQueue(); _agentClockBase += 5000;
                }
            }
            if (!_state.agent.salt.neutralization_observed || !_state.agent.salt.precipitation_observed || !_state.agent.shown.Contains("salt_filter_need_observed") ||
                _state.CountItem("compound_mixed_reaction_slurry") != 2 || _state.agent.salt.mature)
                throw new InvalidOperationException("Actual neutralization / repeated precipitation observations failed");
            // Positioning and manufacturing Stage/raw materials are test prerequisites, not a full player route.
            foreach (string visit in new[] { "dead_city_ruins", "crystal_tundra" })
            { _player.position = RegionSpawnPos(visit); UpdateRegionAndRadiation(); DrainAgentProbeQueue(); _agentClockBase += 5000; }
            _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation();
            _state.laboratoryStage = Math.Max(1, _state.laboratoryStage);
            foreach (string deviceId in new[] { "device_chemical_workbench", CrystalTundraAgentBehavior.DeviceId, SaltMarshAgentBehavior.DeviceId })
            {
                if (!_state.HasCraftedDevice(deviceId))
                {
                    foreach (var q in _deviceRuntime.Devices.First(d => d.id == deviceId).crafting.inputs) _state.AddItem(q.id, q.n);
                    var craft = _deviceRuntime.Craft(deviceId);
                    if (!craft.success) throw new InvalidOperationException("Salt manufacturing fixture " + deviceId + ":" + craft.reason);
                }
                DrainAgentProbeQueue(); _agentClockBase += 5000;
            }
            if (!_state.agent.shown.Contains("salt_distiller_available") || _deviceRuntime.Craft(SaltMarshAgentBehavior.FilterId).reason != "unlock_region_required")
                throw new InvalidOperationException("Distiller availability / early filter gate failed");
            // Use an existing clear fossil-strata scene position to confirm the real region visit.
            var fossilMarker = GameObject.Find("chemistry_node_resource_catalog_fossil_strata");
            if (fossilMarker == null) throw new InvalidOperationException("Fossil region fixture marker missing");
            _player.position = fossilMarker.transform.position; UpdateRegionAndRadiation(); DrainAgentProbeQueue(); _agentClockBase += 5000;
            if (!_state.HasVisitedRegion("fossil_strata")) throw new InvalidOperationException("Fossil region visit not confirmed");
            _player.position = RegionSpawnPos("prologue_lab"); UpdateRegionAndRadiation();
            foreach (var q in _deviceRuntime.Devices.First(d => d.id == SaltMarshAgentBehavior.FilterId).crafting.inputs) _state.AddItem(q.id, q.n);
            if (!_deviceRuntime.Craft(SaltMarshAgentBehavior.FilterId).success) throw new InvalidOperationException("Filter fixture craft failed");
            DrainAgentProbeQueue(); _agentClockBase += 5000;
            foreach (var row in new[] { new[] { SaltMarshAgentBehavior.FilterId, "recipe_phase2_filter" }, new[] { SaltMarshAgentBehavior.DeviceId, "recipe_phase2_distill" } })
            {
                var placed = _deviceRuntime.Place(row[0], "u19_" + row[0], "prologue_lab", _player.position.x + 3f, 1.2f);
                if (!placed.success || !_deviceRuntime.Start(placed.instance_id, row[1]).success) throw new InvalidOperationException("Salt real separation start failed");
                if (row[1] == "recipe_phase2_filter" && _state.agent.salt.separation_used) throw new InvalidOperationException("Salt start counted as delivered separation");
                _deviceRuntime.Advance(10);
                if (!_deviceRuntime.Collect(placed.instance_id).success) throw new InvalidOperationException("Salt real separation collection failed");
                string reaction = row[1] == "recipe_phase2_filter" ? "salt_filtration_completed" : "salt_distillation_completed";
                if (_agent.Queue.Current == null || _agent.Queue.Current.reaction_id != reaction || _state.agent.salt.mature)
                    throw new InvalidOperationException("Salt separation celebration / premature maturity " + row[1]);
                DrainAgentProbeQueue(); _agentClockBase += 5000;
            }
            var analyzer = _deviceRuntime.Place(CrystalTundraAgentBehavior.DeviceId, "u19_analyzer", "prologue_lab", _player.position.x + 6f, 1.2f);
            if (!analyzer.success || !_deviceRuntime.Start(analyzer.instance_id, "recipe_phase2_detect", new Dictionary<string,string> { { "sample_id", acid } }).success)
                throw new InvalidOperationException("Salt actual analyzer failed");
            _deviceRuntime.Advance(10); if (!_deviceRuntime.Collect(analyzer.instance_id).success) throw new InvalidOperationException("Salt analyzer collect failed");
            UpdateAgent(0);
            var text = GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>();
            if (!_state.agent.salt.mature || !_state.agent.salt.analysis_completed || !text.text.Contains("盐酸") ||
                _state.CountItem(acid) != 1 || !_state.agent.salt.filtration_used || !_state.agent.salt.distillation_used)
                throw new InvalidOperationException("Salt confirmed exact HUD / zero-consumption scan / maturity failed");
            // Analysis makes the guidance mature before the separate pH event: attention must still
            // record confirmed pH, while presentation remains quiet as in the source mature contract.
            if (!_state.agent.salt.ph_observed) throw new InvalidOperationException("Salt mature state discarded confirmed pH");
            DrainAgentProbeQueue(); _player.position = new Vector2(resource.transform.position.x, 1.2f); UpdateRegionAndRadiation();
            if (_agent.Queue.Current != null) throw new InvalidOperationException("Mature Salt entry replayed");
            CapturePlayerPosition(); if (!_state.TrySave()) throw new InvalidOperationException(GameState.LastSaveStatus);
            var restored = GameState.LoadOrNew();
            if (JsonUtility.ToJson(restored.agent.salt) != JsonUtility.ToJson(_state.agent.salt)) throw new InvalidOperationException("Salt real-directory snapshot mismatch");
            Debug.Log("[U19_SALT_SCENE_PROBE] PASS: actual menu buttons gather finite reactants; two real neutralization/precipitation callbacks; filter gate; real separation craft/place/process/collection; cross-region analysis exact HUD; mature silence and real-directory snapshot; raw manufacturing/Stage1 and position/clock fixtures explicitly used");
        }

        void RunAgentSceneProbe()
        {
            _ui.CloseAllPanels();
            const string sample = "item_dead_city_iron_bearing_debris_block";
            var target = _terrain.Cells.First(p => p.Key.x >= 45 && p.Key.x < 110 && p.Key.y == -1 &&
                _terrain.Definition(p.Value).item_id == sample && _terrain.MiningStatus(p.Key, TerrainRuntimeService.Center(p.Key) + Vector2.up * 1.7f).success).Key;
            _player.position = TerrainRuntimeService.Center(target) + Vector2.up * 1.7f;
            UpdateRegionAndRadiation();
            if (!_state.agent.shown.Contains("dead_city_first_entry")) throw new InvalidOperationException("Actual region event not routed to Agent");
            MineTerrainResource("dead_city_ruins", sample, "含铁样本");
            var drop = _state.worldDrops.Single();
            _player.position = new Vector2(drop.x, drop.y + 1f);
            RefreshWorldDrops(true);
            string before = GameplaySnapshot(_state);
            if (PublishAgent("MATERIAL_ANALYZED", sample).reason != "unconfirmed_knowledge" || GameplaySnapshot(_state) != before)
                throw new InvalidOperationException("Scene Agent inferred analysis from appearance or mutated gameplay");
            if (!_worldChemistry.AnalyzeDeadCityBlock(sample).success || _agent.Queue.Current.reaction_id != "first_iron_analysis")
                throw new InvalidOperationException("Formal analysis event bridge missing");
            UpdateAgent(0f);
            var text = GameObject.Find("AgentDialogue").GetComponent<UnityEngine.UI.Text>();
            if (!text.text.Contains("含铁") || _lumenSr.color != new Color(0.55f, 1f, 1f))
                throw new InvalidOperationException("Agent speech / SCAN presentation missing");
            before = GameplaySnapshot(_state);
            float remaining = _agent.Queue.Current.remaining;
            _ui.ShowDialogue("Probe", "pause", new[] { "close" }, _ => { });
            UpdateAgent(2f);
            if (text.text != "" || _agent.Queue.Current.remaining != remaining || GameplaySnapshot(_state) != before)
                throw new InvalidOperationException("Panel lock did not pause/hide Agent dialogue");
            _ui.CloseAllPanels();
            before = GameplaySnapshot(_state);
            var failed = _reactionService.Execute("reaction_water_synthesis", new string[0]);
            UpdateAgent(0f);
            if (failed.success || _agent.Queue.Current.action != "WARN" || _agent.Queue.Current.priority != 100 || text.text == "" || GameplaySnapshot(_state) != before)
                throw new InvalidOperationException("Failed experiment event / warning priority / gameplay guard failed");
            CapturePlayerPosition();
            if (!_state.TrySave()) throw new InvalidOperationException(GameState.LastSaveStatus);
            var restored = GameState.LoadOrNew();
            if (!restored.agent.shown.Contains("first_iron_analysis")) throw new InvalidOperationException("Actual disk Agent snapshot lost");
            var loaded = new AgentController(restored, _agentCatalog);
            if (loaded.Publish("REGION_ENTERED", restored.currentRegion, restored.agent.clock_ms + 10000).reason != "already_shown")
                throw new InvalidOperationException("Actual disk reload replayed entry dialogue");
            Debug.Log("[U16_AGENT_SCENE_PROBE] PASS: actual region/collection/analysis/failure event bridge; speech/visual/queue/panel pause; gameplay guard; real-directory save/load suppression");
        }

        void HandleTerrainInput()
        {
            if (!Input.GetMouseButton(0)) _combatPointerCaptured = false;
            if (_ui.AnyPanelOpen() || (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                _mining.Cancel(); _terrainCursor.enabled = false; return;
            }
            if (Input.GetKeyDown(KeyCode.B))
            {
                var items = _terrain.PlaceableItems.Select(b => b.item_id).OrderBy(id => id).ToArray();
                if (items.Length == 0) _ui.ShowMessage("背包中没有可放置的地形方块");
                else
                {
                    _buildItem = items[(Array.IndexOf(items, _buildItem) + 1) % items.Length];
                    _ui.ShowMessage("建造方块：" + ChemistryEngine.DisplayName(_buildItem) + "（右键放置，B切换）");
                }
            }
            Vector2 point = _camera.ScreenToWorldPoint(Input.mousePosition);
            if (_combatPointerCaptured) { _mining.Cancel(); _terrainCursor.enabled = false; return; }
            if (Input.GetMouseButtonDown(0) && TryCombatPrimary(point)) { _combatPointerCaptured = true; _mining.Cancel(); _terrainCursor.enabled = false; return; }
            // Holding a clicked enemy must not start mining behind it on the following frame.
            if (Input.GetMouseButton(0) && _combat.EnemyAtPointer(_player.position, point).Length > 0) { _mining.Cancel(); _terrainCursor.enabled = false; return; }
            var cell = Vector2Int.FloorToInt(point);
            bool close = Vector2.Distance(_player.position, TerrainRuntimeService.Center(cell)) <= TerrainRuntimeService.BuildRange;
            _terrainCursor.enabled = close && _terrain.InBounds(cell);
            _terrainCursor.transform.position = TerrainRuntimeService.Center(cell);
            if (Input.GetMouseButtonDown(1))
            {
                var result = _terrain.Place(cell, _buildItem, _player.position);
                _ui.ShowMessage(result.success ? "放置方块 ×1" : "无法放置：" + result.reason);
                if (result.success) { CapturePlayerPosition(); _state.Save(); }
            }
            if (!Input.GetMouseButton(0)) { _mining.Cancel(); return; }
            var status = _terrain.MiningStatus(cell, _player.position);
            if (!status.success)
            {
                _mining.Cancel();
                if (Input.GetMouseButtonDown(0) && status.reason == "corrosion_resistance_too_low") PublishCorrosionGateFailure();
                if (Input.GetMouseButtonDown(0)) _ui.ShowMessage("无法开采：" + status.reason);
                return;
            }
            if (_mining.active && Mathf.Abs(_mining.duration - status.duration) > 0.0001f) _mining.Cancel();
            _mining.Begin(cell, status.duration);
            _ui.ShowHint("开采 " + ChemistryEngine.DisplayName(status.item_id) + " " + Mathf.RoundToInt(_mining.Ratio * 100f) + "%");
            if (_mining.Advance(Time.deltaTime))
            {
                var result = _terrain.MineToDrop(cell, _player.position);
                _mining.Cancel();
                if (result.success)
                {
                    PublishAgent("MATERIAL_MINED", result.item_id);
                    RefreshWorldDrops(false);
                    _ui.ShowMessage("掉落 " + ChemistryEngine.DisplayName(result.item_id) + " ×1，靠近拾取");
                    Sfx("capture"); CapturePlayerPosition(); _state.Save();
                }
            }
        }

        bool ArriveAtRegion(string regionId, Vector2 destination)
        {
            if (!_regionById.ContainsKey(regionId) || !SurvivalRuntimeService.Finite(destination.x) || !SurvivalRuntimeService.Finite(destination.y) ||
                !_terrain.InBounds(Vector2Int.FloorToInt(destination)) || RegionOf(destination).id != regionId || _terrain.Collides(TerrainRuntimeService.PlayerBounds(destination)))
            { _ui.ShowMessage("目标落点不可用，请选择其他入口"); return false; }
            _player.position = destination; _vy = 0; _hoverElapsed = 0; _moveX = 0; _mining.Cancel(); _combatPointerCaptured = false;
            _camera.transform.position = new Vector3(destination.x, destination.y + CameraVerticalOffset, -10);
            UpdateRegionAndRadiation(true); CapturePlayerPosition(); _state.Save(); return true;
        }

        void UpdateRegionAndRadiation(bool observationOnly = false)
        {
            var region = RegionOf(_player.position);
            if (region.id != _state.currentRegion)
            {
                _state.currentRegion = region.id;
                _state.AddVisitedRegion(region.id);
                _combat.EnsureEncounter(region.id, RegionSpawnPos(region.id));
                PublishAgent(region.id == "prologue_lab" ? "RETURNED_TO_LAB" : "REGION_ENTERED", region.id);
                if (region.id == "rust_jungle") PublishAgent("ENVIRONMENT_EXPOSURE_CHANGED", "corrosion");
                if (region.id == "rust_jungle" && _state.laboratoryStage == 0)
                    _laboratoryProgression.RecordCorrosionGateDiscovered();
                var pal = PaletteOf(region);
                _ui.ShowToast(Localization.Tr(region.name_key) +
                              (region.radiation > 0.01f ? "(辐射!尽快离开或回舱清除)" : ""));
                _ambient.SetStyle(pal.particle, pal.accent);
            }
            if (!observationOnly && !_ui.AnyPanelOpen()) _environmentRuntime.TickExposure(region, Time.deltaTime);
            if (!observationOnly) TickCombat(Time.deltaTime);
            if (!observationOnly && _state.hp <= 0f)
            {
                _state.hp = 60f;
                _state.radiation = 0f;
                var lab = RegionSpawnPos("prologue_lab");
                _player.position = new Vector3(lab.x, lab.y, 0f);
                _camera.transform.position = new Vector3(lab.x, lab.y + CameraVerticalOffset, -10f);
                _ui.ShowToast("意识在休眠舱中重启……");
            }
        }

        void TickCombat(float seconds)
        {
            bool ready = _terrain != null && _terrain.InBounds(Vector2Int.FloorToInt(_player.position)) && RegionOf(_player.position).id == _state.currentRegion;
            _combat.Tick(_player.position, seconds, ready, _ui.AnyPanelOpen()); RefreshCombatVisuals();
        }

        bool TryCombatPrimary(Vector2 point)
        {
            if (_ui.AnyPanelOpen()) return false;
            bool killed, rewarded;
            if (!_combat.ClickAttack(_player.position, point, out killed, out rewarded)) return false;
            RefreshCombatVisuals();
            if (killed) _ui.ShowToast(rewarded ? "敌人已击败，获得材料 ×1" : "敌人已击败，背包无法接收材料");
            CapturePlayerPosition(); _state.Save(); return true;
        }

        void RefreshCombatVisuals()
        {
            if (_combat == null) return;
            foreach (var e in _state.combat.entities)
            {
                CombatEnemyVisual visual;
                if (!_enemyVisuals.TryGetValue(e.id, out visual))
                {
                    if (!e.alive) continue;
                    var def = _combat.Catalog.enemies.FirstOrDefault(d => d.kind == e.kind);
                    if (def == null) continue;
                    visual = CombatEnemyVisual.Create(e, def); visual.transform.SetParent(transform, true); _enemyVisuals[e.id] = visual;
                }
                visual.Sync(e);
            }
        }

        void HandleInteraction()
        {
            if (_ui.AnyPanelOpen()) return;
            Interactable best = null;
            float bestDist = float.MaxValue;
            var pp = new Vector2(_player.position.x, _player.position.y);
            foreach (var it in _interactables)
            {
                if (it.requiredStep != null && !it.requiredStep()) continue;
                float d = Vector2.Distance(pp, it.Pos);
                if (d < it.radius && d < bestDist) { bestDist = d; best = it; }
            }
            if (best != null)
            {
                _ui.ShowHint(best.label());
                if (Input.GetKeyDown(KeyCode.E)) best.action();
            }
        }

        // ───────────────────────── 区域几何 ─────────────────────────
        RegionDef RegionOf(Vector2 p)
        {
            if (_terrain != null)
            {
                var terrainRegion = _terrain.RegionAt(Vector2Int.FloorToInt(p));
                if (_regionById.ContainsKey(terrainRegion)) return _regionById[terrainRegion];
            }
            if (p.y > -6f)
            {
                int idx = Mathf.Clamp(Mathf.FloorToInt((p.x + SurfaceRegionWidth * 0.5f) / SurfaceRegionWidth),
                                      0, _surfaceOrder.Count - 1);
                return _regionById[_surfaceOrder[idx]];
            }
            int layer = Mathf.Clamp(Mathf.FloorToInt((-p.y - 10f) / UndergroundLayerHeight),
                                    0, _undergroundOrder.Count - 1);
            return _regionById[_undergroundOrder[layer]];
        }

        Vector2 RegionSpawnPos(string regionId)
        {
            int undergroundIndex = _undergroundOrder.IndexOf(regionId);
            if (undergroundIndex >= 0)
                return new Vector2(0f, GroundTopY - 10f - (undergroundIndex + 1) * UndergroundLayerHeight + 1.2f);
            int idx = _surfaceOrder.IndexOf(regionId);
            if (idx < 0) idx = 1;
            return new Vector2(idx * SurfaceRegionWidth - 10f, 1.2f);
        }

        // ───────────────────────── 碎片谜题 ─────────────────────────
        void TalkToShard(string shardId)
        {
            var shard = _shardById[shardId];
            if (_state.HasCalmed(shardId))
            {
                _ui.ShowDialogue(Localization.Tr(shard.name_key),
                    Localization.Tr(shard.dialogue.calmed), null, _ => _ui.CloseAllPanels());
                return;
            }
            _activeShard = shard;
            Sfx("dialogue_prompt");
            _ui.ShowDialogue(Localization.Tr(shard.name_key),
                Localization.Tr(shard.dialogue.persona) + "\n\n" +
                Localization.Tr(shard.dialogue.prompt),
                LanguageOptions(shard.language), OnShardOption);
        }

        string[] LanguageOptions(string correctLanguage)
        {
            var all = new List<string>();
            foreach (var s in _shards.shards)
                if (!all.Contains(s.language)) all.Add(s.language);
            var pool = all.Where(l => l != correctLanguage).ToList();
            Shuffle(pool);
            var options = pool.Take(3).ToList();
            options.Add(correctLanguage);
            Shuffle(options);
            return options.Select(LanguageLabel).ToArray();
        }

        static string LanguageLabel(string languageId)
        {
            return "「" + Localization.Tr("content.codex." + languageId + ".name") + "」";
        }

        static void Shuffle(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        void OnShardOption(string optionLabel)
        {
            if (_activeShard == null || optionLabel == "-1") { _ui.CloseAllPanels(); return; }
            var shard = _activeShard;
            var chosen = optionLabel.Trim('「', '」');
            if (chosen == Localization.Tr("content.codex." + shard.language + ".name"))
            {
                _state.AddCalmed(shard.id);
                _state.Save();
                _ui.ShowDialogue(Localization.Tr(shard.name_key),
                    Localization.Tr(shard.dialogue.accepted) + "\n\n" +
                    Localization.Tr(shard.dialogue.calmed),
                    null, _ =>
                    {
                        _ui.CloseAllPanels();
                        if (AtStep("calm_shards") && _state.calmedShards.Length >= 3)
                            AdvanceTo("lab_stage3", "已平息 3 个碎片 — 继续发现新反应(5 个)");
                    });
            }
            else
            {
                _ui.ShowDialogue(Localization.Tr(shard.name_key),
                    Localization.Tr(shard.dialogue.rejected) + "\n\n(提示:" +
                    Localization.Tr(shard.dialogue.hint) + ")",
                    LanguageOptions(shard.language), OnShardOption);
            }
        }

        // ───────────────────────── 记忆 ─────────────────────────
        void OpenMemory(string memoryId)
        {
            if (_state.HasMemory(memoryId)) { _ui.ShowMessage("这段记忆已经重构过了"); return; }
            var mem = _memories.memories.FirstOrDefault(m => m.id == memoryId);
            if (mem == null) return;
            var regionShard = _regionById.ContainsKey(mem.region) ? _regionById[mem.region].shard : "";
            bool unlocked = (!string.IsNullOrEmpty(regionShard) && _state.HasCalmed(regionShard)) ||
                            _state.discoveredReactions.Any(rid =>
                            {
                                var r = ChemistryEngine.Recipe(rid);
                                return r != null && r.memory_id == memoryId;
                            });
            if (!unlocked)
            {
                _ui.ShowMessage("记忆信号模糊 — 先平息此区域的碎片,或完成相关反应");
                return;
            }
            _state.AddMemory(memoryId);
            _state.Save();
            var body = new StringBuilder();
            if (!string.IsNullOrEmpty(mem.body_key))
                body.AppendLine(Localization.Tr(mem.body_key));
            foreach (var fk in mem.fragments)
                body.AppendLine(Localization.Tr(fk));
            _ui.ShowMemory(Localization.Tr(mem.title_key), body.ToString());
            if (AtStep("descend_memories") && _state.reconstructedMemories.Length >= 8)
                AdvanceTo("core_ending", "记忆已齐 — 完成核心准备、8 把主密钥与氮反应链后前往裂核");
        }

        // ───────────────────────── 合成 ─────────────────────────
        void OpenSynthesis()
        {
            var owned = _state.OwnedMaterialIds();
            owned.Sort();
            _ui.ShowSynthesis(owned, new List<string>(), _state.discoveredReactions.Length,
                ChemistryEngine.Recipes.Count, TryCombineSelected);
        }

        ReactionExecutionResult TryCombineSelected()
        {
            var selected = _ui.SelectedMaterials;
            var recipe = ChemistryEngine.MatchExact(selected);
            var environmentTags = CurrentReactionEnvironmentTags();
            bool isNew = recipe != null && !_state.HasReaction(recipe.id);
            var result = _reactionService.Execute(recipe != null ? recipe.id : "", environmentTags);
            if (!result.success) return result;
            // ReactionService 已在事务提交后登记 reaction/language；这里仅负责表现与任务推进。
            var prodText = string.Join(" + ", recipe.products
                .Select(p => ChemistryEngine.DisplayName(p.id) + "×" + p.n).ToArray());
            _ui.ShowMessage((isNew ? "新反应!" : "再次合成:") + recipe.title + " → " + prodText);
            Sfx("h2_reaction");
            if (AtStep("synthesis"))
                AdvanceTo("demonstrate", "合成成功 — 去引路人终端演示 H₂");
            if (AtStep("lab_stage3") && _state.discoveredReactions.Length >= 5)
                AdvanceTo("descend_memories", "研究步入正轨 — 坐电梯下潜,重构 8 段记忆");
            _state.Save();
            return result;
        }

        string[] CurrentReactionEnvironmentTags()
        {
            var tags = new HashSet<string> { "synthesis_device" };
            RegionDef region;
            if (_regionById.TryGetValue(_state.currentRegion, out region))
                foreach (var tag in region.tags ?? new string[0]) tags.Add(tag);
            return tags.ToArray();
        }

        void OnReactionGameplayEvent(ReactionGameplayEvent gameplayEvent)
        {
            // Existing formal reaction event only; do not invent condition publications.
            PublishAgent(gameplayEvent.event_type == "experiment_failed" ? "EXPERIMENT_FAILED" : "REACTION_OBSERVED", gameplayEvent.reaction_id);
            if (gameplayEvent.event_type == "reaction_committed" && gameplayEvent.reaction_id == "reaction_rust" && _state.currentRegion == "rust_jungle")
                PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.reaction_id, evidence: new AgentEvent { observed = true, related_hazard = true });
            Debug.Log("[ReactionEvent] type=" + gameplayEvent.event_type +
                      ", reaction=" + gameplayEvent.reaction_id +
                      ", reason=" + gameplayEvent.reason);
        }

        void OnDeviceGameplayEvent(DeviceGameplayEvent gameplayEvent)
        {
            var planetaryStorage = PlanetaryCoreAgentBehavior.StorageEvent(gameplayEvent);
            if (planetaryStorage != null)
            {
                PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.event_type, evidence: planetaryStorage);
                PublishAgent("WORLD_REACTION_OBSERVED", "final_core_preparation_updated", evidence: new AgentEvent { behavior_profile = "planetary_core", phenomenon = "final_core_preparation_updated" });
            }
            if (gameplayEvent.event_type == "device_built") PublishAgent("DEVICE_CRAFTED", gameplayEvent.device_id, evidence:
                gameplayEvent.device_id == FluorescentGrottoAgentBehavior.TankId ? new AgentEvent { behavior_profile = FluorescentGrottoAgentBehavior.RegionId } :
                gameplayEvent.device_id == EmberThermalAgentBehavior.CondenserId ? new AgentEvent { behavior_profile = "ember_rift" } :
                gameplayEvent.device_id == CrystalTundraAgentBehavior.DeviceId ? new AgentEvent { behavior_profile = CrystalTundraAgentBehavior.RegionId } :
                gameplayEvent.device_id == SaltMarshAgentBehavior.DeviceId || gameplayEvent.device_id == SaltMarshAgentBehavior.FilterId ?
                    new AgentEvent { behavior_profile = SaltMarshAgentBehavior.RegionId } : null);
            else if (gameplayEvent.event_type == "device_placed") PublishAgent("DEVICE_PLACED", gameplayEvent.device_id);
            else if (gameplayEvent.event_type == "device_processing_failed") PublishAgent("EXPERIMENT_FAILED", gameplayEvent.recipe_id);
            if (gameplayEvent.event_type == "device_processing_started" && gameplayEvent.recipe_id == "recipe_phase2_detect" &&
                _agentCatalog != null && _agentCatalog.ForRegion(CrystalTundraAgentBehavior.RegionId).sample_ids.Contains(gameplayEvent.analyzed_sample_id))
            {
                _announcedAnalyses.Add(gameplayEvent.analyzed_sample_id);
                PublishAgent("MATERIAL_ANALYZED", gameplayEvent.analyzed_sample_id, evidence:
                    new AgentEvent { behavior_profile = CrystalTundraAgentBehavior.RegionId, analyzer_used = true });
            }
            if (gameplayEvent.event_type == "device_processing_started" && gameplayEvent.recipe_id == "recipe_phase2_detect" &&
                _agentCatalog != null && SaltGuidanceActive() && _agentCatalog.ForRegion(SaltMarshAgentBehavior.RegionId).sample_ids.Contains(gameplayEvent.analyzed_sample_id))
            {
                _announcedAnalyses.Add(gameplayEvent.analyzed_sample_id);
                PublishAgent("MATERIAL_ANALYZED", gameplayEvent.analyzed_sample_id, evidence:
                    new AgentEvent { behavior_profile = SaltMarshAgentBehavior.RegionId, analyzer_used = true });
                PublishSaltObservation("ph_measured", gameplayEvent.analyzed_sample_id);
            }
            if (gameplayEvent.event_type == "device_processing_started" && gameplayEvent.recipe_id == "recipe_phase2_detect" &&
                _agentCatalog != null && (_state.currentRegion == "ember_rift" || _state.agent.shown.Contains("ember_rift_first_entry")) &&
                _agentCatalog.ForRegion("ember_rift").sample_ids.Contains(gameplayEvent.analyzed_sample_id))
            {
                _announcedAnalyses.Add(gameplayEvent.analyzed_sample_id);
                PublishAgent("MATERIAL_ANALYZED", gameplayEvent.analyzed_sample_id, evidence:
                    new AgentEvent { behavior_profile = "ember_rift", analyzer_used = true });
            }
            if (gameplayEvent.event_type == "device_processing_started" && gameplayEvent.recipe_id == "recipe_phase2_detect" && _agentCatalog != null)
            {
                string owner = _agentCatalog.ForRegion("geode_abyss").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && GeodeGuidanceActive() ? "geode_abyss" :
                    _agentCatalog.ForRegion("planetary_core").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && _state.currentRegion == "planetary_core" ? "planetary_core" :
                    _agentCatalog.ForRegion("core_shell").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && _state.currentRegion == "core_shell" ? "core_shell" :
                    _agentCatalog.ForRegion("mantle_flows").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && _state.currentRegion == "mantle_flows" ? "mantle_flows" :
                    _agentCatalog.ForRegion("radiation_belt").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && _state.currentRegion == "radiation_belt" ? "radiation_belt" :
                    _agentCatalog.ForRegion("magnetic_plateau").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && _state.currentRegion == "magnetic_plateau" ? "magnetic_plateau" :
                    _agentCatalog.ForRegion("abandoned_mine").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && MineGuidanceActive() ? "abandoned_mine" :
                    _agentCatalog.ForRegion("fluorescent_grotto").sample_ids.Contains(gameplayEvent.analyzed_sample_id) && GrottoGuidanceActive() ? "fluorescent_grotto" : "";
                if (owner != "")
                {
                    _announcedAnalyses.Add(gameplayEvent.analyzed_sample_id);
                    PublishAgent("MATERIAL_ANALYZED", gameplayEvent.analyzed_sample_id, evidence: new AgentEvent { behavior_profile = owner, analyzer_used = true });
                    if (owner == "radiation_belt") PublishAgent("MATERIAL_PROPERTY_DISCOVERED", gameplayEvent.analyzed_sample_id, evidence: new AgentEvent { behavior_profile = owner, analyzer_used = true });
                }
            }
            if (gameplayEvent.event_type == "device_processing_started" && gameplayEvent.recipe_id == "recipe_phase3a_store_gas" && GrottoGuidanceActive())
                PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, evidence: new AgentEvent {
                    behavior_profile = FluorescentGrottoAgentBehavior.RegionId, observed = true, phenomenon = "gas_stored" });
            if (gameplayEvent.event_type == "device_output_collected")
            {
                var outputs = (gameplayEvent.items ?? new RecipeQuantity[0]).Where(q => q.n > 0).Select(q => q.id).ToArray();
                var coreEvidence = CoreShellAgentBehavior.DeviceDelivery(gameplayEvent, GameData.ProcessingRecipes);
                if (coreEvidence != null) PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, outputs: outputs, evidence: coreEvidence);
                if (gameplayEvent.recipe_id == "recipe_phase4b_high_pressure_nh3")
                    PublishAgent("WORLD_REACTION_OBSERVED", "final_core_preparation_updated", evidence: new AgentEvent { behavior_profile = "planetary_core", phenomenon = "final_core_preparation_updated" });
                if ((gameplayEvent.recipe_id == "recipe_phase3b_electrolyze" || gameplayEvent.recipe_id == "recipe_phase4b_silver_catalytic_refine") && (_state.currentRegion == "magnetic_plateau" || _state.agent.magnetic.electricity_observed))
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, outputs: outputs, evidence: new AgentEvent { behavior_profile = "magnetic_plateau", observed = true,
                        phenomenon = gameplayEvent.recipe_id == "recipe_phase3b_electrolyze" ? "electrolysis_completed" : "silver_catalytic_material_refined" });
                if (_state.currentRegion == EmberThermalAgentBehavior.RegionId || _state.agent.shown.Contains("ember_rift_first_entry"))
                {
                    string phenomenon = gameplayEvent.recipe_id == "recipe_phase3a_thermal_carbonate" ? "thermal_carbonate_processed" :
                        gameplayEvent.recipe_id == "recipe_phase3a_condense_vapor" ? "vapor_condensed" : "";
                    if (phenomenon != "") PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, outputs: outputs,
                        evidence: new AgentEvent { behavior_profile = EmberThermalAgentBehavior.RegionId, observed = true, phenomenon = phenomenon });
                }
                if (gameplayEvent.recipe_id == "recipe_phase2_filter") PublishSaltObservation("filtration_completed", gameplayEvent.recipe_id, outputs);
                if (gameplayEvent.recipe_id == "recipe_phase2_distill") PublishSaltObservation("distillation_completed", gameplayEvent.recipe_id, outputs);
                if (gameplayEvent.recipe_id == "barium_sulfate_batch_precipitation" && MineGuidanceActive())
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, outputs: outputs, evidence: new AgentEvent {
                        behavior_profile = "abandoned_mine", phenomenon = "first_industrial_precipitation_completed", observed = true, result_success = true });
                if (gameplayEvent.recipe_id == "recipe_phase4a_absorb_gas" && RadiationGuidanceActive())
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, evidence: new AgentEvent { behavior_profile = "radiation_belt", phenomenon = "gas_absorbed", observed = true, has_gas = true });
                if (gameplayEvent.recipe_id == "recipe_phase4a_industrial_crystallize" && (_state.currentRegion == "mantle_flows" || _state.agent.mantle.heat_observed))
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.recipe_id, evidence: new AgentEvent { behavior_profile = "mantle_flows", phenomenon = "industrial_crystallization_completed" });
            }
            PublishAgentAnalyses();
            Debug.Log("[DeviceEvent] type=" + gameplayEvent.event_type +
                      ", device=" + gameplayEvent.device_id +
                      ", instance=" + gameplayEvent.instance_id +
                      ", recipe=" + gameplayEvent.recipe_id +
                      ", reason=" + gameplayEvent.reason);
            if (gameplayEvent.event_type == "device_placed")
                BuildPlacedDeviceVisual(_state.DeviceInstance(gameplayEvent.instance_id));
            if (_laboratoryProgression.RecordDeviceEvent(gameplayEvent))
            {
                _ui.ShowToast("实验室能力已确认 — Stage " + _state.laboratoryStage);
                _state.Save();
            }
        }

        void OnWorldChemistryEvent(WorldChemistryResult gameplayEvent)
        {
            if (gameplayEvent.action_id == "metal_displacement" && MineGuidanceActive())
                PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent { behavior_profile = "abandoned_mine", phenomenon = "metal_displacement",
                    observed = true, result_success = gameplayEvent.success, failure_reason = gameplayEvent.reason, discovery = gameplayEvent.discovery });
            else if (!gameplayEvent.success) PublishAgent(gameplayEvent.action_id == "analyze_terrain_block" || gameplayEvent.action_id == "analyze_rust_sample" || gameplayEvent.action_id == "analyze_sand_block" || gameplayEvent.action_id == "analyze_grotto_block" || gameplayEvent.action_id == "analyze_mine_block" ? "ANALYSIS_FAILED" : "EXPERIMENT_FAILED", gameplayEvent.action_id);
            else
            {
                if ((gameplayEvent.source_event == "ore_identified" || gameplayEvent.source_event == "terrain_block_analyzed") && MineGuidanceActive())
                {
                    _announcedAnalyses.Add(gameplayEvent.subject_id);
                    PublishAgent("MATERIAL_ANALYZED", gameplayEvent.subject_id, evidence: new AgentEvent { behavior_profile = "abandoned_mine" });
                }
                PublishAgentAnalyses();
                if (gameplayEvent.source_event == "radiation_sample_scanned" && RadiationGuidanceActive())
                    PublishAgent("COMPOSITION_DISCOVERED", gameplayEvent.subject_id, evidence: new AgentEvent { behavior_profile = "radiation_belt" });
                if (RadiationGuidanceActive() && gameplayEvent.action_id == "process_gas_bearing_deposit")
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent { behavior_profile = "radiation_belt", has_gas = true, gas_output = "compound_contaminated_gas_stream", observed = true });
                if (RadiationGuidanceActive() && gameplayEvent.action_id == "purify_contamination")
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent { behavior_profile = "radiation_belt", phenomenon = "contamination_purified", observed = true });
                if (gameplayEvent.source_event == "extreme_heat_processed")
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent { behavior_profile = "mantle_flows", phenomenon = "extreme_heat_processed", controlled_extreme_heat = true, process_input = gameplayEvent.subject_id });
                if (gameplayEvent.source_event == "loaded_solution_processed" && (_state.currentRegion == "mantle_flows" || _state.agent.mantle.heat_observed))
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent { behavior_profile = "mantle_flows", phenomenon = "loaded_solution_processed" });
                if (gameplayEvent.source_event == "metal_recovered" && MineGuidanceActive())
                    PublishAgent("MATERIAL_RECOVERED", gameplayEvent.metal, evidence: new AgentEvent { behavior_profile = "abandoned_mine", source_event = "metal_recovered", result_success = true });
                if (gameplayEvent.source_event == "precipitation_test" && MineGuidanceActive())
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.subject_id, evidence: new AgentEvent { behavior_profile = "abandoned_mine", phenomenon = "precipitation_test", observed = true, result_success = true });
                if (gameplayEvent.action_id == "natural_crystallization" && GeodeGuidanceActive())
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent {
                        behavior_profile = GeodeAbyssAgentBehavior.RegionId, observed = true, phenomenon = "natural_crystallization_completed",
                        has_concentration = gameplayEvent.has_concentration, concentration = gameplayEvent.concentration });
                if (gameplayEvent.action_id == "organic_process" && GrottoGuidanceActive())
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence: new AgentEvent {
                        behavior_profile = FluorescentGrottoAgentBehavior.RegionId, observed = true, phenomenon = "organic_process_completed",
                        gas_output = gameplayEvent.inventory_delta.Any(d => d.id == "compound_co2" && d.change > 0) ? "compound_co2" : "" });
                if (gameplayEvent.action_id == "recover_terrain_block")
                    PublishAgent("MATERIAL_RECOVERED", gameplayEvent.action_id, outputs: gameplayEvent.inventory_delta.Where(d => d.change > 0).Select(d => d.id).ToArray());
                if (gameplayEvent.action_id == "use_carbon_fuel" && (_state.currentRegion == "ember_rift" || _state.agent.shown.Contains("ember_rift_first_entry")))
                    PublishAgent("WORLD_REACTION_OBSERVED", gameplayEvent.action_id, evidence:
                        new AgentEvent { behavior_profile = "ember_rift", observed = true, phenomenon = "carbon_fuel_used" });
                if (gameplayEvent.action_id == "neutralize_solutions") PublishSaltObservation("neutralization_completed", gameplayEvent.action_id);
                if (gameplayEvent.action_id == "precipitate_solution") PublishSaltObservation("precipitate_observed", gameplayEvent.action_id);
            }
            Debug.Log("[WorldChemistryEvent] action=" + gameplayEvent.action_id +
                      ", success=" + gameplayEvent.success + ", reason=" + gameplayEvent.reason);
            if (_laboratoryProgression.RecordWorldChemistryEvent(gameplayEvent))
            {
                _ui.ShowToast("实验室能力已确认 — Stage " + _state.laboratoryStage);
                _state.Save();
            }
        }

        void RestorePlacedDeviceVisuals()
        {
            foreach (var runtime in _state.deviceInstances ?? new DeviceRuntimeContract[0])
                BuildPlacedDeviceVisual(runtime);
        }

        void BuildPlacedDeviceVisual(DeviceRuntimeContract runtime)
        {
            if (runtime == null || !runtime.is_placed || string.IsNullOrEmpty(runtime.region_id) ||
                !_deviceVisualIds.Add(runtime.instance_id)) return;
            var device = _deviceRuntime.Devices.FirstOrDefault(value => value.id == runtime.device_id);
            if (device == null) return;
            var pos = new Vector2(runtime.world_x, runtime.world_y);
            var go = SpriteUtil.RectWorld("placed_" + runtime.instance_id, pos + new Vector2(0f, 1.1f),
                new Vector2(Mathf.Max(1.8f, device.footprint_width * 0.55f),
                    Mathf.Max(2.2f, device.footprint_height * 0.55f)),
                new Color(0.24f, 0.36f, 0.42f), 4);
            SpriteUtil.WorldLabel("placed_label_" + runtime.instance_id,
                pos + new Vector2(0f, 3.3f), device.display_name, 13, AccentGold);
            _interactables.Add(new Interactable
            {
                follow = () => go.transform.position,
                label = () => device.display_name + " [E] 操作",
                action = () => OpenDeviceConsole(device.id),
            });
        }

        // ───────────────────────── 采集 ─────────────────────────
        bool CaptureFiniteSource(string source, string materialId, int capacity)
        {
            string reason;
            if (!_resources.Capture(source, materialId, capacity, out reason))
            { _ui.ShowMessage("无法采集：" + reason); return false; }
            _laboratoryProgression.RecordCorrosionRawMaterials();
            _ui.ShowMessage("采集了 " + ChemistryEngine.DisplayName(materialId) + " ×1，剩余 " + _resources.Remaining(source, materialId, capacity));
            PublishAgentCollected(materialId);
            _state.Save();
            Sfx("capture");
            return true;
        }

        void CollectChemicalResource(string regionId, string materialId)
        {
            string reason;
            if (!_resources.GatherRegion(regionId, materialId, out reason))
            { _ui.ShowMessage("无法采集：" + reason); return; }
            _laboratoryProgression.RecordCorrosionRawMaterials();
            _ui.ShowMessage("采集了 " + ChemistryEngine.DisplayName(materialId) + " ×1，剩余 " + _resources.RegionRemaining(regionId, materialId));
            PublishAgentCollected(materialId);
            _state.Save(); Sfx("capture");
        }

        void RefreshWorldDrops(bool pickup)
        {
            bool changed = false;
            foreach (var drop in _state.worldDrops ?? new WorldDropState[0])
            {
                string reason;
                if (pickup && !_ui.AnyPanelOpen() && _drops.Pickup(drop.instance_id, _player.position, out reason))
                {
                    changed = true;
                    PublishAgentCollected(drop.item_id);
                    _ui.ShowMessage("拾取 " + ChemistryEngine.DisplayName(drop.item_id) + " ×" + drop.amount);
                    continue;
                }
                if (_dropVisuals.ContainsKey(drop.instance_id)) continue;
                var visual = SpriteUtil.RectWorld("drop_" + drop.instance_id, new Vector2(drop.x, drop.y),
                    new Vector2(0.625f, 0.625f), AccentGold, 8);
                var definition = _terrain.Definition(drop.source_block_id);
                if (definition != null)
                {
                    var sprite = ArtLib.Single("tile_" + drop.region_id + "_" + (definition.icon_x + 1).ToString("00") +
                        "_" + (Mathf.Min(3, definition.icon_y) + 1).ToString("00"), 128f);
                    if (sprite != null)
                    {
                        visual.GetComponent<SpriteRenderer>().sprite = sprite;
                        visual.GetComponent<SpriteRenderer>().color = Color.white;
                        visual.transform.localScale = new Vector3(0.625f / sprite.bounds.size.x, 0.625f / sprite.bounds.size.y, 1);
                    }
                }
                _dropVisuals.Add(drop.instance_id, visual);
            }
            var active = new HashSet<string>((_state.worldDrops ?? new WorldDropState[0]).Select(d => d.instance_id));
            foreach (string id in _dropVisuals.Keys.Where(id => !active.Contains(id)).ToArray())
            { Destroy(_dropVisuals[id]); _dropVisuals.Remove(id); }
            if (changed) { _laboratoryProgression.RecordCorrosionRawMaterials(); CapturePlayerPosition(); _state.Save(); Sfx("capture"); }
        }

        void MineTerrainResource(string regionId, string materialId, string displayName)
        {
            var access = _environmentRuntime.MiningAccess(regionId);
            if (!access.allowed)
            {
                if (access.reason == "corrosion_resistance_too_low") PublishCorrosionGateFailure();
                _ui.ShowMessage("无法采集: " + EnvironmentFailureText(access.reason) +
                    "（需要 " + access.required_resistance.ToString("0.00") +
                    "，当前 " + access.actual_resistance.ToString("0.00") + "）");
                return;
            }
            if (materialId == "resource_stone" || materialId == "resource_ore")
            {
                // Bounded compatibility sampler; not the full Godot physical vein generator.
                CaptureFiniteSource("slice_ore:" + regionId + ":" + materialId, materialId, 6);
                return;
            }
            var candidates = _terrain.Cells.Where(p => _terrain.RegionAt(p.Key) == regionId &&
                _terrain.Definition(p.Value).item_id == materialId && _terrain.MiningStatus(p.Key, _player.position).success)
                .OrderBy(p => Vector2.Distance(TerrainRuntimeService.Center(p.Key), _player.position)).ToArray();
            if (candidates.Length == 0) { _ui.ShowMessage("附近无可开采的 " + displayName + "，请移动后挖掘实际地层"); return; }
            var result = _terrain.MineToDrop(candidates[0].Key, _player.position);
            if (result.success) { PublishAgent("MATERIAL_MINED", result.item_id); RefreshWorldDrops(false); _state.Save(); _ui.ShowMessage("已开采，靠近掉落物拾取"); }
        }

        static string EnvironmentFailureText(string reason)
        {
            switch (reason)
            {
                case "heat_resistance_too_low": return "耐热不足";
                case "cold_resistance_too_low": return "耐寒不足";
                case "corrosion_resistance_too_low": return "耐腐蚀不足";
                case "radiation_resistance_too_low": return "抗辐射不足";
                case "electrical_resistance_too_low": return "电气防护不足";
                case "radiation_protection_required": return "辐射防护至少需要 35%";
                case "laboratory_stage_locked": return "实验室需要 Stage 3";
                case "final_core_preparation_required": return "最终核心准备尚未完成";
                default: return reason;
            }
        }

        void RunCorrosionRoute()
        {
            LaboratoryProgressionResult result;
            string successMessage;
            if (_state.CountItem("processed_corrosion_coating") > 0)
            {
                result = _laboratoryProgression.UpgradeCorrosionPickaxe();
                successMessage = "防腐涂层已装配，实验室提升至 Stage 1";
            }
            else if (_state.CountItem("processed_corrosion_base") > 0)
            {
                result = _laboratoryProgression.ProcessCorrosionCoating();
                successMessage = "防腐涂层材料制备完成";
            }
            else
            {
                _laboratoryProgression.RecordCorrosionRawMaterials();
                result = _laboratoryProgression.ProcessCorrosionBase();
                successMessage = "防腐基材制备完成";
            }
            if (result.success)
            {
                var facts = ReadAgentEnvironment();
                if (_state.HasLaboratoryMilestone("corrosion_pickaxe_upgraded"))
                    PublishAgent("CAPABILITY_ACQUIRED", RustJungleAgentBehavior.CapabilityId, evidence: new AgentEvent {
                        behavior_profile = "rust_jungle", capability = RustJungleAgentBehavior.CapabilityId, level = facts.corrosion_resistance_level, hazard_reduced = true });
                else if (facts.resistance_available)
                    PublishAgent("CAPABILITY_AVAILABLE", RustJungleAgentBehavior.CapabilityId, evidence: new AgentEvent {
                        behavior_profile = "rust_jungle", capability = RustJungleAgentBehavior.CapabilityId, available = true });
                _ui.ShowMessage(successMessage);
                _state.Save();
            }
            else
            {
                PublishAgent("EXPERIMENT_FAILED", "corrosion_route", evidence: new AgentEvent { behavior_profile = "rust_jungle", corrosion_relevant = true });
                _ui.ShowMessage("防腐路线尚未完成: " + result.reason);
            }
        }

        void RunMechanicalToolWorkstation()
        {
            MechanicalToolResult result;
            string message;
            if (!_state.HasOwnedTool(MechanicalToolService.ImprovedTool))
            {
                result = _mechanicalTools.Craft(MechanicalToolService.ImprovedRecipe, true);
                message = "已制造改良铁镐；再次操作以装备";
            }
            else if (_state.equippedToolId == MechanicalToolService.BaseTool)
            {
                result = _mechanicalTools.Equip(MechanicalToolService.ImprovedTool);
                message = "已装备改良铁镐：采掘力 2";
            }
            else if (!_state.HasOwnedTool(MechanicalToolService.ReinforcedTool))
            {
                result = _mechanicalTools.Craft(MechanicalToolService.ReinforcedRecipe, true);
                message = "已制造复合强化镐；再次操作以装备";
            }
            else if (_state.equippedToolId != MechanicalToolService.ReinforcedTool)
            {
                result = _mechanicalTools.Equip(MechanicalToolService.ReinforcedTool);
                message = "已装备复合强化镐：采掘力 3";
            }
            else
            {
                _ui.ShowMessage("复合强化镐已装备，当前为最高正式镐具等级");
                return;
            }
            if (result.success)
            {
                _ui.ShowMessage(message);
                _state.Save();
            }
            else _ui.ShowMessage("工具操作失败: " + result.reason);
        }

        // ───────────────────────── 引路人 ─────────────────────────
        void TalkToGuide()
        {
            if (AtStep("demonstrate"))
            {
                if (_state.CountItem("compound_h2") > 0) AdvanceTo("airlock", "演示成功 — 前往东侧气闸离开实验室");
                else _ui.ShowMessage("请先在合成仪中用两份氢合成 H₂");
                return;
            }
            if (AtStep("guide_contact"))
            {
                _ui.ShowDialogue("引路人终端",
                    "……信号恢复。欢迎回来,采样员。\n" +
                    "这颗星球的化学结构正在低语。收集元素,在合成仪中重现反应,\n" +
                    "用反应的语言去回应那些被囚禁的碎片。",
                    null, _ =>
                    {
                        _ui.CloseAllPanels();
                        AdvanceTo("find_exit", "寻找离开实验室的方法(东侧气闸)");
                    });
            }
            else
            {
                _ui.ShowMessage("引路人终端待机中");
            }
        }

        // ───────────────────────── 任务链 ─────────────────────────
        bool AtStep(string stepId)
        {
            return _state.questStep < _quest.steps.Length && _quest.steps[_state.questStep].id == stepId;
        }

        bool AtLeastStep(string stepId)
        {
            for (int i = 0; i < _quest.steps.Length; i++)
                if (_quest.steps[i].id == stepId) return _state.questStep >= i;
            return false;
        }

        void AdvanceTo(string stepId, string message)
        {
            for (int i = 0; i < _quest.steps.Length; i++)
            {
                if (_quest.steps[i].id != stepId) continue;
                if (_state.questStep < i)
                {
                    _state.questStep = i;
                    _ui.ShowToast("目标更新:" + Localization.Tr(_quest.steps[i].text_key));
                    if (!string.IsNullOrEmpty(message)) _ui.ShowMessage(message);
                    _state.Save();
                }
                return;
            }
        }

        // ───────────────────────── 结局 ─────────────────────────
        void ShowCoreChoice()
        {
            var body = new StringBuilder();
            foreach (var line in _endings.core_truth)
                body.AppendLine(Localization.Tr(line));
            body.AppendLine();
            body.AppendLine("———— 现在,做出你的抉择 ————");
            _ui.ShowEnding(body.ToString(),
                new[] { "封存裂核 — 让星球沉眠", "与裂核融合 — 成为星海的意识" },
                choice =>
                {
                    if (!new CoreProgressionService(_state).ChooseEnding(choice.StartsWith("封存") ? "sealed" : "fused"))
                    {
                        _ui.ShowMessage("结局条件未满足或已做出选择");
                        return;
                    }
                    _state.Save();
                    ShowSavedEnding();
                });
        }

        void ShowCorePreparation()
        {
            ShowCorePreparationPage(0);
        }

        void ShowSavedEnding()
        {
            var lines = _state.endingChoice == "sealed" ? _endings.@sealed : _endings.fused;
            _ui.ShowEnding(string.Join("\n", lines.Select(Localization.Tr).ToArray()), null, null);
        }

        void ShowCorePreparationPage(int page)
        {
            var labels = new Dictionary<string, string> {
                { "purification_material", "辐射岩带：净化污染矿物" },
                { "gas_absorption", "吸收塔：处理已储存气体" },
                { "high_purity_material", "工业结晶器：制备高纯晶体" },
                { "mother_liquor_recycled", "回收母液 → 浓缩盐溶液" },
                { "metallurgy", "废弃矿道：氧化铁 + 碳冶金还原" },
                { "electrochemistry", "磁能高原：完成工业电解" },
                { "high_pressure_recipe_completed", "高压反应器：合成氨" },
                { "catalyst_used_correctly", "使用铁催化剂（不消耗）" },
                { "logistics", "向大型工业液体储罐存入液体" },
                { "device_12_operational", "制造并放置高级高压催化反应器" },
                { "device_15_operational", "工作台制作储罐套件，再制造并放置储罐" }
            };
            var pages = new List<string>();
            var rows = _environmentRuntime.FinalCorePreparationStatus().ToArray();
            for (int i = 0; i < rows.Length; i += 4)
                pages.Add(string.Join("\n", rows.Skip(i).Take(4).Select(row =>
                    (row.Value ? "✓ " : "○ ") + labels[row.Key]).ToArray()));
            var text = new StringBuilder("终局条件（核心通行另需 Stage 3 / 防辐射 ≥35%）\n");
            var core = new CoreProgressionService(_state);
            text.AppendLine("\n终局：记忆 " + _state.reconstructedMemories.Distinct().Count() + "/8，主密钥 " +
                _state.calmedShards.Distinct().Count() + "/8，氮反应链 " + core.CompletedSteps + "/5");
            if (core.NextReaction != "") text.AppendLine("下一反应：" + core.NextReaction);
            text.AppendLine("反应顺序：N₂ → NH₃ → NO → NO₂ → HNO₃；按 [C] 使用合成仪。\n高压步骤可在星球核心进行，其余电气/潮湿条件可在磁能高原或盐沼完成。");
            pages.Add(text.ToString());
            _ui.ShowDialogue("最终核心准备 " + (page + 1) + "/" + pages.Count, pages[page],
                new[] { "下一页", "回收母液", "关闭" }, option =>
            {
                if (option == "下一页") { ShowCorePreparationPage((page + 1) % pages.Count); return; }
                _ui.CloseAllPanels();
                if (option == "回收母液") ShowWorldChemistryResult(_worldChemistry.RecycleMotherLiquor());
            });
        }

        // ───────────────────────── HUD ─────────────────────────
        void RefreshHud()
        {
            var stepTexts = _quest.steps.Select(st => Localization.Tr(st.text_key)).ToArray();
            _ui.SetQuest(_state.questStep, stepTexts);
            _ui.SetStatsText(_state.energy, _state.hp, _state.radiation, _state.radiationProtection);

            var def = _regionById.ContainsKey(_state.currentRegion)
                ? _regionById[_state.currentRegion] : _regionById[_surfaceOrder[0]];
            float frac;
            int sIdx = _surfaceOrder.IndexOf(def.id);
            if (sIdx >= 0) frac = sIdx / (float)Mathf.Max(1, _surfaceOrder.Count - 1);
            else
            {
                int uIdx = _undergroundOrder.IndexOf(def.id);
                frac = Mathf.Clamp01(1f - (uIdx + 1) / (float)(_undergroundOrder.Count + 1));
            }
            _ui.SetRegion(Localization.Tr(def.name_key), frac, def.id);
            _ui.SetReturnLabVisible(def.id != "prologue_lab");

            int total = 0;
            var owned = _state.OwnedMaterialIds();
            foreach (var id in owned) total += _state.CountItem(id);
            _ui.SetHotbar(total);
        }

        // ───────────────────────── 数据加载 ─────────────────────────
        ChemistryDatabase LoadChemistry()
        {
            var asset = Resources.Load<TextAsset>("Data/chemistry");
            return asset != null
                ? JsonUtility.FromJson<ChemistryDatabase>(asset.text)
                : JsonUtility.FromJson<ChemistryDatabase>(System.IO.File.ReadAllText(
                    Application.dataPath + "/Resources/Data/chemistry.json"));
        }

        LocalizationDatabase LoadLocalization()
        {
            var asset = Resources.Load<TextAsset>("Data/localization_zh");
            return asset != null
                ? JsonUtility.FromJson<LocalizationDatabase>(asset.text)
                : JsonUtility.FromJson<LocalizationDatabase>(System.IO.File.ReadAllText(
                    Application.dataPath + "/Resources/Data/localization_zh.json"));
        }
    }
}
