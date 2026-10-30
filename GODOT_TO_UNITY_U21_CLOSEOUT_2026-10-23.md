# U21 风蚀沙漠 — 首轮核心收口

日期：2026-10-23（Asia/Shanghai）。状态：IMPLEMENTED_WITH_SOURCE_GAPS，不是正式 VERIFIED / 90% 验收。

## 本轮完成

- 在既有沙漠入场回忆、未知假说、确认分析与关联成熟基础上，补齐未知材料 3/6/9 次提示及 30 秒停滞门；提示有冷却和既有一次性/发言预算保护。
- 补齐源定义的正式失败白名单和 1/3/5/7 次梯度合同。拒绝其他区域和未批准失败原因，不把普通加工失败伪装成沙漠正式失败。
- 入场或真实确认分析重置未知/失败计数与进展时间；保留历史提示级别。UI 已解释时仍更新有效进展，成熟后保持普通引导静默。
- 补齐提示级别、失败计数的存档与非法值拒绝；梯度状态经过实际磁盘往返。
- 验证四类沙漠地形样本的真实消耗分析分支：碳性质、一般性质、碳酸盐解释、冻原确认关联以及余烬温度假说。假说不升级为组成事实，不凭空解锁设备。

Godot 对照：`src/agent/sand_desert_agent_behavior.gd`、`region_agent_behavior_profile.gd`、`cross_region_recall.gd` 与实际地形分析组成导出。Unity 对应：`SandMaterialBehavior.cs`、`SandDesertEntryBehavior.cs`、`AgentRuntime.cs`、`SaveRepository.cs`、`WorldChemistryService.cs`、`GameBootstrap.cs` 与 `Editor/SandEntryAcceptance.cs`，均位于 `Assets/Scripts/`。

## 验证证据

| 检查 | 结果 | 日志（项目根目录下） |
| --- | --- | --- |
| Unity 编译及累计合同检查 | 57/57 PASS，无编译错误 | Logs/phase_u21_close_smoke.log |
| Windows x64 可重复构建 | Succeeded，0 errors，2 warnings | Logs/phase_u21_close_build.log |
| 实际游戏进程：余烬链回归与沙漠入场/假说/分析节点/HUD/关联/成熟/提交 | PASS | Logs/phase_u21_close_restart_write.log |
| 另一个游戏进程读档：沙漠分析、确认关联、假说清除及成熟抑制 | PASS，余烬与既有库存/地形恢复回归也通过 | Logs/phase_u21_close_restart_read.log |
| Agent、Chemistry、Terrain 导出 --check | PASS：14源Profile、13源adapter、17区域、64地形身份 | 三类导出脚本 |
| git diff --check / Godot 工作树 | PASS / 无修改 | 本机只读检查 |

独立进程 token：`166e59938b004c0ebc2a41779aece3e4`。没有加载或改写普通玩家档。

实际场景探针使用真实构建、实际交互回调、正式分析服务、HUD 与存档目录；原料/沙漠方块、制造与 Stage 前置、位置和时钟仍使用明确测试夹具。它不是从零材料自然采集、完整鼠标键盘通关或视觉等价验收。失败梯度只验证正式事件合同，不声称存在真实失败发布入口。未运行 full verify、慢世界生成或17区域生成套件。

## 保留缺口与下一阶段

源沙漠 Profile 明确标注加工失败尚无真实发布；本次不补造 Gameplay 事件或修改失败语义。自然地形生成/获取、完整环境、全玩家路线和视觉差距继续归入相应系统待办，不能从验收分母中删除。

首轮区域 adapter 核心实施计数由4/13推进到5/13：锈蚀丛林、晶体冻原、盐碱沼泽、余烬裂隙、风蚀沙漠。还剩8个专属核心待继续迁移/补齐；这个计数不是整体迁移百分比，也不是5个区域正式达到90%。14个源Profile、13个源adapter、3个明确generic区域的目录统计不变。

U21 首轮核心在此收口，下一阶段推进荧光洞穴；不将跨系统缺口反复拆成 U21 字母子阶段。逐系统至少90%的最终要求保持不变。
