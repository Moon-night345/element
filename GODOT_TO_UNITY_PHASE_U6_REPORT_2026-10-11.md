# Godot → Unity 迁移 Phase U6 报告

日期：2026-10-11  
阶段：Phase U6 — 上游世界化学、实例设备与工业液体物流

## 结论

Phase U6 实现和自动验证完成。保守整体迁移进度从 Phase U5 的 54% 提升到 **58%**。

本阶段解决了 U5 中服务测试可执行、但缺少上游世界来源的问题，并完成 Device 15 的实例级
液体存储。它仍不代表正常新存档已经能完成整条工业主线；正式 Laboratory Stage 0→3
能力进阶尚未迁移，是下一阶段的主要门槛。

## 已完成

### 世界化学上游入口

- 荧光洞窟：低氧有机过程，消耗 `organic_glucose`，生成乙醇与 CO2，并把紧邻的
  Godot `register_vapor(ethanol, 1)` 接成一次“发酵并导流”世界交互。
- 废弃矿井：采集正式钡载矿物块；必须经 Device 05 分析后才能制备钡工艺液。
- 熔火裂隙：采集正式含硫矿物块；必须分析后才能制备硫酸盐工艺液。
- 辐射带：采集并处理正式含气沉积块，生成污染气流及 generated-gas 状态。
- 四类入口均生成可交互世界节点，而非仅测试状态注入。

### 动态气体链

- 高压气罐不再只支持 CO2 默认值，可按 Godot 白名单选择实际 generated gas。
- 吸收塔可处理污染气、SO2 和受污染气流。
- 受污染气流产生 `compound_purified_gas_stream` 与
  `compound_contaminated_absorbent`，未套用普通污染气的错误产物。

### Device 15 — 大型工业液体储罐

- 存储状态属于具体设备实例，而非全局临时变量。
- 容量固定来自权威设备数据，目前为 20。
- 只接受 Godot `ItemContractCatalog` 中定义的正式液体 ID。
- 同一实例只允许一种液体。
- 存入、容量超限、物态错误、液体不相容与取出均为原子操作。
- 存入和取出保持库存数量守恒。
- 储存物 ID、数量、容量、部署区域和世界坐标均进入存档。
- `D` 设备控制台提供液体选择、存入和取出按钮。

### 部署设备世界化

- 新部署设备记录当前 region ID 和玩家坐标。
- 部署后生成世界设备实体和名称牌。
- 靠近实体按 `E` 可打开该设备的控制台。
- 读取存档时重建已部署的世界设备实体。
- 旧存档中没有坐标的 U4 设备实例不会被错误堆叠到实验室原点。

## 验证结果

- Godot 权威导出检查：PASS。
- Unity `2022.3.62f3c1` 编译：PASS。
- 冒烟测试：**12/12 PASS**。
- 世界化学链测试：PASS。
- 污染气生成 → 气罐 → 吸收塔：PASS。
- Device 15 物态/相容性/容量/守恒/存档：PASS。
- Windows x64 构建：PASS，0 errors，2 warnings。
- 独立 Player 启动：PASS；17 regions / 40 reactions / 15 devices /
  33 processing recipes 正常载入。
- `git diff --check`：PASS；只有现有 LF/CRLF 提示。

构建产物：`Builds/Windows/ElementalStarSea.exe`

## 仍然缺失

- Unity 的正式 Laboratory Stage 0→1、1→2、2→3 capability contract 尚未迁移。
  当前正常新存档的 `laboratoryStage` 不会随玩法提升，因此高阶段设备仍会被正确锁定。
- Godot Stage 0→1 的腐蚀处理、腐蚀镐升级和早期设备里程碑尚未迁移。
- Stage 1→2 的精确分析、蒸馏、过滤三能力汇总与升级入口尚未迁移。
- Stage 2→3 的九项能力汇总与升级入口尚未迁移。
- 世界设备实体目前使用程序化占位视觉；Godot 15 台正式设备原图尚未全部导入 Unity。
- 尚未实现设备放置碰撞、占地、保护区域和重叠校验。
- 本次独立 Player 验证为 NullGfx/headless 启动；shader 提示来自无图形设备，不是游戏逻辑异常。

## 下一阶段

Phase U7 应迁移 Laboratory Progression：

1. Stage 0→1 腐蚀路线；
2. Stage 1→2 三项正式能力；
3. Stage 2→3 九项能力；
4. 升级事件、存档和设备解锁；
5. 从全新存档执行一次不注入阶段值的端到端工业链测试。
