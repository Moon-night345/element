# element

## 元素星海·裂核 — Unity 提交演示版

这是现有 Godot 项目向 Unity 迁移后的当前工作版本，包含工程源码、游戏资源、数据、阶段记录和可运行 Windows 演示包。

### 直接运行

下载 [Windows 提交演示包](Builds/Deliverables/AstralElementSea_Windows_Submission_2026-10-30.zip)，整体解压后运行 `Windows/ElementalStarSea.exe`。不要只复制 exe；请保留同目录的 Data、Mono 和 UnityPlayer.dll。操作步骤见压缩包内的 `开始游玩.txt`。

包 SHA256：`8555727D2F77C6CCA13500D590F98882D304562A0EA40D088C1A8E8FD1043947`。

### 打开工程

1. 使用 Unity **2022.3.62f3c1**，版本锁定记录在 `ProjectSettings/ProjectVersion.txt`。
2. 在 Unity Hub 中打开仓库根目录，等待资源导入。
3. 打开 `Assets/Scenes/Main.unity`，按 Play 从标题开始。
4. 正式构建场景已加入 Build Settings；构建入口是 `ElementalStarSea.EditorTools.UnitySliceBuilder.BuildWindows64`，输出到 `Builds/Windows`。

运行工程不需要重新导出 Godot 数据：当前导出结果已包含在 `Assets/Resources/Data`。`tools` 中的导出脚本供继续对照原 Godot 工程时使用。

### 操作与演示路线

A/D 或左右键移动，空格跳跃，E 交互，C 合成，TAB 背包，D 设备，M 地图，H 帮助，ESC 暂停。合成氢气后，空中按住空格可短时悬浮并消耗能量。

演示顺序：苏醒 → 引路人对话 → 查看气闸 → 修复采样镐 → 截取两份氢 → 合成 H₂ → 向引路人演示 → 离开实验室。支持返回实验室、保存返回标题和加载世界。

### 当前范围

本版是可运行的提交演示版，不表示所有 Godot 内容已经完成迁移。界面比例、长内容滚动、主要面板、系统面板按键、序章演示、新建/加载和解压启动已做检查。证据与范围见 [提交检查记录](SUBMISSION_CHECKLIST_2026-10-30.md)；完整迁移待办见 [迁移基线](MIGRATION_90_COVERAGE_BASELINE_2026-10-18.md)。

仓库不包含 Unity Library/Temp 缓存、临时日志、测试存档或旧验证目录；这些文件会在本机按需生成。Windows 最新提交包作为明确的交付文件单独保留。
