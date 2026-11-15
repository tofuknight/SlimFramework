# Tools

`tools/` 的写作约束。Luban 的 pin、运行时和生成步骤以 [docs/LUBAN.md](../docs/LUBAN.md) 为准，分层见 [docs/architecture.md](../docs/architecture.md)。本文不授权把表生成进框架。

## 范围

- `tools/Slim.SaveTool/`：程序集 `Slim.SaveTool`，命名空间 `SlimFramework.Tools.SaveTool`。只读写 `SaveSlot` 信封。
- `tools/Slim.AssetCatalogTool/`：程序集 `Slim.AssetCatalogTool`，命名空间 `SlimFramework.Tools.AssetCatalogTool`。读游戏自己的资源清单，写出 `ResourceRefCatalog` 字节和 DisplayKey 常量类。
- `tools/Luban/generate.sh`：核对并调用锁在本仓库的 Luban CLI。压缩包和 `luban.lock` 在 `third_party/Luban`（当前 4.12.0），不在 `tools/`。解压目录 `third_party/Luban/.extracted/` 不入库。

本仓库没有 `luban.conf`。表类和 `.bytes` 留在游戏工程。样例表是手写的，在 `samples/SlimFramework.Demo`，不由本脚本更新。

## 能依赖 / 不能依赖

`Slim.SaveTool` 只引用 `SlimFramework.Contracts`。

`Slim.AssetCatalogTool` 引用 `SlimFramework.Contracts` 和 `SlimFramework`。引用框架是为了调用 `ResourceCatalog.Parse`，让清单校验和运行时是同一套规则。它不引用 Godot 适配：`--project` 只读工程目录里的文件。

新的改字节 CLI 放 `tools/`，引用 `SlimFramework.Contracts`。不需要 Godot 时才可以再引用 `SlimFramework`。禁止引用 `SlimFramework.Godot`、`plugins/`、`samples/`、任何游戏。`tools/Directory.Build.targets` 的 `GuardToolDoesNotReferenceGame` 拦住路径里带 `sample` / `demogame` 的 `ProjectReference`。它不看适配层路径；引用 `SlimFramework.Godot` 仍然禁止。

禁止把 `outputCodeDir` / `outputDataDir` 指到 `src/SlimFramework`。游戏不得自带另一份 Luban CLI。

## 可扩展的公开 API

`Slim.SaveTool` 现有命令只有：

- `pack <file> <displayName>`：写出 `format_version = 1` 的 `SaveSlot`，不写玩法 section
- `unpack <file>`：打印 `format_version`、`display_name`、section 数量

不要在这个工具里解析玩法 message。它不引用 `SlimFramework` 的实现。

`Slim.AssetCatalogTool` 的命令：

- `generate <manifest.json> --catalog <file.bytes> --code <file.cs> --namespace <Namespace> --class <ClassName> [--project <godot-dir>]`
- `check <manifest.json> --project <godot-dir>`

清单是 JSON 数组。每项字段是 `ref_key`、`constant`、`uid`、`path`。`constant` 是 DisplayKey。空 `constant` 写入目录，不生成 C# 常量。输出按 `ref_key` 升序，换行是 LF，UTF-8 无 BOM。同一份清单两次生成，字节和源码都相同。

校验与 `ResourceCatalog.Parse` 相同：map key 就是 `ref_key`（重复的 `ref_key` 在读 JSON 时拒绝）、非空 `constant` 不重复、`constant` 不能有首尾空白、`uid` 和 `path` 至少一个非空白。除此之外，非空 `constant` 必须是 C# 标识符，才能写进常量类。`Parse` 不检查标识符。

`--project` 和 `check` 只读 Godot 工程目录，不启动编辑器。`res://` 文件必须存在。清单中的 uid 必须与同路径的 `.uid`、`.import` 里的 `uid=`，或 `.tscn` / `.tres` 文本头里的 `uid` 一致。同一个文件上有多处 uid 时，这几处必须相同。只有 uid、没有 path 时，在工程里按这些位置查找，且只能命中一个文件。

这个工具不是 Luban，也不读取 `luban.conf`。

`generate.sh` 把参数原样传给 `Luban.dll`。无参数且校验、解压都通过时，退出码是 2。不要在这里加第二套 Luban，也不要换成 Luban 5。升级 pin 必须和 `luban.lock`、`External/Luban` 的四个运行时文件、以及 Runner 放在同一个 PR。锁的是 Luban 4，不是 Luban 5。

## 不变量

- 工具禁止引用游戏，也禁止引用 Godot 适配层。
- 入库的是 `Luban.7z`、`luban.lock`、`generate.sh` 和运行时源文件。不提交 `.extracted/`。
- `generate.sh` 不会更新示例表，也不会把生成物写进核心。
- 运行时仍在 `src/SlimFramework/External/Luban`，命名空间 `Luban`。改读格式不是本目录的日常改动。
- 新的命令名和类型用英文 PascalCase。注释和文档用中文。
- 不要在 `tools/` 的子目录再放 `Directory.Build.targets`，否则父级 Guard 失效。

## 怎么验证

```bash
dotnet build SlimFramework.sln -c Release
dotnet run --project tools/Slim.SaveTool -- pack /tmp/slot.sav night-1
dotnet run --project tools/Slim.SaveTool -- unpack /tmp/slot.sav
dotnet run --project tools/Slim.AssetCatalogTool -- generate samples/DemoGame/Assets/resource_refs.json \
  --catalog /tmp/resource_refs.bytes \
  --code /tmp/AssetKeys.cs \
  --namespace SlimFramework.DemoGame \
  --class AssetKeys \
  --project samples/DemoGame
```

示例清单归 `samples/DemoGame`。工具通过路径读取它，工程引用里没有 `samples/`。生成结果应与已提交的 `resource_refs.bytes` 和 `AssetKeys.cs` 一致。

与 CI 相同，安装 `p7zip-full` 后无参数执行（需要 `sha256sum` 或 `shasum`，以及 `7z`），期望退出码 2：

```bash
tools/Luban/generate.sh
```

这次调用核对压缩包并打印用法，不生成表。仓库没有 `dotnet test`。升级运行时或 pin 时再跑 `dotnet run --project samples/SlimFramework.Demo.Runner`。

## 常见写错

- 让工具引用 `samples/`、游戏工程或 `SlimFramework.Godot`。
- 在 `Slim.SaveTool` 里解析 `DemoProfile` 或其他玩法段。
- 把生成的表写进 `src/SlimFramework`，或在本仓库加一份 `luban.conf` 当作框架表工程。
- 另带一份 Luban CLI，或把 pin 升到 Luban 5。
- 让 `Slim.AssetCatalogTool` 引用 `SlimFramework.Godot` 或启动 Godot 编辑器来核对 uid。
- 把资源清单或生成的常量类写进 `src/SlimFramework`。
- 无参数跑 `generate.sh` 时期望退出码 0。
- 只改运行时里的一两个文件，不同时换同一代 CLI 和 `luban.lock`。
