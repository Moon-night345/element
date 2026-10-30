# Godot → Unity Phase U9 报告

日期：2026-10-11  
阶段：Equipment Upgrade and Late-Region Reachability Closure  
总体迁移估算：**73%**（U8：68%）

## 本阶段完成

- 迁移 Godot `MechanicalToolCatalog` 的三档正式镐具：

| Tool | Cost | Power | Speed | Heat | Cold | Corrosion | Radiation | Electrical |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| geological sampling pickaxe | 初始 | 1 | 1.00 | 0.20 | 0.20 | 0.20 | 0.15 | 0.15 |
| improved iron pickaxe | Fe ×4 | 2 | 1.35 | 0.45 | 0.25 | 0.45 | 0.40 | 0.40 |
| reinforced composite pickaxe | Fe ×6 + Cu ×3 | 3 | 1.70 | 0.70 | 0.70 | 0.70 | 0.65 | 0.65 |

- 制造严格要求实验室机械工具工作站；材料不足、重复制造或无所有权装备均不修改状态。
- 制造与装备保持分离，符合 Godot 行为。
- 当前装备、已拥有镐具、镐具抗性和防腐改装均可存档恢复。
- 区域采矿判定改为读取当前装备镐具的真实属性。
- 在实验室加入机械工具工作站玩家入口。
- 在正式世界区域加入 `resource_stone` / `resource_ore` 地形开采点；它们受区域采矿抗性约束。
- 死城石材与矿石可以按 Godot 成本用于防辐射升级。
- 改良铁镐可覆盖晶原、盐沼、熔火裂隙和磁能高原的正式采矿要求。
- 复合强化镐可覆盖辐射岩带、地幔熔流和核心外壳的正式采矿要求。
- 修正化学资源节点与地形采矿的语义边界：
  - 化学资源节点：直接采样，不执行镐具抗性门；
  - 地形块：执行采矿强度与环境抗性合同。
  - 此修正确保 Stage 0 防腐材料路线不会发生循环锁死。

## 正常玩家路线

1. 在锈蚀丛林直接采样防腐原料，完成 Stage 0 防腐镐路线。
2. 在死城开采 `resource_stone` 与 `resource_ore`。
3. 回实验室将辐射防护提升至至少 40%。
4. 经表土洞穴继续下潜至废弃矿道层。
5. 采样 Fe/Cu，并在机械工具工作站制造改良铁镐。
6. 制造复合强化镐，满足辐射带、地幔和核心外壳采矿要求。

## 验证结果

- Unity：`2022.3.62f3c1`
- 冒烟测试：**15/15 PASS**
- Windows x64 构建：**PASS**，0 errors，2 warnings
- 可执行文件真实启动：**PASS**
- 运行数据：17 区域、40 反应、15 设备、33 加工配方全部载入
- `git diff --check`：**PASS**（仅已有 LF/CRLF 提示）

## 保留的真实缺口

- 星球核能核心的地形采矿要求为热 `0.80`、辐射 `0.80`、电气 `0.75`，而 Godot 当前最高正式镐具只有 `0.70/0.65/0.65`；Godot 未提供更高镐具或对应 override 获取路线，因此 Unity 不擅自发明升级配方。
- 星球核心通行所需的最终准备合同仍缺净化材料、母液回收等完整玩家链路。
- Unity 尚未迁移 Godot 的逐格地形破坏、方块放置和地形增量存档；本阶段以可交互地形资源点承载正式材料与抗性合同。

## 阶段结论

`PHASE_U9_EQUIPMENT_AND_LATE_REGION_REACHABILITY = PASS`

下一建议阶段：**Phase U10 — Final Core Preparation and Endgame Reachability**。
