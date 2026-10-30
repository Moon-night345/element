# Phase U12 — 可交互地形、玩家碰撞与增量恢复（第一批）

日期：2026-10-18。

## 已实现的玩家行为

- 现有17区域切片地面由静态图片改为可修改的Unity Tilemap，共51,200格。实验室内景保留。
- 按住左键按方块硬度和当前镐速持续开采；换目标、松开、打开面板会取消当前进度。
- 同一方块只能挖掘一次，挖后移除可见地块及格子碰撞，产物直接进入背包。
- 右键放置背包中的正式可建造方块，B循环切换；帮助面板增加操作提示。
- 放置检查有效区域、库存、目标为空、相邻支撑、6格范围、玩家占用、已放置设备和保护区域。
- 玩家使用0.75格宽、2格高的占用矩形，分步处理水平/垂直碰撞，跳跃可以落在已放置的方块上。
- 采矿继续检查镐具强度和目标区域的热/冷/腐蚀/辐射/电气抗性。最高正式镐具仍不能绕过星球核心的高抗性要求。
- 挖掘与放置更新存档地形增量，保留放置方块身份。恢复到原始地块时删除冗余增量。
- 存档记录种子、生成版本、玩家位置；旧存档没有新字段时从现有默认切片初始化，位置不安全则回实验室。
- 区域识别优先使用实际地形分区，避免从一层向下挖一格就被旧的高度公式误判为下一层。
- 设备部署高度读取实际玩家脚下坐标。

## 权威数据与源项目边界

新增 `tools/export_terrain_catalog.py`，只读取Godot：

```text
src/mining/mining_catalog.gd
src/mining/biome_block_catalog.gd
src/world/region_resource_catalog.gd
src/content/item_contract_catalog.gd
```

生成 `Assets/Resources/Data/terrain_catalog.json`：4个基础方块、60个正式区域身份，共64项；记录来源文本哈希。导出角色硬度、强度、物品ID与图标坐标，对齐死城玻璃和核心角色的特殊注册覆盖。

源项目中部分正式身份没有有效的 `terrain_material_for()` 放置映射，导出为不可执行放置，不编造映射。64是独立身份数，**不是272个视觉瓦片变体的完整迁移数**。

Godot `MiningProgressTracker`、`MiningCatalog` 和 `WorldSession.terrain_placement_status()` 是计时、硬度和放置检查的来源。尺寸换算为Godot32像素=Unity1格；192像素建造范围=6格。

## 本阶段仍然不同的内容

1. 初始地形仍使用Unity既有7地表+9地下、实验室的切片布局，按种子稳定分配地块身份；没有宣称复刻Godot完整连续地貌、噪声、洞穴、地脉或遗迹结构算法。
2. 切片底层、实验室与电梯周边受保护，以保留现有正式电梯路线。底层保护是过渡措施，后续迁移连续洞穴/深度门后应再对齐，而不是把它记作Godot完整地形等价。
3. 产物直接入包；Godot的落地掉落、拾取范围与掉落状态存档尚未迁移。
4. 旧资源节点/死城废料交互仍保留，其中存在可重复采集的兼容入口；有限采集源容量和旧节点替换仍在INV-02、TER-02待办中。
5. 玩家悬浮、完整能力运动、Godot逐类1×2空间验收、建造物选择界面与正式272视觉变体尚未完成。
6. 地形增量恢复已经加入，但存档依然是原有单文件JSON；校验和、原子提交、多槽和备份回退留待保存恢复阶段。
7. 损坏、重复、越界、未知方块或不支持版本的地形增量会明确拒绝，尚未提供玩家可见的备份恢复界面。

因此，本阶段交付的是可操作且经过验证的地形基础，**地形、玩家和存档三个系统都仍为PARTIAL，未签署≥90%验收**。

## 验证结果

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| Unity真实编译与冒烟检查 | 22/22 PASS | Logs/phase_u12_smoke.log |
| Windows x64构建 | PASS，0 errors / 2原有warnings | Logs/phase_u12_windows_build.log |
| 实际游戏场景探针 | PASS | Logs/phase_u12_scene_probe.log |
| 地形权威导出检查 | PASS | python tools/export_terrain_catalog.py --check |
| 原化学/区域导出检查 | PASS | python tools/export_chem_data.py --check |
| 差异空白检查 | PASS | git diff --check |

新增三组检查包含：十轮挖放守恒、重复开采拒绝、异种放置身份与增量恢复、越界/非地形/占用/无支撑/超距/玩家/设备/保护区拒绝、背包溢出零破坏、镐具强度和核心热抗性门、计时取消、坏增量、纵向高速落地、横向穿墙防护、种子确定性与挖掘后的区域识别。

实际场景探针使用新构建的独立进程，真实加载51,200格、Tilemap和碰撞模型，执行挖掘→磁盘存档→读档重建→放置恢复→落地检查。它使用唯一命名的测试存档，成功后仅删除自身创建的测试存档。普通玩家存档不参与探针。

该探针验证场景和服务连接，不等于鼠标键盘输入回放、视觉等价验收或完整人工通关。

## 重复执行

```powershell
python tools/export_terrain_catalog.py --check
python tools/export_chem_data.py --check
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -nographics -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.ValidateMigration -logFile Logs/phase_u12_smoke.log
& 'D:\18485\Unity\2022.3.62f3c1\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod ElementalStarSea.EditorTools.UnitySliceBuilder.BuildWindows64 -logFile Logs/phase_u12_windows_build.log
& '.\Builds\Windows\ElementalStarSea.exe' -batchmode --u12-smoke -logFile "$PWD\Logs\phase_u12_scene_probe.log"
```

`PHASE_U12_TERRAIN_FOUNDATION = IMPLEMENTED`

`PHASE_U12_AUTOMATED_CHECKS = 22/22 PASS`

`PHASE_U12_ACTUAL_SCENE_PROBE = PASS`

`TERRAIN_SYSTEM_90_ACCEPTED = NO`

`PER_SYSTEM_90_ACCEPTANCE = NOT_READY`
