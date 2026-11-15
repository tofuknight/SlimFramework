using System.Text;
using SlimFramework.Tools.AssetCatalogTool;

if (args.Length < 2)
{
    WriteUsage();
    return 1;
}

try
{
    return args[0] switch
    {
        "generate" => Generate(args),
        "check" => Check(args),
        _ => Unknown(args[0]),
    };
}
catch (AssetCatalogException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int Generate(string[] args)
{
    var manifestPath = args[1];
    var flags = ParseFlags(args, 2);
    RequireKnown(flags, "catalog", "code", "namespace", "class", "project");
    var catalogPath = Require(flags, "catalog");
    var codePath = Require(flags, "code");
    var namespaceName = Require(flags, "namespace");
    var className = Require(flags, "class");
    var entries = ReadManifest(manifestPath);
    var bytes = AssetManifest.ToCatalogBytes(entries);
    CSharpIdentifiers.ValidateNamespace(namespaceName);
    CSharpIdentifiers.ValidateClassName(className);
    CSharpIdentifiers.ValidateConstants(entries, className);
    if (flags.TryGetValue("project", out var projectDir))
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            throw new AssetCatalogException("参数 --project 缺少取值。");
        GodotProjectCheck.Check(projectDir, entries);
    }

    var code = ConstantsWriter.Write(entries, namespaceName, className);
    WriteFile(catalogPath, bytes);
    WriteFile(codePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(code));
    Console.WriteLine($"wrote {catalogPath}");
    Console.WriteLine($"wrote {codePath}");
    return 0;
}

static int Check(string[] args)
{
    var manifestPath = args[1];
    var flags = ParseFlags(args, 2);
    RequireKnown(flags, "project");
    var projectDir = Require(flags, "project");
    var entries = ReadManifest(manifestPath);
    AssetManifest.ToCatalogBytes(entries);
    CSharpIdentifiers.ValidateConstants(entries, className: null);
    GodotProjectCheck.Check(projectDir, entries);
    Console.WriteLine($"check ok: {entries.Count} entries");
    return 0;
}

static IReadOnlyList<AssetManifestEntry> ReadManifest(string path)
{
    if (!File.Exists(path))
        throw new AssetCatalogException($"找不到清单：{path}");
    return AssetManifest.Read(File.ReadAllText(path));
}

static Dictionary<string, string> ParseFlags(string[] args, int start)
{
    var flags = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = start; i < args.Length; i++)
    {
        var token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length < 3)
            throw new AssetCatalogException($"无法识别的参数：{token}");
        var name = token[2..];
        if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new AssetCatalogException($"参数 --{name} 缺少取值。");
        if (!flags.TryAdd(name, args[++i]))
            throw new AssetCatalogException($"参数 --{name} 重复。");
    }

    return flags;
}

static void RequireKnown(Dictionary<string, string> flags, params string[] allowed)
{
    foreach (var name in flags.Keys)
    {
        if (Array.IndexOf(allowed, name) < 0)
            throw new AssetCatalogException($"未知参数 --{name}。");
    }
}

static string Require(Dictionary<string, string> flags, string name)
{
    if (!flags.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
        throw new AssetCatalogException($"缺少 --{name}。");
    return value;
}

static void WriteFile(string path, byte[] bytes)
{
    var directory = Path.GetDirectoryName(Path.GetFullPath(path));
    if (!string.IsNullOrEmpty(directory))
        Directory.CreateDirectory(directory);
    File.WriteAllBytes(path, bytes);
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"未知命令: {command}");
    WriteUsage();
    return 1;
}

static void WriteUsage()
{
    Console.Error.WriteLine("用法:");
    Console.Error.WriteLine("  Slim.AssetCatalogTool generate <manifest.json> --catalog <file.bytes> --code <file.cs> --namespace <Namespace> --class <ClassName> [--project <godot-dir>]");
    Console.Error.WriteLine("  Slim.AssetCatalogTool check <manifest.json> --project <godot-dir>");
    Console.Error.WriteLine("清单是 JSON 数组。每项字段：ref_key、constant、uid、path。缺省的字符串视为空。");
    Console.Error.WriteLine("constant 是 DisplayKey。空 constant 可以入库，但不生成 C# 常量。");
    Console.Error.WriteLine("ref_key 由游戏分配，是运行时和存档身份。");
    Console.Error.WriteLine("--project 核对 res:// 文件是否存在，以及 .uid / .import / 文本资源头里的 uid。不启动 Godot。");
}
