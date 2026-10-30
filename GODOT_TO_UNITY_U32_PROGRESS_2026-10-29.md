# U32 玩家运动与氢悬浮接入进度

后续实际Main增量：新增AdvancePlayerMovement供正式键盘入口与场景探针共享，仍由Terrain.Move处理碰撞。Logs/phase_u32_scene_build.log构建0错误/2警告；Logs/phase_u32_restart_write.log行52通过真实反应解锁、起跳、72个固定时间步悬浮（1.2秒/18能量）、逐步无穿透检查、落地/时长重置、阻挡面板锁与保存。Logs/phase_u32_restart_read.log行22通过独立进程恢复能力、反应加悬浮准确能耗与无碰撞落地位置。U15/U16/U20回归通过。

隔离token：9696a644dbc54bf99be354a1fd217e40，普通玩家档未修改。起始位置/原料和固定步长输入为夹具，不是操作系统键盘注入或自然地下通行；移动过程未声称完整区域切换/环境曝光模拟。先前readiness未覆盖的自然路线、区域边界/电梯、动画触控待办继续保留。93组合同检查计数不变，实机和独立进程证据另列，不折算百分比。下方为实机增量之前的历史记录。

对照Godot PlayerMotor、MotionFrame、AbilityState、PlayerCharacter、prologue/ability_h2_hover.tres、reaction_h2_synthesis.tres及ReactionService。新增只读player_motor导出，保留源参数与哈希，不改Godot源。Unity按32源像素/单位转换220移动、520跳跃、1500重力与900终速，替换旧9/13/30常量，继续使用现有Terrain.Move碰撞。

氢反应成功提交后授予ability_h2_hover，失败/仅持有氢气不授予；空中按住空格最长1.2秒、每秒15能量、四分之一重力。预算不足或达到时长时，剩余帧时间应用普通重力。落地清零悬浮时长；面板交互锁停止运动且不耗能。能力数组持久化，空/重复ID拒绝存档；已有完成氢反应的旧档从正式完成记录恢复能力，不因物品存在自动解锁。瞬时运动速度/悬浮时长仍与源PlayerCharacter一样不声称是持久化玩家能力。

Logs/phase_u32_smoke.log累计93/93 PASS：源参数、轴夹限、跳跃与落地重置、悬浮重力/分时能耗/时长/终速/交互锁；真实ReactionService氢反应成功解锁、失败不授予、物品不能冒充能力、磁盘和旧完成进度兼容。Logs/phase_u32_build.log Windows构建Succeeded、0 errors、2 warnings。player_motor导出--check与git diff --check通过。

上述为合同、实际反应服务/磁盘与真实编译构建证据，未将其冒充实际Main键鼠悬浮路线。下一步仍需Main移动/能耗/碰撞运行验证、自然地下路线、攻击/悬浮动作、触控、切场景与存档位置边界。PLY保持PARTIAL，未声称达到90%；其他系统待办全部保留。未运行full verify、慢世界生成或17区域生成套件。
