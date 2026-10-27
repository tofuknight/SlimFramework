namespace SlimFramework.GodotAdapter;

/// <summary>把相对路径规范成 <c>user://a/b</c>。</summary>
public static class UserDataPaths
{
    public static string ToUserPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("相对路径不能为空。", nameof(relativePath));

        var text = relativePath.Replace('\\', '/').Trim();
        if (text.StartsWith("user://", StringComparison.Ordinal))
            return text.EndsWith('/') ? text.TrimEnd('/') : text;

        return "user://" + text.TrimStart('/');
    }
}
