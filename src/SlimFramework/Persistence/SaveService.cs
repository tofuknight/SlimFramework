using Google.Protobuf;
using SlimFramework.Contracts.Save.V1;

namespace SlimFramework.Persistence;

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

    public void Save(int slot, string displayName)
    {
        Validate(slot);
        var message = new SaveSlot
        {
            FormatVersion = DefaultFormatVersion,
            DisplayName = displayName ?? "",
        };
        foreach (var pair in _contributors)
            message.Sections[pair.Key] = ByteString.CopyFrom(pair.Value.Capture());

        _store.Write(slot, message.ToByteArray());
        CurrentDisplayName = message.DisplayName;
    }

    public bool TryLoad(int slot)
    {
        Validate(slot);
        if (!_store.TryRead(slot, out var payload))
            return false;
        Apply(payload);
        return true;
    }

    public void Apply(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var message = SaveSlot.Parser.ParseFrom(payload);
        if (message.FormatVersion <= 0)
            throw new InvalidDataException("存档 format_version 非法。");

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

    private void Validate(int slot)
    {
        if (slot < 0 || slot >= MaxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"槽位须在 [0, {MaxSlots - 1}]。");
    }
}
