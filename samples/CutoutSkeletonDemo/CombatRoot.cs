using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;
using SlimFramework.GodotAdapter;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 对战场景入口。插件仍在 <c>base.Configure</c> 之后 <c>Use</c>，不写进 SlimBootstrap。
/// 根节点保持 Always，这样暂停时还能收到重开和继续。鱼本身是 Pausable，暂停会停住。
/// </summary>
public partial class CombatRoot : SlimBootstrap
{
    private FishArena? _arena;

    protected override void Configure(SlimApp app)
    {
        base.Configure(app);
        app.Use(new CutoutSkeletonPlugin());
    }

    protected override void OnAppReady()
    {
        ProcessMode = ProcessModeEnum.Always;
        var app = App ?? throw new InvalidOperationException("SlimApp 尚未挂上。");
        _arena = new FishArena(this, app);
        _arena.Start();
    }

    public override void _Process(double delta)
    {
        if (GetTree().Paused)
            return;
        base._Process(delta);
        _arena?.OnProcess(delta);
    }

    public override void _UnhandledInput(InputEvent @event) => _arena?.OnInput(@event);
}
