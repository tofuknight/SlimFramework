namespace SlimFramework.Persistence;

/// <summary>进程内槽位，供测试与无 Godot 的 Demo。</summary>
public sealed class MemorySaveStore : ISaveStore
{
    private readonly Dictionary<int, byte[]> _slots = new();

    public void Write(int slot, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _slots[slot] = payload.ToArray();
    }

    public bool TryRead(int slot, out byte[] payload)
    {
        if (_slots.TryGetValue(slot, out var found))
        {
            payload = found.ToArray();
            return true;
        }

        payload = Array.Empty<byte>();
        return false;
    }

    public bool Delete(int slot) => _slots.Remove(slot);
}
