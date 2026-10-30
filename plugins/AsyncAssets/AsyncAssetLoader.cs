using Godot;
using SlimFramework;
using SlimFramework.GodotAdapter;

namespace SlimFramework.AsyncAssets;

/// <summary>
/// 用 <see cref="ResourceLoader.LoadThreadedRequest"/> 发后台请求，每帧 <see cref="ResourceLoader.LoadThreadedGetStatus"/>。
/// 完成帧对每个未取消的 handle 调一次 <see cref="GodotAssetLoader.Load{T}"/>，拿同一实例并各加一份引用。
/// 同路径的后台请求合并。只在主线程调用。
/// </summary>
public sealed class AsyncAssetLoader : IModule, ITickable
{
    private readonly GodotAssetLoader _assets;
    private readonly Dictionary<string, List<IAsyncLoadHandle>> _pending = new(StringComparer.Ordinal);
    private readonly List<string> _done = new();
    private readonly Godot.Collections.Array _progress = new();

    public AsyncAssetLoader(GodotAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
    }

    public int PendingCount => _pending.Count;

    public AsyncLoadHandle<T> LoadAsync<T>(string path) where T : class
    {
        Validate(path);
        var handle = new AsyncLoadHandle<T>(path);
        if (_pending.TryGetValue(path, out var waiting))
        {
            waiting.Add(handle);
            return handle;
        }

        if (!ResourceLoader.Exists(path))
        {
            ((IAsyncLoadHandle)handle).Fail($"资源不存在: {path}");
            return handle;
        }

        var err = ResourceLoader.LoadThreadedRequest(path);
        if (err != Godot.Error.Ok)
        {
            ((IAsyncLoadHandle)handle).Fail($"后台加载请求失败 {err}: {path}");
            return handle;
        }

        _pending[path] = new List<IAsyncLoadHandle> { handle };
        return handle;
    }

    public void Tick(double delta)
    {
        if (_pending.Count == 0)
            return;

        _done.Clear();
        foreach (var (path, handles) in _pending)
        {
            var status = ResourceLoader.LoadThreadedGetStatus(path, _progress);
            switch (status)
            {
                case ResourceLoader.ThreadLoadStatus.InProgress:
                    var p = _progress.Count > 0 ? (float)_progress[0].AsDouble() : 0f;
                    foreach (var h in handles)
                        h.SetProgress(p);
                    break;
                case ResourceLoader.ThreadLoadStatus.Loaded:
                    // 取走后台结果，让 Godot 缓存持有实例；随后同步 Load 命中同一实例。
                    var loaded = ResourceLoader.LoadThreadedGet(path);
                    _done.Add(path);
                    Complete(path, handles, loaded);
                    break;
                default:
                    _done.Add(path);
                    foreach (var h in handles)
                        if (!h.IsCancelled)
                            h.Fail($"后台加载失败 {status}: {path}");
                    break;
            }
        }

        foreach (var path in _done)
            _pending.Remove(path);
    }

    public void Init(SlimApp app)
    {
    }

    /// <summary>取消未完成请求；不会 Load，也不占引用。</summary>
    public void Shutdown()
    {
        foreach (var handles in _pending.Values)
            foreach (var h in handles)
                if (h is { IsDone: false })
                    CancelUntyped(h);
        _pending.Clear();
    }

    private void Complete(string path, List<IAsyncLoadHandle> handles, Resource? loaded)
    {
        GC.KeepAlive(loaded);
        foreach (var h in handles)
        {
            if (h.IsCancelled)
                continue;
            try
            {
                h.Succeed(LoadTyped(h.AssetType, path));
            }
            catch (Exception ex)
            {
                h.Fail(ex.Message);
            }
        }
    }

    private object LoadTyped(Type type, string path) =>
        typeof(GodotAssetLoader).GetMethod(nameof(GodotAssetLoader.Load))!
            .MakeGenericMethod(type)
            .Invoke(_assets, new object[] { path })!;

    private static void CancelUntyped(IAsyncLoadHandle h) =>
        h.GetType().GetMethod("Cancel")!.Invoke(h, null);

    private static void Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("路径不能为空。", nameof(path));
        if (!path.StartsWith("res://", StringComparison.Ordinal) &&
            !path.StartsWith("uid://", StringComparison.Ordinal))
            throw new ArgumentException("仅允许 res:// 或 uid://。", nameof(path));
    }
}
