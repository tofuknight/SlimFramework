# Samples

`samples/` 的写作约束。样例只演示怎么用框架，不把能力推回 `src/`。分层和双 SDK 见 [docs/architecture.md](../docs/architecture.md)。新游戏空壳见 [EmptyGame/README.md](EmptyGame/README.md)。

## 范围

| 路径 | 程序集 | 做什么 |
| --- | --- | --- |
| `samples/SlimFramework.Demo/` | `SlimFramework.Demo` | 无头示例。`DemoSession`、`DemoPlugin`（`Id` 为 `demo`）、`Protos/demo_profile.proto`、手写表 `Config/`（命名空间 `cfg`）、`Fixtures/tbitem.bytes` |
| `samples/SlimFramework.Demo.Runner/` | Runner 工程 | `dotnet run` 入口，跑 `DemoSession` |
| `samples/DemoGame/` | `SlimFramework.DemoGame` | 最小 Godot 工程。`GameRoot` 先跑 `AdapterSmoke`，再跑 `AssetCatalogSmoke`，然后跑 `DemoSession`。资源清单在 `Assets/resource_refs.json` |
| `samples/CutoutSkeletonDemo/` | `SlimFramework.CutoutSkeletonDemo` | 剪影换装和对战样例。`GameRoot` 在 `base.Configure` 之后 `Use` `CutoutSkeletonPlugin`。鱼的骨骼、三档口 / 体 / 鳍、共用片段和对战规则留在这里。对战场景是 `Combat.tscn` |
| `samples/EmptyGame/` | 不在 `SlimFramework.sln` | 拷到单独游戏仓库的空壳。不要在本仓库里打开或构建 |

`DemoSession` / `DemoPlugin` 是用法样例，不是第二款游戏。关卡、剧情、正式 UI 皮肤、玩法系统不进 `DemoGame`，也不进 `src/`。

## 能依赖 / 不能依赖

`SlimFramework.Demo` 可以引用 `SlimFramework` 和 `SlimFramework.Contracts`。禁止 `GodotSharp`、`SlimFramework.Godot`、`DemoGame`，以及需要 Godot 的插件。`GuardDemoHasNoGodot` 会失败。

`DemoGame` 可以引用适配层、`SlimFramework.Demo` 和可选插件。它不能被 `src/`、`plugins/`、`tools/` 引用。

`CutoutSkeletonDemo` 可以引用适配层和 `plugins/CutoutSkeleton`。它不引用 `SlimFramework.Demo`。鱼的部位对照留在这个样例里，不进插件，也不进 `DemoGame`。

`EmptyGame` 只在拷出的游戏仓库里引用子模块。不要从 `src/`、`plugins/`、`tools/` 添加指向它的 `ProjectReference`。

样例可以向下引用框架。框架、插件、工具禁止反过来引用本目录里的任何游戏工程。

## 可扩展的公开 API

这里实现的是游戏侧用法，不是框架新 API。

- `DemoPlugin` 实现已有的 `IPlugin`，向已经 `Add` 的 `SaveService` 和 `FlowMachine` 登记 `ProfileContributor` 与 `IFlowState`。
- `DemoProfile` 是玩法 message 的样例，留在本目录的 proto。不要移进 `SlimFramework.Contracts`。
- `GameRoot` 继承 `SlimBootstrap`。`Configure` 里先 `base.Configure(app)`，再 `Use`。当前 `Use` 的是 `DemoPlugin` 和 `ClickOutsideClosePlugin`。
- 插件不会自动挂上。要演示 `AsyncAssetsPlugin` 或 `UiLifecyclePlugin`，在 `GameRoot.Configure` 里自己 `Use`。禁止为了样例去改 `SlimBootstrap.Configure`。
- `samples/SlimFramework.Demo` 不要引用 Godot 插件。
- `CutoutSkeletonDemo` 的 `GameRoot` 自己 `Use` `CutoutSkeletonPlugin`。不要把这次 `Use` 写进 `SlimBootstrap` 或 `samples/DemoGame`。

不要在样例里发明框架类型，也不要把样例里的能力补进 `src/`。可选能力做 `plugins/`，由游戏自己 `Use`。不要把玩法补进 `DemoGame`。

`DemoGame` 的 Sdk 保持 `Godot.NET.Sdk/4.7.2`。`SlimDemoGameEditorBuild` 的判定位置不要挪，除非这次 PR 专门改编辑器兼容。见 [DemoGame/README.md](DemoGame/README.md)。

`CutoutSkeletonDemo` 的 Sdk 同样保持 `Godot.NET.Sdk/4.7.2`。判定属性是它自己的 `SlimCutoutSkeletonDemoEditorBuild`，不要改 `SlimDemoGameEditorBuild`，也不要改 Sdk 字符串。

## 不变量

- 样例证明框架，不反向定义框架。
- `GameRoot.OnAppReady` 先 `AdapterSmoke.Run`，再 `AssetCatalogSmoke.Run`，然后 `DemoSession.Run`。通过标准是打出 `SlimFramework Godot adapter smoke OK`、`SlimFramework asset catalog smoke OK` 和 `SlimFramework DemoSession OK`。
- `Assets/resource_refs.json` 归这个样例。`resource_refs.bytes` 和 `AssetKeys.cs` 由 `tools/Slim.AssetCatalogTool` 生成后提交。改清单后要重新生成。玩法代码使用 `AssetKeys`，不写 `res://` 路径。
- 手写示例表不要生成进 `src/SlimFramework`。`AdapterSmoke` 读的 `res://Config` 与 `Fixtures/tbitem.bytes` 是同一份手写表。
- `EmptyGame` 不进解决方案，不在本仓库构建。
- 插件由游戏 `Use`。`SlimBootstrap` 的默认列表不含 `ClickOutsideClose`、`AsyncAssets`、`UiLifecycle`、`CutoutSkeleton`。
- 样例程序集外可见的名字仍用英文 PascalCase。注释和文档用中文。

## 怎么验证

```bash
dotnet build SlimFramework.sln -c Release
dotnet run --project samples/SlimFramework.Demo.Runner
```

不要构建 `samples/EmptyGame/Game.csproj`。仓库没有 `dotnet test`。

动了 `samples/DemoGame` 的场景或冒烟时，用 Godot 4.7.2 .NET 打开 `samples/DemoGame/project.godot`，Play `Main.tscn`。必须打出上面两行，且没有 `ScriptTypeBiMap` 重复登记。命令行构建不需要编辑器，也不能代替这次 Play。

动了 `samples/CutoutSkeletonDemo` 时，用 Godot 4.7.2 .NET 打开 `samples/CutoutSkeletonDemo/project.godot`（不要打开仓库根）。换装 Play `Main.tscn`，通过标准是打出 `SlimFramework cutout skeleton sample smoke OK`，并且换口、体、鳍后游动和咬合仍是同一套片段。对战 Play `Combat.tscn`（文件系统里选中后 F6，或在换装界面按 `C`）。无头换装是 `godot --headless --path samples/CutoutSkeletonDemo`，打印上面那一行后退出。无头对战是 `godot --headless --path samples/CutoutSkeletonDemo -- --combat`，或 `godot --headless --path samples/CutoutSkeletonDemo res://Combat.tscn`，打印 `SlimFramework cutout combat sample smoke OK` 后退出。这次 Play 不能代替 DemoGame 的 AdapterSmoke。

## 常见写错

- 把样例里的玩法、表或 proto 上移到 `src/` 或 `SlimFramework.Contracts`。
- 让 `SlimFramework.Demo` 引用 `GodotSharp`、适配层或 `ClickOutsideClose`。
- 在 `SlimBootstrap.Configure` 里默认 `Use` 插件，而不是在 `GameRoot` 里 `Use`。
- 往 `DemoGame` 加关卡、战斗、背包、剧情，或把剪影鱼的部位表塞进 `DemoGame`。
- 把口、体、鳍的档位写进 `plugins/CutoutSkeleton` 或 `src/`。
- 在本仓库打开 `EmptyGame/project.godot`，或把它加进 `SlimFramework.sln`。
- 改 `DemoGame` 的 `Sdk` 字符串，让 Godot 4.7.2 编辑器重写 csproj。
- 从 `src/`、`plugins/`、`tools/` 引用 `samples/`。
