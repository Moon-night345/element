# U28 Core Shell：首轮核心收口

2026-10-29。首轮核心实施12/13；AGT整体仍为PARTIAL。此计数不是迁移百分比、正式VERIFIED、FULL_COVERAGE或逐系统90%完成率。剩余首轮专属核心为行星核。

## 本次接入

对照Godot的core_shell_agent_behavior.gd、region_agent_behavior_profile.gd、deterministic_agent_response_provider.gd、phase2_chemistry_service.gd、chemical_resource_catalog.gd、agent_controller.gd及world_session.gd。新增只读CoreShellAgentBehavior，沿现有AgentController/Context/Policy/provider接入，不改Godot源代码或Gameplay配方、产物、设备解锁规则。

从源导出四类正式检测组成/性质、0.88层压力、成功现象、银催化身份、失败白名单与压力字段顺序。压力入场只解释环境条件，不算设备压力满足，也不记作处理压力已观察。检测必须同时有真实检测发现记录和已识别身份；仅拾取、仅标记身份不解密组成。样本检测不消耗样本，不伪造处理进度。

保留材料未知/分析、压力观察、多条件矩阵、地幔回忆、催化解释、设备需要提示、失败1/2/4/6阶梯、当前多步观察与成熟静默。字段存在性单独保存，catalyst_consumed存在且值false仍可参与多条件合同；铁催化不自动变为正式银催化。多步字段即使没有已分析身份，也只观察当前步骤，不公开完整链。

正式成功按源替换multi_condition/catalysis/device12三项；成熟状态保持sticky。因此存档不误拒绝“曾成熟、后续不完整成功覆盖当前三项”的有效状态。负失败计数拒绝写入。UI抑制前更新注意状态，跨区域显式behavior_profile保留生产者归属，防止当前位置截获先前区域事件。

实际Main在高压设备产物成功领取后转发两条事实链：合成氨仅含已执行的压力和铁催化事实；CO2/H2转化含配方强制的压力/温度/银催化/设备身份。只有完整配方合同能生成这些证据，不由环境、制造、启动、缓冲或失败伪造成功。

## 验证证据

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 累计针对性编译/合同检查 | 83/83 PASS | Logs/phase_u28_smoke_routing_verified.log |
| 压力/检测 | 有限数值、来源顺序、环境与设备分离、快照只读；四类实际制造检测和知识守卫通过 | CoreShellAgentAcceptance.EntryAndDetection |
| 事件合同/存档 | 需求前置、失败阶梯、字段存在性、铁银区别、回忆顺序、成熟/覆盖、磁盘恢复、跨区事件归属通过 | CoreShellAgentAcceptance.ContractsAndPersistence |
| 实际设备服务 | 两条路线的实际制造/放置/条件失败/加工/领取、产物和催化剂守恒；无虚构多步/成熟 | CoreShellAgentAcceptance.ActualPressureRoutes |
| Windows构建 | Succeeded，0 errors，2 warnings | Logs/phase_u28_scene_build_final.log |
| Main实际联合链 | U20→U23→U27→U28通过；入场/检测/HUD、实际制造、合成氨及三次银转化真实按钮与领取、地幔回忆/多条件/催化/成功响应、保存通过 | Logs/phase_u28_restart_write_final.log，U28行102 |
| 独立进程恢复 | U15/U16/U20/U23/U27/U28通过；压力/多条件/银催化/设备与产物/回忆保留，多步和成熟保持false | Logs/phase_u28_restart_read_final.log，U28行25 |
| 导出与源保护 | Agent/Chemistry/Terrain三类--check通过；Godot工作区无修改 | 各导出脚本、git status --short |

最终隔离token：949bfe84811e48bea4896a04b63975f1。未读取/改写普通玩家档。获取原料/制造成本/Stage/位置/时钟为明确夹具，实际服务、Main回调、真实按钮、HUD、磁盘保存与第二进程读取确实执行；不代表自然资源完整获取、完整键鼠通关或渲染视觉等价。设备加工后重新打开控制台领取，不声称控制台已自动刷新。

前序失败日志保留：初始合同夹具受真实对话队列预算抑制，以及先前fixture已消耗催化响应；修正夹具隔离后通过。首次跨进程检查揭示Core Shell吞掉明确归属前区的事件，已修复路由并增加回归后重新构建、写档、独立读取，最终记录如上。未隐藏失败或用失败日志作为通过证据。

## 保留范围

Godot既有正式压力/催化/条件失败事件与正式多步事件发布backlog未修。多步/失败/需求分支的合同测试不冒充真实生产者完整接入，真实Main不会为凑齐成熟伪造多步事件。源银转化完成事件在OUTPUT_READY发布，Unity此轮沿产物领取发布；时机差异明确留作AGT/DEV最终对齐，不能计为完全等价。

完整原始payload、旧能力/设备映射、源条件上下文与精确动态环境、全部压力字段类型变体及旧档组合、自然路线/地形材料获取、控制台刷新和视觉仍需最终验收。入口有限值保护与显式生产者路由沿Unity现有合同执行，不能把本轮实现冒充源所有边界语义完全复刻。

14源Profile、13源adapter、3明确generic目录统计不变。宏观90%基线与其他系统待办保留。未运行full verify、慢世界生成或17区域生成套件。下一阶段U29行星核，之后继续逐系统展开条目、补齐并验收至少90%且核心玩家流程无阻断。
