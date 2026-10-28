namespace SlimFramework.Persistence;

/// <summary>一段文本的读写。偏好设置用它落盘，核心不关心路径。</summary>
public interface ITextStore
{
    bool TryRead(out string text);

    void Write(string text);
}
