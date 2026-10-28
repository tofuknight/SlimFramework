namespace SlimFramework.GodotAdapter;

/// <summary>一次 Load 的持有者。Dispose 时引用计数减一。</summary>
public sealed class AssetTicket<T> : IDisposable where T : class
{
    private readonly GodotAssetLoader _owner;
    private readonly string _path;
    private bool _disposed;

    internal AssetTicket(GodotAssetLoader owner, string path, T asset)
    {
        _owner = owner;
        _path = path;
        Asset = asset;
    }

    public T Asset { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _owner.Release(_path);
    }
}
