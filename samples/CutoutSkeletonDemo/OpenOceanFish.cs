using Godot;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 远洋流线型这一条物种的骨骼、槽位和片段。
/// 插件只收定义，不知道口、体、鳍怎么分组。
/// </summary>
internal static class OpenOceanFish
{
    public const string RootName = "OpenOceanFish";
    public const string DisplayName = "远洋流线型";
    public const string SwimClip = "swim";
    public const string BiteClip = "bite";
    public const string LungeClip = "lunge";
    public const string TailSwipeClip = "tail_swipe";
    public const string RecoilClip = "recoil";
    public const double SwimLength = 1.2;
    public const double BiteLength = 0.34;
    public const double LungeLength = 0.42;
    public const double TailSwipeLength = 0.48;
    public const double RecoilLength = 0.28;
    public const float DisplayScale = 2f;
    public const float SineAmplitude = 0.05f;
    public const float SineFrequency = 1.35f;
    public const float SinePhaseStep = 0.72f;

    public static readonly string[] AttackClips = [BiteClip, LungeClip, TailSwipeClip];

    public const string BoneSpineFront = "SpineFront";
    public const string BoneHead = "Head";
    public const string BoneJaw = "Jaw";
    public const string BoneRidge = "Ridge";
    public const string BoneDorsal = "Dorsal";
    public const string BonePectoral = "Pectoral";
    public const string BoneSpineMid = "SpineMid";
    public const string BoneSpineTail = "SpineTail";
    public const string BoneTail = "Tail";

    public const string SlotJaw = "Jaw";
    public const string SlotHead = "Head";
    public const string SlotBody = "Body";
    public const string SlotRidge = "Ridge";
    public const string SlotDorsal = "Dorsal";
    public const string SlotPectoral = "Pectoral";
    public const string SlotTail = "Tail";

    public static readonly string[] SineBones =
    [
        BoneHead,
        BoneSpineFront,
        BoneSpineMid,
        BoneSpineTail,
        BoneTail,
        BoneDorsal,
        BonePectoral
    ];

    /// <summary>和 <see cref="SineBones"/> 对齐。头和鳍是 0，旋转留给片段；脊柱越靠尾越大，但整体很小。</summary>
    public static readonly float[] SineAmplitudeScales = [0f, 0.7f, 1.05f, 1.45f, 1.9f, 0f, 0f];

    /// <summary>和 <see cref="SineBones"/> 对齐。尾鳍收到约 0.35，尾段少一些，鳍是很小的反向。</summary>
    public static readonly float[] SineScaleX = [0f, 0.05f, 0.12f, 0.28f, 0.65f, -0.07f, -0.07f];

    public static readonly string[] SlotIds =
    [
        SlotJaw,
        SlotHead,
        SlotBody,
        SlotRidge,
        SlotDorsal,
        SlotPectoral,
        SlotTail
    ];

    public static CutoutRigDefinition Definition(
        OpenOceanParts parts,
        int mouth,
        int body,
        int fin,
        float sineFrequencyHz = SineFrequency) => new()
    {
        Bones =
        [
            Bone(BoneSpineFront, null, new Vector2(0, 0), 40f),
            Bone(BoneHead, BoneSpineFront, new Vector2(18, 0), 28f),
            Bone(BoneJaw, BoneHead, new Vector2(16, 7), 18f),
            Bone(BoneRidge, BoneSpineFront, new Vector2(-18, -14), 44f),
            Bone(BoneDorsal, BoneSpineFront, new Vector2(-6, -18), 18f),
            Bone(BonePectoral, BoneSpineFront, new Vector2(4, 12), 16f),
            Bone(BoneSpineMid, BoneSpineFront, new Vector2(-24, 0), 20f),
            Bone(BoneSpineTail, BoneSpineMid, new Vector2(-18, 0), 16f),
            Bone(BoneTail, BoneSpineTail, new Vector2(-16, 0), 18f)
        ],
        Slots =
        [
            Slot(SlotTail, BoneTail, new Vector2(-4, 0), 0, parts.Get(SlotTail, fin)),
            SkinSlot(SlotBody, -5, parts.Get(SlotBody, body), OpenOceanBody.CreateSkin()),
            SkinSlot(SlotRidge, -4, parts.Get(SlotRidge, body), OpenOceanBody.CreateRidgeSkin()),
            Slot(SlotPectoral, BonePectoral, new Vector2(-2, 6), 3, parts.Get(SlotPectoral, fin)),
            Slot(SlotDorsal, BoneDorsal, new Vector2(0, -14), 4, parts.Get(SlotDorsal, fin)),
            Slot(SlotHead, BoneHead, new Vector2(24, 0), 5, parts.Get(SlotHead, mouth)),
            Slot(SlotJaw, BoneJaw, new Vector2(14, 1), 6, parts.Get(SlotJaw, mouth))
        ],
        SpineSine = new SpineSineDefinition
        {
            BoneNames = SineBones,
            AmplitudeRadians = SineAmplitude,
            FrequencyHz = sineFrequencyHz,
            PhaseStepRadians = SinePhaseStep,
            AmplitudeScales = SineAmplitudeScales,
            ScaleXAmplitudes = SineScaleX
        }
    };

    public static void Apply(CutoutRig rig, OpenOceanParts parts, int mouth, int body, int fin)
    {
        rig.SetSlotTexture(SlotHead, parts.Get(SlotHead, mouth));
        rig.SetSlotTexture(SlotJaw, parts.Get(SlotJaw, mouth));
        rig.SetSlotTexture(SlotBody, parts.Get(SlotBody, body));
        rig.SetSlotTexture(SlotRidge, parts.Get(SlotRidge, body));
        rig.SetSlotTexture(SlotDorsal, parts.Get(SlotDorsal, fin));
        rig.SetSlotTexture(SlotPectoral, parts.Get(SlotPectoral, fin));
        rig.SetSlotTexture(SlotTail, parts.Get(SlotTail, fin));
    }

    /// <summary>这些片段要写到正弦骨骼上。播放时先关掉这条鱼的正弦，结束后再打开。</summary>
    public static bool SuspendsSpineSine(string clip) =>
        clip == LungeClip || clip == TailSwipeClip || clip == RecoilClip;

    public static void AddSharedClips(CutoutRig rig) => BindClipSet(rig, CreateClipSet(rig));

    /// <summary>
    /// 用这套骨骼路径造一份片段。多条鱼应共用返回的实例，不要按鱼再造一份。
    /// 路径只含骨架内的骨骼名，同结构的骨架可以播同一份 <see cref="Animation"/>。
    /// </summary>
    public static OpenOceanClipSet CreateClipSet(CutoutRig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        return new OpenOceanClipSet
        {
            Swim = rig.CreateRotationClip(SwimLength, loop: true,
            [
                new(BoneDorsal, 0d, 0f),
                new(BoneDorsal, 0.3d, 0.18f),
                new(BoneDorsal, 0.6d, -0.1f),
                new(BoneDorsal, 0.9d, 0.16f),
                new(BoneDorsal, SwimLength, 0f),
                new(BonePectoral, 0d, 0.2f),
                new(BonePectoral, 0.3d, -0.28f),
                new(BonePectoral, 0.6d, 0.32f),
                new(BonePectoral, 0.9d, -0.06f),
                new(BonePectoral, SwimLength, 0.2f)
            ]),
            Bite = rig.CreateRotationClip(BiteLength, loop: false,
            [
                new(BoneJaw, 0d, 0f),
                new(BoneJaw, 0.12d, 0.72f),
                new(BoneJaw, BiteLength, 0f)
            ]),
            Lunge = rig.CreateRotationClip(LungeLength, loop: false,
            [
                new(BoneHead, 0d, 0f),
                new(BoneHead, 0.14d, -0.42f),
                new(BoneHead, LungeLength, 0f),
                new(BoneJaw, 0d, 0f),
                new(BoneJaw, 0.16d, 0.85f),
                new(BoneJaw, 0.30d, 0.2f),
                new(BoneJaw, LungeLength, 0f),
                new(BoneSpineFront, 0d, 0f),
                new(BoneSpineFront, 0.14d, -0.28f),
                new(BoneSpineFront, LungeLength, 0f)
            ]),
            TailSwipe = rig.CreateRotationClip(TailSwipeLength, loop: false,
            [
                new(BoneTail, 0d, 0f),
                new(BoneTail, 0.14d, 0.95f),
                new(BoneTail, 0.30d, -0.85f),
                new(BoneTail, TailSwipeLength, 0f),
                new(BoneSpineTail, 0d, 0f),
                new(BoneSpineTail, 0.14d, 0.42f),
                new(BoneSpineTail, 0.30d, -0.38f),
                new(BoneSpineTail, TailSwipeLength, 0f)
            ]),
            Recoil = rig.CreateRotationClip(RecoilLength, loop: false,
            [
                new(BoneHead, 0d, 0f),
                new(BoneHead, 0.08d, 0.38f),
                new(BoneHead, RecoilLength, 0f),
                new(BoneJaw, 0d, 0f),
                new(BoneJaw, 0.10d, -0.15f),
                new(BoneJaw, RecoilLength, 0f)
            ])
        };
    }

    public static void BindClipSet(CutoutRig rig, OpenOceanClipSet set)
    {
        ArgumentNullException.ThrowIfNull(rig);
        ArgumentNullException.ThrowIfNull(set);
        Add(rig, SwimClip, set.Swim);
        Add(rig, BiteClip, set.Bite);
        Add(rig, LungeClip, set.Lunge);
        Add(rig, TailSwipeClip, set.TailSwipe);
        Add(rig, RecoilClip, set.Recoil);
    }

    private static void Add(CutoutRig rig, string name, Animation clip)
    {
        if (!rig.HasClip(name))
            rig.AddClip(name, clip);
    }

    private static CutoutBoneDefinition Bone(string name, string? parent, Vector2 position, float length) => new()
    {
        Name = name,
        Parent = parent,
        Position = position,
        Rotation = 0f,
        Length = length
    };

    private static CutoutSlotDefinition Slot(string id, string bone, Vector2 position, int zIndex, Texture2D texture) => new()
    {
        Id = id,
        Bone = bone,
        Position = position,
        ZIndex = zIndex,
        Texture = texture
    };

    private static CutoutSlotDefinition SkinSlot(string id, int zIndex, Texture2D texture, CutoutSkinDefinition skin) => new()
    {
        Id = id,
        ZIndex = zIndex,
        Texture = texture,
        Skin = skin
    };
}

/// <summary>一条物种共用的片段实例。换贴图不得重新构造。</summary>
internal sealed class OpenOceanClipSet
{
    public required Animation Swim { get; init; }

    public required Animation Bite { get; init; }

    public required Animation Lunge { get; init; }

    public required Animation TailSwipe { get; init; }

    public required Animation Recoil { get; init; }

    public Animation Get(string name) => name switch
    {
        OpenOceanFish.SwimClip => Swim,
        OpenOceanFish.BiteClip => Bite,
        OpenOceanFish.LungeClip => Lunge,
        OpenOceanFish.TailSwipeClip => TailSwipe,
        OpenOceanFish.RecoilClip => Recoil,
        _ => throw new ArgumentException($"没有片段 {name}。", nameof(name))
    };
}
