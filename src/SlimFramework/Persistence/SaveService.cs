using Google.Protobuf;
using SlimFramework.Contracts.Save.V1;

namespace SlimFramework.Persistence;

/// <summary>槽位摘要。只有显示名和 section id，没有玩法字节。</summary>
public readonly record struct SaveSlotInfo(string DisplayName, IReadOnlyList<string> SectionIds);

/// <summary>
/// 用 <see cref="SaveSlot"/> 信封读写槽位。玩法数据只出现在 contributor 的字节里。
/// </summary>
public sealed class SaveService : IModule
{
    public const int DefaultFormatVersion = 1;

    private readonly ISaveStore _store;
    private readonly Dictionary<string, ISaveContributor> _contributors = new(StringComparer.Ordinal);

    public SaveService(ISaveStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public int MaxSlots { get; set; } = 100;

    public string CurrentDisplayName { get; private set; } = "";

    public void Register(ISaveContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        if (string.IsNullOrWhiteSpace(contributor.SectionId))
            throw new ArgumentException("SectionId 不能为空。", nameof(contributor));
        _contributors[contributor.SectionId] = contributor;
    }

    /// <summary>
    /// 覆盖槽位。该槽已有、且当前没有注册 contributor 的 section 原样写回。
    /// </summary>
    public void Save(int slot, string displayName)
    {
        Validate(slot);
        var message = new SaveSlot
        {
            FormatVersion = DefaultFormatVersion,
            DisplayName = displayName ?? "",
        };

        if (_store.TryRead(slot, out var existing) && existing.Length > 0)
        {
            var previous = ParseSupported(existing);
            foreach (var pair in previous.Sections)
            {
                if (!_contributors.ContainsKey(pair.Key))
                    message.Sections[pair.Key] = pair.Value;
            }
        }

        foreach (var pair in _contributors)
            message.Sections[pair.Key] = ByteString.CopyFrom(pair.Value.Capture());

        _store.Write(slot, message.ToByteArray());
        CurrentDisplayName = message.DisplayName;
    }

    public bool TryLoad(int slot)
    {
        Validate(slot);
        if (!_store.TryRead(slot, out var payload) || payload.Length == 0)
            return false;
        Apply(payload);
        return true;
    }

    /// <summary>读取显示名和 section id。不调用 <see cref="ISaveContributor.Restore"/>，也不改 <see cref="CurrentDisplayName"/>。</summary>
    public bool TryPeek(int slot, out SaveSlotInfo info)
    {
        Validate(slot);
        if (!_store.TryRead(slot, out var payload) || payload.Length == 0)
        {
            info = default;
            return false;
        }

        var message = ParseSupported(payload);
        info = new SaveSlotInfo(message.DisplayName, message.Sections.Keys.ToArray());
        return true;
    }

    public bool SlotExists(int slot)
    {
        Validate(slot);
        return _store.Exists(slot);
    }

    /// <summary>删除槽位文件。不调用 <see cref="ISaveContributor.Restore"/>。</summary>
    public bool TryDeleteSlot(int slot)
    {
        Validate(slot);
        return _store.Delete(slot);
    }

    /// <summary>已占用的槽位号。只问存储里有没有字节，不解析，也不改玩法状态。</summary>
    public IReadOnlyList<int> EnumerateOccupiedSlots()
    {
        var occupied = new List<int>();
        for (var slot = 0; slot < MaxSlots; slot++)
        {
            if (_store.Exists(slot))
                occupied.Add(slot);
        }

        return occupied;
    }

    public void Apply(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var message = ParseSupported(payload);

        CurrentDisplayName = message.DisplayName;
        foreach (var pair in _contributors)
        {
            if (message.Sections.TryGetValue(pair.Key, out var section))
                pair.Value.Restore(section.ToByteArray());
            else
                pair.Value.Restore(Array.Empty<byte>());
        }
    }

    public void Init(SlimApp app)
    {
    }

    public void Shutdown()
    {
    }

    private static SaveSlot ParseSupported(byte[] payload)
    {
        var message = SaveSlot.Parser.ParseFrom(payload);
        if (message.FormatVersion <= 0)
            throw new InvalidDataException("存档 format_version 非法。");
        if (message.FormatVersion > DefaultFormatVersion)
            throw new InvalidDataException(
                $"存档 format_version {message.FormatVersion} 高于当前 {DefaultFormatVersion}，尚无迁移器。");
        return message;
    }

    private void Validate(int slot)
    {
        if (slot < 0 || slot >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"槽位须在 [0, {MaxSlots - 1}]。");
    }
}
