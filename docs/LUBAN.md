# Luban

框架程序集带了 Luban 的 C# 运行时：`src/SlimFramework/External/Luban`，命名空间 `Luban`（`ByteBuf`、`BeanBase`）。生成出来的表类 `using Luban` 之后，只要游戏工程引用了 `SlimFramework`，就能编译。

表的 C# 和 `.bytes` **不要**放进 `src/SlimFramework`。一张表示例在 `samples/SlimFramework.Demo`：

- 手写的生成风格代码：`Config/Item.cs`、`Config/TbItem.cs`、`Config/Tables.cs`（命名空间 `cfg`，和 Luban 默认一致）
- bin：`Fixtures/tbitem.bytes`（两行 item：potion、key）
- 加载：`DemoSession` 用 `TableFiles.FromReader` 把字节交给 `new Tables(...)`

## 游戏里怎么加载

Luban `cs-bin` 生成的 `Tables` 构造函数接收 `Func<string, ByteBuf>`。文件名不带扩展名。

```csharp
using Luban;
using SlimFramework.Config;

var tables = new cfg.Tables(TableFiles.FromDirectory(configDir));
var row = tables.TbItem.Get(1);
```

`configDir` 里要有 `tbitem.bytes`。Godot 导出后可以用 `ProjectSettings.GlobalizePath("res://Config")` 得到磁盘路径，或者自己读 `FileAccess.GetFileAsBytes` 再交给 `TableFiles.FromReader`。运行时不调用 Luban CLI。

## 怎么生成

CLI 的小版本留给项目自己锁。back-night 的 `Game/Scripts/Config` 就是 `cs-bin` 输出，本仓库的运行时按那套 `ByteBuf` 读。典型的 Luban 4 调用（以你仓库里的 `luban.conf` 为准，不要把输出目录指到 `src/SlimFramework`）：

```bash
dotnet Luban.dll \
  -t client \
  -c cs-bin \
  -d bin \
  --conf Data/luban.conf \
  -x outputCodeDir=Game/Scripts/Config \
  -x outputDataDir=Game/Resource/Config
```

生成后的 `Tables` 会 `loader("表名")`。保证 `outputDataDir` 下的文件名是 `{表名}.bytes`。

升级 Luban 运行时：整组替换 `External/Luban` 下的四个文件，再跑 `dotnet run --project samples/SlimFramework.Demo.Runner`。Runner 会用 `ByteBuf` 重编码样例并和 `tbitem.bytes` 对比。
