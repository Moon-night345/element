# Godot → Unity Phase U8 报告

日期：2026-10-11  
阶段：17 Region Environment and Traversal Contract  
总体迁移估算：**68%**（U7：63%）

## 本阶段完成

- 从 Godot `WorldContentCatalog` 对齐 17 个正式区域的采矿环境合同。
- 新增五类独立采矿抗性判定及独立失败原因：耐热、耐寒、耐腐蚀、抗辐射、电气防护。
- 对齐地质采样镐基础抗性：热 `0.20`、冷 `0.20`、腐蚀 `0.20`、辐射 `0.15`、电气 `0.15`。
- Phase U7 防腐镐升级现在持久化腐蚀抗性 `0.30`。
- 地形资源开采在提交库存前执行所在区域的正式抗性检查；化学资源节点保持 Godot 的直接采样语义。
- 对齐深层通行门：
  - 深度 5 起要求辐射防护至少 `0.35`；
  - 核心外壳要求 Laboratory Stage 3；
  - 星球核能核心要求 Stage 3 和最终核心准备合同。
- 新增 Godot 同成本的辐射防护升级：`resource_stone ×3 + resource_ore ×1`，每次 `+0.20`，上限 `0.80`。
- 辐射状态改为 Godot 生存模型：防护削减暴露、辐射值上限 100、超过 35 后持续伤害。
- 对齐锈蚀丛林环境腐蚀伤害；防腐镐升级后伤害倍率降至 `0.35`。
- HUD 显示当前辐射防护百分比。
- 电梯在移动前执行通行合同并显示准确阻断原因。
- 补齐此前缺失的正式路线：`dead_city_ruins ↔ topsoil_caves` 地下竖井。
- 环境抗性、辐射防护与区域访问结果均可存档恢复。

## 正式采矿要求

| Region | Requirement |
|---|---|
| prologue_lab | NONE |
| dead_city_ruins | NONE |
| rust_jungle | corrosion 0.30 |
| crystal_tundra | cold 0.25 |
| salt_marsh | corrosion 0.35 + electrical 0.25 |
| ember_rift | heat 0.35 |
| sand_desert | NONE |
| magnetic_plateau | electrical 0.35 |
| topsoil_caves | NONE |
| fossil_strata | NONE |
| fluorescent_grotto | corrosion 0.15 |
| geode_abyss | NONE |
| abandoned_mine | corrosion 0.20 |
| radiation_belt | radiation 0.45 |
| mantle_flows | heat 0.55 + radiation 0.50 |
| core_shell | radiation 0.65 + electrical 0.55 |
| planetary_core | heat 0.80 + radiation 0.80 + electrical 0.75 |

## 验证结果

- Unity：`2022.3.62f3c1`
- 冒烟测试：**14/14 PASS**
- Windows x64 构建：**PASS**，0 errors，2 warnings
- 可执行文件真实启动：**PASS**
- 运行数据：17 区域、40 反应、15 设备、33 加工配方全部载入
- `git diff --check`：**PASS**（仅已有 LF/CRLF 提示）

## 仍存在的真实可达性缺口

- Godot 权威数据目前只给腐蚀抗性提供了正式玩家升级路线；Unity 尚未迁移耐寒、耐热、抗辐射和电气抗性的获得路线。因此合同已生效，但部分中后期矿物会被正确阻断。
- 辐射防护升级逻辑已迁移，但 `resource_stone` 与 `resource_ore` 的正常世界产出入口尚未迁移，玩家当前不能自然完成该升级。
- 最终核心准备中的净化材料、母液回收等环节尚未完整迁移，因此 `planetary_core` 会被正确阻断。
- 区域敌人、战斗和完整地形采掘仍未达到 Godot 等价状态。

## 阶段结论

`PHASE_U8_REGION_ENVIRONMENT_TRAVERSAL = PASS`

下一建议阶段：**Phase U9 — Equipment Upgrade and Late-Region Reachability Closure**。
