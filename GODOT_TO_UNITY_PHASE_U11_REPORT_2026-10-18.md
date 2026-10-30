# Phase U11 — 90% 逐系统基线与新状态工艺路线

日期：2026-10-18。

## 交付结果

建立 `MIGRATION_90_COVERAGE_BASELINE_2026-10-18.md`：15个系统、75个宏观功能验收索引，记录Godot来源、Unity对应、缺失内容、证据规则和后续优先级。没有把历史73%估算或目录数量当成逐系统完成率，也没有把本阶段测试通过判为全部达到90%。

## 修复的三个玩家流程问题

### 1. 区域访问记录无法保留

`GameBootstrap.UpdateRegionAndRadiation()` 先设置 `currentRegion`，再调用 `AddVisitedRegion()`。后者原先通过包含“当前区域”的 `HasVisitedRegion()` 判断去重，导致新访问被跳过；离开后便不再被认为访问过，区域解锁设备可能无法制造。

改为只对持久化的 `visitedRegions` 数组去重。新状态路线实际经过多个区域、回死城准备材料、离开后制造相应设备，并检查序列化后的区域历史。

### 2. 浆料没有正常玩家制备入口

原Stage1→2测试直接注入 `compound_mixed_reaction_slurry`，掩盖了玩家无法制备过滤输入的问题。

迁移 `Phase2ChemistryService.neutralize()` 与 `precipitate()` 的正式工艺：

```text
resource_acidic_solution ×1 + resource_alkaline_solution ×1
    → compound_neutralized_salt_solution ×1
compound_neutralized_salt_solution ×1 + resource_alkaline_solution ×1
    → compound_mixed_reaction_slurry ×1
```

盐沼新增酸碱中和、沉淀工艺槽两个 E 交互入口；随后沿现有过滤器、蒸馏器完成能力链。失败不修改库存或成功知识。

### 3. 每个区域只展示前两个资源

世界布局从 x 偏移20开始，每项+7，超过80×0.42即停止；第三项开始就被截断。例如盐沼酸液/碱液、晶原水样、荧光洞葡萄糖和废弃矿道正式矿物在数据中存在，但没有相应玩家采集入口。

保留原有紧凑资源点，在资源超过两种的区域增加“区域资源采样”终端，通过分页显示完整的正式资源清单并采集。未修改Godot资源表或产物配方。该入口沿用当前Unity采样数量；有限源容量仍记入INV-02待迁移，不在本阶段声称已等价。

## 新增服务集成验收

`Assets/Scripts/Editor/PlayerRouteAcceptance.cs` 从 `new GameState()` 开始：

1. 通过修复/腐蚀里程碑与原料处理推进Stage0→1。
2. 从死城正式原料块执行分析/回收，按正式成本制造工作台和后续设备。
3. 真实制造并部署11台前中期设备；不直接注入“已拥有设备”、阶段或能力。
4. 使用正式采集目录中的原料执行中和→沉淀→过滤→蒸馏，推进Stage1→2。
5. 执行碳酸盐热处理、储气、有机发酵/冷凝、矿物预处理/批量反应、真实潮湿区域水合成、磁能高原电解、天然结晶，推进Stage2→3。
6. 检查设备、能力、母液和区域访问历史序列化恢复。

测试中的原料采集由明确的源区域/原料白名单及已迁移地形节点模拟，区域切换调用当前服务门槛；它不验证键鼠走位、相邻路线、鼠标按钮、视觉或敌人。修复UI入口有代码证据，但本轮未宣称已完成GUI全流程通关。

## 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 2022.3.62f3c1 编译与冒烟 | 19/19 PASS | Logs/phase_u11_smoke.log |
| Windows x64 正式构建 | PASS，0 errors / 2 warnings | Logs/phase_u11_windows_build.log |
| 新构建实际启动与目录载入 | PASS，进程响应正常，观察期间无异常日志 | Logs/phase_u11_runtime.log |
| Godot权威数据导出一致性 | PASS | python tools/export_chem_data.py --check |
| 差异空白检查 | PASS | git diff --check |
| Godot工作树 | 保持干净 | git status --short |

Windows可执行文件启动日志：`Logs/phase_u11_runtime.log`。启动/目录载入与实际新存档通关是不同验收项。

## 当前未完成

15个系统均尚未签署正式≥90%验收。Agent完整逻辑、地形开采放置与碰撞、完整保存恢复、战斗与能力、若干化学/设备分支、图鉴/地图/触控和视觉等价仍需继续迁移，详见基线。

后续优先执行地形/玩家碰撞与全运行态保存，并逐项展开已列出的功能索引。当前阶段结论只覆盖本报告所述修复与服务路线。

`PHASE_U11_BASELINE = ESTABLISHED`

`PHASE_U11_PLAYER_FLOW_FIXES = 3`

`PHASE_U11_TESTS = 19/19 PASS`

`NEW_SAVE_GUI_PLAYTHROUGH = NOT_VERIFIED`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
