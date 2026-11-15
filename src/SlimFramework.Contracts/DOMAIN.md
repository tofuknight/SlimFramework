# Contracts

`src/SlimFramework.Contracts/` 的写作约束。放哪、什么样的格式变更必须走 PR，见 [docs/architecture.md](../../docs/architecture.md) 和 [docs/PROTOBUF.md](../../docs/PROTOBUF.md)。本文不是把玩法类型收进框架契约的许可。

## 范围

程序集 `SlimFramework.Contracts`，工程 `src/SlimFramework.Contracts/SlimFramework.Contracts.csproj`。框架 protobuf 只有存档信封和资源 ref：

| 文件 | message | C# 命名空间 |
| --- | --- | --- |
| `Protos/save_slot.proto` | `SaveSlot` | `SlimFramework.Contracts.Save.V1` |
| `Protos/resource_ref.proto` | `ResourceRef`、`ResourceRefCatalog` | `SlimFramework.Contracts.Resource.V1` |

生成走工程里的 `Grpc.Tools`，`GrpcServices="None"`，只生成 message。不手跑 protoc。版本在根 `Directory.Build.props`。

## 能依赖 / 不能依赖

可以引用 `Google.Protobuf`。

禁止引用 `GodotSharp`、`SlimFramework`、`SlimFramework.Godot`、`plugins/`、`samples/`、任何游戏。`GuardContractsHaveNoGodot` 拦住 `GodotSharp`。`src/Directory.Build.targets` 的 `GuardFrameworkDoesNotReferenceGame` 同样适用。依赖只能向下：本程序集处在最底，不能回头引用框架实现或游戏。

## 可扩展的公开 API

不要新增第三份框架 proto，不要把玩法 message 加进 `SlimFramework.Contracts.csproj`。

玩法 message（样例 `DemoProfile`）放游戏工程，或 `samples/SlimFramework.Demo/Protos`。游戏 csproj 自己引用 `Google.Protobuf` 和 `Grpc.Tools`。这些 message 作为 `ISaveContributor` 的一段字节进入 `SaveSlot.sections`。本程序集看不到那些类型。

`SaveSlot` 现有字段只有 `format_version = 1`、`display_name = 2`、`sections = 3`。禁止把玩法字段加进这个 message。若线格式必须扩展，新字段使用新的 field number，并视同存档格式变更走 PR。已有 field number 不能改作他用。

`ResourceRef` 的字段是 `ref_key`、`constant`、`uid`、`path`。`ResourceRefCatalog.refs` 的 key 必须与 `ref_key` 一致。key 怎么分配由游戏或工具决定。本框架不生成这张表。

## 不变量

- 框架认识的契约只有 `SaveSlot` 和 `ResourceRefCatalog`。
- 游戏 message 不进入本程序集。框架代码不解析玩法字节。
- `format_version`、迁移、section 语义的变更视同存档格式变更，禁止直接 push `main`。
- 本程序集禁止引用游戏，也禁止引用 Godot。
- 公开类型保持英文 PascalCase。注释和文档用中文。proto 字段名保持现有的 snake_case。

## 怎么验证

仓库没有单独的契约测试项目。

```bash
dotnet build SlimFramework.sln -c Release
dotnet run --project samples/SlimFramework.Demo.Runner
dotnet run --project tools/Slim.SaveTool -- pack /tmp/slot.sav night-1
dotnet run --project tools/Slim.SaveTool -- unpack /tmp/slot.sav
```

Runner 做一次 protobuf 存档往返。`Slim.SaveTool` 只读写 `SaveSlot` 信封，不解析玩法段。

## 常见写错

- 把 `DemoProfile` 或任何玩法 proto 加进本 csproj。
- 给 `SaveSlot` 加金币、关卡、背包之类的字段。
- 引用 `GodotSharp`、框架实现或游戏工程。
- 改已有 field number。
- 没走存档格式 PR 就改 `format_version` 或 section 的含义。
- 手跑 protoc，或把 `GrpcServices` 改成生成 service。
