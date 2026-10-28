# Protobuf

两套 proto，编译都走 `Grpc.Tools`（`GrpcServices="None"`，只生成 message）。版本在 `Directory.Build.props`：`Google.Protobuf` 3.34.1、`Grpc.Tools` 2.80.0。不需要手跑 protoc。

## 框架契约

`src/SlimFramework.Contracts/Protos`

| 文件 | message | 谁用 |
| --- | --- | --- |
| `save_slot.proto` | `SaveSlot` | `SaveService` 的槽位信封 |
| `resource_ref.proto` | `ResourceRefCatalog` | `ResourceCatalog.Parse` |

`SaveSlot` 只有 `format_version`、`display_name`、`sections`（`map<string, bytes>`）。玩法字段不要加进这个 message。

## 游戏自己的 proto

放在游戏工程里，例如 Demo 的 `samples/SlimFramework.Demo/Protos/demo_profile.proto`。游戏 csproj 自己引用 `Google.Protobuf` + `Grpc.Tools`，`Protobuf Include` 指向自己的目录。不要把游戏 proto 加进 `SlimFramework.Contracts.csproj`。

## 存档怎么接

`SaveService.Save` 让每个 `ISaveContributor` 交一段字节，写进 `SaveSlot.sections`，再交给 `ISaveStore`。该槽里已有、但当前没有注册 contributor 的 section 会原样写回。`format_version` 小于 1，或高于 `SaveService.DefaultFormatVersion`，在有迁移器之前会拒绝，已有文件不会被覆盖。`TryPeek` 只读显示名和 section id，不调用 `Restore`。`TryDeleteSlot` 和 `EnumerateOccupiedSlots` 只动存储，不改玩法状态。

```csharp
public sealed class ProfileContributor : ISaveContributor
{
    public string SectionId => "demo.profile";

    public byte[] Capture() => new DemoProfile
    {
        PlayerName = _state.PlayerName,
        Coins = _state.Coins,
    }.ToByteArray();

    public void Restore(byte[] payload)
    {
        if (payload.Length == 0)
        {
            _state.Coins = 0;
            return;
        }

        var message = DemoProfile.Parser.ParseFrom(payload);
        _state.Coins = message.Coins;
    }
}
```

框架看不到 `DemoProfile`。缺段时 `Restore` 收到空数组。

无 Godot 时用 `MemorySaveStore`。进了 Godot，`SlimBootstrap` 使用 `GodotUserSaveStore`，路径是 `user://saves/slot_000.sav`。

只看信封、不加载游戏的工具是 `tools/Slim.SaveTool`：

```bash
dotnet run --project tools/Slim.SaveTool -- pack slot.sav "chapter-1"
dotnet run --project tools/Slim.SaveTool -- unpack slot.sav
```

## 资源 ref

工具或游戏把 `ResourceRefCatalog` 写成字节（字段：`ref_key`、`constant`、`uid`、`path`）。运行时：

```csharp
var catalog = ResourceCatalog.Parse(bytes);
if (catalog.TryGet(key, out var entry))
    loader.Load<PackedScene>(entry);
```

`ResourceRefEntry.LoadKey` 在 Uid 非空时用 Uid，否则用 Path。key 怎么分配由游戏决定。本框架不生成这张表。Uid 加载失败后再回退 Path 仍不做。`GodotAssetLoader` 同时接受 `res://` 和 `uid://`。
