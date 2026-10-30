# Phase U13 — 存档保护、多世界槽与剩余工作

日期：2026-10-18。

## 目前到底还差多少

当前跟踪15个系统、75个宏观验收索引；集合项还需展开，所以它们不能作为精确的独立功能总数。目前13个系统是部分实现，战斗运行实现缺失，构建系统已经建立但仍待完整玩家验收。15个系统均未签署“≥90%”正式验收。

这不表示所有系统都没有完成内容，也不表示有75项需要从头重做。现有证据不足以给出可信的“还剩X%”或固定工期，旧的总体73%估计不再作为验收指标。

剩余工作按以下8个大工作包继续推进：

| 工作包 | 尚需修改/验收的主要内容 | 当前规模判断 |
| --- | --- | --- |
| 1. 世界与完整地形 | Godot完整种子地形/洞穴/结构/地脉、正式变体选择、掉落实体与有限采集源、真实区域通行 | 大 |
| 2. 玩家、能力、生存与战斗 | 悬浮和能耗、完整运动合同、医疗/死亡恢复、敌人追踪/攻击/击杀/掉落/存档 | 大；战斗尚无对应运行实现 |
| 3. Agent | 事件/上下文/策略/响应、17区域专属或明确generic合同、知识保护、预算抑制、跨区回忆、视觉队列与保存 | 大；目前主要是跟随与固定引导 |
| 4. 化学、设备与进阶 | 展开全部独立化学服务及33配方分支，补输入选择/取消/环境效果，验收各设备材料来源与完整能力链 | 中到大；已有较多合同实现 |
| 5. 碎片、记忆与任务 | 碎片信任过程、记忆序列重构、任务真实转换、核心前置与双结局完整玩家操作 | 中；终局服务合同已有 |
| 6. UI、物品与输入 | 完整背包物品操作、图鉴、地图探索、实验记录、暂停、自适应和触控/输入路由 | 中到大 |
| 7. 保存恢复收尾 | 完整摘要/索引/元数据，后续Agent/敌人/有限采集快照、跨世界生命周期和异常GUI验收 | 中；本轮已补原子写入等基础 |
| 8. 表现与最终验收 | 17区域同场景画面对照、角色/设备/Lumen状态、音频、实际新存档全流程及双结局、逐系统签收 | 大；需要实际运行证据 |

“8个工作包”是实施分组，不是仅剩8个小功能。后续应继续更新每个功能ID和证据，而不是用测试数量充当完成率。

## 本轮已修改

### 存档提交与完整性

- 新增 `SaveRepository.cs`，仍通过原 `GameState.Save()/LoadOrNew()` 接入现有调用方。
- 保存使用带版本、世界ID、UTC保存时间、完整GameState JSON字符串和SHA-256校验和的封装。
- 先写同目录临时文件并Flush到磁盘，重新读取验证，再用文件替换提交；不直接覆盖主档正文。
- 保留 `.bak1`、`.bak2` 两级备份；读取顺序为主档→备份1→备份2。
- 临时文件不作为已提交存档读取，避免将写到一半的状态当作进度。
- 已损坏主档不覆盖健康备份；后续保存会保留 `.corrupt_<id>` 副本。
- 全部副本损坏时阻止当前槽写入；不将占位新状态自动写回旧档。世界列表显示该槽不可读取，玩家可选择其他槽或新建世界。
- 不支持的未来封装版本阻止覆盖，避免旧程序把新版本存档降级。
- 检查库存并行数组、重复ID、非正数量、非有限核心数值、位置与重复地形增量等基础结构。

### 多世界与旧档

- 原 `elemental_sea_save.json` 作为“原有世界”保留，可读取旧Unity纯JSON格式；首次正常保存会升级封装并保留原文备份。
- 新世界保存到 `saves/<world_id>/save.json`，不删除原世界。
- 世界选择页支持分页列表，显示区域、Stage和可读取/备份恢复状态；损坏槽不能点击进入。
- 新建世界分配独立ID。返回菜单、选择新世界前保存当前玩家位置和进度；正常退出游戏也会保存。
- 世界切换通过重新加载正式Main场景清理原场景对象，避免多个世界叠加。
- 测试使用隔离文件或Logs中的唯一测试目录；没有对普通玩家存档执行清理或覆盖。

## 与Godot的对应与限制

来源是 `src/save/save_repository.gd`、`save_codec.gd`、`save_snapshot_builder.gd` 及世界选择流程。

迁移其临时写入/提交、备份回退、世界身份与完整性验证语义。Unity封装的校验对象是准确的UTF-8 payload JSON字符串；它不是Godot存档文件的二进制或JSON格式兼容实现。所谓旧档兼容是**已有Unity版本存档**，不宣称能直接读取Godot世界档。

当前槽摘要通过扫描已保存世界构造，尚未迁移Godot完整持久索引、创建时间/游玩时间元数据与全部筛选语义。未迁移的Agent、敌人、有限采集源也不能凭本阶段宣布已保存。源项目完整坏档恢复界面、未来版本迁移链和全生命周期操作仍需验收。

## 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity真实编译/冒烟 | 25/25 PASS | Logs/phase_u13_smoke.log |
| Windows x64构建 | PASS，0 errors / 2原有warnings | Logs/phase_u13_windows_build.log |
| 实际场景磁盘存读档与世界槽UI探针 | PASS | Logs/phase_u13_scene_probe.log |
| 既有地形场景探针回归 | PASS，51,200格 | 同上 |
| 地形与原化学权威导出检查 | PASS | 两个export脚本的 --check |
| git diff --check | PASS | 工作区检查 |

新增检查包含：旧档升级、原文备份、地形/位置快照、写入中断、未提交临时文件不加载、非有限状态拒绝、双备份轮换、主档篡改校验、备份1/2回退、健康备份不被损坏主档污染、损坏副本保留、全坏档不覆盖、未来版本保护、多世界隔离、错误世界身份和非法槽ID。

真实进程探针继续使用独立测试存档；加载当前正式游戏场景执行地形磁盘往返，并创建实际世界槽按钮、调用可用按钮回调，检查损坏槽按钮禁用。它不是人工鼠标操作、跨多次应用重启的全流程验收。

复现入口：

```powershell
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile Logs/phase_u13_smoke.log
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.BuildWindows64 -logFile Logs/phase_u13_windows_build.log
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u13-smoke -logFile "$PWD\Logs\phase_u13_scene_probe.log"
```

下一优先工作：继续展开完整地形/有限采集源和Agent迁移，补齐当前最大的玩法差距；存档随新增运行态继续扩展快照。

`PHASE_U13_SAVE_FOUNDATION = IMPLEMENTED`

`PHASE_U13_CHECKS = 25/25 PASS`

`PHASE_U13_SCENE_PROBE = PASS`

`REMAINING_WORK_PACKAGES = 8`

`SYSTEMS_FORMALLY_ACCEPTED_AT_90 = 0`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
