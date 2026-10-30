# Phase U19 — 盐碱湿地 Agent 核心与真实分离事件

日期：2026-10-18。沿用本机 Unity 2022.3.62f3c1 和现有未提交修改。未重建、拉取、回滚项目，Godot 工作区只读。

## 本轮实现

新增 `Assets/Scripts/SaltMarshAgentBehavior.cs`，接入已有 Agent 事件、分离上下文快照、策略、确定性响应、队列及存档。依据 Godot `src/agent/salt_marsh_agent_behavior.gd`、对应 Profile、响应提供器及 `agent_controller.gd` 的归一化规则实现。

| 项目 | 已接通内容 | 证据边界 |
| --- | --- | --- |
| 入场 | 区域/角色/地形就绪且有有效溶液可用度快照，才显示首次观察 | 使用源 ChemicalResourceCatalog 导出的静态 0.90 值和 Profile 0.75 门限，不代表完整动态环境运行态 |
| 未知材料 | 首次观察；3/6/9 次、30 秒停滞门与已说提示抑制 | 未确认前不透露钠、钾、氯化物或氢氧根；支持 UI 重复说明抑制 |
| 组成确认 | 只读取已识别样本的正式组成；源文本区分通用盐类性质、HCl、NaOH、OH、Cl、Na、K | 通用 salt_like/alkaline 不升级为精确化合物 |
| pH | 实际检测仪 Start 成功后的既有样本识别、ph_discovery 记录和源样本 pH 定义共同确认 | 仅事件 observed=true 不足；当前实际确认 ACIDIC/ALKALINE，未给无源定义的样本编造中性结果 |
| 中和/沉淀 | 成功的现有 WorldChemistry 事件被归一化为 Agent 观察；重复实际沉淀后提示固液分离需要 | 不改反应投入、产物或产率，不因失败或外观猜测发布成功 |
| 过滤/蒸馏 | 只在正式设备 `device_output_collected` 成功事件后发布分离完成，核对已提交知识及交付产物快照 | 既有服务在 Start 就登记知识，本轮明确不把 Start 或 OutputReady 当作分离交付成功 |
| 蒸馏器能力 | 已实际制造 device_industrial_distiller 才提示可用；支持跨区标签 | 不提前解锁过滤器，过滤器仍要求 fossil_strata；其制造本身不宣称分离已完成 |
| 成熟与存档 | 分析完成 + 溶液性质已确认 + 至少一次真实分离，三项齐备才成熟 | 成熟后普通引导静默；仍记录随后收到的已确认事件，避免把表现抑制误当成丢弃事实 |

源事件的盐碱引导激活条件沿用：当前在 salt_marsh，或已展示 salt_marsh_first_entry。实际检测与化学观察可以在返回实验室后继续，但不会把无关区域事件全局强制解释成盐碱湿地内容。

没有改 Chemistry、Environment、Device progression、Terrain 或 Region 的玩法规则；本轮在已有 Gameplay 事件消费端增加归一化桥接，并增加只读 Agent 状态。U15 的不可变历史存档方案保留。

## 数量与源缺口

- 源目录仍为 17 个正式区域、14 个 Profile、13 个源专属 adapter、3 个明确 generic 区域。
- Unity 专属 adapter 核心从 **2/13 增至 3/13**：锈蚀丛林、晶体冻原、盐碱湿地；另 **10 个**尚未迁移深层策略。这个计数不是迁移完成百分比，更不是这三个区域全部验收通过。
- Agent 分析目录从 16 项增至 **22 项**：死城 6、锈蚀 2、晶体冻原 8、盐碱湿地 6。
- 盐碱 Profile 的 sample_ids 实际是 7 项。4 项地形块的通用性质导自 ScrapCatalog，2 项酸碱资源导自 ChemicalResourceCatalog。
- 第 7 项 `compound_neutralized_salt_solution` 虽列在源 Profile，但源 COMPOSITIONS 和 Phase2ChemistryService._sample_info 没有它的分析定义。已导出到 `salt_unresolved_analysis_ids`，不生成虚假的分析记录、NaCl 或中性 pH。
- 4 项地形块的完整玩家扫描/分析入口仍待迁移；本轮通用盐类文本验证使用显式已识别夹具，不当作真实玩家扫描证明。酸碱资源则通过既有真实检测仪验证。
- 7 条溶液动态文本和 4 条 pH 动态文本均导自 Godot；模板数量不计功能完成率。

## 验证结果

| 检查 | 结果 | 项目根目录下证据 |
| --- | --- | --- |
| 最终编译 / 合同检查 | 46/46 PASS（既有 42 + 新增 4 组） | Logs/phase_u19_smoke_final.log |
| Windows x64 构建 | Succeeded；0 errors；2 条既有未使用字段警告 | Logs/phase_u19_windows_build.log |
| 实际 Main 场景 | PASS | Logs/phase_u19_scene_probe.log |
| 独立游戏进程写入 / 重启恢复 | PASS，同时回归 U15/U16 状态恢复 | Logs/phase_u19_restart_write.log；Logs/phase_u19_restart_read.log |
| U17 锈蚀丛林实机场景回归 | PASS | Logs/phase_u19_rust_regression.log |
| U18 晶体冻原实机场景回归 | PASS | Logs/phase_u19_crystal_regression.log |
| Agent / Chemistry / Terrain 导出一致性 | PASS | 三个 exporter --check |
| git diff --check | PASS | 最终工作区检查 |
| Godot 工作区 | 未修改，git status --porcelain 为空 | 只读源审计 |

新增四组合同检查包括环境缺失/非法值与门限、3/6/9 梯度和时间门、未知保护、实际检测酸碱及动态 pH、通用与精确组成保护、中和/沉淀与重复分离提示、过滤器正式区域门、Start/OutputReady 不等于交付、真实过滤/蒸馏、先分析或先分离两种成熟顺序、失败与伪造事件保护、跨区标签、发言预算、专属快照/非法成熟拒绝和旧档缺字段默认。事件观察前后比较剔除 Agent 的 Gameplay 快照，确认策略不改玩法状态。

### 实际场景探针做了什么

1. 通过实际区域资源菜单第三页的按钮回调，采集酸液 3、碱液 4；没有注入这些反应物。
2. 调用实际中和与沉淀节点两轮，确认首次沉淀和后续固液分离提示。
3. 由现有区域观察入口确认死城、晶体冻原访问，制造工作台、检测仪和蒸馏器；验证未访问化石层时不能制造过滤器，再定位已有化石层场景位置确认访问。
4. 调用真实过滤器、蒸馏器制造/部署/Start/Advance/Collect 服务，输出领取后才观察成功；完成两种分离但未分析前不成熟。
5. 在实验室用真实检测仪分析剩余酸液；实际 HUD 显示盐酸确认文本，样本不被检测消耗，随后成熟；后续 pH 事件仍记入状态，但因已成熟不重复开口。
6. 返回盐碱湿地不重播普通引导，在正式 persistentDataPath 保存并恢复专属状态。

**边界**：制造原料和 Stage 1 仍是明确夹具；区域移动使用测试定位，设备时间/冷却使用确定性测试推进。这不是全流程键鼠通关，也不是从零材料自然制造所有设备。pH 偏酸/偏碱文本由真实检测服务的合同测试检查；本次 Main 探针因事件顺序在成熟后静默记录 pH，不能冒充实际 HUD 展示了该 pH 对话。

独立进程恢复使用唯一 token `302957b6ba584c1b96c119ab200b740f` 的测试档，保留档案与日志；普通玩家档未加载或改写。再次运行必须换 token。没有运行 full verify、慢世界生成或 17 Region generation suite。

## 剩余范围与下一步

- 盐碱湿地 4 项地形块的完整扫描/分析入口、完整动态环境上下文及世界化学节点模型仍待迁移。
- 上述盐溶液分析是源缺口，不将“文件存在”或测试 PASS 误记为该分析可用。
- 本轮只解释既有中和结果，不代表 Godot 酸池实体、pH、通行与伤害状态全套等价迁移。
- 晶体冻原温变/结晶真实玩法与发布入口、锈蚀丛林高级变体/替代成功等缺口保留，未因新区域接入而移出分母。
- 其余 10 个 adapter、完整跨区主动回忆、Lumen 视觉、地形/运动/敌人等其他系统仍需继续；仍按 15 系统和 8 大工作包跟踪，未达到逐系统至少 90% 的正式验收标准。

下一批优先余烬裂谷：实际热处理、碳燃料与气相/冷凝观察，沿用同样的已确认事件和知识保护边界。

## 复现入口

在项目根目录执行；编译/构建命令不要与另一个打开本项目的 Unity 编辑器并行。

```powershell
python tools/export_agent_catalog.py --check
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath $PWD -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile "$PWD\Logs\phase_u19_smoke_recheck.log"
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -quit -projectPath $PWD -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.BuildWindows64 -logFile "$PWD\Logs\phase_u19_build_recheck.log"
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u19-smoke','-logFile',"$PWD\Logs\phase_u19_scene_recheck.log") -WindowStyle Hidden -Wait
$u19ProbeToken = [Guid]::NewGuid().ToString('N')
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u19-save-write',"--u19-token=$u19ProbeToken",'-logFile',"$PWD\Logs\phase_u19_restart_write_recheck.log") -WindowStyle Hidden -Wait
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u19-save-read',"--u19-token=$u19ProbeToken",'-logFile',"$PWD\Logs\phase_u19_restart_read_recheck.log") -WindowStyle Hidden -Wait
```

`PHASE_U19_SALT_AGENT_CORE = IMPLEMENTED_WITH_LIMITS`

`UNITY_DEDICATED_ADAPTER_CORES_CONNECTED = 3/13`

`OTHER_DEDICATED_ADAPTERS_NOT_PORTED = 10`

`SALT_LIVE_PHENOMENA_BRIDGED = 5/5`

`SALT_PROFILE_ANALYSIS_DEFINED = 6/7 (ONE_SOURCE_GAP)`

`PHASE_U19_CONTRACT_CHECKS = 46/46 PASS`

`PHASE_U19_SCENE_AND_RESTART = PASS`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
