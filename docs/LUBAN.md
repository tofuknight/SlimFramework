# Luban

框架程序集带了 Luban 的 C# 运行时：`src/SlimFramework/External/Luban`，命名空间 `Luban`（`ByteBuf`、`BeanBase`）。生成出来的表类 `using Luban` 之后，只要游戏工程引用了 `SlimFramework`，就能编译。

表的 C# 和 `.bytes` **不要**放进 `src/SlimFramework`。一张表示例在 `samples/SlimFramework.Demo`：

- 手写的生成风格代码：`Config/Item.cs`、`Config/TbItem.cs`、`Config/Tables.cs`（命名空间 `cfg`，和 Luban 默认一致）
- bin：`Fixtures/tbitem.bytes`（两行 item：potion、key）
- 加载：`DemoSession` 用 `TableFiles.FromReader` 把字节交给 `new Tables(...)`

这张示例表是按 Luban 4 `cs-bin` 的 ByteBuf 手写的，用来在没有表工程时跑 Runner。正式表用下面锁定的 CLI 生成。

## 游戏里怎么加载

Luban `cs-bin` 生成的 `Tables` 构造函数接收 `Func<string, ByteBuf>`。文件名不带扩展名。

```csharp
using Luban;
using SlimFramework.Config;

var tables = new cfg.Tables(TableFiles.FromDirectory(configDir));
var row = tables.TbItem.Get(1);
```

`configDir` 里要有 `tbitem.bytes`。运行时不调用 Luban CLI。

导出进 PCK 之后，`res://` 往往没有可被 `System.IO.File` 打开的磁盘路径。`TableFiles.FromDirectory` 和 `ProjectSettings.GlobalizePath("res://Config")` 都读不到包里的字节。改用 Godot 适配：

```csharp
using SlimFramework.GodotAdapter;

var tables = new cfg.Tables(GodotTableFiles.FromGodotRes("res://Config"));
```

`FromGodotRes` 走 `FileAccess`，目录必须是 `res://`，文件名仍是 `{表名}.bytes`。

`samples/DemoGame/AdapterSmoke` 对 `res://Config` 调一次 `FromGodotRes`，读的是手写示例 `tbitem.bytes`（与 `samples/SlimFramework.Demo/Fixtures/tbitem.bytes` 相同）。不要把这张表生成进 `src/SlimFramework`。

## 怎么生成

CLI 锁在本仓库，游戏不要再自己挑小版本。

- 压缩包：`third_party/Luban/Luban.7z`（上游 [Luban v4.12.0](https://github.com/focus-creative-games/Luban/releases/tag/v4.12.0) 原件）
- 校验：`third_party/Luban/luban.lock`
- 脚本：`tools/Luban/generate.sh`。核对 sha256，解压到 `third_party/Luban/.extracted/`（不入库），再执行 `dotnet Luban/Luban.dll`

锁的是 Luban 4.12.0，不是 Luban 5。运行时是 Luban 4 的 `cs-bin` `ByteBuf`，跟 5 的生成代码对不上。需要本机有 `dotnet`、`7z`（Debian/Ubuntu：`p7zip-full`），以及 `sha256sum` 或 `shasum`。Windows 用 Git Bash 或 WSL 跑同一条脚本。macOS 没有 `sha256sum` 时脚本改用 `shasum -a 256`。

```bash
tools/Luban/generate.sh \
  -t client \
  -c cs-bin \
  -d bin \
  --conf Data/luban.conf \
  -x outputCodeDir=Game/Scripts/Config \
  -x outputDataDir=Game/Resource/Config
```

以游戏自己的 `luban.conf` 为准。不要把输出目录指到 `src/SlimFramework`。

CI（`.github/workflows/ci.yml`）安装 `p7zip-full` 后无参数执行本脚本，期望退出码 2。仓库里没有 `luban.conf`，示例表仍是手写的，这次调用不会生成表。

无参数调用会先做 sha256（有 `sha256sum` 就用它，否则 `shasum -a 256`，取第一列），需要解压时还要 `7z`。校验和解压都通过后，以退出码 2 结束。pin 与提交范围见 [architecture.md](architecture.md)。

生成后的 `Tables` 会 `loader("表名")`。保证 `outputDataDir` 下的文件名是 `{表名}.bytes`。

升级 Luban 运行时：整组替换 `External/Luban` 下的四个文件，同时换 `third_party/Luban` 里同一代的 CLI 和 `luban.lock`，再跑 `dotnet run --project samples/SlimFramework.Demo.Runner`。Runner 会用 `ByteBuf` 重编码样例并和 `tbitem.bytes` 对比。
