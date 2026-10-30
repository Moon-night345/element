# Godot → Unity Phase U2 完成报告

日期：2026-10-11  
阶段：权威数据管线与领域契约基线  
Unity：`2022.3.62f3c1`  
Godot 权威提交：`cd9626be38cb2d007e2e07c29e19a0e5b8b532f3`

## 结论

Phase U2 已完成。Unity 现在可以从当前 Godot 权威代码确定性导出核心目录，并在 Unity 冒烟测试中检查跨目录引用。此阶段没有接管或改变现有玩法执行逻辑。

当前整体功能迁移进度由 Phase U1 的 **37%** 调整为 **40%**。增加的 3 个百分点来自数据迁移基础设施、设备/反应内容契约和自动一致性验证；不代表 15 台设备运行时、Agent 或完整区域玩法已经迁移。

## 本阶段完成内容

1. 重建 `tools/export_chem_data.py`：
   - 不再使用旧电脑绝对路径。
   - 默认读取本机 Godot 项目，支持 `--godot`、`--out` 和无写入 `--check`。
   - 从 Godot 当前源代码读取区域、反应、反应内容、正式设备和中文本地化。
   - 记录 Godot 源提交、提交时间、源文件和目录数量。
   - 保留反应的 `tags` 与 `any_tags` 条件语义。

2. 新增 Unity 权威数据：
   - `devices.json`
   - `reaction_content.json`
   - `migration_manifest.json`

3. 新增 Unity 领域契约：
   - 反应内容单元。
   - 15 台正式设备及制造解锁元数据。
   - 区域条件阈值。
   - 反应执行结果。
   - 设备 `Idle / Processing / OutputReady / Blocked` 状态契约。

4. 新增跨目录校验：
   - 区域、物质、反应、设备 ID 唯一性。
   - 17 区域权威顺序。
   - 40 ReactionSpec、40 ReactionContent 和 40 Reaction 元数据集合一致。
   - 反应物/产物、区域、碎片、记忆、替代反应引用存在。
   - 15 台设备槽位、阶段和解锁区域有效。
   - 本地化键唯一且数量与清单一致。

## 权威导出结果

| 数据 | 数量 |
| --- | ---: |
| Region | 17 |
| Material | 54 |
| ReactionSpec | 40 |
| ReactionContent | 40 |
| FormalDevice | 15 |
| 导出本地化词条 | 225 |

本地化的 225 条由 207 条权威 CSV、4 条现有 Unity UI 补充键及 14 条从 Godot `_display_language` 映射补齐的语言显示名组成。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| Python 导出器语法 | PASS |
| 确定性导出 `--check` | PASS |
| Unity C# 编译 | PASS |
| Unity 冒烟测试 | 8/8 PASS |
| Windows x64 Build | PASS，0 errors，2 warnings |
| Build Settings 正式场景 | `Assets/Scenes/Main.unity` |
| 构建产物 | `Builds/Windows/ElementalStarSea.exe` |
| 构建后真实启动 | PASS；加载 16 碎片、16 记忆、17 区域、40 反应、15 设备、12 任务步 |
| Git diff whitespace check | PASS（仅 CRLF 提示） |

无图形批处理启动时出现 Sprite/UI shader 不支持提示，这是 `-nographics` 没有图形设备造成的验证环境提示；数据加载和主场景启动已成功，不是正式图形运行结论。

## 尚未迁移

Phase U2 只建立可靠边界，以下仍未实现：

- Godot 完整 ReactionService 的 preview、失败原因、条件检查和事件发布。
- 15 台设备的制造、放置、输入预留、异步加工、输出缓冲和领取。
- Region Runtime、挖掘条件、资源生成与区域专属交互。
- AgentDecisionPolicy、AgentController、Region Profile/Behavior 和跨区记忆。
- 完整任务链、实验室升级、设备进度、存档 schema 对齐。
- 正式 UI/视觉达到 Godot 版本效果。

## 下一阶段建议

Phase U3 应迁移“反应执行闭环”，以 Godot 为权威实现：preview → 条件判断 → 正式失败原因 → 原子消耗/产出 → 成功/失败事件。完成后再接设备异步处理，避免设备层复制一套不一致的化学逻辑。
