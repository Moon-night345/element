// 元素星海·裂核 — uGUI 全程序化构建(HUD/合成/对话/记忆/结局/帮助)
// 只用 legacy uGUI(Text/Image/Button),不依赖 TextMeshPro。
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ElementalStarSea.Game
{
    public partial class GameUI : MonoBehaviour
    {
        Canvas _canvas;
        Text _objective, _inventory, _hint, _toast, _message, _agentDialogue;
        GameObject _synthesisPanel, _dialoguePanel, _memoryPanel, _endingPanel, _helpPanel, _devicePanel;
        Text _synResult, _synDiscovered, _dlgTitle, _dlgBody;
        Text _memTitle, _memBody;
        Transform _dlgOptions, _synOwned, _synSlots;
        Transform _deviceList, _deviceActions;
        Text _deviceBody;
        string _selectedDeviceId = "";
        Button _synCombine;
        Image _hpFill, _enFill, _radFill;
        Text _radText;
        // 菜单流程
        GameObject _menuRoot;
        string _menuScreen = "";   // title / world / loading
        Image _menuArt, _loadingFill;
        Text _loadingPct;
        // 原版布局 HUD
        Text _regionText, _regionDetailText, _statsText, _questText;
        readonly Image[] _hotbarSlots = new Image[8];
        readonly Text[] _hotbarCounts = new Text[8];
        GameObject _backpackPanel;
        GameObject _pausePanel;
        Text _backpackBody;
        // 对话打字机
        string _dlgFull = "";
        int _dlgRevealed;
        bool _dlgTyping;
        Text _dlgProgress;
        readonly List<string> _selected = new List<string>();
        Func<Data.ReactionExecutionResult> _onCombine;
        Action<string> _onOptionPicked;
        float _toastUntil, _hintUntil;

        static readonly Color PanelBg = new Color(0.05f, 0.03f, 0.02f, 0.94f);
        static readonly Color SlotBg = new Color(0.16f, 0.11f, 0.07f, 1f);
        static readonly Color Accent = new Color(0.98f, 0.78f, 0.42f);   // 暖金(与原版 UI 一致)

        void Awake()
        {
            _canvas = new GameObject("Canvas").AddComponent<Canvas>();
            _canvas.transform.SetParent(transform, false);
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            var scaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            _canvas.gameObject.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
            BuildSynthesis();
            BuildDialogue();
            BuildMemory();
            BuildEnding();
            BuildHelp();
            BuildDevices();
            HideAllPanels();
        }

        /// <summary>进游戏时才构建 HUD(标题菜单阶段保持画面干净)。</summary>
        public void SetupHud()
        {
            if (_hpFill != null) return;   // 已构建
            BuildHud();
            BuildVignette();
            _agentDialogue = MakeText("AgentDialogue", new Vector2(700, 78), TextAnchor.MiddleCenter, 19,
                new Color(0.72f, 0.94f, 1f));
            Place(_agentDialogue.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 240), new Vector2(700, 78));
            _agentDialogue.raycastTarget = false;
        }

        public void SetAgentDialogue(string text, bool visible)
        {
            if (_agentDialogue == null) return;
            _agentDialogue.text = visible && !string.IsNullOrEmpty(text) ? "引路人：" + text : "";
        }

        // ───────────────────────── 暗角 ─────────────────────────
        void BuildVignette()
        {
            var go = new GameObject("Vignette");
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = TexGen.SpriteOf(TexGen.Vignette(), 128f);
            img.raycastTarget = false;
            go.transform.SetAsFirstSibling(); // 暗角只压世界画面，不能覆盖 HUD 文字与按钮
        }

        // ───────────────────────── HUD ─────────────────────────
        static readonly Color HudBg = new Color(0.025f, 0.02f, 0.018f, 0.78f);

        /// <summary>规范摆放:anchor 是标准锚点((0,1)=左上,(1,1)=右上,(0,0)=左下),
        /// pos 是相对锚点的像素偏移,pivot 跟随 anchor 方向。</summary>
        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        GameObject Backdrop(Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("bg");
            go.transform.SetParent(_canvas.transform, false);
            Place(go.AddComponent<RectTransform>(), anchor, pos, size);
            var img = go.AddComponent<Image>();
            img.color = HudBg;
            img.raycastTarget = false;   // 背景不得拦截按钮点击
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.42f, 0.2f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            return go;
        }

        void BuildHud()
        {
            // 左上:地图名 + 进度条(小地图示意)
            Backdrop(new Vector2(0f, 1f), new Vector2(16, -12), new Vector2(250, 110));
            _regionText = MakeText("Region", new Vector2(220, 30),
                                   TextAnchor.UpperLeft, 16, Accent);
            _regionText.text = "地图";
            Place(_regionText.rectTransform, new Vector2(0f, 1f), new Vector2(28, -18), new Vector2(220, 28));
            var strip = new GameObject("ministrip");
            strip.transform.SetParent(_canvas.transform, false);
            Place(strip.AddComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(34, -50), new Vector2(214, 24));
            _minimapImg = strip.AddComponent<Image>();
            _minimapImg.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
            _minimapImg.raycastTarget = false;
            _minimapDot = MakeText("Dot", new Vector2(20, 20), TextAnchor.MiddleCenter, 12, Accent);
            _minimapDot.text = "●";
            Place(_minimapDot.rectTransform, new Vector2(0f, 1f), new Vector2(40, -56), new Vector2(20, 20));
            _regionDetailText = MakeText("RegionDetail", new Vector2(214, 22),
                                         TextAnchor.UpperLeft, 12, new Color(0.82f, 0.78f, 0.7f));
            Place(_regionDetailText.rectTransform, new Vector2(0f, 1f), new Vector2(34, -80), new Vector2(214, 22));

            // 右上:合成能量 / HP / RAD 文本(原版样式)
            Backdrop(new Vector2(1f, 1f), new Vector2(-16, -12), new Vector2(290, 100));
            _statsText = MakeText("Stats", new Vector2(260, 80),
                                  TextAnchor.UpperRight, 16, new Color(0.92f, 0.88f, 0.8f));
            Place(_statsText.rectTransform, new Vector2(1f, 1f), new Vector2(-30, -22), new Vector2(260, 84));

            // 左下:任务目标(✓/•/○)
            Backdrop(new Vector2(0f, 0f), new Vector2(16, 18), new Vector2(360, 168));
            var qTitle = MakeText("QuestTitle", new Vector2(330, 30), TextAnchor.UpperLeft, 17, Accent);
            qTitle.text = "任务目标";
            Place(qTitle.rectTransform, new Vector2(0f, 0f), new Vector2(26, 150), new Vector2(330, 28));
            _questText = MakeText("Quest", new Vector2(330, 126), TextAnchor.UpperLeft, 14);
            Place(_questText.rectTransform, new Vector2(0f, 0f), new Vector2(26, 22), new Vector2(330, 124));

            // 底部:快捷栏 1-8 + 四个按钮
            for (int i = 0; i < 8; i++)
            {
                var slot = new GameObject("slot" + (i + 1));
                slot.transform.SetParent(_canvas.transform, false);
                Place(slot.AddComponent<RectTransform>(), new Vector2(0.5f, 0f),
                      new Vector2(-232 + i * 56, 18), new Vector2(50, 50));
                var simg2 = slot.AddComponent<Image>();
                simg2.color = i == 0 ? new Color(0.24f, 0.16f, 0.08f, 0.92f) : new Color(0.1f, 0.07f, 0.05f, 0.85f);
                simg2.raycastTarget = false;
                _hotbarSlots[i] = simg2;
                var num = MakeText("n" + i, new Vector2(44, 44), TextAnchor.UpperLeft, 13,
                                   i == 0 ? Accent : new Color(0.7f, 0.65f, 0.55f));
                num.text = (i + 1).ToString();
                num.transform.SetParent(slot.transform, false);
                var nrt = num.rectTransform;
                nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(5, -3); nrt.offsetMax = new Vector2(-5, 3);
                var cnt = MakeText("c" + i, new Vector2(44, 20), TextAnchor.LowerRight, 12,
                                   new Color(0.85f, 0.8f, 0.7f));
                cnt.transform.SetParent(slot.transform, false);
                var crt = cnt.rectTransform;
                crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
                crt.offsetMin = new Vector2(5, 4); crt.offsetMax = new Vector2(-5, -4);
                _hotbarCounts[i] = cnt;
            }
            MakeHudButton("ESC 暂停", 250, TogglePause);
            MakeHudButton("TAB 背包", 360, () => { if (OnBackpack != null) OnBackpack(); });
            MakeHudButton("M 地图", 470, () => { if (OnMap != null) OnMap(); });
            MakeHudButton("C 合成", 580, () => { if (OnSynthesisKey != null) OnSynthesisKey(); });

            _message = MakeText("Message", new Vector2(700, 36),
                                TextAnchor.LowerLeft, 15, new Color(0.9f, 0.85f, 0.6f));
            Place(_message.rectTransform, new Vector2(0f, 0f), new Vector2(26, 348), new Vector2(700, 36));

            _hint = MakeText("Hint", new Vector2(700, 40),
                             TextAnchor.LowerCenter, 19, Accent);
            Place(_hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 292), new Vector2(700, 40));

            _toast = MakeText("Toast", new Vector2(800, 60),
                              TextAnchor.UpperCenter, 27, Accent);
            Place(_toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -96), new Vector2(800, 60));
        }

        void MakeHudButton(string label, float x, Action onClick)
        {
            var go = new GameObject("hudbtn_" + label);
            go.transform.SetParent(_canvas.transform, false);
            Place(go.AddComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(x, 18),
                  new Vector2(100, 50));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.13f, 0.09f, 0.05f, 0.92f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
            var t = MakeText("l", new Vector2(140, 46), TextAnchor.MiddleCenter, 15,
                             new Color(0.95f, 0.85f, 0.65f));
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.text = label;
        }

        Text _minimapDot;
        Image _minimapImg;

        /// <summary>原版式状态:合成能量/HP/RAD 三行。</summary>
        public void SetStatsText(float energy, float hp, float radiation, float radiationProtection = 0f)
        {
            if (!_statsText) return;
            _statsText.text = string.Format("合成能量 {0:F0}/100\nHP {1:F0}\nRAD {2:F0}  防护 {3:F0}%",
                                            energy, hp, radiation, radiationProtection * 100f);
        }

        /// <summary>区域名 + 小地图圆点位置(0..1) + 缩略图。</summary>
        public void SetRegion(string regionName, float fraction, string regionId)
        {
            if (_regionText) _regionText.text = "地图 " + regionName;
            if (_minimapImg != null)
            {
                var tex = ArtLib.Sheet("bg_" + regionId);
                if (tex != null)
                    _minimapImg.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                       new Vector2(0.5f, 0.5f), tex.width / 214f);
            }
            if (_minimapDot)
            {
                var pd = _minimapDot.rectTransform.anchoredPosition;
                _minimapDot.rectTransform.anchoredPosition =
                    new Vector2(Mathf.Round(20f + Mathf.Clamp01(fraction) * 188f), pd.y);
            }
            if (_regionDetailText)
                _regionDetailText.text = regionId == "prologue_lab" ? "未标明区域" : string.Empty;
        }

        /// <summary>任务目标:✓已完成 / • 当前 / ○ 待办。</summary>
        public void SetQuest(int currentStep, string[] stepTexts)
        {
            if (!_questText) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(currentStep > 0
                ? string.Format("<color=#8fd8a0>✓ 已完成 {0} 项</color>", currentStep)
                : "<color=#8a8a8a>○ 尚未完成任何目标</color>");
            for (int i = Mathf.Max(0, currentStep - 1); i < Mathf.Min(stepTexts.Length, currentStep + 4); i++)
            {
                string mark = i < currentStep ? "<color=#8fd8a0>✓</color>"
                    : (i == currentStep ? "<color=#e8c878>•</color>" : "<color=#8a8a8a>○</color>");
                sb.AppendLine(mark + " " + stepTexts[i]);
            }
            _questText.text = sb.ToString();
        }

        /// <summary>快捷栏第 1 格显示持有数。</summary>
        public void SetHotbar(int firstSlotCount)
        {
            if (_hotbarCounts[0]) _hotbarCounts[0].text = firstSlotCount > 0 ? "×" + firstSlotCount : "";
        }

        /// <summary>一条状态条(右上簇内):返回填充 Image。</summary>
        Image MakeBar(string name, float y, Color c, string label)
        {
            var lab = MakeText("lab_" + name, new Vector2(58, 22),
                               TextAnchor.MiddleRight, 14, new Color(0.85f, 0.88f, 0.92f));
            lab.text = label;
            Place(lab.rectTransform, new Vector2(1f, 1f), new Vector2(-330, y), new Vector2(58, 22));

            var bg = new GameObject("bar_" + name);
            bg.transform.SetParent(_canvas.transform, false);
            Place(bg.AddComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-160, y),
                  new Vector2(170, 18));
            var bimg = bg.AddComponent<Image>();
            bimg.color = new Color(0f, 0f, 0f, 0.85f);
            bimg.raycastTarget = false;
            var fillGo = new GameObject("fill");
            fillGo.transform.SetParent(bg.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(1f, 1f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            var fimg = fillGo.AddComponent<Image>();
            fimg.color = c;
            fimg.raycastTarget = false;
            return fimg;
        }

        Text MakeText(string name, Vector2 size, TextAnchor align,
                      int fontSize, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = SpriteUtil.UiFont();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        // ───────────────────────── 合成面板 ─────────────────────────
        void BuildSynthesis()
        {
            // 原版布局:全屏半透明遮罩 + 居中标题 + 左对齐配方大字 + 右下 退出/确认合成
            _synthesisPanel = MakePanel("SynthesisPanel", new Vector2(1040, 620));

            var title = MakePanelText(_synthesisPanel, "Title", new Vector2(0, 265), 960, 46,
                                      TextAnchor.MiddleCenter, 28, Color.white);
            title.text = "合成仪";

            _synPreview = MakePanelText(_synthesisPanel, "Preview", new Vector2(-255, 90), 440, 260,
                                        TextAnchor.UpperLeft, 19);
            MakePanelText(_synthesisPanel, "MaterialsTitle", new Vector2(245, 220), 490, 30, TextAnchor.MiddleLeft, 17, Accent).text = "可用材料 · 点击添加，滚轮浏览";

            _synOwned = MakeScrollGrid(_synthesisPanel, "Owned", new Vector2(245, 40), 490, 320);
            var grid = _synOwned.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(156, 48);
            grid.constraintCount = 3;

            MakePanelText(_synthesisPanel, "SelectedTitle", new Vector2(0, -136), 960, 24, TextAnchor.MiddleLeft, 16, Accent).text = "已选材料 · 点击移除";
            _synSlots = MakeGrid(_synthesisPanel, "Slots", new Vector2(0, -184), 960, 48);
            var slotGrid = _synSlots.GetComponent<GridLayoutGroup>();
            slotGrid.cellSize = new Vector2(110, 44);
            slotGrid.spacing = new Vector2(10, 8);
            slotGrid.constraintCount = 8;

            var close = MakePanelButton(_synthesisPanel, "Exit", new Vector2(100, -260), 180, 48, "返回游戏");
            close.onClick.AddListener(CloseAllPanels);
            _synCombine = MakePanelButton(_synthesisPanel, "Combine", new Vector2(340, -260), 240, 48, "确认合成");
            _synCombine.onClick.AddListener(OnCombineClicked);

            _synResult = MakePanelText(_synthesisPanel, "Result", new Vector2(-255, -76), 440, 50,
                                       TextAnchor.UpperLeft, 17, new Color(0.7f, 1f, 0.75f));
            _synDiscovered = MakePanelText(_synthesisPanel, "Discovered", new Vector2(-255, -260), 440, 30,
                                           TextAnchor.UpperLeft, 14, new Color(0.75f, 0.75f, 0.8f));
        }

        Text _synPreview;

        Transform MakeGrid(GameObject parent, string name, Vector2 center, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = center;
            rt.sizeDelta = new Vector2(w, h);
            var grid = go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(132, 42);
            grid.spacing = new Vector2(6, 6);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            return go.transform;
        }

        /// <summary>选中变化时刷新配方预览(投入/反应条件/能量成本/产物/语言词)。</summary>
        void RefreshSynthPreview()
        {
            if (_synPreview == null) return;
            var counts = new System.Text.StringBuilder();
            var grouped = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var id in _selected)
                grouped[id] = (grouped.TryGetValue(id, out var c) ? c : 0) + 1;
            if (grouped.Count == 0) counts.Append("—");
            else
            {
                bool first = true;
                foreach (var kv in grouped)
                {
                    if (!first) counts.Append(" + ");
                    var m = Data.ChemistryEngine.Material(kv.Key);
                    counts.Append((m != null ? m.symbol : kv.Key) + "×" + kv.Value);
                    first = false;
                }
            }

            var recipe = Data.ChemistryEngine.MatchExact(_selected);
            string products = "?";
            string lang = "?";
            if (recipe != null)
            {
                var pl = new System.Text.StringBuilder();
                bool first = true;
                foreach (var q in recipe.products)
                {
                    if (!first) pl.Append(" + ");
                    pl.Append(Data.ChemistryEngine.DisplayName(q.id) + "×" + q.n);
                    first = false;
                }
                products = pl.ToString();
                if (!string.IsNullOrEmpty(recipe.language))
                    lang = Data.Localization.Tr("content.codex." + recipe.language + ".name");
            }
            _synPreview.text =
                "投 入   " + counts.ToString() + "\n" +
                "反应条件   " + (recipe == null ? "?" : RequiredConditions(recipe)) + "\n" +
                "能量成本   " + (recipe == null ? "?" : Data.ChemistryEngine.EnergyCost(recipe).ToString("0.0")) + "\n" +
                "产 物   " + products + "\n" +
                "语言词   " + lang;
        }
        // ───────────────────────── 对话面板 ─────────────────────────
        void BuildDialogue()
        {
            // 原版样式:底部居中宽条,标题在左上,正文打字机,选项在右下
            // 原版样式:无底板,大字居中于屏幕下 1/4,选项按钮在正文下方居中
            _dialoguePanel = MakePanel("DialoguePanel", new Vector2(1040, 360));
            var rt = _dialoguePanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 108f);
            rt.sizeDelta = new Vector2(1040, 360);

            _dlgTitle = MakePanelText(_dialoguePanel, "Title", new Vector2(0, 136), 960, 36,
                                      TextAnchor.MiddleCenter, 18, Accent);
            _dlgBody = MakeScrollText(_dialoguePanel, "Body", new Vector2(0, 40), 960, 136, 19);
            _dlgProgress = MakePanelText(_dialoguePanel, "Progress", new Vector2(0, -48), 300, 24,
                                         TextAnchor.MiddleCenter, 15, new Color(0.75f, 0.7f, 0.6f));
            _dlgOptions = MakeScrollGrid(_dialoguePanel, "Options", new Vector2(0, -112), 960, 104);
            var dlg = _dlgOptions.GetComponent<GridLayoutGroup>();
            dlg.cellSize = new Vector2(474, 46);
            dlg.spacing = new Vector2(12, 10);
            dlg.constraintCount = 2;
        }

        // ───────────────────────── 记忆/结局/帮助 ─────────────────────────
        void BuildMemory()
        {
            _memoryPanel = MakePanel("MemoryPanel", new Vector2(780, 500));
            _memTitle = MakePanelText(_memoryPanel, "Title", new Vector2(0, 215), 700, 40,
                                      TextAnchor.UpperCenter, 21, new Color(0.8f, 0.6f, 1f));
            _memBody = MakeScrollText(_memoryPanel, "Body", new Vector2(0, -10), 700, 340, 17);
            var close = MakePanelButton(_memoryPanel, "Close", new Vector2(0, -215), 180, 44, "关闭(E)");
            close.onClick.AddListener(CloseAllPanels);
        }

        void BuildEnding()
        {
            _endingPanel = MakePanel("EndingPanel", new Vector2(860, 580));
        }

        void BuildHelp()
        {
            _helpPanel = MakePanel("HelpPanel", new Vector2(640, 420));
            var body = MakePanelText(_helpPanel, "Body", new Vector2(0, 10), 560, 320,
                                     TextAnchor.UpperLeft, 16);
            body.text = "操作说明\n" +
                        "A/D / 左右方向键 — 移动      空格 — 跳跃\n" +
                        "C — 打开合成仪              D — 正式设备控制台\n" +
                        "TAB — 背包      M — 地图      H — 本说明\n" +
                        "按住左键 — 开采    右键 — 放置    B — 切换建造方块\n" +
                        "点击近身敌人 — 攻击      ESC — 暂停/关闭面板\n" +
                        "合成氢气后，空中按住空格可短时悬浮并消耗能量。\n\n" +
                        "玩法:合成仪中按精确配比选择物质合成(如 2 个氢 → H₂);\n" +
                        "用反应语言回应碎片的谜题;在记忆终端重构记忆;\n" +
                        "收集足够记忆后前往星核,做出最终抉择。";
            var close = MakePanelButton(_helpPanel, "Close", new Vector2(0, -165), 160, 42, "关闭(H)");
            close.onClick.AddListener(CloseAllPanels);
        }

        void BuildDevices()
        {
            _devicePanel = MakePanel("DevicePanel", new Vector2(1120, 650));
            var title = MakePanelText(_devicePanel, "Title", new Vector2(0, 288), 1040, 40,
                                      TextAnchor.MiddleCenter, 22, Accent);
            title.text = "实验设备  ·  滚轮浏览列表";
            _deviceList = MakeScrollGrid(_devicePanel, "DeviceList", new Vector2(-310, 14), 420, 480);
            var listGrid = _deviceList.GetComponent<GridLayoutGroup>();
            listGrid.cellSize = new Vector2(404, 50);
            listGrid.constraintCount = 1;
            _deviceBody = MakeScrollText(_devicePanel, "Body", new Vector2(225, 190), 560, 140, 16);
            _deviceActions = MakeScrollGrid(_devicePanel, "Actions", new Vector2(225, -94), 560, 312);
            var actionGrid = _deviceActions.GetComponent<GridLayoutGroup>();
            actionGrid.cellSize = new Vector2(270, 56);
            actionGrid.constraintCount = 2;
            var close = MakePanelButton(_devicePanel, "Close", new Vector2(0, -292), 180, 42, "关闭(D)");
            close.onClick.AddListener(CloseAllPanels);
        }

        // ───────────────────────── 面板基件 ─────────────────────────
        Transform MakeScrollGrid(GameObject panel, string name, Vector2 position, float width, float height)
        {
            var viewport = new GameObject(name + "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel.transform, false);
            var vr = viewport.GetComponent<RectTransform>(); vr.anchoredPosition = position; vr.sizeDelta = new Vector2(width, height);
            viewport.GetComponent<Image>().color = new Color(.025f, .025f, .025f, .4f);
            var content = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetComponent<RectTransform>(); cr.anchorMin = new Vector2(0, 1); cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(.5f, 1); cr.sizeDelta = Vector2.zero;
            var grid = content.GetComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2; grid.spacing = new Vector2(10, 10); grid.padding = new RectOffset(0, 0, 2, 2);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = vr; scroll.content = cr; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            return cr;
        }

        GameObject MakePanel(string name, Vector2 size)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(_canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            var img = panel.AddComponent<Image>();
            img.color = PanelBg;
            var outline = panel.AddComponent<Outline>(); outline.effectColor = new Color(.64f, .44f, .24f, .8f); outline.effectDistance = new Vector2(1, -1);
            return panel;
        }

        Text MakePanelText(GameObject panel, string name, Vector2 pos, float w, float h,
                           TextAnchor align, int size, Color? color = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);
            var t = go.AddComponent<Text>();
            t.font = SpriteUtil.UiFont();
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            return t;
        }

        Button MakePanelButton(GameObject panel, string name, Vector2 pos, float w, float h, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panel.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);
            var img = go.AddComponent<Image>();
            img.color = SlotBg;
            var btn = go.AddComponent<Button>();
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.sizeDelta = Vector2.zero;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            var t = txtGo.AddComponent<Text>();
            t.font = SpriteUtil.UiFont();
            t.fontSize = 17;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            return btn;
        }

        Button MakeGridButton(Transform grid, string label, Action onClick, Color? bg = null)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(grid, false);
            var img = go.AddComponent<Image>();
            img.color = bg ?? SlotBg;
            var btn = go.AddComponent<Button>();
            var txtGo = new GameObject("Label");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.sizeDelta = Vector2.zero;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(4, 0);
            trt.offsetMax = new Vector2(-4, 0);
            var t = txtGo.AddComponent<Text>();
            t.font = SpriteUtil.UiFont();
            t.fontSize = 15;
            t.alignment = TextAnchor.MiddleCenter;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = 15;
            t.color = Color.white;
            t.text = label;
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        // ───────────────────────── HUD 更新 ─────────────────────────
        public void SetMessage(string text) { if (_message) _message.text = text; }
        public void ShowMessage(string text) { if (_message) _message.text = "» " + text; }

        public void ShowToast(string text)
        {
            if (!_toast) return;
            _toast.transform.SetAsLastSibling();
            _toast.text = text;
            _toastUntil = Time.unscaledTime + 3f;
        }

        public void ShowHint(string text)
        {
            if (!_hint) return;
            _hint.text = text;
            _hintUntil = Time.unscaledTime + 0.3f;
        }

        public bool AnyPanelOpen()
        {
            return (_mapPanel && _mapPanel.activeSelf) || (_pausePanel && _pausePanel.activeSelf) || (_backpackPanel && _backpackPanel.activeSelf) || (_synthesisPanel && _synthesisPanel.activeSelf) ||
                   (_dialoguePanel && _dialoguePanel.activeSelf) ||
                   (_memoryPanel && _memoryPanel.activeSelf) ||
                   (_endingPanel && _endingPanel.activeSelf) ||
                   (_helpPanel && _helpPanel.activeSelf) ||
                   (_devicePanel && _devicePanel.activeSelf);
        }

        public void TogglePause()
        {
            bool wasOpen = _pausePanel != null && _pausePanel.activeSelf;
            HideAllPanels(); if (wasOpen) return;
            if (_pausePanel == null)
            {
                _pausePanel = MakePanel("PausePanel", new Vector2(600, 380));
                MakePanelText(_pausePanel, "Title", new Vector2(0, 132), 500, 44, TextAnchor.MiddleCenter, 26, Accent).text = "暂 停";
                MakePanelButton(_pausePanel, "Resume", new Vector2(0, 48), 360, 52, "继续游戏").onClick.AddListener(CloseAllPanels);
                MakePanelButton(_pausePanel, "Help", new Vector2(0, -22), 360, 52, "操作说明").onClick.AddListener(ToggleHelp);
                MakePanelButton(_pausePanel, "Title", new Vector2(0, -92), 360, 52, "保存并返回标题").onClick.AddListener(() => { if (OnBackToTitle != null) OnBackToTitle(); });
            }
            _pausePanel.transform.SetAsLastSibling(); _pausePanel.SetActive(true);
        }

        public void CloseAllPanels()
        {
            HideAllPanels();
        }

        public void ToggleHelp()
        {
            bool show = !_helpPanel.activeSelf;
            HideAllPanels();
            _helpPanel.SetActive(show);
        }

        void HideAllPanels()
        {
            foreach (var panel in new[] { _synthesisPanel, _dialoguePanel, _memoryPanel, _endingPanel, _helpPanel, _devicePanel, _backpackPanel, _pausePanel, _mapPanel })
                if (panel != null) panel.transform.SetAsLastSibling();
            if (_synthesisPanel) _synthesisPanel.SetActive(false);
            if (_dialoguePanel) _dialoguePanel.SetActive(false);
            if (_memoryPanel) _memoryPanel.SetActive(false);
            if (_endingPanel) _endingPanel.SetActive(false);
            if (_helpPanel) _helpPanel.SetActive(false);
            if (_devicePanel) _devicePanel.SetActive(false);
            if (_backpackPanel) _backpackPanel.SetActive(false);
            if (_pausePanel) _pausePanel.SetActive(false);
            if (_mapPanel) _mapPanel.SetActive(false);
        }

        void Update()
        {
            TickMenuKeys();
            // 菜单点击诊断:打印 UI 射线命中链
            if (_menuRoot != null && Input.GetMouseButtonDown(0))
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es != null)
                {
                    var ped = new UnityEngine.EventSystems.PointerEventData(es);
                    ped.position = Input.mousePosition;
                    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    es.RaycastAll(ped, hits);
                    Debug.Log("[MenuUI] 点击 " + Input.mousePosition + " 命中 " + hits.Count + " 层: " +
                              string.Join(" -> ", hits.ConvertAll(h => h.gameObject.name).ToArray()));
                }
                else Debug.Log("[MenuUI] 无 EventSystem!");
            }
            TickTypewriter();
            if (_toast && Time.unscaledTime > _toastUntil && _toast.text.Length > 0) _toast.text = "";
            if (_hint && Time.unscaledTime > _hintUntil && _hint.text.Length > 0) _hint.text = "";
        }

        // ───────────────────────── 合成面板 API ─────────────────────────
        /// <summary>刷新合成仪:owned=可选中物质,selected=已选,discovered=已发现反应数。</summary>
        public void ShowSynthesis(List<string> ownedIds, List<string> selected, int discovered, int total,
                                  Func<Data.ReactionExecutionResult> onCombine)
        {
            HideAllPanels();
            _synthesisPanel.SetActive(true);
            _onCombine = onCombine;
            _selected.Clear();
            _selected.AddRange(selected);
            _lastOwned.Clear();
            _lastOwned.AddRange(ownedIds);
            ClearChildren(_synOwned);
            foreach (var id in ownedIds)
            {
                var captured = id;
                var m = Data.ChemistryEngine.Material(captured);
                MakeGridButton(_synOwned, m != null ? m.symbol : captured, () => ToggleMaterial(captured),
                    _selected.Contains(captured) ? new Color(0.25f, 0.5f, 0.6f) : (Color?)null);
            }
            RefreshSlots();
            RefreshSynthPreview();
            if (_synResult) _synResult.text = "";
            if (_synDiscovered)
                _synDiscovered.text = string.Format("已发现反应 {0}/{1}", discovered, total);
        }

        void ToggleMaterial(string id)
        {
            if (_selected.Count < 8) _selected.Add(id);
            RefreshSynthesisGrids();
            RefreshSynthPreview();
        }

        void RefreshSynthesisGrids()
        {
            ClearChildren(_synOwned);
            foreach (var id in _lastOwned)
            {
                var captured = id;
                var m = Data.ChemistryEngine.Material(captured);
                MakeGridButton(_synOwned, m != null ? m.symbol : captured, () => ToggleMaterial(captured),
                    _selected.Contains(id) ? new Color(0.25f, 0.5f, 0.6f) : (Color?)null);
            }
            RefreshSlots();
        }

        void RefreshSlots()
        {
            ClearChildren(_synSlots);
            for (int i = 0; i < 8; i++)
            {
                int capturedIndex = i;
                var label = i < _selected.Count
                    ? Data.ChemistryEngine.Material(_selected[i]).symbol : "—";
                MakeGridButton(_synSlots, label, i < _selected.Count ? (Action)(() =>
                {
                    if (capturedIndex < _selected.Count) _selected.RemoveAt(capturedIndex);
                    RefreshSynthesisGrids();
                    RefreshSynthPreview();
                }) : null, new Color(0.1f, 0.13f, 0.2f));
            }
        }

        readonly List<string> _lastOwned = new List<string>();

        void OnCombineClicked()
        {
            if (_onCombine == null) return;
            var result = _onCombine();
            bool ok = result != null && result.success;
            if (_synResult)
                _synResult.text = ok ? "合成成功!" : ReactionFailureText(result);
            if (ok && _synDiscovered)
                _synDiscovered.text = string.Format("已发现反应(见编年史)");
        }

        public List<string> SelectedMaterials { get { return new List<string>(_selected); } }

        static string RequiredConditions(Data.ReactionRecipe recipe)
        {
            var required = new List<string>(recipe.tags ?? new string[0]);
            if (recipe.any_tags != null && recipe.any_tags.Length > 0)
                required.Add("(" + string.Join(" 或 ", recipe.any_tags) + ")");
            return required.Count == 0 ? "合成台" : string.Join(" + ", required.ToArray());
        }

        static string ReactionFailureText(Data.ReactionExecutionResult result)
        {
            if (result == null || result.failure == Data.ReactionFailure.InvalidDefinition)
                return "没有匹配的反应配方(需要精确配比)";
            switch (result.failure)
            {
                case Data.ReactionFailure.MissingReactant: return "反应物不足";
                case Data.ReactionFailure.MissingEnvironment:
                    return "缺少环境条件: " + string.Join(", ", result.missing_conditions ?? new string[0]);
                case Data.ReactionFailure.InsufficientEnergy: return "合成能量不足";
                default: return "反应提交失败,库存未改变";
            }
        }

        // ───────────────────────── 对话 API ─────────────────────────
        List<Action> _optionActions = new List<Action>();

        /// <summary>显示对话:options 为空时只显示继续按钮(onOption 收到 "-1")。</summary>
        public void ShowDialogue(string title, string body, string[] options, Action<string> onOption)
        {
            HideAllPanels();
            _dialoguePanel.SetActive(true);
            _dlgTitle.text = title;
            _dlgFull = body;
            _dlgRevealed = 0;
            _dlgTyping = true;
            _dlgBody.text = "";
            if (_dlgProgress) _dlgProgress.text = options == null || options.Length == 0 ? "(E 继续)" : "";
            _onOptionPicked = onOption;
            _optionActions.Clear();
            ClearChildren(_dlgOptions);
            if (options == null || options.Length == 0)
            {
                Action advance = () => PickOption("-1");
                _optionActions.Add(advance);
                MakeGridButton(_dlgOptions, "继 续 (E)", advance, new Color(0.28f, 0.19f, 0.09f));
            }
            else
            {
                foreach (var opt in options)
                {
                    var captured = opt;
                    Action pick = () => PickOption(captured);
                    _optionActions.Add(pick);
                    MakeGridButton(_dlgOptions, opt, pick);
                }
            }
        }

        void TickTypewriter()
        {
            if (!_dlgTyping || string.IsNullOrEmpty(_dlgFull)) return;
            if (_dlgRevealed < _dlgFull.Length)
            {
                _dlgRevealed = Mathf.Min(_dlgFull.Length, _dlgRevealed + (int)(Time.unscaledDeltaTime * 46f) + 1);
                _dlgBody.text = _dlgFull.Substring(0, _dlgRevealed);
            }
            else _dlgTyping = false;
        }

        void PickOption(string value)
        {
            if (_onOptionPicked != null) _onOptionPicked(value);
        }

        /// <summary>
        /// 面板打开时按 E:对话面板触发第一个选项(继续),记忆/帮助面板关闭,
        /// 结局面板不响应(必须做抉择),合成面板关闭。
        /// </summary>
        public void AdvanceOrClose()
        {
            if (_dialoguePanel.activeSelf)
            {
                if (_dlgTyping) { _dlgTyping = false; _dlgRevealed = _dlgFull.Length; _dlgBody.text = _dlgFull; return; }
                if (_optionActions.Count > 0)
                {
                    var first = _optionActions[0];
                    first();
                }
            }
            else if (_endingPanel.activeSelf)
            {
                // 结局必须用按钮抉择
            }
            else
            {
                CloseAllPanels();
            }
        }

        // ───────────────────────── 记忆/结局 API ─────────────────────────
        public void ShowMemory(string title, string body)
        {
            HideAllPanels();
            _memoryPanel.SetActive(true);
            _memTitle.text = title;
            _memBody.text = body;
        }

        public void ShowEnding(string body, string[] choices, Action<string> onChoice)
        {
            HideAllPanels();
            _endingPanel.SetActive(true);
            ClearChildren(_endingPanel.transform);
            var t = MakePanelText(_endingPanel, "Body", new Vector2(0, 30), 780, 430,
                                  TextAnchor.UpperLeft, 17);
            t.text = body;
            float y = -200f;
            if (choices != null && onChoice != null)
            {
                foreach (var choice in choices)
                {
                    var captured = choice;
                    var capturedAction = onChoice;
                    MakePanelButton(_endingPanel, "Choice_" + captured, new Vector2(0, y), 460, 48,
                                    captured)
                        .onClick.AddListener(() => capturedAction(captured));
                    y -= 62f;
                }
            }
            else
            {
                var restart = MakePanelButton(_endingPanel, "Restart", new Vector2(0, y), 300, 46,
                                              "重新开始（保留原世界）");
                restart.onClick.AddListener(() =>
                {
                    Data.GameState.BeginNewWorld();
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                });
            }
        }

        // ───────────────────────── 菜单流程 ─────────────────────────
        public Action OnNewWorld, OnLoadWorld, OnQuit, OnCreateWorld, OnBackToTitle;
        public Action OnSynthesisKey, OnBackpack, OnReturnLab;
        GameObject _returnLabBtn;

        /// <summary>野外区域的「返回实验室」快速传送按钮(原版右下角)。</summary>
        public void SetReturnLabVisible(bool visible)
        {
            if (_returnLabBtn == null && visible)
            {
                _returnLabBtn = new GameObject("ReturnLab", typeof(RectTransform));
                _returnLabBtn.transform.SetParent(_canvas.transform, false);
                Place((RectTransform)_returnLabBtn.transform, new Vector2(1f, 0f), new Vector2(-190, 84),
                      new Vector2(170, 52));
                var img = _returnLabBtn.AddComponent<Image>();
                img.color = new Color(0.16f, 0.11f, 0.05f, 0.92f);
                var btn = _returnLabBtn.AddComponent<Button>();
                btn.onClick.AddListener(() => { if (OnReturnLab != null) OnReturnLab(); });
                var t = MakeText("l", new Vector2(160, 48), TextAnchor.MiddleCenter, 16, Accent);
                t.transform.SetParent(_returnLabBtn.transform, false);
                var trt = t.rectTransform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
                t.text = "返回实验室";
            }
            if (_returnLabBtn != null) _returnLabBtn.SetActive(visible);
        }

        /// <summary>标题画面:原版主菜单图 + 三个按钮。</summary>
        public void ShowTitle()
        {
            ClearMenu();
            _menuScreen = "title";
            _menuRoot = NewMenuRoot();
            var art = FullScreenImage(_menuRoot, "menu_title");

            // 透明点击区:整行横条(宽容命中),键盘 N/L/Q 亦可
            ArtZone(art, "zone_new", new Vector2(0.73f, 0.55f), new Vector2(0.7f, 0.14f),
                () => { if (OnNewWorld != null) OnNewWorld(); });
            ArtZone(art, "zone_load", new Vector2(0.73f, 0.41f), new Vector2(0.7f, 0.14f),
                () => { if (OnLoadWorld != null) OnLoadWorld(); });
            ArtZone(art, "zone_quit", new Vector2(0.73f, 0.27f), new Vector2(0.7f, 0.14f),
                () => { if (OnQuit != null) OnQuit(); });
        }

        /// <summary>世界选择画面。</summary>
        public void ShowWorldSelect()
        {
            ClearMenu();
            _menuScreen = "world";
            _menuRoot = NewMenuRoot();
            var wsArt = FullScreenImage(_menuRoot, "menu_world");

            ArtZone(wsArt, "zone_create", new Vector2(0.5f, 0.79f), new Vector2(1.2f, 0.4f),
                () => { if (OnCreateWorld != null) OnCreateWorld(); });
            ArtZone(wsArt, "zone_back", new Vector2(0.5f, 0.2f), new Vector2(1.2f, 0.3f),
                () => { if (OnBackToTitle != null) OnBackToTitle(); });
        }

        public void ShowSaveSlots(Data.SaveSlotInfo[] slots, Action<string> select, int page = 0)
        {
            ClearMenu();
            _menuScreen = "slots";
            _menuRoot = NewMenuRoot();
            FullScreenImage(_menuRoot, "menu_world");
            var rows = slots ?? new Data.SaveSlotInfo[0];
            int pages = Math.Max(1, (rows.Length + 3) / 4);
            page = Mathf.Clamp(page, 0, pages - 1);
            var title = MakePanelText(_menuRoot, "SlotsTitle", new Vector2(0, 265), 1000, 42, TextAnchor.MiddleCenter, 25);
            title.text = "选择世界 " + (page + 1) + "/" + pages + (rows.Length == 0 ? " — 暂无存档，请返回新建世界" : "");
            int row = 0;
            foreach (var slot in rows.Skip(page * 4).Take(4))
            {
                var selected = slot;
                var savedRegion = Data.GameData.Regions.regions.FirstOrDefault(r => r.id == slot.region);
                string label = (slot.id == "legacy" ? "原有世界" : "世界存档 " + (page * 4 + row + 1)) + "\n" +
                    (savedRegion == null ? "未知区域" : Data.Localization.Tr(savedRegion.name_key)) + "  ·  实验室阶段 " + slot.stage + "  ·  " + slot.status;
                var button = MakePanelButton(_menuRoot, "Slot_" + slot.id, new Vector2(0, 175 - row * 85), 1020, 72, label);
                button.interactable = slot.loadable;
                button.onClick.AddListener(() => select(selected.id));
                row++;
            }
            if (pages > 1)
            {
                int next = (page + 1) % pages;
                MakePanelButton(_menuRoot, "NextSlots", new Vector2(200, -235), 220, 50, "下一页")
                    .onClick.AddListener(() => ShowSaveSlots(rows, select, next));
            }
            MakePanelButton(_menuRoot, "BackSlots", new Vector2(-180, -235), 220, 50, "返回标题")
                .onClick.AddListener(() => { if (OnBackToTitle != null) OnBackToTitle(); });
        }

        /// <summary>加载屏:黑底 + 进度条(WORLD SYNTHESIS SYSTEM)。</summary>
        public void ShowLoading(string title)
        {
            ClearMenu();
            _menuScreen = "loading";
            _menuRoot = new GameObject("MenuRoot");
            _menuRoot.transform.SetParent(_canvas.transform, false);
            var bgGo = new GameObject("black");
            bgGo.transform.SetParent(_menuRoot.transform, false);
            var brt = bgGo.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var bimg = bgGo.AddComponent<Image>();
            bimg.color = new Color(0.01f, 0.01f, 0.015f, 1f);

            var t = MakeText("lt", new Vector2(700, 50), TextAnchor.MiddleCenter, 26, Accent);
            t.transform.SetParent(_menuRoot.transform, false);
            Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(700, 50));
            t.text = title;

            var barBg = new GameObject("barBg");
            barBg.transform.SetParent(_menuRoot.transform, false);
            Place(barBg.AddComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0, -40),
                  new Vector2(560, 14));
            var bbar = barBg.AddComponent<Image>();
            bbar.color = new Color(0.15f, 0.18f, 0.22f, 1f);
            _loadingFill = new GameObject("fill").AddComponent<Image>();
            _loadingFill.transform.SetParent(barBg.transform, false);
            var frt = _loadingFill.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = new Vector2(2, 2); frt.offsetMax = new Vector2(0, -2);
            frt.sizeDelta = new Vector2(10, 0);
            _loadingFill.color = new Color(0.4f, 0.85f, 0.85f);

            var pct = MakeText("pct", new Vector2(120, 34), TextAnchor.UpperCenter, 18,
                               new Color(0.5f, 0.85f, 0.85f));
            pct.transform.SetParent(_menuRoot.transform, false);
            Place(pct.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(220, -58), new Vector2(120, 34));
            pct.text = "0%";
            _loadingPct = pct;

            var sub = MakeText("sub", new Vector2(700, 28), TextAnchor.UpperCenter, 13,
                               new Color(0.45f, 0.5f, 0.55f));
            sub.transform.SetParent(_menuRoot.transform, false);
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -110), new Vector2(700, 28));
            sub.text = "WORLD SYNTHESIS SYSTEM · BUILD 01";
        }

        public void SetLoadingProgress(float f)
        {
            f = Mathf.Clamp01(f);
            if (_loadingFill != null)
            {
                var frt = _loadingFill.rectTransform;
                frt.anchorMax = new Vector2(f, 1f);
                frt.sizeDelta = new Vector2((560f - 4f) * f, 0f);
            }
            if (_loadingPct != null) _loadingPct.text = Mathf.RoundToInt(f * 100f) + "%";
        }

        public void HideMenu()
        {
            ClearMenu();
            _menuScreen = "";
        }

        /// <summary>菜单键盘快捷键(N/L/Q/Enter/B),鼠标事件异常时保底。</summary>
        public void TickMenuKeys()
        {
            if (_menuScreen == "") return;
            if (Input.GetKeyDown(KeyCode.N) && _menuScreen == "title" && OnNewWorld != null) OnNewWorld();
            if (Input.GetKeyDown(KeyCode.L) && _menuScreen == "title" && OnLoadWorld != null) OnLoadWorld();
            if (Input.GetKeyDown(KeyCode.Q) && _menuScreen == "title" && OnQuit != null) OnQuit();
            if (Input.GetKeyDown(KeyCode.Return) && _menuScreen == "world" && OnCreateWorld != null) OnCreateWorld();
            if (Input.GetKeyDown(KeyCode.B) && _menuScreen == "world" && OnBackToTitle != null) OnBackToTitle();
        }

        void ClearMenu()
        {
            if (_menuRoot != null) { Destroy(_menuRoot); _menuRoot = null; }
            _loadingFill = null;
            _loadingPct = null;
        }

        Image FullScreenImage(GameObject parent, string artName)
        {
            var go = new GameObject("art");
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            var tex = ArtLib.Sheet(artName);
            if (tex != null)
            {
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                           new Vector2(0.5f, 0.5f), tex.width / 1280f);
                var aspect = go.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = tex.width / (float)tex.height;
                img.preserveAspect = true;
            }
            else
                Debug.LogError("[GameUI] 菜单图加载失败: " + artName);
            img.raycastTarget = true;
            return img;
        }

        GameObject NewMenuRoot()
        {
            var root = new GameObject("MenuRoot", typeof(RectTransform));
            root.transform.SetParent(_canvas.transform, false);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            root.AddComponent<Image>().color = new Color(.015f, .018f, .025f, 1);
            return root;
        }

        /// <summary>分数锚点透明点击区:挂在背景图上,尺寸/位置为图的百分比,分辨率无关。</summary>
        void ArtZone(Image art, string zoneId, Vector2 fracCenter, Vector2 fracSize, Action onClick)
        {
            var go = new GameObject("azone_" + zoneId, typeof(RectTransform));
            go.transform.SetParent(art.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(fracCenter.x - fracSize.x * 0.5f, fracCenter.y - fracSize.y * 0.5f);
            rt.anchorMax = new Vector2(fracCenter.x + fracSize.x * 0.5f, fracCenter.y + fracSize.y * 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        /// <summary>透明点击区:对准画中已绘制的按钮。</summary>
        void TransparentZone(GameObject parent, Vector2 pos, Vector2 size, Action onClick)
        {
            var go = new GameObject("zone", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            Place(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), pos, size);
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick());
        }

        void MenuButton(GameObject parent, string label, Vector2 pos, Action onClick, float w = 460f)
        {
            var go = new GameObject("mbtn_" + label);
            go.transform.SetParent(parent.transform, false);
            Place(go.AddComponent<RectTransform>(), new Vector2(0.5f, 0.5f), pos, new Vector2(w, 66));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.04f, 0.05f, 0.06f, 0.72f);
            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.5f, 0.65f, 0.7f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());
            var t = MakeText("l", new Vector2(w - 20, 60), TextAnchor.MiddleCenter, 21,
                             new Color(0.92f, 0.9f, 0.85f));
            t.transform.SetParent(go.transform, false);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            t.text = label;
        }

        // ───────────────────────── 背包面板 ─────────────────────────
        /// <summary>背包:TAB 打开,列出持有物质。</summary>
        public void ShowBackpack(string[] lines)
        {
            if (_backpackPanel == null)
            {
                _backpackPanel = MakePanel("BackpackPanel", new Vector2(720, 520));
                var title = MakePanelText(_backpackPanel, "Title", new Vector2(0, 228), 640, 40,
                                          TextAnchor.UpperLeft, 21, Accent);
                title.text = "背包 · 滚轮浏览";
                var viewport = new GameObject("InventoryViewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
                viewport.transform.SetParent(_backpackPanel.transform, false);
                var vr = viewport.GetComponent<RectTransform>(); vr.sizeDelta = new Vector2(640, 370); vr.anchoredPosition = new Vector2(0, -4);
                viewport.GetComponent<Image>().color = new Color(0, 0, 0, .15f);
                _backpackBody = MakePanelText(viewport, "Body", Vector2.zero, 616, 370, TextAnchor.UpperLeft, 17);
                var br = _backpackBody.rectTransform; br.anchorMin = new Vector2(0, 1); br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(.5f, 1); br.sizeDelta = new Vector2(-24, 0); br.anchoredPosition = new Vector2(0, -8);
                _backpackBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.GetComponent<ScrollRect>(); scroll.content = br; scroll.viewport = vr; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
                _backpackBody.raycastTarget = false;
                var close = MakePanelButton(_backpackPanel, "Close", new Vector2(0, -228), 180, 44, "关闭(TAB)");
                close.onClick.AddListener(CloseAllPanels);
            }
            HideAllPanels();
            _backpackPanel.SetActive(true);
            if (_backpackBody) _backpackBody.text = lines != null && lines.Length > 0
                ? string.Join("\n", lines)
                : "背包空空如也 — 去采集资源吧。";
        }

        public void ShowDeviceConsole(Data.FormalDeviceDef[] devices, Data.GameState state,
            Func<string, Data.DeviceOperationResult> craft,
            Func<string, Data.DeviceOperationResult> place,
            Func<string, Data.ProcessingRecipeDef[]> recipesFor,
            Func<string, string, IDictionary<string, string>, Data.DeviceOperationResult> start,
            Func<string, Data.DeviceOperationResult> collect,
            Func<string, string, Data.DeviceOperationResult> store,
            Func<string, Data.DeviceOperationResult> withdraw,
            string selectedDeviceId = "")
        {
            HideAllPanels();
            _devicePanel.SetActive(true);
            var rows = devices ?? new Data.FormalDeviceDef[0];
            if (!string.IsNullOrEmpty(selectedDeviceId)) _selectedDeviceId = selectedDeviceId;
            if (string.IsNullOrEmpty(_selectedDeviceId) || !rows.Any(value => value.id == _selectedDeviceId))
                _selectedDeviceId = rows.Length > 0 ? rows[0].id : "";

            Action refresh = null;
            refresh = () =>
            {
                ClearChildren(_deviceList);
                foreach (var row in rows)
                {
                    var captured = row.id;
                    var owned = state.HasCraftedDevice(row.id) ? " ✓" : "";
                    MakeGridButton(_deviceList, row.slot.ToString("00") + " " + row.display_name + owned,
                        () => { _selectedDeviceId = captured; refresh(); },
                        row.id == _selectedDeviceId ? new Color(0.34f, 0.22f, 0.09f, 1f) : (Color?)null);
                }

                ClearChildren(_deviceActions);
                var selected = rows.FirstOrDefault(value => value.id == _selectedDeviceId);
                if (selected == null) { _deviceBody.text = "没有正式设备数据。"; return; }
                var instances = (state.deviceInstances ?? new Data.DeviceRuntimeContract[0])
                    .Where(value => value != null && value.device_id == selected.id).ToArray();
                _deviceBody.text = selected.display_name + "\n" + selected.purpose +
                    "\n需要实验室阶段 " + selected.stage + "  ·  当前阶段 " + state.laboratoryStage +
                    "\n制造：" + (state.HasCraftedDevice(selected.id) ? "已完成" : "尚未制造") +
                    "  ·  已部署 " + instances.Length + " 台" +
                    (instances.Length == 0 ? "" : "\n状态: " + string.Join(" / ", instances.Select(value =>
                        DeviceStateLabel(value.state) +
                        (string.IsNullOrEmpty(value.error_reason) ? "" : "（" + DeviceFailureLabel(value.error_reason) + "）") +
                        (value.storage_capacity > 0 ? " [" +
                            (string.IsNullOrEmpty(value.stored_item_id) ? "空" : Data.ChemistryEngine.DisplayName(value.stored_item_id)) +
                            " " + value.stored_amount + "/" + value.storage_capacity + "]" : "")).ToArray()));

                if (!state.HasCraftedDevice(selected.id))
                    MakeGridButton(_deviceActions, "制造设备", () => { ShowDeviceResult(craft(selected.id)); refresh(); });
                else
                    MakeGridButton(_deviceActions, "部署设备", () => { ShowDeviceResult(place(selected.id)); refresh(); });

                foreach (var instance in instances)
                {
                    var capturedInstance = instance;
                    if (capturedInstance.state == Data.DeviceProcessingState.OutputReady)
                        MakeGridButton(_deviceActions, "领取产物 · 设备 " + (Array.IndexOf(instances, capturedInstance) + 1),
                            () => { ShowDeviceResult(collect(capturedInstance.instance_id)); refresh(); }).name = "Collect_" + capturedInstance.instance_id;
                    if (capturedInstance.state == Data.DeviceProcessingState.Processing) continue;
                    if (selected.processing_type == "store_liquid")
                    {
                        if (!string.IsNullOrEmpty(capturedInstance.stored_item_id) && capturedInstance.stored_amount > 0)
                            MakeGridButton(_deviceActions, "取出 " + Data.ChemistryEngine.DisplayName(capturedInstance.stored_item_id),
                                () => { ShowDeviceResult(withdraw(capturedInstance.instance_id)); refresh(); });
                        foreach (var liquidId in state.OwnedMaterialIds()
                            .Where(Data.SpecializedDeviceChemistryService.IsLiquid).OrderBy(value => value))
                        {
                            var capturedLiquid = liquidId;
                            MakeGridButton(_deviceActions, "存入 " + Data.ChemistryEngine.DisplayName(capturedLiquid),
                                () => { ShowDeviceResult(store(capturedInstance.instance_id, capturedLiquid)); refresh(); });
                        }
                    }
                    foreach (var recipe in (recipesFor(selected.id) ?? new Data.ProcessingRecipeDef[0])
                        .Where(value => !value.id.EndsWith("_kit", StringComparison.Ordinal) ||
                            value.id == "recipe_phase4b_liquid_tank_kit" ||
                            value.id == "recipe_phase4b_high_pressure_reactor_kit"))
                    {
                        var capturedRecipe = recipe;
                        if (capturedRecipe.id == "recipe_phase4a_industrial_crystallize")
                        {
                            foreach (string solution in new[] { "compound_concentrated_salt_solution", "compound_loaded_absorption_solution" })
                            {
                                var capturedSolution = solution;
                                MakeGridButton(_deviceActions, "结晶 " + Data.ChemistryEngine.DisplayName(capturedSolution), () => {
                                    var conditions = DefaultDeviceConditions(capturedRecipe); conditions["solution_id"] = capturedSolution;
                                    ShowDeviceResult(start(capturedInstance.instance_id, capturedRecipe.id, conditions)); refresh(); });
                            }
                            continue;
                        }
                        MakeGridButton(_deviceActions, "运行 " + ShortRecipeName(capturedRecipe.id), () =>
                        {
                            var conditions = DefaultDeviceConditions(capturedRecipe);
                            ShowDeviceResult(start(capturedInstance.instance_id, capturedRecipe.id, conditions));
                            refresh();
                        });
                    }
                }
            };
            refresh();
        }

        void ShowDeviceResult(Data.DeviceOperationResult result)
        {
            ShowToast(result != null && result.success ? "设备操作已接受" : "设备拒绝: " +
                DeviceFailureLabel(result != null ? result.reason : ""));
        }

        static string ShortRecipeName(string id)
        {
            return RecipeLabel(id);
        }

        static IDictionary<string, string> DefaultDeviceConditions(Data.ProcessingRecipeDef recipe)
        {
            var result = new Dictionary<string, string>();
            foreach (var condition in recipe.required_conditions ?? new Data.ConditionValue[0])
                if (condition.id != "required_stage") result[condition.id] = condition.value;
            if (recipe.id == "recipe_phase4a_industrial_crystallize")
            {
                result["concentration"] = "0.95";
                result["temperature"] = "0.10";
            }
            return result;
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
        }
    }
}
