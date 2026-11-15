using SlimFramework.Contracts.Resource.V1;

namespace SlimFramework.Resources;

/// <summary>
/// ref_key 解析结果。<see cref="Constant"/> 是 DisplayKey，给代码和清单用。
/// 加载地址是 <see cref="LoadKey"/>：非空 Uid，否则 Path。
/// Uid 加载失败后再试 Path 不在此策略内。
/// </summary>
public readonly record struct ResourceRefEntry(int RefKey, string Constant, string Uid, string Path)
{
    /// <summary>非空 <see cref="Uid"/> 作为加载地址，否则 <see cref="Path"/>。空白 Uid 视为没有 Uid。</summary>
    public string LoadKey => string.IsNullOrWhiteSpace(Uid) ? Path ?? "" : Uid;
}

/// <summary>
/// 读取 <see cref="ResourceRefCatalog"/> 字节。表内容来自游戏或工具，不进框架源码。
/// <see cref="TryGet(int, out ResourceRefEntry)"/> 按 ref_key（运行时和存档身份）。
/// <see cref="TryGet(string, out ResourceRefEntry)"/> 按 DisplayKey（字段 constant）。
/// </summary>
public sealed class ResourceCatalog
{
    private readonly Dictionary<int, ResourceRefEntry> _byKey;
    private readonly Dictionary<string, ResourceRefEntry> _byConstant;

    private ResourceCatalog(
        Dictionary<int, ResourceRefEntry> byKey,
        Dictionary<string, ResourceRefEntry> byConstant)
    {
        _byKey = byKey;
        _byConstant = byConstant;
    }

    public int Count => _byKey.Count;

    public static ResourceCatalog Parse(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var message = ResourceRefCatalog.Parser.ParseFrom(payload);
        var byKey = new Dictionary<int, ResourceRefEntry>(message.Refs.Count);
        var byConstant = new Dictionary<string, ResourceRefEntry>(message.Refs.Count, StringComparer.Ordinal);
        foreach (var pair in message.Refs)
        {
            var row = pair.Value;
            if (pair.Key != row.RefKey)
            {
                throw new InvalidDataException(
                    $"资源目录 map key {pair.Key} 与 ref_key {row.RefKey} 不一致。");
            }

            var constant = row.Constant ?? "";
            if (constant.Length != 0 && !string.Equals(constant, constant.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"资源引用 {row.RefKey} 的 constant 含首尾空白。");
            }

            var uid = row.Uid ?? "";
            var path = row.Path ?? "";
            if (string.IsNullOrWhiteSpace(uid) && string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidDataException(
                    $"资源引用 {row.RefKey} 没有 Uid 或 Path。");
            }

            var entry = new ResourceRefEntry(row.RefKey, constant, uid, path);
            if (constant.Length != 0)
            {
                if (!byConstant.TryAdd(constant, entry))
                {
                    var previous = byConstant[constant].RefKey;
                    throw new InvalidDataException(
                        $"资源目录 constant「{constant}」重复：ref_key {previous} 与 {row.RefKey}。");
                }
            }

            byKey.Add(pair.Key, entry);
        }

        return new ResourceCatalog(byKey, byConstant);
    }

    public bool TryGet(int refKey, out ResourceRefEntry entry) => _byKey.TryGetValue(refKey, out entry);

    /// <summary>
    /// 按 DisplayKey 查找。参数是字段 constant，不是 res:// 路径。
    /// 空字符串不是名字，返回 false。大小写按序数比较，不忽略。
    /// </summary>
    public bool TryGet(string displayKey, out ResourceRefEntry entry)
    {
        ArgumentNullException.ThrowIfNull(displayKey);
        if (displayKey.Length == 0 || !_byConstant.TryGetValue(displayKey, out entry))
        {
            entry = default;
            return false;
        }

        return true;
    }
}
