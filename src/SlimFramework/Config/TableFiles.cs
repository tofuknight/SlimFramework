using Luban;

namespace SlimFramework.Config;

/// <summary>
/// 把「表名 → bytes」收成 Luban 生成类需要的 <c>Func&lt;string, ByteBuf&gt;</c>。
/// 游戏把生成代码和 .bytes 放在自己的程序集，不要放进 SlimFramework。
/// </summary>
public static class TableFiles
{
    public static Func<string, ByteBuf> FromReader(Func<string, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return name =>
        {
            var bytes = read(name) ?? throw new FileNotFoundException($"配置表为空: {name}");
            if (bytes.Length == 0)
                throw new FileNotFoundException($"配置表为空: {name}");
            return new ByteBuf(bytes);
        };
    }

    /// <summary>
    /// 目录内文件名为 <c>{表名}.bytes</c>，与 Luban bin 输出一致。
    /// 导出进 PCK 的 <c>res://</c> 没有磁盘文件，改用 Godot 适配里的 <c>GodotTableFiles.FromGodotRes</c>。
    /// </summary>
    public static Func<string, ByteBuf> FromDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        return FromReader(name =>
        {
            var path = Path.Combine(directory, name + ".bytes");
            if (!File.Exists(path))
                throw new FileNotFoundException($"找不到配置表 {path}", path);
            return File.ReadAllBytes(path);
        });
    }
}
