using Google.Protobuf;
using SlimFramework.Demo.V1;
using SlimFramework.Persistence;

namespace SlimFramework.Demo;

/// <summary>把 <see cref="ProfileState"/> 写进存档信封的一段。message 属于 Demo，不属于框架。</summary>
public sealed class ProfileContributor : ISaveContributor
{
    public const string Id = "demo.profile";

    private readonly ProfileState _state;

    public ProfileContributor(ProfileState state) => _state = state;

    public string SectionId => Id;

    public byte[] Capture() => new DemoProfile
    {
        PlayerName = _state.PlayerName,
        Coins = _state.Coins,
    }.ToByteArray();

    public void Restore(byte[] payload)
    {
        if (payload.Length == 0)
        {
            _state.PlayerName = "";
            _state.Coins = 0;
            return;
        }

        var message = DemoProfile.Parser.ParseFrom(payload);
        _state.PlayerName = message.PlayerName;
        _state.Coins = message.Coins;
    }
}

public sealed class ProfileState
{
    public string PlayerName { get; set; } = "";

    public int Coins { get; set; }
}
