using System.Text;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// 同目录先写临时文件并刷到磁盘，再改名覆盖。
/// 崩溃时旧文件还在，正式路径不会停在写了一半的内容上。
/// </summary>
internal static class AtomicUserFile
{
    public static void WriteBytes(string userPath, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var destination = Globalize(userPath);
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var temp = destination + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(payload, 0, payload.Length);
            stream.Flush(flushToDisk: true);
        }

        try
        {
            File.Move(temp, destination, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temp);
            throw new IOException($"无法提交 {userPath}", ex);
        }
    }

    /// <summary>UTF-8、无 BOM，与 Godot <c>FileAccess.StoreString</c> 一致，<c>GetFileAsString</c> 能读回。</summary>
    public static void WriteText(string userPath, string text) =>
        WriteBytes(userPath, Encoding.UTF8.GetBytes(text ?? ""));

    private static string Globalize(string userPath)
    {
        var absolute = Godot.ProjectSettings.GlobalizePath(userPath);
        if (string.IsNullOrWhiteSpace(absolute) || absolute.Contains("://", StringComparison.Ordinal))
            throw new IOException($"无法解析 {userPath}");
        return absolute;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
