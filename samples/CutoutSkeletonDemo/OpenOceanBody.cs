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
        RidgeUv = UvFor(RidgeVertices);
        var ridgeMax = RidgeUv[0];
        foreach (var point in RidgeUv)
            ridgeMax = ridgeMax.Max(point);
        RidgeTextureWidth = Mathf.CeilToInt(ridgeMax.X);
        RidgeTextureHeight = Mathf.CeilToInt(ridgeMax.Y);
    }

    private static Vector2[] UvFor(Vector2[] vertices)
    {
        var min = vertices[0];
        foreach (var vertex in vertices)
            min = min.Min(vertex);
        var uv = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            uv[i] = vertices[i] - min;
        return uv;
    }

    public static readonly Vector2[] RidgeVertices =
    [
        new(14, -12),
        new(2, -22),
        new(-16, -24),
        new(-36, -20),
        new(-54, -14),
        new(-66, -8),
        new(-66, -2),
        new(-54, -6),
        new(-36, -12),
        new(-16, -16),
        new(2, -14),
        new(14, -8)
    ];

    public static Vector2[] RidgeUv { get; }

    public static int RidgeTextureWidth { get; }

    public static int RidgeTextureHeight { get; }

    public static CutoutSkinDefinition CreateSkin() => SkinAlongSpine(Vertices, Uv);

    public static CutoutSkinDefinition CreateRidgeSkin() => SkinAlongSpine(RidgeVertices, RidgeUv);

    private static CutoutSkinDefinition SkinAlongSpine(Vector2[] vertices, Vector2[] uv)
    {
        var bones = new[]
        {
            Weights(vertices, OpenOceanFish.BoneSpineFront, 0f),
            Weights(vertices, OpenOceanFish.BoneSpineMid, -24f),
            Weights(vertices, OpenOceanFish.BoneSpineTail, -42f),
            Weights(vertices, OpenOceanFish.BoneTail, -58f)
        };
        Normalize(bones);
        return new CutoutSkinDefinition
        {
            Vertices = vertices,
            Uv = uv,
            Bones = bones
        };
    }

    private static CutoutSkinBone Weights(Vector2[] vertices, string name, float centerX)
    {
        var weights = new float[vertices.Length];
        for (var i = 0; i < weights.Length; i++)
            weights[i] = Falloff(vertices[i].X, centerX);
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
        for (var vertex = 0; vertex < bones[0].Weights.Length; vertex++)
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
