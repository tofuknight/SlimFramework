using System.Globalization;
using Google.Protobuf;
using Luban;
using SlimFramework;
using SlimFramework.Audio;
using SlimFramework.Config;
using SlimFramework.Contracts.Resource.V1;
using SlimFramework.Contracts.Save.V1;
using SlimFramework.Flow;
using SlimFramework.Persistence;
using SlimFramework.Demo.V1;
using SlimFramework.Resources;
using cfg;

namespace SlimFramework.Demo;

/// <summary>
/// 无 Godot 进程的启动证明：Luban 表、protobuf 存档、流程、偏好、资源目录。
/// </summary>
public static class DemoSession
{
    public static void Run()
    {
        var saves = new MemorySaveStore();
        var texts = new MemoryTextStore();
        var tick = new FrameCounter();
        var app = new SlimApp();
        app.Add(new SaveService(saves));
        app.Add(new PreferenceStore(texts));
        app.Add(new FlowMachine());
        app.Add(tick);

        var plugin = new DemoPlugin();
        app.Use(plugin);
        app.Init();

        var fixture = LoadFixture();
        var encoded = EncodeFixture();
        if (!fixture.AsSpan().SequenceEqual(encoded))
            throw new InvalidOperationException("tbitem.bytes 与 Luban ByteBuf 编码不一致: " + Convert.ToHexString(fixture));

        var tables = new Tables(TableFiles.FromReader(name =>
            name == "tbitem" ? fixture : throw new FileNotFoundException(name)));
        var potion = tables.TbItem.Get(1);
        var key = tables.TbItem.Get(2);
        if (potion.Name != "potion" || potion.Desc != "heal" || potion.Count != 3)
            throw new InvalidOperationException("Luban 表 potion 读取失败。");
        if (key.Name != "key" || key.Count != 1 || tables.TbItem.DataList.Count != 2)
            throw new InvalidOperationException("Luban 表 key 读取失败。");

        plugin.State.PlayerName = "ada";
        plugin.State.Coins = 12;
        var save = app.Get<SaveService>();
        save.Save(0, "slot-a");
        plugin.State.Coins = 0;
        plugin.State.PlayerName = "";
        if (save.TryLoad(1))
            throw new InvalidOperationException("空槽位不应读到存档。");
        if (!save.TryLoad(0) || plugin.State.Coins != 12 || plugin.State.PlayerName != "ada")
            throw new InvalidOperationException("Protobuf 存档往返失败。");
        if (save.CurrentDisplayName != "slot-a")
            throw new InvalidOperationException("存档显示名丢失。");

        if (!saves.TryRead(0, out var raw))
            throw new InvalidOperationException("槽位 0 应已写入。");
        var envelope = SaveSlot.Parser.ParseFrom(raw);
        envelope.Sections["future.pack"] = ByteString.CopyFromUtf8("keep-me");
        saves.Write(0, envelope.ToByteArray());
        plugin.State.Coins = 4;
        save.Save(0, "slot-a");
        if (!saves.TryRead(0, out raw))
            throw new InvalidOperationException("槽位 0 在第二次保存后丢失。");
        envelope = SaveSlot.Parser.ParseFrom(raw);
        if (!envelope.Sections.TryGetValue("future.pack", out var kept) || kept.ToStringUtf8() != "keep-me")
            throw new InvalidOperationException("未注册的存档段被丢掉了。");

        plugin.State.Coins = 0;
        plugin.State.PlayerName = "mutated";
        if (!save.TryPeek(0, out var info)
            || info.DisplayName != "slot-a"
            || !info.SectionIds.Contains(ProfileContributor.Id)
            || !info.SectionIds.Contains("future.pack"))
            throw new InvalidOperationException("Peek 没有读到显示名或 section id。");
        if (plugin.State.Coins != 0 || plugin.State.PlayerName != "mutated" || save.CurrentDisplayName != "slot-a")
            throw new InvalidOperationException("Peek 不应改写当前状态。");
        if (!save.TryLoad(0) || plugin.State.Coins != 4 || plugin.State.PlayerName != "ada")
            throw new InvalidOperationException("保留未知段后，已注册段应仍能读回。");

        var future = new SaveSlot
        {
            FormatVersion = SaveService.DefaultFormatVersion + 1,
            DisplayName = "later",
        };
        future.Sections["future.pack"] = ByteString.CopyFromUtf8("new");
        saves.Write(3, future.ToByteArray());
        if (!save.SlotExists(3))
            throw new InvalidOperationException("高版本槽位应仍算占用。");
        ExpectUnsupported(() => save.TryPeek(3, out var _));
        ExpectUnsupported(() => save.TryLoad(3));
        ExpectUnsupported(() => save.Save(3, "nope"));
        if (!saves.TryRead(3, out var futureBytes)
            || SaveSlot.Parser.ParseFrom(futureBytes).FormatVersion != SaveService.DefaultFormatVersion + 1
            || SaveSlot.Parser.ParseFrom(futureBytes).DisplayName != "later")
            throw new InvalidOperationException("拒绝高版本时不应改写槽位。");
        if (plugin.State.Coins != 4)
            throw new InvalidOperationException("拒绝高版本时不应 Restore。");
        saves.Write(4, new SaveSlot { FormatVersion = 0, DisplayName = "bad" }.ToByteArray());
        ExpectUnsupported(() => save.TryLoad(4));

        var occupied = save.EnumerateOccupiedSlots();
        if (!occupied.Contains(0) || !occupied.Contains(3) || occupied.Contains(1))
            throw new InvalidOperationException("占用槽位列表不正确。");
        if (save.TryDeleteSlot(1))
            throw new InvalidOperationException("空槽位删除应返回 false。");
        if (!save.TryDeleteSlot(3) || save.SlotExists(3) || save.EnumerateOccupiedSlots().Contains(3))
            throw new InvalidOperationException("删除槽位失败。");
        if (plugin.State.Coins != 4)
            throw new InvalidOperationException("删除槽位不应改写玩法状态。");

        RunAudioPreferenceCheck();

        var prefs = app.Get<PreferenceStore>();
        prefs.SetFloat("music", 0.25f);
        prefs.Flush();
        var reloaded = new PreferenceStore(texts);
        reloaded.Init(app);
        if (Math.Abs(reloaded.GetFloat("music") - 0.25f) > 0.0001f)
            throw new InvalidOperationException("偏好往返失败。");

        var flowEvents = 0;
        using var _ = app.Events.Subscribe<FlowChanged>(_ => flowEvents++);
        var flow = app.Get<FlowMachine>();
        flow.GoTo("boot");
        flow.GoTo("play");
        flow.Pause();
        if (flow.CurrentId != "play" || !flow.IsPaused || flowEvents != 3)
            throw new InvalidOperationException("流程事件不符合预期。");

        app.TickFrame(0.016);
        app.TickFrame(0.016);
        if (tick.Frames != 2)
            throw new InvalidOperationException("Tick 未驱动模块。");

        var entry = RunResourceCatalogChecks();

        RunMigrationRegression(saves);

        var mounted = SlimApp.Current;
        app.Shutdown();
        if (!ReferenceEquals(SlimApp.Current, mounted))
            throw new InvalidOperationException("Demo 不应改写 SlimApp.Current。");

        Console.WriteLine("SlimFramework demo OK");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"item={potion.Name} coins={plugin.State.Coins} ref={entry.Uid}"));
    }

    private static ResourceRefEntry RunResourceCatalogChecks()
    {
        var catalogMessage = new ResourceRefCatalog();
        catalogMessage.Refs[7] = new ResourceRef
        {
            RefKey = 7,
            Constant = "PotionIcon",
            Path = "res://icon.svg",
            Uid = "uid://potion",
        };
        catalogMessage.Refs[8] = new ResourceRef
        {
            RefKey = 8,
            Constant = "PathOnly",
            Path = "res://path-only.svg",
        };
        var catalog = ResourceCatalog.Parse(catalogMessage.ToByteArray());
        if (!catalog.TryGet(7, out var entry) || entry.Constant != "PotionIcon" || entry.Path != "res://icon.svg")
            throw new InvalidOperationException("资源目录 protobuf 往返失败。");
        if (entry.LoadKey != "uid://potion")
            throw new InvalidOperationException("非空 Uid 应作为加载地址。");
        if (!catalog.TryGet(8, out var pathOnly) || pathOnly.LoadKey != "res://path-only.svg")
            throw new InvalidOperationException("Uid 为空时应使用 Path。");
        if (!catalog.TryGet("PotionIcon", out var byName) || byName.RefKey != 7 || byName.LoadKey != "uid://potion")
            throw new InvalidOperationException("按 DisplayKey 查找失败。");
        if (!catalog.TryGet("PathOnly", out var pathByName) || pathByName.RefKey != 8)
            throw new InvalidOperationException("按 DisplayKey 查找 PathOnly 失败。");
        if (catalog.TryGet("potionicon", out _) || catalog.TryGet("", out _) || catalog.TryGet(" ", out _) || catalog.TryGet("missing", out _))
            throw new InvalidOperationException("DisplayKey 查找应区分大小写，并且空名字和缺名字都找不到。");

        var loader = new RecordingLoader();
        if (loader.Load<string>(entry) != "uid://potion" || loader.Load<string>(catalog, 8) != "res://path-only.svg")
            throw new InvalidOperationException("按资源引用加载没有走 LoadKey。");
        if (loader.Load<string>(catalog, "PotionIcon") != "uid://potion"
            || loader.Load<string>(catalog, "PathOnly") != "res://path-only.svg")
            throw new InvalidOperationException("按 DisplayKey 加载没有走 LoadKey。");
        ExpectMissingDisplayKey(() => loader.Load<string>(catalog, "MissingIcon"));
        ExpectBlankDisplayKey(() => loader.Load<string>(catalog, " "));

        var cases = new ResourceRefCatalog();
        cases.Refs[1] = new ResourceRef { RefKey = 1, Constant = "Icon", Path = "res://a.svg" };
        cases.Refs[2] = new ResourceRef { RefKey = 2, Constant = "icon", Path = "res://b.svg" };
        var parsedCases = ResourceCatalog.Parse(cases.ToByteArray());
        if (!parsedCases.TryGet("Icon", out var upper) || upper.RefKey != 1
            || !parsedCases.TryGet("icon", out var lower) || lower.RefKey != 2)
            throw new InvalidOperationException("DisplayKey 应按序数比较，不忽略大小写。");

        var unnamed = new ResourceRefCatalog();
        unnamed.Refs[1] = new ResourceRef { RefKey = 1, Path = "res://a.svg" };
        unnamed.Refs[2] = new ResourceRef { RefKey = 2, Constant = "", Uid = "uid://b" };
        var parsedUnnamed = ResourceCatalog.Parse(unnamed.ToByteArray());
        if (parsedUnnamed.Count != 2 || parsedUnnamed.TryGet("", out _))
            throw new InvalidOperationException("空 constant 应允许重复，且不能按空名字查到。");
        if (!parsedUnnamed.TryGet(1, out var firstUnnamed) || firstUnnamed.Constant != "" || firstUnnamed.LoadKey != "res://a.svg")
            throw new InvalidOperationException("空 constant 的条目应按 ref_key 取到 Path。");
        if (!parsedUnnamed.TryGet(2, out var secondUnnamed) || secondUnnamed.LoadKey != "uid://b")
            throw new InvalidOperationException("空 constant 且有 Uid 的条目应使用 Uid。");

        var spacedUid = new ResourceRefCatalog();
        spacedUid.Refs[9] = new ResourceRef { RefKey = 9, Constant = "SpacedUid", Uid = "   ", Path = "res://spaced.svg" };
        var parsedSpaced = ResourceCatalog.Parse(spacedUid.ToByteArray());
        if (!parsedSpaced.TryGet("SpacedUid", out var spaced) || spaced.LoadKey != "res://spaced.svg")
            throw new InvalidOperationException("空白 Uid 应视为没有 Uid，LoadKey 用 Path。");

        var mismatch = new ResourceRefCatalog();
        mismatch.Refs[1] = new ResourceRef { RefKey = 2, Constant = "Mismatch", Path = "res://mismatch.svg" };
        ExpectCatalogRejected(mismatch, "不一致");

        var duplicate = new ResourceRefCatalog();
        duplicate.Refs[1] = new ResourceRef { RefKey = 1, Constant = "Same", Path = "res://a.svg" };
        duplicate.Refs[2] = new ResourceRef { RefKey = 2, Constant = "Same", Uid = "uid://b" };
        ExpectCatalogRejected(duplicate, "重复");

        var bare = new ResourceRefCatalog();
        bare.Refs[4] = new ResourceRef { RefKey = 4, Constant = "Bare" };
        ExpectCatalogRejected(bare, "没有 Uid 或 Path");

        var blank = new ResourceRefCatalog();
        blank.Refs[5] = new ResourceRef { RefKey = 5, Constant = " ", Path = "res://a.svg" };
        ExpectCatalogRejected(blank, "空白");

        var padded = new ResourceRefCatalog();
        padded.Refs[6] = new ResourceRef { RefKey = 6, Constant = " Icon", Path = "res://a.svg" };
        ExpectCatalogRejected(padded, "空白");

        return entry;
    }

    private static void ExpectCatalogRejected(ResourceRefCatalog message, string fragment)
    {
        try
        {
            ResourceCatalog.Parse(message.ToByteArray());
        }
        catch (InvalidDataException ex)
        {
            if (ex.Message.Contains(fragment, StringComparison.Ordinal))
                return;
            throw new InvalidOperationException($"拒绝原因应包含「{fragment}」，实际：{ex.Message}");
        }

        throw new InvalidOperationException($"资源目录应拒绝（{fragment}）。");
    }

    private static void ExpectMissingDisplayKey(Action action)
    {
        try
        {
            action();
        }
        catch (KeyNotFoundException)
        {
            return;
        }

        throw new InvalidOperationException("缺少的 DisplayKey 应失败。");
    }

    private static void ExpectBlankDisplayKey(Action action)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }

        throw new InvalidOperationException("空白 DisplayKey 应失败。");
    }

    private static void RunAudioPreferenceCheck()
    {
        var audio = new VolumeAudio();
        var app = new SlimApp();
        var prefs = new PreferenceStore(new MemoryTextStore());
        app.Add(prefs);
        app.Add(audio);
        app.Init();

        AudioPreferences.Apply(app);
        if (Math.Abs(audio.GetVolume(AudioBus.Ui) - 1f) > 0.0001f)
            throw new InvalidOperationException("缺键时不应改音量。");

        prefs.SetFloat(AudioPreferences.MusicKey, 0.2f);
        prefs.SetFloat(AudioPreferences.SfxKey, 0.4f);
        prefs.SetFloat(AudioPreferences.UiKey, 0.8f);
        AudioPreferences.Apply(app);
        if (Math.Abs(audio.GetVolume(AudioBus.Music) - 0.2f) > 0.0001f
            || Math.Abs(audio.GetVolume(AudioBus.Sfx) - 0.4f) > 0.0001f
            || Math.Abs(audio.GetVolume(AudioBus.Ui) - 0.8f) > 0.0001f)
            throw new InvalidOperationException("音量偏好没有套到 IAudio。");

        audio.PlayUi("res://ui.wav");
        audio.PlaySfx("res://sfx.wav");
        if (audio.UiPlays != 1 || audio.SfxPlays != 1)
            throw new InvalidOperationException("PlayUi 与 PlaySfx 应分开计数。");
        app.Shutdown();
    }

    private static void RunMigrationRegression(MemorySaveStore saves)
    {
        var legacy = new SaveSlot
        {
            FormatVersion = 1,
            DisplayName = "legacy",
        };
        legacy.Sections[ProfileContributor.Id] = ByteString.CopyFrom(new DemoProfile
        {
            PlayerName = "old",
            Coins = 9,
        }.ToByteArray());
        saves.Write(8, legacy.ToByteArray());

        var blockedState = new ProfileState { PlayerName = "stay", Coins = 3 };
        var blocked = new SaveService(saves) { FormatVersion = 2 };
        blocked.Register(new ProfileContributor(blockedState));
        ExpectUnsupported(() => blocked.TryPeek(8, out _));
        ExpectUnsupported(() => blocked.TryLoad(8));
        ExpectUnsupported(() => blocked.Save(8, "nope"));
        if (blockedState.Coins != 3 || blockedState.PlayerName != "stay")
            throw new InvalidOperationException("缺少迁移器时不应 Restore。");
        if (!SameLegacy(saves))
            throw new InvalidOperationException("缺少迁移器时不应改写旧档。");

        var loaded = new ProfileState();
        var migrating = new SaveService(saves) { FormatVersion = 2 };
        migrating.Register(new ProfileContributor(loaded));
        migrating.RegisterMigrator(new DemoFormatMigrator());
        if (!migrating.TryPeek(8, out var peeked)
            || peeked.DisplayName != "legacy"
            || !peeked.SectionIds.Contains(ProfileContributor.Id)
            || !peeked.SectionIds.Contains(DemoFormatMigrator.MarkerSection))
            throw new InvalidOperationException("Peek 应看到迁移后的段，且不改玩法。");
        if (loaded.Coins != 0 || loaded.PlayerName != "")
            throw new InvalidOperationException("Peek 不应 Restore。");
        if (!SameLegacy(saves))
            throw new InvalidOperationException("Peek 不应改写槽位文件。");
        if (!migrating.TryLoad(8) || loaded.Coins != 9 || loaded.PlayerName != "old" || migrating.CurrentDisplayName != "legacy")
            throw new InvalidOperationException("迁移后应读回旧段。");
        if (!SameLegacy(saves))
            throw new InvalidOperationException("加载迁移不应改写槽位文件。");

        migrating.Save(8, "legacy");
        if (!saves.TryRead(8, out var upgraded))
            throw new InvalidOperationException("迁移后保存失败。");
        var written = SaveSlot.Parser.ParseFrom(upgraded);
        if (written.FormatVersion != 2 || written.DisplayName != "legacy")
            throw new InvalidOperationException("保存应写回当前 format_version。");
        if (!written.Sections.TryGetValue(DemoFormatMigrator.MarkerSection, out var marker) || marker.ToStringUtf8() != "1to2")
            throw new InvalidOperationException("迁移标记段没有写回。");
        if (!written.Sections.ContainsKey(ProfileContributor.Id))
            throw new InvalidOperationException("迁移后玩法段丢失。");
    }

    private static bool SameLegacy(MemorySaveStore saves)
    {
        if (!saves.TryRead(8, out var raw))
            return false;
        var message = SaveSlot.Parser.ParseFrom(raw);
        return message.FormatVersion == 1
            && message.DisplayName == "legacy"
            && !message.Sections.ContainsKey(DemoFormatMigrator.MarkerSection);
    }

    private static void ExpectUnsupported(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new InvalidOperationException("应拒绝不受支持的 format_version。");
    }

    private static byte[] LoadFixture()
    {
        var assembly = typeof(DemoSession).Assembly;
        using var stream = assembly.GetManifestResourceStream("SlimFramework.Demo.Fixtures.tbitem.bytes")
            ?? throw new InvalidOperationException("缺少嵌入的 tbitem.bytes。");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[] EncodeFixture()
    {
        var buf = new ByteBuf();
        buf.WriteSize(2);
        WriteItem(buf, 1, "potion", "heal", 3);
        WriteItem(buf, 2, "key", "door", 1);
        return buf.CopyData();
    }

    private static void WriteItem(ByteBuf buf, int id, string name, string desc, int count)
    {
        buf.WriteInt(id);
        buf.WriteString(name);
        buf.WriteString(desc);
        buf.WriteInt(count);
    }

    private sealed class VolumeAudio : IAudio
    {
        public float Music { get; private set; } = 1f;

        public float Sfx { get; private set; } = 1f;

        public float Ui { get; private set; } = 1f;

        public int UiPlays { get; private set; }

        public int SfxPlays { get; private set; }

        public void SetVolume(AudioBus bus, float linear01)
        {
            switch (bus)
            {
                case AudioBus.Music:
                    Music = linear01;
                    break;
                case AudioBus.Sfx:
                    Sfx = linear01;
                    break;
                case AudioBus.Ui:
                    Ui = linear01;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bus), bus, null);
            }
        }

        public float GetVolume(AudioBus bus) => bus switch
        {
            AudioBus.Music => Music,
            AudioBus.Sfx => Sfx,
            AudioBus.Ui => Ui,
            _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, null),
        };

        public void PlayMusic(string path)
        {
        }

        public void StopMusic()
        {
        }

        public void PlaySfx(string path) => SfxPlays++;

        public void PlayUi(string path) => UiPlays++;

        public void Init(SlimApp app)
        {
        }

        public void Shutdown()
        {
        }
    }

    private sealed class RecordingLoader : IAssetLoader
    {
        public T Load<T>(string path) where T : class
        {
            if (typeof(T) != typeof(string))
                throw new InvalidOperationException("Demo loader 只用于核对加载地址。");
            return (T)(object)path;
        }

        public void Release(string path)
        {
        }
    }

    private sealed class FrameCounter : IModule, ITickable
    {
        public int Frames { get; private set; }

        public void Init(SlimApp app)
        {
        }

        public void Shutdown()
        {
        }

        public void Tick(double delta) => Frames++;
    }
}
