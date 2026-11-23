using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 身体蒙皮的静息外形。顶点和贴图共用这一套坐标，换档只换贴图像素。
/// 权重按脊柱在静息时的位置铺开，插件不知道这是鱼。
/// </summary>
internal static class OpenOceanBody
{
    public static readonly Vector2[] Vertices =
    [
        new(20, -8),
        new(6, -16),
        new(-8, -18),
        new(-22, -16),
        new(-36, -13),
        new(-48, -10),
        new(-58, -6),
        new(-68, -3),
        new(-68, 4),
        new(-58, 8),
        new(-48, 12),
        new(-36, 15),
        new(-22, 17),
        new(-8, 16),
        new(6, 13),
        new(20, 8)
    ];

    public static Vector2 Min { get; }

    public static Vector2 Size { get; }

    public static Vector2[] Uv { get; }

    public static int TextureWidth { get; }

    public static int TextureHeight { get; }

    static OpenOceanBody()
    {
        var min = Vertices[0];
        var max = Vertices[0];
        foreach (var vertex in Vertices)
        {
            min = min.Min(vertex);
            max = max.Max(vertex);
        }

        Min = min;
        Size = max - min;
        Uv = new Vector2[Vertices.Length];
        for (var i = 0; i < Vertices.Length; i++)
            Uv[i] = Vertices[i] - min;
        TextureWidth = Mathf.CeilToInt(Size.X);
        TextureHeight = Mathf.CeilToInt(Size.Y);
    }

    public static CutoutSkinDefinition CreateSkin()
    {
        var bones = new[]
        {
            Weights(OpenOceanFish.BoneSpineFront, 0f),
            Weights(OpenOceanFish.BoneSpineMid, -24f),
            Weights(OpenOceanFish.BoneSpineTail, -42f),
            Weights(OpenOceanFish.BoneTail, -58f)
        };
        Normalize(bones);
        return new CutoutSkinDefinition
        {
            Vertices = Vertices,
            Uv = Uv,
            Bones = bones
        };
    }

    private static CutoutSkinBone Weights(string name, float centerX)
    {
        var weights = new float[Vertices.Length];
        for (var i = 0; i < weights.Length; i++)
            weights[i] = Falloff(Vertices[i].X, centerX);
        return new CutoutSkinBone { Name = name, Weights = weights };
    }

    private static float Falloff(float x, float center)
    {
        const float radius = 34f;
        var t = Mathf.Clamp(1f - Mathf.Abs(x - center) / radius, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static void Normalize(CutoutSkinBone[] bones)
    {
        for (var vertex = 0; vertex < Vertices.Length; vertex++)
        {
            var sum = 0f;
            foreach (var bone in bones)
                sum += bone.Weights[vertex];
            if (sum <= 0.0001f)
            {
                bones[0].Weights[vertex] = 1f;
                continue;
            }

            foreach (var bone in bones)
                bone.Weights[vertex] /= sum;
        }
    }
}
