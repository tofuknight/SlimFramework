using Godot;
using SlimFramework.Demo;
using SlimFramework.GodotAdapter;

namespace SlimFramework.DemoGame;

/// <summary>Godot 入口。框架在基类里启动，这里只跑与引擎无关的 Demo 自检。</summary>
public partial class GameRoot : SlimBootstrap
{
    protected override void OnAppReady()
    {
        DemoSession.Run();
        GD.Print("SlimFramework DemoSession OK");
    }
}
