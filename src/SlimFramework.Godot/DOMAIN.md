# Godot 适配

`src/SlimFramework.Godot/` 的写作约束。脚本暂存见 [docs/extension-rules.md](../../docs/extension-rules.md)。默认挂上哪些服务见 [docs/architecture.md](../../docs/architecture.md)「全局能力地图」。本文不是把插件收进默认启动路径的许可。

## 范围

程序集名 `SlimFramework.Godot`，工程 `src/SlimFramework.Godot/SlimFramework.Godot.csproj`。命名空间 `SlimFramework.GodotAdapter`。引擎类型只放这里。

游戏入口是 `SlimBootstrap`（`Bootstrap/SlimBootstrap.cs`）。Godot 游戏继承它，在 `Configure` 里 `Use` 自己的插件。

`SlimBootstrap` 和 `UiPage` 的源文件留在本目录，但适配 csproj `Compile Remove` 了它们，类型不进 `SlimFramework.Godot.dll`。游戏 `Import` `build/SlimFramework.Godot.targets`，构建时拷到游戏工程的 `SlimFramework.Scripts/` 再编译。拷贝不入库。

## 能依赖 / 不能依赖

可以引用 `SlimFramework` 和 `GodotSharp`（版本 `$(GodotSharpVersion)`）。

禁止引用 `plugins/`、`samples/`、任何游戏工程。适配层禁止引用游戏。`GuardFrameworkDoesNotReferenceGame` 适用。不要为了少写插件，把可选能力加进 `SlimBootstrap.Configure`。

## 可扩展的公开 API

沿已有适配类型扩展，不要平行再造一套入口：

- `SlimBootstrap`（`Configure`、`OnAppReady`）
- `GodotAssetLoader`、`AssetTicket<T>`
- `GodotAudio`
- `GodotUiStack`、`IUiPage`（`OnOpen` / `OnClose`）、`UiPage`
- `GodotTableFiles.FromGodotRes`
- `GodotUserSaveStore`、`GodotUserTextStore`、`UserDataPaths`
- `GodotPauseSink`
- `NodeSingleton<T>`

`SlimBootstrap.Configure` 现在挂上：`GodotAssetLoader`、`SaveService`（`GodotUserSaveStore`）、`PreferenceStore`（`GodotUserTextStore`）、`FlowMachine`（`GodotPauseSink`）、`GodotAudio`、`GodotUiStack`。增减默认服务是框架 PR，不是插件附带改动。禁止在这里 `Use` 插件。

仅 Godot 的新行为放本目录，命名空间保持 `SlimFramework.GodotAdapter`。要挂到场景上、或被场景脚本继承的 `Node` / `Control`，同一 PR 里做完脚本暂存：源文件留在这里、适配 csproj `Compile Remove`、targets 增加一条 `SlimFrameworkGodotScript`（`StagedPath` 指向 `SlimFramework.Scripts/<Type>.cs`）。不要提交 `SlimFramework.Scripts/`。

新的页面回调加在 `IUiPage` 上。`GodotUiStack` 只通过 `is IUiPage` 调用 `OnOpen` / `OnClose`，不要改成依赖具体类 `UiPage`。

`GodotUiStack` 已有可选事件 `PagePresented` / `PageDismissed`（参数 `Control`）。不要再发明一套页面通知。

## 不变量

- `_Ready` 顺序是 `Configure` → `Init` → `AudioPreferences.Apply` → `Mount` → `OnAppReady`。`_Process` 把帧更新转到 `TickFrame`。
- 没有订阅者时，`Open` / `CloseTop` 与现在相同。`PagePresented` 的订阅者不要在回调里再 `Open` 或 `CloseTop`。
- `SlimBootstrap` 和 `UiPage` 禁止编回适配程序集。不 `Import` targets 则游戏缺类型。
- `IUiPage` 留在适配程序集。它不继承 `Node` / `Control`。
- `NodeSingleton<T>` 今天仍编进适配 DLL，仓库里没有场景继承它。任何游戏脚本或 `.tscn` 继承它之前，必须按暂存规则移出适配程序集。禁止新增「只编译进适配 DLL、没有 `res://`」的场景可继承类型。
- `GodotAssetLoader` 接受 `res://` 与 `uid://`。存档槽是 `user://saves/slot_000.sav`。偏好文本是 `user://` + `GodotUserTextStore.DefaultRelativePath`（`prefs.json`）。`GodotTableFiles.FromGodotRes` 的目录必须是 `res://`，文件名仍是 `{表名}.bytes`。
- 适配层禁止引用游戏和插件。公开 API 英文 PascalCase。注释和文档用中文。

## 怎么验证

```bash
dotnet build SlimFramework.sln -c Release
```

`dotnet build` 不需要安装 Godot，只能证明类型编得过。仓库没有适配层的 `dotnet test`。

动了适配、脚本暂存，或 `samples/DemoGame` 的场景 / 冒烟时，按 [CONTRIBUTING.md](../../CONTRIBUTING.md)：用 Godot 4.7.2 .NET 打开 `samples/DemoGame/project.godot`（不要打开仓库根），Play `Main.tscn`。必须打出 `SlimFramework Godot adapter smoke OK`、`SlimFramework asset catalog smoke OK` 和 `SlimFramework DemoSession OK`，且没有 `ScriptTypeBiMap` 重复登记。

`dotnet run --project samples/SlimFramework.Demo.Runner` 不进编辑器，不能代替这次 Play。

## 常见写错

- 把 `SlimBootstrap` 或 `UiPage` 编回 `SlimFramework.Godot.dll`。
- 在 `SlimBootstrap.Configure` 里 `Use` `ClickOutsideClose`、`AsyncAssets`、`UiLifecycle` 或游戏插件。
- 让 `GodotUiStack` 依赖 `UiPage` 具体类。
- 在没有订阅者时改变 `Open` / `CloseTop`。
- 在 `PagePresented` 里再 `Open` 或 `CloseTop`。
- 引用 `plugins/`、`samples/` 或游戏工程。
- 把引擎类型放回 `src/SlimFramework`。
- 提交 `SlimFramework.Scripts/` 下的拷贝。
- 场景继承 `NodeSingleton<T>`，却不把它移出适配程序集。
