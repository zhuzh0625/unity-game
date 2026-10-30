# 海岸疾驰 / Coast Rush

Windows 键盘街机赛车，Unity `6000.3.23f1` + URP。原创车辆和海滨道具在 Blender `5.2.1 LTS` 中制作。

## 开始游戏

双击 `play.cmd`，或运行 `Builds/Windows/CoastRush.exe`。构建目录由 Git 忽略，新检出需要先构建。

| 按键 | 操作 |
| --- | --- |
| Enter | 开始竞速 / 结算后再来一场 |
| T | 主菜单进入单人计时 |
| C | 主菜单切换四种车身配色 |
| W / ↑ | 加速，倒车时先刹停 |
| S / ↓ | 刹车，接近停止后倒车 |
| A、D / ←、→ | 转向 |
| 空格 | 手刹，降低后轮横向抓地力 |
| R | 返回最近有效检查点之后的安全路段 |
| Esc | 暂停 / 继续；结算后返回主菜单 |
| 暂停菜单 ↑ / ↓、Enter | 选择并确认继续、重新开始或返回主菜单 |
| M | 静音 / 恢复声音 |

赛道全长约 1.37 公里，每场三圈。竞速模式包括三位 AI 对手；计时模式只有玩家。必须按顺序通过 24 个检查点，逆行或离开路面穿越检查点不会计数。翻车或严重偏离赛道会自动复位。最佳比赛用时和车身配色保存在本机 PlayerPrefs。

## 编辑与构建

- 双击 `open.cmd` 或在 Unity Hub 打开本目录。
- 场景：`Assets/CoastRush/Scenes/CoastCircuit.unity`。
- Unity 菜单 `Coast Rush > Build coast scene` 重新生成本项目场景。此操作替换生成场景，手工调整前请另存场景副本。
- Unity 菜单 `Coast Rush > Build Windows player`，或在本项目 Editor 打开时执行 `build.cmd`。
- Git 在父目录统一管理，资源与对应 `.meta` 一起提交。`Library`、`Builds`、`tmp`、Blender 备份文件不入库。

## 资源与实现

- `ArtSource/CoastRushAssets.blend`：可编辑跑车、棕榈树、房屋、灯塔、岩石、护栏、隧道和道路模块。原始 Blender 默认场景保留。
- `ArtSource/build_assets.py`：从干净 Blender 场景生成原创资产和 FBX。已有 `CoastRushSource` 场景时主动拒绝覆盖。
- `Assets/CoastRush/Models`：导出的 FBX。导入模型统一旋转 180°，通过前向/右向标记校验。四个车轮是独立多材质网格，原点位于轮轴。
- `CoastBuilder.cs`：Unity 内生成连续路面、海岸地形、场景、光照、车辆、中文界面和 Windows 构建。静态道具按 64 米区域合并，保留远处物体剔除；采用 Forward、FXAA 和两级阴影。
- `CoastRoadside.cs`：沿赛道公共横断面生成连续护栏、红白路缘和闭合碰撞边界，弯道及环线首尾共用接点。护栏源模型保留在 Blender 资源库中。
- `VehicleController.cs`：WheelCollider 驱动、键盘与 AI 共用输入、车轮显示、手刹及复位。
- `RaceProgress.cs`：有方向的顺序检查点与三圈完赛规则。
- `VehicleAudio.cs`：本项目生成的引擎和轮胎合成音效。
- 中文字体 Noto Sans CJK SC 采用 SIL Open Font License，许可证随字体存放。

## 验证入口

- `CoastChecks.RulesAndAssets()`：检查点反例、三圈结算、道路碰撞采样、车辆配置及着色器。
- `CoastRoadside.Verify()`：检查弯道偏移连续性、护栏/路缘接头及全线护栏碰撞覆盖。
- Play 模式调用 `DrivingChecks.Begin()`：加速、刹停、倒车、暂停、复位和重开。
- 三圈自动驾驶使用实际 WheelCollider 和同一套车辆控制器；报告明确区分自动驾驶与人工键盘验收。
- 独立包传入 `-coast-verify` 时启动 38 秒自动驾驶验收，前 8 秒预热，记录后 30 秒的平均帧率、检查点和复位次数，截图并自动退出。普通启动不会启用自动驾驶。
- 独立包传入 `-coast-keyboard-verify` 时使用 Unity Input System 的键盘状态事件验证正常输入链路：Enter、WASD、方向键、手刹、复位、暂停和键盘菜单重开。该测试关闭自动驾驶，不使用车辆控制覆盖，也不等同于 Windows 物理键盘人工试玩。
- 验证记录位于 `doc/验收记录.md`。原始报告和游戏截图是保存在 `doc/` 的本地验收材料，不纳入 Git；新检出可通过上述入口重新生成。

首版是风格化独立赛车原型，音效为合成音。性能以最终 Windows 包在实际机器上的测量为准。
