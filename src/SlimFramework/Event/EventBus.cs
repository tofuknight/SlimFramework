namespace SlimFramework.Event;

/// <summary>事件标记。载荷用 readonly record struct 即可。</summary>
public interface IEvent
{
}

/// <summary>按载荷类型广播。发布时对订阅列表做快照。</summary>
public sealed class EventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();

    public IDisposable Subscribe<T>(Action<T> handler) where T : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        var list = GetOrCreate(typeof(T));
        list.Add(handler);
        return new Subscription(() => list.Remove(handler));
    }

    public void Publish<T>(T payload) where T : IEvent
    {
        if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
            return;

        var snapshot = list.ToArray();
        foreach (var handler in snapshot)
            ((Action<T>)handler)(payload);
    }

    public void Clear() => _handlers.Clear();

    private List<Delegate> GetOrCreate(Type key)
    {
        if (!_handlers.TryGetValue(key, out var list))
        {
            list = new List<Delegate>();
            _handlers[key] = list;
        }

        return list;
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose()
        {
            _unsubscribe?.Invoke();
            _unsubscribe = null;
        }
    }
}
