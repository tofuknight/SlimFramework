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

公开入口是 `SlimApp`。模块实现 `IModule`。插件实现 `IPlugin`，游戏用 `SlimApp.Use` 接上。Godot 游戏继承 `SlimBootstrap`。

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

Demo 只证明框架。关卡、剧情、正式 UI 皮肤、玩法系统不进 `DemoGame`，也不进 `src/`。`SlimFramework.Demo` 里现有的 `DemoSession` / `DemoPlugin` 是用法样例，不是第二款游戏。

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

适配类型：`SlimBootstrap`、`GodotAssetLoader`、`GodotAudio`、`GodotUiStack`、`IUiPage`、`UiPage`、`NodeSingleton<T>`、`GodotUserSaveStore`、`GodotUserTextStore`、`GodotTableFiles`、`GodotPauseSink`。其中 `SlimBootstrap` 和 `UiPage` 的编译位置见 [extension-rules.md](extension-rules.md)。

`SlimApp` 是组合根。`Add` 在 `Init` 之前，关闭顺序相反。`Use` 在 `Init` 之前。`TickFrame` 驱动每个 `ITickable`。`SlimApp.Current` 只由 `SlimApp.Mount` 设置。每种具体类型、每个框架接口（`IAudio`、`IUiStack`、`IAssetLoader` 等）只有一个实例。`IModule` / `ITickable` 不是服务键。模块在主线程用，内部不加锁。
