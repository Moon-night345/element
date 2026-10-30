# Godot → Unity：逐系统 90% 迁移验收基线

当前实机增量（U32）：Main正式键盘入口与探针共享运动回调，实际起跳/1.2秒悬浮与18能量/地形不穿透/落地重置/面板锁，以及独立进程能力/能量/落地位置恢复均PASS。日志与夹具限制见 GODOT_TO_UNITY_U32_PROGRESS_2026-10-29.md；自然地下通行、区域过渡、动画/触控仍未验收，PLY保持PARTIAL。

最新增量（2026-10-29，U32）：接入源PlayerMotor参数、氢悬浮/分时能耗及真实氢反应成功奖励、能力存档与旧完成进度兼容。93组检查、Windows构建0错误；Main自然移动/悬浮/碰撞和地下通行仍待运行验收，PLY保持PARTIAL，详见 GODOT_TO_UNITY_U32_PROGRESS_2026-10-29.md。

最新增量（2026-10-29，U31）：SurvivalRuntimeService接入伤害/治疗、辐射/附加防护、医疗包专用计数与存档，Main面板暂停环境伤害；91组检查、Windows构建0错误。医疗包获取/入口为源缺口，死亡生命周期、真实自然路线和完整动态环境仍待验收，SUR保持PARTIAL，详见 GODOT_TO_UNITY_U31_PROGRESS_2026-10-29.md。

当前检查点（2026-10-29，取代下方U29时间点）：U30战斗核心从MISSING推进为PARTIAL。15类源敌人、追击/冷却/鼠标攻击/一次性奖励/战斗快照和Main轮廓血条已接入；89组检查、Windows构建0错误、实际Main及独立进程恢复通过。仍未正式验收任一大系统达到90%，剩余自然路线、动作反馈、移动/生存/完整地形/化学设备分支/叙事/视觉等不得省略。详见 GODOT_TO_UNITY_U30_PROGRESS_2026-10-29.md。

日期：2026-10-18（Asia/Shanghai）。版本：v1，工作区代码审计与迁移待办基线。

目标：**每一个系统分别达到至少 90%，且没有阻断正常玩家流程的缺口。不能用总体平均值代替单项验收。**

最新检查点（2026-10-29）：U29行星核首轮核心收口，专属Agent核心实施13/13，不等于区域 VERIFIED、FULL_COVERAGE或90%。累计87组检查通过，构建0错误；Main入场/未知静默/检测/HUD/储罐实际按钮、U28产物守恒与独立进程恢复通过。正式条件选择/失败/能量需要/突破等生产者缺口不由合同测试伪造；完整上下文、事件时机、动态环境、自然路线、战斗、视觉和其他系统待办仍保留。详见 GODOT_TO_UNITY_U29_CLOSEOUT_2026-10-29.md。下一阶段U30优先补当前MISSING的战斗系统。下方各日期增量为历史记录。

历史检查点（2026-10-29）：U28核心壳首轮核心收口，12/13核心实施、83组检查、实际Main与跨进程记录见 GODOT_TO_UNITY_U28_CLOSEOUT_2026-10-29.md。

历史检查点（2026-10-29）：U27 地幔流首轮核心收口，11/13核心实施、80组检查、实际Main与跨进程记录见 GODOT_TO_UNITY_U27_CLOSEOUT_2026-10-29.md。

历史检查点（2026-10-29）：U27分步实现与前序验证见 GODOT_TO_UNITY_U27_ANALYSIS_2026-10-29.md，最新实际联合链及剩余范围以上方收口记录为准。

历史检查点（2026-10-29）：U26过程记录与前序缺口见 GODOT_TO_UNITY_U26_ANALYSIS_2026-10-29.md；最新扫描/字段增量及剩余范围以上方收口记录为准。

历史检查点（2026-10-28）：U25功能逐步接入时尚未计入完整核心；已由上方2026-10-29收口检查点替代。过程与失败日志仍保留，详见 GODOT_TO_UNITY_U25_ANALYSIS_2026-10-28.md。

U21 后续功能包（2026-10-23）：接入未知样本假说→真实消耗分析→确认关联/成熟/存档，累计55组；新增 SandMaterialBehavior，详情 GODOT_TO_UNITY_U21_HYPOTHESIS_LINK_2026-10-23.md。梯度/失败合同等仍待补齐，核心计数不变。

2026-10-23 增量：U20A 接入余烬热分解、冷凝交付展示和盐碱分离回忆的局部分支，合同检查累计 47 组。余烬完整 adapter 尚未完成，核心计数仍为 3/13；详见 GODOT_TO_UNITY_PHASE_U20A_REPORT_2026-10-23.md。未将局部分支算作整个区域通过。

2026-10-23 后续：U20B 增加四种余烬样本的源分析定义与真实检测事件解释，检查累计 48 组；仍缺专属状态、环境确认、梯度与实际 Main 验收，核心计数不变。详见 GODOT_TO_UNITY_PHASE_U20B_REPORT_2026-10-23.md。

2026-10-23 U20C：补余烬入场温度确认、专属事实/进展/成熟快照和存档校验，检查累计 49 组；未知/失败梯度、能力与联合实机验收仍待完成，核心计数仍 3/13。详见 GODOT_TO_UNITY_PHASE_U20C_REPORT_2026-10-23.md。

2026-10-23 U20D：补余烬未知材料提示梯度、计数重置和磁盘恢复，检查累计 50 组；失败梯度、能力、碳燃料与联合实机验收仍未完成，核心计数不变。详见 GODOT_TO_UNITY_PHASE_U20D_REPORT_2026-10-23.md。

2026-10-23 U20E：接入真实冷凝器能力与正式失败合同，检查累计 51 组，补余烬联合 Main 场景与重启探针；失败匹配发布入口、碳燃料与完整逐事实策略仍缺，不提前计整区完成。详见 GODOT_TO_UNITY_PHASE_U20E_REPORT_2026-10-23.md。

2026-10-23 U20F：继续接入源碳燃料消费/次数/实际节点/燃烧观察并重跑联合游戏进程与恢复检查，累计 52 组；正式失败发布和完整逐事实策略等仍缺，余烬未提前计整区完成。详见 GODOT_TO_UNITY_PHASE_U20F_REPORT_2026-10-23.md。

2026-10-23 U20G：补余烬新确认事实差集、精确事实保护和重复分析的进展语义，累计 53 组；联合 Main 与重启回归见最新报告。正式失败发布及自然玩家路线等仍待复核，未提前提升区域完成计数。详见 GODOT_TO_UNITY_PHASE_U20G_REPORT_2026-10-23.md。

## 1. 比较范围与证据边界

- Godot 权威工作区：`C:/Users/18485/Documents/GodotProjects/Astral-Element-Sea`，HEAD `cd9626be38cb2d007e2e07c29e19a0e5b8b532f3`；本轮检查时 git 工作树为空。
- Unity 工作区：`C:/Users/18485/Documents/UnityProjects/elemental-galaxy-core-fracture`，HEAD `b303679179c8d32ea98f527b6682ffe5f73414ac`，包括现有 U1–U10 未提交修改及本轮修改。
- 比较的是 Godot 当前实际代码与可调用合同，不把旧审计报告中的完成结论当作当前事实。Godot 自身的 `DESIGN_TBD`、事件未发布和无正式获取路线等缺口，单独记为源项目限制。
- Unity 当前拥有 17 区域、40 正式反应、15 正式设备、33 加工配方的数据导出；**这些是目录数量，不是运行功能完成率**。
- 本文是第一版系统分解，不声称已穷举每一个 Godot 函数。后续发现新功能应加入原系统分母，保留历史记录，不删掉缺失项来提高比例。
- U10 的 18/18 测试是历史局部合同证据；本轮继续执行 Unity 验证并增加新状态连续服务路线测试。测试原料采集和区域切换的服务模拟，不等于鼠标键盘实际通关。

## 2. 完成度与验收规则

对每个系统建立功能项：稳定 ID、Godot 来源、Unity 对应实现、玩家入口、存档要求、正/反向测试和实际运行证据。每个功能项保留以下状态：

| 状态 | 含义 | 计入正式通过分子 |
| --- | --- | --- |
| VERIFIED | 实现对齐、玩家入口可用、持久化适用时通过、验证证据齐全 | 是 |
| IMPLEMENTED | 实现与部分合同验证存在，玩家等价性仍待验收 | 否 |
| PARTIAL | 只迁移部分行为或仍是简化替代 | 否 |
| MISSING | 查无对应运行功能或已确认缺失 | 否 |
| NOT_VERIFIED | 现有证据无法确定等价程度 | 否 |
| SOURCE_GAP | Godot 本身无完整实现，记录边界与相同表现 | 不混入已实现功能；必须说明理由 |

正式完成率 = 系统内 VERIFIED 功能项 / 系统内适用功能项 ×100%。功能项默认等权，必须先冻结范围并记录新增项，不能临时拆分已完成项来凑比例。

**额外硬门槛**：新存档不能软锁；核心玩家入口不能缺失；不能出现库存/能量重复扣除或复制；不能因迁移丢失进度；本系统要求的实际运行、失败分支和恢复验证必须完成。即使计数达到 90%，任一硬门槛未满足仍不验收。

目前没有任何系统完成上述正式验收，因此不沿用旧报告中粗略的 73% 或据此推算新的总体百分比。下表给出可核实的实现状态，不把“未验收”伪装成“代码完全没做”。

## 3. 当前逐系统对应与差距

| ID | 系统 | Godot 主要来源（src/） | Unity 主要对应（Assets/Scripts/） | 当前状态 | 阻止达到 90% 的主要内容 |
| --- | --- | --- | --- | --- | --- |
| RGN | 世界与 17 区域 | world/world_content_catalog.gd、region_runtime_contract.gd、region_route_contract.gd、region_streaming_contract.gd | GameBootstrap.cs、RegionEnvironmentService.cs、DataModels.cs | PARTIAL | 区域数据齐全；连续地形、真实邻接通行、分段碰撞/视觉就绪、返回与区域重入验收不足 |
| TER | 地形与采掘放置 | world/terrain_model.gd、mineable_tile_layer.gd、mining/* | TerrainRuntimeService.cs、TerrainWorldView.cs、WorldDropService.cs、GameBootstrap.cs | PARTIAL | U12格子挖放/计时/碰撞/增量恢复/64身份，U14新增实体掉落、来源身份、近身拾取和掉落快照；仍缺完整种子地形/洞穴/结构/实体矿脉生成、正式变体及全玩家验收 |
| PLY | 玩家移动与能力 | player/player_motor.gd、player_character.gd、abilities/ability_state.gd | GameBootstrap.cs、CharacterSprite.cs、TerrainRuntimeService.cs | PARTIAL | U12新增0.75×2格玩家占用与分步碰撞；仍缺悬浮/能耗/能力解锁和Godot全套通行与运动状态等价验收 |
| SUR | 生存与环境 | survival/survival_state.gd、session/world_session.gd | RegionEnvironmentService.cs、GameState.cs | PARTIAL | 有辐射与防护、腐蚀伤害；缺医疗包状态/入口、有限值完整防护、保护因子、死亡恢复等价验收 |
| INV | 库存、物品与采集 | inventory/inventory_model.gd、gathering/capture_source_model.gd、content/item_contract_catalog.gd、material_property_contract.gd | GameState.cs、FiniteResourceService.cs、WorldDropService.cs、WorldChemistryService.cs、GameBootstrap.cs | PARTIAL | U14新增区域每资源容量6/跨入口共享/耗尽恢复、氢源2次各1份、库存拒收不扣源/不删掉落；旧石矿点仍是有限兼容采样器。完整源生成、统一物品属性、源int64上限/聚合删除及玩家验收未对齐 |
| CBT | 战斗与敌人 | combat/combat_model.gd、session/world_session.gd、world/sandbox_world.gd | CombatRuntimeService.cs、CombatEnemyVisual.cs、GameBootstrap.cs、combat_catalog.json | PARTIAL | U30已接入15类正式参数、全局存活生成规则、追击与辐射加成、范围/冷却、鼠标点击12伤害、唯一库存奖励、轮廓/血条与存档；仍缺攻击动作与音效、活动记录、完整自然键鼠路线/死亡恢复与17区域呈现对比，未正式90%验收 |
| CHM | 化学反应与世界化学 | chemistry/full_reaction_service.gd、phase2_chemistry_service.gd、world/reaction_content_catalog.gd | ReactionService.cs、ChemistryEngine.cs、WorldChemistryService.cs、SpecializedDeviceChemistryService.cs、ore_profiles.json | PARTIAL | 有40反应与多条加工链；U24补权威八矿档案、六矿识别预处理、四金属还原、置换/无反应与沉淀及选材入口；仍缺完整区域效果、全部溶液/离子/处理分支、完整次数/原始品位与产率元数据/实验记录及全玩家验收 |
| DEV | 15 设备与加工 | progression/formal_device_catalog.gd、device_controller.gd、device_instance_state.gd、phase2_recipe_catalog.gd | DeviceRuntimeService.cs、GameUI.cs、SpecializedDeviceChemistryService.cs | PARTIAL | 有目录、制造/放置/加工/领取/储罐；缺完整取消语义、样本/工艺输入选择、全部分支与各设备真实玩家路线验收 |
| PRG | 实验室与装备进阶 | progression/laboratory_progress_state.gd、underground_progress_state.gd、hand_assembly_model.gd、tools/mechanical_tool_catalog.gd | LaboratoryProgressionService.cs、MechanicalToolService.cs、GameBootstrap.cs | PARTIAL | 有Stage0→3和3档镐；缺完整手工装配语义、配方发现和可达性验收。本轮发现并修复访问历史不持久化 |
| AGT | Agent 与区域行为 | agent/agent_decision_policy.gd、agent_controller.gd、agent_context.gd、region_agent_behavior_profile.gd、cross_region_recall.gd、区域adapter | AgentRuntime.cs、RustJungleAgentBehavior.cs、CrystalTundraAgentBehavior.cs、SaltMarshAgentBehavior.cs、EmberThermalAgentBehavior.cs、SandDesertEntryBehavior.cs、SandMaterialBehavior.cs、FluorescentGrottoAgentBehavior.cs、GeodeAbyssAgentBehavior.cs、AbandonedMineAgentBehavior.cs、MagneticPlateauEntryBehavior.cs、MagneticMaterialBehavior.cs、RadiationBeltAgentBehavior.cs、MantleFlowAgentBehavior.cs、CoreShellAgentBehavior.cs、PlanetaryCoreAgentBehavior.cs、GameBootstrap.cs、agent_catalog.json | PARTIAL | 首轮专属核心实施13/13（非正式验收比例）；正式失败/多步/行星核条件与突破生产者/磁暴身份与redox/晶洞纯度源缺口、盐溶液分析源缺口、完整地形/采集前扫描、冻原温变/结晶、锈蚀高级变体/替代成功、完整上下文/事件时机/视觉及全玩家验收仍缺 |
| NAR | 任务、碎片、记忆与结局 | prologue/*、dialogue/*、session/world_session.gd | GameBootstrap.cs、CoreProgressionService.cs、GameState.cs | PARTIAL | 核心反应链与双结局合同已有；碎片信任过程、记忆序列重构、任务与实验室真实进阶、返回后交互仍简化或待验证 |
| UIX | UI、图鉴、地图与输入 | ui/gameplay/*、ui/world_select/*、ui/touch/*、input/*、codex/codex_state.gd | GameUI.cs、GameBootstrap.cs、Localization.cs | PARTIAL | 面板/快捷键已有；图鉴交互、真实地图探索、暂停语义、触控与设备输入路由、背包物品操作与自适应未对齐 |
| ART | 美术、动画、音效与体验 | world/environment_art_catalog.gd、biome_background_controller.gd、agent/lumen_visual_state.gd、audio/prologue_audio_director.gd | ArtLib.cs、WorldArt.cs、CharacterSprite.cs、AmbientParticles.cs、WavLoader.cs | PARTIAL | 素材加载已有；需17区域同场景对比、相机/角色/地面比例、Lumen状态、区域背景混合和事件音频验收；素材存在不等于呈现等价 |
| SAV | 存档、世界槽与恢复 | save/save_repository.gd、save_codec.gd、save_snapshot_builder.gd、ui/world_select/* | SaveRepository.cs、GameState.cs、GameUI.cs、TerrainRuntimeService.cs、AgentRuntime.cs | PARTIAL | U13原子存档、U14采集/掉落、U15正式目录兼容、U16基础Agent快照；U17锈蚀、U18冻原、U19盐碱专属计数/事实/成熟/观察与能力状态及独立进程恢复，非法成熟与分离标记冲突拒绝；仍缺历史空间管理、完整元数据/索引、敌人及其他专属Agent状态、全生命周期验收；实机证据见分阶段报告 |
| RUN | 构建与真实运行验证 | project.godot、export_presets.cfg、tests/integration/*、tests/app/* | ProjectSettings/EditorBuildSettings.asset、Editor/UnitySliceBuilder.cs | IMPLEMENTED | 引擎锁定与可重复Windows构建已有；仍缺新存档完整交互路线、双结局UI和长期保存恢复的验收证据 |

## 4. 固定的第一批功能验收项

这些是工作项，不是以文件数计算的进度。每项后续在完成时补 Godot/Unity 行号和运行证据；当前不预先打 VERIFIED 勾。

| 系统 | 第一批必须逐项验收的功能 ID |
| --- | --- |
| RGN | RGN-01 17区域权威ID与顺序；RGN-02 邻接/竖井/气闸/电梯；RGN-03 阶段/防护/准备门；RGN-04 分段就绪与安全出生；RGN-05 返回实验室和区域重入 |
| TER | TER-01 种子与区域地形；TER-02 有限方块采矿与正式掉落；TER-03 放置/占用/库存原子提交；TER-04 地形碰撞/光标/采矿进度；TER-05 增量保存与恢复 |
| PLY | PLY-01 地面/空中运动参数；PLY-02 地形碰撞与1×2空间；PLY-03 悬浮解锁/时长/能耗；PLY-04 交互与演出锁；PLY-05 采集范围与运动动画 |
| SUR | SUR-01 辐射/安全阈值/防护；SUR-02 环境腐蚀与危险状态；SUR-03 医疗包/治疗/清辐射；SUR-04 死亡与恢复；SUR-05 存档与非有限值失败防护 |
| INV | INV-01 原子增量/批量消耗/数值上限；INV-02 有限采集源/耗尽/失败不减容量；INV-03 统一物品分类/属性/数量显示；INV-04 分析与回收分离及来源身份；INV-05 库存与采集状态恢复 |
| CBT | CBT-01 正式敌人生成参数；CBT-02 追踪与辐射速度；CBT-03 攻击范围与冷却；CBT-04 受击/死亡/唯一掉落；CBT-05 玩家入口、呈现与存档 |
| CHM | CHM-01 40反应条件/原子提交；CHM-02 全部独立Phase2化学操作（排除别名重复）；CHM-03 区域反应效果；CHM-04 玩家输入/提示/实验记录；CHM-05 成功/失败/存档与回归 |
| DEV | DEV-01 15设备成本/发现/阶段；DEV-02 33配方路由及分支；DEV-03 部署/取消/预留/加工/领取；DEV-04 气液储存守恒；DEV-05 逐设备玩家输入与恢复 |
| PRG | PRG-01 手工装配与首台设备；PRG-02 Stage0腐蚀里程碑；PRG-03 Stage1→2三能力；PRG-04 Stage2→3九能力；PRG-05 三档镐、升级与持久化 |
| AGT | AGT-01 事件/上下文/决策/响应；AGT-02 17区域覆盖与明确generic合同；AGT-03 知识保护/抑制/发言预算；AGT-04 跨区回忆与存档；AGT-05 Lumen跟随/视觉/对话队列 |
| NAR | NAR-01 序章/任务正式转换；NAR-02 碎片信任/语言/密钥；NAR-03 记忆片段/序列重构；NAR-04 最终准备与5步核心链；NAR-05 双结局/唯一选择/重载 |
| UIX | UIX-01 主菜单/世界选择/暂停；UIX-02 背包/热栏/物品操作；UIX-03 合成/设备/实验/图鉴；UIX-04 小地图/地图/探索；UIX-05 键鼠/触控/输入设备/分辨率 |
| ART | ART-01 实验室构图/比例；ART-02 17区域背景/地形/混合；ART-03 玩家/Lumen/设备动画；ART-04 灯光/粒子/交互反馈；ART-05 音效/事件音频/同场景视觉证据 |
| SAV | SAV-01 多世界槽/列表摘要；SAV-02 编码/版本/校验和；SAV-03 临时写入/备份/回退；SAV-04 全部运行态快照（地形/位置/敌人/Agent等）；SAV-05 老存档兼容/异常恢复 |
| RUN | RUN-01 锁定引擎与Main构建场景；RUN-02 可重复编译/构建；RUN-03 新存档全流程玩家验收；RUN-04 双结局与中途重启；RUN-05 运行日志/性能/发布目录 |

CHM-02、AGT-02、DEV-02、ART-02 等集合项在执行时必须展开为真实条目。宏观五项只是索引；正式90%统计必须用展开后的功能清单，不能靠宏观项平均判定通过。

## 5. 实施顺序

1. **U11 / P0：新存档与基础工艺连续路线**。修复区域访问持久化；补中和/沉淀玩家入口；修复资源点布局只展示前两项导致后续原料无法取得的问题，增加完整目录分页采集；构造从新状态开始、原料采集明确、设备必须真实制造的Stage0→3服务集成验证。随后进行GUI路线验收。
2. **U12 / P0：地形与玩家运动**。第一批格子地形、采矿/放置、碰撞与保存增量已实现，见U12报告。完整Godot生成器、真实掉落实体、正式变体/洞穴结构与全套通行验收继续保留，尚未签署90%。
3. **U13 / P0：保存恢复**。多槽、原子写入、校验、两级备份回退和旧Unity档兼容已实现，见U13报告；全运行态快照、完整索引/元数据及生命周期验收继续保留，尚未签署90%。
4. **U14 / P1：Agent完整迁移**。先基础事件与策略，再各区域Profile/adapter与generic合同，之后知识保护、回忆、发言队列和视觉。
5. **U15 / P1：战斗、生存与能力**。迁移当前Godot实际敌人/伤害/掉落/恢复/悬浮合同，接入玩家入口和存档。
6. **U16 / P1：化学、设备与进阶分支清单闭合**。展开每条独立服务函数和33配方分支，补输入选择、反应效果与完整材料路线。
7. **U17 / P1：叙事与UI**。碎片信任、记忆序列、图鉴/地图/实验记录、暂停和触控。
8. **U18 / P1：美术音频等价与逐系统最终验收**。17区域相同地点/状态对照，真实新存档双结局路线，逐系统达到90%后签收。

阶段编号是后续工作安排，不代表这些阶段已经实现。若P0证据表明顺序需要调整，在本表保留原因。

## 6. 源项目边界

- `combat/combat_model.gd` 是实际存在的战斗实现；需要迁移当前内容，不将“完整商业游戏敌人生态”臆造为Godot已有功能。
- `region_streaming_contract.gd` 明确说明当前Godot是预生成确定地形，分段状态表达就绪；迁移其就绪合同，不擅自把目标扩大为另一套无限异步世界。
- `phase2_chemistry_service.gd` 有别名函数。化学分母应按独立行为去重，不能将同一功能的三个名字算三次完成。
- Godot最高镐具与planetary_core采矿抗性要求的差距保留SOURCE_GAP；不能为提高完成率编造更高级配方。
- Phase S/T已记录的正式压力/催化/多步骤事件发布backlog不以Unity测试通过宣布已补完；迁移审计需持续区分玩法实现与事件发布缺口。

## 7. 本轮交付与后续更新方式

- 当前基线和75个宏观功能索引已经建立；15个系统均尚未签署≥90%正式验收。
- 本轮代码变更与实际验证结果见 `GODOT_TO_UNITY_PHASE_U11_REPORT_2026-10-18.md`。
- 未来每次交付必须写“本轮完成哪些ID、用什么证据、仍缺哪些ID”，并给出各系统状态。功能实现、服务集成测试、实际玩家操作与视觉证据分开记录。

`PER_SYSTEM_TARGET = 90%`

`SYSTEMS_TRACKED = 15`

`MACRO_ACCEPTANCE_ITEMS = 75`

`SYSTEMS_FORMALLY_ACCEPTED_AT_90 = 0`

`MIGRATION_90_ACCEPTANCE = NOT_READY`
