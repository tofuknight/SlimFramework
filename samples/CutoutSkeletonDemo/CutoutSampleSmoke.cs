using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 进场景后核对：换装只改贴图，身体是蒙皮，正弦是一条传到尾巴的波。
/// 无头运行时打印通过行并退出。编辑器 Play 不退出。
/// </summary>
internal static class CutoutSampleSmoke
{
    public const string OkLine = "SlimFramework cutout skeleton sample smoke OK";

    private static readonly (string Slot, string Bone)[] SlotBones =
    [
        (OpenOceanFish.SlotJaw, OpenOceanFish.BoneJaw),
        (OpenOceanFish.SlotHead, OpenOceanFish.BoneHead),
        (OpenOceanFish.SlotDorsal, OpenOceanFish.BoneDorsal),
        (OpenOceanFish.SlotPectoral, OpenOceanFish.BonePectoral),
        (OpenOceanFish.SlotTail, OpenOceanFish.BoneTail)
    ];

    public static void Run(SlimApp app, CutoutRig rig, OpenOceanParts parts, int mouth, int body, int fin, Node host)
    {
        app.Get<CutoutSkeletonModule>();
        ExpectSwallower(app, rig, host);
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
        var skinnedBody = ExpectSkinned(rig, OpenOceanFish.SlotBody, OpenOceanBody.Vertices.Length);
        var skinnedRidge = ExpectSkinned(rig, OpenOceanFish.SlotRidge, OpenOceanBody.RidgeVertices.Length);
        ExpectHeadFlush(rig);

        var rests = SnapshotRests(rig);
        var swim = rig.GetClip(OpenOceanFish.SwimClip);
        var bite = rig.GetClip(OpenOceanFish.BiteClip);
        Expect(swim.LoopMode == Animation.LoopModeEnum.Linear, "游动片段应当循环。");
        Expect(bite.LoopMode == Animation.LoopModeEnum.None, "咬合片段不应当循环。");
        ExpectRotationTracks(swim);
        ExpectRotationTracks(bite);

        var marker = Marker();
        var bodyId = skinnedBody.GetInstanceId();
        var ridgeId = skinnedRidge.GetInstanceId();
        foreach (var slot in OpenOceanFish.SlotIds)
            rig.SetSlotTexture(slot, marker);
        Expect(rig.GetSkinnedSlot(OpenOceanFish.SlotBody).GetInstanceId() == bodyId, "换装换成了另一块身体。");
        Expect(rig.GetSkinnedSlot(OpenOceanFish.SlotRidge).GetInstanceId() == ridgeId, "换装换成了另一条背脊。");
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
        rig.Root.Scale = new Vector2(2.5f, -2.5f);
        rig.SetUniformScale(3f);
        Expect(rig.Root.Scale.IsEqualApprox(new Vector2(3f, -3f)), "均匀缩放抹掉了朝向。");
        rig.Root.Scale = new Vector2(3f, 3f);
        rig.SetUniformScale(2.5f);
        Expect(rig.Root.Scale.IsEqualApprox(new Vector2(2.5f, 2.5f)), "朝向恢复后缩放不是均匀的。");
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
        const double waveTime = 0.18d;
        app.TickFrame(waveTime);
        ExpectTravellingWave(rig, waveTime);
        ExpectTailScaleVaries(rig, app, waveTime);
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
                Expect(Mathf.IsEqualApprox(rig.GetBone(name).Scale.Y, 1f), $"一帧之后骨骼 {name} 的 Y 缩放变了。");
            Expect(rig.GetBone(OpenOceanFish.BoneHead).Scale.IsEqualApprox(Vector2.One), "头的局部缩放被正弦改了。");
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

    private static void ExpectSwallower(SlimApp app, CutoutRig placeholder, Node host)
    {
        var slice = SwallowerFish.Slice;
        Expect(slice.Head.GetWidth() > 8, "吞食者的头没有读到。");
        Expect(slice.Jaw.GetHeight() > 4, "吞食者的下颌没有读到。");
        Expect(slice.Body.GetWidth() > 8, "吞食者的身体没有读到。");
        Expect(slice.Dorsal.GetWidth() > 4, "吞食者的背鳍没有读到。");
        Expect(slice.Pectoral.GetWidth() > 4, "吞食者的胸鳍没有读到。");
        Expect(slice.Anal.GetWidth() > 4 && slice.Anal.GetWidth() < 30, "吞食者的臀鳍没有从胸鳍里分开。");
        Expect(slice.Tail.GetWidth() > 8, "吞食者的尾鳍没有读到。");
        Expect(slice.BodySkin.Vertices.Length >= 8, "吞食者的身体轮廓太短。");
        var module = app.Get<CutoutSkeletonModule>();
        var probe = module.Attach(host, SwallowerFish.Definition(), "SwallowerProbe");
        try
        {
            Expect(probe.BoneCount == 9, $"吞食者骨骼数是 {probe.BoneCount}。");
            Expect(probe.BonePath(OpenOceanFish.BoneJaw).ToString() == placeholder.BonePath(OpenOceanFish.BoneJaw).ToString(), "吞食者的下颌路径和共用片段不一致。");
            Expect(probe.BonePath(OpenOceanFish.BoneTail).ToString() == placeholder.BonePath(OpenOceanFish.BoneTail).ToString(), "吞食者的尾路径和共用片段不一致。");
            Expect(probe.GetBone(OpenOceanFish.BoneHead).GetParent() == probe.GetBone(OpenOceanFish.BoneSpineFront), "吞食者的头没有跟着脊柱前段。");
            var body = probe.GetSkinnedSlot(OpenOceanFish.SlotBody);
            Expect(body.GetBoneCount() == 4, "吞食者的身体没有绑上脊柱。");
            Expect(body.Texture == slice.Body, "吞食者的身体贴图不是切片。");
            Expect(probe.SlotIds.Count == OpenOceanFish.SlotIds.Length + 1, "吞食者应当比占位鱼多一个臀鳍槽。");
            Expect(probe.GetSlotNode(SwallowerFish.SlotAnal).GetParent() == probe.GetBone(OpenOceanFish.BoneSpineTail), "臀鳍没有挂在尾段上。");
            Expect(probe.GetSlotNode(OpenOceanFish.SlotPectoral).GetParent() == probe.GetBone(OpenOceanFish.BonePectoral), "胸鳍没有留在自己的骨头上。");
            Expect(probe.GetSlotNode(OpenOceanFish.SlotDorsal).GetParent() == probe.GetBone(OpenOceanFish.BoneDorsal), "背鳍没有留在自己的骨头上。");
            Expect(probe.GetSlotNode(OpenOceanFish.SlotTail).GetParent() == probe.GetBone(OpenOceanFish.BoneTail), "尾鳍没有留在尾根上。");
        }
        finally
        {
            module.Detach(probe);
            probe.Root.Free();
        }
    }

    private static Polygon2D ExpectSkinned(CutoutRig rig, string slot, int vertices)
    {
        var skin = rig.GetSkinnedSlot(slot);
        Expect(skin.GetParent() == rig.Skeleton, $"{slot} 没有挂在骨架上。");
        Expect(skin.Polygon.Length == vertices, $"{slot} 顶点数不对。");
        Expect(skin.GetBoneCount() == 4, $"{slot} 绑了 {skin.GetBoneCount()} 根骨头。");
        Expect(skin.GetBonePath(0).ToString() == OpenOceanFish.BoneSpineFront, $"{slot} 第一根骨头不是脊柱前段。");
        Expect(skin.GetBoneWeights(0).Length == skin.Polygon.Length, $"{slot} 权重数量不对。");
        Expect(skin.UV.Length == skin.Polygon.Length, $"{slot} UV 数量不对。");
        return skin;
    }

    private static void ExpectTravellingWave(CutoutRig rig, double time)
    {
        var tail = 0f;
        var front = 0f;
        var head = 0f;
        for (var i = 0; i < OpenOceanFish.SineBones.Length; i++)
        {
            var expected = OpenOceanFish.SineAmplitude * OpenOceanFish.SineAmplitudeScales[i]
                * Mathf.Sin(((float)time * OpenOceanFish.SineFrequency * Mathf.Tau) + (i * OpenOceanFish.SinePhaseStep));
            var actual = rig.GetBone(OpenOceanFish.SineBones[i]).Rotation;
            Expect(Mathf.Abs(actual - expected) < 0.02f, $"{OpenOceanFish.SineBones[i]} 的摆动是 {actual}，应当接近 {expected}。");
            if (OpenOceanFish.SineBones[i] == OpenOceanFish.BoneTail)
                tail = actual;
            if (OpenOceanFish.SineBones[i] == OpenOceanFish.BoneSpineFront)
                front = actual;
            if (OpenOceanFish.SineBones[i] == OpenOceanFish.BoneHead)
                head = actual;
        }

        Expect(Mathf.Abs(tail) > Mathf.Abs(front) + 0.05f, "尾巴的振幅没有比身体前段大。");
        Expect(Mathf.Abs(head) < 0.02f, "头没有跟着脊柱前段。");
    }

    private static void ExpectTailScaleVaries(CutoutRig rig, SlimApp app, double time)
    {
        var root = rig.Root.Scale;
        var first = TailScaleX(rig, time);
        var half = 0.5d / OpenOceanFish.SineFrequency;
        app.TickFrame(half);
        var second = TailScaleX(rig, time + half);
        Expect(Mathf.Abs(first - second) > 0.2f, $"尾鳍局部 Scale.X 从 {first} 到 {second}，没有缩短。");
        Expect(Mathf.Min(first, second) < 0.5f, "尾鳍没有收到一半以下。");
        Expect(Mathf.Max(first, second) > 0.9f, "尾鳍没有回到接近 1。");
        var squeezed = first < second ? time : time + half;
        var tailIndex = Array.IndexOf(OpenOceanFish.SineBones, OpenOceanFish.BoneTail);
        var spineIndex = Array.IndexOf(OpenOceanFish.SineBones, OpenOceanFish.BoneSpineTail);
        Expect(ExpectedScaleX(spineIndex, squeezed) > ExpectedScaleX(tailIndex, squeezed), "尾段缩短得不该比尾鳍多。");
        Expect(rig.Root.Scale.IsEqualApprox(root), "缩短改了根缩放。");
        foreach (var name in rig.BoneNames)
            Expect(Mathf.IsEqualApprox(rig.GetBone(name).Scale.Y, 1f), $"骨骼 {name} 的 Y 缩放被正弦改了。");
        rig.SetSpineSineEnabled(false);
        Expect(rig.GetBone(OpenOceanFish.BoneTail).Scale.IsEqualApprox(Vector2.One), "关掉正弦后尾鳍还缩着。");
        rig.SetSpineSineEnabled(true);
    }

    private static float TailScaleX(CutoutRig rig, double time)
    {
        var index = Array.IndexOf(OpenOceanFish.SineBones, OpenOceanFish.BoneTail);
        var expected = ExpectedScaleX(index, time);
        var actual = rig.GetBone(OpenOceanFish.BoneTail).Scale.X;
        Expect(Mathf.Abs(actual - expected) < 0.02f, $"尾鳍 Scale.X 是 {actual}，应当接近 {expected}。");
        return actual;
    }

    private static float ExpectedScaleX(int index, double time)
    {
        var phase = ((float)time * OpenOceanFish.SineFrequency * Mathf.Tau) + (index * OpenOceanFish.SinePhaseStep);
        var squeeze = 0.5f + (0.5f * Mathf.Sin(phase));
        return 1f - (OpenOceanFish.SineScaleX[index] * squeeze);
    }

    private static void ExpectHeadFlush(CutoutRig rig)
    {
        var head = rig.GetSlot(OpenOceanFish.SlotHead);
        var body = rig.GetSkinnedSlot(OpenOceanFish.SlotBody);
        var frontX = float.NegativeInfinity;
        var frontTop = float.PositiveInfinity;
        var frontBottom = float.NegativeInfinity;
        foreach (var point in body.Polygon)
        {
            if (point.X < frontX - 0.5f)
                continue;
            if (point.X > frontX + 0.5f)
            {
                frontX = point.X;
                frontTop = point.Y;
                frontBottom = point.Y;
                continue;
            }

            frontX = Mathf.Max(frontX, point.X);
            frontTop = Mathf.Min(frontTop, point.Y);
            frontBottom = Mathf.Max(frontBottom, point.Y);
        }

        var image = head.Texture.GetImage();
        var size = image.GetSize();
        var origin = rig.Skeleton.ToLocal(head.GlobalPosition);
        var axisX = rig.Skeleton.ToLocal(head.ToGlobal(Vector2.Right)) - origin;
        var axisY = rig.Skeleton.ToLocal(head.ToGlobal(Vector2.Down)) - origin;
        var neckX = float.PositiveInfinity;
        var neckTop = float.PositiveInfinity;
        var neckBottom = float.NegativeInfinity;
        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                if (image.GetPixel(x, y).A <= 0.1f)
                    continue;
                var local = new Vector2(x + 0.5f - (size.X * 0.5f), y + 0.5f - (size.Y * 0.5f));
                var at = origin + (axisX * local.X) + (axisY * local.Y);
                if (at.X > neckX + 3f)
                    continue;
                if (at.X < neckX - 0.5f)
                {
                    neckX = at.X;
                    neckTop = at.Y;
                    neckBottom = at.Y;
                    continue;
                }

                neckX = Mathf.Min(neckX, at.X);
                neckTop = Mathf.Min(neckTop, at.Y);
                neckBottom = Mathf.Max(neckBottom, at.Y);
            }
        }

        var overlap = frontX - neckX;
        Expect(overlap > 1f, $"头的后缘在 {neckX}，没有盖住身体前缘 {frontX}。");
        Expect(overlap < 14f, $"头插进身体 {overlap} 像素，太深。");
        Expect(neckTop <= frontTop + 2f && neckBottom >= frontBottom - 2f,
            $"头的后缘高度 {neckTop}..{neckBottom} 没有盖住身体前缘 {frontTop}..{frontBottom}。");
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
        var sine = new HashSet<string>();
        for (var bone = 0; bone < OpenOceanFish.SineBones.Length; bone++)
        {
            if (OpenOceanFish.SineAmplitudeScales[bone] != 0f)
                sine.Add(OpenOceanFish.SineBones[bone]);
        }
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
