using Godot;
using SlimFramework;

namespace SlimFramework.CutoutSkeleton;

/// <summary>
/// 可选 2D 剪影换装。游戏在 <c>base.Configure</c> 之后 <c>Use</c>，不进 SlimBootstrap。
/// 骨架是 Godot 内置的 <see cref="Skeleton2D"/> / <see cref="Bone2D"/>，不另做骨骼编辑器。
/// </summary>
public sealed class CutoutSkeletonPlugin : IPlugin
{
    public string Id => "cutout-skeleton";

    public void Register(SlimApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.Add(new CutoutSkeletonModule());
    }
}

/// <summary>
/// 持有已经挂上的 <see cref="CutoutRig"/>，在 <see cref="Tick"/> 里推进正弦。
/// 不读取 UI、存档或资源服务。没人 <c>Use</c> 时核心行为不变。
/// </summary>
public sealed class CutoutSkeletonModule : IModule, ITickable
{
    private readonly List<CutoutRig> _rigs = new();
    private bool _lateQueued;

    public void Init(SlimApp app)
    {
    }

    public void Shutdown() => _rigs.Clear();

    /// <summary>按定义生成内置骨架并开始驱动。节点挂在 <paramref name="parent"/> 下，所有权归场景。</summary>
    public CutoutRig Attach(Node parent, CutoutRigDefinition definition, string? rootName = null)
    {
        var rig = CutoutRig.Build(parent, definition, rootName);
        _rigs.Add(rig);
        return rig;
    }

    /// <summary>停止驱动。不释放节点。</summary>
    public void Detach(CutoutRig rig)
    {
        ArgumentNullException.ThrowIfNull(rig);
        _rigs.Remove(rig);
    }

    /// <summary>
    /// 推进每套骨架的正弦。本帧只排一次空闲补写，覆盖全部仍在树上的骨架。
    /// 每套自己 <c>CallDeferred</c> 时，几十套会在同一帧排几十次空闲调用。
    /// </summary>
    public void Tick(double delta)
    {
        var writeLate = false;
        for (var i = _rigs.Count - 1; i >= 0; i--)
        {
            var rig = _rigs[i];
            if (!GodotObject.IsInstanceValid(rig.Root))
            {
                _rigs.RemoveAt(i);
                continue;
            }

            if (rig.AdvanceImmediate(delta))
                writeLate = true;
        }

        if (writeLate)
            QueueLateWrite();
    }

    private void QueueLateWrite()
    {
        if (_lateQueued)
            return;
        _lateQueued = true;
        Callable.From(LateWriteAll).CallDeferred();
    }

    private void LateWriteAll()
    {
        _lateQueued = false;
        for (var i = 0; i < _rigs.Count; i++)
        {
            var rig = _rigs[i];
            if (GodotObject.IsInstanceValid(rig.Root))
                rig.WriteSpinePose();
        }
    }
}
