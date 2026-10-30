using SlimFramework;
using SlimFramework.GodotAdapter;

namespace SlimFramework.AsyncAssets;

/// <summary>
/// 可选异步加载 + 分帧释放。游戏在 <c>base.Configure</c> 之后 <c>Use</c>，不进 SlimBootstrap。
/// 需要已经 Add 的 <see cref="GodotAssetLoader"/>。未挂插件时同步加载行为不变。
/// </summary>
public sealed class AsyncAssetsPlugin : IPlugin
{
    private readonly int _releasesPerFrame;

    public AsyncAssetsPlugin(int releasesPerFrame = 8)
    {
        if (releasesPerFrame <= 0)
            throw new ArgumentOutOfRangeException(nameof(releasesPerFrame));
        _releasesPerFrame = releasesPerFrame;
    }

    public string Id => "async-assets";

    public void Register(SlimApp app)
    {
        if (!app.TryGet<GodotAssetLoader>(out var assets) || assets == null)
            throw new InvalidOperationException("异步加载需要已经 Add 的 GodotAssetLoader。");

        app.Add(new AsyncAssetLoader(assets));
        app.Add(new ReleaseQueue(assets, _releasesPerFrame));
    }
}
