# Plugins

插件是独立类库：引用 `SlimFramework`（需要引擎时再引用 `SlimFramework.Godot`），由游戏在 `SlimApp.Use` 时接入。

- 实现 `IPlugin`，在 `Register` 里 `app.Add` 模块或向已有服务登记扩展（例如 `SaveService.Register`）。
- 禁止引用 `samples/`、游戏程序集、具体关卡或剧情代码。
- 本目录下的工程会走 `Directory.Build.targets` 的引用检查。

硬性边界见 [docs/extension-rules.md](../docs/extension-rules.md)。脚手架尚未落地，规格在该文，缺口在 [docs/gap-backlog.md](../docs/gap-backlog.md)。
