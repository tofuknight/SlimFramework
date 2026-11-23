using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 把吞食者画到洋红底上，数静息轮廓里的背景像素。
/// 给切片回归用。无头假渲染器读不回像素时改走 <c>--holes</c> 的带显示运行。
/// </summary>
internal static class SwallowerHoleAudit
{
    public const string OkLine = "SlimFramework swallower hole audit OK";

    private const int Width = 160;
    private const int Height = 96;
    private const int FrameCount = 300;
    private const int HoleLimit = 32;
    private static readonly int[] BiteStarts = [21, 121, 221];
    private static readonly Color Magenta = new(1f, 0f, 1f);

    public static async Task Run(Node host)
    {
        RenderingServer.SetDefaultClearColor(Magenta);
        var viewport = new SubViewport
        {
            Name = "HoleViewport",
            Size = new Vector2I(Width * 2, Height * 2),
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest
        };
        host.AddChild(viewport);
        var rig = CutoutRig.Build(viewport, SwallowerFish.Definition(), "HoleFish");
        var anchor = SwallowerFish.Slice.ImagePoint(OpenOceanFish.BoneSpineFront);
        rig.Root.Position = anchor * 2f;
        rig.Root.Scale = new Vector2(2f, 2f);
        foreach (var slot in rig.SlotIds)
            rig.GetSlotNode(slot).TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        OpenOceanFish.AddSharedClips(rig);
        rig.Player.CallbackModeProcess = AnimationPlayer.AnimationCallbackModeProcess.Manual;
        rig.SetSpineSineEnabled(false);

        var slice = SwallowerFish.Slice;
        GD.Print($"pectoral size {slice.Pectoral.GetSize()} pos {slice.SlotPosition(OpenOceanFish.SlotPectoral)}");
        GD.Print($"anal size {slice.Anal.GetSize()} pos {slice.SlotPosition(SwallowerFish.SlotAnal)}");

        var rest = await Capture(viewport, rig, 0, swim: false, biteStart: -1);
        var silhouette = Dilate(Coverage(rest), rest.GetWidth(), rest.GetHeight(), 2);
        var worst = 0;
        var worstFrame = -1;
        Image? worstImage = null;
        var bitePeak = new int[BiteStarts.Length];
        for (var frame = 0; frame < FrameCount; frame++)
        {
            var image = await Capture(viewport, rig, frame, swim: true, biteStart: BiteIndex(frame));
            var holes = Holes(image, silhouette);
            if (holes > worst)
            {
                worst = holes;
                worstFrame = frame;
                worstImage = image;
            }

            var bite = BiteIndex(frame);
            if (bite >= 0)
                bitePeak[bite] = Math.Max(bitePeak[bite], holes);
        }

        worstImage?.SavePng("/tmp/swallower-worst.png");
        rest.SavePng("/tmp/swallower-rest.png");
        GD.Print($"hole frames {FrameCount} max {worst} at {worstFrame} bites {bitePeak[0]}/{bitePeak[1]}/{bitePeak[2]}");
        if (worst > HoleLimit)
        {
            GD.PushError($"吞食者轮廓里的洞是 {worst}px，超过 {HoleLimit}。");
            host.GetTree().Quit(1);
            return;
        }

        GD.Print(OkLine);
        host.GetTree().Quit();
    }

    private static int BiteIndex(int frame)
    {
        for (var i = 0; i < BiteStarts.Length; i++)
        {
            var local = (frame - BiteStarts[i]) / 60.0;
            if (local >= 0 && local <= OpenOceanFish.BiteLength)
                return i;
        }

        return -1;
    }

    private static async Task<Image> Capture(SubViewport viewport, CutoutRig rig, int frame, bool swim, int biteStart)
    {
        if (swim)
            ApplyPose(rig, frame / 60.0, biteStart);
        else
            ResetPose(rig);

        await hostFrame();
        var image = viewport.GetTexture().GetImage();
        if (image.IsEmpty())
            throw new InvalidOperationException("视口没有读回像素。");
        return image;

        static async Task hostFrame()
        {
            await RenderingServer.Singleton.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
    }

    private static void ResetPose(CutoutRig rig)
    {
        foreach (var name in new[]
        {
            OpenOceanFish.BoneHead, OpenOceanFish.BoneJaw, OpenOceanFish.BoneDorsal, OpenOceanFish.BonePectoral,
            OpenOceanFish.BoneSpineFront, OpenOceanFish.BoneSpineMid, OpenOceanFish.BoneSpineTail, OpenOceanFish.BoneTail
        })
        {
            var bone = rig.GetBone(name);
            bone.Rotation = 0f;
            bone.Scale = Vector2.One;
        }
    }

    private static void ApplyPose(CutoutRig rig, double time, int biteStart)
    {
        var swimTime = time % OpenOceanFish.SwimLength;
        var biteLocal = biteStart >= 0 ? (time * 60.0 - BiteStarts[biteStart]) / 60.0 : -1;
        var frozen = biteStart >= 0 ? (BiteStarts[biteStart] / 60.0) % OpenOceanFish.SwimLength : swimTime;
        rig.GetBone(OpenOceanFish.BoneDorsal).Rotation = Lerp(SwimDorsal, biteStart >= 0 ? frozen : swimTime);
        rig.GetBone(OpenOceanFish.BonePectoral).Rotation = Lerp(SwimPectoral, biteStart >= 0 ? frozen : swimTime);
        rig.GetBone(OpenOceanFish.BoneJaw).Rotation = biteLocal >= 0 ? Lerp(BiteJaw, biteLocal) : 0f;
        for (var i = 0; i < OpenOceanFish.SineBones.Length; i++)
        {
            var phase = (time * OpenOceanFish.SineFrequency * Math.Tau) + (i * OpenOceanFish.SinePhaseStep);
            var bone = rig.GetBone(OpenOceanFish.SineBones[i]);
            if (OpenOceanFish.SineAmplitudeScales[i] != 0f)
                bone.Rotation = OpenOceanFish.SineAmplitude * OpenOceanFish.SineAmplitudeScales[i] * Mathf.Sin((float)phase);
            var squeeze = 0.5f + (0.5f * Mathf.Sin((float)phase));
            bone.Scale = new Vector2(1f - (OpenOceanFish.SineScaleX[i] * squeeze), 1f);
        }
    }

    private static readonly (double Time, float Radians)[] SwimDorsal = [(0, 0f), (0.3, 0.18f), (0.6, -0.1f), (0.9, 0.16f), (1.2, 0f)];
    private static readonly (double Time, float Radians)[] SwimPectoral = [(0, 0.2f), (0.3, -0.28f), (0.6, 0.32f), (0.9, -0.06f), (1.2, 0.2f)];
    private static readonly (double Time, float Radians)[] BiteJaw = [(0, 0f), (0.12, 0.72f), (0.34, 0f)];

    private static float Lerp((double Time, float Radians)[] keys, double time)
    {
        if (time <= keys[0].Time)
            return keys[0].Radians;
        if (time >= keys[^1].Time)
            return keys[^1].Radians;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            if (keys[i].Time <= time && time <= keys[i + 1].Time)
            {
                var span = keys[i + 1].Time - keys[i].Time;
                var u = span == 0 ? 0 : (time - keys[i].Time) / span;
                return (float)(keys[i].Radians + ((keys[i + 1].Radians - keys[i].Radians) * u));
            }
        }

        return keys[^1].Radians;
    }

    private static bool[] Coverage(Image image)
    {
        var mask = new bool[image.GetWidth() * image.GetHeight()];
        for (var y = 0; y < image.GetHeight(); y++)
        {
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var color = image.GetPixel(x, y);
                mask[(y * image.GetWidth()) + x] = color.A > 0.05f && !IsMagenta(color);
            }
        }

        return mask;
    }

    private static bool[] Dilate(bool[] mask, int width, int height, int radius)
    {
        var grown = (bool[])mask.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!mask[(y * width) + x])
                    continue;
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) > radius)
                            continue;
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                            grown[(ny * width) + nx] = true;
                    }
                }
            }
        }

        return grown;
    }

    private static int Holes(Image image, bool[] silhouette)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        var fish = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                fish[(y * width) + x] = !IsMagenta(image.GetPixel(x, y));
        }

        var count = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = (y * width) + x;
                if (!silhouette[index] || fish[index])
                    continue;
                if ((Hits(fish, width, height, x, y, 0, -1) && Hits(fish, width, height, x, y, 0, 1))
                    || (Hits(fish, width, height, x, y, -1, 0) && Hits(fish, width, height, x, y, 1, 0)))
                    count++;
            }
        }

        return count;
    }

    private static bool Hits(bool[] fish, int width, int height, int x, int y, int dx, int dy)
    {
        for (var step = 1; step <= 1; step++)
        {
            var nx = x + (dx * step);
            var ny = y + (dy * step);
            if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                return false;
            if (fish[(ny * width) + nx])
                return true;
        }

        return false;
    }

    private static bool IsMagenta(Color color) =>
        color.R > 0.9f && color.G < 0.1f && color.B > 0.9f;
}
