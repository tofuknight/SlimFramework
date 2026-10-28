# Third-party notices

## Luban

`src/SlimFramework/External/Luban` (`BeanBase.cs`, `ByteBuf.cs`, `ITypeId.cs`, `StringUtil.cs`) is the Luban C# runtime.

- Upstream: https://github.com/focus-creative-games/Luban
- License: MIT
- These copies match the files vendored in https://github.com/tofuknight/back-night (`SlimFramework/External/LubanLib` on `dev`).

The namespace stays `Luban` so Luban-generated table code compiles unchanged.

`third_party/Luban/Luban.7z` is the upstream Luban 4.12.0 CLI archive (MIT), unmodified. The version and checksum are in `third_party/Luban/luban.lock`. It is the code generator, not the runtime.

## Packages (not vendored)

Restored from NuGet, versions in `Directory.Build.props`:

- GodotSharp (Godot Engine C# bindings)
- Google.Protobuf
- Grpc.Tools
