# Emberfall 本机验证记录

最新中文与美术版本及前台帧率问题的最终结果见 [新版验证记录](ChineseArt/RESULTS.md)。下文保留原型早期验证历史。

验证日期：2026-11-06。工程使用 Unity 6000.3.23f1 / Universal 2D，最终 Windows 版本为非开发构建。

## 已通过

| 检查 | 结果 | 本地证据 |
| --- | --- | --- |
| 编辑器编译、Windows 构建 | 成功 | `exit-fix-build.log`，`EMBERFALL_BUILD_OK` |
| 集成回归 | 25 项通过，游戏真实退出码 0 | `smoke-results.txt`、`final-smoke.log`、`final-smoke-exit.txt` |
| 完整步行键盘流程 | 通过，击杀 10 个敌人，剩余 3 瓶血瓶，真实退出码 0 | `keyboard-journey.txt`、`final-journey.log`、`final-journey-exit.txt` |
| 窗口关闭修复 | 连续 3 次真实退出码 0，无 `Native Crash Reporting` | `close-after-frame-1.log` 至 `close-after-frame-3.log`，对应 `*-exit.txt` |

完整流程从普通营地剑、100 生命和 5 瓶血瓶开始，实际步行经过野外、固定地牢，击杀普通敌人与首领、拾取正常随机装备、通过 B / 方向键 / Enter 换装，再步行返回营地并保存。没有传送、直接调用伤害或赠送测试装备。自动测试仅使用 Unity Input System 的键盘状态事件；为支持隐藏窗口，只有显式测试模式会忽略窗口焦点并启用测试键盘设备，普通游玩不改变焦点行为。测试存档独立于正常玩家档案。

截图：`camp.png`、`crypt.png`、`inventory.png`、`boss-cleared.png`、`returned-camp.png`。

## 退出问题及修复

修改前，开发版与非开发版通过窗口关闭按钮退出均复现 Unity/Mono 的 `Native Crash Reporting / Got a UNKNOWN`，没有可用的托管异常堆栈。把构建改成非开发版单独无法消除该记录。

`GracefulExit` 先取消首次原生退出请求，等待一个玩家循环帧，再保存并调用托管退出。此路径在本机连续 3 次窗口关闭复测中没有再出现原生崩溃记录；最终两组自动测试也正常退出。这是针对已复现退出路径的规避措施，不代表已确定 Unity/Mono 底层根因或验证全部硬件环境。

旧记录保留在 `manual-play.log`、`close-recheck-1.log`、`native-input-visible.log`。早期直接启动 GUI 程序后得到的 shell 返回值不等于游戏退出码；表格只采用持有游戏进程句柄后取得的真实退出码。

## 明确限制

- **硬件键盘人工通关尚未验收。** 电脑控制工具发送 B / Escape 后，诊断显示窗口持续推进帧、保持焦点、键盘设备启用，但只增加文本事件，没有产生 `wasPressedThisFrame` 按键状态。因此不能将系统级模拟按键当作真实键盘验收，也没有为迎合该工具而改变正常游戏输入逻辑。证据：`native-input-visible.log`。
- 自动状态事件已验证完整流程；它不能代替玩家手感、连续长按及硬件键盘测试。
- 正常退出仍记录一条 `GarbageCollector disposing of ComputeBuffer` 清理警告；最终测试退出码为 0，未伴随原生崩溃。该警告尚未定位或消除。
- 首轮隐藏窗口回归曾因测试设备/焦点处理失败而中止，已修正显式测试模式并完成上表中的最终回归；不将中止记录计为通过。
- Pipeline 实时编辑器控制包先前因网络无法获取；编译、场景生成和构建使用本机已有编辑器完成。

原有赛车工程的两处未提交改动未修改；未提交、推送或发布。`.log`、测试存档、Unity 缓存和构建产物被 Git 忽略，但仍保留在本机供排查。

最新第二轮场景与 UI 优化验收：[Feedback2/RESULTS.md](Feedback2/RESULTS.md)。
