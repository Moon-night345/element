# 元素星海·裂核 — Unity 迁移交接文档(2026-10-04)

> 本轮完成:Unity 垂直切片从 0 到可运行——主场景、玩家移动、18 区域世界、
> 化学反应引擎(40 配方)、碎片语言谜题(16)、记忆重构(16)、12 步任务链、
> 双结局。Unity 自带编译器验证零错误,无头冒烟测试 6/6 全绿。

---

## 0. 一分钟速览

- **位置**:`C:\Users\18483\OneDrive\Desktop\元素星海\UnityProject`
- **引擎**:Unity 2022.3.62f3c1(中国版,装在 `D:\2022.3.62f3c1\Editor\Unity.exe`)
- **打开方式**:Unity Hub → 添加 `UnityProject` → 打开;或直接双击项目内场景
  `Assets/Scenes/Main.unity`;运行点 Play
- **数据来源**:Godot 仓库单一事实来源导出(JSON),见 §3
- **验证**:批量模式一条命令跑场景生成 + 6 项冒烟测试(见 §5)

## 1. 本轮做了什么

1. **修复 Safe Mode**(三处):
   - `DataModels.cs` 的 `public string[] sealed` —— `sealed` 是 C# 关键字,
     改为 `@sealed`(字段名不变,JSON 键照常映射)
   - `GameDataLoader.cs` 的 `Object` 二义性(CS0104)→ `UnityEngine.Object`
   - 清单中 `com.unity.textmeshpro` 下载超时 → 移除(项目完全不依赖 TMP)
2. **数据管线**(tools/export_chem_data.py):
   - 从 `world_content_catalog.gd` 解析 **40 条反应配方**(reactants/products)
   - 54 种物质(14 元素 + 5 离子 + 化合物/资源)带中文名和化学式符号
   - `zh_CN.csv` 全量 683 条词条 → `localization_zh.json`(含 4 条切片新增目标文案)
   - 修复原版导出缺失的 **记忆→区域映射**(16 条,来自 memory_content_catalog.gd)
3. **玩法层**(C#,全部程序化构建,无外部资源依赖):
   - `ChemistryEngine`:精确多重集配比匹配,与 Godot reaction_service 语义一致
   - `GameState`:库存(并行数组)/生命/能量/辐射/任务步/碎片/记忆,JsonUtility 存档
   - `GameBootstrap`:18 区域世界(prologue_lab + 7 地表 + 9 地下竖井电梯)、
     交互系统(冷冻舱/引路人/工作台/氢泄漏舱/合成仪/气闸/裂核)、碎片谜题
     (正确语言 + 3 干扰项乱序)、记忆重构(需碎片平息或对应反应)、辐射伤害、
     死亡回舱、30 秒自动存档、双结局抉择
   - `GameUI`:uGUI 全代码构建——HUD(目标/状态/库存/提示/公告)、合成面板
     (库存点选 4 格 + 合成)、对话面板、记忆面板、结局面板、帮助(H 键)
   - 中文渲染:系统微软雅黑(OS 动态字体),不用 TMP
4. **验证闭环**:`Editor/UnitySliceBuilder.cs` 提供 `BuildAll()`:
   生成主场景 + 6 项断言(数据量/配比匹配/本地化完整/存档往返/关联完整),
   退出码 0/1 —— **当前全绿**。

## 2. UnityProject 结构

```
Assets/
  Scenes/Main.unity            ← 主场景(只含一个 GameBootstrap 节点,其余运行时构建)
  Scripts/
    ChemData.cs                ← 化学数据模型(chemistry.json 对应)
    Localization.cs            ← 词条查表 Localization.Tr(key)
    ChemistryEngine.cs         ← 纯逻辑反应引擎(可脱离场景测试)
    GameState.cs               ← 可序列化状态 + 存档(persistentDataPath)
    SpriteUtil.cs              ← 方块精灵/世界文字(无外部资源)
    GameUI.cs                  ← uGUI 全程序化界面
    GameBootstrap.cs           ← 主控:世界/玩家/交互/谜题/任务/结局
    DataModels.cs              ← shards/memories/endings/regions/reactions/quest_chain 模型
    GameDataLoader.cs          ← Resources/Data 加载器
    Editor/UnitySliceBuilder.cs← 批量构建 + 冒烟测试(编辑器专用)
  Resources/Data/*.json        ← 8 个数据文件(见 §3)
tools/export_chem_data.py      ← Godot → Unity 数据导出(单一事实来源)
```

## 3. 数据文件(Resources/Data/)

| 文件 | 内容 | 来源 |
|---|---|---|
| shards.json | 16 碎片人格/谜题/语言 | export_unity_data.gd(原版) |
| memories.json | 16 记忆(**region 字段本轮补全**) | 同上 + 本轮修复 |
| endings.json | 核心真相/封存/融合 | 原版 |
| regions.json | 17 区域(1 实验室+7 地表+9 地下)+顺序 | 原版 |
| reactions.json | 40 反应元数据 | 原版 |
| quest_chain.json | 12 步任务链 | 原版 |
| chemistry.json | 54 物质 + 40 配方(**本轮新增**) | export_chem_data.py |
| localization_zh.json | 683 条中文(**本轮新增**) | 同上 |

> Godot 侧内容有改动时:`python tools/export_chem_data.py` 重导,
> 配方解析自 `src/world/world_content_catalog.gd`,词条来自 `localization/zh_CN.csv`。

## 4. 玩法流程(切片实现的主线)

苏醒(按任意键)→ 引路人终端(E)→ 气闸方向 → 工作台修镐 → 氢泄漏舱截 2H →
合成仪 2H→H₂(C 键)→ 终端演示 → 气闸出实验室 → 各地表区域平息 3 个碎片
(语言谜题)→ 发现 5 种反应 → 电梯下潜重构 6 段记忆 → 最深裂核 →
看完核心真相 → **抉择:封存 / 融合** → 尾声 + 删除存档重开。

操作:WASD/方向键移动,E 交互/推进,C 合成仪,H 帮助,Esc 关面板。

## 5. 验证命令(无头,不进编辑器)

```bash
D:/2022.3.62f3c1/Editor/Unity.exe -batchmode -quit \
  -projectPath "C:/Users/18483/OneDrive/Desktop/元素星海/UnityProject" \
  -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.BuildAll \
  -logFile "%TEMP%/unity_batch.log"
# 退出码 0 = 场景生成 + 6 项测试全过;日志里搜 [PASS]/[FAIL]
```

注意:批量模式前必须关闭正在运行的 Unity 编辑器(项目锁)。

## 6. 踩坑记录(给下一轮)

1. **C# 关键字字段**:`@sealed` 在**引用处也要带 @**(`_endings.@sealed`),
   只改声明会报 CS1525
2. **批量模式 Destroy**:编辑模式下 `Object.Destroy` 报错,
   `GameDataLoader.Destroy()` 已按 `isEditor && !isPlaying` 分流 DestroyImmediate
3. **EventSystem 命名空间**:在 `UnityEngine.EventSystems`,不在 `UnityEngine.UI`
4. **JsonUtility 静默失败**:JSON 键与字段名必须精确一致(大小写敏感),
   冒烟测试第 6 项专门防这个
5. **中文字体**:内置 LegacyRuntime.ttf 缺 CJK,用
   `Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 16)`
6. **区域数**:17 不是 18(prologue_lab 不在 order.surface 里,需手动加最西)

## 7. 与 Godot 版的差距(下一轮方向)

- **反应条件**:切片只有配比匹配;Godot 版还有 tags 条件(heat/wet/electric/
  high_pressure…)和 any_tags,数据已导出("tags" 字段未导,需要时补)
- **装置系统**:device04~12(高压反应器等)未迁移
- **敌人/战斗**:未迁移
- **代理系统**(16 碎片的 AI 行为决策树):切片只实现了语言谜题,
  Godot 的 `src/agent/*` 行为档案未迁移
- **TileMap 视觉**:现在是色块+文字标签;正式版应导入 Tilemap 资源与美术
- **地图结构**:切片用 80 单位/地表区域 + 40 单位/地下层的示意布局;
  Godot 版的 EnvironmentLayoutCatalog 精确尺寸未照搬
- **音效/BGM**:无
- **输入系统**:用的旧 Input 类;若用 InputSystem 包需重写

## 8. Godot 侧状态(未动)

`C:\Users\18483\Astral-Element-Sea` 保持上轮交接状态(见
`交接文档_2026-09-14.md`),本轮只读未改。上轮遗留:游玩期轻微卡顿待查
(怀疑 `_draw` 全量重绘/战争迷雾每帧调用,需 profiler 实测)。

---

## 附:原版美术移植完成(2026-10-04 晚,第二轮)

用户反馈 Unity 画面远逊 Godot 原版后,发现 Godot 仓库 `assets/` 与 `art_source/`
有完整美术管线,已整体移植:

- **392 个资产** → `UnityProject/Assets/StreamingAssets/{Art,Audio}/`
- **背景画**:17 区域手绘背景(`bg_<region>.png`,1672×941),跟随相机、按区域切换
- **地形**:每区域 16 块专属 128px 瓦片(`tile_<region>_XX_XX.png`),随机变体铺放
- **角色**:女化学家 80 帧(`chem_idle/walk/run/jump/hit/...`,128px),idle/walk/jump 动画已接
- **引路人机器人**:4 帧悬浮动画伴随玩家(`lumen_00..03`)
- **实验室装置立绘**:冷冻舱/引路人终端/气闸/氢泄漏口/合成台/工作台
  (`lab_*.png`、`device_*.png`)替换彩色方块
- **音效**:12 个 wav(`Audio/`),WavLoader 运行时解析;
  脚步声随移动启停、采集/合成/气闸/电梯/对话均有音效、实验室环境音循环
- **UI**:深棕底+暖金强调色,对齐原版 UI 风格
- 程序天空/山峦压到背景画之后(仅兜底);`WavLoader.cs`、`ArtLib.Single/Frames/Clip` 为新增
- Kenney 素材仍保留(世界标签兜底/道具),StreamingAssets 加载管线不变

剩余差距:无标题菜单、无快捷栏/小地图、装置立绘尺寸待精调、缺 BGM(素材里暂无)。

---

## 附2:完整体验链还原(同日深夜,第三轮)

对照玩家提供的原版视频逐项还原:

- **标题菜单**:`menu_title.png` 整图(按钮已画在图中)+ 透明点击区;新建世界会清存档
- **世界选择**:`menu_world.png` + 创建并进入世界/返回
- **加载屏**:黑底青条 + "正在生成死城废墟地形"(1.4s) + WORLD SYNTHESIS SYSTEM 字样
- **原版布局 HUD**:左上 地图名+小地图条 / 右上 合成能量·HP·RAD 三行 / 左下 任务目标(✓已完成·当前·○待办) / 底部快捷栏1-8 + ESC暂停·TAB背包·M地图·C合成(TAB/C 真实可用)
- **打字机对话**:底部居中宽条,逐字显示,E 先补全再继续
- **碎片**:名字+信任度+绿色光柱(平息后熄灭)
- **相机**:orthographicSize 7(原版视角)+ 像素网格取整
- **菜单流程**:`GameStarted` 静态标记,标题→选择→加载→InitGame;菜单期 HUD 不构建、相机先行(否则 Game view 显示 No cameras rendering)
- **AudioListener** 挂在相机上(缺失会导致全部音效无声——曾踩坑)

⚠ 踩坑:uGUI 的 MenuRoot 忘加 RectTransform 会导致整图被放大到只剩暗色像素;
ShowTitle 里创建背景图的语句在重构时丢失过一次,表现为纯黑菜单。

---

## 附3:实验室室内化 + 菜单键盘快捷键(同日,第四轮)

- **实验室改为真室内**:内景画(高22单位)锚定世界坐标随镜头 1:1 滚动,三幅镜像平铺;
  画中地板线(85%高)精确对齐行走线 y=0,floor_strip 压表面;矿石/植物不进实验室。
- **室外背景** 0.5 视差跟随相机。
- **装置名称牌 + 黄色▼标记**(原版样式);碎片升级为底座+三晶簇+金名+信任+光柱。
- **对话**去底板:大字居中 + 打字机 + (E 继续)。
- **合成仪**改原版布局:全屏遮罩+左对齐配方大字(投入/反应条件/能量成本/产物/语言词,
  选料实时预览)+退出/确认合成。
- **空格跳跃**(重力30/起跳13)+跳跃音效;**返回实验室**按钮(野外右下角,点击传送)。
- **小地图**显示区域背景缩略图。
- **菜单键盘快捷键**:标题 N=新建 L=载入 Q=退出;世界选择 Enter=创建 B=返回。
  (uGUI 鼠标点击在部分环境下不触发,键盘为保底通路;`[MenuUI]` 诊断日志可查射线命中链)
- 工具:`tools/game_input.ps1`(模拟点击/按键)、`tools/screenshot.ps1`(截屏)、
  `tools/diag_window.ps1`(窗口/焦点诊断)。

⚠ 待修:鼠标点击 uGUI 按钮在实测中不触发(键盘正常,`Input.GetMouseButtonDown` 对
模拟点击不返回 true)——已留诊断日志,需用一次真实鼠标点击定位。
