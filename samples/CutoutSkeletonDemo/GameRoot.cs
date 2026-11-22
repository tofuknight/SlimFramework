using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;
using SlimFramework.GodotAdapter;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>样例入口。插件在 <c>base.Configure</c> 之后 <c>Use</c>，不写进 SlimBootstrap。</summary>
public partial class GameRoot : SlimBootstrap
{
    private OpenOceanCutoutDemo? _demo;

    protected override void Configure(SlimApp app)
    {
        base.Configure(app);
        app.Use(new CutoutSkeletonPlugin());
    }

    protected override void OnAppReady()
    {
        var app = App ?? throw new InvalidOperationException("SlimApp 尚未挂上。");
        _demo = new OpenOceanCutoutDemo(this, app);
        _demo.Start();
    }

    public override void _UnhandledInput(InputEvent @event) => _demo?.OnInput(@event);

    public override void _Process(double delta)
    {
        base._Process(delta);
        _demo?.OnProcess();
    }
}
