# Plugins

插件是独立类库：引用 `SlimFramework`（需要引擎时再引用 `SlimFramework.Godot`），由游戏在 `SlimApp.Use` 时接入。

- 实现 `IPlugin`，在 `Register` 里 `app.Add` 模块或向已有服务登记扩展（例如 `SaveService.Register`）。
- 禁止引用 `samples/`、游戏程序集、具体关卡或剧情代码。
- 本目录下的工程会走 `Directory.Build.targets` 的引用检查。

`ClickOutsideClose` 是第一个插件：指针在栈顶页面矩形外按下时调用 `IUiStack.CloseTop`。`SlimBootstrap.Configure` 不 `Use` 它。`samples/DemoGame` 的 `GameRoot` 在 `base.Configure` 之后 `Use`。无头 `samples/SlimFramework.Demo` 不引用它。

钩子在 `GodotUiStack.PagePresented` / `PageDismissed`。没有订阅者时，打开和关闭与未挂插件时相同。

硬性边界见 [docs/extension-rules.md](../docs/extension-rules.md)。
