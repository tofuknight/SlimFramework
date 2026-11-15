# Tools

`tools/` 的写作约束。Luban 的 pin、运行时和生成步骤以 [docs/LUBAN.md](../docs/LUBAN.md) 为准，分层见 [docs/architecture.md](../docs/architecture.md)。本文不授权把表生成进框架。

## 范围

- `tools/Slim.SaveTool/`：程序集 `Slim.SaveTool`，命名空间 `SlimFramework.Tools.SaveTool`。只读写 `SaveSlot` 信封。
- `tools/Luban/generate.sh`：核对并调用锁在本仓库的 Luban CLI。压缩包和 `luban.lock` 在 `third_party/Luban`（当前 4.12.0），不在 `tools/`。解压目录 `third_party/Luban/.extracted/` 不入库。

本仓库没有 `luban.conf`。表类和 `.bytes` 留在游戏工程。样例表是手写的，在 `samples/SlimFramework.Demo`，不由本脚本更新。

## 能依赖 / 不能依赖

`Slim.SaveTool` 只引用 `SlimFramework.Contracts`。

新的改字节 CLI 放 `tools/`，引用 `SlimFramework.Contracts`。不需要 Godot 时才可以再引用 `SlimFramework`。禁止引用 `SlimFramework.Godot`、`plugins/`、`samples/`、任何游戏。`tools/Directory.Build.targets` 的 `GuardToolDoesNotReferenceGame` 拦住路径里带 `sample` / `demogame` 的 `ProjectReference`。它不看适配层路径；引用 `SlimFramework.Godot` 仍然禁止。

禁止把 `outputCodeDir` / `outputDataDir` 指到 `src/SlimFramework`。游戏不得自带另一份 Luban CLI。

## 可扩展的公开 API

`Slim.SaveTool` 现有命令只有：

- `pack <file> <displayName>`：写出 `format_version = 1` 的 `SaveSlot`，不写玩法 section
- `unpack <file>`：打印 `format_version`、`display_name`、section 数量

不要在这个工具里解析玩法 message。它不引用 `SlimFramework` 的实现。

`generate.sh` 把参数原样传给 `Luban.dll`。无参数且校验、解压都通过时，退出码是 2。不要在这里加第二套生成器，也不要换成 Luban 5。升级 pin 必须和 `luban.lock`、`External/Luban` 的四个运行时文件、以及 Runner 放在同一个 PR。锁的是 Luban 4，不是 Luban 5。

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
```

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
- 无参数跑 `generate.sh` 时期望退出码 0。
- 只改运行时里的一两个文件，不同时换同一代 CLI 和 `luban.lock`。
