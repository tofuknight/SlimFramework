using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>UI 页面根节点。打开时 <see cref="OnOpen"/>，关闭时 <see cref="OnClose"/>。</summary>
public partial class UiPage : Control
{
    public virtual void OnOpen()
    {
    }

    public virtual void OnClose()
    {
    }
}
