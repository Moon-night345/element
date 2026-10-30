# U20E — 余烬能力、失败合同与联合运行验证

日期：2026-10-23。连续推进 U20A–D 后续增量；保留现有工作区，Godot 源未修改。

同轮后续继续实现碳燃料入口并重跑联合链，最新 52 组及燃料恢复证据见 GODOT_TO_UNITY_PHASE_U20F_REPORT_2026-10-23.md；本文 51 组/无燃料范围为前一检查点。

## 本轮实现

- 已确认制造 device_industrial_condenser 才触发冷凝器能力提示；伪造制造事件不授予能力。读取现有制造结果，不改解锁区域、Stage 或成本。
- 从源 Profile 导出正式失败原因集合。带余烬标签且原因匹配时，记录首次失败与 3/5/7 次梯度；已确认进展重置失败计数。计数/等级持久化，拒绝非法值。
- 不匹配原因的显式余烬失败不再落入通用警告分支冒充正式失败；普通未标记设备失败的既有反馈未改。
- 本机设备层实际发布 condition_missing:* 等原因，不能直接冒充 insufficient_temperature 等源正式原因。**本轮失败梯度是合同接入，尚无匹配的真实发布入口**，没有强行映射。
- 增加 --u20-smoke 联合 Main 场景探针；复用唯一 GUID 隔离存档机制，增加独立游戏进程恢复检查。

## 联合运行范围

实际游戏进程通过区域观察入口确认高温、未知样本保护、真实设备制造/放置、检测仪 Start 与实际硫文本 HUD、热处理与冷凝输出领取、成熟静默及正式 persistentDataPath 存档恢复。

制造材料、样本、碳酸盐原料、乙醇蒸汽、Stage、部分解锁访问为明确夹具；位置与冷却是测试定位/时钟偏移，设备时间由服务推进。不是从零采集的键鼠通关，不代表真实蒸汽上游链、碳燃料或正式失败发布入口已完成。源当前未发布的热暴露/氧化事件仍不补造。

## 验证

- 合同检查 51/51 PASS：Logs/phase_u20e_smoke_final.log。
- Windows 构建 Succeeded，0 errors，2 条既有未使用字段警告：Logs/phase_u20e_build_final.log。
- 余烬 Main 联合探针 PASS：Logs/phase_u20e_scene.log；最终构建再次执行同一联合链的证据见 Logs/phase_u20e_restart_write.log。
- 盐碱湿地实际场景回归 PASS：Logs/phase_u20e_salt_regression.log。
- 独立进程恢复：见 Logs/phase_u20e_restart_read.log，测试 token b4fafcadb15b4f0ea055fdb38618937f。仅隔离测试档，不加载或改写普通玩家档。
- 三类导出一致性、git diff --check PASS；Godot 工作树为空。未跑 full verify、慢世界生成或 17 Region generation suite。

新增合同组验证真实冷凝器制造、伪造保护、正式失败首个及 3/5/7 次梯度、不匹配原因不计数、分析进展重置。正式失败使用显式事件夹具，不计实机失败发布验收。初轮发现未匹配余烬失败会落回通用警告，已修正并回归，早期失败日志保留。

## 重启探针复现

在项目根目录使用新 GUID；沿用已有 U19 重启启动器，附 --u20-chain-probe 选择余烬联合链。

```powershell
$u20Token = [Guid]::NewGuid().ToString('N')
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u19-save-write','--u20-chain-probe',"--u19-token=$u20Token",'-logFile',"$PWD\Logs\u20_write_recheck.log") -WindowStyle Hidden -Wait
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u19-save-read','--u20-chain-probe',"--u19-token=$u20Token",'-logFile',"$PWD\Logs\u20_read_recheck.log") -WindowStyle Hidden -Wait
```

## 剩余缺口

碳燃料真实入口、正式失败发布匹配、完整逐事实决策等尚未完成；本轮不将余烬提前计入完整核心完成。核心 adapter 计数仍 3/13，逐系统至少 90% 尚未达标。其余 10 个 adapter 和先前其他系统缺口继续保留。

本轮已连续完成能力、失败合同、构建、联合实机及跨进程恢复，不以单个小分支 PASS 作为整区通过依据。下一实现重点是碳燃料上游入口与源逐事实策略。
