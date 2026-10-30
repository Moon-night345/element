# Phase U18 — 晶体冻原 Agent 核心与真实检测事件

日期：2026-10-18。延续现有本机项目和未提交修改，不重建、不拉取、不回滚。Godot 工作区仅作只读权威参照。

后续增量：U19 已接入盐碱湿地核心和五类真实观察事件，详见 `GODOT_TO_UNITY_PHASE_U19_REPORT_2026-10-18.md`。本文 2/13、42/42 为 U18 历史检查点。

## 本轮增量

新增 `Assets/Scripts/CrystalTundraAgentBehavior.cs`，由现有 AgentDecisionPolicy 接入；事件、脱离 Gameplay 的快照、策略、确定性文本和对话队列继续沿用现有架构。

| 功能 | 来源与 Unity 对应 | 边界 |
| --- | --- | --- |
| 低温入场 | Godot crystal_tundra_agent_behavior / ChemicalResourceCatalog.environment_profile；导出正式 0.12 温度，确认区域与地形/角色就绪后读取 | 是静态著作环境快照，不是完整 Godot 环境运行态；缺温度、NaN、Infinity、未就绪或温度高于正式 0.25 门限均不说话 |
| 未知样本观察 | 源 Profile 的 8 个 sample_ids，首次观察与 3/6/9 次提示 | 所有梯度要求自上次进展满 30 秒；不提前泄漏组成；独立冷却、一次提示和全局发言预算仍生效 |
| 组成确认 | exporter 从 ScrapCatalog 与 ChemicalResourceCatalog 读取这 8 项正式组成 | 仅已识别样本的上下文事实可进入响应；事件本身不能伪造组成 |
| 碳酸盐文本 | 确认 carbonate_bearing 等只说碳酸盐；compound_caco3 才说碳酸钙（CaCO₃） | 两个模板均导自 Godot，不用区域名推断精确组成 |
| 检测仪接入 | 正式 ID 为 device_multi_chemical_analyzer；PRECISE_ANALYSIS；实际制造与 recipe_phase2_detect | 仅已制造设备可触发可用提示；扫描事件携带实际选中样本，配合已提交 ph_discovery 记录确认使用 |
| 成熟与恢复 | 分析完成 + 碳酸盐已知 + 真实检测仪使用，三项同时成立才成熟 | 成熟后普通入场/采集/重复分析静默；专属计数、已知事实、进展时间和成熟状态保存；旧 Unity 档缺此字段使用空状态 |
| 温度变化 / 结晶响应 | 源 reaction ID 和文本可解析，已接入只读的事件 + 已提交知识双证据合同 | **尚无真实 Gameplay 发布入口，只做合同检查，不计玩家可玩功能通过**；不把 geode_abyss 自然结晶或工业结晶冒充冻原事件 |

AgentCatalog 仍为 14 个源 Profile、13 个源 adapter 身份、3 个明确 generic 区域；分析目录从 8 项增至 16 项（死城 6、锈蚀 2、冻原 8），不是完成率。

设备层只增加已提交检测事件的 `analyzed_sample_id` 证据字段，并以现有 Detect 选择的同一样本传递。没有改制造成本、解锁区域、Stage、分析产率、耗材、配方或设备处理时机。分析结果仍在原有 Start 原子提交点生效，Agent 不负责 IdentifySample。

显式 profile 标签可跨区路由，防止在锈蚀丛林观察冻原已确认检测结果时被另一 adapter 截走。未标记且在其他区域的无关样本不会自动按冻原解释。

## 验证

| 检查 | 结果 | 证据（项目根目录下） |
| --- | --- | --- |
| Unity 2022.3.62f3c1 编译及合同检查 | 42/42 PASS（原 38 + 新增 4） | Logs/phase_u18_smoke_accepted.log |
| Windows x64 构建 | Succeeded，0 errors，2 条既有未使用字段警告 | Logs/phase_u18_windows_build_accepted.log |
| 实际 Main 场景 / 菜单采样 / 检测 / HUD / 成熟抑制 | PASS | Logs/phase_u18_scene_accepted.log |
| 独立进程写入与恢复 | PASS，同时回归 U15/U16 库存、掉落、地形和一次提示恢复 | Logs/phase_u18_restart_write_accepted.log；Logs/phase_u18_restart_read.log |
| U17 锈蚀丛林实际场景回归 | PASS | Logs/phase_u18_rust_regression.log |
| Agent / Chemistry / Terrain 三类导出一致性 | PASS | 三个 exporter --check |
| git diff --check | PASS（既有 CRLF 提示不属错误） | 最终工作区检查 |
| Godot 工作区 | 未修改，git status --porcelain 为空 | 只读源检查 |

独立进程验收使用测试 token `bc40aec2e7d8400ba341cb81edd09722`，测试档与历史留作证据；再次复现请生成新 token，不覆盖该记录。

新增四组检查覆盖低温缺失/非法值与就绪、知识保护、3/6/9 次和 30 秒门、真实检测仪制造/部署/处理/领取、非碳酸盐与精确/通用碳酸盐文本、伪造制造/能力/扫描/现象、成熟静默、UI 重复抑制、跨区路由、专属快照恢复、非法成熟拒绝、旧档默认与失败保护。

场景探针通过已有资源采样菜单第二页和真实按钮回调获得有限样本，然后调用真实制造、部署与检测服务、事件桥和实际 HUD。制造原料与 Stage 1 是**明确的测试前置夹具**，玩家位置和冷却使用测试定位/时钟偏移，不是全流程键鼠通关，也不是从零资源自然制造全部设备。未知样本不通过库存注入获得。

初轮探针曾错误寻找不在预览点中的碳酸盐岩实体，随后改为现有分页菜单；再发现测试前置未访问工作台正式解锁区域，已改为通过现有区域观察入口实际确认死城访问。紧接工作台祝贺的检测仪提示也被正确的发言预算抑制，现于探针两次制造之间显式推进测试时钟并排空队列，另增加冷却期间“保留已确认可用状态但不重复说话”的边界检查。上述是探针前置修正，没有放宽 Gameplay 制造条件或 Agent 预算。早期失败日志保留。

跨进程恢复使用正式 persistentDataPath 下唯一 GUID 测试档，沿用 U15 不可变历史写入；普通玩家存档不加载、不覆盖。没有运行完整世界生成、full verify 或 17 Region generation suite。

## 未完成，不计入 90% 验收

- Godot 实际 `phase2_temperature_transform` / `phase2_crystallize`、晶体相态持久化和对应 live publisher 尚未在 Unity 实现；本轮没有模拟成功绕过此缺口。
- 8 项样本的 Agent 合同已读取，但不能据此声称 8 项都有完整玩家分析入口：4 项地形块的分析/扫描专属入口仍待迁移；普通资源检测沿用现有正式检测仪。通用碳酸盐分支测试直接建立已识别夹具，不声称是玩家扫描路线。
- 完整地形矿脉、世界化学节点扫描、动态环境上下文及 Lumen 全套动画仍未等价。
- 跨区 Recall 仍只有现有只读查询基础，尚非所有区域主动回忆策略。
- 源专属 adapter 13 个，Unity 现在有 **2 个接入核心链路，另 11 个未迁移深层策略**；2/13 是实施计数，**不是迁移完成率**。锈蚀丛林与晶体冻原自身的缺口继续保留在分母。
- 逐系统 90% 正式验收仍未满足，不能根据 42 组局部检查 PASS 给出总体 90%。

下一批优先盐碱湿地：确认酸碱/溶液、真实中和/沉淀/过滤/蒸馏事件与分离能力，再继续其余专属 adapter。冻原温变/结晶 Gameplay 缺口并行保留于后续化学工作包，不标为完成。

## 复现入口

```powershell
python tools/export_agent_catalog.py --check
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath $PWD -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile "$PWD\Logs\phase_u18_smoke_recheck.log"
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u18-smoke -logFile "$PWD\Logs\phase_u18_scene_recheck.log"
$u18ProbeToken = [Guid]::NewGuid().ToString('N')
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u18-save-write',"--u18-token=$u18ProbeToken",'-logFile',"$PWD\Logs\phase_u18_restart_write_verified.log") -WindowStyle Hidden -Wait
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u18-save-read',"--u18-token=$u18ProbeToken",'-logFile',"$PWD\Logs\phase_u18_restart_read.log") -WindowStyle Hidden -Wait
```

`PHASE_U18_CRYSTAL_AGENT_CORE = IMPLEMENTED_WITH_LIMITS`

`UNITY_DEDICATED_ADAPTER_CORES_CONNECTED = 2/13`

`OTHER_DEDICATED_ADAPTERS_NOT_PORTED = 11`

`CRYSTAL_TEMPERATURE_CRYSTALLIZATION_LIVE_PUBLISHERS = MISSING`

`PHASE_U18_CONTRACT_CHECKS = 42/42 PASS`

`PHASE_U18_SCENE_AND_RESTART = PASS`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
