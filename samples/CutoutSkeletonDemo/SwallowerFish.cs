using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 吞食者。贴图来自切好的像素图，骨骼名和片段与远洋流线型相同。
/// 背肌没有单独的脊线，Ridge 槽位是一张空贴图。
/// </summary>
internal static class SwallowerFish
{
    public const string SpeciesName = "Swallower";

    public const string DisplayName = "吞食者";

    public const float DisplayScale = 2f;

    public const float CombatScale = 1f;

    private static SwallowerSlice? _slice;

    public static SwallowerSlice Slice => _slice ??= SwallowerSlice.Load();

    public static CutoutRigDefinition Definition(float sineFrequencyHz = OpenOceanFish.SineFrequency)
    {
        var slice = Slice;
        return new CutoutRigDefinition
        {
            Bones = slice.Bones,
            Slots =
            [
                OpenOceanFish.SkinSlot(OpenOceanFish.SlotBody, -5, slice.Body, slice.BodySkin),
                OpenOceanFish.Slot(OpenOceanFish.SlotTail, OpenOceanFish.BoneTail, slice.SpritePosition(OpenOceanFish.SlotTail), 1, slice.Tail),
                OpenOceanFish.Slot(OpenOceanFish.SlotRidge, OpenOceanFish.BoneRidge, Vector2.Zero, 2, slice.Ridge),
                OpenOceanFish.Slot(OpenOceanFish.SlotPectoral, OpenOceanFish.BonePectoral, slice.SpritePosition(OpenOceanFish.SlotPectoral), 3, slice.Pectoral),
                OpenOceanFish.Slot(OpenOceanFish.SlotDorsal, OpenOceanFish.BoneDorsal, slice.SpritePosition(OpenOceanFish.SlotDorsal), 4, slice.Dorsal),
                OpenOceanFish.Slot(OpenOceanFish.SlotHead, OpenOceanFish.BoneHead, slice.SpritePosition(OpenOceanFish.SlotHead), 5, slice.Head),
                OpenOceanFish.Slot(OpenOceanFish.SlotJaw, OpenOceanFish.BoneJaw, slice.SpritePosition(OpenOceanFish.SlotJaw), 6, slice.Jaw)
            ],
            SpineSine = new SpineSineDefinition
            {
                BoneNames = OpenOceanFish.SineBones,
                AmplitudeRadians = OpenOceanFish.SineAmplitude,
                FrequencyHz = sineFrequencyHz,
                PhaseStepRadians = OpenOceanFish.SinePhaseStep,
                AmplitudeScales = OpenOceanFish.SineAmplitudeScales,
                ScaleXAmplitudes = OpenOceanFish.SineScaleX
            }
        };
    }
}

/// <summary>切片表和贴图。运行时不调用切片脚本。</summary>
internal sealed class SwallowerSlice
{
    private readonly Dictionary<string, Vector2> _spritePositions;
    private readonly Dictionary<string, Vector2> _imagePoints;

    private SwallowerSlice(
        Dictionary<string, Vector2> imagePoints,
        Dictionary<string, Vector2> spritePositions,
        IReadOnlyList<CutoutBoneDefinition> bones,
        Texture2D head,
        Texture2D jaw,
        Texture2D body,
        Texture2D dorsal,
        Texture2D pectoral,
        Texture2D tail,
        Texture2D ridge,
        CutoutSkinDefinition bodySkin)
    {
        _imagePoints = imagePoints;
        _spritePositions = spritePositions;
        Bones = bones;
        Head = head;
        Jaw = jaw;
        Body = body;
        Dorsal = dorsal;
        Pectoral = pectoral;
        Tail = tail;
        Ridge = ridge;
        BodySkin = bodySkin;
    }

    public IReadOnlyList<CutoutBoneDefinition> Bones { get; }

    public Texture2D Head { get; }

    public Texture2D Jaw { get; }

    public Texture2D Body { get; }

    public Texture2D Dorsal { get; }

    public Texture2D Pectoral { get; }

    public Texture2D Tail { get; }

    public Texture2D Ridge { get; }

    public CutoutSkinDefinition BodySkin { get; }

    public Vector2 SpritePosition(string slot) => _spritePositions[slot];

    public Vector2 ImagePoint(string bone) => _imagePoints[bone];

    public static SwallowerSlice Load()
    {
        var text = Godot.FileAccess.GetFileAsString("res://Art/Swallower/slices.json");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("读不到吞食者切片表。");
        var root = Json.ParseString(text).AsGodotDictionary();
        var anchor = Point(root["anchor"].AsGodotDictionary()["image"]);
        var parts = root["parts"].AsGodotDictionary();
        var textures = new Dictionary<string, Texture2D>();
        var spritePositions = new Dictionary<string, Vector2>();
        foreach (var key in parts.Keys)
        {
            var name = key.AsString();
            var part = parts[key].AsGodotDictionary();
            var texture = LoadTexture("res://Art/Swallower/" + part["file"].AsString());
            textures[name] = texture;
            var pivot = Point(part["pivotInTexture"]);
            var size = texture.GetSize();
            var local = new Vector2(pivot.X + 0.5f, pivot.Y + 0.5f) - (size * 0.5f);
            spritePositions[name] = -local;
        }

        var imagePoints = new Dictionary<string, Vector2>();
        var bones = new List<CutoutBoneDefinition>();
        foreach (var entry in root["bones"].AsGodotArray())
        {
            var bone = entry.AsGodotDictionary();
            var name = bone["name"].AsString();
            var image = Point(bone["image"]);
            imagePoints[name] = image;
            var parent = bone["parent"].VariantType == Variant.Type.Nil ? null : bone["parent"].AsString();
            var local = parent == null ? Vector2.Zero : image - imagePoints[parent];
            bones.Add(new CutoutBoneDefinition
            {
                Name = name,
                Parent = parent,
                Position = local,
                Rotation = 0f,
                Length = 16f
            });
        }

        var crop = Point(parts["Body"].AsGodotDictionary()["crop"]);
        var polygon = root["bodyPolygon"].AsGodotArray();
        var vertices = new Vector2[polygon.Count];
        var uv = new Vector2[polygon.Count];
        for (var i = 0; i < polygon.Count; i++)
        {
            var image = Point(polygon[i]);
            vertices[i] = image - anchor;
            uv[i] = image - crop;
        }

        var empty = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
        empty.Fill(new Color(0, 0, 0, 0));
        return new SwallowerSlice(
            imagePoints,
            spritePositions,
            bones,
            textures["Head"],
            textures["Jaw"],
            textures["Body"],
            textures["Dorsal"],
            textures["Pectoral"],
            textures["Tail"],
            ImageTexture.CreateFromImage(empty),
            Skin(vertices, uv, imagePoints, anchor));
    }

    private static CutoutSkinDefinition Skin(
        Vector2[] vertices,
        Vector2[] uv,
        Dictionary<string, Vector2> imagePoints,
        Vector2 anchor)
    {
        string[] names =
        [
            OpenOceanFish.BoneSpineFront,
            OpenOceanFish.BoneSpineMid,
            OpenOceanFish.BoneSpineTail,
            OpenOceanFish.BoneTail
        ];
        var bones = new CutoutSkinBone[names.Length];
        for (var bone = 0; bone < names.Length; bone++)
        {
            var center = imagePoints[names[bone]].X - anchor.X;
            var weights = new float[vertices.Length];
            for (var vertex = 0; vertex < vertices.Length; vertex++)
                weights[vertex] = Falloff(vertices[vertex].X, center);
            bones[bone] = new CutoutSkinBone { Name = names[bone], Weights = weights };
        }

        for (var vertex = 0; vertex < vertices.Length; vertex++)
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

        return new CutoutSkinDefinition
        {
            Vertices = vertices,
            Uv = uv,
            Bones = bones
        };
    }

    private static float Falloff(float x, float center)
    {
        const float radius = 30f;
        var t = Mathf.Clamp(1f - (Mathf.Abs(x - center) / radius), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static Texture2D LoadTexture(string path)
    {
        if (ResourceLoader.Exists(path))
        {
            var imported = ResourceLoader.Load<Texture2D>(path);
            if (imported != null && imported.GetWidth() > 0 && imported.GetHeight() > 0)
                return imported;
        }

        var image = new Image();
        var error = image.Load(path);
        if (error != Error.Ok || image.IsEmpty())
            throw new InvalidOperationException($"吞食者贴图读不出来：{path}（{error}）。");
        return ImageTexture.CreateFromImage(image);
    }

    private static Vector2 Point(Variant value)
    {
        var array = value.AsGodotArray();
        return new Vector2(Number(array[0]), Number(array[1]));
    }

    private static float Number(Variant value) =>
        value.VariantType == Variant.Type.Float ? (float)value.AsDouble() : value.AsInt32();
}
