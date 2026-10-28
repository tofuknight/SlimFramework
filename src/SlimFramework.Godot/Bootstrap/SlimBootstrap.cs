using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// 挂到主场景或 Autoload 的入口。子类在 <see cref="Configure"/> 里 Use 游戏插件。
/// 帧更新由本节点 <c>_Process</c> 转发，不再单独挂驱动节点。
/// </summary>
public partial class SlimBootstrap : Node
{
    protected SlimApp? App { get; private set; }

    public override void _Ready()
    {
        var app = new SlimApp();
        Configure(app);
        app.Init();
        AudioPreferences.Apply(app);
        SlimApp.Mount(app);
        App = app;
        OnAppReady();
    }

    public override void _Process(double delta) => App?.TickFrame(delta);

    public override void _ExitTree()
    {
        if (App != null)
        {
            var app = App;
            App = null;
            app.Dispose();
        }

        base._ExitTree();
    }

    /// <summary>默认挂上存档、偏好、流程、音频、UI、资源。游戏插件在 base 之后 Use。</summary>
    protected virtual void Configure(SlimApp app)
    {
        var assets = new GodotAssetLoader();
        app.Add(assets);
        app.Add(new SaveService(new GodotUserSaveStore()));
        app.Add(new PreferenceStore(new GodotUserTextStore(GodotUserTextStore.DefaultRelativePath)));
        app.Add(new FlowMachine(new GodotPauseSink()));
        app.Add(new GodotAudio(this, assets));
        app.Add(new GodotUiStack(this, assets));
    }

    protected virtual void OnAppReady()
    {
    }
}
