using Godot;
using SlimFramework;
using SlimFramework.GodotAdapter;
using SlimFramework.UI;

namespace SlimFramework.UiLifecycle;

/// <summary>页面可选实现：被新页面盖住 / 重新成为栈顶时回调。回调里不要 Open 或 CloseTop。</summary>
public interface IUiPageLifecycle
{
    void OnCovered();

    void OnRevealed();
}

/// <summary>
/// 补 Covered/Revealed。栈顶 = 打开顺序（与 <see cref="IUiStack.CloseTop"/> 一致），不按 UiLayer。
/// 游戏在 <c>base.Configure</c> 之后 <c>Use</c>，不进 SlimBootstrap。
/// </summary>
public sealed class UiLifecyclePlugin : IPlugin
{
    public string Id => "ui-lifecycle";

    public void Register(SlimApp app)
    {
        if (app.Get<IUiStack>() is not GodotUiStack stack)
            throw new InvalidOperationException("UI 生命周期需要已经 Add 的 GodotUiStack。");

        app.Add(new UiLifecycleModule(stack));
    }
}

/// <summary>
/// 由 PagePresented/PageDismissed 自维护打开序。
/// Presented：旧栈顶 Covered。Dismissed 若是末尾：新末尾 Revealed。
/// Shutdown 时 GodotUiStack 只 OnClose 不发 Dismissed，因此期间没有 Revealed。
/// </summary>
public sealed class UiLifecycleModule : IModule
{
    private readonly GodotUiStack _stack;
    private readonly List<Control> _pages = new();

    public UiLifecycleModule(GodotUiStack stack)
    {
        ArgumentNullException.ThrowIfNull(stack);
        _stack = stack;
    }

    public event Action<Control>? PageCovered;

    public event Action<Control>? PageRevealed;

    public void Init(SlimApp app)
    {
        _stack.PagePresented += OnPresented;
        _stack.PageDismissed += OnDismissed;
    }

    public void Shutdown()
    {
        _stack.PagePresented -= OnPresented;
        _stack.PageDismissed -= OnDismissed;
        _pages.Clear();
    }

    private void OnPresented(Control page)
    {
        Prune();
        var previous = _pages.Count > 0 ? _pages[^1] : null;
        _pages.Add(page);
        if (previous == null)
            return;
        (previous as IUiPageLifecycle)?.OnCovered();
        PageCovered?.Invoke(previous);
    }

    private void OnDismissed(Control page)
    {
        var index = _pages.LastIndexOf(page);
        if (index < 0)
            return;
        var wasTop = index == _pages.Count - 1;
        _pages.RemoveAt(index);
        Prune();
        if (!wasTop || _pages.Count == 0)
            return;
        var top = _pages[^1];
        (top as IUiPageLifecycle)?.OnRevealed();
        PageRevealed?.Invoke(top);
    }

    private void Prune() => _pages.RemoveAll(p => !GodotObject.IsInstanceValid(p));
}
