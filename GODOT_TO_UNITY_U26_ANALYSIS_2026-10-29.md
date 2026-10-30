# U26 辐射岩带：环境与检测检查点

2026-10-29。U26进行中，首轮完整核心计数仍9/13；不是整体或逐系统90%完成。

对照源radiation_belt_agent_behavior.gd/profile接入环境风险入场、未知样本、污染事实分析、单独氧化性质事件与失败跟踪。实际RegionDef辐射参数经分离快照读取；就绪、正确区域、有限且大于零才作WARN，否则仅FOLLOW_UP观察。Agent不修改环境、防护或库存。

四类辐射方块的检测组成/性质由Phase2源导出；AgentContext必须有实际检测记录才提供事实，不把普通识别索引当检测组成。污染确认只匹配源污染事实，氧化性质只响应独立MATERIAL_PROPERTY_DISCOVERED；不能从样本名或普通分析直接补出已发生氧化反应。Main已路由当前区域的正式检测提交。新增RadiationAttention并校验负计数，兼容旧档默认值。

验证：Logs/phase_u26_analysis_smoke_verified.log累计73/73 PASS，含风险零值/正值/NaN/Infinity、Gameplay只读、四类实际检测仪制造放置/检测、不消费样本、污染/氧化分离、磁盘写读与坏计数拒绝。Agent导出--check和git diff --check通过，Godot工作树无修改。没有声称本轮Windows构建、实际Main渲染或独立进程验收。

后续处理增量：接入产气/吸收/净化分支和四条件成熟（污染分析、氧化性质观察、气体处理、净化验证），成熟后普通引导静默，非法成熟存档拒绝。Main在实际检测提交后单独发布已确认性质，在现有产气/净化成功和吸收输出领取时发布相应观察。吸收结果源gas_id字段存在会使气体分支先于吸收庆祝分支，使用has_gas保留此语义，不猜气体身份。

处理验证：Logs/phase_u26_process_smoke_verified.log累计74/74 PASS，覆盖实际检测、产气和净化服务、失败不消费/不确认、四门成熟、磁盘恢复与非法成熟拒绝，以及吸收能力/字段优先级合同。吸收分支此轮为事件合同夹具，未冒充实际吸收塔联合实机通过。git diff --check通过。

设备/实机增量：实际储罐与吸收塔制造、储气转移、无吸收剂失败保护、吸收剂/储气扣除、缓冲与领取边界、净化气流/污染吸收液交付均已验证。Main扩展U26探针，使用独立隔离存档的U20→U26链，避免与U22已有CO2储气混装，不清空或伪造原有储气状态。

| 验证 | 结果 | 日志 |
| --- | --- | --- |
| 累计合同与编译 | 75/75 PASS | Logs/phase_u26_absorption_smoke.log |
| Windows构建 | Succeeded，0 errors，2 warnings | Logs/phase_u26_scene_build.log |
| 实际Main进程 | PASS：风险入场、检测/性质HUD、实际储气与吸收失败/领取/守恒、成熟提交 | Logs/phase_u26_restart_write.log |
| 独立进程读档 | PASS：污染/氧化/气体/净化、实际吸收产物和成熟静默；U20及既有存档回归通过 | Logs/phase_u26_restart_read.log |

token：930f36a77d8a4e20b18c522e22185aa9。普通玩家档未加载或改写。制造/Stage/访问、原料、位置和时间仍为明确夹具，不是从零自然获取/完整键鼠通关或视觉等价验收。Main链这次为U20→U26，不冒充U20–U25所有场景均在本次串行执行。导出--check与git diff --check通过，Godot工作树无修改。

剩余：原始污染字段存在性语义、其余身份/源事件入口复核及自然玩家路线。失败计数遵循源任意EXPERIMENT_FAILED规则，未照搬其他区域的白名单/梯度。不得把Profile中存在的提示键当作源adapter实际使用的梯度。源ChemicalResourceCatalog对污染矿料/溶液/氧化残渣有另外的组成记录，不能将四类方块检测验收扩展成所有扫描入口均已迁移。核心计数保持9/13。

任务目标仍为逐系统至少90%且核心流程无阻断，源项目缺口单列；本轮只形成明确进度，不缩小整体目标。未重建、回滚、清理工作区，未跑full verify、慢世界生成或17区域生成套件。
