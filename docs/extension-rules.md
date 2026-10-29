# 扩展门禁

改插件、延后功能、Godot 脚本暂存、向上依赖时，用本文挡。分层和 Guard 的表见 [architecture.md](architecture.md)。合入条件见 [../CONTRIBUTING.md](../CONTRIBUTING.md)。

## 插件

核心保持瘦。可选能力进 `plugins/`，由游戏 `SlimApp.Use`，不写进 `SlimBootstrap.Configure` 的默认列表。

现在 `plugins/` 只有 `README.md` 和 `Directory.Build.targets`。还没有插件工程。脚手架规格在下面，落地状态见 [gap-backlog.md](gap-backlog.md)。

### 边界

插件可以：

- 放在 `plugins/<Name>/`，作为独立类库加入 `SlimFramework.sln`。
- 实现 `IPlugin`（`Id`、`Register(SlimApp)`）。
- `ProjectReference` `SlimFramework`。需要引擎类型时再引用 `SlimFramework.Godot`。
- 在 `Register` 里 `app.Add` 模块，或向已经 `Add` 的服务登记扩展（例如 `SaveService.Register`）。

插件禁止：

- 引用 `samples/`、`DemoGame`，或任何游戏程序集、关卡、剧情。`GuardPluginDoesNotReferenceGame` 会拦住路径里带 `sample` / `demogame` 的 `ProjectReference`。其余情况审阅拒绝。
- 让 `src/SlimFramework` 或 `src/SlimFramework.Godot` 反过来 `ProjectReference` 插件。
- 为了少写一个插件工程，把功能直接加进核心或适配默认启动路径。
- 在 `Register` 里依赖游戏类型。玩法类型留在游戏程序集。

插件需要核心尚不存在的钩子时：钩子是框架 PR，默认行为与未挂插件时一致；功能本体仍在 `plugins/`。禁止把插件实现移进 `src/` 来「省事」。

需要 Godot 的插件只能由 `samples/DemoGame` 或真实游戏 `Use`。`samples/SlimFramework.Demo` 保持无 Godot，禁止引用这种插件。

### 脚手架

第一个插件 PR 按这个形状加，不要另起目录约定：

```
plugins/<Name>/<Name>.csproj
plugins/<Name>/<Name>Plugin.cs
```

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\SlimFramework\SlimFramework.csproj" />
  </ItemGroup>
</Project>
```

只有插件自己的代码引用 Godot 类型时，才加上对 `src/SlimFramework.Godot/SlimFramework.Godot.csproj` 的 `ProjectReference`。

```csharp
public sealed class ExamplePlugin : IPlugin
{
    public string Id => "example";

    public void Register(SlimApp app)
    {
    }
}
```

游戏侧只在自己的 `Configure` 里 `app.Use`。不要为了某个插件去改 `base.Configure`。那里现有的默认服务是 `GodotAssetLoader`、`SaveService`、`PreferenceStore`、`FlowMachine`、`GodotAudio`、`GodotUiStack`。增减默认服务是框架 PR，不是插件附带改动。

## 延后功能

这些已经点名，不在核心里。要做就做插件，一次一个 PR：

| 功能 | 落点 |
| --- | --- |
| 异步加载、分帧卸载 | 插件。禁止扩成资源管线或 DI |
| BGM 交叉淡化 | 插件。`IAudio` 现有的 `PlayMusic` / `StopMusic` 保持直接切换，除非钩子 PR 写明未挂插件时行为不变 |
| 点外部关闭 | 插件。优先作为脚手架的那一个例子 |
| 更完整的 UI 生命周期 | 插件。核心侧页面生命周期只有 `IUiPage.OnOpen` / `OnClose` |

明确不进仓库的膨胀项见 [gap-backlog.md](gap-backlog.md)。

## Godot 脚本暂存

Godot 只认游戏工程目录下的 `res://`。继承 `Node` / `Control`、而且编辑器要挂到场景上或被场景脚本继承的类型，必须同时满足：

1. 源文件留在 `src/SlimFramework.Godot`，命名空间保持 `SlimFramework.GodotAdapter`。这里是唯一事实来源。
2. 适配 csproj `Compile Remove` 该文件，类型不进 `SlimFramework.Godot.dll`。
3. 游戏 csproj `Import` `src/SlimFramework.Godot/build/SlimFramework.Godot.targets`。
4. targets 在 `BeforeCompile` 把源文件拷到游戏工程的 `SlimFramework.Scripts/*.cs` 再编译。每次构建覆盖。拷贝结果不入库。

`samples/DemoGame` 已经 `Import`。`.gitignore` 忽略 `samples/DemoGame/SlimFramework.Scripts/`。接到别的游戏时，游戏自己的 gitignore 要忽略它的 `SlimFramework.Scripts/`。子模块若在游戏目录里面，游戏 csproj 还要 `Compile Remove="third_party/**"`，否则 Godot.NET.Sdk 会把子模块里的 `.cs` 再编一遍。见根 [README.md](../README.md)。

当前暂存：

| 源（事实来源） | 适配 csproj | 游戏工程里的编译副本 |
| --- | --- | --- |
| `Bootstrap/SlimBootstrap.cs` | `Compile Remove` | `SlimFramework.Scripts/SlimBootstrap.cs` |
| `UI/UiPage.cs` | `Compile Remove` | `SlimFramework.Scripts/UiPage.cs` |

禁止把这两个类型编回适配程序集。先例：只留在适配 DLL、没有单独的 `res://` 路径时，编辑器 Play 会 `ScriptTypeBiMap` 重复登记（`SlimBootstrap`、`UiPage`；修复在把脚本改到游戏工程内编译的那次变更）。不 `Import` targets 则两边都不编，游戏会缺类型。两种改法都不合。

新增一个要挂场景的类型时，同一 PR 里做完：源文件、`Compile Remove`、targets 里一条 `SlimFrameworkGodotScript`（`StagedPath` 指向 `SlimFramework.Scripts/<Type>.cs`）。不要提交 `SlimFramework.Scripts/` 下的文件。

`NodeSingleton<T>`（`Singleton/NodeSingleton.cs`）是场景可继承的 `Node` 基类，今天仍编进适配 DLL，仓库里没有场景继承它。在任何游戏脚本或 `.tscn` 继承它之前，必须按上面四步移出适配程序集。禁止新增「只编译进适配 DLL、没有 `res://`」的同名编辑器类型。以后任何场景可继承基类走同一条规则。

`IUiPage` 留在适配程序集。它不继承 `Node` / `Control`，不是编辑器脚本。`GodotUiStack` 只通过 `is IUiPage` 调用 `OnOpen` / `OnClose`，禁止改成依赖具体类 `UiPage`。新的页面回调加在 `IUiPage` 上。`UiPage` 是给场景继承的 `Control`，继续只在游戏工程里编译。

## 向上依赖

Framework、Plugins、Tools 禁止引用 Game（`DemoGame` 或任何游戏工程）。游戏只向下引用。

机械检查是三份 `Directory.Build.targets`，加上核心 / 契约 / Demo 的 GodotSharp Guard。覆盖范围和盖不住的洞见 [architecture.md](architecture.md)。审阅按「禁止」执行，不按「Guard 会不会响」执行。
