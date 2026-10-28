using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// 进入场景树的第一个实例成为 <see cref="I"/>。重复实例会被排队释放。
/// 服务优先走 <see cref="SlimApp"/>；仅 Godot 节点需要这个单例。
/// </summary>
public abstract partial class NodeSingleton<T> : Node where T : NodeSingleton<T>
{
    private static T? _current;

    public static T? I => _current;

    public static T Require() =>
        _current ?? throw new InvalidOperationException($"{typeof(T).Name} 尚未进入场景树。");

    public override void _EnterTree()
    {
        base._EnterTree();
        if (_current != null && !ReferenceEquals(_current, this))
        {
            GD.PushWarning($"{typeof(T).Name}: 重复单例，销毁多余实例。");
            CallDeferred(Node.MethodName.QueueFree);
            return;
        }

        _current = (T)this;
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(_current, this))
            _current = null;
        base._ExitTree();
    }
}
