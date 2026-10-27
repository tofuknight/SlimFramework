using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>Music / SFX / UI 三条总线。音效用小对象池，播完释放资源引用。</summary>
public sealed class GodotAudio : IAudio
{
    public const string BusMusic = "Music";
    public const string BusSfx = "SFX";
    public const string BusUi = "UI";

    private const int PoolMax = 16;

    private readonly Node _parent;
    private readonly GodotAssetLoader _assets;
    private readonly Stack<AudioStreamPlayer> _pool = new();
    private readonly List<AssetTicket<AudioStream>> _liveSfx = new();
    private Node? _host;
    private AudioStreamPlayer? _music;
    private string? _musicPath;
    private bool _shuttingDown;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;
    private float _uiVolume = 1f;

    public GodotAudio(Node parent, GodotAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(assets);
        _parent = parent;
        _assets = assets;
    }

    public void Init(SlimApp app)
    {
        EnsureBuses();
        ApplyVolumes();
        _host = new Node { Name = "SlimAudio", ProcessMode = Node.ProcessModeEnum.Always };
        _music = new AudioStreamPlayer { Name = "Music", Bus = BusMusic };
        _host.AddChild(_music);
        _parent.AddChild(_host);
    }

    public void Shutdown()
    {
        _shuttingDown = true;
        StopMusic();
        foreach (var ticket in _liveSfx.ToArray())
            ticket.Dispose();
        _liveSfx.Clear();
        _host?.QueueFree();
        _host = null;
        _music = null;
        _pool.Clear();
    }

    public void SetVolume(AudioBus bus, float linear01)
    {
        linear01 = Mathf.Clamp(linear01, 0f, 1f);
        switch (bus)
        {
            case AudioBus.Music:
                _musicVolume = linear01;
                break;
            case AudioBus.Sfx:
                _sfxVolume = linear01;
                break;
            case AudioBus.Ui:
                _uiVolume = linear01;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
        }

        ApplyBus(bus, linear01);
    }

    public float GetVolume(AudioBus bus) => bus switch
    {
        AudioBus.Music => _musicVolume,
        AudioBus.Sfx => _sfxVolume,
        AudioBus.Ui => _uiVolume,
        _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, null),
    };

    public void PlayMusic(string path)
    {
        EnsureHost();
        if (_musicPath == path && _music!.Playing)
            return;

        StopMusic();
        var stream = _assets.Load<AudioStream>(path);
        _musicPath = path;
        _music!.Stream = stream;
        _music.Play();
    }

    public void StopMusic()
    {
        if (_music != null)
        {
            _music.Stop();
            _music.Stream = null;
        }

        if (_musicPath == null)
            return;
        _assets.Release(_musicPath);
        _musicPath = null;
    }

    public void PlaySfx(string path)
    {
        EnsureHost();
        var ticket = _assets.LoadTicket<AudioStream>(path);
        _liveSfx.Add(ticket);
        var player = Rent();
        player.Bus = BusSfx;
        player.Stream = ticket.Asset;
        player.Finished += OnFinished;
        player.Play();
        return;

        void OnFinished()
        {
            if (_shuttingDown || !GodotObject.IsInstanceValid(player))
                return;
            player.Finished -= OnFinished;
            _liveSfx.Remove(ticket);
            ticket.Dispose();
            Return(player);
        }
    }

    private void EnsureHost()
    {
        if (_host == null || _music == null)
            throw new InvalidOperationException("GodotAudio 尚未 Init。");
    }

    private AudioStreamPlayer Rent()
    {
        while (_pool.Count > 0)
        {
            var pooled = _pool.Pop();
            if (GodotObject.IsInstanceValid(pooled))
            {
                pooled.Stream = null;
                return pooled;
            }
        }

        var created = new AudioStreamPlayer();
        _host!.AddChild(created);
        return created;
    }

    private void Return(AudioStreamPlayer player)
    {
        player.Stop();
        player.Stream = null;
        if (_pool.Count >= PoolMax || _host == null)
        {
            player.QueueFree();
            return;
        }

        _pool.Push(player);
    }

    private void ApplyVolumes()
    {
        ApplyBus(AudioBus.Music, _musicVolume);
        ApplyBus(AudioBus.Sfx, _sfxVolume);
        ApplyBus(AudioBus.Ui, _uiVolume);
    }

    private static void ApplyBus(AudioBus bus, float linear)
    {
        var index = AudioServer.GetBusIndex(BusName(bus));
        if (index < 0)
            return;
        var db = linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear);
        AudioServer.SetBusVolumeDb(index, db);
    }

    private static void EnsureBuses()
    {
        EnsureBus(BusMusic);
        EnsureBus(BusSfx);
        EnsureBus(BusUi);
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0)
            return;
        AudioServer.AddBus();
        var index = AudioServer.GetBusCount() - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
    }

    private static string BusName(AudioBus bus) => bus switch
    {
        AudioBus.Music => BusMusic,
        AudioBus.Sfx => BusSfx,
        AudioBus.Ui => BusUi,
        _ => BusSfx,
    };
}
