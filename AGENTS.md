# 给写代码的代理

改哪一块，先读那一块的 `DOMAIN.md`。Domain 是约束清单，不是扩大核心的许可。分层、默认能力和合入条件仍以文档为准：

- [docs/architecture.md](docs/architecture.md) — 程序集分层，以及文末「全局能力地图」
- [docs/extension-rules.md](docs/extension-rules.md) — 插件、脚本暂存、禁止向上依赖
- [CONTRIBUTING.md](CONTRIBUTING.md) — 合入 `main` 的门禁

| 正在改 | 先读 |
| --- | --- |
| `src/SlimFramework/` | [src/SlimFramework/DOMAIN.md](src/SlimFramework/DOMAIN.md) |
| `src/SlimFramework.Contracts/` | [src/SlimFramework.Contracts/DOMAIN.md](src/SlimFramework.Contracts/DOMAIN.md) |
| `src/SlimFramework.Godot/` | [src/SlimFramework.Godot/DOMAIN.md](src/SlimFramework.Godot/DOMAIN.md) |
| `plugins/` | [plugins/DOMAIN.md](plugins/DOMAIN.md) |
| `tools/` | [tools/DOMAIN.md](tools/DOMAIN.md) |
| `samples/` | [samples/DOMAIN.md](samples/DOMAIN.md) |

不要按文件夹再拆 Domain。Save、Assets、UI、Audio 写在 Core 的 `DOMAIN.md` 里。

全局：

- 依赖只能向下。Framework、Plugins、Tools 禁止引用 Game，包括 `samples/` 和以后任何一个游戏工程。
- 程序集外可见的类型、成员、参数用英文 PascalCase。中文只出现在注释和文档。
- 不要引入 QFramework 的 `Command`、`Query`、`BindableProperty`、`IController`、`Architecture`。组合根是 `SlimApp`。
- 能力有哪些、用 `Get<T>` 还是 `Use(IPlugin)`，看架构文档的「全局能力地图」，不要在 Domain 里另写一份。
