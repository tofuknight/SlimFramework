# 架构门禁

改程序集分层、`DemoGame` 的 SDK 开关、Luban pin、Protobuf 契约位置时，用本文挡。不符合就不合。插件、脚本暂存、延后功能见 [extension-rules.md](extension-rules.md)。合入条件见 [../CONTRIBUTING.md](../CONTRIBUTING.md)。

## 程序集分层

依赖只能向下。

```
samples/DemoGame  →  SlimFramework.Demo  →  SlimFramework  →  SlimFramework.Contracts
                  →  SlimFramework.Godot →  SlimFramework
plugins/*         →  SlimFramework（可选再 → SlimFramework.Godot）
tools/*           →  SlimFramework.Contracts（不需要 Godot 时可以 → SlimFramework）
```

| 程序集 | 工程 | 可以引用 | 禁止引用 |
| --- | --- | --- | --- |
| `SlimFramework.Contracts` | `src/SlimFramework.Contracts` | `Google.Protobuf` | `GodotSharp`、游戏、Demo |
| `SlimFramework` | `src/SlimFramework` | Contracts；编进本工程的 Luban 运行时 | `GodotSharp`、`SlimFramework.Godot`、`plugins/`、游戏、Demo |
| `SlimFramework.Godot` | `src/SlimFramework.Godot` | `SlimFramework`、`GodotSharp` | `plugins/`、游戏、Demo |
| `SlimFramework.Demo` | `samples/SlimFramework.Demo` | `SlimFramework`、Contracts | `GodotSharp`、`SlimFramework.Godot`、`DemoGame` |
| `SlimFramework.DemoGame` | `samples/DemoGame` | 适配层、`SlimFramework.Demo`、可选插件 | 被 `src/`、`plugins/`、`tools/` 引用 |
| `Slim.SaveTool` | `tools/Slim.SaveTool` | Contracts | Demo、游戏、适配层 |
| 插件 | `plugins/<Name>/` | `SlimFramework`，需要引擎类型时再加适配层 | `samples/`、任何游戏工程 |

`SlimFramework` 不得引用 `GodotSharp`。引擎类型只在 `SlimFramework.Godot`，命名空间 `SlimFramework.GodotAdapter`。

Framework、Plugins、Tools 禁止引用 Game。包括 `DemoGame` 和以后任何一个游戏工程。游戏可以向下引用框架。

公开入口是 `SlimApp`。模块实现 `IModule`。插件实现 `IPlugin`，游戏用 `SlimApp.Use` 接上。Godot 游戏继承 `SlimBootstrap`。默认能力、可选插件、留在游戏里的部分见文末「全局能力地图」。

### MSBuild 怎么拦住向上引用

最近一份 `Directory.Build.targets` 会截断向上查找。这三份就是当前的检查，不要在子目录再盖一层：

| Target | 文件 | 失败条件 |
| --- | --- | --- |
| `GuardFrameworkDoesNotReferenceGame` | `src/Directory.Build.targets` | `ProjectReference` 路径小写后含 `sample` 或 `demogame` |
| `GuardPluginDoesNotReferenceGame` | `plugins/Directory.Build.targets` | 同上 |
| `GuardToolDoesNotReferenceGame` | `tools/Directory.Build.targets` | 同上 |

时机是 `BeforeTargets="AssignProjectConfiguration"`。失败文本以「依赖方向错误」开头。

另外四个 Guard 管 Godot 边界：

| Target | 文件 | 要求 |
| --- | --- | --- |
| `GuardCoreHasNoGodot` | `src/SlimFramework/SlimFramework.csproj` | 禁止 `PackageReference` `GodotSharp` |
| `GuardContractsHaveNoGodot` | `src/SlimFramework.Contracts/SlimFramework.Contracts.csproj` | 同上 |
| `GuardDemoHasNoGodot` | `samples/SlimFramework.Demo/SlimFramework.Demo.csproj` | 禁止引用适配工程，禁止 `PackageReference` `GodotSharp` |
| `GuardDemoGameGodotSharpSwitch` | `samples/DemoGame/SlimFramework.DemoGame.csproj` | `GodotSharp` 的 `PackageReference` 恰好一份 |

检查盖不住、审阅仍要拒绝的情况：

- 游戏路径里既没有 `sample` 也没有 `demogame`。要加这种工程，先把三份 targets 的条件改掉。
- `HintPath` 直接引游戏 DLL。Guard 只看 `ProjectReference`。
- `src/` 引用 `plugins/`。路径不含那两个子串，Guard 不响。仍然禁止。要机械拦住就在 `GuardFrameworkDoesNotReferenceGame` 里加上 `plugin`，和引用一起进同一个 PR。
- 工具引用 `SlimFramework.Godot`。现有工具 Guard 不看适配层路径。仍然禁止。

## DemoGame 双 SDK

同一份 `samples/DemoGame/SlimFramework.DemoGame.csproj` 给命令行和 Godot 4.7.2 编辑器用。程序集名 `SlimFramework.DemoGame`。编辑器按 `project.godot` 的 `dotnet/project/assembly_name` 找这个文件名；旁边的 `SlimFramework.DemoGame.sln` 让编辑器认到这份工程。

Sdk 固定 `Godot.NET.Sdk/4.7.2`。改成别的字符串，编辑器会重写 csproj。

开关是 `SlimDemoGameEditorBuild`。判定在 `samples/DemoGame/Directory.Build.props`：必须在 SDK 给空的 `GodotTargetPlatform` 填上当前系统之前，看调用方有没有传入该属性。本目录的 `Directory.Build.props` 会挡住仓库根那份，所以它先 `Import` 根上的 props。

| `SlimDemoGameEditorBuild` | 何时 | 引用 |
| --- | --- | --- |
| 未设置 / `false` | `dotnet build`、CI | 关掉 SDK 隐式 Godot 包，用仓库 `GodotSharp` `$(GodotSharpVersion)`（4.7.2）。配置 Debug / Release |
| `true` | 编辑器传入 `-p:GodotTargetPlatform=<平台>` | SDK 自带的 GodotSharp、Debug 下的 GodotSharpEditor、SourceGenerators。配置 Debug / ExportDebug / ExportRelease |

两条路径都输出到 `.godot/mono/temp/bin/`。不要改回 `bin/`。命令和「为什么 Sdk 不能改」见 [../samples/DemoGame/README.md](../samples/DemoGame/README.md)。

改哪一边：

- 不依赖 Godot 的框架行为、存档、表加载：改 `samples/SlimFramework.Demo`，用 `SlimFramework.Demo.Runner` 证明。
- Godot 服务有没有真跑起来：改 `samples/DemoGame` 的 `AdapterSmoke`、`GameRoot`、场景。
- `Sdk` 字符串和 `SlimDemoGameEditorBuild` 的判定位置保持不动，除非 PR 专门改编辑器兼容，并在 Godot 4.7.2 里打开工程验证编辑器没有重写 csproj。

Demo 只证明框架。关卡、剧情、正式 UI 皮肤、玩法系统不进 `DemoGame`，也不进 `src/`。`SlimFramework.Demo` 里现有的 `DemoSession` / `DemoPlugin` 是用法样例，不是第二款游戏。新游戏是单独的仓库，起点是 [../samples/EmptyGame/README.md](../samples/EmptyGame/README.md)。不要把玩法补进 `DemoGame`。

`GameRoot.OnAppReady` 先跑 `AdapterSmoke`，再跑 `DemoSession`。Play `Main.tscn` 的通过标准见 [../CONTRIBUTING.md](../CONTRIBUTING.md)。

## Luban

| 东西 | 位置 | 规则 |
| --- | --- | --- |
| CLI pin | `third_party/Luban`，当前 4.12.0 | 游戏不得自带另一份 CLI。锁的是 Luban 4，不是 Luban 5 |
| 锁文件 | `third_party/Luban/luban.lock` | `version`、`archive`、`sha256`。`generate.sh` 解压前核对 |
| 压缩包 | `third_party/Luban/Luban.7z` | 入库。解压目录 `.extracted/` 不入库 |
| 生成脚本 | `tools/Luban/generate.sh` | 参数原样传给 `Luban.dll`。无参数且校验、解压都通过时，退出码 2 |
| 运行时 | `src/SlimFramework/External/Luban`，命名空间 `Luban` | 不要改读格式。升级时四个文件整组换，并换同一代 CLI 和 `luban.lock`，然后跑 Runner |
| 表类和 `.bytes` | 游戏工程 | 禁止把 `outputCodeDir` / `outputDataDir` 指到 `src/SlimFramework` |
| 本仓库示例 | `samples/SlimFramework.Demo/Config`（`cfg`）、`Fixtures/tbitem.bytes` | 按 Luban 4 `cs-bin` 手写，供没有表工程时跑 Runner |

生成命令、`TableFiles` 和 `GodotTableFiles.FromGodotRes` 的用法见 [LUBAN.md](LUBAN.md)。

当前约定（要改就和 CI 或脚本同一个 PR 改本文）：

- CI 安装 `p7zip-full` 后无参数执行 `generate.sh`，期望退出码 2（核对 sha256、必要时解压、打印用法后结束）。本仓库没有 `luban.conf`。示例表是手写的，Runner 用 `ByteBuf` 重编码后和 `tbitem.bytes` 对比。这次调用不会生成表，也不要把生成物写进核心。
- 提交进 git 的是压缩包、`luban.lock`、`generate.sh`、运行时源文件、手写示例表和 `tbitem.bytes`。不提交 `.extracted/`。
- `generate.sh` 做的事：读 lock，优先用 `sha256sum`、否则用 `shasum -a 256` 核对压缩包，必要时用 `7z` 解压，再 `dotnet Luban.dll`。它不会更新示例表。
- 两个命令用先存在的那个，取第一列和 `luban.lock` 的 `sha256` 比较。都没有就失败，不跳过校验。哈希和 pin 仍是 Luban 4.12.0，不改 lock。

升级 pin 属于「Luban pin」门禁：必须 PR，Forge 架构审阅，并跑通 Runner。

## Protobuf

版本在根 `Directory.Build.props`：`Google.Protobuf` 3.34.1、`Grpc.Tools` 2.80.0。`GrpcServices="None"`，只生成 message。字段级说明见 [PROTOBUF.md](PROTOBUF.md)。

| 契约 | 位置 | 规则 |
| --- | --- | --- |
| `SaveSlot`、`ResourceRefCatalog` | `src/SlimFramework.Contracts/Protos` | 框架认识的只有这两份。玩法字段禁止加进 `SaveSlot` |
| 玩法 message（示例 `DemoProfile`） | 游戏工程，或样例 `samples/SlimFramework.Demo/Protos` | 禁止加进 `SlimFramework.Contracts.csproj` |
| 存档格式（`format_version`、迁移、section 语义） | `SaveService` 与 Contracts | 视同存档格式变更：必须 PR，禁止直接 push `main` |

游戏 message 作为 `ISaveContributor` 的一段字节放进信封。框架代码看不到具体 message 类型。

## 代码放哪

| 任务 | 位置 |
| --- | --- |
| 与引擎无关的新服务 | `src/SlimFramework/<Area>/`，实现 `IModule`。游戏要调用的能力先有核心接口 |
| 仅 Godot 的行为 | `src/SlimFramework.Godot`。要挂到场景上的 `Node` / `Control` 另见脚本暂存 |
| 共享线格式 | `src/SlimFramework.Contracts/Protos` |
| 游戏功能、表、玩法 proto | 游戏工程。写法样例用 `samples/SlimFramework.Demo` |
| 改字节的 CLI | `tools/`，引用 Contracts；不需要 Godot 时才引用 `SlimFramework` |
| 可选附加能力 | `plugins/`，实现 `IPlugin`。延后功能清单见 [extension-rules.md](extension-rules.md) |

核心类型：`SlimApp`、`IModule`、`ITickable`、`IPlugin`、`TickList`、`EventBus`、`IEvent`、`FlowMachine`、`IFlowState`、`IPauseSink`、`SaveService`、`ISaveContributor`、`ISaveStore`、`ISaveMigrator`、`MemorySaveStore`、`PreferenceStore`、`ITextStore`、`ResourceCatalog`、`IAssetLoader`、`TableFiles`、`IAudio`、`AudioPreferences`、`IUiStack`、`UiLayer`、`AudioBus`。

适配类型：`SlimBootstrap`、`GodotAssetLoader`、`GodotAudio`、`GodotUiStack`、`IUiPage`、`UiPage`、`NodeSingleton<T>`、`GodotUserSaveStore`、`GodotUserTextStore`、`GodotTableFiles`、`GodotPauseSink`。其中 `SlimBootstrap` 和 `UiPage` 的编译位置见 [extension-rules.md](extension-rules.md)。`GodotUiStack` 另有可选事件 `PagePresented` / `PageDismissed`。没有订阅者时，打开和关闭不变。点外部关闭插件见 [extension-rules.md](extension-rules.md)。

`SlimApp` 是组合根。`Add` 在 `Init` 之前，关闭顺序相反。`Use` 在 `Init` 之前。`TickFrame` 驱动每个 `ITickable`。`SlimApp.Current` 只由 `SlimApp.Mount` 设置。每种具体类型、每个框架接口（`IAudio`、`IUiStack`、`IAssetLoader` 等）只有一个实例。`IModule` / `ITickable` 不是服务键。模块在主线程用，内部不加锁。

## 全局能力地图

打开文档先看这里：默认有哪些全局能力、用 `SlimApp.Get<T>` 怎么取、哪些要游戏 `Use(IPlugin)` 才出现、哪些留在游戏里。下文对应当前 `SlimBootstrap.Configure` 和 `plugins/` 里的 `Id`。

### 组合根怎么用

`SlimApp` 是组合根。游戏和插件依赖它；它不依赖游戏，也不依赖 Godot。

- `Add` 在 `Init` 之前。`Init` 之后再 `Add` 会抛 `InvalidOperationException`。
- `Use(IPlugin)` 在 `Init` 之前。`Register` 里再 `Add` 模块，或向已经 `Add` 的服务登记扩展。`Id` 不能为空。
- 取用是 `Get<T>()` / `TryGet<T>()`。`Add` 登记具体类型，以及除 `IModule`、`ITickable`、`IDisposable` 以外的接口。每种具体类型、每个这样的接口只有一个实例。
- `SlimApp.Current` 在 `SlimApp.Mount` 之后才有。`Dispose` 时 `Unmount`。

Godot 入口继承 `SlimBootstrap`，重写 `Configure`：先 `base.Configure(app)`，再 `Use` 插件。`_Ready` 的顺序是 `Configure` → `Init` → `AudioPreferences.Apply` → `Mount` → `OnAppReady`。节点脚本用 `SlimApp.Current` 取服务。

```csharp
public partial class GameRoot : SlimBootstrap
{
    protected override void Configure(SlimApp app)
    {
        base.Configure(app);
        app.Use(new MyGamePlugin());
    }
}
```

### 默认已挂

`SlimBootstrap.Configure` 挂上下表。`EventBus` 在构造 `SlimApp` 时就有，不经过 `Add`。`AudioPreferences.Apply` 在 `Init` 之后由 `_Ready` 调用，不是一项 `Add`。

| 能力 | 取用接口/类型 | 实现（适配） | 备注 |
| --- | --- | --- | --- |
| 资源加载 | `IAssetLoader` | `GodotAssetLoader` | 同步 `Load` / `Release`。也可 `Get<GodotAssetLoader>()`。路径接受 `res://` 与 `uid://` |
| 存档 | `SaveService` | `SaveService` + `GodotUserSaveStore` | 槽位 `user://saves/slot_000.sav`。`ISaveStore` 不单独注册 |
| 偏好 | `PreferenceStore` | `PreferenceStore` + `GodotUserTextStore` | 文本 `user://prefs.json`（`GodotUserTextStore.DefaultRelativePath`）。`ITextStore` 不单独注册 |
| 流程 | `FlowMachine` | `FlowMachine` + `GodotPauseSink` | 暂停写到 `SceneTree.Paused`。`IPauseSink` 不单独注册。切换时经 `Events` 发布 `FlowChanged` |
| 音频 | `IAudio` | `GodotAudio` | `Music` / `Sfx` / `Ui`。也可 `Get<GodotAudio>()` |
| UI 栈 | `IUiStack` | `GodotUiStack` | `Open` / `CloseTop`。也可 `Get<GodotUiStack>()`。`PagePresented` / `PageDismissed` 没有订阅者时，打开和关闭不变 |
| 事件 | `SlimApp.Events` | `EventBus` | 不走 `Add`，也不是 `Get<EventBus>()`。`Subscribe<T>` / `Publish<T>`，载荷实现 `IEvent` |
| 音量套用 | `AudioPreferences.Apply` | 静态方法 | `_Ready` 在 `Init` 之后、`Mount` 之前调用。读 `PreferenceStore` 的 `audio.music` / `audio.sfx` / `audio.ui`，写到 `IAudio`。缺键或解析失败保持当前音量，不写回偏好 |

### 插件可选

游戏在 `GameRoot.Configure` 里、`base.Configure` 之后 `Use`。禁止把这些插件写进 `SlimBootstrap.Configure` 的默认列表。`main` 上 `plugins/` 只有下面三个。`samples/SlimFramework.Demo` 的 `DemoPlugin`（`Id` 为 `demo`）是无头用法样例，不在此表，也不进默认列表。

没 `Use` 的插件不改变核心行为：资源仍是同步 `Load` / `Release`，页面仍只有打开和关闭，点在页面矩形外不会 `CloseTop`。

| 插件 | Id | 程序集路径 | 提供什么 | 取用方式 |
| --- | --- | --- | --- | --- |
| `ClickOutsideClosePlugin` | `click-outside-close` | `plugins/ClickOutsideClose/ClickOutsideClose.csproj` | 指针在栈顶页面矩形外按下时调用 `IUiStack.CloseTop` | `app.Use(new ClickOutsideClosePlugin())`。需要已经 `Add` 的 `GodotUiStack`。模块是 `internal`，没有 `Get` 键 |
| `AsyncAssetsPlugin` | `async-assets` | `plugins/AsyncAssets/AsyncAssets.csproj` | 后台加载 `AsyncAssetLoader`，分帧释放 `ReleaseQueue`（默认每帧 8 次） | `app.Use(new AsyncAssetsPlugin())`。`Get<AsyncAssetLoader>()`、`Get<ReleaseQueue>()`。需要已经 `Add` 的 `GodotAssetLoader` |
| `UiLifecyclePlugin` | `ui-lifecycle` | `plugins/UiLifecycle/UiLifecycle.csproj` | 页面被盖住 / 重新成为栈顶时 `IUiPageLifecycle.OnCovered` / `OnRevealed`。栈顶按打开顺序，与 `CloseTop` 一致 | `app.Use(new UiLifecyclePlugin())`。`Get<UiLifecycleModule>()`。页面可选实现 `IUiPageLifecycle`。需要已经 `Add` 的 `GodotUiStack` |

### 明确不进框架

- DI 容器、Manager 集合、MVVM
- 玩法系统：关卡、战斗、背包、任务、剧情
- 表生成物、玩法 proto（留在游戏工程；框架 protobuf 只有 `SaveSlot`、`ResourceRefCatalog`）

见 [gap-backlog.md](gap-backlog.md)「明确不做」。

### 和 QFramework Architecture 的对应

要的是能力清单这一层清楚。对应关系：`Architecture` ≈ `SlimApp`，`RegisterSystem` ≈ `Add` / `Use`，`GetSystem` ≈ `Get<T>`。框架不引入 `Architecture`、`Command`、`Query`、`BindableProperty`，也不另做一套静态注册表。
