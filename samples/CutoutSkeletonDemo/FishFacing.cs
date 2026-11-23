using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 肚皮始终朝屏幕下方。朝左时把根节点的 Y 缩放反过来，旋转仍指向游动方向。
/// 垂直附近留一段滞回，避免每帧来回翻。
/// </summary>
internal static class FishFacing
{
    public const float MirrorOn = -0.12f;

    public const float MirrorOff = 0.12f;

    public static float BellyScreen(CutoutRig rig) =>
        Mathf.Cos(rig.Root.Rotation) * rig.Root.Scale.Y;

    public static bool IsBellyDown(CutoutRig rig) =>
        BellyScreen(rig) >= -0.2f * rig.UniformScale;

    public static void Apply(CutoutRig rig, float heading, bool bellyUp, ref bool mirrored)
    {
        var cosine = Mathf.Cos(heading);
        if (!mirrored && cosine < MirrorOn)
            mirrored = true;
        else if (mirrored && cosine > MirrorOff)
            mirrored = false;

        var sign = mirrored ? -1f : 1f;
        if (bellyUp)
            sign = -sign;
        var scale = rig.UniformScale;
        rig.Root.Rotation = heading;
        rig.Root.Scale = new Vector2(scale, sign * scale);
    }
}
