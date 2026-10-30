# U20G — 余烬逐事实分析与联合回归

日期：2026-10-23。现有 Unity 项目增量修改；保留全部既有工作区，Godot 源未修改。

## 实现

对照 Godot EmberRiftAgentBehavior._analysis_decision，响应改为使用“本次确认事实减去 EmberAttention.known_facts”的差集：

- 没有已确认分析定义时不解释、不写入新事实。
- 差集为空时返回 ember_fact_already_explained，而不是只依靠 reaction ID 的 already_shown 抑制。
- 只有本次新事实包含 element_s / ion_so4 时，才选择对应精确解释；不能重用之前已知的硫事实为本次新增的一般性质套用精确硫解释。
- 受热性质与一般材料解释也只依据差集。现有四种源样本没有硫酸根，未添加虚构事实。
- 注意力更新仍记录全部已确认事实；即使 UI 已说明或没有新事实，也按源更新确认进展时间、重置停滞计数。表现静默不等于丢弃已确认进展。
- 保留既有热处理、冷凝、燃料、能力、成熟和一次响应预算，没有改 Gameplay 规则。

## 验证

- 编译与合同检查 53/53 PASS：Logs/phase_u20g_smoke.log。
- Windows x64 构建 Succeeded，0 errors，2 条既有未使用字段警告：Logs/phase_u20g_build.log。
- 最终构建联合 Main 链 PASS：Logs/phase_u20g_restart_write.log，包括实际燃料节点、检测硫文本 HUD、热处理/冷凝领取、成熟静默与实际目录存档。
- 独立游戏进程恢复证据：Logs/phase_u20g_restart_read.log；token eb7ece5ae8a3454fb9aedadcd3bc6af5。
- 三类导出 --check 与 git diff --check PASS；Godot 工作树为空。未运行 full verify、慢世界生成或 17 Region generation suite。

新增合同组使用明确的“部分已知事实”注意力夹具验证差集选择、旧精确事实不复用、重复分析进展时间、UI 抑制后事实记忆、序列化恢复和未知样本保护。原四种样本真实设备检测检查继续通过；重复分析/恢复断言已从通用 already_shown 更新为源专属静默原因。

联合运行仍使用明确的制造原料/样本/燃料/蒸汽/Stage 等前置夹具、定位与时钟推进，不冒充从零材料键鼠通关。独立进程恢复使用唯一隔离测试档，普通玩家档未加载或改写。

## 仍需完成

正式失败原因真实发布与 condition_missing:* 格式尚未匹配，失败梯度仍只有合同；自然材料/蒸汽上游完整路线、热量返回元数据、完整环境运行态与全条目玩家验收仍缺。

本轮关闭“分析仅按响应 ID 去重、尚未逐事实选择”的缺口，但不据此宣称余烬全部完成。核心 adapter 计数保持 3/13，余烬仍待正式覆盖复核；逐系统至少 90% 尚未达标。

下一步应复核余烬完整覆盖矩阵，明确源事件缺口与 Unity 真正缺失项，再推进其余专属区域。
