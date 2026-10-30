# Godot → Unity Phase U3 完成报告

日期：2026-10-11  
阶段：正式反应执行闭环  
Unity：`2022.3.62f3c1`  
Godot 权威基线：`cd9626be38cb2d007e2e07c29e19a0e5b8b532f3`

## 结论

Phase U3 已完成。Unity 合成不再直接执行“删原料、加产物、固定扣 5 能量”，而是经过正式的 preview → 条件验证 → 原子提交 → 发现登记 → Gameplay 事件发布流程。

当前整体功能迁移进度由 **40%** 调整为 **44%**。本阶段完成的是 40 条基础反应的统一执行边界；多步工业反应、设备异步处理和区域专属 Gameplay 仍未包含在这 44% 中。

## 与 Godot 对齐的语义

- 失败类型数值顺序与 `ReactionPreview.Failure` 一致：
  - `None = 0`
  - `MissingReactant = 1`
  - `MissingEnvironment = 2`
  - `InsufficientEnergy = 3`
  - `InvalidDefinition = 4`
- H₂ 合成能量成本为 10。
- 其他反应成本为 `5 + 必需环境标签数 × 3 + 反应物种类数`。
- `tags` 必须全部满足。
- `any_tags` 至少满足一个，例如氮气合成可使用 `electric` 或 `cold`。
- 所有前置条件失败均不改变库存、能量或发现状态。
- 库存增量和能量消耗一次性提交；提交失败不会留下部分修改。
- 成功后登记 reaction 与 language，重复发现保持幂等。

## Unity 接入

- 新增 `ReactionService` 与 `IReactionState`。
- `GameState` 新增原子反应提交和已发现语言集合。
- `GameBootstrap` 的合成入口已切换到正式服务。
- 当前区域的环境标签与 `synthesis_device` 会传入 preview/execute。
- 正式事件：
  - 成功：`reaction_committed`
  - 失败：`experiment_failed`
- 事件包含 reaction、reason、energy、inventory delta 和 language；订阅者得到防御性快照，不能反向修改执行结果。

## 合成 UI 修正

- 显示真实能量成本，不再固定显示 5.0。
- 显示配方必需条件和任选条件。
- 显示明确失败原因：配方不匹配、原料不足、缺环境条件、能量不足或提交失败。
- 合成槽由 4 个扩展为 8 个，支持需要重复元素的精确配比。
- 点击材料会增加一个单位，点击已选槽位会移除对应单位。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| Godot 权威导出 `--check` | PASS |
| Unity C# 编译 | PASS |
| Unity 冒烟测试 | 9/9 PASS |
| 缺环境失败零副作用 | PASS |
| 能量不足失败零副作用 | PASS |
| `any_tags` 任选条件 | PASS |
| 库存与能量原子提交 | PASS |
| reaction/language 发现登记 | PASS |
| 正式成功/失败事件 | PASS |
| 事件防御性快照 | PASS |
| Windows x64 Build | PASS，0 errors，2 warnings |
| 构建后启动与数据加载 | PASS |

构建产物：`Builds/Windows/ElementalStarSea.exe`

## 下一阶段

Phase U4 建议迁移设备运行时基础：

1. 15 台设备的解锁与制造检查。
2. 设备输入预留。
3. `Idle → Processing → OutputReady → Idle` 状态机。
4. 输出缓冲和玩家主动领取。
5. 设备处理统一调用 Phase U3 的反应服务，避免重复化学规则。
6. 存档中保存设备实例与处理中状态。
