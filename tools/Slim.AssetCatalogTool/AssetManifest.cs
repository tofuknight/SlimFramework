using System.Globalization;
using System.Text.Json;
using Google.Protobuf;
using SlimFramework.Contracts.Resource.V1;
using SlimFramework.Resources;

namespace SlimFramework.Tools.AssetCatalogTool;

/// <summary>清单里的一条。字段名与 <c>ResourceRef</c> 一致。</summary>
internal readonly record struct AssetManifestEntry(int RefKey, string Constant, string Uid, string Path);

/// <summary>读取游戏持有的 JSON 清单，并交给 <see cref="ResourceCatalog.Parse"/> 做同一套校验。</summary>
internal static class AssetManifest
{
    public static IReadOnlyList<AssetManifestEntry> Read(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new AssetCatalogException($"清单不是合法 JSON：{ex.Message}");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new AssetCatalogException("清单根节点必须是 JSON 数组。");

            var entries = new List<AssetManifestEntry>();
            var seenKeys = new Dictionary<int, int>();
            var index = 0;
            foreach (var element in document.RootElement.EnumerateArray())
            {
                index++;
                if (element.ValueKind != JsonValueKind.Object)
                    throw new AssetCatalogException($"清单第 {index.ToString(CultureInfo.InvariantCulture)} 条必须是对象。");

                int? refKey = null;
                string? constant = null;
                string? uid = null;
                string? path = null;
                foreach (var property in element.EnumerateObject())
                {
                    switch (property.Name)
                    {
                        case "ref_key":
                            if (!property.Value.TryGetInt32(out var key))
                            {
                                throw new AssetCatalogException(
                                    $"清单第 {index.ToString(CultureInfo.InvariantCulture)} 条的 ref_key 不是 int32。");
                            }

                            refKey = key;
                            break;
                        case "constant":
                            constant = RequireString(property, index);
                            break;
                        case "uid":
                            uid = RequireString(property, index);
                            break;
                        case "path":
                            path = RequireString(property, index);
                            break;
                        default:
                            throw new AssetCatalogException(
                                $"清单第 {index.ToString(CultureInfo.InvariantCulture)} 条有未知字段「{property.Name}」。只接受 ref_key、constant、uid、path。");
                    }
                }

                if (refKey is null)
                    throw new AssetCatalogException($"清单第 {index.ToString(CultureInfo.InvariantCulture)} 条缺少 ref_key。");

                if (seenKeys.TryGetValue(refKey.Value, out var previous))
                {
                    throw new AssetCatalogException(
                        $"清单第 {previous.ToString(CultureInfo.InvariantCulture)} 条和第 {index.ToString(CultureInfo.InvariantCulture)} 条的 ref_key 都是 {refKey.Value.ToString(CultureInfo.InvariantCulture)}。");
                }

                seenKeys.Add(refKey.Value, index);
                entries.Add(new AssetManifestEntry(refKey.Value, constant ?? "", uid ?? "", path ?? ""));
            }

            return entries;
        }
    }

    /// <summary>
    /// 按 ref_key 升序写入目录字节。<see cref="ResourceCatalog.Parse"/> 拒绝时，异常文本原样抛出。
    /// </summary>
    public static byte[] ToCatalogBytes(IReadOnlyList<AssetManifestEntry> entries)
    {
        var message = new ResourceRefCatalog();
        foreach (var entry in entries.OrderBy(static item => item.RefKey))
        {
            message.Refs[entry.RefKey] = new ResourceRef
            {
                RefKey = entry.RefKey,
                Constant = entry.Constant,
                Uid = entry.Uid,
                Path = entry.Path,
            };
        }

        var bytes = message.ToByteArray();
        try
        {
            ResourceCatalog.Parse(bytes);
        }
        catch (InvalidDataException ex)
        {
            throw new AssetCatalogException(ex.Message);
        }

        return bytes;
    }

    private static string RequireString(JsonProperty property, int index)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            throw new AssetCatalogException(
                $"清单第 {index.ToString(CultureInfo.InvariantCulture)} 条的 {property.Name} 必须是字符串。");
        }

        return property.Value.GetString() ?? "";
    }
}
