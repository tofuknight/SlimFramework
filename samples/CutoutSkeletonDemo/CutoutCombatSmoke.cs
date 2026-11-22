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
        foreach (var fish in arena.Fish)
        {
            Expect(fish.Rig.BoneCount == 9, $"骨骼数是 {fish.Rig.BoneCount}。");
            Expect(fish.Rig.SlotIds.Count == OpenOceanFish.SlotIds.Length, "槽位数不对。");
            Expect(fish.Rig.Root.Scale.Y > 0f, "开场时根缩放被翻过去了。");
        }
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
        for (var i = 0; i < 360 && !AttacksSatisfied(arena); i++)
        {
            arena.App.TickFrame(1.0 / 60.0);
            arena.Step(1.0 / 60.0);
        }

        if (!AttacksSatisfied(arena))
        {
            throw new InvalidOperationException(
                $"对战没有打全出手：咬合 {arena.BitePlays}/{arena.VerifiedBite}，冲刺 {arena.LungePlays}/{arena.VerifiedLunge}，甩尾 {arena.TailPlays}/{arena.VerifiedTail}。");
        }

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
