# Godot → Unity Phase U7 报告

日期：2026-10-11  
阶段：Laboratory Stage 0→3 Progression Closure  
总体迁移估算：**63%**（U6：58%）

## 本阶段完成

- 新增可由 `JsonUtility` 持久化的实验室里程碑与能力集合。
- 按 Godot 权威逻辑接入 Stage 0 腐蚀路线：
  - 地质采样镐修复；
  - 锈蚀丛林腐蚀门发现；
  - 铁/氯或锈蚀金属残渣/氯化物沉积物原料；
  - 防腐基材；
  - 防腐涂层；
  - 在废墟实验室为采样镐装配涂层并进入 Stage 1。
- 接入 Stage 1→2 三项能力：
  - `PRECISE_ANALYSIS`
  - `DISTILLATION`
  - `SOLID_LIQUID_FILTRATION`
- 接入 Stage 2→3 九项能力合同：
  - 上述三项；
  - `THERMAL_PROCESSING`
  - `GAS_HANDLING`
  - `CONDENSATION`
  - `NATURAL_CRYSTALLIZATION`
  - `BATCH_REACTION`
  - `ELECTROCHEMISTRY`
- 能力只在设备成功加工并领取输出，或晶洞深渊自然结晶成功后授予。
- 制造/拥有/放置设备和访问区域本身不会授予能力。
- 新增晶洞深渊自然结晶世界节点，支持浓缩盐溶液或矿物溶液，浓度阈值为 `0.80`，产出晶体与母液。
- 新增全新存档零阶段注入的 Stage 0→3 端到端验证与存档往返验证。

## 验证结果

- Unity 版本：`2022.3.62f3c1`
- 冒烟测试：**13/13 PASS**
- Windows x64 构建：**PASS**
  - errors：0
  - warnings：2
  - 输出：`Builds/Windows/ElementalStarSea.exe`
- 可执行文件真实启动：**PASS**
  - Unity 引擎初始化成功；
  - Direct3D 11 初始化成功；
  - 载入 17 区域、40 反应、15 设备、33 加工配方；
  - 启动日志未发现脚本异常。
- `git diff --check`：**PASS**（仅已有 LF/CRLF 提示）

## 未在本阶段解决

- Godot 的 Agent 系统、17 区域专属行为与跨区回忆尚未迁入 Unity。
- 完整任务/门禁叙事与实验室阶段反馈 UI 尚未完全对齐 Godot。
- 17 区域环境机制、存档兼容迁移及完整发布级回归仍未闭合。
- Godot 已记录的 gameplay event backlog 不在本阶段范围。

## 阶段结论

`PHASE_U7_LABORATORY_PROGRESSION = PASS`

下一建议阶段：**Phase U8 — 17 Region Environment and Traversal Contract**。
