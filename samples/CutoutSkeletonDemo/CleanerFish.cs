using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 正式的青金清洁鱼。60×32 是对战和换装的默认皮，98×52 是换装特写。
/// 裁切和轴点都是源图像素。每张部件 PNG 带 1px 透明边，左上角是 pngOrigin，可以是负数。
/// 槽位按居中精灵摆：左上角落在 pngOrigin，不能把 PNG 左上角当成裁切原点，也不改高度。
/// 臀鳍和小后背鳍都挂在已有的 SpineTail 上。没有单独的腹鳍，也没有背肌条。
/// </summary>
internal static class CleanerFish
{
    public const string SpeciesName = "Cleaner";

    public const string DisplayName = "清洁鱼";

    public const string GameId = "60x32";

    public const string CloseupId = "98x52";

    public const string SlotAnal = "Anal";

    public const string SlotFinlet = "Finlet";

    public const float LabScale = 4f;

    public const float CombatScale = 2f;

    private static CleanerSlice? _game;

    private static CleanerSlice? _closeup;

    public static CleanerSlice Game => _game ??= CleanerSlice.Load("res://Art/Cleaner/60x32");

    public static CleanerSlice Closeup => _closeup ??= CleanerSlice.Load("res://Art/Cleaner/98x52");

    public static CutoutRigDefinition Definition(CleanerSlice slice, float sineFrequencyHz = OpenOceanFish.SineFrequency)
    {
        ArgumentNullException.ThrowIfNull(slice);
        return new CutoutRigDefinition
        {
            Bones = slice.Bones,
            Slots =
            [
                OpenOceanFish.SkinSlot(OpenOceanFish.SlotBody, -5, slice.Texture(OpenOceanFish.SlotBody), slice.BodySkin),
                OpenOceanFish.Slot(OpenOceanFish.SlotTail, OpenOceanFish.BoneTail, slice.SlotPosition(OpenOceanFish.SlotTail), 1, slice.Texture(OpenOceanFish.SlotTail)),
                OpenOceanFish.Slot(SlotAnal, OpenOceanFish.BoneSpineTail, slice.SlotPosition(SlotAnal), 2, slice.Texture(SlotAnal)),
                OpenOceanFish.Slot(OpenOceanFish.SlotPectoral, OpenOceanFish.BonePectoral, slice.SlotPosition(OpenOceanFish.SlotPectoral), 3, slice.Texture(OpenOceanFish.SlotPectoral)),
                OpenOceanFish.Slot(OpenOceanFish.SlotDorsal, OpenOceanFish.BoneDorsal, slice.SlotPosition(OpenOceanFish.SlotDorsal), 4, slice.Texture(OpenOceanFish.SlotDorsal)),
                OpenOceanFish.Slot(SlotFinlet, OpenOceanFish.BoneSpineTail, slice.SlotPosition(SlotFinlet), 5, slice.Texture(SlotFinlet)),
                OpenOceanFish.Slot(OpenOceanFish.SlotJaw, OpenOceanFish.BoneJaw, slice.SlotPosition(OpenOceanFish.SlotJaw), 6, slice.Texture(OpenOceanFish.SlotJaw)),
                OpenOceanFish.Slot(OpenOceanFish.SlotHead, OpenOceanFish.BoneHead, slice.SlotPosition(OpenOceanFish.SlotHead), 7, slice.Texture(OpenOceanFish.SlotHead))
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

/// <summary>一份分辨率的切片。运行时只读清单和 PNG，不重切。</summary>
internal sealed class CleanerSlice
{
    private readonly Dictionary<string, Vector2> _pngOrigin;
    private readonly Dictionary<string, Vector2> _pivot;
    private readonly Dictionary<string, Vector2> _cropSize;
    private readonly Dictionary<string, string> _partBones;
    private readonly Dictionary<string, Vector2> _imagePoints;
    private readonly Dictionary<string, Texture2D> _textures;

    private CleanerSlice(
        string id,
        Dictionary<string, Vector2> pngOrigin,
        Dictionary<string, Vector2> pivot,
        Dictionary<string, Vector2> cropSize,
        Dictionary<string, string> partBones,
        Dictionary<string, Vector2> imagePoints,
        Dictionary<string, Texture2D> textures,
        IReadOnlyList<CutoutBoneDefinition> bones,
        CutoutSkinDefinition bodySkin,
        bool jawHingeIsOutsideOpaque)
    {
        Id = id;
        _pngOrigin = pngOrigin;
        _pivot = pivot;
        _cropSize = cropSize;
        _partBones = partBones;
        _imagePoints = imagePoints;
        _textures = textures;
        Bones = bones;
        BodySkin = bodySkin;
        JawHingeIsOutsideOpaque = jawHingeIsOutsideOpaque;
    }

    public string Id { get; }

    public IReadOnlyList<CutoutBoneDefinition> Bones { get; }

    public CutoutSkinDefinition BodySkin { get; }

    /// <summary>98×52 的下颌轴点落在归头部的口裂上，下颌贴图这一格是透明的。不能收到不透明包围盒上。</summary>
    public bool JawHingeIsOutsideOpaque { get; }

    public Texture2D Texture(string part) => _textures[part];

    public Vector2 PngOrigin(string part) => _pngOrigin[part];

    public Vector2 Pivot(string part) => _pivot[part];

    public Vector2 CropSize(string part) => _cropSize[part];

    public Vector2 ImagePoint(string bone) => _imagePoints[bone];

    /// <summary>居中精灵的局部位置。源像素 S 的左上角落到骨架上的 S。</summary>
    public Vector2 SlotPosition(string part)
    {
        var bone = _partBones[part];
        var size = _textures[part].GetSize();
        return _pngOrigin[part] - _imagePoints[bone] + (size * 0.5f);
    }

    public static CleanerSlice Load(string directory)
    {
        var text = Godot.FileAccess.GetFileAsString(directory + "/manifest.json");
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException($"读不到清洁鱼切片表：{directory}。");
        var root = Json.ParseString(text).AsGodotDictionary();
        var id = root["id"].AsString();
        var parts = root["parts"].AsGodotDictionary();
        var pngOrigin = new Dictionary<string, Vector2>();
        var pivot = new Dictionary<string, Vector2>();
        var cropSize = new Dictionary<string, Vector2>();
        var textures = new Dictionary<string, Texture2D>();
        var bodyWidth = 0;
        var bodyHeight = 0;
        var jawOutside = false;
        foreach (var key in parts.Keys)
        {
            var name = key.AsString();
            var part = parts[key].AsGodotDictionary();
            var image = LoadImage(directory + "/" + part["file"].AsString());
            RejectLegacyColors(image, name);
            var origin = Point(part["pngOrigin"]);
            var crop = part["crop"].AsGodotArray();
            var size = new Vector2(Number(crop[2]), Number(crop[3]));
            if (image.GetWidth() != size.X + 2 || image.GetHeight() != size.Y + 2)
                throw new InvalidOperationException($"{id} 的 {name} 没有保住 1px 透明边。");
            textures[name] = ImageTexture.CreateFromImage(image);
            pngOrigin[name] = origin;
            pivot[name] = Point(part["pivot"]);
            cropSize[name] = size;
            if (name == OpenOceanFish.SlotBody)
            {
                bodyWidth = image.GetWidth();
                bodyHeight = image.GetHeight();
            }
            if (name == OpenOceanFish.SlotJaw)
                jawOutside = JawHingeIsClear(image, pivot[name] - origin);
        }

        if (bodyWidth < 3 || bodyHeight < 3)
            throw new InvalidOperationException($"{id} 没有身体。");

        var bodyPivot = pivot[OpenOceanFish.SlotBody];
        var imagePoints = new Dictionary<string, Vector2>
        {
            [OpenOceanFish.BoneSpineFront] = bodyPivot,
            [OpenOceanFish.BoneHead] = pivot[OpenOceanFish.SlotHead],
            [OpenOceanFish.BoneJaw] = pivot[OpenOceanFish.SlotJaw],
            [OpenOceanFish.BoneRidge] = pivot[OpenOceanFish.SlotDorsal],
            [OpenOceanFish.BoneDorsal] = pivot[OpenOceanFish.SlotDorsal],
            [OpenOceanFish.BonePectoral] = pivot[OpenOceanFish.SlotPectoral],
            [OpenOceanFish.BoneSpineMid] = new Vector2((bodyPivot.X + pivot[CleanerFish.SlotAnal].X) * 0.5f, bodyPivot.Y),
            [OpenOceanFish.BoneSpineTail] = new Vector2(pivot[CleanerFish.SlotAnal].X, bodyPivot.Y),
            [OpenOceanFish.BoneTail] = pivot[OpenOceanFish.SlotTail]
        };
        var partBones = new Dictionary<string, string>
        {
            [OpenOceanFish.SlotHead] = OpenOceanFish.BoneHead,
            [OpenOceanFish.SlotJaw] = OpenOceanFish.BoneJaw,
            [OpenOceanFish.SlotDorsal] = OpenOceanFish.BoneDorsal,
            [OpenOceanFish.SlotPectoral] = OpenOceanFish.BonePectoral,
            [OpenOceanFish.SlotTail] = OpenOceanFish.BoneTail,
            [CleanerFish.SlotAnal] = OpenOceanFish.BoneSpineTail,
            [CleanerFish.SlotFinlet] = OpenOceanFish.BoneSpineTail
        };
        var bones = new List<CutoutBoneDefinition>
        {
            Bone(OpenOceanFish.BoneSpineFront, null, imagePoints),
            Bone(OpenOceanFish.BoneHead, OpenOceanFish.BoneSpineFront, imagePoints),
            Bone(OpenOceanFish.BoneJaw, OpenOceanFish.BoneHead, imagePoints),
            Bone(OpenOceanFish.BoneRidge, OpenOceanFish.BoneSpineFront, imagePoints),
            Bone(OpenOceanFish.BoneDorsal, OpenOceanFish.BoneSpineFront, imagePoints),
            Bone(OpenOceanFish.BonePectoral, OpenOceanFish.BoneSpineFront, imagePoints),
            Bone(OpenOceanFish.BoneSpineMid, OpenOceanFish.BoneSpineFront, imagePoints),
            Bone(OpenOceanFish.BoneSpineTail, OpenOceanFish.BoneSpineMid, imagePoints),
            Bone(OpenOceanFish.BoneTail, OpenOceanFish.BoneSpineTail, imagePoints)
        };
        return new CleanerSlice(
            id,
            pngOrigin,
            pivot,
            cropSize,
            partBones,
            imagePoints,
            textures,
            bones,
            Skin(bodyWidth, bodyHeight, pngOrigin[OpenOceanFish.SlotBody], bodyPivot, imagePoints),
            jawOutside);
    }

    private static bool JawHingeIsClear(Image image, Vector2 texel)
    {
        if (texel.X < 0f || texel.Y < 0f || texel.X >= image.GetWidth() || texel.Y >= image.GetHeight())
            return true;
        return image.GetPixel((int)texel.X, (int)texel.Y).A <= 0.1f;
    }

    private static CutoutBoneDefinition Bone(string name, string? parent, Dictionary<string, Vector2> imagePoints)
    {
        var local = parent == null ? Vector2.Zero : imagePoints[name] - imagePoints[parent];
        return new CutoutBoneDefinition
        {
            Name = name,
            Parent = parent,
            Position = local,
            Rotation = 0f,
            Length = 12f
        };
    }

    /// <summary>
    /// 身体用整张贴图的矩形，沿 X 加密顶点。断开的补色像素也在这张图里。
    /// 透明边不显形。静息时顶点落在源图格子上，和居中精灵同一套坐标。
    /// </summary>
    private static CutoutSkinDefinition Skin(
        int width,
        int height,
        Vector2 pngOrigin,
        Vector2 anchor,
        Dictionary<string, Vector2> imagePoints)
    {
        var xs = new List<int>();
        for (var x = 0; x <= width; x += 2)
            xs.Add(x);
        if (xs[^1] != width)
            xs.Add(width);
        var count = xs.Count * 2;
        var vertices = new Vector2[count];
        var uv = new Vector2[count];
        var index = 0;
        foreach (var x in xs)
        {
            vertices[index] = pngOrigin + new Vector2(x, 0) - anchor;
            uv[index] = new Vector2(x, 0);
            index++;
        }

        for (var i = xs.Count - 1; i >= 0; i--)
        {
            var x = xs[i];
            vertices[index] = pngOrigin + new Vector2(x, height) - anchor;
            uv[index] = new Vector2(x, height);
            index++;
        }

        string[] names =
        [
            OpenOceanFish.BoneSpineFront,
            OpenOceanFish.BoneSpineMid,
            OpenOceanFish.BoneSpineTail,
            OpenOceanFish.BoneTail
        ];
        var radius = Mathf.Max(12f, width * 0.45f);
        var bones = new CutoutSkinBone[names.Length];
        for (var bone = 0; bone < names.Length; bone++)
        {
            var center = imagePoints[names[bone]].X - anchor.X;
            var weights = new float[count];
            for (var vertex = 0; vertex < count; vertex++)
                weights[vertex] = Falloff(vertices[vertex].X, center, radius);
            bones[bone] = new CutoutSkinBone { Name = names[bone], Weights = weights };
        }

        for (var vertex = 0; vertex < count; vertex++)
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

    private static float Falloff(float x, float center, float radius)
    {
        var t = Mathf.Clamp(1f - (Mathf.Abs(x - center) / radius), 0f, 1f);
        return t * t * (3f - (2f * t));
    }

    private static void RejectLegacyColors(Image image, string part)
    {
        var width = image.GetWidth();
        var height = image.GetHeight();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = image.GetPixel(x, y);
                if (pixel.A <= 0.1f)
                    continue;
                var red = Mathf.RoundToInt(pixel.R * 255f);
                var green = Mathf.RoundToInt(pixel.G * 255f);
                var blue = Mathf.RoundToInt(pixel.B * 255f);
                var legacy = (red == 0x5E && green == 0x8C && blue == 0x7A)
                    || (red == 0xA8 && green == 0x74 && blue == 0x3A);
                if (legacy)
                    throw new InvalidOperationException($"清洁鱼的 {part} 用了旧的橄榄或锈色。");
            }
        }
    }

    private static Image LoadImage(string path)
    {
        var image = new Image();
        var error = image.Load(path);
        if (error != Error.Ok || image.IsEmpty())
            throw new InvalidOperationException($"清洁鱼贴图读不出来：{path}（{error}）。");
        return image;
    }

    private static Vector2 Point(Variant value)
    {
        var array = value.AsGodotArray();
        return new Vector2(Number(array[0]), Number(array[1]));
    }

    private static float Number(Variant value) =>
        value.VariantType == Variant.Type.Float ? (float)value.AsDouble() : value.AsInt32();
}
