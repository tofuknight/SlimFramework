namespace SlimFramework.Resources;

/// <summary>
/// 把 <see cref="ResourceRefEntry.LoadKey"/> 交给现有的 <see cref="IAssetLoader"/>。
/// 不在 Uid 失败后再试 Path。
/// </summary>
public static class ResourceRefLoader
{
    public static T Load<T>(this IAssetLoader loader, ResourceRefEntry entry) where T : class
    {
        ArgumentNullException.ThrowIfNull(loader);
        var key = entry.LoadKey;
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException($"资源引用 {entry.RefKey} 没有 Uid 或 Path。");
        return loader.Load<T>(key);
    }

    public static T Load<T>(this IAssetLoader loader, ResourceCatalog catalog, int refKey) where T : class
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(catalog);
        if (!catalog.TryGet(refKey, out var entry))
            throw new KeyNotFoundException($"资源目录没有 ref_key {refKey}。");
        return loader.Load<T>(entry);
    }
}
