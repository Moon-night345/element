# U29 行星核：专属Agent首轮接入收口

2026-10-29。13个源专属adapter的首轮核心已有Unity对应；这不是游戏完成率，也不是AGT正式90%验收。14源Profile/13源adapter/3明确generic的目录计数不变，17区域不能由13个文件数替代审计。逐系统基线继续生效，AGT保留PARTIAL。

## 本次内容与来源

对照Godot的planetary_core_agent_behavior.gd、region_agent_behavior_profile.gd、deterministic_agent_response_provider.gd、agent_context.gd、agent_controller.gd、phase2_chemistry_service.gd、formal_device_catalog.gd和world_session.gd。只读导出四类正式样本组成/性质、8个权威条件字段、分类字段顺序、失败白名单、11类回忆映射及失败原因分类；源目录变化会使导出检查失败，不用记忆推测配方与身份。

新增PlanetaryCoreAgentBehavior及分离的事件/决策事实和注意状态，接入现有事件→快照→策略→确定性响应链。保留危险事实入场、无权威危险时安静观察、未知样本静默、真实检测后分析、首次失败静默与后续阶梯、只回忆一个相关区域、只讨论当前问题类别、储液非反应能力、充能设备复用、普通成功克制、玩家拥有解法、成熟后的未知/失败/突破例外。成熟后入场即使带危险，也沿源成熟抑制顺序，不擅自改写源决策。

分析、正式条件字段存在性、问题分类和设备使用分别更新，四项齐全才成熟。UI抑制前保留注意更新，错误成熟存档拒绝写入。字段存在并不等于条件值正确；此处只迁移源Agent的观察合同，不修改实际化学判定。每条决策最多携带一个recall_region_id，full_solution_disclosed保持false，Agent不执行路线或解锁设备。

Main从实际储罐成功存入/取出事件转发bulk_liquid_stored/reused，携带源定义的设备/储液分类和ordinary_success。失败、非液体、空取出或其他设备不生成此类成功证据。实际合成氨和储罐操作后转发准备状态更新；准备更新本身没有正式条件/下一问题类别时保持静默，不从准备列表推算整条解法或宣布突破。Godot源代码、Gameplay配方/产物/解锁、Region Runtime均未修改。

## 验证

| 项目 | 结果 | 证据 |
| --- | --- | --- |
| 累计针对性编译/合同检查 | 87/87 PASS | Logs/phase_u29_smoke.log |
| 入场/样本 | 权威危险字段白名单与快照；四类实际制造检测、身份不能冒充组成、样本不消耗、分析不能冒充处理 | PlanetaryAgentAcceptance.EntryAndDetection |
| 失败/储液需要/能量需要 | 第一次静默、2/3/4阶梯、明确失败归属、单相关回忆、Stage与已知区域、真实制造充能设备后复用，不自动解锁 | PlanetaryAgentAcceptance.FailuresStorageAndEnergy |
| 回忆/成熟/磁盘 | 11类映射的源顺序、分类字段优先级、普通成功注意更新、成熟例外、突破不等于通关、非法成熟拒绝、独立磁盘读写 | PlanetaryAgentAcceptance.RecallMatureAndDisk |
| 实际储罐服务 | 实际制造/放置/存取、非法状态拒绝、产物守恒、普通成功静默、跨区域生产者归属 | PlanetaryAgentAcceptance.ActualStorage |
| Windows构建 | Succeeded，0 errors，2 warnings | Logs/phase_u29_scene_build.log |
| Main联合链 | U20→U23→U27→U28→U29通过；未知静默、实际检测/HUD、实际储罐制造、两次存入/一次取出真实按钮、准备UI与保存 | Logs/phase_u29_restart_write.log，U29行113 |
| 独立进程恢复 | U15/U16/U20/U23/U27/U28/U29通过；样本分析、储液分类/设备、储罐与背包、发现记录保留，无虚构条件/成熟/终局 | Logs/phase_u29_restart_read.log，U29行26 |
| 导出/工作区 | Agent/Chemistry/Terrain三类--check、git diff --check通过；Godot工作区无修改 | 导出脚本与git检查 |

隔离token：495cb44e2dad4b97b1ee236353d1b995。普通玩家档未读取或改写。Main使用U28真实转化产出的3份还原碳液，存入2次、取出1次后背包2份/储罐1份；跨进程仍为此分布。U28既有读取检查同步按背包加实际储罐总量核对，不能因合法储存误报产物丢失，也未降低为不检查产物。

样本/制造原料、Stage、访问/位置和时钟仍有明确夹具；进入最终区域未证明自然路线的所有门槛已满足。实际服务、Main回调、真实按钮、HUD和两个独立进程确实执行，不冒充全键鼠通关、自然采集路线或渲染视觉对齐。没有运行full verify、慢世界生成或17区域生成套件。

## 尚未完成的范围

源src中储液需要、能量不足、final_process_step_observed与multi_system_breakthrough字段目前仅见于Agent消费端/Profile，实际WorldSession主桥提供储液与准备更新，缺少完整的正式条件选择/问题分类/失败/突破生产者。上述分支此轮主要为显式合同夹具，不冒充自然玩家过程已发布这些证据；真实Main中condition_selected与mature仍为false，coreTestPassed不被Agent改写。源缺口继续单列，未修backlog或为了凑成熟伪造事件。

完整原始payload、旧设备/能力状态映射、精确动态环境与危险生产者、全面知识与抑制边界、事件时机、完整自然获取/通行/终局路线、Lumen与视觉/音效仍待AGT/相关系统最终验收。条件/回忆合同通过不能证明这些项目达到90%。

Agent首轮接入已经跨过13个专属核心；继续迁移的优先级转向基线中MISSING的CBT战斗运行时（U30），再补齐玩家通行/生存/设备/化学/叙事/视觉等逐系统条目。只有每个系统分别达到至少90%且核心玩家流程无阻断，才可完成整体目标。
