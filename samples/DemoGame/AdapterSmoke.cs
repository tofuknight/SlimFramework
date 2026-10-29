using cfg;
using Godot;
using SlimFramework.Audio;
using SlimFramework.Demo;
using SlimFramework.GodotAdapter;
using SlimFramework.Persistence;
using SlimFramework.Resources;
using SlimFramework.UI;

namespace SlimFramework.DemoGame;

/// <summary>
/// 走 Bootstrap 已经挂上的 Godot 服务：UI 打开、UI 总线 <see cref="IAudio.PlayUi"/>、
/// 音量偏好、<c>res://</c> 表经 <see cref="GodotTableFiles.FromGodotRes"/>、user:// 存档往返。
/// 无头 Runner 不进这里。
/// </summary>
internal static class AdapterSmoke
{
    public const string PagePath = "res://SmokePage.tscn";

    /// <summary>短静音 WAV。Godot 导入成 <c>AudioStream</c> 后才能 <see cref="IAudio.PlayUi"/>。</summary>
    public const string UiClickPath = "res://ui-click.wav";

    /// <summary>
    /// 与 <c>samples/SlimFramework.Demo/Fixtures/tbitem.bytes</c> 同一份手写表。
    /// 不生成进 <c>src/SlimFramework</c>。
    /// </summary>
    public const string TableDirectory = "res://Config";

    public static void Run(SlimApp app, DemoPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(plugin);

        if (app.Get<IAssetLoader>() is not GodotAssetLoader)
            throw new InvalidOperationException("IAssetLoader 应是 GodotAssetLoader。");
        if (app.Get<IUiStack>() is not GodotUiStack ui)
            throw new InvalidOperationException("IUiStack 应是 GodotUiStack。");
        if (app.Get<IAudio>() is not GodotAudio audio)
            throw new InvalidOperationException("IAudio 应是 GodotAudio。");

        var opened = SmokePage.TimesOpened;
        ui.Open(PagePath, UiLayer.Hud);
        if (ui.OpenCount != 1 || SmokePage.TimesOpened != opened + 1)
            throw new InvalidOperationException("UI 栈没有打开 SmokePage。");
        ui.CloseTop();
        if (ui.OpenCount != 0)
            throw new InvalidOperationException("CloseTop 后栈应为空。");

        const string prefsPath = "user://" + GodotUserTextStore.DefaultRelativePath;
        if (Godot.FileAccess.FileExists(prefsPath))
            Godot.DirAccess.RemoveAbsolute(prefsPath);

        var prefs = app.Get<PreferenceStore>();
        prefs.SetFloat(AudioPreferences.MusicKey, 0.25f);
        prefs.SetFloat(AudioPreferences.SfxKey, 0.5f);
        prefs.SetFloat(AudioPreferences.UiKey, 0.75f);
        prefs.Flush();
        AudioPreferences.Apply(app);
        ExpectVolume(audio, AudioBus.Music, 0.25f);
        ExpectVolume(audio, AudioBus.Sfx, 0.5f);
        ExpectVolume(audio, AudioBus.Ui, 0.75f);
        audio.PlayUi(UiClickPath);
        ExpectPlayingOnUiBus();
        ExpectSampleTable();

        var reread = new PreferenceStore(new GodotUserTextStore(GodotUserTextStore.DefaultRelativePath));
        reread.Load();
        if (Math.Abs(reread.GetFloat(AudioPreferences.UiKey) - 0.75f) > 0.0001f)
            throw new InvalidOperationException("偏好没有通过 GodotUserTextStore 落盘。");
        if (Godot.FileAccess.FileExists(prefsPath + ".tmp"))
            throw new InvalidOperationException("偏好原子写入留下了临时文件。");

        var disk = new GodotUserSaveStore();
        var slotPath = disk.GetUserPath(0);
        disk.Delete(0);
        plugin.State.PlayerName = "godot";
        plugin.State.Coins = 7;
        var save = app.Get<SaveService>();
        save.Save(0, "godot-slot");
        plugin.State.Coins = 0;
        plugin.State.PlayerName = "";
        if (!save.TryLoad(0) || plugin.State.Coins != 7 || plugin.State.PlayerName != "godot")
            throw new InvalidOperationException("user:// 存档往返失败。");
        if (save.CurrentDisplayName != "godot-slot")
            throw new InvalidOperationException("user:// 存档显示名丢失。");
        if (!disk.TryRead(0, out var raw) || raw.Length == 0)
            throw new InvalidOperationException("GodotUserSaveStore 没有读到刚写入的槽位。");
        if (!Godot.FileAccess.FileExists(slotPath))
            throw new InvalidOperationException("槽位文件不在 user://。");
        if (Godot.FileAccess.FileExists(slotPath + ".tmp"))
            throw new InvalidOperationException("存档原子写入留下了临时文件。");

        GD.Print("SlimFramework Godot adapter smoke OK");
    }

    private static void ExpectVolume(IAudio audio, AudioBus bus, float expected)
    {
        if (Math.Abs(audio.GetVolume(bus) - expected) > 0.0001f)
            throw new InvalidOperationException($"{bus} 音量不是 {expected}。");
    }

    private static void ExpectPlayingOnUiBus()
    {
        if (AudioServer.GetBusIndex(GodotAudio.BusUi) < 0)
            throw new InvalidOperationException("AudioServer 上没有 UI 总线。");
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("没有 SceneTree，无法确认 PlayUi。");

        var found = 0;
        CountPlayingUiPlayers(tree.Root, ref found);
        if (found != 1)
            throw new InvalidOperationException($"PlayUi 应在 UI 总线上留下 1 个正在播放的播放器，实际 {found}。");
    }

    private static void CountPlayingUiPlayers(Node node, ref int found)
    {
        if (node is AudioStreamPlayer player
            && player.Bus == GodotAudio.BusUi
            && player.Stream != null
            && player.Playing)
            found++;

        foreach (var child in node.GetChildren())
            CountPlayingUiPlayers(child, ref found);
    }

    private static void ExpectSampleTable()
    {
        var tables = new Tables(GodotTableFiles.FromGodotRes(TableDirectory));
        var potion = tables.TbItem.Get(1);
        var key = tables.TbItem.Get(2);
        if (potion.Name != "potion" || potion.Desc != "heal" || potion.Count != 3)
            throw new InvalidOperationException("FromGodotRes 没有读到 potion。");
        if (key.Name != "key" || key.Count != 1 || tables.TbItem.DataList.Count != 2)
            throw new InvalidOperationException("FromGodotRes 没有读到 key。");
    }
}
