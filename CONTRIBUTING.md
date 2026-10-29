# 贡献

产品是库。游戏代码留在游戏工程。本文是合入 `main` 的门禁；分层、插件、脚本暂存、缺口的细则在：

- [docs/architecture.md](docs/architecture.md) — 程序集分层、`DemoGame` 双 SDK、Luban / Protobuf 放哪
- [docs/extension-rules.md](docs/extension-rules.md) — 插件、延后功能、Godot 脚本暂存、禁止向上依赖
- [docs/gap-backlog.md](docs/gap-backlog.md) — 现在补的缺口，以及明确不做的东西
- [docs/LUBAN.md](docs/LUBAN.md)、[docs/PROTOBUF.md](docs/PROTOBUF.md) — 生成步骤和契约字段

代码和这些文档不一致：不合。服从文档，或在同一次 PR 里改文档并走对应审阅。

## 公开 API

程序集外可见的类型、成员、参数用英文 PascalCase。中文只出现在注释和文档。

## 什么必须走 PR

| 改动 | 门禁 |
| --- | --- |
| 文档，或不改变行为的极小改动 | 走 PR，方便看见。不直接 push `main`。 |
| 公开 API、程序集分层、Godot 适配、存档格式、Luban pin（`third_party/Luban`） | 必须 PR。未经审阅禁止直接 push `main`。Forge 架构审阅通过后才合。 |
| 动到 Godot 适配、脚本暂存、`samples/DemoGame` 的场景或冒烟 | 上面两条之外，再加本地 AdapterSmoke。 |

放宽本文、[docs/architecture.md](docs/architecture.md) 或 [docs/extension-rules.md](docs/extension-rules.md) 里的禁止项，视同改分层政策，走 Forge 架构审阅。只补充缺口说明、不放宽禁止项的，走 PR，并在描述里写明没有改门禁。

## 合入 main

三条都要满足。CI 不能代替后两条。

1. CI 绿。`.github/workflows/ci.yml` 在 `ubuntu-latest` 上执行 `dotnet build SlimFramework.sln -c Release`，然后 `dotnet run --project samples/SlimFramework.Demo.Runner`。SDK 见 `global.json`。
2. 动了框架或分层：Forge 架构审阅通过。
3. 动了 Godot：本地用 Godot 4.7.2 .NET 打开 `samples/DemoGame/project.godot`（不要打开仓库根），Play `Main.tscn`。必须打出这两行，且没有 `ScriptTypeBiMap` 重复登记：
   - `SlimFramework Godot adapter smoke OK`
   - `SlimFramework DemoSession OK`

PR 描述写明第 2 条和第 3 条的结果。没动框架或分层，写「未改框架 / 分层，跳过 Forge 架构审阅」。没动 Godot，写「未触碰 Godot，跳过 AdapterSmoke」。

审阅清单：

- [ ] CI 绿
- [ ] 框架 / 分层 / 规则文档：Forge 架构审阅通过，或写明跳过原因
- [ ] 动了 Godot：Play `Main.tscn` 两行 OK，无 `ScriptTypeBiMap` 重复登记，或写明跳过原因
- [ ] `src/`、`plugins/`、`tools/` 没有新增指向游戏或 `samples/` 的 `ProjectReference`
- [ ] 新的、要挂到场景上的 `Node` / `Control` 类型已按 [docs/extension-rules.md](docs/extension-rules.md) 暂存，没有只留在适配 DLL 里

## 本地检查

与 CI 相同：

```bash
dotnet build SlimFramework.sln -c Release
dotnet run --project samples/SlimFramework.Demo.Runner
```

`dotnet build` 不需要安装 Godot。无头 Runner 不进编辑器，也不能代替 AdapterSmoke。

Luban：`tools/Luban/generate.sh`。CI 当前不跑它。约定和原因见 [docs/architecture.md](docs/architecture.md)。

## 向上依赖的机械检查

`src/Directory.Build.targets`、`plugins/Directory.Build.targets`、`tools/Directory.Build.targets` 在 `AssignProjectConfiguration` 之前扫描 `ProjectReference`。路径转小写后含 `sample` 或 `demogame` 就失败。

这只拦住路径里带这两个子串的引用。游戏工程若改名，或放到不含这两个子串的目录，检查不会响。那种引用仍然禁止。要引入新的游戏路径，先改这三份 targets，再加引用。

禁止在 `src/`、`plugins/`、`tools/` 的子目录另放 `Directory.Build.targets`。MSBuild 找到最近的一份就停止向上查找，父级 Guard 会失效。新的框架、插件、工具工程放在这三棵树下。

Target 名字、GodotSharp Guard、检查盖不住的洞，见 [docs/architecture.md](docs/architecture.md)。
