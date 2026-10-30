# Phase U15 — 正式目录保存兼容与重启恢复

日期：2026-10-18。

## 结果

U14 发现的“本机正式存档目录首次能保存、连续保存失败”已通过新增不可变快照提交方式解决游戏兼容问题。本轮没有修改目录权限、关闭 EFS 加密、改变正常存档目录、删除旧玩家档或改动 Godot 玩法事实来源。

这不是 Windows 权限/证书根因修复：原 File.Replace 在该目录的拒绝原因仍未完全定位。游戏改用该目录已验证可行的“创建新快照并提交新文件”，不再要求替换已有文件。

## 实现与安全边界

### 保存与读取

- 原 `GameState → SaveRepository` 调用关系保持不变；没有修改 Chemistry、环境门槛、设备配方或地形源数据。
- 非加密目录且没有历史快照时，保留原先 `.tmp → File.Replace`、`.bak1/.bak2` 方式。
- EFS 加密目录自动采用 `save.json.history/` 快照方式；存在已提交快照后始终按该方式保存，不因目录属性改变而返回旧主档。
- 每次生成新的单调序号与 GUID 文件名，先写 `.json.tmp`、Flush(true)、校验完整封装和内容，再在同目录改名为此前不存在的 `.json` 文件；不覆盖既有主档或快照。
- 读取只认符合命名合同的已提交 `.json`，不认 `.tmp`。按序号读取当前与前两代；损坏后回退，三代都不可读则失败，不偷偷退到任意古老历史。
- 初次迁移时，原 Unity 主档及备份可作为不足三代时的恢复候选；保留旧文件原文。
- 延续 SHA-256、世界身份、状态结构和未来 schema 保护。未来格式不能被新快照遮蔽或覆盖。
- `HasSave` 和世界列表通过 Repository 识别“只有快照、没有主 save.json”的世界。

采用同目录新文件改名作为提交点，测试涵盖写入完成后、提交前中断与重试；没有宣称已模拟硬件断电、磁盘固件故障或同一世界多进程并发写入。API 参考：[Microsoft File.Move](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move?view=netstandard-2.0)。

### 失败时的玩家保护

- 新增 `TrySave()` 返回明确结果，原 `Save()` 保持可用。
- 失败会通过游戏内提示告知玩家，避免仅在日志中报错。
- 新建世界、加载其他世界、返回标题之前，必须成功保存当前运行世界；失败时留在当前世界。
- 正常退出通过 `Application.wantsToQuit` 检查保存结果，保存失败则拒绝该次退出；强制结束进程和操作系统强制关机不在此保护范围内。
- 事件在场景销毁时解绑，避免世界切换后旧对象继续接收回调。
- 结局页“重新开始”改为新建独立世界并保留原世界，不再删除旧进度。

## 验证

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 实际编译与合同测试 | 30/30 PASS | `Logs/phase_u15_smoke.log` |
| 本机正式目录连续提交/读取 | PASS，7 次，测试目录仍 Encrypted=True | 同上 U15_PERSISTENT_DIRECTORY |
| Windows 正式 Main 构建 | PASS，0 errors / 2 原有 warnings | `Logs/phase_u15_windows_build.log` |
| 正式目录下 Main 地形/掉落/世界槽探针 | PASS，不使用 U14 Logs 目录开关 | `Logs/phase_u15_scene_probe.log` |
| 保存失败时的菜单/退出/提示/重试探针 | PASS | 同上 U15_FAILURE_UI_PROBE |
| 独立游戏进程连续写入 | PASS，7 次 | `Logs/phase_u15_restart_write.log` |
| 随后启动的独立游戏进程恢复 | PASS | `Logs/phase_u15_restart_read.log` |
| 化学/地形权威导出 --check | PASS | 两个 exporter |
| git diff --check | PASS | 工作区检查 |

新增合同测试包括：旧档不变、连续多代、无需强制开关也继续读取/写入快照、两级损坏回退、拒绝任意古老回退、不同世界身份拒绝、提交前中断、临时文件忽略、重试、未来格式保护、第一代损坏后恢复旧档。

实际进程重启测试从同一唯一测试 token 生成的存档恢复：库存、有限采集剩余量、未拾取掉落、地形增量、玩家位置字段与剩余能量；验证矿块没有再生，拾取后保存不再出现该掉落。使用本机正式 persistentDataPath，没有切换到项目 Logs 存档目录。

失败 UI 探针故意把测试状态能量设为 NaN，触发 4 次 `invalid_state` 保存失败；随后检查真实菜单回调没有切世界、退出检查返回拒绝、Toast 包含错误提示，恢复有效状态后重试保存成功。日志中这 4 条为明确标记的预期故障注入，不是正常运行中新发现的保存异常。

所有测试使用唯一文件或新建唯一目录。未加载或改写普通玩家档；进程重启测试及故障诊断样本保留，场景探针只清理自己生成的隔离存档文件。测试是自动化游戏进程/实际 UI 回调验证，不是人工完整通关。

## 使用与后续限制

**手动备份世界时，必须一起备份 `save.json.history`（旧世界则是 `elemental_sea_save.json.history`），不能仅复制原主 JSON。** 已提交快照成为最新进度的来源；旧 Unity 构建不认识该历史目录，不能靠降级旧可执行文件读取最新进度。Godot 存档格式仍不与 Unity 直接互通。

目前保留历史快照，不自动回收，因此长期游玩会增加文件数和空间占用。正式读档仍仅使用最新三代恢复；空间管理、快照索引与性能需要继续完善。完整元数据、Agent/敌人快照、全部世界生命周期及长时间玩家流程仍未验收。

本轮消除了一个真实保存阻断，但不等于存档系统已完整签收，也不据此提高总体百分比。仍按 15 系统、8 个大工作包跟踪，每项至少 90% 的目标尚未正式验收。

复现：

```powershell
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile Logs/phase_u15_smoke.log
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u15-smoke -logFile "$PWD\Logs\phase_u15_scene_probe.log"
# 两个进程按顺序执行，第二个启动前等第一个退出；每轮使用新的 token。
$probeToken = [Guid]::NewGuid().ToString('N')
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u15-save-write',"--u15-token=$probeToken",'-logFile',"$PWD\Logs\phase_u15_restart_write.log") -WindowStyle Hidden -Wait
Start-Process '.\Builds\Windows\ElementalStarSea.exe' -ArgumentList @('-batchmode','--u15-save-read',"--u15-token=$probeToken",'-logFile',"$PWD\Logs\phase_u15_restart_read.log") -WindowStyle Hidden -Wait
```

`PHASE_U15_SAVE_DIRECTORY_COMPATIBILITY = IMPLEMENTED`

`PERSISTENT_DATA_DIRECTORY_REPEAT_SAVE = PASS`

`CROSS_PROCESS_SAVE_RESTORE = PASS`

`PHASE_U15_CONTRACT_CHECKS = 30/30 PASS`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
