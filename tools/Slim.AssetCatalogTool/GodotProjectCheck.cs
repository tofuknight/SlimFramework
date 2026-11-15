using System.Text.RegularExpressions;

namespace SlimFramework.Tools.AssetCatalogTool;

/// <summary>
/// 对照 Godot 工程目录核对清单。不启动编辑器。
/// 路径必须是 <c>res://</c> 且文件存在。uid 与同路径的 <c>.uid</c>、<c>.import</c>，
/// 以及 <c>.tscn</c> / <c>.tres</c> 文本头里的 <c>uid</c> 比较；多处都有时必须相同。
/// </summary>
internal static class GodotProjectCheck
{
    private static readonly Regex UidAttribute = new(
        "uid\\s*=\\s*\"(uid://[^\"]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.Ordinal)
    {
        ".godot", ".mono", "bin", "obj", ".git", ".vs", ".idea",
    };

    public static void Check(string projectDir, IReadOnlyList<AssetManifestEntry> entries)
    {
        var root = Path.GetFullPath(projectDir);
        if (!File.Exists(Path.Combine(root, "project.godot")))
            throw new AssetCatalogException($"不是 Godot 工程目录（缺少 project.godot）：{root}");

        foreach (var entry in entries)
        {
            var hasPath = !string.IsNullOrWhiteSpace(entry.Path);
            var hasUid = !string.IsNullOrWhiteSpace(entry.Uid);
            if (hasPath)
                CheckPath(root, entry, hasUid);
            else if (hasUid)
                CheckUidOnly(root, entry);
        }
    }

    private static void CheckPath(string root, AssetManifestEntry entry, bool hasUid)
    {
        var absolute = ResolveResPath(root, entry.Path);
        if (!File.Exists(absolute))
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的文件不存在：{entry.Path}");

        var sources = ReadSources(absolute, entry.Path);
        if (!hasUid)
            return;

        if (!entry.Uid.StartsWith("uid://", StringComparison.Ordinal))
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的 uid「{entry.Uid}」必须以 uid:// 开头。");
        if (entry.Uid.Any(char.IsWhiteSpace))
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的 uid「{entry.Uid}」含空白。");

        if (sources.Count == 0)
        {
            throw new AssetCatalogException(
                $"{entry.Path} 的清单 uid 是 {entry.Uid}，工程里没有 .uid、.import 或文本资源头。");
        }

        foreach (var source in sources)
        {
            if (!string.Equals(source.Uid, entry.Uid, StringComparison.Ordinal))
            {
                throw new AssetCatalogException(
                    $"{entry.Path} 的清单 uid 是 {entry.Uid}，{source.Label} 是 {source.Uid}。");
            }
        }
    }

    private static void CheckUidOnly(string root, AssetManifestEntry entry)
    {
        if (!entry.Uid.StartsWith("uid://", StringComparison.Ordinal))
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的 uid「{entry.Uid}」必须以 uid:// 开头。");
        if (entry.Uid.Any(char.IsWhiteSpace))
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的 uid「{entry.Uid}」含空白。");

        var matches = new List<string>();
        foreach (var file in EnumerateProjectFiles(root))
        {
            if (IsSidecar(file))
                continue;
            var display = ToResPath(root, file);
            foreach (var source in ReadSources(file, display))
            {
                if (!string.Equals(source.Uid, entry.Uid, StringComparison.Ordinal))
                    continue;
                matches.Add(ToResPath(root, file));
                break;
            }
        }

        var distinct = matches.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0)
            throw new AssetCatalogException($"ref_key {entry.RefKey} 的 uid {entry.Uid} 在工程里找不到。");
        if (distinct.Count > 1)
            throw new AssetCatalogException($"uid {entry.Uid} 对应了多个文件：{string.Join("、", distinct)}。");
    }

    private static List<UidSource> ReadSources(string resourcePath, string display)
    {
        var sources = new List<UidSource>();
        var uidFile = resourcePath + ".uid";
        if (File.Exists(uidFile))
        {
            var uid = ReadUidFile(uidFile);
            sources.Add(new UidSource(".uid", uid));
        }

        var importFile = resourcePath + ".import";
        if (File.Exists(importFile))
        {
            var uid = ReadUidAttribute(File.ReadAllText(importFile), importFile);
            if (uid.Length > 0)
                sources.Add(new UidSource(".import", uid));
        }

        var extension = Path.GetExtension(resourcePath);
        if (extension is ".tscn" or ".tres")
        {
            var header = ReadFirstLine(resourcePath);
            var uid = ReadUidAttribute(header, resourcePath);
            if (uid.Length > 0)
                sources.Add(new UidSource("文本资源头", uid));
        }

        if (sources.Count > 1)
        {
            var first = sources[0].Uid;
            for (var i = 1; i < sources.Count; i++)
            {
                if (!string.Equals(sources[i].Uid, first, StringComparison.Ordinal))
                {
                    throw new AssetCatalogException(
                        $"{display} 的 {sources[0].Label} 是 {first}，{sources[i].Label} 是 {sources[i].Uid}。");
                }
            }
        }

        return sources;
    }

    private static string ReadUidFile(string path)
    {
        var text = File.ReadAllText(path).Trim();
        if (!text.StartsWith("uid://", StringComparison.Ordinal) || text.Any(char.IsWhiteSpace))
            throw new AssetCatalogException($"{path} 的内容不是单行 uid://。");
        return text;
    }

    private static string ReadUidAttribute(string text, string path)
    {
        var matches = UidAttribute.Matches(text);
        string? uid = null;
        foreach (Match match in matches)
        {
            var found = match.Groups[1].Value;
            if (found.Any(char.IsWhiteSpace))
                throw new AssetCatalogException($"{path} 的 uid 含空白。");
            if (uid is null)
            {
                uid = found;
                continue;
            }

            if (!string.Equals(uid, found, StringComparison.Ordinal))
                throw new AssetCatalogException($"{path} 里有多个不同的 uid：{uid} 与 {found}。");
        }

        return uid ?? "";
    }

    private static string ReadFirstLine(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        return reader.ReadLine() ?? "";
    }

    private static string ResolveResPath(string root, string resPath)
    {
        if (!resPath.StartsWith("res://", StringComparison.Ordinal))
            throw new AssetCatalogException($"check 只接受 res:// 路径：{resPath}");

        var relative = resPath["res://".Length..];
        if (relative.Length == 0 || relative.Contains('\\', StringComparison.Ordinal) || relative.Contains("//", StringComparison.Ordinal))
            throw new AssetCatalogException($"res:// 路径不合法：{resPath}");

        var segments = relative.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
                throw new AssetCatalogException($"res:// 路径不合法：{resPath}");
        }

        var absolute = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(prefix, StringComparison.Ordinal))
            throw new AssetCatalogException($"res:// 路径跳出了工程目录：{resPath}");
        return absolute;
    }

    private static string ToResPath(string root, string absoluteFile)
    {
        var relative = Path.GetRelativePath(root, absoluteFile).Replace('\\', '/');
        return "res://" + relative;
    }

    private static bool IsSidecar(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".uid", StringComparison.Ordinal) || name.EndsWith(".import", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EnumerateProjectFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            List<string> children;
            try
            {
                children = Directory.EnumerateFileSystemEntries(dir).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new AssetCatalogException($"无法读取目录 {dir}：{ex.Message}");
            }

            foreach (var entry in children)
            {
                var name = Path.GetFileName(entry);
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!SkippedDirectories.Contains(name))
                        pending.Push(entry);
                    continue;
                }

                yield return entry;
            }
        }
    }

    private readonly record struct UidSource(string Label, string Uid);
}
