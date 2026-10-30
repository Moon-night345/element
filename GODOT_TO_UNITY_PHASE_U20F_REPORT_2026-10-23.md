# U20F — 连续推进余烬联合链与碳燃料入口

日期：2026-10-23。延续 U20E 能力/失败合同与联合场景工作，本轮继续实现而非停在单个分支。Godot 源未修改，既有用户修改保留。

## 新增真实玩法

依据 Godot Phase2ChemistryService.use_carbon_fuel，新增 Unity WorldChemistryService.UseCarbonFuel 和余烬场景碳燃料观察节点。只接受 resource_carbonaceous_material 或 material_carbon，扣除一份后递增 carbonFuelBurned；错误类型、缺料、库存提交失败不记录成功，极值计数拒绝溢出。

燃烧次数是迁入 Unity 的 Gameplay 状态，Agent 只读取分离快照；新增字段进入既有序列化和存档校验，旧 Unity 存档默认零，负计数拒绝。没有给合成能量加值，也没有把源 heat_units=1 擅自变成环境温度或伤害变化。源热量返回元数据未扩展到本项目通用结果结构，当前仅实现库存/次数/观察链，不声称完整热预算模拟。

成功提交后归一化 carbon_fuel_used，只有已提交燃烧次数大于零才解释；Agent 持久化 combustion_observed。与 U20A 的热分解、冷凝、U20B 分析、U20C 成熟、U20D 梯度、U20E 冷凝器能力组成联合链。

## 本次连续完成的验证

- 合同检查 **52/52 PASS**：Logs/phase_u20f_smoke.log。
- 最终 Windows x64 构建 Succeeded，0 errors，2 条既有警告：Logs/phase_u20f_build.log。
- 最终构建在真实 Main 游戏进程调用燃料节点回调，再执行未知样本保护、检测仪实际硫文本 HUD、热处理与冷凝领取、能力/成熟静默、真实目录保存恢复：Logs/phase_u20f_restart_write.log。
- 独立游戏进程恢复证据：Logs/phase_u20f_restart_read.log；本轮检查额外要求 carbonFuelBurned==1 且 combustion_observed 保留。
- 测试 token：ab169707e4324f9c9422c1d0e8c91843。测试档隔离，普通玩家档未加载或改写；再次复现须换 GUID。
- Agent/Chemistry/Terrain 三类导出一致性及 git diff --check PASS；Godot 工作树为空。未跑 full verify、慢世界生成或 17 Region generation suite。

制造原料、燃料、样本、乙醇蒸汽、Stage/部分访问是明确夹具；角色使用测试定位、冷却使用测试时钟、设备时间由服务推进。不是自然采集全部材料的键鼠通关，但确实调用正式节点、服务、事件桥、HUD、存档和独立游戏进程。

燃料合同检查验证两种原料的单份消耗、计数、失败不提交、能量不变、序列化恢复及 Agent 燃烧观察。U20E 已另通过真实冷凝器制造、正式失败 1/3/5/7 合同、盐碱实机场景回归与无燃料版跨进程恢复，见其报告。

## 不能提前计完成的内容

正式余烬失败原因与设备 condition_missing:* 发布格式仍不匹配，因此失败梯度只有合同，未伪造 live publisher。完整逐事实策略、完整自然材料获取/蒸汽上游路线、热量返回元数据与完整环境模型仍待对齐。成熟后源事件集合及细节也需完整验收。

余烬继续标部分实现，核心 adapter 计数仍 3/13，不通过拆小阶段虚增区域完成数；各系统至少 90% 尚未正式验收。其余 10 个专属 adapter 及其他系统缺口仍在分母。

复现最终重启链沿用 U20E 命令：--u19-save-write/--u19-save-read 加 --u20-chain-probe 和新 --u19-token；--u20-smoke 可单进程验证并清理自身唯一测试档。
