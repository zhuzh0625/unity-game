# Orbit Collector 示例

Unity 6000.3.23f1 / URP。场景由 Unity 插件通过正在运行的编辑器创建并保存。

## 运行

1. 打开 `Assets/CollectorDemo/CollectorDemo.unity`，点击 Unity 顶部 Play。
2. 点击 Game 视图取得键盘焦点。
3. 使用 WASD 或方向键移动小球，收集 8 枚金色晶体。
4. R 随时重新开始；通关后也可点击 **Play Again**。

Game 视图建议选择 **Full HD (1920x1080)** 或 **16:9 Aspect**。

## 文件

- `Assets/CollectorDemo/Scripts/CollectorGame.cs`：移动、边界、收集、计时、UI 与重开。
- `Assets/CollectorDemo/CollectorDemo.unity`：可编辑、可直接运行的完整场景，已列为构建列表首场景。
- `AgentScripts/CreateCollectorDemo.cs`：通过 Pipeline 的 `script run` 执行的一次性场景生成脚本；场景存在时拒绝覆盖。
- `AgentScripts/VerifyCollectorDemo.cs`：Play 模式下的验证脚本，创建临时虚拟键盘，使用真实 Input System 驱动移动，不直接修改分数或传送玩家，结束后移除虚拟设备。
- `Assets/Screenshots/`：运行与通关截图。

## Blender 晶体模型

Demo 的 8 个收集点现已使用 Blender 5.2.1 LTS 制作的切面晶体，包含主晶体、两片侧晶簇、金属束环和绿色能量嵌线。每枚模型共 5 个网格、240 个三角面。

- 源文件：`ArtSource/CollectorCrystal.blend`，可直接用 Blender 打开继续编辑。
- 导出模型：`Assets/CollectorDemo/Models/CollectorCrystal.fbx`。
- 可复用预制体：`Assets/CollectorDemo/Prefabs/EnergyCrystal.prefab`。
- URP 材质：`Assets/CollectorDemo/Materials/BlenderCrystal/`。
- 建模脚本：`AgentScripts/create_blender_crystal.py`，通过 Blender MCP 在运行中的 Blender 执行。
- 替换脚本：`AgentScripts/ReplaceCollectorCrystals.cs`，通过 Unity Pipeline 的 `script run` 执行。
- 替换前场景备份：`ArtSource/Backups/CollectorDemo-before-blender.unity`。
- 新版运行截图：`Assets/Screenshots/blender-crystals-demo.png`。

FBX 按米导出，`-Z Forward / Y Up`，只导出模型对象，不含相机、灯光或动画。Unity 保留 FBX 根节点的坐标转换，外层收集点保持单位缩放；材质通过 ModelImporter 显式映射到 URP/Lit。以后修改源模型时，按相同对象/材质名重新导出到该 FBX 路径，保留 `.meta`，Unity 可重导入模型并沿用材质映射。源 `.blend` 放在 `Assets` 外，避免与 FBX 重复导入。

已验证新版模型的完整收集、通关、R 重开与实际按钮重开；原 Blender 场景保留。制作与替换脚本遇到已有产物会拒绝重复覆盖。

## 插件验证命令

在项目目录运行：

```powershell
unity status --format json
unity command editor.playmode start --caller plugin --skill unity-cli --format json
unity status --until-ready --project-path D:\workspace\game\unity-demo --format json
unity command editor focus --caller plugin --skill unity-cli --format json
# 点击 Game 视图取得键盘焦点，再运行键盘验证；后台键盘可能被 Input System 禁用。
unity command script run --caller plugin --skill unity-cli --file AgentScripts/VerifyCollectorDemo.cs --entry VerifyCollectorDemo.Run --timeout 55 --format json
unity logs --console --level error --tail 10 --format json
```

已验证：帧持续推进、WASD/方向键移动、边界限制、R 重开、8/8 收集、分数/进度/通关状态一致、通关计时停止，以及实际鼠标点击 Play Again。验证范围为 Unity 编辑器 Play 模式，未导出独立可执行文件。
