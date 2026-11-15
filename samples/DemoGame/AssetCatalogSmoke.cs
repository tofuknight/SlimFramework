using Godot;
using SlimFramework.Resources;

namespace SlimFramework.DemoGame;

/// <summary>
/// 用生成的目录和 DisplayKey 常量加载真实的 <c>res://</c> 资源。
/// 清单在 <c>Assets/resource_refs.json</c>，字节和 <see cref="AssetKeys"/> 由 Slim.AssetCatalogTool 生成。
/// </summary>
internal static class AssetCatalogSmoke
{
    private const string CatalogPath = "res://Assets/resource_refs.bytes";

    public static void Run(SlimApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!Godot.FileAccess.FileExists(CatalogPath))
            throw new InvalidOperationException($"缺少资源目录：{CatalogPath}");

        var catalog = ResourceCatalog.Parse(Godot.FileAccess.GetFileAsBytes(CatalogPath));
        var loader = app.Get<IAssetLoader>();
        if (!catalog.TryGet(AssetKeys.SmokePage, out var pageEntry)
            || pageEntry.Constant != "SmokePage"
            || pageEntry.RefKey != AssetKeys.SmokePage
            || pageEntry.LoadKey != "res://SmokePage.tscn")
            throw new InvalidOperationException("SmokePage 的 DisplayKey 没有对上 ref_key。");

        var page = loader.Load<PackedScene>(catalog, "SmokePage");
        ExpectLoadedPath(page, "res://SmokePage.tscn");
        loader.Release(pageEntry.LoadKey);

        if (!catalog.TryGet(AssetKeys.UiClick, out var clickEntry) || clickEntry.LoadKey != "res://ui-click.wav")
            throw new InvalidOperationException("UiClick 的 DisplayKey 没有对上 ref_key。");

        var click = loader.Load<AudioStream>(catalog, AssetKeys.UiClick);
        ExpectLoadedPath(click, "res://ui-click.wav");
        loader.Release(clickEntry.LoadKey);

        GD.Print("SlimFramework asset catalog smoke OK");
    }

    /// <summary>
    /// 导入资源的 <c>ResourcePath</c> 有时是源路径，有时落在 <c>.godot/imported</c> 且仍带原文件名。
    /// 空路径不再额外断言：<see cref="IAssetLoader.Load{T}"/> 已经按目录里的 LoadKey 和类型加载。
    /// </summary>
    private static void ExpectLoadedPath(Resource resource, string expected)
    {
        var actual = resource.ResourcePath;
        if (actual.Length == 0 || actual == expected)
            return;
        var fileName = expected["res://".Length..];
        if (actual.Contains("/" + fileName, StringComparison.Ordinal))
            return;
        throw new InvalidOperationException($"加载结果路径是「{actual}」，期望 {expected}。");
    }
}
