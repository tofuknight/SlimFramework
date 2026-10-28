namespace SlimFramework.GodotAdapter;

/// <summary>把偏好 JSON 写到 <c>user://</c>。先写临时文件再改名。</summary>
public sealed class GodotUserTextStore : ITextStore
{
    public const string DefaultRelativePath = "prefs.json";

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

    public void Write(string text) => AtomicUserFile.WriteText(_userPath, text ?? "");
}
