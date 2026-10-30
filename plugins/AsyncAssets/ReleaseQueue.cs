using SlimFramework;
using SlimFramework.GodotAdapter;

namespace SlimFramework.AsyncAssets;

/// <summary>分帧释放：每帧最多调用 N 次 <see cref="GodotAssetLoader.Release"/>。Shutdown 时同步清空。</summary>
public sealed class ReleaseQueue : IModule, ITickable
{
    private readonly GodotAssetLoader _assets;
    private readonly Queue<string> _queue = new();

    public ReleaseQueue(GodotAssetLoader assets, int releasesPerFrame)
    {
        ArgumentNullException.ThrowIfNull(assets);
        if (releasesPerFrame <= 0)
            throw new ArgumentOutOfRangeException(nameof(releasesPerFrame));
        _assets = assets;
        ReleasesPerFrame = releasesPerFrame;
    }

    public int ReleasesPerFrame { get; }

    public int Count => _queue.Count;

    public void Enqueue(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径不能为空。", nameof(path));
        _queue.Enqueue(path);
    }

    public void Tick(double delta)
    {
        for (var i = 0; i < ReleasesPerFrame && _queue.Count > 0; i++)
            _assets.Release(_queue.Dequeue());
    }

    public void Init(SlimApp app)
    {
    }

    public void Shutdown()
    {
        while (_queue.Count > 0)
            _assets.Release(_queue.Dequeue());
    }
}
