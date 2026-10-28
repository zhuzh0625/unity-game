# Unity 游戏 Monorepo

本仓库统一管理两个独立的 Unity 工程，使用 Unity `6000.3.23f1`。

| 工程目录 | 内容 | 说明入口 |
| --- | --- | --- |
| `unity-demo/` | Orbit Collector 演示 | [DEMO.md](unity-demo/DEMO.md) |
| `unity-tank/` | 坦克游戏 | [README.md](unity-tank/README.md) |

## 打开与开发

在 Unity Hub 中分别添加 `unity-demo/` 或 `unity-tank/`，打开需要开发的工程。仓库根目录是 Git 管理入口，不是 Unity 工程目录。

两个工程分别维护自己的 `Assets/`、`Packages/`、`ProjectSettings/` 和 `.gitignore`。资源及对应的 `.meta` 文件应一起提交；`Library/`、日志和构建产物继续由各工程的忽略规则排除。新检出首次打开时，Unity 会重新生成本地缓存。

在仓库根目录统一查看变更、提交和推送。修改任意工程后，可用 `git status` 查看整个仓库状态。

## 历史迁移

2026-10-28 使用 `git subtree add` 导入两个原仓库的 `main` 分支，未使用 `--squash`，保留了原始提交及其哈希：

| 原仓库 | 导入提交 |
| --- | --- |
| `unity-demo` | `fd17a37d26086739454ccf03ea24254ded8ab8b8` |
| `unity-tank` | `110a91d0c404e608f4e5f51dbb01f665f91ad21e` |

可用 `git log --graph --oneline --all` 查看合并历史。迁移前的提交保留原有目录路径，导入后的文件分别位于两个工程子目录。

两个原仓库的完整目录和 Git bundle 已备份在仓库外。远程仓库地址需为此 monorepo 单独配置。
