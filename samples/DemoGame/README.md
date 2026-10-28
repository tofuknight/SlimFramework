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
| 未设置 / `false` | `dotnet build`、CI | 关掉 SDK 隐式 Godot 包，使用仓库的 `GodotSharp` `$(GodotSharpVersion)`（4.7.2）。配置为 Debug / Release |
| `true` | 编辑器构建传入 `-p:GodotTargetPlatform=<平台>` | SDK 自带的 GodotSharp、Debug 下的 GodotSharpEditor、SourceGenerators。配置为 Debug / ExportDebug / ExportRelease |

```bash
# 与 CI 相同
dotnet build ../../SlimFramework.sln

# 与编辑器相同（linuxbsd / windows / macos）
dotnet build SlimFramework.DemoGame.csproj -p:GodotTargetPlatform=linuxbsd

# 或者直接打开开关；强制走无编辑器路径则设为 false
dotnet build SlimFramework.DemoGame.csproj -p:SlimDemoGameEditorBuild=true
dotnet build SlimFramework.DemoGame.csproj -p:SlimDemoGameEditorBuild=false
```

两种路径都输出到 `.godot/mono/temp/bin/`。SDK 在还原 NuGet 之前就改了这个目录，编辑器也从这里加载 `SlimFramework.DemoGame.dll`。
