# Godot → Unity Phase U4 完成报告

日期：2026-10-11  
阶段：正式设备运行时基础  
Unity：`2022.3.62f3c1`  
Godot 权威基线：`cd9626be38cb2d007e2e07c29e19a0e5b8b532f3`

## 结论

Phase U4 的设备运行时基础已完成。Unity 已具备 15 台正式设备的权威制造/解锁契约、实例身份、持久化状态以及 `Idle → Processing → OutputReady → Idle` 加工闭环。

当前整体功能迁移进度由 **44%** 调整为 **48%**。本阶段完成的是设备公共基础设施，不代表 18 条 Godot 专属 ChemistryService 流程和完整设备 UI 已经迁移。

## 权威数据扩展

- 从 `FormalDeviceCatalog` 导出 15 台正式设备。
- 从 `Phase2RecipeCatalog` 导出 33 条有效 ProcessingRecipe。
- 排除 Godot 已明确标记 `processing_design_tbd` 且没有产物的无效配方。
- 校验所有设备 recipe 引用均能解析。
- 迁移清单新增 `processing_recipes = 33`。
- 权威源文件清单新增：
  - `src/progression/phase2_recipe_catalog.gd`
  - `src/chemistry/phase2_chemistry_service.gd`

## 已实现的设备公共能力

1. 制造检查：
   - 设备资源状态。
   - Laboratory Stage。
   - 解锁区域是否到达。
   - 工作台依赖。
   - 制造材料是否充足。
   - 制造材料原子扣除。

2. 实例与放置状态：
   - 唯一 `instance_id`。
   - `device_id`、解锁、放置状态。
   - 同种设备多个实例相互独立。

3. 加工状态机：
   - `Idle`
   - `Processing`
   - `OutputReady`
   - `InvalidInput`
   - `ConditionMissing`
   - `Error` 等正式状态。

4. 加工事务：
   - 开始前检查设备/配方身份、阶段、条件、原料和 process input。
   - 消耗型输入在开始时原子预留。
   - 催化剂等 process input 只校验，不消耗。
   - 产物保存在设备输出缓冲，不提前进入玩家库存。
   - 玩家领取成功后设备回到 Idle。

5. 存档：
   - 当前配方。
   - 加工状态、进度、已用时间、总时长。
   - 已预留输入。
   - 输出缓冲。
   - 错误原因。
   - 已制造设备、实验室阶段和已到达区域。

6. 正式事件：
   - `device_built`
   - `device_placed`
   - `device_processing_started`
   - `device_processing_failed`
   - `device_output_ready`
   - `device_output_collected`

## Source-of-truth 保护

Godot 中有 18 条 ProcessingRecipe 被明确标记为 `runtime_service_only`。这些配方实际由 `Phase2ChemistryService`、腐蚀路线或区域专属服务执行，不能仅凭声明式输入/产物直接模拟。

Unity 当前对此返回明确的 `specialized_runtime_required`，并保证不消耗输入。这样避免蒸馏、过滤、电解、结晶、高压氨合成等流程在专属化学规则尚未迁移时被通用设备层错误执行。

已具备完整声明契约的 Device 12 CO₂/H₂ 高压催化转化已经通过条件、非消耗催化剂、加工、缓冲和领取闭环测试。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| 权威 ProcessingRecipe 导出 | 33 条，PASS |
| 15 台设备 recipe 引用解析 | PASS |
| 制造阶段/区域/工作台检查 | PASS |
| 制造材料原子扣除 | PASS |
| 设备放置和实例身份 | PASS |
| 专属 runtime 配方保护 | PASS |
| 必需条件失败零副作用 | PASS |
| Process input 非消耗 | PASS |
| 输入预留和输出缓冲 | PASS |
| 加工中存档恢复 | PASS |
| 输出主动领取 | PASS |
| Unity 冒烟测试 | 10/10 PASS |
| Windows x64 Build | PASS，0 errors |

## 尚未完成

- 18 条 `runtime_service_only` 专属化学服务。
- 设备制造/放置/加工的正式玩家 UI。
- 正式世界设备 Sprite、碰撞、放置预览与保护区域检查。
- 气体罐和液体罐的专属容量/单一物质约束。
- 充能台恢复能量的专属逻辑。
- 设备产出触发的 Capability 与后续任务推进。

## 下一阶段

Phase U5 应迁移专属设备化学服务和设备操作 UI。建议按依赖顺序：

1. Stage 0：熔炉、充能台、基础反应台。
2. Stage 1：检测仪、过滤器、蒸馏器、冷凝器。
3. Stage 2：电解槽、气体罐、大型反应器。
4. Stage 3：吸收塔、结晶器、高压催化反应器、液体罐。
