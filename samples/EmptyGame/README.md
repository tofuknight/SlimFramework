# 新游戏空壳

本仓库的产品是库。`samples/DemoGame` 只冒烟 Godot 适配，不放关卡、战斗、背包、剧情。新游戏是**单独的仓库**。把本目录拷出去当起点，再把 SlimFramework 加为子模块。

本目录不在 `SlimFramework.sln` 里。不要在框架仓库里打开 `project.godot`，也不要 `dotnet build` 这里的 `Game.csproj`。`third_party/SlimFramework` 只在拷出去的游戏仓库里存在。

步骤正文仍以根 [README.md](../../README.md)「接到自己的游戏」为准。下面是按那段拼好的空壳，外加拷走时要勾的清单。文中的相对链接在框架仓库里有效；拷走之后以本页正文为准。

## 清单

- [ ] 新建空的游戏仓库。把本目录里的文件放到那个仓库的根（包含 `project.godot`、`Game.csproj`、`Game.sln`、`Game/`、`.gitignore`）。本 README 可以留下当第一次的说明，也可以换成游戏自己的文档。
- [ ] 在游戏仓库根执行 `git submodule add https://github.com/tofuknight/SlimFramework.git third_party/SlimFramework`。框架仓库根本身不要当 Godot 工程打开。
- [ ] 用 Godot **4.7.2 .NET** 打开游戏仓库根的 `project.godot`。需要 .NET SDK 8。
- [ ] `Game.csproj` 保持 `Sdk="Godot.NET.Sdk/4.7.2"`。有这三行，且路径指向子模块：`Import` `SlimFramework.Godot.targets`、`ProjectReference` `SlimFramework.Godot.csproj`、`Compile Remove="third_party/**"`。少了 `Import`，编辑器 Play 会把 `SlimBootstrap` 登记两次。少了 `Compile Remove`，子模块躺在游戏目录里时，Godot.NET.Sdk 会把框架的 `.cs` 再编一遍。
- [ ] `.gitignore` 忽略 `SlimFramework.Scripts/`。那是构建时从适配层源文件拷出来的，不入库。
- [ ] `Game/GameRoot.cs` 继承 `SlimBootstrap`。`Configure` 里先 `base.Configure`，再 `Use` 自己的插件。`base.Configure` 会挂上 `GodotAssetLoader`、`SaveService`、`PreferenceStore`、`FlowMachine`、`GodotAudio`、`GodotUiStack`。
- [ ] 玩法、表、玩法 proto 放在这个游戏仓库里。框架认识的 protobuf 只有 `SaveSlot` 和 `ResourceRefCatalog`。

## 空壳里有什么

| 文件 | 作用 |
| --- | --- |
| `project.godot` | 主场景 `res://Game/Main.tscn`。程序集名 `Game`，对应 `Game.csproj` |
| `Game.sln` | 让编辑器认到这份工程。缺了它，编辑器会另存 csproj |
| `Game.csproj` | 只向下引用子模块里的 `SlimFramework.Godot` |
| `Game/GameRoot.cs` | 入口。没有插件，没有关卡 |
| `Game/Main.tscn` | 一个挂了 `GameRoot` 的节点 |
| `.gitignore` | 忽略 `SlimFramework.Scripts/`、`.godot/`、`.import/` |

没有 Autoload。入口就是主场景上的节点。不要再挂一份同名 Autoload，否则 `GameRoot` 会进树两次。

## 不要做

- 不把关卡、战斗、剧情、正式 UI 皮肤写进 `samples/DemoGame` 或 `src/`。`DemoGame` 的 `AdapterSmoke` 只证明适配层。
- 不从 `src/`、`plugins/`、`tools/` `ProjectReference` 本目录或游戏仓库。游戏只向下引用框架。
- 不做 DI、Manager 集合、MVVM、Command / Query。见 [docs/gap-backlog.md](../../docs/gap-backlog.md)「明确不做」。
- 不把 `SlimBootstrap`、`UiPage` 编回适配程序集。暂存规则见 [docs/extension-rules.md](../../docs/extension-rules.md)。
- 不继承 `NodeSingleton<T>`。它还编在适配 DLL 里，仓库里没有场景继承它。真要继承时，同一 PR 里按扩展规则暂存，不要先挂场景。
- 不把 `samples/SlimFramework.Demo` 引用进游戏。那是无头用法样例，不是第二款游戏。
- 不为了这个空壳去改 `DemoGame` 的双 SDK 开关。那个开关只服务本仓库的无编辑器 CI。你的游戏用编辑器打开，保持上面的 Sdk 即可。

可选：点外部关闭在 `plugins/ClickOutsideClose`。需要时再 `ProjectReference` `third_party/SlimFramework/plugins/ClickOutsideClose/ClickOutsideClose.csproj`，并在 `base.Configure` 之后 `Use`。`SlimBootstrap.Configure` 不 `Use` 它。空壳不预置这个引用。
