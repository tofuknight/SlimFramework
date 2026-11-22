using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 进场景后核对：换装只改贴图，片段还是那两段，正弦只摆脊柱链。
/// 无头运行时打印通过行并退出。编辑器 Play 不退出。
/// </summary>
internal static class CutoutSampleSmoke
{
    public const string OkLine = "SlimFramework cutout skeleton sample smoke OK";

    private static readonly (string Slot, string Bone)[] SlotBones =
    [
        (OpenOceanFish.SlotJaw, OpenOceanFish.BoneJaw),
        (OpenOceanFish.SlotHead, OpenOceanFish.BoneHead),
        (OpenOceanFish.SlotBody, OpenOceanFish.BoneSpineFront),
        (OpenOceanFish.SlotRidge, OpenOceanFish.BoneRidge),
        (OpenOceanFish.SlotDorsal, OpenOceanFish.BoneDorsal),
        (OpenOceanFish.SlotPectoral, OpenOceanFish.BonePectoral),
        (OpenOceanFish.SlotTail, OpenOceanFish.BoneTail)
    ];

    public static void Run(SlimApp app, CutoutRig rig, OpenOceanParts parts, int mouth, int body, int fin, Node host)
    {
        app.Get<CutoutSkeletonModule>();
        Expect(rig.BoneCount == 9, $"骨骼数是 {rig.BoneCount}，应当是 9。");
        Expect(rig.Skeleton.GetBoneCount() == rig.BoneCount, $"Skeleton2D 登记了 {rig.Skeleton.GetBoneCount()} 根骨头。");
        Expect(rig.Skeleton.Name == CutoutRig.SkeletonNodeName, "骨架节点名不对。");
        Expect(rig.Player.Name == CutoutRig.AnimationPlayerNodeName, "动画播放器节点名不对。");
        Expect(rig.Skeleton.GetParent() == rig.Root, "Skeleton2D 不在根下。");
        Expect(rig.Player.GetParent() == rig.Root, "AnimationPlayer 不在根下。");
        Expect(rig.GetBone(OpenOceanFish.BoneSpineFront).GetParent() == rig.Skeleton, "SpineFront 不在骨架下。");
        ExpectParent(rig, OpenOceanFish.BoneHead, OpenOceanFish.BoneSpineFront);
        ExpectParent(rig, OpenOceanFish.BoneJaw, OpenOceanFish.BoneHead);
        ExpectParent(rig, OpenOceanFish.BoneRidge, OpenOceanFish.BoneSpineFront);
        ExpectParent(rig, OpenOceanFish.BoneDorsal, OpenOceanFish.BoneSpineFront);
        ExpectParent(rig, OpenOceanFish.BonePectoral, OpenOceanFish.BoneSpineFront);
        ExpectParent(rig, OpenOceanFish.BoneSpineMid, OpenOceanFish.BoneSpineFront);
        ExpectParent(rig, OpenOceanFish.BoneSpineTail, OpenOceanFish.BoneSpineMid);
        ExpectParent(rig, OpenOceanFish.BoneTail, OpenOceanFish.BoneSpineTail);
        foreach (var (slot, bone) in SlotBones)
            Expect(rig.GetSlot(slot).GetParent() == rig.GetBone(bone), $"槽位 {slot} 没有挂在 {bone} 上。");

        var rests = SnapshotRests(rig);
        var swim = rig.GetClip(OpenOceanFish.SwimClip);
        var bite = rig.GetClip(OpenOceanFish.BiteClip);
        Expect(swim.LoopMode == Animation.LoopModeEnum.Linear, "游动片段应当循环。");
        Expect(bite.LoopMode == Animation.LoopModeEnum.None, "咬合片段不应当循环。");
        ExpectRotationTracks(swim);
        ExpectRotationTracks(bite);

        var marker = Marker();
        foreach (var slot in OpenOceanFish.SlotIds)
            rig.SetSlotTexture(slot, marker);
        ExpectSameRests(rests, rig, "换装改了骨骼静息。");
        Expect(rig.Skeleton.GetBoneCount() == rig.BoneCount, "换装改了骨骼数量。");
        foreach (var (slot, bone) in SlotBones)
            Expect(rig.GetSlot(slot).GetParent() == rig.GetBone(bone), $"换装改了槽位 {slot} 的父骨骼。");
        foreach (var slot in OpenOceanFish.SlotIds)
            Expect(rig.GetSlotTexture(slot) == marker, $"槽位 {slot} 没有换成新贴图。");
        Expect(rig.GetClip(OpenOceanFish.SwimClip) == swim, "换装换成了另一段游动。");
        Expect(rig.GetClip(OpenOceanFish.BiteClip) == bite, "换装换成了另一段咬合。");

        rig.SetSlotTexture(OpenOceanFish.SlotRidge, null);
        Expect(rig.GetSlotTexture(OpenOceanFish.SlotRidge) == null, "槽位没有清空。");
        ExpectSameRests(rests, rig, "清空贴图改了骨骼静息。");

        rig.SetUniformScale(2.5f);
        Expect(rig.Root.Scale.IsEqualApprox(new Vector2(2.5f, 2.5f)), "根缩放不是均匀的。");
        foreach (var name in rig.BoneNames)
            Expect(rig.GetBone(name).Scale.IsEqualApprox(Vector2.One), $"骨骼 {name} 的局部缩放被改了。");
        ExpectSameRests(rests, rig, "根缩放改了骨骼静息。");

        rig.SetSpineSineEnabled(false);
        var parked = SnapshotRotations(rig);
        app.TickFrame(0.2d);
        Expect(Mathf.Abs(rig.GetBone(OpenOceanFish.BoneHead).Rotation - parked[OpenOceanFish.BoneHead]) < 0.0001f, "关闭正弦后头动了。");
        Expect(Mathf.Abs(rig.GetBone(OpenOceanFish.BoneSpineMid).Rotation - parked[OpenOceanFish.BoneSpineMid]) < 0.0001f, "关闭正弦后脊柱动了。");
        ExpectSameRests(rests, rig, "关闭正弦改了静息。");

        rig.SetSpineSineEnabled(true);
        app.TickFrame(0.37d);
        Expect(SineMoved(rig, parked), "正弦没有推动脊柱链。");
        Expect(Mathf.Abs(rig.GetBone(OpenOceanFish.BoneHead).Rotation - parked[OpenOceanFish.BoneHead]) < 0.02f, "正弦改到了头。");
        Expect(Mathf.Abs(rig.GetBone(OpenOceanFish.BoneJaw).Rotation - parked[OpenOceanFish.BoneJaw]) < 0.02f, "正弦改到了下颌。");
        ExpectSameRests(rests, rig, "正弦改了骨骼静息。");

        rig.Play(OpenOceanFish.BiteClip);
        Expect(rig.CurrentClip == OpenOceanFish.BiteClip, $"当前片段是 {rig.CurrentClip}，应当是咬合。");
        ExpectSameRests(rests, rig, "播放咬合改了骨骼静息。");

        rig.SetUniformScale(OpenOceanFish.DisplayScale);
        OpenOceanFish.Apply(rig, parts, mouth, body, fin);
        foreach (var slot in OpenOceanFish.SlotIds)
            Expect(rig.GetSlotTexture(slot) != marker, $"槽位 {slot} 还留着检查用贴图。");
        rig.Play(OpenOceanFish.SwimClip);

        Callable.From(() => FinishFrame(rig, rests, parked, host)).CallDeferred();
    }

    private static void FinishFrame(CutoutRig rig, Dictionary<string, Transform2D> rests, Dictionary<string, float> parked, Node host)
    {
        try
        {
            Expect(GodotObject.IsInstanceValid(rig.Root), "骨架在一帧后被释放了。");
            ExpectSameRests(rests, rig, "一帧之后骨骼静息变了。");
            Expect(rig.Skeleton.GetBoneCount() == rig.BoneCount, "一帧之后骨骼数量变了。");
            Expect(rig.Root.Scale.IsEqualApprox(new Vector2(OpenOceanFish.DisplayScale, OpenOceanFish.DisplayScale)), "一帧之后根缩放变了。");
            foreach (var name in rig.BoneNames)
                Expect(rig.GetBone(name).Scale.IsEqualApprox(Vector2.One), $"一帧之后骨骼 {name} 出现了局部缩放。");
            Expect(SineMoved(rig, parked), "正弦没有留在脊柱上。");
            Expect(Mathf.Abs(rig.GetBone(OpenOceanFish.BoneJaw).Rotation - parked[OpenOceanFish.BoneJaw]) < 0.05f, "游动片段改到了下颌。");
            Expect(rig.CurrentClip == OpenOceanFish.SwimClip, $"一帧后片段是 {rig.CurrentClip}，应当仍是游动。");
            Expect(rig.SpineSineEnabled, "正弦被关掉了。");
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

    private static bool SineMoved(CutoutRig rig, Dictionary<string, float> parked)
    {
        foreach (var name in OpenOceanFish.SineBones)
        {
            if (Mathf.Abs(rig.GetBone(name).Rotation - parked[name]) > 0.02f)
                return true;
        }

        return false;
    }

    private static void ExpectRotationTracks(Animation animation)
    {
        var sine = new HashSet<string>(OpenOceanFish.SineBones);
        Expect(animation.GetTrackCount() > 0, "片段没有轨道。");
        for (var i = 0; i < animation.GetTrackCount(); i++)
        {
            Expect(animation.TrackGetType(i) == Animation.TrackType.Value, "片段里有不是数值的轨道。");
            var path = animation.TrackGetPath(i).ToString();
            var pieces = path.Split(':');
            Expect(pieces.Length == 2 && pieces[1] == "rotation", $"轨道不是骨骼旋转：{path}");
            Expect(!path.Contains("Slot_", StringComparison.Ordinal), $"轨道指向了槽位：{path}");
            var node = pieces[0].Split('/').Last();
            Expect(!sine.Contains(node), $"正弦骨骼被写进了片段：{path}");
        }
    }

    private static Dictionary<string, Transform2D> SnapshotRests(CutoutRig rig)
    {
        var rests = new Dictionary<string, Transform2D>(rig.BoneCount);
        foreach (var name in rig.BoneNames)
            rests[name] = rig.GetBone(name).Rest;
        return rests;
    }

    private static Dictionary<string, float> SnapshotRotations(CutoutRig rig)
    {
        var rotations = new Dictionary<string, float>(rig.BoneCount);
        foreach (var name in rig.BoneNames)
            rotations[name] = rig.GetBone(name).Rotation;
        return rotations;
    }

    private static void ExpectSameRests(Dictionary<string, Transform2D> before, CutoutRig rig, string message)
    {
        foreach (var (name, rest) in before)
        {
            var now = rig.GetBone(name).Rest;
            var same = rest.Origin.DistanceTo(now.Origin) < 0.001f
                && rest.X.DistanceTo(now.X) < 0.001f
                && rest.Y.DistanceTo(now.Y) < 0.001f;
            Expect(same, $"{message}（{name}）");
        }
    }

    private static void ExpectParent(CutoutRig rig, string bone, string parent) =>
        Expect(rig.GetBone(bone).GetParent() == rig.GetBone(parent), $"{bone} 的父节点不是 {parent}。");

    private static ImageTexture Marker()
    {
        var image = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        image.Fill(new Color(1f, 0f, 1f));
        return ImageTexture.CreateFromImage(image);
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
