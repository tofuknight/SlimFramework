# 缺口

补洞按这个顺序。P1 没做完，不构成拒绝无关 PR 的理由。禁止把还没落地的行为写成已经有。明确不做的项不要开任务。

规则正文在 [architecture.md](architecture.md)、[extension-rules.md](extension-rules.md)、[../CONTRIBUTING.md](../CONTRIBUTING.md)。

## P1 现在可以补

按下面的顺序，一项一个 PR。

## P2 及以后

脚手架落地之后，一项一个插件 PR：

- 异步加载、分帧卸载
- BGM 交叉淡化
- 比 `IUiPage.OnOpen` / `OnClose` 更完整的 UI 生命周期

可选加固，不是功能：`GuardFrameworkDoesNotReferenceGame` 的条件加上路径子串 `plugin`，让 `src/` 引用 `plugins/` 时构建失败。加上之前，审阅已经按 [extension-rules.md](extension-rules.md) 拒绝这种引用。

## 明确不做

这些是 QFramework 式膨胀。不进核心，也不做插件，不开任务：

- DI 容器
- Manager 集合（一个功能一个 Manager）
- MVVM
- 核心里的玩法系统：关卡、战斗、背包、任务、剧情
- Command / Query / Architecture 容器
- 用反射单例代替 `SlimApp` 做服务定位

`NodeSingleton<T>` 只给必须活在场景树里的节点。它不是服务定位器。启用前要先完成下面的暂存。

## 触发才做

`NodeSingleton<T>` 今天编在 `SlimFramework.Godot.dll` 里，没有场景继承它。一旦有游戏脚本或 `.tscn` 要继承它，同一 PR 里按 [extension-rules.md](extension-rules.md) 暂存到 `SlimFramework.Scripts/`，并从适配 csproj `Compile Remove`。禁止先挂上场景、以后再补暂存。
