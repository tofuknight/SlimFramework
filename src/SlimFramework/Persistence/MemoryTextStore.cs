namespace SlimFramework.Persistence;

/// <summary>内存文本。TryRead 在从未 Write 时返回 false。</summary>
public sealed class MemoryTextStore : ITextStore
{
    public string? Text { get; private set; }

    public bool TryRead(out string text)
    {
        if (Text == null)
        {
            text = "";
            return false;
        }

        text = Text;
        return true;
    }

    public void Write(string text) => Text = text ?? "";
}
