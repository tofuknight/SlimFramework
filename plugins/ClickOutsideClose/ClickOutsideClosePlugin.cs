using Godot;
using SlimFramework;
using SlimFramework.GodotAdapter;
using SlimFramework.UI;

namespace SlimFramework.ClickOutsideClose;

/// <summary>
/// 指针在栈顶页面矩形外按下时，调用 <see cref="IUiStack.CloseTop"/>。
/// 游戏在 <c>base.Configure</c> 之后 <c>Use</c>。不要放进 SlimBootstrap 的默认 Configure。
/// </summary>
public sealed class ClickOutsideClosePlugin : IPlugin
{
    public string Id => "click-outside-close";

    public void Register(SlimApp app)
    {
        if (app.Get<IUiStack>() is not GodotUiStack stack)
            throw new InvalidOperationException("点外部关闭需要已经 Add 的 GodotUiStack。");

        app.Add(new ClickOutsideCloseModule(stack));
    }
}

/// <summary>
/// 订阅 <see cref="GodotUiStack.PagePresented"/> / <see cref="GodotUiStack.PageDismissed"/>，
/// 在主窗 <see cref="Window.WindowInput"/> 里判断按下位置。
/// 矩形用页面的 <see cref="Control.GetGlobalRect"/>，指针用同一画布上的 <see cref="CanvasItem.GetGlobalMousePosition"/>。
/// 只关最后打开的那一页，和 <see cref="IUiStack.CloseTop"/> 的顺序一致。
/// </summary>
internal sealed class ClickOutsideCloseModule : IModule
{
    private readonly GodotUiStack _stack;
    private readonly List<Control> _pages = new();
    private Window? _window;

    public ClickOutsideCloseModule(GodotUiStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _stack = stack;
    }

    public void Init(SlimApp app)
    {
        _stack.PagePresented += OnPresented;
        _stack.PageDismissed += OnDismissed;
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("点外部关闭需要在场景树里 Init。");

        _window = tree.Root;
        _window.WindowInput += OnWindowInput;
    }

    public void Shutdown()
    {
        _stack.PagePresented -= OnPresented;
        _stack.PageDismissed -= OnDismissed;
        if (_window != null && GodotObject.IsInstanceValid(_window))
            _window.WindowInput -= OnWindowInput;
        _window = null;
        _pages.Clear();
    }

    private void OnPresented(Control page) => _pages.Add(page);

    private void OnDismissed(Control page) => _pages.Remove(page);

    private void OnWindowInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;

        while (_pages.Count > 0 && !GodotObject.IsInstanceValid(_pages[^1]))
            _pages.RemoveAt(_pages.Count - 1);
        if (_pages.Count == 0)
            return;

        var top = _pages[^1];
        if (!top.IsVisibleInTree())
            return;
        var rect = top.GetGlobalRect();
        // 还没布局时尺寸是 0，点哪都在矩形外。等有面积再判断。
        if (rect.Size.X <= 0f || rect.Size.Y <= 0f)
            return;
        if (rect.HasPoint(top.GetGlobalMousePosition()))
            return;

        top.GetViewport()?.SetInputAsHandled();
        _stack.CloseTop();
    }
}
