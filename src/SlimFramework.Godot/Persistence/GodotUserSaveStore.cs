namespace SlimFramework.GodotAdapter;

/// <summary>槽位文件 <c>user://saves/slot_000.sav</c>。先写临时文件再改名，不解析 protobuf。</summary>
public sealed class GodotUserSaveStore : ISaveStore
{
    public string DirectoryName { get; init; } = "saves";

    public string Extension { get; init; } = ".sav";

    public int Padding { get; init; } = 3;

    public string GetUserPath(int slot)
    {
        var ext = Extension.StartsWith('.') ? Extension : "." + Extension;
        var name = $"slot_{slot.ToString($"D{Math.Clamp(Padding, 1, 8)}")}{ext}";
        return UserDataPaths.ToUserPath($"{DirectoryName.Trim('/')}/{name}");
    }

    public void Write(int slot, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        AtomicUserFile.WriteBytes(GetUserPath(slot), payload);
    }

    public bool TryRead(int slot, out byte[] payload)
    {
        var path = GetUserPath(slot);
        if (!Godot.FileAccess.FileExists(path))
        {
            payload = Array.Empty<byte>();
            return false;
        }

        payload = Godot.FileAccess.GetFileAsBytes(path);
        return payload.Length > 0;
    }

    public bool Exists(int slot)
    {
        var path = GetUserPath(slot);
        if (!Godot.FileAccess.FileExists(path))
            return false;

        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return file != null && file.GetLength() > 0;
    }

    public bool Delete(int slot)
    {
        var path = GetUserPath(slot);
        if (!Godot.FileAccess.FileExists(path))
            return false;
        return Godot.DirAccess.RemoveAbsolute(path) == Godot.Error.Ok;
    }
}
