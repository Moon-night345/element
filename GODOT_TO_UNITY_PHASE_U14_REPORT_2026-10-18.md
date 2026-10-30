# Phase U14 — 有限采集、实体掉落与存档目录阻断

日期：2026-10-18。目标仍是每个系统分别达到 Godot 功能的至少 90%，本轮不宣称达标。

后续更新：U15 已通过不可变快照方式解决本机目录的连续保存兼容，并补正式目录/独立进程恢复验证。本文以下保留 U14 当时的失败记录，不代表 U15 当前状态，见 `GODOT_TO_UNITY_PHASE_U15_REPORT_2026-10-18.md`。

## 本轮已实现

- 新增 `FiniteResourceService`。正式区域的每种资源容量为 6；区域预览节点与分页采样共用 `region:region_id:item_id`，不再各自无限生成。容量耗尽、库存拒收和非法区域/资源均不发物品；库存拒收不扣容量。
- 氢泄漏舱按源定义改为容量 2、每次 1 份、运行内冷却 0.6 秒；两次截取后推进原 Unity 合成任务。污染矿物封存罐容量为 4。
- 旧石料、矿石入口改为每区域/物品各容量 6 的有限兼容采样器；这是当前切片适配，不是 Godot 完整实体矿脉生成。
- 钡、含硫、含气样本与死城回收原料入口不再直接生成物品，改为选择附近、可开采且身份匹配的实际方块。找不到时提示移动并挖掘地层；保留现有采矿强度/环境门槛。快捷入口尚未统一到按住鼠标的硬度计时流程。
- 新增 `WorldDropService`。正式鼠标采矿走 `MineToDrop`，挖掉方块只产生带唯一实例 ID、物品、数量、来源方块、来源区域和位置的掉落实体。近身拾取成功后才入库并删除实体；拒收/距离不足不删，重复拾取不复制。
- 拾取区域按 Godot 脚底坐标的 44×76 像素矩形、偏移 (0,-32) 换算；显示为 20×20 像素等价尺寸，沿用静态掉落而非额外加入重力。已做边界包含关系测试。
- 保存采集源剩余容量与未拾取掉落。存档校验拒绝越界容量、重复来源/实体、非法数量及非有限坐标。进入世界重新绑定服务到加载后的 GameState，恢复掉落显示。

兼容边界：`TerrainRuntimeService.Mine` 保留为 U12 立即拾取合同 API；实际 GameBootstrap 玩法入口均使用 `MineToDrop`，没有调用旧 API。源物品数量仍使用既有 Unity int32，并非完整迁移 Godot int64 上限。

## 权威来源

| 内容 | Godot 来源 | Unity 对应 |
| --- | --- | --- |
| 区域资源容量 6、入库成功后才扣容量 | `src/session/world_session.gd`：ENVIRONMENT_RESOURCE_CAPACITY / gather_environment_resource | FiniteResourceService.GatherRegion |
| 氢源容量/单次产量/冷却 | `content/prologue/source_hydrogen_leak.tres`、WorldSession.capture_hydrogen | lab hydrogen action + finite source |
| 封存罐 4、矿脉源 6 | `src/world/chemical_resource_catalog.gd`：SOURCE_TYPES | sealed source / bounded slice ore sampler |
| 采集失败不扣容量 | `src/gathering/capture_source_model.gd` | FiniteResourceService.Capture |
| 掉落描述、拾取矩形和显示尺寸 | `src/world/world_drop.gd`、WorldSession.mine_terrain_cell / collect_world_drop | MineToDrop / WorldDropService / GameBootstrap |

区域菜单仍允许选定材料，而 Godot gather_environment_resource 顺序选择未耗尽材料；容量与守恒合同已迁移，UI选择方式不称完全等价。掉落唯一实例 ID 和快照是 Unity 的持久化适配，不宣称 Godot 与 Unity 存档格式互通。旧 Unity 档没有历史采集记录，无法反推此前消耗；首次接入后从相应默认容量开始记账，不删旧库存。

## 加强的进阶验证

原 U11 路线直接 AddItem 模拟原料采集，掩盖了二级采矿强度要求。本轮将该路线改为：

1. 用有限区域来源完成防腐 Stage 1。
2. 从有限石料/矿石采样器获取 6 石料与 2 矿石，真实支付两次防辐射升级。
3. 到正式废弃矿道资源表采集 Fe×4，返回实验室按原配方制造并装备改良铁镐。
4. 从死城真实有限方块采矿、拾取、分析并回收原料，制造/放置 11 台设备。
5. 通过有限区域材料与真实加工操作达到 Stage 3，并读档核对。

没有直接注入库存、设备或实验室阶段。区域移动仍为服务模拟，修镐发现等前序动作仍通过进阶服务调用；没有验证实际走路、挖掘通道与 GUI 全程操作，不是新玩家完整通关证据。

## 验证结果与必须披露的失败

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity 实际编译与合同测试 | 27/27 PASS | `Logs/phase_u14_smoke.log` |
| Windows x64 正式 Main 构建 | PASS，0 errors / 2 原有未使用字段 warnings | `Logs/phase_u14_windows_build.log` |
| 正式存档目录连续保存 | **FAIL：文件替换被拒绝** | `Logs/phase_u14_encrypted_directory_failure.log` |
| 项目 Logs 内隔离目录的实际 Main 场景探针 | PASS | `Logs/phase_u14_scene_probe.log` |
| 正式化学/地形数据导出 --check | PASS | 两个 exporter |
| git diff --check | PASS | 工作区检查 |

实际 Main 场景探针验证：容量 6 的采集入口第 7 次不再增加、容量保存、51,200 格世界、挖矿移除可见 Tile、生成掉落视觉、未拾取快照磁盘往返、拾取/删除视觉、放置守恒、碰撞，以及原世界槽按钮回归。它是独立 Windows 游戏进程中的代码探针，不是人工鼠标验收。

### 正式存档目录阻断（尚未解决）

本机 `Application.persistentDataPath` 对应目录为 `C:/Users/18485/AppData/LocalLow/DefaultCompany/UnityProject`，带 EFS Encrypted 属性。在唯一隔离测试文件上，首次保存成功，第二次原子替换失败。独立 PowerShell/.NET 的唯一诊断目录也复现 `File.Replace` 访问拒绝；这并不足以确定最终根因是证书、权限、安全软件还是其他目录策略。

尝试忽略元数据合并错误及同卷原生替换也未解决，相关试验代码已撤回；正式写入仍保留原有安全原子提交，不采用删除主档或直接覆盖主档的降级方式。不修改目录权限，不解密，不移动或删除普通玩家存档。

为区分玩法与目录问题，新增仅由测试参数启用的项目 Logs 唯一目录。该目录下的真实场景验证通过，**不能用它替代正式目录通过结论**。正常运行仍使用原 persistentDataPath，连续存档阻断仍需优先解决。失败探针及诊断文件保留供排查；成功探针仅清理自己的隔离存档副本。

原子替换 API 的语义参考：[Microsoft File.Replace](https://learn.microsoft.com/dotnet/api/system.io.file.replace)、[Microsoft MoveFileExW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw)。这些文档不证明本机失败的具体根因。

复现隔离探针：

```powershell
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u14-smoke --u14-smoke-local-save -logFile "$PWD\Logs\phase_u14_scene_probe.log"
```

省略 `--u14-smoke-local-save` 时使用原真实目录，当前该条件下失败。

## 还剩多少

仍按 U13 的 8 个大工作包跟踪，不能说只剩 8 个小功能，也不能根据 27 组测试推算百分比。15 个系统尚无一项完成“≥90%”正式签收。

本轮收窄了有限采集、掉落实体和相应快照缺口，但没有完成：完整种子世界/洞穴/结构/化学源生成、完整运动/生存/战斗、Agent区域决策、化学设备全部分支、碎片信任与记忆序列、完整UI/地图/输入、完整存档生命周期、美术音效和新档双结局全程验收。正式目录连续保存失败是新增发现的阻断，应优先排查后再继续推进。

`PHASE_U14_FINITE_SOURCES_AND_DROPS = IMPLEMENTED_WITH_LIMITS`

`PHASE_U14_CONTRACT_CHECKS = 27/27 PASS`

`PHASE_U14_ISOLATED_SCENE_PROBE = PASS`

`PERSISTENT_DATA_DIRECTORY_REPEAT_SAVE = FAIL`

`REMAINING_WORK_PACKAGES = 8`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
