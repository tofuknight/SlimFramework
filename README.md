# For agents

SlimFramework is a small Godot 4 C# game framework. The product is the libraries, not a game.

Dependency direction (enforced by `Directory.Build.targets` where noted):

```
samples/DemoGame  →  SlimFramework.Demo  →  SlimFramework  →  SlimFramework.Contracts
                  →  SlimFramework.Godot →  SlimFramework
tools/Slim.SaveTool → SlimFramework.Contracts
plugins/*           → SlimFramework (+ optional SlimFramework.Godot)
```

Never add a `ProjectReference` from `src/` or `tools/` to `samples/` or to a game. Game code may reference the framework. `SlimFramework` must not reference `GodotSharp`; engine types live in `SlimFramework.Godot` (`namespace SlimFramework.GodotAdapter`).

Public entry: `SlimApp` (`src/SlimFramework/SlimApp.cs`). Modules implement `IModule`. Plugins implement `IPlugin` and are attached with `SlimApp.Use`. Godot games subclass `SlimBootstrap`.

Luban runtime (do not edit casually): `src/SlimFramework/External/Luban`, namespace `Luban`.
Shared protos: `src/SlimFramework.Contracts/Protos`. Game-only protos stay in the game or in `samples/SlimFramework.Demo/Protos`.
Sample table + save round-trip: `samples/SlimFramework.Demo/DemoSession.cs`.

Build: `dotnet build SlimFramework.sln`. Headless proof: `dotnet run --project samples/SlimFramework.Demo.Runner`.

CI (`.github/workflows/ci.yml`) runs `dotnet build SlimFramework.sln -c Release` and `dotnet run --project samples/SlimFramework.Demo.Runner`.

Luban CLI is pinned at `third_party/Luban` (4.12.0, not Luban 5). Generate with `tools/Luban/generate.sh`. Games do not pick their own CLI. Runtime stays `src/SlimFramework/External/Luban`.

License: root `LICENSE` (MIT). Third-party Luban notices stay in `THIRD_PARTY_NOTICES.md`.

`samples/DemoGame` keeps `Sdk="Godot.NET.Sdk/4.7.2"` so the Godot 4.7.2 editor does not rewrite the project. Plain `dotnet build` does not need the editor: `SlimDemoGameEditorBuild` stays off, the SDK's implicit GodotSharp reference is disabled, and the repo `GodotSharp` package is used instead. The editor passes `-p:GodotTargetPlatform=...`, which turns that property on and uses the SDK references. The project file is `SlimFramework.DemoGame.csproj` (Godot looks up `{assembly_name}.csproj`). Details: `samples/DemoGame/README.md`.

# SlimFramework

轻量 Godot 4 / C# 游戏框架。给独立游戏用：模块生命周期、事件、流程、存档信封、偏好、资源目录、Luban 表加载，以及一层薄的 Godot 适配。

不包含关卡、剧情、具体 UI 皮肤。那些放在游戏程序集里，通过 `IPlugin` 接进来。

## 环境

- .NET SDK 8
- 可选：Godot 4.7.2 .NET 编辑器（只在打开 `samples/DemoGame` 时需要）
- `GodotSharp` 4.7.2、`Google.Protobuf` 3.34.1、`Grpc.Tools` 2.80.0（版本写在 `Directory.Build.props`）

## 构建

```bash
dotnet build SlimFramework.sln
dotnet run --project samples/SlimFramework.Demo.Runner
dotnet run --project tools/Slim.SaveTool -- pack /tmp/slot.sav night-1
dotnet run --project tools/Slim.SaveTool -- unpack /tmp/slot.sav
tools/Luban/generate.sh -t client -c cs-bin -d bin --conf Data/luban.conf -x outputCodeDir=Game/Scripts/Config -x outputDataDir=Game/Resource/Config
```

持续集成见 `.github/workflows/ci.yml`：`dotnet build SlimFramework.sln -c Release`，然后 `dotnet run --project samples/SlimFramework.Demo.Runner`。

`dotnet build` 不需要安装 Godot。Runner 会加载一张 Luban bin 表、用 protobuf 做一次存档往返，并走一遍流程 / 偏好 / 资源目录。

`samples/DemoGame` 同一份 csproj 同时给命令行和 Godot 4.7.2 编辑器用。Sdk 保持 `Godot.NET.Sdk/4.7.2`；无编辑器时引用仓库里的 GodotSharp 包，编辑器构建（传入 `GodotTargetPlatform`）改用 SDK 自带引用。打开方式和开关见 [samples/DemoGame/README.md](samples/DemoGame/README.md)。

## 目录

```
src/SlimFramework.Contracts   共享 protobuf（存档信封、资源 ref）
src/SlimFramework              核心。无 Godot 类型
src/SlimFramework/External/Luban   Luban C# 运行时（命名空间 Luban）
src/SlimFramework.Godot        Godot 适配（程序集名 SlimFramework.Godot）
samples/SlimFramework.Demo     示例游戏逻辑，不引用 Godot
samples/SlimFramework.Demo.Runner
samples/DemoGame               最小 Godot 工程。主场景冒烟 Godot 适配，再跑 DemoSession
tools/Slim.SaveTool            只依赖契约的存档信封工具
tools/Luban/generate.sh        用锁定的 Luban CLI 生成表
third_party/Luban              Luban 4.12.0 CLI 压缩包和校验
plugins/                       以后放插件工程
docs/                          架构、Luban、Protobuf
.github/workflows/ci.yml       Release 构建和无头 Demo
```

## 接到自己的游戏

### 子模块

框架仓库根不要当成 Godot 工程打开。把本仓库加为子模块，游戏工程只做 ProjectReference：

```bash
git submodule add https://github.com/tofuknight/SlimFramework.git third_party/SlimFramework
```

```xml
<ItemGroup>
  <ProjectReference Include="third_party/SlimFramework/src/SlimFramework.Godot/SlimFramework.Godot.csproj" />
  <Compile Remove="third_party/**" />
</ItemGroup>
```

`Compile Remove` 是必须的：Godot.NET.Sdk 会编译工程目录下全部 `.cs`。子模块如果躺在游戏目录里，不排除就会和引用的程序集撞类型。子模块放在游戏目录外面时可以不写这条。

核心程序集 `SlimFramework.csproj` 只有在你不需要 Godot 适配时才单独引用（无头工具、编辑器外测试）。

### 入口

游戏根节点继承 `SlimBootstrap`，在 `Configure` 里 `Use` 自己的插件：

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

`base.Configure` 会挂上：`GodotAssetLoader`、`SaveService`、`PreferenceStore`、`FlowMachine`、`GodotAudio`、`GodotUiStack`。

`project.godot`：

```
[autoload]
Bootstrap="*res://Game/GameRoot.cs"
```

或者像 `samples/DemoGame` 那样把该节点设成主场景。节点脚本通过 `SlimApp.Current` 取服务。

### 插件怎么接

实现 `IPlugin`。`Register` 里只能调用框架服务，例如 `app.Get<SaveService>().Register(...)`、`app.Get<FlowMachine>().Register(...)`。玩法类型留在游戏程序集。

## Luban 与 Protobuf

生成步骤、表放哪、存档段怎么写，见 [docs/LUBAN.md](docs/LUBAN.md) 和 [docs/PROTOBUF.md](docs/PROTOBUF.md)。

约定：

- Luban 运行时在框架里；**CLI 锁在 `third_party/Luban`（4.12.0）**；**表类和 `.bytes` 在游戏里**。示例表在 `samples/SlimFramework.Demo/Config`，生成脚本是 `tools/Luban/generate.sh`。
- 框架认识的 protobuf 只有 `SaveSlot` 和 `ResourceRefCatalog`。
- 玩法 message（示例 `DemoProfile`）编译进游戏工程，作为 `ISaveContributor` 的一段字节放进信封。

## 和 back-night 的差别

保留了模块、帧更新、事件、流程、存档参与者、资源引用、UI 层、Node 单例这些用途，但组合根改成显式的 `SlimApp`，不再用反射单例当服务定位器。Godot API 只出现在 `SlimFramework.Godot`。核心可以在没有编辑器的进程里跑（Demo Runner 就是这样）。

刻意没搬过来的东西：异步分帧卸载、BGM 交叉淡化、弹窗点外部关闭。需要时在适配层加，不要加回核心。

更细的类型地图见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。
