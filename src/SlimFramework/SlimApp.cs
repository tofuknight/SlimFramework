namespace SlimFramework;

/// <summary>
/// 组合根。游戏与插件依赖它；它不依赖游戏。
/// 模块约定在主线程使用，内部不加锁。
/// </summary>
public sealed class SlimApp : IDisposable
{
    private readonly List<IModule> _modules = new();
    private readonly Dictionary<Type, object> _services = new();
    private readonly TickList _tick = new();
    private bool _initialized;

    /// <summary>最近一次 <see cref="Mount"/> 的实例，供节点脚本取用。</summary>
    public static SlimApp? Current { get; private set; }

    public EventBus Events { get; } = new();

    public static void Mount(SlimApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Current = app;
    }

    public static void Unmount(SlimApp app)
    {
        if (ReferenceEquals(Current, app))
            Current = null;
    }

    public SlimApp Add(IModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (_initialized)
            throw new InvalidOperationException("Init 之后不能再 Add 模块。请在 Init 前注册。");

        Bind(module.GetType(), module);
        foreach (var iface in module.GetType().GetInterfaces())
        {
            if (iface == typeof(IModule) || iface == typeof(ITickable) || iface == typeof(IDisposable))
                continue;
            Bind(iface, module);
        }

        _modules.Add(module);
        if (module is ITickable tickable)
            _tick.Add(tickable);
        return this;
    }

    public SlimApp Use(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrWhiteSpace(plugin.Id))
            throw new ArgumentException("插件 Id 不能为空。", nameof(plugin));
        plugin.Register(this);
        return this;
    }

    public T Get<T>() where T : class
    {
        if (TryGet<T>(out var service))
            return service!;
        throw new InvalidOperationException($"未注册服务 {typeof(T).FullName}。");
    }

    public bool TryGet<T>(out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var found) && found is T typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    public void Init()
    {
        if (_initialized)
            return;

        foreach (var module in _modules)
            module.Init(this);
        _initialized = true;
    }

    public void TickFrame(double delta) => _tick.Tick(delta);

    public void Shutdown()
    {
        if (!_initialized)
            return;

        for (var i = _modules.Count - 1; i >= 0; i--)
            _modules[i].Shutdown();
        _initialized = false;
        Events.Clear();
    }

    public void Dispose()
    {
        Shutdown();
        Unmount(this);
    }

    private void Bind(Type key, object service)
    {
        if (_services.TryGetValue(key, out var existing) && !ReferenceEquals(existing, service))
            throw new InvalidOperationException($"服务 {key.FullName} 已由 {existing.GetType().FullName} 注册。");
        _services[key] = service;
    }
}
