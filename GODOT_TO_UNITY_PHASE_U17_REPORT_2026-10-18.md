# Phase U17 — 锈蚀丛林专属 Agent 与扫描入口

日期：2026-10-18。继续本机现有 Unity 项目，不重建、不回滚工作区。

后续增量：U18 已接入晶体冻原核心，详见 `GODOT_TO_UNITY_PHASE_U18_REPORT_2026-10-18.md`。本文的 1/13 和 38/38 是 U17 历史检查点，不代表最新总量。

## 已完成的本轮增量

新增并接入 `RustJungleAgentBehavior.cs`，不再仅使用 U16 的区域入场数据。专属策略读取分离的事件、环境快照和已确认分析事实，不接收可变 Gameplay 服务。

- 只有区域运行态已确认，才发锈蚀丛林入场观察。
- 只有实际抗性不足导致采矿拒绝，才由该入口发送腐蚀观察；普通环境观察不会自动累加腐蚀尝试次数。
- 首次腐蚀、重复抑制、3/6 次暴露梯度与至少 30 秒无进展条件已接入；阈值读取 Godot Profile 导出值。
- 已确认分析后解释新性质；分析前不凭区域或物品名称说出铁、氯。只有已识别、没有可用成分事实的记录仍保持沉默。
- 在已观察腐蚀后，结合真实分析说明机制；没有先前观察时只说明确认的材料性质。
- 接入 Godot 的动态含铁/含氯/两者/默认文本选择，文本直接从源函数导出，不手工续写化学结论。
- 防腐材料处理和镐具升级成功后，按实际库存、加工里程碑与工具抗性发布“可用/获得”提示；在实验室也能路由回锈蚀丛林策略。
- “已分析＋已理解腐蚀＋已获得防护”共同满足时进入成熟状态；不是仅凭 Stage 1、进入区域或拥有原料判定成熟。
- 真实防腐路线失败可推进专属失败梯度；确认的新进展会重置停滞计数。已有正式生锈反应成功可提供世界反应观察证据。
- 专属计数、已知性质、理解/防护/成熟状态与最后进展时间保存、校验并恢复。

决策和提示阶段只修改 `GameState.agent`。正常采集、扫描和加工对 Gameplay 的合法改动仍由既有服务或本轮移植的扫描方法执行；Agent 不替玩家发物品、不制造涂层、不改抗性、不授予进阶。

## 权威对应

| Godot 来源 | Unity 对应 | 说明 |
| --- | --- | --- |
| `src/agent/rust_jungle_agent_behavior.gd` | RustJungleAgentBehavior | 专属注意更新、决策、成熟/危险分支 |
| `src/agent/region_agent_behavior_profile.gd` | agent_catalog.json / RegionAgentBehaviorProfile | 事件组、反应键、hint阈值、成熟条件、预算/视觉映射 |
| `src/agent/deterministic_agent_response_provider.gd` | DeterministicAgentResponseProvider | 静态台词及 `_rust_analysis_text` 四分支 |
| `src/agent/agent_controller.gd` | GameBootstrap / AgentController | 采矿拒绝、确认分析、防腐可用/获得的归一化路由 |
| `src/world/chemical_resource_catalog.gd` | 导出的两种锈蚀区域样本事实 | 锈蚀金属残留 Fe/O、氯盐沉积 Cl/H/O，只在实际分析后使用 |
| WorldSession.scan_chemical_resource | WorldChemistryService.AnalyzeRustSample / 环境样本扫描节点 | 免费确认样本，不凭空产生材料，也不引入 Device 05 门槛 |

Godot 工作区仍无修改；化学配方、设备成本、抗性阈值与正式区域列表未修改。没有连接外部 LLM，也没有修 Phase S 的压力/催化/条件或 multi-step 正式事件 backlog。

## 新增玩家入口与迁移差异

锈蚀丛林新增“环境样本扫描 [E]”节点。玩家先从已有有限来源获得锈蚀金属残留或氯盐沉积，然后在节点确认持有样本；优先选未扫描的那一类。扫描不消耗库存，且未知类型、空库存或不在锈蚀丛林时拒绝。

**这仍不是完整 Godot 世界资源节点模型。** Godot 扫描的是世界中的 chemical resource node，并可在扫描后开放采集；当前 Unity 使用“已采集库存样本＋扫描节点”的适配。世界节点身份、扫描前后采集权限、资源生成及完整信息 UI 仍需后续迁移，不能把本入口称为扫描系统完全对齐。

环境快照由现有 Region/工具运行服务读取，并复制给 Agent；不会用区域名字猜测腐蚀是否活跃。新增高危险事件必须同时得到运行态确认，单独伪造事件标志不会越过成熟抑制。

## 已知分支缺口

| 项目 | 当前状态 |
| --- | --- |
| 腐蚀观察、分析、制作涂层、真实升级、成熟抑制 | 代码/针对性合同/实际场景均已接入 |
| 高级腐蚀变体与 30 秒警告冷却 | 已实现受确认保护的 adapter 分支并做合同测试；当前世界切片无真实高级变体发布，未冒称实机覆盖 |
| 世界腐蚀现象 | 可消费已有正式生锈反应成功；完整世界环境现象观察器尚未迁移 |
| alternative_solution_succeeded | 源 adapter 的额外替代方案成功重置尚未迁移；没有对应的真实 Unity 发布链，本轮不制造替代配方 |
| 资源扫描模型 | 有两个样本的可玩扫描入口；仍是库存样本适配，不是完整世界 node 模型 |
| 表现 | 基础台词队列与颜色语义已有；完整 Lumen 八态动画、音频与视觉对照仍待验收 |

因此，本轮只能称“首个专属 adapter 核心链路接入”，不能称锈蚀丛林或 Agent 系统已正式达到 ≥90%。

## 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 实际编译/合同回归 | 38/38 PASS | Logs/phase_u17_smoke.log |
| Windows 正式 Main 构建 | PASS，0 errors / 2 原有 warnings | Logs/phase_u17_windows_build.log |
| 实际采矿拒绝→有限采样→扫描回调→动态文本 | PASS | Logs/phase_u17_scene_probe.log |
| 实际加工基材/涂层→升级→跨区能力提示→成熟回访 | PASS | 同上 U17_RUST_SCENE_PROBE |
| 正式目录专属状态磁盘读档 | PASS | 同上 |
| 两个独立游戏进程恢复 | PASS，恢复成熟/理解/性质/防护状态 | Logs/phase_u17_restart_write.log、phase_u17_restart_read.log |
| Agent/化学/地形导出 --check | PASS | 三个 exporter |
| git diff --check | PASS | 工作区检查 |

新增四组测试覆盖：环境未就绪、首次/重复腐蚀、3/6 次及 30 秒门、被动观察不算尝试、伪造分析/能力/反应保护、动态铁/氯文本、真实无消耗扫描、扫描失败零副作用、真实防腐材料消费和 Stage 1 升级、实验室跨区路由、成熟回访抑制、经确认的新危险及冷却、失败阈值、真实反应后计数重置、专属快照和非法成熟状态拒绝。原 34 组检查继续通过。

实际 Main 探针调用真实的交互回调、采集/扫描/防腐服务和真实 UI Text，未直接注入实验室等级、涂层或抗性。修镐前置通过现有修复服务建立；角色移动使用探针定位，冷却使用明确的测试时钟偏移，**不是人工键鼠全程、不是真实等待 30 秒的通关测试**。30 秒边界另由确定性合同测试检查。

独立进程探针先在正式 persistentDataPath 下的唯一测试档完成真实防腐路线并保存，再启动第二个游戏进程恢复；同时回归 U15/U16 的库存、掉落、地形、玩家位置和一次提示抑制。普通玩家档未加载或改写，测试证据保留。没有慢世界生成或 17 Region generation suite。

复现实际场景：

```powershell
python tools/export_agent_catalog.py --check
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u17-smoke -logFile "$PWD\Logs\phase_u17_scene_probe.log"
$probeToken = [Guid]::NewGuid().ToString('N')
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u17-save-write',"--u17-token=$probeToken",'-logFile',"$PWD\Logs\phase_u17_restart_write.log") -WindowStyle Hidden -Wait
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u17-save-read',"--u17-token=$probeToken",'-logFile',"$PWD\Logs\phase_u17_restart_read.log") -WindowStyle Hidden -Wait
```

## 剩余范围

源专属 adapter 共 13 个，Unity 现在有 1 个接入核心链路，另 12 个尚未移植深层策略；**1/13 是实施计数，不是完成率或验收通过数**。本 adapter 上述缺口也继续保留，不从分母移除。

下一批优先晶体冻原：实际分析确认、低温/晶体条件引导、精密分析能力及对应成熟状态。主动跨区回忆、全体区域上下文、完整表现和其他系统仍待继续。整体仍按 15 系统、8 个大工作包跟踪，未给出未经逐项验收的百分比。

`PHASE_U17_RUST_AGENT_CORE = IMPLEMENTED_WITH_LIMITS`

`UNITY_DEDICATED_ADAPTER_CORES_CONNECTED = 1/13`

`OTHER_DEDICATED_ADAPTERS_NOT_PORTED = 12`

`PHASE_U17_CONTRACT_CHECKS = 38/38 PASS`

`PHASE_U17_SCENE_AND_RESTART = PASS`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
