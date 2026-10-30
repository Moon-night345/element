# U21 — 风蚀沙漠假说、分析确认与关联

日期：2026-10-23。继续现有 Unity 工作区，不重建、不回滚，Godot 源只读。

## 已接通的功能包

1. 已有冻原碳酸盐/钙知识且访问过冻原时，遇到未分析的正式沙漠样本，只说相似性猜测，记录 hypothesis_pending；不识别当前样本、不确认组成、不建立已证实关联。
2. 从源 ScrapCatalog 导出四种 item_wind_desert_*_block 的真实 composition，分析目录由 26 增至 30 项。没有把 knowledge_mapping 的 compound_caco3 当作当前样本已确认组成。
3. 新增沙漠地形样本分析服务和实际场景节点。按源 WorldSession.analyze_terrain_block_item 的非死城规则，消费一份已有方块后才登记确认结果，没有添加产物、配方或设备解锁。
4. 已确认当前样本的碳酸盐性质后：有冻原相关旧知识则建立跨区联系、清除待确认假说；只有余烬热知识时提示温度条件假说；无旧知识则只解释当前已确认的碳酸盐。碳/钙及一般性质按源分支选择。
5. 七条动态文本从源 provider 导出，只有精确事实才允许精确元素/化合物名称；当前沙漠碳酸盐岩 composition 是通用性质，不额外透露 CaCO₃。
6. 新增 SandAttention，持久化未知次数、进展时间、分析完成、假说、关联及成熟状态。分析完成且确认跨区关联才成熟，普通回访抑制；存档拒绝非法关联/成熟状态。

只使用现有 Agent 事件、分离上下文、策略、响应与存档，不改动架构。Gameplay 的组成确认使用现有已识别索引承载，代表已提交的地形分析，不表示 Device05 已使用或测得 pH。地形样本分析入口与无消耗检测仪入口没有混为一谈。

## 验证

| 检查 | 结果 | 日志 |
| --- | --- | --- |
| Unity 编译与合同检查 | 55/55 PASS | Logs/phase_u21_link_smoke.log |
| 最终 Windows x64 构建 | Succeeded，0 errors，2 条既有警告 | Logs/phase_u21_link_build_final.log |
| 实际 Main 假说→分析节点→确认 HUD | PASS | Logs/phase_u21_link_scene.log；最终构建重跑见 restart_write |
| 独立游戏进程写入 | PASS，含 U20 联合链回归 | Logs/phase_u21_link_restart_write.log |
| 独立游戏进程恢复 | PASS：分析、关联、假说清除及成熟静默保留 | Logs/phase_u21_link_restart_read.log |
| 三类导出一致性 / git diff --check | PASS | 工作区检查 |

新增合同组覆盖未知假说不变成 Gameplay 事实、伪造分析拒绝、真实服务消耗一份样本后关联成熟、无旧知识不能建立关联、通用碳酸盐不升级为精确化合物、缺料失败无副作用、磁盘恢复与非法状态拒绝。源目录数量和测试数量均不当作功能完成百分比。

实际游戏探针先通过已有真实检测仪确认旧碳酸盐知识，再显示沙漠假说，调用实际沙漠分析节点回调，核对消耗、确认文本、关联、成熟和正式目录存档。原料、沙漠方块、制造/Stage 前置仍是明确夹具，位置/冷却/设备时间采用测试推进；不是从零材料键鼠通关，也不是完整地形自然采集验收。

测试 token：41a5c5a91ac545f186f97d517f197b89，普通玩家档未加载或改写。Godot 工作树为空。未运行 full verify、慢世界生成或 17 Region generation suite。

## 剩余范围

U21 的 3/6/9 停滞提示梯度、正式失败合同、完整状态与源事件覆盖复核仍待继续；本轮不把首次未知提示冒充完整梯度。沙漠失败源 Profile 已明确标注尚无真实发布，不伪造发布入口。

U20 已收口，不重新打开。核心接入计数保持 4/13，沙漠本轮为功能包增量，尚不计第五个完整核心。自然获取/完整环境/整体视觉等跨系统缺口继续保留，逐系统至少 90% 尚未正式验收。

复现单进程：`ElementalStarSea.exe -batchmode --u20-smoke --u21-link-probe`。

重启链沿用既有唯一 GUID 启动器，依次运行 `--u19-save-write`、`--u19-save-read`，两次均附 `--u20-chain-probe --u21-link-probe --u19-token=<新GUID>`。不要复用已有 token 写入。
