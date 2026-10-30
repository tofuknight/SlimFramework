namespace SlimFramework.AsyncAssets;

/// <summary>
/// 一次异步加载请求。完成帧里由 <see cref="AsyncAssetLoader"/> 调 <c>GodotAssetLoader.Load&lt;T&gt;</c>，
/// 因此 <see cref="Asset"/> 已持有一份引用，用完照常 <c>Release(path)</c> 或交给 <see cref="ReleaseQueue"/>。
/// </summary>
public sealed class AsyncLoadHandle<T> : IAsyncLoadHandle where T : class
{
    internal AsyncLoadHandle(string path) => Path = path;

    public string Path { get; }

    /// <summary>0..1。完成（含失败、取消）后为 1。</summary>
    public float Progress { get; private set; }

    public bool IsDone { get; private set; }

    public bool IsCancelled { get; private set; }

    /// <summary>失败原因；成功或取消时为 null。</summary>
    public string? Error { get; private set; }

    public T? Asset { get; private set; }

    /// <summary>成功、失败都会触发一次；取消不触发。</summary>
    public event Action<AsyncLoadHandle<T>>? Completed;

    /// <summary>未完成时取消：之后不会 Load，也不占引用。已完成时无效。</summary>
    public void Cancel()
    {
        if (IsDone)
            return;
        IsCancelled = true;
        IsDone = true;
        Progress = 1f;
    }

    void IAsyncLoadHandle.SetProgress(float progress)
    {
        if (!IsDone)
            Progress = Math.Clamp(progress, 0f, 0.99f);
    }

    Type IAsyncLoadHandle.AssetType => typeof(T);

    void IAsyncLoadHandle.Succeed(object asset)
    {
        Asset = (T)asset;
        Finish(null);
    }

    void IAsyncLoadHandle.Fail(string error) => Finish(error);

    private void Finish(string? error)
    {
        Error = error;
        IsDone = true;
        Progress = 1f;
        Completed?.Invoke(this);
    }
}

internal interface IAsyncLoadHandle
{
    bool IsDone { get; }
    bool IsCancelled { get; }
    Type AssetType { get; }
    void SetProgress(float progress);
    void Succeed(object asset);
    void Fail(string error);
}
