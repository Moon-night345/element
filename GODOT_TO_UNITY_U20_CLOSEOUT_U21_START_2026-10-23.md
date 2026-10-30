# U20 收口与 U21 启动

日期：2026-10-23。现有工作区增量推进，Godot 源未改动。

## 为什么长期停留在 U20

不是 Unity 编译或运行被阻断，而是阶段管理边界不清：把一个区域拆成 A–G 多次小交付，又将完整自然材料路线、动态环境等跨系统验收当成区域 Agent 阶段的退出条件。源项目没有发布的正式失败事件也一直被列为未收口阻碍。

本轮重新核对源代码与已实现链路，不通过删除缺口或宣称总体 90% 来收口。

## U20 范围复核

| 范围 | Unity 状态 | 剩余项归属 |
| --- | --- | --- |
| 入场温度、未知梯度、四样本确认与逐事实解释 | 已接入，合同覆盖 | 完整自然获取路线归 Terrain / Chemistry 玩家流程验收 |
| 碳燃料、热分解、冷凝观察 | 已接入正式服务与输出事件 | 完整蒸汽上游/动态热模型归化学与环境工作包 |
| 冷凝器能力、盐碱热处理回忆 | 已接入 | 完整跨区语义总体验收仍开放 |
| 成熟抑制、专属状态、存档与独立游戏进程恢复 | 已实现并验证，证据见 U20E–G | 更完整全生命周期验收仍属 Save 系统 |
| 正式失败 1/3/5/7 梯度 | 已接入原因合同，未伪造事件 | SOURCE_EVENT_GAP；源当前没有对应带余烬标签的发布入口 |
| heat_units=1 返回及事件元数据 | 本轮补齐真实漏项 | 不转换为合成能量或环境升温 |

源证据：`src/chemistry/phase2_chemistry_service.gd` 有 insufficient_temperature 返回；`src/session/world_session.gd` 当前 experiment_failed 发布分支约 2080 行显式标记 rust_jungle；`src/agent/agent_controller.gd` 约 241 行仅转发 experiment_failed 数据，没有自动添加余烬标签。余烬 adapter 要求标签与正式原因同时匹配。因此不能把“原因常量存在”当成余烬 live publisher 已存在，也不能强行改造 Unity condition_missing:* 冒充此事件。

`U20_FIRST_PASS_CORE = IMPLEMENTED_WITH_SOURCE_GAPS`

`U20_GAMEPLAY_90_ACCEPTANCE = NOT_CLAIMED`

核心接入计数调整为 **4/13**（锈蚀、冻原、盐碱、余烬），含义是首轮核心实施，不是 4 个区域全部 VERIFIED，更不是迁移率。其他 9 个完整核心仍待迁移/补齐，所有上述跨系统缺口保留在 90% 验收清单，不移出分母。历史报告的 3/13 是当时未完成收口复核的检查点。

## U21 已开始：风蚀沙漠

新增 SandDesertEntryBehavior，将源入场回忆分支接到现有策略和源文本：

- 冻原已访问且有确认的碳酸盐或钙知识；或
- 余烬已访问且有已提交碳酸盐热处理知识；或
- 盐碱湿地已访问且有已提交分离知识。

满足任一条件才使用 entry_recall，否则正常 entry。只读取脱离 Gameplay 的上下文，不用 Agent 旧提示记录制造化学知识，也不新增第二套 Gameplay 知识库。

U21 仅接通入场分支；未知样本假说、正式分析后建立关联、梯度/失败/成熟及专属状态仍待下一批实现，**不计为第五个核心已接入**。

## 验证与边界

合同检查 54/54 PASS（原 53 + U21 入场组），Logs/phase_u21_smoke_final.log。

最终 Windows x64 构建 Succeeded，0 errors，2 条既有警告，Logs/phase_u21_build_final.log。源热量元数据同时验证了返回值与订阅事件，不改变能量。

U21 合同组覆盖无旧知识、只知道样本但未访问地区、三类已确认旧知识、源文本连接、策略不修改输入状态、序列化恢复抑制。已知状态是显式夹具，不冒充完整玩家路线。

实际场景探针命令：`ElementalStarSea.exe -batchmode --u20-smoke --u21-entry-probe`，最终 Logs/phase_u21_scene_final.log 中 U20 与 U21 均 PASS。它在既有余烬联合链后进入沙漠，核对实际回忆 HUD 与正式目录存档。继承余烬制造/原料/Stage/定位/时钟夹具边界，不声称从零资源键鼠通关。

早期 Logs/phase_u21_scene.log 中回忆已触发，但探针漏计 HUD 固定“引路人：”前缀而失败；修正的是探针完整文本期望，没有删掉断言或改变游戏显示规则。最终重跑通过，早期日志保留。

三类导出一致性、git diff --check PASS，Godot 工作树为空。未跑 full verify、慢世界生成或 17 Region generation suite。

后续按功能包推进，不继续把 U20 拆成更多字母阶段。下一包是 U21 未知材料假说与确认关联；项目逐系统至少 90% 目标未改变，也尚未达标。
