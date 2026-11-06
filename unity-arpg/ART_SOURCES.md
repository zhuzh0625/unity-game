# 美术与字体来源

## 角色与场景图集

文件：`Assets/Emberfall/Resources/Art/EmberfallAtlas.png`。

2026-11-06 使用 OpenAI 内置图像生成工具为本项目生成的原创 4×4 透明图集，包含旅人剑士、劫掠者、骸骨弓手、咒师、守墓者、常青树、岩石、篝火、石门、帐篷、墓碑、木箱、战利品袋、草丛、石柱和路牌。没有从商业游戏提取或复用素材，也没有使用第三方游戏素材包。生成内容的使用遵循所用 OpenAI 服务条款，不把它标记为第三方 CC0 素材。

最终生成提示：原创暗色中世纪奇幻 ARPG；严格 4 列 4 行，每格一个独立素材、透明留白、无文字、无地板背景；手绘质感、斜俯视、清晰轮廓，炭灰石材、旧皮革、暗青布料、苔藓与暖色火光；依次排列上述 16 种素材，不包含现有游戏角色或品牌标识。

实际工具提示原文：

```text
ORIGINAL 2D dark fantasy ARPG sprite sheet, transparent alpha. Exactly 4 equal columns by 4 equal rows. One isolated sprite per cell with transparent margins, not touching any cell edge. No text, no labels. Hand-painted detailed 3/4 overhead game sprites, readable silhouettes, weathered charcoal stone/leather, muted teal and moss, amber firelight. Order LEFT to RIGHT TOP to BOTTOM: row1 teal-cloaked human swordsman, corrupted red-brown axe raider, skeleton archer holding bow, purple-robed sorcerer holding staff; row2 large armored crypt warden orange eye glow, gnarled evergreen tree, mossy boulders, burning campfire with stone ring; row3 ruined stone gateway with teal magical center, medieval canvas tent, weathered gravestone, wooden iron-bound chest; row4 gear loot pouch, grass and fern clump, ruined stone column, wooden signpost. No floor squares, no backgrounds, no large external shadows. Consistent detailed painterly dark medieval game asset style. All original generic designs with no franchise references. Square image suitable for slicing into 16 sprites.
```

该图集保持原有素材，主角四方向动作与敌人攻击动作使用下述独立图集。

## 主角四方向动作图集

文件：`Assets/Emberfall/Resources/Art/WandererDirections.png`。

2026-11-06 使用 OpenAI 内置 imagegen 工具，以原图集左上角旅人作为角色与画风参考生成。透明 PNG，1254×1254，四行分别为朝下、右、上、左；四列分别为待机、左脚步行、右脚步行和攻击。保留原图，不覆盖基础图集。运行时按网格切片，向左攻击帧额外包含透明间隔内的剑尖。

实际工具提示原文：

```text
Use case: identity-preserve / game sprite sheet. Derive a new animation sprite sheet from ONLY the male wanderer in the top-left cell of the reference atlas. Preserve his recognizable unhooded brown hair and beard, dark teal tattered cloak, brown leather armor, steel longsword and mature hand-painted dark fantasy style. Deliver EXACTLY 4 columns by 4 rows on a square transparent canvas, 16 equal square cells, no borders, no text. Every cell contains one complete isolated full-body wanderer, same size, planted foot baseline and centered anchor, with large transparent safety margins; no part crosses cell boundaries. Rows top to bottom: facing SOUTH toward camera, facing EAST screen right, facing NORTH away from camera with back and cloak visible, facing WEST screen left. Columns left to right for each direction: neutral idle with sword ready; walking with left foot forward; walking with right foot forward; clear sword attack follow-through extended in facing direction. Keep the elevated 3/4 top-down RPG camera consistent for all sixteen views. Body must be large and readable with brighter rim highlights, sword silhouette distinct, no glow background, no scenery, no dropshadows, no magic effects, no other characters. Align all cells precisely to a uniform 4x4 grid. This is a production sprite animation atlas for a Unity ARPG; preserve actual transparent alpha.
```

## 敌人攻击动作图集

文件：`Assets/Emberfall/Resources/Art/EnemyAttacks.png`。2026-11-06 使用 OpenAI 内置 imagegen 工具生成，未使用 CLI 回退。以原图集中的四类敌人为身份和画风参考；先生成动作，再单独修正留白，最终透明 PNG 为 1254×1254。

四行依次为劫掠者、骸骨弓手、沼泽咒师、守墓者；四列依次为待机、蓄力、出手、收势。按左右朝向翻转。各行按实际足底位置设置切片锚点，并归一角色高度；没有覆盖基础图集。伤害和弹体发射事件驱动出手帧，随后播放收势。

最终采用的提示词序列：

```text
Use case: identity-preserve. Asset type: production Unity 2D enemy attack sprite sheet. Input image is a character identity and painterly style reference, not a layout to preserve. Create a NEW square transparent RGBA sprite sheet with EXACTLY 4 equal columns by 4 equal rows (16 isolated full-body sprites). Rows top to bottom: 1 the skull-masked red-brown axe raider from reference top row second cell; 2 the hooded skeletal archer from reference top row third cell; 3 the ragged purple-robed staff sorcerer from reference top row fourth cell; 4 the massive armored orange-eyed crypt warden from reference second row first cell. Preserve each identity, costume, colors, weapon and mature hand-painted texture. EVERY character faces screen RIGHT in a consistent 3/4 elevated game camera. Columns left to right: 1 neutral ready stance; 2 visibly anticipated attack windup; 3 unmistakable attack release; 4 follow-through recovery. Raider: ready axe, raise axe far overhead, sweep axe forward-right, low extended follow-through. Archer: hold bow upright toward right, fully draw string and arrow back by cheek, release with bow arm extended right and fingers open, bow dips as arm relaxes. Sorcerer: hold staff, raise staff and gather small violet light in free hand, thrust glowing hand and staff toward right, lower arm recovering. Warden: sword ready, lift greatsword overhead with both hands, powerful downward slam toward right, sword at low follow-through. Keep all four frames in each row the SAME body height and foot baseline, center torso at x=50% and feet at y=84% of its cell. Whole characters and weapons fit within their own cells with 12% clear transparent margins, no clipping or crossing cell boundaries. Background truly transparent; no ground, tiles, shadows, scene, labels, borders, text or extra characters. No large baked-in particles or swirls; physical limb and weapon movement must read clearly. All original generic fantasy characters from the supplied reference.

Use case: precise-object-edit. This is a Unity animation atlas with 4 rows and 4 columns. Preserve all sixteen existing characters, colors, painterly details, weapons and exact distinct attack poses from the input. Fix ONLY layout and safe margins: scale down EACH individual character and its entire weapon inside its original cell to leave at least 10% transparent padding at ALL four cell edges, including the raised axe in row 1 column 2 and the tall bows in row 2. Keep the full canvas square, 4 equal rows and 4 equal columns, no grid lines. Make every character's feet rest on a common baseline at 82% from the TOP of its cell. Keep torso centers at 50% of cell width. Keep the same anatomical body size in all four frames of each row; overhead weapons may extend higher but must not touch any cell edge. Reconstruct any clipped weapon tips. No added text, no borders, no scenery, no shadows, no effects beyond the original small sorcerer hand glow. Genuine transparent alpha background. Do not combine, omit or reorder any cells; 16 separate full-body sprites remain.
```

## 原创程序纹理与特效

`WorldArt.cs` 中的地面噪声、石砖纹理、柔光、圆环、挥砍弧、技能及装备符号、阴影及场景布置，由本项目代码生成。`ProjectileArt.cs` 生成并缓存金橙火焰弹、实体箭矢、紫色法术核心和熔火碎片的纹理；`ProjectileVisual.cs` 控制朝向、光晕和尾迹。`ActorVisual.cs` 控制角色动作，`CombatFeedback.cs` 复用弹体、伤害数字、发射及命中特效。上述弹体素材为项目原生程序纹理，没有下载第三方图片。

`CombatFeedback.cs` 在启动时以确定性噪声与振荡器合成挥剑、命中、受伤、施法、治疗、拾取、闪避、预警和首领冲击九种短音效，没有下载或引用第三方录音。默认低音量，M 切换静音。

## 中文字体

- 字体：Noto Sans CJK SC Regular，保留原始字体文件。
- 官方来源：[Noto CJK 字体仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese)。
- 许可：[SIL Open Font License 1.1](https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE)，完整副本位于 `Assets/Emberfall/Resources/Fonts/OFL.txt`。
- 游戏使用由该字体烘焙的 `EmberfallChinese.asset` 静态字形图集，包含玩家界面所需中文、数字、标点与键名。

## 用户反馈附件

Library 文件 `libfile_c13fd9cbd8c48191be686fb6a0982a8e` 已通过受支持的 Library 流程解析，但授权传输连接失败，未能取得本地有效图片，因此没有把提取文字或自动描述当作实际像素参考。实际视觉核对使用本机旧版与新版构建截图。

2026-11-06 第二轮反馈：两张 Library 截图 `libfile_1fc7e5b3896c8191aab8b4267e947130`、`libfile_4723a986033481919f9e01c35beec261` 已通过支持的下载流程落盘至 `Verification/Feedback2` 并实际查看。新增软边泥土纹理、道路、踩踏区、分组植被、接触阴影与帐篷灯光均由项目代码生成，复用既有原创图集；没有引入新的外部素材。截图仅作用户画面反馈参考。
