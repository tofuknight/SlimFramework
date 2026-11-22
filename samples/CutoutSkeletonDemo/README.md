# 剪影换装样例

用 Godot **4.7.2 .NET** 打开本目录的 `project.godot`，Play `Main.tscn`。不要把仓库根目录当游戏工程。

这里演示可选插件 `plugins/CutoutSkeleton`。鱼的部位对照留在本目录，不进插件，也不进 `samples/DemoGame`。

## 画面里有什么

一条远洋流线型。骨骼是 Godot 内置的 `Skeleton2D` / `Bone2D`：

`Root → SpineFront（Head→Jaw、Ridge、Dorsal、Pectoral）→ SpineMid → SpineTail → Tail`

槽位是挂在骨骼上的 `Sprite2D`：下颌、头、身体、背肌、背鳍、胸鳍、尾。贴图是代码里画的色块，没有正式像素图。

口、体、鳍各三档。换档只改对应槽位的贴图。游动和咬合是同一台 `AnimationPlayer` 上的两段片段，三条外观共用。脊柱（含尾骨）用插件的正弦摆动，不写进片段。

| 输入 | 作用 |
| --- | --- |
| `1` `2` `3` 或按钮「口」 | 换下颌和头 |
| `4` `5` `6` 或按钮「体」 | 换身体和背肌 |
| `7` `8` `9` 或按钮「鳍」 | 换背鳍、胸鳍和尾 |
| `Space` 或按钮「咬合」 | 播咬合，结束后回到循环游动 |

根节点缩放是均匀的，用来把鱼放到看得清的尺寸。没有幼体 / 成体切换。

通过时输出一行：`SlimFramework cutout skeleton sample smoke OK`。编辑器里这条出现后场景继续跑。

无头（Godot 4.7.2 .NET 可执行文件）：

```bash
godot --headless --path samples/CutoutSkeletonDemo
```

无头时打印上面那一行后退出。

## 工程开关

`Sdk` 固定 `Godot.NET.Sdk/4.7.2`。判定属性是 `SlimCutoutSkeletonDemoEditorBuild`，写在本目录的 `Directory.Build.props`。规则和 `samples/DemoGame` 相同，只是属性名分开，避免两个 Godot 工程绑在一起。长说明见 [../DemoGame/README.md](../DemoGame/README.md)。

`dotnet build ../../SlimFramework.sln` 会编这个工程，不需要安装编辑器。

`SlimBootstrap` 和 `UiPage` 仍由适配层 targets 拷到本目录的 `SlimFramework.Scripts/`（不入库）。

## 不做

吞食片段、变异叠层、闪光 / 水花 / 受击粒子、转身翻转和挤压、比赛用的 3D 像素化，以及任何自定义骨骼编辑器。
