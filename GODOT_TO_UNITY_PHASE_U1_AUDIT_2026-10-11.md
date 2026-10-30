# 《元素星海·裂核》Godot → Unity 完整迁移 Phase U1 审计

审计日期：2026-10-11  
审计性质：只读功能差异审计  
Godot 权威项目：`C:\Users\18485\Documents\GodotProjects\Astral-Element-Sea`  
Unity 迁移项目：`C:\Users\18485\Documents\UnityProjects\elemental-galaxy-core-fracture`

## 1. 结论

Unity 项目已经是一个**可编译、可构建、可启动，并能演示主线骨架的垂直切片**，但还不是 Godot 正式玩法的等价移植。

按“Godot 当前真实代码能力是否在 Unity 中形成可运行、可保存、可验证的等价闭环”计算：

```text
GODOT_TO_UNITY_FUNCTIONAL_PARITY = 37%
REMAINING_MIGRATION = 63%
CURRENT_UNITY_STATE = PLAYABLE_VERTICAL_SLICE
FULL_GODOT_PARITY = NO
FULL_PLAYTHROUGH_PARITY_VERIFIED = NO
```

这个 37% 不是文件复制率，也不是 Unity 编译通过率。17 张背景或 40 条 JSON 配方存在，只能证明内容已带入；只有当运行规则、失败路径、进度门、存档和测试也接通时，才计为完整迁移。

### 分层进度

| 口径 | 进度 | 判断 |
| --- | ---: | --- |
| 数据与 ID 搬运 | 74% | 17 区域、16 碎片、16 记忆、40 反应、54 物质、207 条权威 CSV 词条已进入 Unity；仍缺 Godot 的完整目录语义和权威导出校验 |
| 美术与音频搬运 | 75% | 17 背景、272 瓦片、80 角色帧、4 引路人帧、12 WAV 已带入；正式装置美术、完整演出和效果接线不足 |
| 主线垂直切片 | 58% | 菜单、玩家、实验室、区域移动、合成、碎片、记忆、双结局骨架可运行 |
| 正式 Gameplay 系统等价度 | 25% | 装置、Agent、世界运行时、地形、采矿、战斗、基地、条件化学和存档契约差距最大 |
| 自动验证覆盖 | 18% | Unity 只有 6 项冒烟测试；Godot 当前有 155 个 GDScript 测试文件 |
| 综合加权迁移进度 | **37%** | 以玩家真实功能等价为主，不以资源数量或编译成功率代替 |

## 2. 审计基线

### Godot 权威侧

- 分支：`main`
- 当前提交：`cd9626b chore: publish complete deployable project`
- 工作区：审计时干净
- `src/`：180 个 `.gd`
- `tests/`：155 个 `.gd`
- 场景：41 个 `.tscn`
- Resource：17 个 `.tres`
- 正式世界：17 区域
- 正式装置：15 台
- Agent：29 个脚本文件，包括决策、上下文、Profile、跨区回忆和区域适配器

### Unity 迁移侧

- 分支：`main`
- 基线提交：`b303679 元素星海·裂核 — Unity 迁移垂直切片`
- `Assets/Scripts`：15 个 `.cs`（含 Editor 构建脚本）
- 数据：8 个 JSON
- 美术：386 个 PNG，其中 17 张区域背景、272 张区域瓦片、80 帧角色、4 帧引路人
- 音频：12 个 WAV
- 正式场景：`Assets/Scenes/Main.unity`
- 当前存在 6 个未提交的既有修改文件；本审计没有覆盖或回滚它们

已验证的 Unity 基线：

- Unity 版本锁定：`2022.3.62f3c1`
- 数据/逻辑冒烟测试：6/6 PASS
- Windows x64 构建：PASS，errors=0
- 独立播放器启动：PASS
- 运行日志异常扫描：PASS

## 3. 状态定义

| 状态 | 定义 |
| --- | --- |
| `MIGRATED` | Godot 功能在 Unity 中已有等价运行逻辑、失败路径、持久化和验证证据 |
| `PARTIAL` | 已有数据、画面或简化玩法，但缺少 Godot 正式契约的一部分 |
| `MISSING` | Unity 中没有对应的正式运行实现 |
| `BLOCKED` | 依赖的上游系统缺失，当前无法完成真实闭环验证 |
| `VERIFIED` | 已通过当前 Unity 的自动或真实运行验证；它是验证标签，不替代迁移状态 |

## 4. 总功能对应表

| 功能域 | Godot 权威能力 | Unity 当前能力 | 状态 | 关键缺口 |
| --- | --- | --- | --- | --- |
| 项目启动与构建 | Godot 正式入口、导出与测试工具 | `Main.unity`、锁定 Unity 版本、可重复 Windows x64 构建 | `MIGRATED` | 尚未建立发布安装包和目标平台矩阵 |
| 标题/世界选择/加载 | 正式菜单、世界槽、加载状态、恢复信息 | 标题、世界选择、1.4 秒加载、键盘保底入口 | `PARTIAL` | 没有 Godot 多世界索引、世界 seed、存档摘要和恢复状态 |
| 权威数据/本地化 | 完整 Catalog、资源身份、反应内容、当前权威中文词条 | 8 个旧 JSON 快照 | `PARTIAL` | 审计时仍缺确定性导出、设备目录和运行时引用校验；当前权威 CSV 实为 207 条 |
| 玩家移动/相机/输入 | 移动、跳跃、采矿输入、建造输入、触控、环境协调 | WASD、跳跃、地面检测、相机、交互 | `PARTIAL` | 无触控、挖掘保持、建造输入、正式能力/工具约束 |
| 17 区域目录 | 17 正式区域及地表/地下路线 | 17 ID、名称、背景、瓦片、区域切换 | `PARTIAL` | Unity 使用固定 80×40 示意布局，不是 Godot Region Runtime/Streaming/Layout 契约 |
| 地形与世界生成 | 272 Block 设计、Biome/Terrain Profile、streaming、terrain delta | 背景图、区域瓦片和简化地面带 | `PARTIAL` | 无真实块身份、矿脉生成、地形 delta、挖除/放置、流送生命周期 |
| 采集/采矿/资源身份 | 资源节点、Block Item、分析、回收、矿镐能力和环境抗性 | 靠近资源点按 E，直接增加物质 | `PARTIAL` | 无持续采矿、矿镐等级、硬度、耐热/耐腐蚀/绝缘/辐射检查、分析回收闭环 |
| 库存/工具 | 正式库存、工具制造/装备、原子性交易 | 物质计数、背包显示、配方扣除 | `PARTIAL` | 无工具物品、装备栏、部署件、正式原子性服务和失败回滚测试 |
| 基础化学 | ReactionService、FullReactionService、preview/result | 40 配方精确多重集匹配与产物发放 | `PARTIAL` | 40 个内容单元已核实；仍缺统一 preview/result、事件发布和正式失败原因 |
| 条件化学 | 温度、压力、催化、电、湿度、氧气、浓度等条件 | JSON 中有简化文本；运行只匹配反应物 | `PARTIAL` | 条件未参与 Unity 反应判定；能量不足也没有在执行前阻止反应 |
| 多步/工业化学 | 多步反应、异步 processing、输出缓冲、气液储存 | 无正式工业处理状态机 | `MISSING` | 缺 `PROCESSING → OUTPUT_READY`、输入预留、产物领取、气液状态和批量反应 |
| 15 正式装置 | 制造、放置、解锁、配方、实例状态、UI、存档 | 实验室有工作台/合成仪等少量交互道具 | `PARTIAL` | 没有 FormalDeviceCatalog、DeviceController、DeviceInstanceState 和 15 台正式闭环 |
| 实验室 Stage | Stage 0→1→2→3 能力门与里程碑 | 12 步简化 questStep | `PARTIAL` | 缺能力授予、正式 Stage 门、设备制造门和阶段状态持久化 |
| Agent 架构 | DecisionPolicy、Controller、Context、Profile、Recall、事件、知识守卫 | 只有引路人视觉伴随和预写对话/碎片选项 | `MISSING` | 整个 Agent 决策与 17 区域行为层未迁移 |
| 碎片/记忆/对话 | ShardState、MemoryReconstruction、DialogueState、Agent presentation | 16 碎片语言选择、16 记忆展示、打字机对话 | `PARTIAL` | 简化为一次选择/解锁；无信任过程、知识边界、事件队列和完整重构状态 |
| 环境/生存 | RegionEnvironmentRuntimeState、区域条件、辐射与防护 | HP、Energy、Radiation；按区域持续加辐射 | `PARTIAL` | 无防护升级、局部环境状态、持续化学变化和多危险类型 |
| 战斗 | 敌人生成、追踪、攻击、伤害、掉落和持久化模型 | 未发现敌人或 Combat 实现 | `MISSING` | CombatModel 全部未迁移 |
| 基地/建造 | 放置/移除方块、工作站、设备、电梯条件 | 固定世界与固定设备；电梯为直接传送 | `MISSING` | BaseModel、建造模式、方块/设备放置和持久化未迁移 |
| 图鉴/实验记录 | CodexState、知识/发现/实验历史 | 只有 discoveredReactions 数组和计数 | `PARTIAL` | 无图鉴 UI、实验条件/现象/失败历史、知识来源与查询 |
| 存档 | schema、checksum、双备份恢复、世界索引、完整 snapshot | 单 JSON，保存少量字段，30 秒自动保存 | `PARTIAL` | 无 schema/checksum/备份恢复；不保存地形、设备、Agent、敌人、工具、环境和完整世界状态 |
| HUD/面板 | 正式 HUD、设备 UI、地图、图鉴、触控、建造、菜单 | HUD、背包、合成、对话、记忆、结局、帮助 | `PARTIAL` | 地图按钮仍是提示；缺设备处理、图鉴、建造、触控和正式多分辨率验收 |
| 视觉/音频 | 区域视觉、角色、设备、反应现象、UI 和音频链 | 大部分背景/瓦片/角色帧、12 WAV 已带入 | `PARTIAL` | 正式装置图、反应特效、敌人、建造表现、音乐和完整演出未接通 |
| 核心与结局 | 前置设备/能力/记忆/Master Key 综合门，双结局 | 达到简化任务条件后显示双结局选择 | `PARTIAL` | 未经过 Godot 正式 Device12/15、核心准备和综合验证链 |
| 自动测试/发布 QA | 155 个 Godot 测试文件与大量专项报告 | 6 项冒烟测试、Windows 构建和启动扫描 | `PARTIAL` | 缺系统级/场景级/存档恢复/17 区域/15 装置/完整通关回归 |

## 5. 17 区域迁移审计

Unity 已拥有全部 17 个 `region_id`、对应背景和瓦片。因此没有“区域 ID 完全缺失”，但所有区域的正式运行契约仍是 `PARTIAL`。

| Region ID | 显示名 | 数据/背景/瓦片 | Unity 运行状态 | 主要缺口 |
| --- | --- | --- | --- | --- |
| `prologue_lab` | 废墟实验室 | 已迁移 | `PARTIAL` | 实验室主线可运行；缺正式设备制造/放置、世界 Session 和完整存档 |
| `dead_city_ruins` | 死城废墟 | 已迁移 | `PARTIAL` | 有地表区域与资源点；缺正式 Block Item、分析/回收和 Device01 bootstrap |
| `rust_jungle` | 锈蚀丛林 | 已迁移 | `PARTIAL` | 缺腐蚀环境运行态、Device04 路线和区域 Agent 行为 |
| `crystal_tundra` | 晶晶冻原 | 已迁移 | `PARTIAL` | 缺寒冷采矿约束、精确分析和 Device05 闭环 |
| `salt_marsh` | 盐碱滩涂 | 已迁移 | `PARTIAL` | 缺酸碱/腐蚀环境、蒸馏与正式路线门 |
| `ember_rift` | 余烬裂谷 | 已迁移 | `PARTIAL` | 缺高温环境运行态、冷凝链和热抗性 |
| `sand_desert` | 风沙荒漠 | 已迁移 | `PARTIAL` | 缺干旱环境过程、正式资源复用与区域 Agent |
| `magnetic_plateau` | 磁暴高原 | 已迁移 | `PARTIAL` | 缺电环境、电解设备、绝缘检查和区域 Agent |
| `topsoil_caves` | 表土洞穴层 | 已迁移 | `PARTIAL` | 只有深度带；缺洞穴结构、深度门和正式资源身份 |
| `fossil_strata` | 化石岩层 | 已迁移 | `PARTIAL` | 缺化石/碳酸盐分析、沉淀过滤链 |
| `fluorescent_grotto` | 荧光菌窟 | 已迁移 | `PARTIAL` | 缺低氧/发酵过程和气体储存链 |
| `geode_abyss` | 晶洞深渊 | 已迁移 | `PARTIAL` | 缺自然结晶现象、晶体资源身份和正式 route gate |
| `abandoned_mine` | 废弃矿道层 | 已迁移 | `PARTIAL` | 缺矿石分析、冶炼、工业批量反应与正式矿脉 |
| `radiation_belt` | 辐射岩带 | 已迁移 | `PARTIAL` | 只有辐射数值；缺污染净化、吸收塔和防护进度 |
| `mantle_flows` | 地幔熔流 | 已迁移 | `PARTIAL` | 缺极端高温、工业结晶和热抗性采矿 |
| `core_shell` | 核心外壳 | 已迁移 | `PARTIAL` | 缺高强度材料链、Device12 高压催化和正式 Agent 逻辑 |
| `planetary_core` | 星球核能核心 | 已迁移 | `PARTIAL` | 有简化终端/结局；缺 Device15、核心准备状态和正式综合考验 |

结论：

```text
REGION_ID_COVERAGE = 17/17
REGION_ART_COVERAGE = 17/17
REGION_FORMAL_RUNTIME_PARITY = 0/17 FULL, 17/17 PARTIAL
```

## 6. 15 正式装置迁移审计

| # | Godot 正式装置 | Unity 状态 | 证据/缺口 |
| ---: | --- | --- | --- |
| 01 | 基础化学工程工作台 | `PARTIAL` | 有工作台交互，但只是直接推进修镐任务；没有正式制造/放置/配方系统 |
| 02 | 小型化学冶炼熔炉 | `MISSING` | 无热处理装置运行态 |
| 03 | 腕戴式合成仪充能台 | `MISSING` | 休眠舱可直接补能，但不是正式 Device03 |
| 04 | 基础化学反应台 | `PARTIAL` | 有通用合成仪和精确配比合成；无正式设备实例、条件和异步输出 |
| 05 | 多功能化学检测仪 | `MISSING` | 无样品选择和 Knowledge 写回 |
| 06 | 工业实验室蒸馏器 | `MISSING` | 无挥发物/残渣输出 |
| 07 | 工业电解槽 | `MISSING` | 无电解、环境电条件和能力授予 |
| 08 | 独立工业冷凝器 | `MISSING` | 无气相白名单和相变过程 |
| 09 | 工业固液分离过滤器 | `MISSING` | 无浆液/沉淀/滤液过程 |
| 10 | 高压气体储罐 | `MISSING` | 无气体 ID、容量和取回 |
| 11 | 大型工业反应釜 | `MISSING` | 无批量反应和工业输入输出 |
| 12 | 高级高压催化反应器 | `MISSING` | 无压力、温度、催化剂判定和核心前置 |
| 13 | 工业气体吸收塔 | `MISSING` | 无气体净化 |
| 14 | 工业结晶器 | `MISSING` | 无浓度/温度结晶和母液回收 |
| 15 | 大型工业液体储罐 | `MISSING` | 无液体状态、容量、存取和核心前置 |

```text
DEVICE_FULLY_MIGRATED = 0/15
DEVICE_PARTIAL = 2/15
DEVICE_MISSING = 13/15
```

## 7. Agent 迁移审计

Godot 中已经存在：

- `AgentDecisionPolicy`
- `AgentController`
- `AgentContext`
- `RegionAgentBehaviorProfile`
- `CrossRegionRecall`
- `AgentEvent`
- `AgentDialogueQueue`
- `AgentPresentationState`
- 确定性 Response Provider
- 多个区域 thin behavior adapter

Unity 搜索不到上述架构的 C# 对应实现。当前引路人机器人动画、预写对话和碎片选项不能视为 Agent 系统迁移。

```text
AGENT_ARCHITECTURE = MISSING
AGENT_REGION_PROFILE = MISSING
AGENT_BEHAVIOR_ADAPTERS = MISSING
CROSS_REGION_RECALL = MISSING
KNOWLEDGE_GUARD = MISSING
```

## 8. 存档差异

Unity 当前保存：

- `questStep`
- 已平息碎片
- 已重构记忆
- 已发现反应
- HP / Energy / Radiation
- 当前区域
- 结局选择
- 物质库存

Godot 另外具有或要求保存：

- schema/content/generator version
- checksum
- 双备份轮换与损坏恢复
- 世界槽与摘要
- 区域发现、环境运行状态
- 地形 delta
- 已放置方块/工作站/设备
- 设备 active recipe、reserved inputs、elapsed/duration、output buffer、storage
- 工具、能力、实验室 Stage、知识/图鉴
- Agent 上下文、区域记忆与呈现状态
- 敌人和掉落物状态

因此 Unity 存档属于可运行切片存档，不是 Godot 存档契约的等价实现。

## 9. 加权进度算法

| 功能域 | 权重 | Unity 等价度 | 加权得分 |
| --- | ---: | ---: | ---: |
| 项目启动与构建 | 3% | 100% | 3.00% |
| 权威数据与本地化 | 7% | 80% | 5.60% |
| 菜单与加载 | 3% | 80% | 2.40% |
| 玩家/输入/相机 | 5% | 60% | 3.00% |
| 世界区域与路线 | 7% | 45% | 3.15% |
| 地形/采矿/资源 | 8% | 25% | 2.00% |
| 库存/工具 | 4% | 35% | 1.40% |
| 化学系统 | 10% | 35% | 3.50% |
| 装置与 Stage | 12% | 15% | 1.80% |
| Agent | 8% | 5% | 0.40% |
| 对话/碎片/记忆 | 5% | 50% | 2.50% |
| 战斗与基地 | 7% | 0% | 0.00% |
| 环境与生存 | 4% | 25% | 1.00% |
| 存档 | 5% | 30% | 1.50% |
| UI/图鉴 | 4% | 55% | 2.20% |
| 美术与音频 | 3% | 75% | 2.25% |
| 核心与结局 | 3% | 45% | 1.35% |
| 测试与发布 QA | 2% | 30% | 0.60% |
| **总计** | **100%** |  | **37.05% ≈ 37%** |

## 10. 下一阶段执行顺序

### U2-A：建立自动化权威数据导出

1. 不再手工维护旧 JSON 快照。
2. 从 Godot 当前 Catalog 导出区域、反应、资源身份、装置、Stage、路线和环境条件。
3. Unity 导入时校验 ID、数量、引用和版本。
4. 锁定 40 个 ReactionSpec 与 40 个 ReactionContent 单元的一一对应关系。

### U2-B：先迁移共享领域模型

优先建立不依赖画面的 C# 层：

- ReactionResult / Preview / Event
- Region runtime/profile
- Inventory atomic transaction
- LaboratoryProgress / Capability
- DeviceDefinition / DeviceInstanceState / DeviceController
- Save envelope/schema/backup

### U3：迁移世界、采矿和区域闭环

1. 精确复刻 17 区域路线和布局契约。
2. 实现 Block Identity、矿脉、挖掘进度、工具能力与 Terrain Delta。
3. 每区完成进入、退出、采矿、保存、加载测试。

### U4：迁移 15 装置和条件化学

按依赖顺序迁移：01→04→05/06/09→02/08/10→07/11→13/14→12/15。

每台设备必须同时具备：制造、放置、输入、条件、异步状态、输出、失败、存档、UI 和测试。

### U5：迁移 Agent

先迁移核心五件套，再迁移区域 Profile/adapter，最后接入真实 Gameplay Event。不要把预写提示当作 Agent 完成。

### U6：补齐战斗、基地、图鉴和完整存档

这些系统当前是主要空白，完成前不能宣称 Godot 内容已全部迁移。

### U7：完整通关与发布验收

最终必须从新游戏经 17 区域、15 装置、核心考验到两个结局各完成一次，不使用调试跳转或手工改存档。

## 11. 本次审计边界

- 没有修改 Godot 玩法源代码。
- 没有修改 Unity Gameplay 源代码。
- 没有重构现有 Unity 垂直切片。
- 没有 reset/clean/checkout 或覆盖既有工作区。
- 只新增本审计 Markdown 文档。
- Unity 的 6 项冒烟测试证明当前切片基线稳定，但不等于功能等价。

## 12. Phase U1 最终状态

```text
PHASE_U1_AUDIT = COMPLETE
AUTHORITATIVE_GODOT_PROJECT = FOUND
UNITY_MIGRATION_PROJECT = FOUND
UNITY_COMPILE_BASELINE = PASS
UNITY_WINDOWS_BUILD_BASELINE = PASS
UNITY_RUNTIME_START_BASELINE = PASS
GODOT_TO_UNITY_FUNCTIONAL_PARITY = 37%
MIGRATION_GAPS_FOUND = YES
NEXT_PHASE = U2 AUTHORITATIVE DATA AND DOMAIN CONTRACT MIGRATION
```
