# 美术与字体来源

## 角色与场景图集

文件：`Assets/Emberfall/Resources/Art/EmberfallAtlas.png`。

2026-11-06 使用 OpenAI 内置图像生成工具为本项目生成的原创 4×4 透明图集，包含旅人剑士、劫掠者、骸骨弓手、咒师、守墓者、常青树、岩石、篝火、石门、帐篷、墓碑、木箱、战利品袋、草丛、石柱和路牌。没有从商业游戏提取或复用素材，也没有使用第三方游戏素材包。生成内容的使用遵循所用 OpenAI 服务条款，不把它标记为第三方 CC0 素材。

最终生成提示：原创暗色中世纪奇幻 ARPG；严格 4 列 4 行，每格一个独立素材、透明留白、无文字、无地板背景；手绘质感、斜俯视、清晰轮廓，炭灰石材、旧皮革、暗青布料、苔藓与暖色火光；依次排列上述 16 种素材，不包含现有游戏角色或品牌标识。

实际工具提示原文：

```text
ORIGINAL 2D dark fantasy ARPG sprite sheet, transparent alpha. Exactly 4 equal columns by 4 equal rows. One isolated sprite per cell with transparent margins, not touching any cell edge. No text, no labels. Hand-painted detailed 3/4 overhead game sprites, readable silhouettes, weathered charcoal stone/leather, muted teal and moss, amber firelight. Order LEFT to RIGHT TOP to BOTTOM: row1 teal-cloaked human swordsman, corrupted red-brown axe raider, skeleton archer holding bow, purple-robed sorcerer holding staff; row2 large armored crypt warden orange eye glow, gnarled evergreen tree, mossy boulders, burning campfire with stone ring; row3 ruined stone gateway with teal magical center, medieval canvas tent, weathered gravestone, wooden iron-bound chest; row4 gear loot pouch, grass and fern clump, ruined stone column, wooden signpost. No floor squares, no backgrounds, no large external shadows. Consistent detailed painterly dark medieval game asset style. All original generic designs with no franchise references. Square image suitable for slicing into 16 sprites.
```

图集在本次任务中已完整保存并检查实际像素。当前每个角色使用一个方向的基础立绘，辅以呼吸、移动摆动、受击变色和伤害数字；尚非完整逐帧动作集。

## 原创程序纹理与特效

`WorldArt.cs` 中的地面噪声、石砖纹理、柔光、圆环、阴影及场景布置，以及 `ActorVisual.cs` 中的移动反馈与伤害数字，由本项目代码生成，无外部美术素材来源。视觉装饰不改变原有碰撞与存档数据。

## 中文字体

- 字体：Noto Sans CJK SC Regular，保留原始字体文件。
- 官方来源：[Noto CJK 字体仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese)。
- 许可：[SIL Open Font License 1.1](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE)，完整副本位于 `Assets/Emberfall/Resources/Fonts/OFL.txt`。
- 游戏使用由该字体烘焙的 `EmberfallChinese.asset` 静态字形图集，包含玩家界面所需中文、数字、标点与键名。

## 用户反馈附件

Library 文件 `libfile_c13fd9cbd8c48191be686fb6a0982a8e` 已通过受支持的 Library 流程解析，但授权传输连接失败，未能取得本地有效图片，因此没有把提取文字或自动描述当作实际像素参考。实际视觉核对使用本机旧版与新版构建截图。

2026-11-06 第二轮反馈：两张 Library 截图 `libfile_1fc7e5b3896c8191aab8b4267e947130`、`libfile_4723a986033481919f9e01c35beec261` 已通过支持的下载流程落盘至 `Verification/Feedback2` 并实际查看。新增软边泥土纹理、道路、踩踏区、分组植被、接触阴影与帐篷灯光均由项目代码生成，复用既有原创图集；没有引入新的外部素材。截图仅作用户画面反馈参考。
