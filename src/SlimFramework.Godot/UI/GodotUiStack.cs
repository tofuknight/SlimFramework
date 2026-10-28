using Godot;

namespace SlimFramework.GodotAdapter;

/// <summary>
/// 全屏 UI 宿主。打开顺序是栈，<see cref="CloseTop"/> 关闭最后打开的页面。
/// <see cref="UiLayer"/> 只参与绘制顺序。可选按路径缓存一份实例。
/// </summary>
public sealed class GodotUiStack : IUiStack
{
    private readonly Node _parent;
    private readonly GodotAssetLoader _assets;
    private readonly List<PageEntry> _stack = new();
    private readonly Dictionary<string, PageEntry> _cache = new(StringComparer.Ordinal);
    private CanvasLayer? _canvas;
    private Control? _host;
    private int _subOrder;

    public GodotUiStack(Node parent, GodotAssetLoader assets)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(assets);
        _parent = parent;
        _assets = assets;
    }

    public int OpenCount => _stack.Count;

    public void Init(SlimApp app)
    {
        _canvas = new CanvasLayer
        {
            Name = "SlimUI",
            Layer = 128,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        _host = new Control { Name = "SlimUIHost", MouseFilter = Control.MouseFilterEnum.Ignore };
        FullRect(_host);
        _canvas.AddChild(_host);
        _parent.AddChild(_canvas);
    }

    public void Shutdown()
    {
        foreach (var entry in _stack.ToArray())
        {
            if (entry.Root is UiPage page && GodotObject.IsInstanceValid(entry.Root))
                page.OnClose();
            Destroy(entry);
        }
        _stack.Clear();
        foreach (var entry in _cache.Values)
            Destroy(entry);
        _cache.Clear();
        _canvas?.QueueFree();
        _canvas = null;
        _host = null;
    }

    public void Open(string scenePath, UiLayer layer = UiLayer.Auto, bool cache = false) =>
        Show(scenePath, layer, cache);

    public Control Show(string scenePath, UiLayer layer = UiLayer.Auto, bool cache = false)
    {
        if (_host == null)
            throw new InvalidOperationException("GodotUiStack 尚未 Init。");
        if (string.IsNullOrWhiteSpace(scenePath))
            throw new ArgumentException("场景路径不能为空。", nameof(scenePath));

        PageEntry entry;
        if (cache && _cache.Remove(scenePath, out var cached))
        {
            entry = cached;
            entry.SubOrder = ++_subOrder;
            entry.Root.Visible = true;
            entry.Root.MouseFilter = Control.MouseFilterEnum.Stop;
        }
        else
        {
            var ticket = _assets.LoadTicket<PackedScene>(scenePath);
            var node = ticket.Asset.Instantiate();
            if (node is not Control control)
            {
                node.QueueFree();
                ticket.Dispose();
                throw new InvalidOperationException($"UI 根节点必须是 Control: {scenePath}");
            }

            entry = new PageEntry(scenePath, control, ticket, layer, cache, ++_subOrder);
            _host.AddChild(control);
        }

        _stack.Add(entry);
        if (entry.Root is UiPage page)
            page.OnOpen();
        Restack();
        return entry.Root;
    }

    public void CloseTop()
    {
        if (_stack.Count == 0)
            return;

        Close(_stack[^1]);
    }

    private void Close(PageEntry entry)
    {
        _stack.Remove(entry);
        if (entry.Root is UiPage page)
            page.OnClose();

        if (entry.Cache && !_cache.ContainsKey(entry.Path))
        {
            entry.Root.Visible = false;
            entry.Root.MouseFilter = Control.MouseFilterEnum.Ignore;
            _cache[entry.Path] = entry;
        }
        else
        {
            Destroy(entry);
        }

        Restack();
    }

    private void Destroy(PageEntry entry)
    {
        if (GodotObject.IsInstanceValid(entry.Root))
            entry.Root.QueueFree();
        entry.Ticket.Dispose();
    }

    private void Restack()
    {
        if (_host == null)
            return;

        var ordered = _stack.OrderBy(entry => (int)entry.Layer).ThenBy(entry => entry.SubOrder).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var root = ordered[i].Root;
            root.ZIndex = (int)ordered[i].Layer;
            if (root.GetParent() == _host)
                _host.MoveChild(root, i);
        }
    }

    private static void FullRect(Control control)
    {
        control.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        control.GrowHorizontal = Control.GrowDirection.Both;
        control.GrowVertical = Control.GrowDirection.Both;
    }

    private sealed class PageEntry
    {
        public PageEntry(string path, Control root, AssetTicket<PackedScene> ticket, UiLayer layer, bool cache, int subOrder)
        {
            Path = path;
            Root = root;
            Ticket = ticket;
            Layer = layer;
            Cache = cache;
            SubOrder = subOrder;
        }

        public string Path { get; }
        public Control Root { get; }
        public AssetTicket<PackedScene> Ticket { get; }
        public UiLayer Layer { get; }
        public bool Cache { get; }
        public int SubOrder { get; set; }
    }
}
