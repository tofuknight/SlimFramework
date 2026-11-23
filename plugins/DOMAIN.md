# Plugins

`plugins/` 的写作约束。边界以 [docs/extension-rules.md](../docs/extension-rules.md) 为准，四个插件的取用方式见 [docs/architecture.md](../docs/architecture.md)「全局能力地图」。不要再开一类 Domain。某个插件的特例写在下面的小节里。

## 范围

`plugins/<Name>/`，独立类库，加入 `SlimFramework.sln`。脚手架是 `plugins/<Name>/<Name>.csproj` 和 `<Name>Plugin.cs`。

`main` 上只有这四个。不要把还没落地的功能写成已经有：

| 插件 | Id | 命名空间 | 路径 |
| --- | --- | --- | --- |
| `ClickOutsideClosePlugin` | `click-outside-close` | `SlimFramework.ClickOutsideClose` | `plugins/ClickOutsideClose/` |
| `AsyncAssetsPlugin` | `async-assets` | `SlimFramework.AsyncAssets` | `plugins/AsyncAssets/` |
| `UiLifecyclePlugin` | `ui-lifecycle` | `SlimFramework.UiLifecycle` | `plugins/UiLifecycle/` |
| `CutoutSkeletonPlugin` | `cutout-skeleton` | `SlimFramework.CutoutSkeleton` | `plugins/CutoutSkeleton/` |

`samples/SlimFramework.Demo` 的 `DemoPlugin`（`Id` 为 `demo`）是无头用法样例，不在本目录，也不进默认列表。

## 能依赖 / 不能依赖

可以 `ProjectReference` `SlimFramework`。插件自己的代码用到引擎类型时，再引用 `SlimFramework.Godot`。

禁止引用 `samples/`、`DemoGame`、任何游戏程序集、关卡、剧情。`plugins/Directory.Build.targets` 的 `GuardPluginDoesNotReferenceGame` 拦住路径里带 `sample` / `demogame` 的 `ProjectReference`。其余情况仍然禁止。

`src/SlimFramework` 和 `src/SlimFramework.Godot` 不得反过来引用插件。插件禁止引用游戏。

## 可扩展的公开 API

实现已有的 `IPlugin`：`string Id`、`void Register(SlimApp app)`。不要另做插件基类或注册表。

`Register` 里可以 `app.Add` 模块，或向已经 `Add` 的服务登记扩展（例如 `SaveService.Register`）。不要在 `Register` 里依赖游戏类型。

游戏在自己的 `Configure` 里、`base.Configure` 之后 `app.Use`。禁止把插件写进 `SlimBootstrap.Configure`。没 `Use` 的插件不得改变核心行为。

需要核心尚不存在的钩子时：钩子是框架 PR，未挂插件时行为不变；功能本体仍在 `plugins/`。禁止把实现移进 `src/`。

需要 Godot 的插件只能由 `samples/DemoGame`、`samples/CutoutSkeletonDemo` 或真实游戏 `Use`。`samples/SlimFramework.Demo` 禁止引用这种插件。

新可选能力一次一个插件 PR。明确不做的项（DI、Manager、MVVM、玩法系统、`Command` / `Query` / `Architecture`）见 [docs/gap-backlog.md](../docs/gap-backlog.md)。BGM 交叉淡化还没做；要做也是新插件，且未挂时 `PlayMusic` / `StopMusic` 仍是直接切换。

## 不变量

- `Id` 不能为空。`Use` 发生在 `Init` 之前。
- 插件不会自动进入 `SlimBootstrap`。游戏必须自己 `Use`。
- 未挂插件时：资源仍是同步 `Load` / `Release`，页面仍只有打开和关闭，点在页面矩形外不会 `CloseTop`。
- 公开 API 英文 PascalCase。中文只在注释和文档。
- 本目录禁止引用游戏。

### ClickOutsideClose

`Register` 要求已经 `Add` 的 `GodotUiStack`（`Get<IUiStack>()` 必须是它），否则抛 `InvalidOperationException`。模块是 `internal`，没有 `Get` 键。它订阅 `PagePresented` / `PageDismissed`，在主窗输入里用页面的全局矩形判断，只调用已有的 `IUiStack.CloseTop`。`Init` 时主循环必须是场景树。矩形面积还是 0 时不关闭。

### AsyncAssets

`Register` 要求已经 `Add` 的 `GodotAssetLoader`。取用是 `Get<AsyncAssetLoader>()` 和 `Get<ReleaseQueue>()`。`AsyncAssetsPlugin` 的 `releasesPerFrame` 默认 8，且必须大于 0。

`LoadAsync` 只发出后台请求。完成发生在后续 `Tick`：状态变为已加载的那一帧，先取走后台结果，再对每个未取消的 handle 调一次 `GodotAssetLoader.Load<T>`。同路径请求合并。取消的 handle 不会 `Load`，也不占引用。路径只接受 `res://` 与 `uid://`。`ReleaseQueue` 每帧最多 `Release` 次数等于构造时的 N，`Shutdown` 时同步清空。

### UiLifecycle

`Register` 要求已经 `Add` 的 `GodotUiStack`。取用是 `Get<UiLifecycleModule>()`。页面可选实现 `IUiPageLifecycle`（`OnCovered` / `OnRevealed`）。

打开序只来自 `PagePresented` / `PageDismissed`，不另做一套栈。栈顶按打开顺序，与 `CloseTop` 一致，不按 `UiLayer`。`GodotUiStack.Shutdown` 只调用 `OnClose`，不发 `PageDismissed`，因此这段时间没有 `OnRevealed`。回调里不要 `Open` 或 `CloseTop`。核心侧页面生命周期仍然只有 `IUiPage.OnOpen` / `OnClose`。

### CutoutSkeleton

`Register` 不要求已经 `Add` 的服务。取用是 `Get<CutoutSkeletonModule>()`。`Attach` 用调用方给的骨骼表和槽位表建 Godot 内置 `Skeleton2D` / `Bone2D`。槽位要么是挂在一根骨头上的 `Sprite2D`，要么是骨架上的蒙皮 `Polygon2D`（`CutoutSkinDefinition`，骨头路径相对骨架，权重和顶点对齐）。`SetSlotTexture` 两种都只改贴图。片段在同一台 `AnimationPlayer` 上，不按贴图复制。`SpineSine` 可选：调用方点名哪些骨骼，每帧把它们的旋转写成静息角加正弦；`AmplitudeScales` 与名单等长，用来让振幅沿链变化或反向，系数为 0 的骨头不写旋转。`ScaleXAmplitudes` 可选，只改这些骨头的局部 `Scale.X`（从静息收到 `1 - 振幅`），不写根节点，关掉正弦时恢复。不改骨骼层级，也不改贴图。`Tick` 里每套骨架先立刻写一次，再把本帧还在摆动的骨架合成一次空闲补写，避免每套各排一个 `CallDeferred`。直接调用 `Advance` 的单套骨架仍自己推迟一次。`SetUniformScale` 写根节点缩放的绝对值，保留已经有的 Y 符号。插件里没有物种、基因或玩法表。

## 怎么验证

四个插件工程都在 `SlimFramework.sln` 里。仓库没有插件专用测试。

```bash
dotnet build SlimFramework.sln -c Release
```

`samples/DemoGame` 的 `GameRoot` 在 `base.Configure` 之后 `Use` 了 `ClickOutsideClosePlugin`（以及示例 `DemoPlugin`）。它不 `Use` `AsyncAssetsPlugin`、`UiLifecyclePlugin` 或 `CutoutSkeletonPlugin`。无头 Runner 不引用这些插件，不能证明插件行为。

动了 Godot 插件时，仍按 [CONTRIBUTING.md](../CONTRIBUTING.md) Play `samples/DemoGame` 的 `Main.tscn`（两行 OK，且没有 `ScriptTypeBiMap` 重复登记）。那次冒烟不能代替对 AsyncAssets、UiLifecycle 或 CutoutSkeleton 行为的核对。剪影换装看 `samples/CutoutSkeletonDemo`：Play `Main.tscn` 应打出 `SlimFramework cutout skeleton sample smoke OK`。

## 常见写错

- 把 `app.Use(插件)` 写进 `SlimBootstrap.Configure`。
- 引用 `samples/` 或游戏工程，或让 `src/` 引用 `plugins/`。
- 为了少建一个工程，把功能直接加进核心或适配层的默认启动路径。
- 在 `LoadAsync` 的当帧就调用 `GodotAssetLoader.Load`。完成必须等后续 `Tick`。
- 让 UiLifecycle 按 `UiLayer` 决定栈顶，或在 `GodotUiStack.Shutdown` 期间期待 `OnRevealed`。
- `GodotUiStack` 还没 `Add` 就 `Use` 点外部关闭或 UI 生命周期。
- 让 `samples/SlimFramework.Demo` 引用需要 Godot 的插件。
- 把 BGM 交叉淡化写成已经存在的插件。
- 把物种表、换装档位或玩法规则写进 `CutoutSkeleton`。
- 在 `SlimBootstrap.Configure` 里 `Use` `CutoutSkeletonPlugin`。
