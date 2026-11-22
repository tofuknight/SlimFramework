using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 三十条鱼的观看沙盘。运动是运动学转向，片段全场共用一份。
/// 生命、目标和出手只留在这个样例里。
/// </summary>
internal sealed class FishArena
{
    public const int FishCount = 30;

    private readonly Node _host;
    private readonly SlimApp _app;
    private readonly OpenOceanParts _parts = new();
    private readonly List<CombatFish> _fish = new(FishCount);
    private readonly Random _random;
    private OpenOceanClipSet? _clips;
    private Label? _status;
    private Button? _pauseButton;
    private bool _fastForward;
    private bool _reported;

    public FishArena(Node host, SlimApp app)
    {
        _host = host;
        _app = app;
        _random = new Random(CutoutCombatSmoke.ShouldFastForward() ? 20261122 : System.Environment.TickCount);
    }

    public Rect2 Bounds { get; } = new(56f, 48f, 1168f, 624f);

    public SlimApp App => _app;

    public OpenOceanParts Parts => _parts;

    public IReadOnlyList<CombatFish> Fish => _fish;

    public OpenOceanClipSet Clips => _clips ?? throw new InvalidOperationException("片段还没建好。");

    public int BitePlays { get; private set; }

    public int LungePlays { get; private set; }

    public int TailPlays { get; private set; }

    public int VerifiedBite { get; private set; }

    public int VerifiedLunge { get; private set; }

    public int VerifiedTail { get; private set; }

    public int LivingCount
    {
        get
        {
            var count = 0;
            foreach (var fish in _fish)
            {
                if (fish.Alive)
                    count++;
            }

            return count;
        }
    }

    public void Start()
    {
        try
        {
            BuildTank();
            BuildUi();
            Spawn();
            if (CutoutCombatSmoke.ShouldFastForward())
            {
                _fastForward = true;
                CutoutCombatSmoke.Run(this, _host);
            }
            else
            {
                CutoutCombatSmoke.AssertSpawned(this);
                CutoutCombatSmoke.AssertShared(this);
            }

            RefreshStatus();
        }
        catch (Exception ex)
        {
            GD.PushError(ex.ToString());
            _host.GetTree()?.Quit(1);
        }
    }

    public void OnProcess(double delta)
    {
        if (_fastForward || _host.GetTree().Paused)
            return;
        Step(Math.Min(delta, 0.05d));
        RefreshStatus();
        if (_reported || !CutoutCombatSmoke.AttacksSatisfied(this))
            return;
        _reported = true;
        CutoutCombatSmoke.Finish(this, _host);
    }

    public void OnInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.P:
                TogglePause();
                break;
            case Key.R:
                Restart();
                break;
            case Key.M:
                GoToSlotSwap();
                break;
            default:
                return;
        }

        _host.GetViewport()?.SetInputAsHandled();
    }

    public void Step(double dt)
    {
        for (var i = 0; i < _fish.Count; i++)
            _fish[i].Step(dt, this);
        NoteAttacks();
    }

    public void NotifyAttack(string clip)
    {
        switch (clip)
        {
            case OpenOceanFish.BiteClip:
                BitePlays++;
                break;
            case OpenOceanFish.LungeClip:
                LungePlays++;
                break;
            case OpenOceanFish.TailSwipeClip:
                TailPlays++;
                break;
        }
    }

    public Vector2 RandomPoint()
    {
        var min = Bounds.Position + new Vector2(70f, 56f);
        var size = Bounds.Size - new Vector2(140f, 112f);
        return new Vector2(
            min.X + (float)_random.NextDouble() * size.X,
            min.Y + (float)_random.NextDouble() * size.Y);
    }

    public int NextInt(int exclusiveMax) => _random.Next(exclusiveMax);

    public double NextDouble() => _random.NextDouble();

    public string RandomAttack() => OpenOceanFish.AttackClips[_random.Next(OpenOceanFish.AttackClips.Length)];

    public Vector2 Clamp(Vector2 point)
    {
        var min = Bounds.Position + new Vector2(36f, 28f);
        var max = Bounds.End - new Vector2(36f, 28f);
        return new Vector2(Mathf.Clamp(point.X, min.X, max.X), Mathf.Clamp(point.Y, min.Y, max.Y));
    }

    public Vector2 Separation(CombatFish self)
    {
        var push = Vector2.Zero;
        const float range = 48f;
        foreach (var other in _fish)
        {
            if (!other.Alive || ReferenceEquals(other, self))
                continue;
            var delta = self.Position - other.Position;
            var distSq = delta.LengthSquared();
            if (distSq < 1f || distSq > range * range)
                continue;
            var dist = Mathf.Sqrt(distSq);
            push += delta / dist * (range - dist);
        }

        return push * 4.5f;
    }

    public Vector2 WallPush(Vector2 point)
    {
        var push = Vector2.Zero;
        const float edge = 72f;
        var min = Bounds.Position;
        var max = Bounds.End;
        if (point.X < min.X + edge)
            push.X += (min.X + edge - point.X) / edge;
        else if (point.X > max.X - edge)
            push.X -= (point.X - (max.X - edge)) / edge;
        if (point.Y < min.Y + edge)
            push.Y += (min.Y + edge - point.Y) / edge;
        else if (point.Y > max.Y - edge)
            push.Y -= (point.Y - (max.Y - edge)) / edge;
        return push * 220f;
    }

    private void BuildTank()
    {
        var tank = new Node2D
        {
            Name = "Tank",
            ZIndex = -10
        };
        _host.AddChild(tank);
        var points = RectPoints(Bounds);
        tank.AddChild(new Polygon2D
        {
            Color = new Color(0.012f, 0.055f, 0.09f),
            Polygon = points
        });
        tank.AddChild(new Line2D
        {
            Width = 4f,
            DefaultColor = new Color(0.45f, 0.78f, 0.84f),
            Closed = true,
            Points = points
        });
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer { Layer = 10 };
        _host.AddChild(layer);
        var box = new VBoxContainer { Position = new Vector2(20, 14) };
        box.AddThemeConstantOverride("separation", 6);
        layer.AddChild(box);
        var title = new Label { Text = "远洋流线型 · 对战" };
        title.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(title);
        box.AddChild(new Label { Text = "观看    P 暂停    R 重开    M 换装" });

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        box.AddChild(row);
        _pauseButton = MakeButton("暂停");
        _pauseButton.Pressed += TogglePause;
        row.AddChild(_pauseButton);
        var restart = MakeButton("重开");
        restart.Pressed += Restart;
        row.AddChild(restart);
        var back = MakeButton("换装");
        back.Pressed += GoToSlotSwap;
        row.AddChild(back);

        _status = new Label();
        _status.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(_status);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        FocusMode = Control.FocusModeEnum.None,
        CustomMinimumSize = new Vector2(88, 36)
    };

    private void Spawn()
    {
        ClearFish();
        for (var i = 0; i < FishCount; i++)
            _fish.Add(CreateFish(i));

        _clips = OpenOceanFish.CreateClipSet(_fish[0].Rig);
        foreach (var fish in _fish)
        {
            OpenOceanFish.BindClipSet(fish.Rig, _clips);
            fish.Boot(this);
        }
    }

    private CombatFish CreateFish(int index)
    {
        var mouth = _random.Next(PlaceholderCutouts.TierCount);
        var body = _random.Next(PlaceholderCutouts.TierCount);
        var fin = _random.Next(PlaceholderCutouts.TierCount);
        var frequency = 1.05f + (float)_random.NextDouble() * 0.5f;
        var module = _app.Get<CutoutSkeletonModule>();
        var rig = module.Attach(
            _host,
            OpenOceanFish.Definition(_parts, mouth, body, fin, frequency),
            $"Fish{index:00}");
        rig.Root.ProcessMode = Node.ProcessModeEnum.Pausable;
        rig.Root.ZIndex = 1;
        rig.Root.Position = SpawnPoint(index);
        rig.Root.Rotation = (float)(_random.NextDouble() * Mathf.Tau);
        rig.SetUniformScale(0.56f + (float)_random.NextDouble() * 0.3f);
        rig.Root.Modulate = Colors.White;
        foreach (var slot in rig.SlotIds)
            rig.GetSlot(slot).TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        var speed = 112f + (float)_random.NextDouble() * 56f;
        var preferred = OpenOceanFish.AttackClips[index % OpenOceanFish.AttackClips.Length];
        return new CombatFish(rig, body, preferred, speed);
    }

    private Vector2 SpawnPoint(int index)
    {
        const int columns = 6;
        var column = index % columns;
        var row = index / columns;
        var x = Bounds.Position.X + 140f + column * 170f;
        var y = Bounds.Position.Y + 96f + row * 112f;
        x += (float)(_random.NextDouble() * 32.0 - 16.0);
        y += (float)(_random.NextDouble() * 24.0 - 12.0);
        return Clamp(new Vector2(x, y));
    }

    private void ClearFish()
    {
        var module = _app.Get<CutoutSkeletonModule>();
        foreach (var fish in _fish)
        {
            module.Detach(fish.Rig);
            if (GodotObject.IsInstanceValid(fish.Rig.Root))
                fish.Rig.Root.QueueFree();
        }

        _fish.Clear();
        BitePlays = 0;
        LungePlays = 0;
        TailPlays = 0;
        VerifiedBite = 0;
        VerifiedLunge = 0;
        VerifiedTail = 0;
    }

    private void NoteAttacks()
    {
        if (_clips == null)
            return;
        foreach (var fish in _fish)
        {
            if (!fish.DealsDamage || fish.ActiveClip == null)
                continue;
            var clip = fish.ActiveClip;
            if (fish.Rig.CurrentClip != clip || fish.Rig.GetClip(clip) != _clips.Get(clip))
                continue;
            switch (clip)
            {
                case OpenOceanFish.BiteClip:
                    VerifiedBite++;
                    break;
                case OpenOceanFish.LungeClip:
                    VerifiedLunge++;
                    break;
                case OpenOceanFish.TailSwipeClip:
                    VerifiedTail++;
                    break;
            }
        }
    }

    private void TogglePause()
    {
        var tree = _host.GetTree();
        tree.Paused = !tree.Paused;
        RefreshStatus();
    }

    private void Restart()
    {
        var tree = _host.GetTree();
        if (tree.Paused)
            tree.Paused = false;
        Spawn();
        RefreshStatus();
    }

    private void GoToSlotSwap()
    {
        var tree = _host.GetTree();
        tree.Paused = false;
        tree.ChangeSceneToFile(CutoutScenes.SlotSwap);
    }

    private void RefreshStatus()
    {
        if (_status == null)
            return;
        var paused = _host.GetTree().Paused;
        var tail = paused
            ? "    暂停"
            : LivingCount switch
            {
                0 => "    同归于尽",
                1 => "    只剩一条",
                _ => ""
            };
        _status.Text = $"剩余 {LivingCount} / {FishCount}    咬合 {BitePlays}    冲刺 {LungePlays}    甩尾 {TailPlays}{tail}";
        if (_pauseButton != null)
            _pauseButton.Text = paused ? "继续" : "暂停";
    }

    private static Vector2[] RectPoints(Rect2 rect)
    {
        var min = rect.Position;
        var max = rect.End;
        return [new(min.X, min.Y), new(max.X, min.Y), new(max.X, max.Y), new(min.X, max.Y)];
    }
}
