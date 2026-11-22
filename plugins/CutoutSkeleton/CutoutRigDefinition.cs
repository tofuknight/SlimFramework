using Godot;

namespace SlimFramework.CutoutSkeleton;

/// <summary>一根 Godot 内置 <see cref="Bone2D"/>。父节点必须在列表里更早出现。</summary>
public sealed class CutoutBoneDefinition
{
    public required string Name { get; init; }

    /// <summary>父骨骼名。<c>null</c> 表示直接挂在 <see cref="Skeleton2D"/> 下。</summary>
    public string? Parent { get; init; }

    public Vector2 Position { get; init; }

    public float Rotation { get; init; }

    /// <summary>编辑器里骨头的显示长度。剪影子节点跟的是节点变换，不吃这个长度。</summary>
    public float Length { get; init; } = 16f;
}

/// <summary>一个槽位：挂在某根骨骼上的 <see cref="Sprite2D"/>。换装只改它的贴图。</summary>
public sealed class CutoutSlotDefinition
{
    public required string Id { get; init; }

    public required string Bone { get; init; }

    public Vector2 Position { get; init; }

    public int ZIndex { get; init; }

    public Texture2D? Texture { get; init; }
}

/// <summary>
/// 可选正弦。调用方列出要摆动的骨骼（通常是脊柱链），插件不假定物种或部位。
/// 这些骨骼每帧会被写成「静息旋转 + 正弦」，不要再给它们加旋转轨道。
/// </summary>
public sealed class SpineSineDefinition
{
    public required IReadOnlyList<string> BoneNames { get; init; }

    public float AmplitudeRadians { get; init; } = 0.18f;

    public float FrequencyHz { get; init; } = 1.4f;

    /// <summary>沿 <see cref="BoneNames"/> 依次增加的相位，用来把摆动传下去。</summary>
    public float PhaseStepRadians { get; init; } = 0.8f;
}

/// <summary>建骨架用的数据。骨骼、槽位、片段名都由调用方决定。</summary>
public sealed class CutoutRigDefinition
{
    public required IReadOnlyList<CutoutBoneDefinition> Bones { get; init; }

    public required IReadOnlyList<CutoutSlotDefinition> Slots { get; init; }

    public SpineSineDefinition? SpineSine { get; init; }
}

/// <summary>一根骨骼上的旋转关键帧。时间单位是秒，角度是弧度。</summary>
public readonly struct CutoutRotationKey
{
    public CutoutRotationKey(string boneName, double time, float radians)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            throw new ArgumentException("骨骼名不能为空。", nameof(boneName));
        BoneName = boneName;
        Time = time;
        Radians = radians;
    }

    public string BoneName { get; }

    public double Time { get; }

    public float Radians { get; }
}
