# U20B — 余烬样本检测与知识保护

日期：2026-10-23。延续现有工作区，不拉取、不重建、不回滚。Godot 源工作区未修改。

## 本轮增量

- Agent 导出器从 Godot Phase2ChemistryService._sample_info 提取四种余烬样本的 composition_ids 与 property，遇到无法解析的定义立即失败，不凭名称编写化学事实。
- 四种样本为 scorched_rock、sulfur_bearing_mineral、ash_deposit、carbonaceous_residue，对应完整正式 item_ember_rift_*_block ID。分析目录从 22 增至 26 项，目录数量不代表完成率。
- 现有真实检测仪成功开始检测并提交识别结果后，将实际选中样本归一化到余烬 Agent。当前在余烬或已展示其首次入场时可跨区继续检测引导。
- 只有上下文内已识别且有源定义的样本才可解释；事件 analyzer_used=true 本身不能创造组成。
- 已确认 element_s 才说硫；heat 相关事实对应受热性质，其余对应一般材料解释。四种样本均无 ion_so4，不虚构硫酸盐检测结果。
- 重复响应与快照恢复使用已有 shown 状态抑制。没有新增配方、消耗、设备解锁或修改 Gameplay source-of-truth。

## 验证

Unity 编译及合同检查 48/48 PASS，日志 Logs/phase_u20b_smoke.log。

新增一组检查逐项调用四种样本的真实设备制造、放置及检测 Start 服务，验证未识别样本拒绝、检测不消耗样本、硫/硫酸盐知识保护、策略调用前后输入状态不变、重复扫描和 JSON 序列化恢复后不重复解释。制造原料、样本与 Stage 为明确测试夹具，不能当作从零采集的玩家路线。

Windows x64 构建 Succeeded，0 errors，2 条既有未使用字段警告。日志 Logs/phase_u20b_build.log。

Agent、Chemistry、Terrain 三类导出 --check 均 PASS，git diff --check PASS。未运行 full verify、慢世界生成或 17 Region generation suite。

## 尚未完成

这仍是 U20 的子阶段，不是完整余烬 adapter。尚缺正式入场温度门、未知材料与失败梯度、碳燃料入口、冷凝器能力、完整专属注意力/成熟状态和实际 Main HUD/独立进程恢复探针。

本轮只使用已有一次提示状态，没有声称完成源 ember_known_fact_ids 等全套专属状态；同一响应 ID 的一次抑制也不等于完整逐事实注意力迁移。

核心 adapter 计数仍为 3/13，另 10 个未完成核心迁移，包含当前局部实现的余烬。逐系统 90% 验收仍未满足。

下一步应补余烬环境确认和专属状态，再联合热处理、冷凝、分析做实机场景验收。
