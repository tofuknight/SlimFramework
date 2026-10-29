# Protobuf

两套 proto，编译都走 `Grpc.Tools`（`GrpcServices="None"`，只生成 message）。版本在 `Directory.Build.props`：`Google.Protobuf` 3.34.1、`Grpc.Tools` 2.80.0。不需要手跑 protoc。契约放哪、什么样的格式变更必须走 PR，见 [architecture.md](architecture.md) 和 [../CONTRIBUTING.md](../CONTRIBUTING.md)。

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

`SaveService.Save` 让每个 `ISaveContributor` 交一段字节，写进 `SaveSlot.sections`，再交给 `ISaveStore`。该槽里已有、但当前没有注册 contributor 的 section 会原样写回。`TryPeek` 只读显示名和 section id，不调用 `Restore`。`TryDeleteSlot` 和 `EnumerateOccupiedSlots` 只动存储，不改玩法状态。

`format_version` 小于 1，或高于 `SaveService.FormatVersion`（默认 `DefaultFormatVersion`，也就是 1），会拒绝。拒绝时已有文件不会被覆盖，也不会调用 `Restore`。

低于当前版本时，必须有一条从该版本出发的 `ISaveMigrator`，一步一步升到 `FormatVersion`，然后才 `Restore`。缺任何一步都拒绝，不会把旧信封当成现在的格式。迁移只发生在内存里：`TryLoad` 和 `TryPeek` 不回写文件。下一次 `Save` 才把当前 `FormatVersion` 写回去，并保留迁移后仍然没有 contributor 的 section。高于当前版本的档不能往下降。

```csharp
var save = app.Get<SaveService>();
save.FormatVersion = 2;
save.RegisterMigrator(new V1ToV2());
```

`ISaveMigrator.FromVersion` 是这一步读的版本，`ToVersion` 必须更大，而且 `Migrate` 返回的 `format_version` 必须等于 `ToVersion`。同一个 `FromVersion` 只能登记一个。

Demo 的回归在 `DemoSession`：目标版本设为 2 时，没有迁移器的 v1 槽会被拒绝，文件和玩法状态都不变。注册 `DemoFormatMigrator` 之后能读回玩法段；再保存时写成 version 2，并留下迁移器补上的标记段。`TryPeek` 能看到这个标记段，但不会 `Restore`，也不会改文件。

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
