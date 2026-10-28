# 云芽冒险 · CloudSprout

原创 2.5D 横版平台跳跃游戏，使用 Unity 6000.3.23f1 / URP 和 Blender 5.2 制作。
3D 模型、固定正交侧视镜头，角色在 XY 平面活动。当前包含第一个完整关卡「草地之路」。

## 启动

Git 仓库包含工程源码、素材和验收记录，不包含 `Builds` 下的可执行程序。首次克隆后，在 Unity Hub 中添加本目录，使用 Unity 6000.3.23f1 打开并等待依赖导入，再通过 Build Profiles 构建 Windows x64 到 `Builds/Windows/CloudSprout.exe`。

`open.cmd` 和 `build.cmd` 需要已安装 Unity CLI，且 `unity` 命令位于 PATH；未安装 CLI 时可直接使用 Unity Hub 和编辑器构建。建模脚本通过自身文件位置定位项目，可用 Blender 的 `--python` 参数运行相应 `.py` 文件。

- 双击 `play.cmd`，或运行 `Builds/Windows/CloudSprout.exe`。
- 修改工程：双击 `open.cmd`，打开 `Assets/CloudSprout/Scenes/MeadowTrail.unity` 并进入 Play 模式。
- 运行程序需要保留 Windows 目录中的数据文件与 DLL；不要只复制 exe。

## 操作与目标

| 按键 | 功能 |
| --- | --- |
| A / D 或左右方向键 | 左右移动 |
| 空格、W 或上方向键 | 跳跃；短按小跳，长按高跳 |
| Shift | 加速跑，配合跳跃跨越长缺口 |
| Esc | 暂停或继续 |
| M | 切换声音 |
| R | 暂停/结算界面重新开始 |
| Enter | 开始界面进入关卡 |

沿金币路线前进，到达太阳之门即可通关；三枚隐藏太阳种子是可选收集目标。
从上方下落可以踩倒巡逻甲虫。侧面碰敌人或碰到尖刺会扣血，短时间闪烁表示受伤保护。
尖刺位于有暗色坑壁和醒目边框的下沉陷阱内。跌入后可在保护时间内从坑底正常起跳返回岸边，也可提前跳过整个坑口。
跌落深坑或生命耗尽后，在最近检查点复活；两处检查点恢复三颗生命，已收集物品保留。
奖励方块从下方顶撞，每个奖励五枚金币且只领取一次。金币会弹出并显示 `+5 COINS`，同时直接计入右上角总数，无需再次拾取；用过的方块变棕。

## 关卡

1. 草地入门：学习移动、可变高度跳跃、金币、奖励方块和踩敌人。
2. 风之峡谷：移动平台、连续平台与高处隐藏路线。
3. 日光遗迹：组合障碍、第三枚太阳种子、太阳门与通关结算。

关卡约 300 米长，包含 113 枚可获得金币（88 枚场景金币及 25 枚方块奖励）、3 枚太阳种子、9 个巡逻敌人、2 个检查点和 2 个移动平台。首次游玩时长取决于探索和重试次数。

## 工程结构

- `Assets/CloudSprout/Scenes/MeadowTrail.unity`：可直接编辑的完整场景。
- `Assets/CloudSprout/Scripts/`：控制器、玩法、镜头和界面。
- `Assets/CloudSprout/Models/`：17 类原创 Blender FBX 模型（含下沉陷阱模块）。
- `ArtSource/CloudSprout.blend`：可编辑的模型源文件；`palette.json` 为配色。
- `Tools/create_art.py`：Blender 建模与导出脚本，保护已有源文件。
- `Tools/create_spike_pit.py`：在原 Blender 模型集上新增陷阱模块并导出；已有模块受重复创建保护。
- `Tools/create_audio.py`：原创合成音效及循环配乐生成脚本。
- `Verification/`：实际运行验证记录。
- `Assets/Screenshots/`：Unity 游戏画面。
- `Builds/Windows/`：Windows x64 可执行版本。

主角与敌人使用分部件程序动画，模型保持可编辑；当前界面为英文，说明为中文。
工程与 `unity-demo`、`unity-tank` 并列，所有新增内容都在本文件夹。

当前版本 1.0.4：6 处下沉陷阱均按完整地砖重新定位，每处替换一块 2 米宽地砖，坑边对齐砖缝，周围地砖保持完整。坑底可站立、起跳，暗色坑壁突出尖刺轮廓。金币同步浮动及均匀间距、奖励方块修复均保留。

## 开发与验证

Unity 编辑器打开本工程、退出 Play 模式后，可运行 `build.cmd`，再用提示的命令查询构建结果。
也可以使用 Unity 的 Build Profiles 构建 Windows x64。

`LevelVerification` 和 `RouteVerification` 仅在编辑器编译，用于分别验证玩法边界和连续通关。前者使用定点样本，后者从起点驱动真实角色输入，不传送位置、不提供无敌、不修改物理参数。测试结果分别保存到 `Verification/mechanics.txt` 与 `Verification/route.txt`。

`SpikePitVerification` 检查 6 处坑口与两岸碰撞、完整巡逻范围，并用真实控制器验证掉入受伤、保护时间、坑底起跳和跨坑；结果保存到 `Verification/spike-pits.txt`。

运行时使用 Unity Input System、uGUI / TextMeshPro。字体和 Unity 包的第三方许可随工程保留；角色、模型、玩法、音效和配乐均在本项目中制作。
