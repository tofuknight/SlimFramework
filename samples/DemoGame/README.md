# DemoGame

用 Godot **4.7.2 .NET** 编辑器打开本目录的 `project.godot`。不要把仓库根目录当游戏工程。

`dotnet build SlimFramework.sln` 不需要安装编辑器。和编辑器打开是同一份 `SlimFramework.DemoGame.csproj`，用属性切换引用，不用手改 Sdk。

## 为什么 Sdk 不能改

Godot 4.7.2 只认 `Sdk="Godot.NET.Sdk/4.7.2"`。打开工程时如果不是这个字符串，编辑器会改写 csproj。SDK 在编辑器构建里会自己引用 GodotSharp 4.7.2；再留一份 `PackageReference` 会类型重复。

编辑器按 `project.godot` 里的 `dotnet/project/assembly_name` 找 `{程序集名}.csproj`，所以文件名是 `SlimFramework.DemoGame.csproj`。旁边的 `SlimFramework.DemoGame.sln` 让编辑器认到这份工程。没有这个解决方案时，编辑器会新建 csproj，把项目引用和开关一起覆盖掉。

## 开关 `SlimDemoGameEditorBuild`

判定在 `Directory.Build.props`：Godot.NET.Sdk 会给空的 `GodotTargetPlatform` 填上当前系统，所以要在那之前看调用方有没有传入该属性。

| 值 | 何时 | 引用 |
| --- | --- | --- |
| 未设置 / `false` | `dotnet build`、`.github/workflows/ci.yml` | 关掉 SDK 隐式 Godot 包，使用仓库的 `GodotSharp` `$(GodotSharpVersion)`（4.7.2）。配置为 Debug / Release |
| `true` | 编辑器构建传入 `-p:GodotTargetPlatform=<平台>` | SDK 自带的 GodotSharp、Debug 下的 GodotSharpEditor、SourceGenerators。配置为 Debug / ExportDebug / ExportRelease |

```bash
# 与 .github/workflows/ci.yml 的构建同一条命令。CI 使用 -c Release，并再跑 Demo.Runner。
dotnet build ../../SlimFramework.sln

# 与编辑器相同（linuxbsd / windows / macos）
dotnet build SlimFramework.DemoGame.csproj -p:GodotTargetPlatform=linuxbsd

# 或者直接打开开关；强制走无编辑器路径则设为 false
dotnet build SlimFramework.DemoGame.csproj -p:SlimDemoGameEditorBuild=true
dotnet build SlimFramework.DemoGame.csproj -p:SlimDemoGameEditorBuild=false
```

两种路径都输出到 `.godot/mono/temp/bin/`。SDK 在还原 NuGet 之前就改了这个目录，编辑器也从这里加载 `SlimFramework.DemoGame.dll`。

## 主场景在验什么

`GameRoot.Configure` 在 `base.Configure` 之后 `Use` `ClickOutsideClosePlugin`。`SlimBootstrap.Configure` 不挂它。冒烟不点击，所以这两行 OK 不覆盖点外部关闭。手动验法：打开一个矩形没有铺满视口的页面，在矩形外按下鼠标左键，栈顶应被 `CloseTop` 关掉。未 `Use` 该插件时，`GodotUiStack` 的打开和关闭与没有 `PagePresented` / `PageDismissed` 订阅者时相同。

`GameRoot.OnAppReady` 先跑 `AdapterSmoke`，再跑无头的 `DemoSession`。冒烟用的是 Bootstrap 已经挂上的服务，不是另一套 `MemorySaveStore`：

- `res://SmokePage.tscn`（根节点是 `Control`）经 `IUiStack.Open` / `CloseTop`
- `audio.music` / `audio.sfx` / `audio.ui` 写入偏好后再 `AudioPreferences.Apply`，读回 `GodotAudio` 的三条总线
- `IAudio.PlayUi("res://ui-click.wav")` 在 `AudioBus.Ui`（总线名 `UI`）上实际播放一次。音量断言不能代替这次播放
- `GodotTableFiles.FromGodotRes("res://Config")` 经 `FileAccess` 读 `tbitem.bytes`。字节与 `samples/SlimFramework.Demo/Fixtures/tbitem.bytes` 相同，是手写示例，不生成进 `src/SlimFramework`
- 槽位 0 经 `SaveService` 往返，并由另一个 `GodotUserSaveStore` 从 `user://` 读到字节

`dotnet build` 只能证明这些类型编得过。适配器有没有跑，要看 Godot 里这两行输出：`SlimFramework Godot adapter smoke OK`，然后 `SlimFramework DemoSession OK`。无头证明仍是 `dotnet run --project samples/SlimFramework.Demo.Runner`，它不进 Godot。

`SlimBootstrap` 和 `UiPage` 由 `src/SlimFramework.Godot/build/SlimFramework.Godot.targets` 拷到本目录的 `SlimFramework.Scripts/`（不入库）再编译。不要把这两个类型改回适配程序集，否则编辑器 Play 会报 `ScriptTypeBiMap` 重复键。脚本暂存规则见 [docs/extension-rules.md](../../docs/extension-rules.md)。Play 门禁见 [CONTRIBUTING.md](../../CONTRIBUTING.md)。
