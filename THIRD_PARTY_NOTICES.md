# Third-party notices

## Luban

`src/SlimFramework/External/Luban` (`BeanBase.cs`, `ByteBuf.cs`, `ITypeId.cs`, `StringUtil.cs`) is the Luban C# runtime.

- Upstream: https://github.com/focus-creative-games/Luban
- License: MIT
- These copies match the files vendored in https://github.com/tofuknight/back-night (`SlimFramework/External/LubanLib` on `dev`).

The namespace stays `Luban` so Luban-generated table code compiles unchanged.

## Packages (not vendored)

Restored from NuGet, versions in `Directory.Build.props`:

- GodotSharp (Godot Engine C# bindings)
- Google.Protobuf
- Grpc.Tools
