# 铁甲堡垒 · Iron Bastion

Unity + Blender 制作的 Windows 单人 3D 坦克防守游戏。玩法参考 FC《坦克大战》：四方向移动、朝车头射击、摧毁敌军并保卫南侧指挥部。模型、地图和界面为本项目制作，不包含原游戏素材。

## 直接玩

仓库只保存工程源码和原始资源，不包含 Windows 构建包。首次克隆后，用 Unity **6000.3.23f1** 打开项目，等待依赖导入，打开 `Assets/Scenes/Battlefield.unity` 即可在编辑器运行；通过菜单 `Iron Bastion > Build Windows player` 生成可执行程序后，再使用下面的启动方式。已有 FBX 可直接导入，运行游戏不需要安装 Blender。

双击根目录 `play.cmd`，或运行 `Builds/Release-1.0.3/IronBastion.exe`。发布时需要完整保留 Release-1.0.3 文件夹，不能只复制 exe。界面左上角显示 `v1.0.3`。

v1.0.3 调整第一关入门节奏：首波 5 秒，出兵间隔 4.5 秒，最多同时 3 辆，前 4 辆均为普通型。首辆普通敌军固定掉落火力升级，第 8、12 辆继续提供随机补给。第二、三关沿用原有节奏。

v1.0.2 在主菜单加入“资料库”，分为道具补给与敌军档案两页。每次复活提供 3.5 秒无敌保护，并显示提示、动态护盾光圈与剩余时间；暂停不会消耗保护时间。

v1.0.1 修复敌军瞄准开火后被移动逻辑覆盖朝向的问题；坦克会先完成转向再开火，使炮口和炮弹方向一致。已经打开的旧版需关闭后重新通过 `play.cmd` 启动。

| 按键 | 功能 |
| --- | --- |
| WASD / 方向键 | 四方向移动、改变炮口朝向 |
| 空格 / J | 射击，可按住连续射击 |
| Enter | 开始、继续或进入下一关 |
| I | 主菜单打开资料库；资料库中返回主菜单 |
| Tab / 左右方向键 | 资料库中切换道具、敌军分类 |
| Esc | 资料库中返回主菜单 |
| Esc / P | 暂停、继续 |
| R | 暂停或结算时重新开始整个战役 |
| M | 声音开关 |

鼠标可以操作开始、资料库分类、暂停、继续、下一关与返回菜单按钮。

## 玩法

- 共三关，敌军数量依次为 12、16、20 辆；第一关最多同时 3 辆，第二、三关最多同时 4 辆。
- 初始 3 条生命。被击毁后消耗生命并重生，每次实际出生时获得完整 3.5 秒无敌保护，保护期间可以正常移动和射击；生命耗尽或指挥部被击毁即失败。
- 砖墙可分块击毁；钢墙阻挡普通炮弹，满级火力可以击穿。水域阻挡坦克但允许炮弹飞越，树木可穿行。
- 敌军分为普通型、快速型、速射型与三段装甲型。携带金色信标的敌军被摧毁后掉落补给。
- 五种补给：金色火力升级、青色护盾、红色清场炸弹、蓝色冻结、绿色额外生命。拾取后会显示中文效果提示。
- 本关全部敌军被摧毁后进入下一关，保留剩余生命与火力等级。三关全部完成则胜利。
- 最高分和声音设置本地保存。炮弹会伤害己方指挥部，射击时注意基地所在方向。

## 打开工程

使用 **Unity 6000.3.23f1（6.3 LTS）** 打开本目录，场景为 `Assets/Scenes/Battlefield.unity`，按 Play 即可运行。使用 URP、Input System、uGUI 与 TextMeshPro。

`Assets/Scripts/WorldBuilder.cs` 中的三组 13×13 字符地图控制关卡：`.` 空地、`B` 砖墙、`S` 钢墙、`W` 水域、`H` 指挥部。场景内保留一份可观察的编辑器预览，运行时根据地图重新生成战场。

菜单 `Iron Bastion > Build battlefield scene` 可重新生成场景、模型材质与中文字体资源。它会重建当前场景，应先保存个人场景修改。首次缺少 TMP Essentials 时，命令先导入资源，导入结束后再执行一次。

菜单 `Iron Bastion > Build Windows player` 输出 Windows 64 位版本到 `Builds/Release-1.0.3`。也可使用根目录 `build.cmd`，它控制已经打开的本项目 Unity 编辑器。

## Blender 模型

- 可编辑源文件：`ArtSource/IronBastion.blend`。
- 导出资源：`Assets/Resources/Models/*.fbx`，包括坦克、基地、砖块、钢墙和松树。
- 可重复建模脚本：`Tools/create_models.py`。在 Blender 的脚本环境中执行，或使用 `blender --background --python Tools/create_models.py`。
- 脚本创建独立场景，不删除当前 Blender 场景；FBX 以原点导出，`.blend` 内按资产展示排布。

## 验证

`Docs/` 中的 Markdown 报告随源码保存；文中引用的 JSON 回执和 PNG 截图是本地测试产物，不提交 Git。运行以下检查可重新生成临时回执，历史测试的结论与限制保留在报告中。

`PlayVerification` 为仅在 Unity 编辑器编译的集成检查，不进入发布版本。在 Play 模式调用 `IronBastion.PlayVerification.Run()`，结果写入 `tmp/play-verification.json`。它使用真实 Input System 键盘事件、移动与炮弹逻辑，并通过受控敌军样本验证状态切换；三关流程检查不是人工无辅助通关。

当前共 50 项检查，包含资料库 UI 射线命中与按钮回调、I / Tab / Esc / Enter 键盘交互，以及连续两次复活、真实敌弹在无敌期和到期后的伤害、暂停保留保护时长。

`IronBastion.FacingVerification.Run()` 检查四向瞄准、转向期间禁止提前开火、Blender 模型炮口与实际炮弹方向、装填时保持瞄准和遇阻射击。结果为 `tmp/facing-verification.json`。两套检查需分别运行；键盘检查使用临时虚拟设备与输入配置，结束后恢复。

构建结果：`tmp/build-result.json`。详细验证说明见 `Docs/验证记录.md`。

第一关的 12 局自动输入难度测试、限制与建议见 `Docs/第一关难度测试.md`。复现使用仅编辑器可用的 `BalancePlaytest.Run()` 与 `BalancePlaytest.RunGuard()`，两组需串行运行并分别保存临时回执；这些自动策略结果不代表真人通关率。

v1.0.3 的调整和复测结果见 `Docs/第一关平衡调整-v1.0.3.md`。对照测试可调用 `BalancePlaytest.RunAll()`，一次运行相同四种策略和三个种子。`PacingVerification.Run()` 验证实际出兵时间、场上上限、首辆星星掉落与拾取，以及后两关原有节奏；回执为 `tmp/pacing-verification.json`。所有检查必须串行执行。

## 资源许可

中文字体使用 Noto Sans CJK SC，按 SIL Open Font License 1.1 分发；许可证为 `Assets/Fonts/OFL.txt`。字体来源：<https://github.com/notofonts/noto-cjk>。模型和合成音效由本项目生成。

当前是可玩的首版，包含三关固定地图；没有多人联机、关卡编辑器或存档续关。

## 版本管理范围

- 保留 `Assets/` 中的游戏资源及配套 `.meta`、`Packages/` 依赖清单和锁文件、`ProjectSettings/` 工程配置。
- 保留 `ArtSource/IronBastion.blend`、`Tools/create_models.py`、构建/启动脚本和必要的中文说明。
- 排除 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、`Builds/`、`tmp/`、IDE 生成文件、截图、回执和未使用的模板教程。忽略规则只影响提交，不删除本地文件。
- `Assets/Settings/SampleSceneProfile.asset` 仍由 URP 配置引用，属于必要资源，随工程保留。
