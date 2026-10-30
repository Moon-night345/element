# U22 荧光洞穴 — 首轮核心收口

日期：2026-10-23（Asia/Shanghai）。状态 IMPLEMENTED_WITH_SOURCE_GAPS；不是正式90%验收。

## 本轮实现

- 对照源 `fluorescent_grotto_agent_behavior.gd` 接入薄 adapter 与专属注意力状态。读取分离上下文，不写 Gameplay；入场必须有就绪的含氧快照，源环境参数从 ChemicalResourceCatalog 导出，余烬燃烧知识必须有访问历史与实际燃料消耗。
- 四种洞穴样本的地形组成和检测仪组成分别导出；地形分析消费一份，真实检测仪不消费。当前已提交的分析索引与检测仪记录决定可用事实，不混用两条来源，不凭空透露元素、CO2或pH。
- 接入逐事实差集、UI重复抑制、3/6/9未知提示与30秒停滞、1/2/4/6正式失败合同、进展重置和冷却预算。
- 已有真实发酵成功后发布产气观察；气体身份未确认时不说二氧化碳。接入储气进阶方向、冷凝与储气区别、真实储罐制造及实际储气提交观察。
- 源五条件成熟门：含氧已观察、分析已完成、有机过程完成、产气已观察、储气能力已理解；成熟后静默。专属状态持久化，拒绝非法级别、负计数与非法成熟状态，兼容旧档默认值。
- 真实场景验证发现 RegionSpawnPos 只处理地表，已按现有地下传送门坐标补齐地下区域定位，不修改区域顺序/门槛。修正跨区域读档探针的沙漠事件标签，避免把远端沙漠回归当作当前洞穴事件。

Unity 实现位于 `Assets/Scripts/FluorescentGrottoAgentBehavior.cs`、`AgentRuntime.cs`、`WorldChemistryService.cs`、`SaveRepository.cs`、`GameBootstrap.cs`；新增合同检查 `Editor/GrottoAgentAcceptance.cs`。未重建项目、未回滚已有工作区，Godot源只读。

## 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 编译与累计合同检查 | 61/61 PASS | Logs/phase_u22_core_smoke_final61.log |
| 最终Windows x64构建 | Succeeded，0 errors，2 warnings | Logs/phase_u22_core_build_verified.log |
| 实际Main场景链 | PASS：真实入场、分析/发酵回调、实际HUD文本、储罐制造放置/储气与存档 | Logs/phase_u22_restart_write_verified.log |
| 独立进程恢复 | PASS：洞穴专属知识、储气数量、成熟静默；余烬/沙漠及既有存档链回归通过 | Logs/phase_u22_restart_read_verified.log |
| 三类导出一致性 / git diff --check | PASS | 工作区检查 |

测试token：`fc46fec2faeb4e70af343fbfd52f32c3`，普通玩家档未加载或改写。首次验证失败与修复保留在前序日志，不将失败日志当作通过证据。

实际进程使用明确的原料/制造/Stage、位置与时间夹具，调用真实服务、场景交互回调和HUD；不是自然采集、键鼠完整通关或视觉等价验收。没有运行full verify、慢世界生成或17区域生成套件。

## 保留缺口

源Profile明确说明有机处理失败没有WorldSession发布入口；本轮只实现白名单正式事件合同，不伪造实时失败发布。完整动态环境、自然地形采集/样本加工获取、全部玩家路线、视觉与其他系统仍按基线待办，不因本轮通过从分母移除。

专属adapter首轮核心实施计数6/13，还剩7个：晶洞深渊、废弃矿井、磁暴高原、辐射岩带、地幔流、核心壳、行星核。目录仍为14源Profile、13源adapter与3个明确generic区域。这不是游戏整体完成率。

下一步U23晶洞深渊：按源自然结晶/浓度/跨区比较/纯度事实边界推进；源未提供纯度时绝不生成纯度数值。总体目标继续有效：每个系统至少90%，核心玩家流程无阻断。
