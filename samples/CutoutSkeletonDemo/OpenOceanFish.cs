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
    public const float DisplayScale = 2f;

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
        BoneSpineFront,
        BoneSpineMid,
        BoneSpineTail,
        BoneTail
    ];

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

    public static CutoutRigDefinition Definition(OpenOceanParts parts, int mouth, int body, int fin) => new()
    {
        Bones =
        [
            Bone(BoneSpineFront, null, new Vector2(0, 0), 40f),
            Bone(BoneHead, BoneSpineFront, new Vector2(42, -1), 28f),
            Bone(BoneJaw, BoneHead, new Vector2(14, 8), 18f),
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
            Slot(SlotBody, BoneSpineFront, new Vector2(-6, 1), 1, parts.Get(SlotBody, body)),
            Slot(SlotRidge, BoneRidge, new Vector2(-4, 0), 2, parts.Get(SlotRidge, body)),
            Slot(SlotPectoral, BonePectoral, new Vector2(-2, 6), 3, parts.Get(SlotPectoral, fin)),
            Slot(SlotDorsal, BoneDorsal, new Vector2(0, -14), 4, parts.Get(SlotDorsal, fin)),
            Slot(SlotHead, BoneHead, new Vector2(8, 0), 5, parts.Get(SlotHead, mouth)),
            Slot(SlotJaw, BoneJaw, new Vector2(6, 1), 6, parts.Get(SlotJaw, mouth))
        ],
        SpineSine = new SpineSineDefinition
        {
            BoneNames = SineBones,
            AmplitudeRadians = 0.11f,
            FrequencyHz = 1.3f,
            PhaseStepRadians = 0.6f
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

    public static void AddSharedClips(CutoutRig rig)
    {
        rig.AddClip(SwimClip, rig.CreateRotationClip(1.2d, loop: true,
        [
            new(BoneDorsal, 0d, 0f),
            new(BoneDorsal, 0.3d, 0.18f),
            new(BoneDorsal, 0.6d, -0.1f),
            new(BoneDorsal, 0.9d, 0.16f),
            new(BoneDorsal, 1.2d, 0f),
            new(BonePectoral, 0d, 0.2f),
            new(BonePectoral, 0.3d, -0.28f),
            new(BonePectoral, 0.6d, 0.32f),
            new(BonePectoral, 0.9d, -0.06f),
            new(BonePectoral, 1.2d, 0.2f)
        ]));

        rig.AddClip(BiteClip, rig.CreateRotationClip(0.34d, loop: false,
        [
            new(BoneJaw, 0d, 0f),
            new(BoneJaw, 0.12d, 0.72f),
            new(BoneJaw, 0.34d, 0f),
            new(BoneHead, 0d, 0f),
            new(BoneHead, 0.12d, -0.1f),
            new(BoneHead, 0.34d, 0f)
        ]));
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
}
