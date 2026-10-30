# U20A — 余烬裂谷热处理事件子阶段

日期：2026-10-23。现有项目增量修改，Godot 源工作区未修改。

本轮新增 EmberThermalAgentBehavior，只迁移两个已有真实处理入口的展示分支，不声称完成整个余烬裂谷 adapter。

- recipe_phase3a_thermal_carbonate：实际领取 CaO 与 CO2 后，结合已提交 thermal 知识才解释热分解。
- recipe_phase3a_condense_vapor：实际领取乙醇且已有 condenser_used 知识后才祝贺冷凝。
- 若 Agent 已记录盐碱分离经历，热处理使用源 ember_salt_heat_link 响应；否则使用热分解响应。
- 跨区归一化条件沿用源约定：当前在余烬裂谷或已展示该区首次入场。没有把所有实验室加工都全局标成余烬事件。
- 不把开始加工或输出待领取当作实际交付；不改变配方、成本、设备解锁、Gameplay 知识登记时机。
- 使用现有一次提示状态与序列化恢复抑制重复响应，不接外部 LLM。

## 验证边界

Unity 合同检查 47/47 PASS（既有 46 + 新增一组）。日志：Logs/phase_u20a_smoke.log。

新增检查调用真实设备制造、放置、Start、Advance、Collect 服务，验证伪造结果静默、Start/OutputReady 不触发交付、交付响应、Gameplay 快照不变、序列化往返后一次提示抑制与盐碱回忆分支。制造原料、Stage 和加工原料是明确测试夹具，回忆状态也是夹具；不是从零资源玩家通关证明。序列化往返不是新增的独立进程磁盘恢复验收。

Windows x64 构建 Succeeded，0 errors，2 条既有未使用字段警告；日志 Logs/phase_u20a_build.log。git diff --check PASS；Agent 导出一致性检查 PASS。未运行 full verify、慢世界生成或 17 Region generation suite。

## 未完成

余烬专属入场温度门、四种样本的完整事实提取与分析路由、未知提示梯度、正式失败梯度、冷凝器能力、成熟状态尚未接入。本轮没有把碳燃料观察伪造为真实成功，也没有补造源未发布的热暴露/氧化事件。

尚未针对本轮两个分支做 Main 场景 HUD 和独立进程恢复探针；服务合同 PASS 不替代这些验收。既有锈蚀、冻原与盐碱阶段的限制继续保留。

专属 adapter 核心计数仍是 3/13，10 个尚未完整接入深层核心，其中余烬仅新增此局部分支。逐系统至少 90% 仍未达标。

下一步继续余烬的分析事实、入场环境确认和专属持久状态，再补实际场景探针；不能把 U20A 当作完整 U20 完成。
