namespace SlimFramework.Flow;

/// <summary>字符串 Id 状态机。Enter/Exit 在调用 GoTo 的线程同步执行。</summary>
public sealed class FlowMachine : IModule
{
    private readonly Dictionary<string, IFlowState> _states = new(StringComparer.Ordinal);
    private readonly IPauseSink? _pause;
    private SlimApp? _app;
    private IFlowState? _current;

    public FlowMachine(IPauseSink? pause = null) => _pause = pause;

    public string? CurrentId => _current?.Id;

    public bool IsPaused { get; private set; }

    public void Register(IFlowState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (string.IsNullOrWhiteSpace(state.Id))
            throw new ArgumentException("流程 Id 不能为空。", nameof(state));
        _states[state.Id] = state;
    }

    public void GoTo(string id)
    {
        if (_app == null)
            throw new InvalidOperationException("FlowMachine 尚未 Init。");
        if (!_states.TryGetValue(id, out var next))
            throw new InvalidOperationException($"未注册流程：{id}。");
        if (ReferenceEquals(_current, next))
            return;

        var from = _current?.Id;
        _current?.Exit();
        _current = next;
        next.Enter(_app);
        _app.Events.Publish(new FlowChanged(from, id, IsPaused));
    }

    public void Pause()
    {
        if (IsPaused)
            return;
        IsPaused = true;
        _pause?.SetPaused(true);
        _app?.Events.Publish(new FlowChanged(CurrentId, CurrentId, true));
    }

    public void Resume()
    {
        if (!IsPaused)
            return;
        IsPaused = false;
        _pause?.SetPaused(false);
        _app?.Events.Publish(new FlowChanged(CurrentId, CurrentId, false));
    }

    public void Init(SlimApp app) => _app = app;

    public void Shutdown()
    {
        _current?.Exit();
        _current = null;
        if (IsPaused)
        {
            IsPaused = false;
            _pause?.SetPaused(false);
        }

        _states.Clear();
        _app = null;
    }
}
