namespace SlimFramework.GodotAdapter;

/// <summary>把偏好 JSON 写到 <c>user://</c>。</summary>
public sealed class GodotUserTextStore : ITextStore
{
    private readonly string _userPath;

    public GodotUserTextStore(string relativeUserPath) =>
        _userPath = UserDataPaths.ToUserPath(relativeUserPath);

    public bool TryRead(out string text)
    {
        if (!Godot.FileAccess.FileExists(_userPath))
        {
            text = "";
            return false;
        }

        text = Godot.FileAccess.GetFileAsString(_userPath);
        return true;
    }

    public void Write(string text)
    {
        using var file = Godot.FileAccess.Open(_userPath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
            throw new IOException($"无法写入 {_userPath}: {Godot.FileAccess.GetOpenError()}");
        file.StoreString(text ?? "");
    }
}
