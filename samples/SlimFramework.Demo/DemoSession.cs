using System.Globalization;
using Google.Protobuf;
using Luban;
using SlimFramework;
using SlimFramework.Config;
using SlimFramework.Contracts.Resource.V1;
using SlimFramework.Flow;
using SlimFramework.Persistence;
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

        var catalogMessage = new ResourceRefCatalog();
        catalogMessage.Refs[7] = new ResourceRef
        {
            RefKey = 7,
            Constant = "PotionIcon",
            Path = "res://icon.svg",
            Uid = "uid://potion",
        };
        var catalog = ResourceCatalog.Parse(catalogMessage.ToByteArray());
        if (!catalog.TryGet(7, out var entry) || entry.Constant != "PotionIcon" || entry.Path != "res://icon.svg")
            throw new InvalidOperationException("资源目录 protobuf 往返失败。");

        var mounted = SlimApp.Current;
        app.Shutdown();
        if (!ReferenceEquals(SlimApp.Current, mounted))
            throw new InvalidOperationException("Demo 不应改写 SlimApp.Current。");

        Console.WriteLine("SlimFramework demo OK");
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"item={potion.Name} coins={plugin.State.Coins} ref={entry.Uid}"));
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
