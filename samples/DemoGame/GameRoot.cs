using Godot;
using SlimFramework.Demo;
using SlimFramework.GodotAdapter;

namespace SlimFramework.DemoGame;

/// <summary>Godot 入口。先冒烟适配层，再跑与引擎无关的 Demo 自检。</summary>
public partial class GameRoot : SlimBootstrap
{
    private readonly DemoPlugin _plugin = new();

    protected override void Configure(SlimApp app)
    {
        base.Configure(app);
        app.Use(_plugin);
    }

    protected override void OnAppReady()
    {
        AdapterSmoke.Run(App ?? throw new InvalidOperationException("SlimApp 尚未挂上。"), _plugin);
        DemoSession.Run();
        GD.Print("SlimFramework DemoSession OK");
    }
}
