# 远程弹体与敌人攻击动作验收

日期：2026-11-06。项目：余烬行者 / unity-arpg。Unity 6000.3.23f1，Unity CLI 1.0.0-beta.17，本机 Windows 非开发版。

## 最终效果

- 玩家余烬弹：金橙火焰核心、起伏火尾、柔光和消散尾迹。
- 骸骨弓手：有金属箭头、木质箭杆和尾羽的实体箭矢，箭头朝向与飞行方向一致。
- 沼泽咒师：紫色法术核心、旋纹与环形光晕，施法时手部蓄能。
- 守墓者：狂怒冲击仍发射六向弹体，改用红橙熔火碎片和短尾迹。
- 命中角色或墙面：箭矢产生短暂碎屑，法术产生光环及火花；不改变原有伤害、飞行速度和沿移动线段检测墙体的规则。
- 四类敌人各有待机、蓄力、出手、收势四个姿势帧；分别表现举斧挥砍、拉弓放箭、举杖施法和举锤下砸。伤害或发射事件直接触发出手帧，随后 0.28–0.42 秒收势期间停步，避免滑行动作。

新素材：[EnemyAttacks.png](../../Assets/Emberfall/Resources/Art/EnemyAttacks.png)。使用内置 imagegen 根据原敌人图集生成，并修正留白；按各行足底位置设置锚点。完整提示词和来源见 [ART_SOURCES](../../ART_SOURCES.md)。弹体继续采用项目原生程序纹理，四类纹理预先生成并缓存；弹体层级、发射与命中特效均复用。

## 验证结果

最终构建：[final-build.log](final-build.log)，包含 `EMBERFALL_BUILD_OK`，构建退出码 0。之后仅新增文档、清理 Unity 字体序列化文件行尾空白，没有再改变运行逻辑。

| 验证范围 | 结果 | 记录 |
| --- | --- | --- |
| 本轮专项 | 33/33 通过 | [presentation-results.txt](Final/presentation-results.txt) |
| 前轮优化回归 | 41/41 通过 | [regression-results.txt](Regression/regression-results.txt) |
| 原有功能冒烟 | 35/35 通过 | [smoke-results.txt](Smoke/smoke-results.txt) |
| 正常初始角色完整流程 | 54.80 秒完成，1,156 个键盘状态事件、10 击杀，回营地完成委托，剩余生命 94、血瓶 3 | [keyboard-journey.txt](Journey/keyboard-journey.txt) |

以上运行退出码均为 0。专项检查覆盖出手前不发射、出手帧与战斗事件同步、收势与回到待机、箭矢方向、暂停冻结动作与尾迹、法术命中伤害、隔墙拦截、施法中被击杀取消发射、切图清除活动特效及复用池保留。

专项与冒烟使用明确测试夹具和独立存档。完整流程则使用正常初始角色与模拟键盘状态步行、战斗、拾取和返回营地，不传送、不直接扣除敌人生命，也不赠送装备；仍不等同于硬件键盘人工手感验收。普通玩家存档未用于测试。

## 实际画面

在可见窗口中核对了弹体、攻击前后姿势、武器边缘与脚底位置。专项截图采用 4.5 单位近景相机以便检查，普通游戏相机保持原设置。

- [玩家火焰弹](Final/player-ember.png)
- [弓手拉弓](Final/enemy-1-windup.png)、[放箭](Final/enemy-1-release.png)、[飞行箭矢](Final/enemy-1-projectile.png)
- [咒师施法与紫色弹体](Final/enemy-2-projectile.png)
- [劫掠者举斧](Final/enemy-0-windup.png)、[命中挥砍](Final/enemy-0-release.png)
- [守墓者举锤](Final/enemy-3-windup.png)、[下砸](Final/enemy-3-release.png)、[熔火弹体](Final/enemy-3-projectile.png)

首轮 `Run1` 中的箭矢截图被尚未刷新的暂停面板遮挡；调整截图等待时序后，最终 `Final` 目录的箭矢画面已再次实际查看。最终验收采用 `Final` 目录。

## 固定负载采样

可见窗口、60 FPS 限制，每场景 180 帧。满背包为 16 件装备；战斗为 30 个敌人，给予足够生命并持续请求攻击。原始数据：[performance.csv](Performance/performance.csv)。

| 场景 | 中位帧时间 | P95 | P99 | SpriteRenderer 数量 |
| --- | ---: | ---: | ---: | ---: |
| 营地待机 | 16.668 ms | 16.772 ms | 16.979 ms | 315 |
| 满背包 | 16.669 ms | 16.766 ms | 17.036 ms | 315 |
| 30 敌人战斗 | 16.670 ms | 17.270 ms | 18.532 ms | 467 |

数量是对象计数，不是绘制调用数。帧时间包含限帧等待；这次短采样不构成长期压力测试或 GPU 净耗时结论。发布版 GC 计数器仍不可用，CSV 为 `-1`，不报告 GC 分配改善比例。

运行日志仍包含既有 ComputeBuffer 退出释放警告，最终各运行正常退出，没有复现原生崩溃。

## 入口

普通试玩：[Emberfall.exe](../../Builds/Emberfall/Emberfall.exe)。

专项复验参数：`-combat-presentation-check -presentation-output <新的绝对目录>`。不要隐藏窗口，否则截图可能为黑色。

实现入口：`ProjectileArt.cs`、`ProjectileVisual.cs`、`CombatFeedback.cs`、`ActorVisual.cs`、`AdventureCombat.cs`、`WorldArt.cs`；专项脚本为 `CombatPresentationVerification.cs`。
