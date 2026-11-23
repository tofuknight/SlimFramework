using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 无头时快进同一套状态机，核对三十条鱼、共用片段和至少一次出手。
/// 编辑器里不快进，三种出手都出现后打印通过行，场景继续跑。
/// </summary>
internal static class CutoutCombatSmoke
{
    public const string OkLine = "SlimFramework cutout combat sample smoke OK";

    private static readonly string[] ClipNames =
    [
        OpenOceanFish.SwimClip,
        OpenOceanFish.BiteClip,
        OpenOceanFish.LungeClip,
        OpenOceanFish.TailSwipeClip,
        OpenOceanFish.RecoilClip
    ];

    public static bool ShouldFastForward() => ShouldQuitAfterSmoke();

    public static bool AttacksSatisfied(FishArena arena) =>
        arena.VerifiedBite > 0 && arena.VerifiedLunge > 0 && arena.VerifiedTail > 0;

    public static void AssertSpawned(FishArena arena)
    {
        Expect(arena.Fish.Count == FishArena.FishCount, $"鱼的数量是 {arena.Fish.Count}，应当是 {FishArena.FishCount}。");
        Expect(arena.LivingCount == FishArena.FishCount, $"开场存活数是 {arena.LivingCount}。");
        var swallowers = 0;
        foreach (var fish in arena.Fish)
        {
            Expect(fish.Rig.BoneCount == 9, $"骨骼数是 {fish.Rig.BoneCount}。");
            Expect(fish.Rig.SlotIds.Count == OpenOceanFish.SlotIds.Length, "槽位数不对。");
            var body = fish.Rig.GetSkinnedSlot(OpenOceanFish.SlotBody);
            Expect(body.GetParent() == fish.Rig.Skeleton, "身体没有挂在骨架上。");
            Expect(body.GetBoneCount() == 4, "身体没有绑上脊柱。");
            Expect(FishFacing.IsBellyDown(fish.Rig), "开场就有鱼肚皮朝上。");
            if (fish.Species != SwallowerFish.SpeciesName)
                continue;
            swallowers++;
            var head = fish.Rig.GetSlotTexture(OpenOceanFish.SlotHead);
            var tail = fish.Rig.GetSlotTexture(OpenOceanFish.SlotTail);
            Expect(head != null && head.GetWidth() > 8, "对战里的吞食者没有头贴图。");
            Expect(tail != null && tail.GetWidth() > 8, "对战里的吞食者没有尾贴图。");
            Expect(Mathf.IsEqualApprox(fish.Rig.Root.Scale.X, SwallowerFish.CombatScale), "对战里的吞食者不是整数缩放。");
            Expect(fish.Rig.GetSlotNode(OpenOceanFish.SlotHead).TextureFilter == CanvasItem.TextureFilterEnum.Nearest, "吞食者没有用最近邻过滤。");
        }

        Expect(swallowers == FishArena.FishCount / 2, $"吞食者有 {swallowers} 条，应当一半。");
    }

    public static void AssertShared(FishArena arena, bool swapSlot = true)
    {
        Expect(arena.Fish.Count == FishArena.FishCount, $"鱼的数量是 {arena.Fish.Count}，应当是 {FishArena.FishCount}。");
        foreach (var name in ClipNames)
        {
            var shared = arena.Clips.Get(name);
            Expect(GodotObject.IsInstanceValid(shared), $"片段 {name} 无效。");
            foreach (var fish in arena.Fish)
                Expect(fish.Rig.GetClip(name) == shared, $"鱼没有共用片段 {name}。");
        }

        if (!swapSlot)
            return;

        var sample = arena.Fish[4];
        var listed = sample.Rig.Player.GetAnimationList().Length;
        var previous = sample.Rig.GetSlotTexture(OpenOceanFish.SlotBody);
        var replacement = arena.Parts.Get(OpenOceanFish.SlotBody, (sample.BodyTier + 1) % PlaceholderCutouts.TierCount);
        sample.Rig.SetSlotTexture(OpenOceanFish.SlotBody, replacement);
        Expect(sample.Rig.GetSlotTexture(OpenOceanFish.SlotBody) == replacement, "抽查换装没有换上贴图。");
        Expect(sample.Rig.Player.GetAnimationList().Length == listed, "换装改了片段数量。");
        foreach (var name in ClipNames)
            Expect(sample.Rig.GetClip(name) == arena.Clips.Get(name), $"换装换成了另一段 {name}。");
        sample.Rig.SetSlotTexture(OpenOceanFish.SlotBody, previous);
    }

    public static void Run(FishArena arena, Node host)
    {
        AssertSpawned(arena);
        AssertShared(arena);
        ProbeFacing(arena.Fish[0]);
        for (var i = 0; i < 360; i++)
        {
            arena.App.TickFrame(1.0 / 60.0);
            arena.Step(1.0 / 60.0);
            if (AttacksSatisfied(arena) && arena.SawWave && arena.FlashStarts >= 1 && arena.SawBrightFlash)
                break;
        }

        if (!AttacksSatisfied(arena))
        {
            throw new InvalidOperationException(
                $"对战没有打全出手：咬合 {arena.BitePlays}/{arena.VerifiedBite}，冲刺 {arena.LungePlays}/{arena.VerifiedLunge}，甩尾 {arena.TailPlays}/{arena.VerifiedTail}。");
        }

        Expect(!arena.SawBellyUpAlive, "对战里有活鱼肚皮朝上。");
        Expect(arena.SawWave, "脊柱没有弯出身体的波。");
        Expect(arena.FlashStarts >= 1 && arena.SawBrightFlash, $"没有看到受击闪白（{arena.FlashStarts}）。");
        ExpectTailScaleVaries(arena);
        ExpectFade(arena);

        AssertShared(arena);
        Callable.From(() => Finish(arena, host)).CallDeferred();
    }

    public static void Finish(FishArena arena, Node host)
    {
        try
        {
            Expect(GodotObject.IsInstanceValid(arena.Fish[0].Rig.Root), "骨架在一帧后被释放了。");
            Expect(arena.Fish.Count == FishArena.FishCount, "一帧之后鱼的数量变了。");
            AssertShared(arena, swapSlot: false);
            if (!AttacksSatisfied(arena))
                throw new InvalidOperationException("一帧之后出手记录丢了。");
            GD.Print(OkLine);
            if (ShouldQuitAfterSmoke())
                host.GetTree()?.Quit();
        }
        catch (Exception ex)
        {
            GD.PushError(ex.ToString());
            host.GetTree()?.Quit(1);
        }
    }

    private static void ExpectTailScaleVaries(FishArena arena)
    {
        var fish = arena.Fish[1];
        var root = fish.Rig.Root.Scale;
        fish.Rig.SetSpineSineEnabled(true);
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        var step = 1.0 / OpenOceanFish.SineFrequency / 8.0;
        for (var i = 0; i < 8; i++)
        {
            arena.App.TickFrame(step);
            var scale = fish.Rig.GetBone(OpenOceanFish.BoneTail).Scale.X;
            min = Mathf.Min(min, scale);
            max = Mathf.Max(max, scale);
        }

        Expect(max - min > 0.2f, $"对战里尾鳍 Scale.X 只在 {min} 到 {max} 之间。");
        Expect(Mathf.IsEqualApprox(fish.Rig.GetBone(OpenOceanFish.BoneTail).Scale.Y, 1f), "对战里尾鳍的 Y 缩放变了。");
        Expect(fish.Rig.Root.Scale.IsEqualApprox(root), "对战里的缩短改了根缩放。");
    }

    private static void ExpectFade(FishArena arena)
    {
        var fish = arena.Fish[0];
        fish.PrepareFadeForSmoke();
        Expect(fish.Rig.Root.Modulate.A > 0.99f, "倒下前的透明度不是 1。");
        Expect(!FishFlash.Source.Contains("TEXTURE", StringComparison.Ordinal), "闪白着色器又采样了 TEXTURE。");
        Expect(FishFlash.Source.Contains("COLOR.rgb = mix(COLOR.rgb, vec3(1.0), amount)", StringComparison.Ordinal),
            "闪白没有把 COLOR.rgb 混向白色。");
        arena.Step(0.5);
        Expect(fish.Rig.Root.Modulate.A < 0.85f, $"倒下后的透明度是 {fish.Rig.Root.Modulate.A}，没有变淡。");
    }

    private static void ProbeFacing(CombatFish fish)
    {
        var heading = fish.Rig.Root.Rotation;
        fish.FaceForSmoke(0f, bellyUp: false);
        Expect(FishFacing.IsBellyDown(fish.Rig), "朝右时肚皮朝上。");
        Expect(fish.Rig.Root.Scale.Y > 0f, "朝右时不该翻 Y。");
        fish.FaceForSmoke(Mathf.Acos(-0.05f), bellyUp: false);
        Expect(fish.Rig.Root.Scale.Y > 0f, "还没明显朝左就翻了。");
        fish.FaceForSmoke(Mathf.Acos(-0.5f), bellyUp: false);
        Expect(fish.Rig.Root.Scale.Y < 0f, "明显朝左时没有翻 Y。");
        Expect(FishFacing.IsBellyDown(fish.Rig), "朝左翻过去以后肚皮朝上。");
        fish.FaceForSmoke(Mathf.Pi, bellyUp: false);
        Expect(FishFacing.IsBellyDown(fish.Rig), "朝左时肚皮朝上。");
        Expect(fish.Rig.Root.Scale.Y < 0f, "朝左时没有翻 Y。");
        fish.FaceForSmoke(Mathf.Pi, bellyUp: true);
        Expect(!FishFacing.IsBellyDown(fish.Rig), "朝左倒下后肚皮仍朝下。");
        fish.FaceForSmoke(0f, bellyUp: true);
        Expect(fish.Rig.Root.Scale.Y < 0f, "朝右倒下后没有把肚皮翻上去。");
        Expect(!FishFacing.IsBellyDown(fish.Rig), "朝右倒下后肚皮仍朝下。");
        fish.FaceForSmoke(heading, bellyUp: false);
        Expect(FishFacing.IsBellyDown(fish.Rig), "朝向探针没有把鱼放回肚皮朝下。");
    }

    private static bool ShouldQuitAfterSmoke()
    {
        if (string.Equals(DisplayServer.GetName(), "headless", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--smoke")
                return true;
        }

        return false;
    }

    private static void Expect(bool condition, string message)
    {
        if (condition)
            return;
        GD.PushError(message);
        throw new InvalidOperationException(message);
    }
}
