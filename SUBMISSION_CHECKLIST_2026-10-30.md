# 提交版收尾检查

当前交付：Builds/Deliverables/AstralElementSea_Windows_Submission_2026-10-30.zip

| 提交检查 | 结果与证据 |
| --- | --- |
| 画面比例与排版 | 1280×720、1280×800、1024×768实际Unity渲染；HUD/地图/设备/对话/合成/背包/暂停/标题截图在Logs，代表截图已查看，无所检查按钮重叠。标题图片等比显示，实验室行走面与玩家脚底对齐。 |
| 长内容边界 | 28行长对话、8个长选项各自具有滚动区域；长设备说明与记忆文本使用相同滚动组件，背包40行检查。Logs/release_ui_*.log中LONG_CONTENT、MAP_UI、UI_REVIEW均PASS。 |
| 玩家提示 | 33个设备工艺使用中文名称，设备状态/失败原因改为玩家提示；存档列表显示区域中文名与阶段，领取按钮不显示内部实例ID；帮助包含地图、跳跃、悬浮、攻击及暂停。未改变工艺规则与产物。 |
| 实际系统按键 | Logs/submission_system_keys.log：系统键盘事件M/M/TAB/ESC/ESC/ESC经Unity实际Input处理全部PASS。仅对已确认前台属于测试游戏的窗口发送，失焦会停止。此项覆盖面板键，不冒充整条演示的物理键鼠手玩。 |
| 序章演示 | Logs/release_candidate_route.log：新状态苏醒→对话→气闸发现→修镐→有限氢源两次采集/冷却→实际材料按钮与合成→引路人演示→离开→返回/磁盘恢复PASS。沿共享移动入口固定步长行走，无库存或任务注入；非OS键盘整段路线。 |
| 标题/新建/加载 | Logs/release_candidate_menu.log：实际按钮、两次真实场景重载、新存档、暂停保存返回、列表选择及进度位置恢复PASS。 |
| 构建 | Logs/submission_release_candidate_build.log：Unity2022.3.62f3c1，Succeeded，0错误、2警告。git diff --check通过。 |
| 最终包独立启动 | 最终ZIP已解压到新目录，解压后exe再次通过菜单与序章路线，Logs/submission_archive_menu.log、submission_archive_route.log均PASS。 |

最终ZIP 89,100,544字节，SHA256：8555727D2F77C6CCA13500D590F98882D304562A0EA40D088C1A8E8FD1043947。校验文件SHA256_Submission.txt同目录。旧v1/v2包保留，提交优先使用本文件列出的Submission包。

所有检查使用独立存档，普通玩家存档未改写。构建包含Data、Mono、UnityPlayer.dll及开始游玩.txt，应整体解压运行。最终包作为可运行演示交付；本检查范围为已确认的提交阻断项与核心演示，不宣称所有后续区域/终局/完整Godot迁移已验收，也不能保证未知问题绝不存在。完整迁移基线保留，不阻碍本次演示交付。
