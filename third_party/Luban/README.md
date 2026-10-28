# Luban CLI

这里是锁定的 Luban **CLI**，不是 C# 运行时。运行时在 `src/SlimFramework/External/Luban`。

- `Luban.7z`：上游 [v4.12.0](https://github.com/focus-creative-games/Luban/releases/tag/v4.12.0) 的发布包，未改内容。
- `luban.lock`：版本和 sha256。`tools/Luban/generate.sh` 解压前会核对。
- 解压目录 `.extracted/` 不入库。

用法见 [docs/LUBAN.md](../../docs/LUBAN.md)。
