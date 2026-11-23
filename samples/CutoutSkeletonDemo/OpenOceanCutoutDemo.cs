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
    private OpenOceanClipSet? _clips;
    private Label? _title;
    private Label? _status;
    private Button[]? _mouthButtons;
    private Button[]? _bodyButtons;
    private Button[]? _finButtons;
    private int _mouth;
    private int _body = 1;
    private int _fin = 2;
    private Species _species = Species.Cleaner;
    private bool _closeup;
    private bool _returningToSwim;

    private enum Species
    {
        Cleaner,
        Swallower,
        Placeholder
    }

    public OpenOceanCutoutDemo(Node host, SlimApp app)
    {
        _host = host;
        _app = app;
    }

    public void Start()
    {
        _rig = AttachRig(CurrentDefinition(), CurrentScale());
        _clips = OpenOceanFish.CreateClipSet(_rig);
        OpenOceanFish.BindClipSet(_rig, _clips);
        _rig.Player.AnimationFinished += OnAnimationFinished;
        BuildUi();
        _rig.Play(OpenOceanFish.SwimClip);
        var probe = AttachRig(OpenOceanFish.Definition(_parts, _mouth, _body, _fin), OpenOceanFish.DisplayScale);
        probe.Root.Visible = false;
        probe.Root.Position = new Vector2(-2000, -2000);
        OpenOceanFish.BindClipSet(probe, _clips);
        probe.Play(OpenOceanFish.SwimClip);
        CutoutSampleSmoke.Run(_app, probe, _parts, _mouth, _body, _fin, _host);
        _app.Get<CutoutSkeletonModule>().Detach(probe);
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
            case Key.C:
                OpenCombat();
                break;
            case Key.V:
                ToggleSpecies();
                break;
            case Key.B:
                ToggleCloseup();
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
        _status.Text = _species switch
        {
            Species.Cleaner => $"{CleanerFish.DisplayName}    {(_closeup ? CleanerFish.CloseupId : CleanerFish.GameId)}    {clip}",
            Species.Swallower => $"{SwallowerFish.DisplayName}    {clip}",
            _ => $"口 {_mouth + 1}    体 {_body + 1}    鳍 {_fin + 1}    {clip}"
        };
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

        _title = new Label { Text = CleanerFish.DisplayName };
        box.AddChild(_title);
        box.AddChild(new Label { Text = "1-3 口     4-6 体     7-9 鳍     V 物种     B 特写     Space 咬合     C 对战" });
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
        var species = new Button
        {
            Text = "物种",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(88, 36)
        };
        species.Pressed += ToggleSpecies;
        box.AddChild(species);
        var closeup = new Button
        {
            Text = "特写",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(88, 36)
        };
        closeup.Pressed += ToggleCloseup;
        box.AddChild(closeup);
        var combat = new Button
        {
            Text = "对战",
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(88, 36)
        };
        combat.Pressed += OpenCombat;
        box.AddChild(combat);
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

    private CutoutRig AttachRig(CutoutRigDefinition definition, float scale)
    {
        var module = _app.Get<CutoutSkeletonModule>();
        var rig = module.Attach(_host, definition, OpenOceanFish.RootName);
        rig.Root.Position = new Vector2(760, 390);
        rig.SetUniformScale(scale);
        foreach (var slot in rig.SlotIds)
            rig.GetSlotNode(slot).TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        return rig;
    }

    private void ToggleSpecies()
    {
        if (_rig == null || _clips == null)
            return;
        _species = _species switch
        {
            Species.Cleaner => Species.Swallower,
            Species.Swallower => Species.Placeholder,
            _ => Species.Cleaner
        };
        SwapRig();
    }

    private void ToggleCloseup()
    {
        if (_rig == null || _clips == null || _species != Species.Cleaner)
            return;
        _closeup = !_closeup;
        SwapRig();
    }

    private void SwapRig()
    {
        if (_rig == null || _clips == null)
            return;
        _rig.Player.AnimationFinished -= OnAnimationFinished;
        var module = _app.Get<CutoutSkeletonModule>();
        module.Detach(_rig);
        _rig.Root.Free();
        _rig = AttachRig(CurrentDefinition(), CurrentScale());
        OpenOceanFish.BindClipSet(_rig, _clips);
        _rig.Player.AnimationFinished += OnAnimationFinished;
        _rig.Play(OpenOceanFish.SwimClip);
        if (_title != null)
            _title.Text = _species switch
            {
                Species.Cleaner => CleanerFish.DisplayName,
                Species.Swallower => SwallowerFish.DisplayName,
                _ => OpenOceanFish.DisplayName
            };
        RefreshButtons();
    }

    private CutoutRigDefinition CurrentDefinition() => _species switch
    {
        Species.Cleaner => CleanerFish.Definition(_closeup ? CleanerFish.Closeup : CleanerFish.Game),
        Species.Swallower => SwallowerFish.Definition(),
        _ => OpenOceanFish.Definition(_parts, _mouth, _body, _fin)
    };

    private float CurrentScale() => _species switch
    {
        Species.Cleaner => CleanerFish.LabScale,
        Species.Swallower => SwallowerFish.DisplayScale,
        _ => OpenOceanFish.DisplayScale
    };

    private void SetMouth(int tier) => SetTier(ref _mouth, tier);

    private void SetBody(int tier) => SetTier(ref _body, tier);

    private void SetFin(int tier) => SetTier(ref _fin, tier);

    private void SetTier(ref int slot, int tier)
    {
        if (_rig == null || _species != Species.Placeholder || tier < 0 || tier >= PlaceholderCutouts.TierCount)
            return;
        slot = tier;
        OpenOceanFish.Apply(_rig, _parts, _mouth, _body, _fin);
        RefreshButtons();
    }

    private void Bite()
    {
        _rig?.Play(OpenOceanFish.BiteClip);
    }

    private void OpenCombat() => _host.GetTree().ChangeSceneToFile(CutoutScenes.Combat);

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

    private void Mark(Button[]? buttons, int selected)
    {
        if (buttons == null)
            return;
        for (var i = 0; i < buttons.Length; i++)
            buttons[i].Disabled = _species != Species.Placeholder || i == selected;
    }
}
