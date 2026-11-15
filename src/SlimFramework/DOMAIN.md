# Core

`src/SlimFramework/` 的写作约束。分层和默认能力以 [docs/architecture.md](../../docs/architecture.md) 为准，插件边界见 [docs/extension-rules.md](../../docs/extension-rules.md)。本文不是扩大核心的许可。

## 范围

程序集 `SlimFramework`，工程 `src/SlimFramework/SlimFramework.csproj`。命名空间 `SlimFramework` 及其子命名空间（`Audio`、`Config`、`Event`、`Flow`、`Persistence`、`Resources`、`UI`）。

Luban 运行时在 `External/Luban`，命名空间 `Luban`（`ByteBuf`、`BeanBase`）。不要改读格式。

本目录没有 Godot 类型，没有插件，也没有游戏或 `samples/`。

## 能依赖 / 不能依赖

可以引用 `SlimFramework.Contracts`，以及已经编进本工程的 Luban 运行时。

禁止引用 `GodotSharp`、`SlimFramework.Godot`、`plugins/`、`samples/`、任何游戏工程。`GuardCoreHasNoGodot` 拦住 `GodotSharp` 包。`src/Directory.Build.targets` 的 `GuardFrameworkDoesNotReferenceGame` 拦住路径里带 `sample` / `demogame` 的 `ProjectReference`。Guard 不响的引用（游戏目录改名、`HintPath`、引用 `plugins/`）仍然禁止。

## 可扩展的公开 API

组合根和扩展点已经有了，不要另起一套：

- `SlimApp`：`Add`、`Use`、`Get<T>`、`TryGet<T>`、`Init`、`TickFrame`、`Shutdown`、`Mount` / `Unmount`、`Current`、`Events`
- `IModule`、`ITickable`、`IPlugin`。`IPlugin` 的实现放 `plugins/` 或游戏，不要为了省事写进核心
- `IEvent`、`EventBus`（`Subscribe<T>` / `Publish<T>`）
- `FlowMachine`（`Register`、`GoTo`、`Pause`、`Resume`）、`IFlowState`、`IPauseSink`、`FlowChanged`
- `SaveService`、`ISaveContributor`、`ISaveStore`、`ISaveMigrator`、`MemorySaveStore`、`SaveSlotInfo`
- `PreferenceStore`、`ITextStore`、`MemoryTextStore`
- `IAssetLoader`（`Load<T>` / `Release`）、`ResourceCatalog`、`ResourceRefEntry`、`ResourceRefLoader`
- `TableFiles.FromReader`、`TableFiles.FromDirectory`
- `IAudio`、`AudioBus`（`Music` / `Sfx` / `Ui`）、`AudioPreferences`
- `IUiStack`（`Open` / `CloseTop`）、`UiLayer`

与引擎无关的新服务可以放 `src/SlimFramework/<Area>/`，实现 `IModule`。游戏要调用的能力先有核心接口。已有扩展点是：`ISaveContributor`、`ISaveStore`、`ISaveMigrator`、`ITextStore`、`IAssetLoader`、`IFlowState`、`IPauseSink`、`IEvent`、`ITickable`。引擎相关的实现放 `src/SlimFramework.Godot`。玩法实现放游戏工程，不要把玩法类型写进核心。

不要发明 `Architecture`、`Command`、`Query`、`BindableProperty`、`IController`、DI 容器、Manager 集合、MVVM，或第二套静态注册表。对应关系只到 `SlimApp` / `Add` / `Use` / `Get<T>`。

## 不变量

- `Add` 在 `Init` 之前。之后再 `Add` 会抛 `InvalidOperationException`。关闭顺序与注册相反。
- `Use(IPlugin)` 在 `Init` 之前。`Id` 不能为空。`Register` 只触达框架服务。
- `Get<T>` 的键是具体类型，以及除 `IModule`、`ITickable`、`IDisposable` 以外的接口。每种具体类型、每个这样的接口只有一个实例。`IModule` / `ITickable` 不是服务键。
- `SlimApp.Current` 只由 `SlimApp.Mount` 设置。`Dispose` 时 `Unmount`。
- 模块在主线程用，内部不加锁。
- 公开 API 英文 PascalCase。中文只在注释和文档。
- 本程序集禁止引用游戏。

Godot 入口怎么把下面这些能力挂上，看架构文档「全局能力地图」。核心只提供端口。

### 存档

`SaveService` 把每个 `ISaveContributor` 的字节写进 `SaveSlot.sections`。框架不解析玩法 message。`ISaveStore` 不单独注册。`format_version`、迁移、section 语义视同存档格式变更。`TryPeek` 不调用 `Restore`。缺段时 `Restore` 收到空数组。无 Godot 的存储是 `MemorySaveStore`。字段级约定见 [docs/PROTOBUF.md](../../docs/PROTOBUF.md)。

### 资源

`IAssetLoader` 只约定同步 `Load<T>` / `Release`（每次 `Load` 加一次引用）。路径含义由实现决定。核心没有异步加载，也没有分帧释放。`ResourceCatalog.Parse` 只装载 `ResourceRefCatalog`。Uid 加载失败后再回退 Path 不做。

### UI

`IUiStack` 只有 `Open`、`CloseTop`、`OpenCount`。`UiLayer` 只影响绘制，不决定关闭顺序。没有 `OnCovered` / `OnRevealed`。点页面矩形外不会 `CloseTop`。

### 音频

`IAudio` 是 `SetVolume` / `GetVolume`、`PlayMusic` / `StopMusic`、`PlaySfx`、`PlayUi`。`PlayMusic` / `StopMusic` 直接切换，没有交叉淡化。`AudioPreferences.Apply` 读 `audio.music` / `audio.sfx` / `audio.ui`，缺键或解析失败保持当前音量，不写回偏好。它不是一项 `Add`。

### 表加载

`TableFiles` 把「表名 → bytes」收成 Luban 的 `Func<string, ByteBuf>`。文件名是 `{表名}.bytes`。生成的表类和 `.bytes` 留在游戏工程，不要放进本目录。`res://` 包内读取不在核心。运行时四个文件整组换，见 [docs/LUBAN.md](../../docs/LUBAN.md)。

### 事件、流程、偏好

`EventBus` 在构造 `SlimApp` 时就有，不走 `Add`，也不是 `Get<EventBus>()`。使用 `SlimApp.Events`。流程切换经 `Events` 发布 `FlowChanged`。`IPauseSink` 不单独注册。场景切换由 `IFlowState` 自己完成，核心不绑定场景类型。偏好是 `PreferenceStore` + `ITextStore`；`ITextStore` 不单独注册。无 Godot 的文本存储是 `MemoryTextStore`。

## 怎么验证

仓库没有单独的测试工程。与 CI 相同：

```bash
dotnet build SlimFramework.sln -c Release
dotnet run --project samples/SlimFramework.Demo.Runner
```

`dotnet build` 不需要安装 Godot。Runner 覆盖表加载、protobuf 存档往返、流程、偏好、资源目录。它不进编辑器。

## 常见写错

- 引用 `GodotSharp`、`SlimFramework.Godot`、`plugins/` 或 `samples/`。
- 把异步加载、点外部关闭、`OnCovered` / `OnRevealed`、BGM 交叉淡化写进核心。前三项已是插件，交叉淡化仍未做。见 [docs/extension-rules.md](../../docs/extension-rules.md)。
- 把 `IModule` 或 `ITickable` 当成 `Get<T>` 的键，或同一种服务注册两次。
- `Init` 之后 `Add`。
- 发明 `Command`、`Query`、`BindableProperty`、`Architecture`。
- 把表生成物、玩法 proto、关卡或剧情放进本目录。
- 改 `External/Luban` 的读格式，或只换其中一两个文件。
- 公开 API 用中文命名。
