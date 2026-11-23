using Godot;

namespace SlimFramework.CutoutSkeleton;

/// <summary>
/// 一套剪影骨架：内置 <see cref="Skeleton2D"/>、精灵或蒙皮多边形槽位、
/// 一台共用的 <see cref="AnimationPlayer"/>。换装只改槽位贴图。
/// 均匀尺寸写根节点缩放的绝对值，保留已经有的 Y 符号。
/// </summary>
public sealed class CutoutRig
{
    public const string SkeletonNodeName = "Skeleton";
    public const string AnimationPlayerNodeName = "AnimationPlayer";
    public const string DefaultRootName = "CutoutRoot";

    private readonly Dictionary<string, Bone2D> _bones;
    private readonly Dictionary<string, string> _paths;
    private readonly Dictionary<string, float> _restRotation;
    private readonly Dictionary<string, Vector2> _restScale;
    private readonly Dictionary<string, CanvasItem> _slots;
    private readonly AnimationLibrary _library;
    private readonly SpineSineDefinition? _sine;
    private double _sineTime;
    private bool _sineEnabled;
    private bool _lateQueued;

    private CutoutRig(
        Node2D root,
        Skeleton2D skeleton,
        AnimationPlayer player,
        AnimationLibrary library,
        Dictionary<string, Bone2D> bones,
        Dictionary<string, string> paths,
        Dictionary<string, float> restRotation,
        Dictionary<string, CanvasItem> slots,
        SpineSineDefinition? sine)
    {
        Root = root;
        Skeleton = skeleton;
        Player = player;
        _library = library;
        _bones = bones;
        _paths = paths;
        _restRotation = restRotation;
        _restScale = new Dictionary<string, Vector2>(bones.Count);
        foreach (var (name, bone) in bones)
            _restScale[name] = bone.Scale;
        _slots = slots;
        _sine = sine;
        _sineEnabled = sine != null;
        UniformScale = 1f;
    }

    public Node2D Root { get; }

    public Skeleton2D Skeleton { get; }

    public AnimationPlayer Player { get; }

    public int BoneCount => _bones.Count;

    public float UniformScale { get; private set; }

    public bool SpineSineEnabled => _sineEnabled;

    public IReadOnlyCollection<string> BoneNames => _bones.Keys;

    public IReadOnlyCollection<string> SlotIds => _slots.Keys;

    public string CurrentClip => Player.CurrentAnimation.ToString();

    /// <summary>
    /// 生成骨架并挂到 <paramref name="parent"/>。父骨骼必须先于子骨骼列出。
    /// 节点结构是 <c>根 / Skeleton / 骨骼...</c>，旁边一台 <c>AnimationPlayer</c>。
    /// </summary>
    public static CutoutRig Build(Node parent, CutoutRigDefinition definition, string? rootName = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(definition);
        var resolvedRoot = string.IsNullOrWhiteSpace(rootName) ? DefaultRootName : rootName;
        Validate(definition, resolvedRoot);

        Node2D? root = null;
        try
        {
            root = new Node2D { Name = resolvedRoot };
            var skeleton = new Skeleton2D { Name = SkeletonNodeName };
            root.AddChild(skeleton);

            var bones = new Dictionary<string, Bone2D>(definition.Bones.Count);
            var paths = new Dictionary<string, string>(definition.Bones.Count);
            var restRotation = new Dictionary<string, float>(definition.Bones.Count);
            foreach (var spec in definition.Bones)
            {
                var bone = new Bone2D
                {
                    Name = spec.Name,
                    Position = spec.Position,
                    Rotation = spec.Rotation
                };
                // Godot 4.7 里长度和自动计算是方法，不是属性。长度只影响骨头示意，不参与换装。
                bone.SetAutocalculateLengthAndAngle(false);
                bone.SetLength(spec.Length);
                if (spec.Parent == null)
                    skeleton.AddChild(bone);
                else
                    bones[spec.Parent].AddChild(bone);

                bone.Rest = bone.Transform;
                bones.Add(spec.Name, bone);
                var parentPath = spec.Parent == null ? SkeletonNodeName : paths[spec.Parent];
                paths.Add(spec.Name, $"{parentPath}/{spec.Name}");
                restRotation.Add(spec.Name, spec.Rotation);
            }

            var slots = new Dictionary<string, CanvasItem>(definition.Slots.Count);
            foreach (var spec in definition.Slots)
            {
                if (spec.Skin != null)
                {
                    var polygon = new Polygon2D
                    {
                        Name = $"Slot_{spec.Id}",
                        ZIndex = spec.ZIndex,
                        Color = Colors.White,
                        Polygon = spec.Skin.Vertices,
                        UV = spec.Skin.Uv ?? UvFromBounds(spec.Skin.Vertices),
                        Texture = spec.Texture,
                        Skeleton = new NodePath("..")
                    };
                    skeleton.AddChild(polygon);
                    foreach (var bone in spec.Skin.Bones)
                        polygon.AddBone(SkeletonBonePath(paths, bone.Name), bone.Weights);
                    slots.Add(spec.Id, polygon);
                    continue;
                }

                var sprite = new Sprite2D
                {
                    Name = $"Slot_{spec.Id}",
                    Position = spec.Position,
                    ZIndex = spec.ZIndex,
                    Centered = true,
                    Texture = spec.Texture
                };
                bones[spec.Bone!].AddChild(sprite);
                slots.Add(spec.Id, sprite);
            }

            var player = new AnimationPlayer
            {
                Name = AnimationPlayerNodeName,
                RootNode = new NodePath("..")
            };
            root.AddChild(player);
            var library = new AnimationLibrary();
            player.AddAnimationLibrary(string.Empty, library);

            parent.AddChild(root);
            // 进树之后按节点的局部变换再写一次静息，避免和进入树时的更新拧着。
            foreach (var bone in bones.Values)
                bone.Rest = bone.Transform;

            return new CutoutRig(root, skeleton, player, library, bones, paths, restRotation, slots, definition.SpineSine);
        }
        catch
        {
            if (root != null && GodotObject.IsInstanceValid(root))
            {
                if (root.GetParent() != null)
                    root.QueueFree();
                else
                    root.Free();
            }

            throw;
        }
    }

    /// <summary>
    /// 只改根节点缩放的绝对值。<c>(scale, ±scale)</c>，Y 的正负保持原样，不动骨骼的局部缩放。
    /// </summary>
    public void SetUniformScale(float scale)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));
        var ySign = Root.Scale.Y < 0f ? -1f : 1f;
        UniformScale = scale;
        Root.Scale = new Vector2(scale, ySign * scale);
    }

    /// <summary>只改这个槽位的贴图。精灵和蒙皮多边形都认。不改骨骼、静息姿势和动画库。</summary>
    public void SetSlotTexture(string slotId, Texture2D? texture)
    {
        var visual = GetSlotNode(slotId);
        if (!GodotObject.IsInstanceValid(visual))
            throw new ArgumentException($"没有槽位 {slotId}。", nameof(slotId));
        switch (visual)
        {
            case Sprite2D sprite:
                sprite.Texture = texture;
                break;
            case Polygon2D polygon:
                polygon.Texture = texture;
                break;
            default:
                throw new InvalidOperationException($"槽位 {slotId} 不能换贴图。");
        }
    }

    public Texture2D? GetSlotTexture(string slotId) => GetSlotNode(slotId) switch
    {
        Sprite2D sprite => sprite.Texture,
        Polygon2D polygon => polygon.Texture,
        _ => throw new InvalidOperationException($"槽位 {slotId} 没有贴图。")
    };

    public CanvasItem GetSlotNode(string slotId)
    {
        if (!_slots.TryGetValue(slotId, out var visual))
            throw new ArgumentException($"没有槽位 {slotId}。", nameof(slotId));
        return visual;
    }

    public bool IsSkinnedSlot(string slotId) => GetSlotNode(slotId) is Polygon2D;

    public Sprite2D GetSlot(string slotId)
    {
        if (GetSlotNode(slotId) is Sprite2D sprite)
            return sprite;
        throw new InvalidOperationException($"槽位 {slotId} 是蒙皮多边形，不是 Sprite2D。");
    }

    public Polygon2D GetSkinnedSlot(string slotId)
    {
        if (GetSlotNode(slotId) is Polygon2D polygon)
            return polygon;
        throw new InvalidOperationException($"槽位 {slotId} 不是蒙皮多边形。");
    }

    public Bone2D GetBone(string boneName)
    {
        if (!_bones.TryGetValue(boneName, out var bone))
            throw new ArgumentException($"没有骨骼 {boneName}。", nameof(boneName));
        return bone;
    }

    /// <summary>相对根节点的路径，例如 <c>Skeleton/Spine/Head</c>。</summary>
    public NodePath BonePath(string boneName)
    {
        if (!_paths.TryGetValue(boneName, out var path))
            throw new ArgumentException($"没有骨骼 {boneName}。", nameof(boneName));
        return new NodePath(path);
    }

    public bool HasClip(string name) => !string.IsNullOrWhiteSpace(name) && _library.HasAnimation(name);

    public Animation GetClip(string name)
    {
        if (!HasClip(name))
            throw new ArgumentException($"没有片段 {name}。", nameof(name));
        return _library.GetAnimation(name);
    }

    public void AddClip(string name, Animation animation)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("片段名不能为空。", nameof(name));
        ArgumentNullException.ThrowIfNull(animation);
        if (_library.HasAnimation(name))
            throw new InvalidOperationException($"片段 {name} 已存在。");
        _library.AddAnimation(name, animation);
    }

    /// <summary>按骨骼名写入旋转轨道。轨道指向骨骼，不指向槽位贴图。</summary>
    public Animation CreateRotationClip(double length, bool loop, IReadOnlyList<CutoutRotationKey> keys)
    {
        if (!double.IsFinite(length) || length <= 0d)
            throw new ArgumentOutOfRangeException(nameof(length));
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
            throw new ArgumentException("片段至少要一个关键帧。", nameof(keys));

        var animation = new Animation
        {
            Length = length,
            LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None
        };

        foreach (var group in keys.GroupBy(key => key.BoneName))
        {
            if (!_paths.TryGetValue(group.Key, out var path))
                throw new ArgumentException($"没有骨骼 {group.Key}。", nameof(keys));

            var track = animation.AddTrack(Animation.TrackType.Value);
            animation.TrackSetPath(track, new NodePath($"{path}:rotation"));
            animation.TrackSetInterpolationType(track, Animation.InterpolationType.Linear);
            double? previous = null;
            foreach (var key in group.OrderBy(key => key.Time))
            {
                if (!double.IsFinite(key.Time) || key.Time < 0d || key.Time > length)
                    throw new ArgumentOutOfRangeException(nameof(keys), $"关键帧时间超出片段：{key.BoneName} @ {key.Time}。");
                if (!float.IsFinite(key.Radians))
                    throw new ArgumentOutOfRangeException(nameof(keys), $"关键帧角度无效：{key.BoneName}。");
                if (previous == key.Time)
                    throw new ArgumentException($"同一骨骼的关键帧时间重复：{key.BoneName} @ {key.Time}。", nameof(keys));
                previous = key.Time;
                animation.TrackInsertKey(track, key.Time, key.Radians);
            }
        }

        return animation;
    }

    public void Play(string clipName)
    {
        if (!HasClip(clipName))
            throw new ArgumentException($"没有片段 {clipName}。", nameof(clipName));
        Player.Play(clipName);
    }

    public void SetSpineSineEnabled(bool enabled)
    {
        if (enabled && _sine == null)
            throw new InvalidOperationException("这个骨架没有正弦驱动。");
        if (!enabled && _sineEnabled)
            RestoreSineScale();
        _sineEnabled = enabled;
    }

    /// <summary>
    /// 推进正弦并写到指定骨骼的旋转，以及可选的局部 Scale.X。会再推迟到本帧空闲写一次，
    /// 以便落在 <see cref="Skeleton2D"/> 和 <see cref="AnimationPlayer"/> 的处理之后。
    /// 只覆盖正弦名单里的骨骼。
    /// 单套骨架自己推迟一次。<see cref="CutoutSkeletonModule"/> 的 <c>Tick</c> 不走这里的推迟，
    /// 而是把本帧全部骨架合成一次空闲写入，避免每套各排一个 <c>CallDeferred</c>。
    /// </summary>
    public void Advance(double delta)
    {
        if (!AdvanceImmediate(delta))
            return;
        QueueLateWrite();
    }

    /// <summary>推进并立刻写姿态，不为本骨架排队。给模块批量补写用。</summary>
    internal bool AdvanceImmediate(double delta)
    {
        if (!double.IsFinite(delta) || _sine == null || !_sineEnabled)
            return false;
        _sineTime += delta;
        WritePose();
        return true;
    }

    /// <summary>再写一次当前正弦。调用方负责把多套骨架合成到同一次空闲调用里。</summary>
    internal void WriteSpinePose() => WritePose();

    private void QueueLateWrite()
    {
        if (_lateQueued || !GodotObject.IsInstanceValid(Root) || !Root.IsInsideTree())
            return;
        _lateQueued = true;
        Callable.From(LateWrite).CallDeferred();
    }

    private void LateWrite()
    {
        _lateQueued = false;
        WritePose();
    }

    private void WritePose()
    {
        if (_sine == null || !_sineEnabled || !GodotObject.IsInstanceValid(Root))
            return;

        var time = (float)_sineTime;
        for (var i = 0; i < _sine.BoneNames.Count; i++)
        {
            var name = _sine.BoneNames[i];
            var bone = _bones[name];
            if (!GodotObject.IsInstanceValid(bone))
                continue;
            var phase = (time * _sine.FrequencyHz * Mathf.Tau) + (i * _sine.PhaseStepRadians);
            var amplitudeScale = _sine.AmplitudeScales == null ? 1f : _sine.AmplitudeScales[i];
            if (amplitudeScale != 0f)
            {
                var wave = _sine.AmplitudeRadians * amplitudeScale * Mathf.Sin(phase);
                bone.Rotation = _restRotation[name] + wave;
            }

            if (_sine.ScaleXAmplitudes == null)
                continue;
            var squeeze = 0.5f + (0.5f * Mathf.Sin(phase));
            var rest = _restScale[name];
            bone.Scale = new Vector2(rest.X * (1f - (_sine.ScaleXAmplitudes[i] * squeeze)), rest.Y);
        }
    }

    private void RestoreSineScale()
    {
        if (_sine?.ScaleXAmplitudes == null)
            return;
        for (var i = 0; i < _sine.BoneNames.Count; i++)
        {
            var name = _sine.BoneNames[i];
            var bone = _bones[name];
            if (GodotObject.IsInstanceValid(bone))
                bone.Scale = _restScale[name];
        }
    }

    private static void Validate(CutoutRigDefinition definition, string rootName)
    {
        RequireNodeName(rootName, nameof(rootName));
        if (definition.Bones == null || definition.Bones.Count == 0)
            throw new ArgumentException("至少要一根骨骼。", nameof(definition));
        if (definition.Slots == null)
            throw new ArgumentException("槽位列表不能为空。", nameof(definition));

        var seen = new HashSet<string>(definition.Bones.Count);
        foreach (var bone in definition.Bones)
        {
            if (bone == null)
                throw new ArgumentException("骨骼定义不能为空。", nameof(definition));
            RequireNodeName(bone.Name, nameof(definition));
            if (!seen.Add(bone.Name))
                throw new ArgumentException($"骨骼重名：{bone.Name}。", nameof(definition));
            if (bone.Parent != null && !seen.Contains(bone.Parent))
                throw new ArgumentException($"骨骼 {bone.Name} 的父节点必须先列出。", nameof(definition));
            if (!float.IsFinite(bone.Length) || bone.Length < 0f)
                throw new ArgumentOutOfRangeException(nameof(definition), $"骨骼长度无效：{bone.Name}。");
            if (!float.IsFinite(bone.Rotation) || !IsFinite(bone.Position))
                throw new ArgumentException($"骨骼变换无效：{bone.Name}。", nameof(definition));
        }

        var slotIds = new HashSet<string>(definition.Slots.Count);
        foreach (var slot in definition.Slots)
        {
            if (slot == null)
                throw new ArgumentException("槽位定义不能为空。", nameof(definition));
            RequireNodeName(slot.Id, nameof(definition));
            if (!slotIds.Add(slot.Id))
                throw new ArgumentException($"槽位重名：{slot.Id}。", nameof(definition));
            if (slot.Skin == null)
            {
                if (slot.Bone == null || !seen.Contains(slot.Bone))
                    throw new ArgumentException($"槽位 {slot.Id} 的骨骼不存在。", nameof(definition));
            }
            else
                ValidateSkin(slot, seen);

            if (!IsFinite(slot.Position))
                throw new ArgumentException($"槽位位置无效：{slot.Id}。", nameof(definition));
        }

        var sine = definition.SpineSine;
        if (sine == null)
            return;
        if (sine.BoneNames == null || sine.BoneNames.Count == 0)
            throw new ArgumentException("正弦至少要一根骨骼。", nameof(definition));
        if (!float.IsFinite(sine.FrequencyHz) || sine.FrequencyHz <= 0f)
            throw new ArgumentOutOfRangeException(nameof(definition), "正弦频率必须大于 0。");
        if (!float.IsFinite(sine.AmplitudeRadians) || !float.IsFinite(sine.PhaseStepRadians))
            throw new ArgumentException("正弦参数无效。", nameof(definition));
        if (sine.AmplitudeScales != null && sine.AmplitudeScales.Count != sine.BoneNames.Count)
            throw new ArgumentException("正弦振幅系数必须和骨骼一样多。", nameof(definition));
        if (sine.ScaleXAmplitudes != null && sine.ScaleXAmplitudes.Count != sine.BoneNames.Count)
            throw new ArgumentException("正弦缩短系数必须和骨骼一样多。", nameof(definition));

        var waved = new HashSet<string>(sine.BoneNames.Count);
        foreach (var name in sine.BoneNames)
        {
            if (!seen.Contains(name))
                throw new ArgumentException($"正弦骨骼不存在：{name}。", nameof(definition));
            if (!waved.Add(name))
                throw new ArgumentException($"正弦骨骼重复：{name}。", nameof(definition));
        }

        if (sine.AmplitudeScales != null)
        {
            foreach (var scale in sine.AmplitudeScales)
            {
                if (!float.IsFinite(scale))
                    throw new ArgumentException("正弦振幅系数无效。", nameof(definition));
            }
        }

        if (sine.ScaleXAmplitudes == null)
            return;
        foreach (var amplitude in sine.ScaleXAmplitudes)
        {
            if (!float.IsFinite(amplitude) || amplitude >= 1f)
                throw new ArgumentException("正弦缩短系数必须是有限数，并且小于 1。", nameof(definition));
        }
    }

    private static void ValidateSkin(CutoutSlotDefinition slot, HashSet<string> bones)
    {
        var skin = slot.Skin!;
        if (skin.Vertices == null || skin.Vertices.Length < 3)
            throw new ArgumentException($"蒙皮至少要三个顶点：{slot.Id}。", nameof(slot));
        foreach (var vertex in skin.Vertices)
        {
            if (!IsFinite(vertex))
                throw new ArgumentException($"蒙皮顶点无效：{slot.Id}。", nameof(slot));
        }

        if (skin.Uv != null && skin.Uv.Length != skin.Vertices.Length)
            throw new ArgumentException($"蒙皮 UV 数量不对：{slot.Id}。", nameof(slot));
        if (skin.Bones == null || skin.Bones.Count == 0)
            throw new ArgumentException($"蒙皮至少要一根骨头：{slot.Id}。", nameof(slot));

        var used = new HashSet<string>(skin.Bones.Count);
        foreach (var bone in skin.Bones)
        {
            if (bone == null || string.IsNullOrWhiteSpace(bone.Name) || !bones.Contains(bone.Name))
                throw new ArgumentException($"蒙皮骨头不存在：{slot.Id}。", nameof(slot));
            if (!used.Add(bone.Name))
                throw new ArgumentException($"蒙皮骨头重复：{bone.Name}。", nameof(slot));
            if (bone.Weights == null || bone.Weights.Length != skin.Vertices.Length)
                throw new ArgumentException($"蒙皮权重数量不对：{bone.Name}。", nameof(slot));
            foreach (var weight in bone.Weights)
            {
                if (!float.IsFinite(weight) || weight < 0f)
                    throw new ArgumentException($"蒙皮权重无效：{bone.Name}。", nameof(slot));
            }
        }
    }

    private static Vector2[] UvFromBounds(Vector2[] vertices)
    {
        var min = vertices[0];
        for (var i = 1; i < vertices.Length; i++)
            min = min.Min(vertices[i]);
        var uv = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            uv[i] = vertices[i] - min;
        return uv;
    }

    private static NodePath SkeletonBonePath(Dictionary<string, string> paths, string boneName)
    {
        var full = paths[boneName];
        var prefix = SkeletonNodeName + "/";
        var relative = full.StartsWith(prefix, StringComparison.Ordinal) ? full[prefix.Length..] : full;
        return new NodePath(relative);
    }

    private static void RequireNodeName(string name, string param)
    {
        if (string.IsNullOrWhiteSpace(name) || !IsNodeName(name))
            throw new ArgumentException($"名称只能用英文标识符：{name}。", param);
    }

    private static bool IsNodeName(string name)
    {
        if (name.Length == 0 || !(char.IsAsciiLetter(name[0]) || name[0] == '_'))
            return false;
        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
                return false;
        }

        return true;
    }

    private static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
}
