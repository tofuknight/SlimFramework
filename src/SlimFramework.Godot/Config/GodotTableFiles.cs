using Luban;
using SlimFramework.Config;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// 从 <c>res://</c> 读 Luban bin。导出进 PCK 之后没有可被 <see cref="File"/> 打开的磁盘路径，
/// <see cref="TableFiles.FromDirectory"/> 读不到，这里改走 Godot <c>FileAccess</c>。
/// </summary>
public static class GodotTableFiles
{
    /// <summary>
    /// <paramref name="resDirectory"/> 例如 <c>res://Config</c>。文件名为 <c>{表名}.bytes</c>。
    /// </summary>
    public static Func<string, ByteBuf> FromGodotRes(string resDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resDirectory);
        var prefix = resDirectory.Replace('\\', '/').Trim();
        if (prefix.EndsWith('/'))
            prefix = prefix.TrimEnd('/');
        if (!prefix.StartsWith("res://", StringComparison.Ordinal))
            throw new ArgumentException("目录必须是 res:// 路径。", nameof(resDirectory));

        return TableFiles.FromReader(name => Read(prefix, name));
    }

    private static byte[] Read(string prefix, string name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.Contains('/')
            || name.Contains('\\')
            || name.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException($"表名非法: {name}");

        var path = prefix + "/" + name + ".bytes";
        if (!Godot.FileAccess.FileExists(path))
            throw new FileNotFoundException($"找不到配置表 {path}", path);

        var bytes = Godot.FileAccess.GetFileAsBytes(path);
        if (bytes == null || bytes.Length == 0)
            throw new FileNotFoundException($"配置表为空: {path}", path);
        return bytes;
    }
}
