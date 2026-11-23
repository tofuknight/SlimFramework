# 剪影换装与对战样例

用 Godot **4.7.2 .NET** 打开本目录的 `project.godot`。不要把仓库根目录当游戏工程。

这里演示可选插件 `plugins/CutoutSkeleton`。鱼的部位对照、占位图和对战规则留在本目录，不进插件，也不进 `samples/DemoGame`。

主场景是换装。对战是同一工程里的另一场，三十条鱼各自游动并出手。玩家不指挥单条鱼。

## 换装

Play `Main.tscn`。一条远洋流线型。骨骼是 Godot 内置的 `Skeleton2D` / `Bone2D`：

`Root → SpineFront（Head→Jaw、Ridge、Dorsal、Pectoral）→ SpineMid → SpineTail → Tail`

槽位里，下颌、头、背鳍、胸鳍、尾是挂在骨骼上的 `Sprite2D`。身体和背肌是绑在 `SpineFront` / `SpineMid` / `SpineTail` / `Tail` 上的蒙皮 `Polygon2D`，所以脊柱一弯，这两块跟着弯。默认贴图是代码里画的色块。换档只改贴图，不换那块多边形。

`V` 或按钮「物种」切到吞食者。这是 `Art/Swallower/` 里的像素图，按 `slices.json` 的枢轴装上同一套骨骼和同一套片段。头跟着脊柱前段。下颌绕铰链张开。身体蒙皮仍走脊柱和尾。这条鱼没有单独的背肌条，Ridge 槽位留空。显示是 2 倍，过滤是最近邻。切回占位鱼之后，`1`–`9` 继续换档。

口、体、鳍各三档。游动、咬合、冲刺、甩尾、后仰写在同一套片段里，挂到这一台 `AnimationPlayer` 上。换装场景只操作游动和咬合。仍是侧视。脊柱正弦的平面旋转很小，只留一点活气。左右摆用骨头局部 `Scale.X` 假装透视缩短：尾鳍大约从 1 收到 0.35，尾段少一些，相位从头传到尾。背鳍和胸鳍有一点反向缩放。头的旋转振幅是 0，跟着脊柱前段，后缘盖住身体前端。咬合只动下颌，波继续走。冲刺、甩尾、后仰会暂时关掉这条鱼的正弦，局部缩放回到静息。

| 输入 | 作用 |
| --- | --- |
| `1` `2` `3` 或按钮「口」 | 换下颌和头 |
| `4` `5` `6` 或按钮「体」 | 换身体和背肌 |
| `7` `8` `9` 或按钮「鳍」 | 换背鳍、胸鳍和尾 |
| `V` 或按钮「物种」 | 在占位鱼和吞食者之间切换 |
| `Space` 或按钮「咬合」 | 播咬合，结束后回到循环游动 |
| `C` 或按钮「对战」 | 进入对战场景 |

根节点缩放是均匀的，用来把鱼放到看得清的尺寸。没有幼体 / 成体切换。

通过时输出一行：`SlimFramework cutout skeleton sample smoke OK`。编辑器里这条出现后场景继续跑。

## 对战

Play `Combat.tscn`：在文件系统里选中它，按 F6（运行当前场景）。也可以先 Play 换装，再按 `C`。

三十条鱼，用同一套 `CutoutRig` 和同一套片段。偶数序号是吞食者，1 倍显示；奇数序号是远洋流线型，出生时随机口、体、鳍档位，以及一个不大的均匀根缩放。身体和换装场景一样是蒙皮。平面摆动和尾部缩短也是同一条正弦，不改根节点缩放。

场地是矩形水池。鱼用运动学转向，不进 `RigidBody2D`。每条鱼自己游、在附近找活鱼、靠近、进入距离后播一段攻击、短暂停一下，再重复。朝左时把根节点的 `Scale.Y` 反过来，旋转仍指向游动方向，所以肚皮一直朝下；接近垂直时有一点滞回，避免来回翻。`SetUniformScale` 只改绝对值，不抹掉这个符号。

简单生命。被打到时所有槽位先白约 0.06 秒，再在约 0.08 秒里淡回本色，同时被推开大约 40 像素。倒下的鱼不再出手，在当前朝向上把肚皮翻到屏幕上方，然后下沉变淡。`R` 重开会清掉这一场，再生成三十条。

攻击片段全场共用同一份 `Animation`：咬合、冲刺（头、下颌，加上身体前倾）、甩尾（尾和尾椎）。受击时可以再播一段短后仰。换槽位贴图不重建、也不复制片段库。冲刺、甩尾和后仰会暂时关掉这条鱼的脊柱正弦；结束后回到循环游动，正弦重新打开。咬合时正弦保持。

| 输入 | 作用 |
| --- | --- |
| `P` 或按钮「暂停」 | 暂停 / 继续 |
| `R` 或按钮「重开」 | 重新生成三十条 |
| `M` 或按钮「换装」 | 回到换装场景 |

画面左上角是剩余条数，以及咬合、冲刺、甩尾的出手次数。

编辑器里三种出手都出现过之后，输出一行：`SlimFramework cutout combat sample smoke OK`。场景继续跑。

## 无头

Godot 4.7.2 .NET 可执行文件，在仓库根目录执行。

换装：

```bash
godot --headless --path samples/CutoutSkeletonDemo
```

打印 `SlimFramework cutout skeleton sample smoke OK` 后退出。

对战（从主场景转过去）：

```bash
godot --headless --path samples/CutoutSkeletonDemo -- --combat
```

直接打开对战场景也可以：

```bash
godot --headless --path samples/CutoutSkeletonDemo res://Combat.tscn
```

两条都会快进同一套状态机，打印 `SlimFramework cutout combat sample smoke OK` 后退出。

## 重切吞食者

原图是 `Art/Swallower/swallower_full.png`，115×64，头朝右。在仓库根目录执行：

```bash
python3 samples/CutoutSkeletonDemo/Art/Swallower/slice_swallower.py
```

脚本只用 Python 标准库，不进游戏运行时。它按像素规则切出头、下颌、身体、背鳍、胸鳍和尾，关节处留 1 到 2 像素重叠，并把下颌盖住的嘴填上邻近的深色。结果写到 `Art/Swallower/parts/` 和 `slices.json`。样例直接读这两份，不必先跑脚本。旁边的 `.import` 把 PNG 收成无损、不生成 mipmap。场景里把槽位过滤设成最近邻。有导入缓存时走导入后的贴图；没有缓存时读源文件。

## 工程开关

`Sdk` 固定 `Godot.NET.Sdk/4.7.2`。判定属性是 `SlimCutoutSkeletonDemoEditorBuild`，写在本目录的 `Directory.Build.props`。规则和 `samples/DemoGame` 相同，只是属性名分开，避免两个 Godot 工程绑在一起。长说明见 [../DemoGame/README.md](../DemoGame/README.md)。

`dotnet build ../../SlimFramework.sln` 会编这个工程，不需要安装编辑器。

`SlimBootstrap` 和 `UiPage` 仍由适配层 targets 拷到本目录的 `SlimFramework.Scripts/`（不入库）。

## 不做

吞食片段、变异叠层、水花和受击粒子、繁殖、玩家指挥单条鱼，以及比赛用的 3D 像素化。受击只是槽位上短暂混白。没有自定义骨骼编辑器。换装场景不把鱼翻过去；对战里的朝向和倒下只写在这个样例里。
