# Godot → Unity 迁移 Phase U5 报告

日期：2026-10-11  
阶段：Phase U5 — 专属设备化学运行时与设备控制台  
Godot gameplay source-of-truth：`C:\Users\18485\Documents\GodotProjects\Astral-Element-Sea`

## 结论

Phase U5 第一遍实现完成。Unity 不再把 18 条 `runtime_service_only` 配方统一拒绝为
`specialized_runtime_required`，而是按 Godot `Phase2ChemistryService` 与
`WorldSession.execute_device_recipe()` 的实际规则分别执行。

本阶段后的保守整体迁移进度估计为 **54%**（Phase U4 为 48%）。这个百分比表示功能迁移覆盖，
不是内容量、画面相似度或发布完成度。

## 已完成

- 18 条专属设备配方均有显式 Unity 路由，无静默通用 fallback。
- 样品检测不消耗样品，并写入可存档的 `identifiedSampleIds`。
- 过滤与蒸馏补齐 Godot 的双产物语义。
- 冷凝器使用内部 vapor stream，不把普通库存液体冒充气相。
- 高压气罐使用 generated/stored gas 状态与 20 单位容量契约。
- 铁/铜矿、银载体与高强度矿物执行分析前置条件。
- 电解要求磁力高原环境；银催化材料精炼要求受控电流。
- 大型反应釜支持铁精炼批次、钡硫酸盐批次和高强度材料调理。
- 吸收塔从储气状态取气，只消耗吸收液。
- 工业结晶器校验浓度 `>= 0.90` 与温度 `<= 0.35`。
- 高压氨合成校验压力、纯度与铁催化剂，催化剂不消耗。
- 设备专属化学知识、气体和气相状态已进入 JsonUtility 存档结构。
- 新增 `D` 键正式设备控制台：制造、部署、运行配方、显示错误、领取输出。
- 修复载入存档后 Reaction/Device service 仍绑定旧 `GameState` 的问题。
- 修复设备加工状态机未在游戏 Update 中持续推进的问题。
- 熔炉铁/铜配方按 Godot 的 `recipe.device_id` 路由，不再被不完整的展示型
  `FormalDeviceCatalog.recipe_ids` 错误拦截。

## 18 条专属路由

1. `recipe_phase2_detect`
2. `recipe_phase2_filter`
3. `recipe_phase2_distill`
4. `recipe_phase3a_condense_vapor`
5. `recipe_phase3a_store_gas`
6. `recipe_phase3a_thermal_carbonate`
7. `recipe_abandoned_mine_iron_ore`
8. `recipe_abandoned_mine_copper_ore`
9. `recipe_phase4b_core_high_strength_preprocess`
10. `recipe_phase4b_silver_bearing_preprocess`
11. `recipe_phase3b_electrolyze`
12. `recipe_phase4b_silver_catalytic_refine`
13. `recipe_phase3b_reactor_batch`
14. `barium_sulfate_batch_precipitation`
15. `recipe_phase4b_high_strength_conditioning`
16. `recipe_phase4a_absorb_gas`
17. `recipe_phase4a_industrial_crystallize`
18. `recipe_phase4b_high_pressure_nh3`

`recipe_phase4b_device12_co2_h2_conversion` 已在 U4 通过正式声明式条件与 non-consumed
process input 运行，不属于上述 18 条 `runtime_service_only` 路由。

## 验证结果

- Godot 权威数据导出检查：PASS（17 regions / 40 reactions / 15 devices /
  33 processing recipes / 54 materials）。
- Unity 版本：`2022.3.62f3c1`。
- Unity 编译：PASS。
- 冒烟测试：11/11 PASS。
- 新增专属语义测试：PASS。
- Windows x64 构建：PASS，0 errors，2 warnings。
- 构建产物：`Builds/Windows/ElementalStarSea.exe`。
- 独立 Player 启动：PASS；成功加载 17 区域、40 反应、15 设备、33 加工配方。
- `git diff --check`：PASS（仅现有 LF/CRLF 提示，无 whitespace error）。

## 尚未完成 / 不得误判为完整迁移

- 正常玩家路线尚缺部分专属设备的上游世界行为：污染气体生成/入罐、气相注册、
  钡/硫酸盐工艺液制备等目前可由状态契约和测试验证，但未全部接入世界交互。
- Analyzer 控制台当前自动选择第一个可识别且未分析的持有样品；Godot 的完整手动样品选择 UX
  尚未迁移。
- Device 15 大型液体储罐的存入/取出交互尚未迁移。
- 充能台、基础反应台的独立操作界面仍缺失。
- 设备仍通过全局 `D` 控制台操作，尚未全部绑定到世界中的实体、碰撞与近距离交互。
- Godot 的完整 Environment/Chemistry 世界动作、Agent、17 区域内容表现、教程与事件发布仍需后续阶段。
- 当前只做首次独立 Player 启动日志验证，尚未完成逐按钮人工视觉验收。

## 下一阶段建议

Phase U6 应优先做“设备世界可达性闭环”：把 vapor/generated gas/工艺液的上游环境动作迁移到
Unity，完成 Device 15 液体储罐，并将设备控制台操作绑定到已部署的世界设备实体。完成后再做
17 区域逐区玩法迁移，避免只有服务测试可达、正常玩家不可达。
