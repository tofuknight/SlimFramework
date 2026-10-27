# Architecture

SlimFramework is the library. A game is a separate assembly that references it.

## Assemblies

| Assembly | Project | May reference | Must not reference |
| --- | --- | --- | --- |
| `SlimFramework.Contracts` | `src/SlimFramework.Contracts` | Google.Protobuf | Godot, game, demo |
| `SlimFramework` | `src/SlimFramework` | Contracts, Luban sources compiled in | GodotSharp, game, demo |
| `SlimFramework.Godot` | `src/SlimFramework.Godot` | SlimFramework, GodotSharp | game, demo |
| `SlimFramework.Demo` | `samples/SlimFramework.Demo` | SlimFramework, Contracts | GodotSharp, SlimFramework.Godot |
| `SlimFramework.DemoGame` | `samples/DemoGame` | Godot adapter, Demo | — |
| `Slim.SaveTool` | `tools/Slim.SaveTool` | Contracts | Demo, game, Godot adapter |

`src/Directory.Build.targets`, `tools/Directory.Build.targets`, and `plugins/Directory.Build.targets` fail the build if a project under those trees references `samples` or `DemoGame`. The demo csproj fails if it references Godot.

## Runtime shape

`SlimApp` is the composition root.

1. `Add` modules before `Init`. Order is init order; shutdown is reversed.
2. `Use` plugins any time before `Init` (plugins may `Get` services already added).
3. `Init` calls `IModule.Init(SlimApp)`.
4. `TickFrame` runs every `ITickable` module. `SlimBootstrap._Process` is the Godot driver.
5. `SlimApp.Current` is set only by `SlimApp.Mount` (the bootstrap does this after `Init`). Headless code should not depend on it.

One instance per concrete type and per framework interface (`IAudio`, `IUiStack`, `IAssetLoader`, …). `IModule` / `ITickable` are not service keys.

Modules are main-thread. There is no lock.

## Where to put a change

| Task | Location |
| --- | --- |
| New engine-agnostic service | `src/SlimFramework/<Area>/`, implement `IModule`, register from `SlimBootstrap.Configure` or the game plugin |
| New Godot-only behavior | `src/SlimFramework.Godot`, behind an interface in the core if games should call it |
| Shared wire format | `src/SlimFramework.Contracts/Protos`, `GrpcServices="None"` |
| Game feature, table, proto | the game assembly, or `samples/SlimFramework.Demo` as the pattern |
| CLI that edits bytes | `tools/`, reference Contracts (or SlimFramework if it needs no Godot) |
| Optional add-on | `plugins/`, implement `IPlugin` |

## Public types worth knowing

- `SlimApp`, `IModule`, `ITickable`, `IPlugin`, `TickList`
- `EventBus`, `IEvent` — payload is the subscription key (`FlowChanged` is the built-in one)
- `FlowMachine`, `IFlowState`, `IPauseSink`
- `SaveService`, `ISaveContributor`, `ISaveStore`, `MemorySaveStore`
- `PreferenceStore`, `ITextStore`
- `ResourceCatalog`, `IAssetLoader`
- `TableFiles` — adapts a byte reader to Luban `Func<string, ByteBuf>`
- `IAudio`, `IUiStack`, `UiLayer`, `AudioBus`
- Godot: `SlimBootstrap`, `GodotAssetLoader`, `GodotAudio`, `GodotUiStack`, `UiPage`, `NodeSingleton<T>`, `GodotUserSaveStore`, `GodotUserTextStore`, `GodotPauseSink`

## Not in this tree on purpose

- QFramework-style command / query / architecture containers
- Reflection singletons as the way to reach services (`NodeSingleton<T>` remains for nodes that must live in the tree)
- Async resource loads, music crossfade, click-outside UI dismiss
- Game tables or game protos inside the core assembly

## Threading and Godot

`SlimBootstrap` adds audio and UI as children of itself during `Init`, which runs from `_Ready`, so those nodes are in the tree before `OnAppReady`. Pause is `FlowMachine` → `GodotPauseSink` → `SceneTree.Paused`. The UI canvas uses `ProcessMode.Always`.
