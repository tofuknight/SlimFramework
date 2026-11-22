using Godot;
using SlimFramework;
using SlimFramework.CutoutSkeleton;

namespace SlimFramework.CutoutSkeletonDemo;

/// <summary>
/// 样例里的操作：三档口、体、鳍，以及共用的游动 / 咬合。
/// 换档只换贴图，片段不重建。
/// </summary>
internal sealed class OpenOceanCutoutDemo
{
    private readonly Node _host;
    private readonly SlimApp _app;
    private readonly OpenOceanParts _parts = new();
    private CutoutRig? _rig;
    private Label? _status;
    private Button[]? _mouthButtons;
    private Button[]? _bodyButtons;
    private Button[]? _finButtons;
    private int _mouth;
    private int _body = 1;
    private int _fin = 2;
    private bool _returningToSwim;

    public OpenOceanCutoutDemo(Node host, SlimApp app)
    {
        _host = host;
        _app = app;
    }

    public void Start()
    {
        var module = _app.Get<CutoutSkeletonModule>();
        _rig = module.Attach(_host, OpenOceanFish.Definition(_parts, _mouth, _body, _fin), OpenOceanFish.RootName);
        _rig.Root.Position = new Vector2(760, 390);
        _rig.SetUniformScale(OpenOceanFish.DisplayScale);
        foreach (var slot in _rig.SlotIds)
            _rig.GetSlot(slot).TextureFilter = CanvasItem.TextureFilterEnum.Nearest;

        OpenOceanFish.AddSharedClips(_rig);
        _rig.Player.AnimationFinished += OnAnimationFinished;
        BuildUi();
        _rig.Play(OpenOceanFish.SwimClip);
        CutoutSampleSmoke.Run(_app, _rig, _parts, _mouth, _body, _fin, _host);
        RefreshButtons();
    }

    public void OnInput(InputEvent @event)
    {
        if (_rig == null || @event is not InputEventKey { Pressed: true, Echo: false } key)
            return;

        switch (key.Keycode)
        {
            case Key.Key1:
                SetMouth(0);
                break;
            case Key.Key2:
                SetMouth(1);
                break;
            case Key.Key3:
                SetMouth(2);
                break;
            case Key.Key4:
                SetBody(0);
                break;
            case Key.Key5:
                SetBody(1);
                break;
            case Key.Key6:
                SetBody(2);
                break;
            case Key.Key7:
                SetFin(0);
                break;
            case Key.Key8:
                SetFin(1);
                break;
            case Key.Key9:
                SetFin(2);
                break;
            case Key.Space:
                Bite();
                break;
            default:
                return;
        }

        _host.GetViewport()?.SetInputAsHandled();
    }

    public void OnProcess()
    {
        if (_rig == null || _status == null)
            return;
        var clip = _rig.CurrentClip switch
        {
            OpenOceanFish.SwimClip => "游动",
            OpenOceanFish.BiteClip => "咬合",
            _ => _rig.CurrentClip
        };
        _status.Text = $"口 {_mouth + 1}    体 {_body + 1}    鳍 {_fin + 1}    {clip}";
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        _host.AddChild(layer);
        var box = new VBoxContainer
        {
            Position = new Vector2(16, 12)
        };
        box.AddThemeConstantOverride("separation", 6);
        layer.AddChild(box);

        box.AddChild(new Label { Text = OpenOceanFish.DisplayName });
        box.AddChild(new Label { Text = "1-3 口     4-6 体     7-9 鳍     Space 咬合" });
        _mouthButtons = AddTierRow(box, "口");
        _bodyButtons = AddTierRow(box, "体");
        _finButtons = AddTierRow(box, "鳍");
        for (var i = 0; i < PlaceholderCutouts.TierCount; i++)
        {
            var tier = i;
            _mouthButtons[i].Pressed += () => SetMouth(tier);
            _bodyButtons[i].Pressed += () => SetBody(tier);
            _finButtons[i].Pressed += () => SetFin(tier);
        }

        var bite = new Button
        {
            Text = "咬合",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(88, 36)
        };
        bite.Pressed += Bite;
        box.AddChild(bite);
        _status = new Label();
        box.AddChild(_status);
    }

    private static Button[] AddTierRow(VBoxContainer box, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        box.AddChild(row);
        var buttons = new Button[PlaceholderCutouts.TierCount];
        for (var i = 0; i < buttons.Length; i++)
        {
            buttons[i] = new Button
            {
                Text = $"{label} {i + 1}",
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(88, 36)
            };
            row.AddChild(buttons[i]);
        }

        return buttons;
    }

    private void SetMouth(int tier) => SetTier(ref _mouth, tier);

    private void SetBody(int tier) => SetTier(ref _body, tier);

    private void SetFin(int tier) => SetTier(ref _fin, tier);

    private void SetTier(ref int slot, int tier)
    {
        if (_rig == null || tier < 0 || tier >= PlaceholderCutouts.TierCount)
            return;
        slot = tier;
        OpenOceanFish.Apply(_rig, _parts, _mouth, _body, _fin);
        RefreshButtons();
    }

    private void Bite()
    {
        _rig?.Play(OpenOceanFish.BiteClip);
    }

    private void OnAnimationFinished(StringName name)
    {
        if (_returningToSwim || _rig == null || name.ToString() != OpenOceanFish.BiteClip)
            return;
        _returningToSwim = true;
        _rig.Play(OpenOceanFish.SwimClip);
        _returningToSwim = false;
    }

    private void RefreshButtons()
    {
        Mark(_mouthButtons, _mouth);
        Mark(_bodyButtons, _body);
        Mark(_finButtons, _fin);
    }

    private static void Mark(Button[]? buttons, int selected)
    {
        if (buttons == null)
            return;
        for (var i = 0; i < buttons.Length; i++)
            buttons[i].Disabled = i == selected;
    }
}
