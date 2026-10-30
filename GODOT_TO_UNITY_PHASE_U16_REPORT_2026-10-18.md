# Phase U16 — Agent 基础链路、区域入场与只读引导

日期：2026-10-18。

后续更新：U17 已接入锈蚀丛林专属 adapter 的核心链路，见 `GODOT_TO_UNITY_PHASE_U17_REPORT_2026-10-18.md`。下文保留 U16 当时的范围和计数，不代表 U17 最新状态。

## 本轮结果

Unity 引路人不再只有外观跟随和固定终端对话：已接入事件、只读上下文、决策、确定性响应、优先对话队列和基础提示状态存档。正式区域的 14 份 Profile 均能路由入场提示，另外 3 区明确使用源项目通用策略。

**这不是 Agent 全量迁移完成。13 个源专属 adapter 的深层分支尚未迁移；导出 Profile 或静态台词不等于对应策略完成。没有任何系统据本轮测试签收为 ≥90%。**

## 源码与边界

| Godot 权威来源 | Unity 对应 | 本轮范围 |
| --- | --- | --- |
| agent_event.gd / agent_controller.gd | AgentController、GameBootstrap 事件连接 | 采矿、拾取、分析、制造/放置、反应和失败通知；事件去重、顺序和基础注意状态 |
| agent_context.gd | AgentContext | 分离的已分析物品、访问区域、设备、库存身份与已导出成分事实副本；不传入可变 Gameplay 服务 |
| agent_decision_policy.gd | AgentDecisionPolicy | 通用早期提示、分析保护、回收、工作台里程碑、成熟抑制、冷却/UI抑制/发言预算 |
| region_agent_behavior_profile.gd | AgentCatalog / RegionAgentBehaviorProfile | 14 份源定义数据、反应键、预算、视觉映射与入场；完整定义作为 JSON 保存供后续迁移核对 |
| deterministic_agent_response_provider.gd | DeterministicAgentResponseProvider | 214 条直接返回的静态台词导出；动态文本分支不猜测，不连接外部 LLM |
| agent_dialogue_queue.gd | AgentDialogueQueue | 优先级、警告抢占、原提示恢复、面板暂停；基础 UI 显示 |
| cross_region_recall.gd | CrossRegionRecall | 已确认事实与访问区域的只读查询基础，**尚无完整主动回忆策略** |
| progression/scrap_catalog.gd | AgentAnalysis / AgentContext | 6 种死城方块的正式成分事实；其他物品未有对应事实时不推断 |

Godot 仓库保持只读，未修改化学、设备、环境、地形的源定义。已有源项目 pressure/catalysis/condition 和 multi-step 正式事件 backlog 未修复。接入的是 Unity 既有服务的真实结果与通知，不捏造区域专属的条件标志、设备能力或化学结论。

## 已接入的行为

- 新状态唤醒提示；14 区各自的权威入场 ID 和台词；回访不重复入队。
- 对未知且具备正式分析合同的采集样本给出分析提示，不依据名称、颜色或外观说出成分。
- 实际分析成功后从已确认记录提取源事实。伪造“已分析”事件不能代替 Gameplay 分析；只有识别标记、没有已迁移成分事实时保持沉默。
- 死城早期含铁、含铜、玻璃等分析与回收提示；重复开采/分析失败的基础梯度提示。
- 已确认制造基础化学工作台后庆祝并抑制早期通用引导；不能凭伪造事件授予设备或进度。
- 正式失败通知给出通用警告；优先级高于普通提示，仍受源预算/冷却限制。
- 面板打开时暂停并隐藏 Agent 台词，避免占用正常面板输入；独立 HUD 文本不覆盖原交互提示字段。
- 使用源视觉语义作基础颜色反馈；没有声称已移植完整 Lumen 八态、方向保持或状态动画。
- 正常帧内先确认区域，再分发采矿/拾取事件，减少边界事件沿用上一帧区域标签的问题。

决策阶段只提交 `GameState.agent`，不写库存、能量、配方发现、知识、设备、门槛或实验室进阶。制造、采样、分析等真实 Gameplay 操作仍由原服务执行，其正常状态变化不由 Agent 代办。测试用完整序列化比较（排除 Agent 自身状态）检查该边界。

## 17 区源覆盖与 Unity 当前边界

下表的“源 adapter”是 Godot 现状，不表示 Unity 已有对应深层实现。3 个 generic 区不是凭记忆假定，由正式区域表与源 Profile/通用策略核对。

| Region ID | Godot Profile | Godot 行为入口 | Unity 本轮 |
| --- | --- | --- | --- |
| prologue_lab | 通用 | AgentDecisionPolicy/Controller | 唤醒和通用基础；序章完整事件仍待补 |
| dead_city_ruins | 专属 | 通用决策中的死城逻辑 | 入场及早期分析/回收/工作台引导；配方可用等完整分支仍待补 |
| rust_jungle | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| crystal_tundra | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| salt_marsh | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| ember_rift | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| sand_desert | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| magnetic_plateau | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| topsoil_caves | 通用 | AgentDecisionPolicy/Controller | 明确通用路由，无自行编造的专属入场台词 |
| fossil_strata | 通用 | AgentDecisionPolicy/Controller | 明确通用路由，无自行编造的专属入场台词 |
| fluorescent_grotto | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| geode_abyss | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| abandoned_mine | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| radiation_belt | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| mantle_flows | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| core_shell | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |
| planetary_core | 专属 | 专属 adapter | 入场已接；深层分支未迁移 |

源计数精确对应：17 区 = 14 专属 Profile + 3 generic；14 专属 Profile = 13 专属 adapter + 1 通用决策内的死城 Profile。Unity 对专属区暂未支持的普通事件返回 `dedicated_branch_not_migrated`，不拿通用化学解释冒充区域策略。

## 保存与恢复

保存已入队反应 ID、最近 64 个事件 ID、事件顺序、冷却、逻辑时间、见过的物品、待分析/重复动作/分析失败计数和基础成熟状态。通过 U15 存档后端保存，继续支持本机 EFS 目录。

运行内时间以保存的逻辑时间为基准延续；关闭游戏的时间不计入冷却。已入队即记入 shown，与源 Controller 的抑制方式对应；对话队列及未播完台词不恢复，不保证重启后补播。13 个区域专属注意状态尚未迁移，也不声称已保存。旧 Unity 档不含 Agent 字段时可读取并初始化基础状态。

## 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 实际编译/合同测试 | 34/34 PASS | Logs/phase_u16_smoke.log |
| Windows 正式 Main 构建 | PASS，0 errors / 2 原有 warnings | Logs/phase_u16_windows_build.log |
| 实际区域/采矿/拾取/分析/失败事件连接 | PASS | Logs/phase_u16_scene_probe.log |
| 实际 HUD 台词、颜色反馈、警告优先级与面板暂停 | PASS | 同上 U16_AGENT_SCENE_PROBE |
| 正式目录磁盘保存/读档抑制 | PASS | 同上 |
| 两个顺序启动的独立游戏进程恢复 | PASS，唤醒提示不重播 | Logs/phase_u16_restart_write.log、phase_u16_restart_read.log |
| Agent/化学/地形源导出 --check | PASS | 三个 exporter |
| git diff --check | PASS | 工作区检查 |

新增检查包括：17 区可解析路由、14 入场与去重、3 generic 不发明台词、未确认/仅识别样本保护、上下文副本隔离、真实分析与制造里程碑、成熟抑制、未迁移区域边界、全局预算、警告抢占与冷却、暂停/恢复、事件去重和上限、策略不改传入状态、回忆不窃取其他区域事实、Agent 快照/坏状态拒绝、无 Agent 字段的旧 Unity 档兼容。

场景探针运行正式 Main 和真实 Windows 可执行文件，调用实际区域确认/采矿/拾取/化学服务，核对实际 UI Text 与 Lumen Renderer，并使用正式 persistentDataPath 下的唯一测试档。重启测试使用两个独立进程。它们不是人工完整通关，也不是 13 专属 adapter 或全美术效果验收。没有运行慢世界生成或 17 Region generation suite。

```powershell
python tools/export_agent_catalog.py --check
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile Logs/phase_u16_smoke.log
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u16-smoke -logFile "$PWD\Logs\phase_u16_scene_probe.log"
```

## 剩余工作与下一步

下一批先迁移锈蚀丛林专属的已确认腐蚀、分析、停滞与防腐能力引导，再逐区域迁移其余 adapter；同步补足真正可用的事实/条件上下文、成熟条件、主动跨区回忆、动态文本和完整视觉队列。不能只把 214 条静态台词接上就称策略完成。

总体仍按 15 系统、8 个大工作包跟踪。完整世界、玩家能力/生存/战斗、设备化学分支、叙事、UI、存档空间管理和全程验收仍在，不以 34 组测试推算完成百分比。

`PHASE_U16_AGENT_FOUNDATION = IMPLEMENTED_WITH_LIMITS`

`SOURCE_PROFILE_COUNT = 14`

`SOURCE_DEDICATED_ADAPTER_COUNT = 13`

`UNITY_PROFILE_ENTRY_ROUTES = 14`

`UNITY_EXPLICIT_GENERIC_REGION_ROUTES = 3`

`UNITY_DEEP_REGION_ADAPTERS_PORTED = 0/13`

`EXTERNAL_LLM = NOT_CONNECTED`

`AGENT_DECISION_GAMEPLAY_MUTATION = NONE`

`PHASE_U16_CONTRACT_CHECKS = 34/34 PASS`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
