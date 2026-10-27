namespace SlimFramework;

/// <summary>每帧回调列表。回调内增删不会影响本轮遍历。</summary>
public sealed class TickList
{
    private readonly List<ITickable> _items = new();

    public void Add(ITickable tickable)
    {
        ArgumentNullException.ThrowIfNull(tickable);
        if (!_items.Contains(tickable))
            _items.Add(tickable);
    }

    public void Remove(ITickable tickable) => _items.Remove(tickable);

    public void Tick(double delta)
    {
        var snapshot = _items.ToArray();
        foreach (var item in snapshot)
            item.Tick(delta);
    }
}
