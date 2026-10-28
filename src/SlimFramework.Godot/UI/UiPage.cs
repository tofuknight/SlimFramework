using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// UI 页面根节点。打开时 <see cref="OnOpen"/>，关闭时 <see cref="OnClose"/>。
/// 和 <see cref="SlimBootstrap"/> 一样，由游戏工程编译，不进适配程序集。
/// </summary>
public partial class UiPage : Control, IUiPage
{
    public virtual void OnOpen()
    {
    }

    public virtual void OnClose()
    {
    }
}
