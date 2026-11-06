# 余烬行者：键盘 ARPG 原型

Unity **6000.3.23f1**，Universal 2D / URP。Windows 单机原型，使用本项目原创生成的手绘图集与程序纹理，不包含暗黑破坏神的素材。素材与字体许可见 [来源记录](ART_SOURCES.md)。

## 开始游玩

在 Unity Hub 中打开本目录 `D:\workspace\game\unity-game\unity-arpg`，打开 `Assets/Scenes/Emberfall.unity`，点击 Play 并聚焦 Game 窗口。

已构建的本地版本入口：`Builds/Emberfall/Emberfall.exe`。构建产物不会纳入 Git。

当前构建为非开发版。窗口关闭经过 `GracefulExit` 延后一帧处理并保存进度，避免本机曾复现的 Unity/Mono 原生退出崩溃。详细实测范围见 [验证记录](Verification/RESULTS.md)。

运行时关闭垂直同步并明确限制到 60 FPS。本机原同步配置曾使前台窗口停留在约 4 FPS；修正后通过帧推进诊断验证。中文与美术版本的结果见 [新版验证记录](Verification/ChineseArt/RESULTS.md)。

流程：营地篝火补给 → 向东从石门进入灰烬荒野 → 继续向东进入幽寂古墓 → 穿过中央缺口进入东侧房间 → 击败幽寂守墓者 → E 拾取战利品 → 依次使用西侧出口返回营地。完成后在篝火休息可重置任务，再次出征。

## 键位

| 操作 | 按键 |
| --- | --- |
| 八方向移动 | WASD / 方向键 |
| 奔跑 | 左 Shift + 移动 |
| 挥剑，可按住连续攻击 | J / Space；鼠标左键可辅助 |
| 顺劈：范围伤害，22 专注，3.5 秒冷却 | U |
| 冰霜：范围伤害、减速四秒，28 专注，6 秒冷却 | I |
| 余烬：直线弹体，18 专注，2 秒冷却 | O |
| 血瓶：恢复 55 生命，1 秒间隔 | H |
| 拾取最近装备、使用入口、篝火补给 | E |
| 打开 / 关闭背包 | B |
| 背包选择 | W/S / 上下方向键 |
| 装备选中物品，与当前装备交换 | Enter |
| 丢下选中装备 | Delete |
| 暂停 / 关闭背包 / 恢复 | Esc |
| 暂停恢复、死亡回营地 | Enter |
| 保存（游戏中或暂停时） | F5 |

所有游戏操作可用键盘完成。背包、暂停面板中的按钮可用鼠标辅助。近距离攻击自动朝最近目标；没有目标时朝移动方向释放，远程技能在 10 单位内自动瞄准最近敌人。无需鼠标瞄准。

## 原型规则

- 一个持剑角色、一类武器和护甲，三个主动技能，专注自动恢复。
- 普通敌人：追击近战的腐化劫掠者、远程骸骨弓手、施放慢速弹体的沼泽咒师。
- 首领幽寂守墓者：生命较高，红色圆环预警后对区域造成伤害；预警期间离开范围。
- 随机装备包含品质、攻击 / 护甲和额外生命；首次击杀和首领保证掉落，其余普通怪有 80% 掉落概率。背包容量 16。
- 背包显示与当前同部位装备的属性差值，打开背包或暂停时世界模拟停止。
- 营地安全，篝火免费恢复生命、专注和五瓶血瓶。死亡保留装备，扣除最多 10 金币，Enter 返回营地。
- 金币仅用于原型反馈，暂无商店、经验等级、技能树、联网或随机地图。
- 当前地图使用固定布局与简化碰撞；怪物采用追击 / 退避逻辑，暂不使用复杂寻路。
- 玩家界面、提示、怪物与物品显示均为简体中文，使用 Noto 简体中文字体。角色与场景采用原创手绘图集，加入移动摆动、呼吸、受击变色与伤害数字；尚未制作完整逐帧动作与音效。

## 存档

编辑器：本目录 `SaveData/emberfall.json`。Windows 版本：`Application.persistentDataPath` 下的 `emberfall.json`（通常在 `%USERPROFILE%\AppData\LocalLow\Local Prototypes\Emberfall`）。

保存装备、背包、血瓶、金币、首领及任务进度。拾取、换装、过门、篝火和正常退出时保存；也可 F5 手动保存。重新启动回到安全营地，恢复生命；不保存场景中的临时掉落、怪物状态、玩家坐标或技能冷却。主存档损坏时尝试 `.bak` 备份。

中文化只改变显示文字，不改存档版本、英文物品标识、产品标识或存储目录。旧存档无需迁移，旧物品也会显示为中文。

## 开发与验证

主要代码位于 `Assets/Emberfall/Scripts`，编辑器工具位于 `Assets/Emberfall/Editor`。场景保持简洁，由 `Adventure` 在运行时生成区域；打开工程即可 Play，不需要先运行生成工具。

批处理构建（使用现有 Unity / Unity CLI）：

```powershell
unity run . --timeout 300 --log-file Verification/build.log --no-tail -- -executeMethod Emberfall.Editor.PrototypeSetup.Build
```

集成验证入口（仅在明确传入参数时运行）：

```powershell
& .\Builds\Emberfall\Emberfall.exe -emberfall-smoke -smoke-output 'D:\workspace\game\unity-game\unity-arpg\Verification' -logFile 'D:\workspace\game\unity-game\unity-arpg\Verification\player-smoke.log'
```

测试将输入系统按键与部分短距离传送结合，缩短遍历时间；它不代表人工从头到尾的操作体验验收。测试在 `Verification` 输出结果、截图和独立存档，不覆盖正常玩家档案。普通运行不执行测试。

额外的完整步行验收模式：`-keyboard-journey -journey-output <绝对输出目录>`。从正常初始角色出发，通过键盘状态事件步行、战斗、拾取、换装和回营地，不传送、不直接调用伤害或赠送装备。它仍是输入系统自动验收，不能替代硬件键盘人工体验验收。

`-native-input-probe` 仅用于诊断窗口焦点、帧推进和输入事件计数，普通游玩不启用。当前电脑控制工具的按键操作送达的是文本事件，不产生游戏按键状态，故不能用它验证硬件键盘操作。

本轮画面优化：营地采用软边泥土道路、踩踏区、成组植被与接触阴影；缩小顶部和底部 HUD，并调整营地镜头以露出帐篷屋顶。F1 打开完整操作手册，Esc 返回。行囊采用左侧 16 格列表、右侧物品详情与已装备对照，攻击、护甲、生命差值逐项标注。最新验证见 [Feedback2/RESULTS.md](Verification/Feedback2/RESULTS.md)。
