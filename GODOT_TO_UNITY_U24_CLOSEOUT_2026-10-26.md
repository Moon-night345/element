# U24 废弃矿井 — 首轮核心收口

日期：2026-10-26（Asia/Shanghai）。状态 IMPLEMENTED_WITH_SOURCE_GAPS；不是正式 VERIFIED 或逐系统90%验收。

## 实现与来源

- 从 Godot `Phase2ChemistryService._ore_profile` 导出八类矿物档案到独立 Gameplay 目录 `ore_profiles.json`，由 GameData 加载；Agent 不拥有或修改矿物真值。
- 对照源 `identify_ore / pretreat_ore / smelt_ore / metal_displacement / precipitation_test`，补齐直接识别、预处理、冶炼、置换和沉淀服务；保留库存原子提交与原有铁路线调用入口。六种正式矿物识别/预处理、四种金属还原经过合同检查。识别不消费，地形分析消费一份，检测仪不消费；不将三者的组成记录混用。
- 源无反应分支在活性比较后记录发现，不扣材料；Unity 保留该语义，并在玩家界面明确保存无反应记录，不冒充成功产物。
- 新增实际矿井地形分析节点和实验台：玩家选择操作、当前持有的投入材料和配对材料。既有熔炉、检测仪、反应釜、设备制造与进阶门槛不变。
- 新增薄 Mine adapter 和专属状态：未知矿石3/6/9及30秒停滞、正式失败1/2/4/6合同、逐事实差集、样本/置换结果差异、实际冶金、盐碱沉淀/晶洞结晶区别、设备复用与反应釜需要。三门成熟为分析完成、冶金已观察、材料差异已观察；成熟后静默，进展/事实/结果/级别持久化。
- 对已有工业硫酸钡批量反应，只有实际领取输出后才发布正式沉淀观察；开始与缓冲不等同领取。

Godot 依据：`src/agent/abandoned_mine_agent_behavior.gd`、`region_agent_behavior_profile.gd`、`cross_region_recall.gd`、`src/chemistry/phase2_chemistry_service.gd`、`src/progression/scrap_catalog.gd`、`src/session/world_session.gd`。Unity 对应位于 `Assets/Scripts/AbandonedMineAgentBehavior.cs`、`AgentRuntime.cs`、`WorldChemistryService.cs`、`GameBootstrap.cs`、`GameDataLoader.cs`、`AuthoritativeContracts.cs`、`SaveRepository.cs` 与 `Editor/MineAgentAcceptance.cs`。

## 本轮抓到并修正的问题

1. 新实验台与旧铁冶炼节点位置重叠，真实场景交互被截走；改到独立位置，实际探针检查回调位置唯一性。
2. U10 旧测试把所有新增化学观察都误当成核心前置；测试改为检查正式九类知识前置，未修改 Gameplay 通行门槛。
3. 新矿石识别/重复地形分析不能被已宣布集合吞掉；按实际结果明确发布分析提交，仍由逐事实策略去重。

失败日志保留，没有把失败当作通过。

## 验证

| 项目 | 结果 | 日志 |
| --- | --- | --- |
| Unity编译及累计合同检查 | 68/68 PASS | Logs/phase_u24_core_smoke_verified.log |
| 最终Windows x64构建 | Succeeded，0 errors，2 warnings | Logs/phase_u24_build_verified.log |
| 实际Main联合进程 | PASS：矿井入场、地形分析/HUD、真实检测仪、真实实验台按钮选材、置换/无反应、沉淀、预处理/还原、工业批量制造加工领取、成熟存档 | Logs/phase_u24_restart_write_verified.log |
| 独立进程读档 | PASS：矿井知识、置换结果/无反应、沉淀库存与成熟抑制；U20–U23及既有存档链回归通过 | Logs/phase_u24_restart_read_verified.log |
| 三类导出 --check / git diff --check | PASS | 工作区检查 |
| Godot工作树 | 无修改 | git status --short |

测试token `136c096a37794653bbb1ad31b35aeb88`。普通玩家档未加载或改写。原料获取、制造材料/Stage、位置与时间仍使用明确夹具；真实服务、场景交互、按钮回调、HUD、正式目录与独立进程恢复已运行。不是自然采集、完整键鼠通关或视觉等价验收。未运行full verify、慢世界生成或17区域生成套件。

## 保留缺口

源没有矿井通用错误矿石/设备/条件的显式矿井失败发布；只实现未来正式白名单合同，不伪造live失败事件。四种可解析检测定义与六种地形定义各按真实来源导出，没有为其余样本补造检测组成。

本轮不是整个Chemistry系统完成：原始品位/产率返回元数据、完整恢复次数/实验历史、两类辅助档案的同ID预处理边界、全部原始处理分支及自然获取路线仍须后续CHM/INV验收；不能从正式90%分母删去。完整动态环境、地形生成、视觉与全玩家路线待办仍保留。

首轮专属核心实施8/13，剩5个：磁暴高原、辐射岩带、地幔流、核心壳、行星核。目录统计仍为14源Profile、13源adapter、3明确generic。这不是游戏整体完成率。下一步 U25 磁暴高原；整体逐系统至少90%的目标保持进行中。
