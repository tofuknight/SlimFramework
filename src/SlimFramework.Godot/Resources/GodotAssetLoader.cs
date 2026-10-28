using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>同步加载 + 引用计数。<see cref="Release"/> 到 0 后只移出表，不调用 Resource.Dispose。</summary>
public sealed class GodotAssetLoader : IAssetLoader, IModule
{
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public T Load<T>(string path) where T : class
    {
        Validate(path);
        if (_entries.TryGetValue(path, out var existing))
        {
            if (existing.Asset is not T typed)
                throw new InvalidOperationException($"资源类型与 {typeof(T).Name} 不符: {path}");
            existing.RefCount++;
            return typed;
        }

        if (!ResourceLoader.Exists(path))
            throw new FileNotFoundException($"资源不存在: {path}", path);

        var loaded = ResourceLoader.Load(path);
        if (loaded is not T created)
            throw new InvalidOperationException($"资源类型与 {typeof(T).Name} 不符: {path}");

        _entries[path] = new Entry { Asset = loaded, RefCount = 1 };
        return created;
    }

    public AssetTicket<T> LoadTicket<T>(string path) where T : class =>
        new(this, path, Load<T>(path));

    public void Release(string path)
    {
        if (!_entries.TryGetValue(path, out var entry))
            return;
        entry.RefCount--;
        if (entry.RefCount <= 0)
            _entries.Remove(path);
    }

    public void Init(SlimApp app)
    {
    }

    public void Shutdown() => _entries.Clear();

    private static void Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径不能为空。", nameof(path));
        if (!path.StartsWith("res://", StringComparison.Ordinal) &&
            !path.StartsWith("uid://", StringComparison.Ordinal))
            throw new ArgumentException("仅允许 res:// 或 uid://。", nameof(path));
    }

    private sealed class Entry
    {
        public GodotObject Asset { get; init; } = null!;
        public int RefCount { get; set; }
    }
}
