namespace SlimFramework.Tools.AssetCatalogTool;

/// <summary>
/// 生成常量类时，非空 constant 必须是 C# 标识符。
/// 这是工具规则。<see cref="SlimFramework.Resources.ResourceCatalog.Parse"/> 不检查标识符，字符串查找仍可用。
/// </summary>
internal static class CSharpIdentifiers
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while",
        "add", "alias", "and", "args", "ascending", "async", "await", "by", "descending",
        "dynamic", "equals", "file", "from", "get", "global", "group", "init", "into", "join",
        "let", "managed", "nameof", "nint", "not", "notnull", "nuint", "on", "or", "orderby",
        "partial", "record", "remove", "required", "scoped", "select", "set", "unmanaged",
        "value", "var", "when", "where", "with", "yield",
    };

    public static void ValidateNamespace(string namespaceName)
    {
        if (namespaceName.Length == 0)
            throw new AssetCatalogException("命名空间不能为空。");

        var segments = namespaceName.Split('.');
        foreach (var segment in segments)
        {
            if (!IsIdentifier(segment) || IsKeyword(segment))
                throw new AssetCatalogException($"命名空间「{namespaceName}」里的「{segment}」不是合法的 C# 标识符。");
        }
    }

    public static void ValidateClassName(string className)
    {
        if (!IsIdentifier(className) || IsKeyword(className))
            throw new AssetCatalogException($"类名「{className}」不是合法的 C# 标识符。");
    }

    public static void ValidateConstants(IReadOnlyList<AssetManifestEntry> entries, string? className)
    {
        foreach (var entry in entries)
        {
            if (entry.Constant.Length == 0)
                continue;
            if (!IsIdentifier(entry.Constant))
            {
                throw new AssetCatalogException(
                    $"ref_key {entry.RefKey} 的 constant「{entry.Constant}」不是合法的 C# 标识符，不能生成常量。");
            }

            if (IsKeyword(entry.Constant))
            {
                throw new AssetCatalogException(
                    $"ref_key {entry.RefKey} 的 constant「{entry.Constant}」是 C# 关键字，不能生成常量。");
            }

            if (className is not null && string.Equals(entry.Constant, className, StringComparison.Ordinal))
            {
                throw new AssetCatalogException(
                    $"ref_key {entry.RefKey} 的 constant「{entry.Constant}」与类名相同，不能生成常量。");
            }
        }
    }

    private static bool IsIdentifier(string text)
    {
        if (text.Length == 0)
            return false;
        if (!IsStart(text[0]))
            return false;
        for (var i = 1; i < text.Length; i++)
        {
            if (!IsPart(text[i]))
                return false;
        }

        return true;
    }

    private static bool IsKeyword(string text) => Keywords.Contains(text);

    private static bool IsStart(char value) =>
        value is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or '_';

    private static bool IsPart(char value) => IsStart(value) || value is >= '0' and <= '9';
}
