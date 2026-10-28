using SlimFramework.Contracts.Resource.V1;

namespace SlimFramework.Resources;

/// <summary>
/// ref_key 解析结果。加载地址是 <see cref="LoadKey"/>：非空 Uid，否则 Path。
/// Uid 加载失败后再试 Path 不在此策略内。
/// </summary>
public readonly record struct ResourceRefEntry(int RefKey, string Constant, string Uid, string Path)
{
    /// <summary>非空 <see cref="Uid"/> 作为加载地址，否则 <see cref="Path"/>。</summary>
    public string LoadKey => string.IsNullOrWhiteSpace(Uid) ? Path ?? "" : Uid;
}

/// <summary>读取 <see cref="ResourceRefCatalog"/> 字节。表内容来自游戏或工具，不进框架源码。</summary>
public sealed class ResourceCatalog
{
    private readonly Dictionary<int, ResourceRefEntry> _byKey;

    private ResourceCatalog(Dictionary<int, ResourceRefEntry> byKey) => _byKey = byKey;

    public int Count => _byKey.Count;

    public static ResourceCatalog Parse(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var message = ResourceRefCatalog.Parser.ParseFrom(payload);
        var map = new Dictionary<int, ResourceRefEntry>(message.Refs.Count);
        foreach (var pair in message.Refs)
        {
            var row = pair.Value;
            map[pair.Key] = new ResourceRefEntry(pair.Key, row.Constant, row.Uid, row.Path);
        }

        return new ResourceCatalog(map);
    }

    public bool TryGet(int refKey, out ResourceRefEntry entry) => _byKey.TryGetValue(refKey, out entry);
}
