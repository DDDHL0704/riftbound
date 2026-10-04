using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class OfficialCardView : PanelContainer
{
    [Signal]
    public delegate void ActivatedEventHandler(Godot.Collections.Dictionary card);

    public event System.Action<Godot.Collections.Dictionary>? PreviewRequested;
    public event System.Action<Godot.Collections.Dictionary>? InspectionRequested;

    private TextureRect _cardTexture = null!;
    private ColorRect _fallbackBackground = null!;
    private Label _fallbackLabel = null!;
    private Panel _stateBorder = null!;
    private PanelContainer _countBadge = null!;
    private Label _countLabel = null!;
    private Label _exhaustedLabel = null!;
    private Label _damageLabel = null!;
    private Godot.Collections.Dictionary _card = new();
    private OfficialCardVisualState _state = OfficialCardVisualState.Disabled;
    private bool _hasPendingDisplay;

    public bool PreserveOfficialAspect => true;

    public bool TryGetVisibleCard(out Godot.Collections.Dictionary card)
    {
        var visible = ReadBool(_card, "visible", true);
        var faceDown = ReadBool(_card, "faceDown", false);
        if (_card.Count == 0 || !visible || faceDown || _state == OfficialCardVisualState.Hidden)
        {
            card = new Godot.Collections.Dictionary();
            return false;
        }

        card = _card.Duplicate(true);
        return true;
    }

    public override void _Ready()
    {
        _cardTexture = GetNode<TextureRect>("%CardTexture");
        _fallbackBackground = GetNode<ColorRect>("%FallbackBackground");
        _fallbackLabel = GetNode<Label>("%FallbackLabel");
        _stateBorder = GetNode<Panel>("%StateBorder");
        _countBadge = GetNode<PanelContainer>("%CountBadge");
        _countLabel = GetNode<Label>("%CountLabel");
        _exhaustedLabel = GetNode<Label>("%ExhaustedLabel");
        _damageLabel = GetNode<Label>("%DamageLabel");

        GuiInput += OnGuiInput;
        MouseEntered += Preview;
        FocusEntered += Preview;
        _cardTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _cardTexture.TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        MinimalTheme.Apply(this);
        var cardFrame = MinimalTheme.Panel(MinimalTheme.AppBackground);
        cardFrame.SetContentMarginAll(4);
        AddThemeStyleboxOverride("panel", cardFrame);
        _countBadge.AddThemeStyleboxOverride("panel", CountBadgeStyle());
        _exhaustedLabel.AddThemeStyleboxOverride("normal", CountBadgeStyle());
        _exhaustedLabel.AddThemeFontSizeOverride("font_size", 11);
        _exhaustedLabel.AddThemeColorOverride("font_color", MinimalTheme.Selected);
        _damageLabel.AddThemeStyleboxOverride("normal", CountBadgeStyle());
        _damageLabel.AddThemeFontSizeOverride("font_size", 11);
        _damageLabel.SelfModulate = new Color("ff9c99");
        if (_hasPendingDisplay)
        {
            ApplyDisplay();
        }
        else
        {
            Clear();
        }
    }

    public void Display(
        Godot.Collections.Dictionary card,
        OfficialCardVisualState state)
    {
        _card = card.Duplicate();
        _state = state;
        _hasPendingDisplay = true;
        if (!IsNodeReady())
        {
            return;
        }

        ApplyDisplay();
    }

    private void ApplyDisplay()
    {
        _hasPendingDisplay = false;

        var visible = ReadBool(_card, "visible", true);
        var faceDown = ReadBool(_card, "faceDown", false);
        var rotated = ReadBool(_card, "rotated", false);
        var canRevealIdentity = visible && !faceDown && _state != OfficialCardVisualState.Hidden;
        var texture = canRevealIdentity
            ? LoadTexture(ReadString(_card, "imagePath"), rotated)
            : GD.Load<Texture2D>("res://assets/card-back.svg");

        _cardTexture.Texture = texture;
        _cardTexture.Visible = texture is not null;
        _fallbackBackground.Visible = texture is null;
        _fallbackLabel.Visible = texture is null;
        _fallbackLabel.Text = canRevealIdentity
            ? ReadString(_card, "cardName", ReadString(_card, "cardNo", "CARD"))
            : "RIFTBOUND\nCARD BACK";
        _fallbackLabel.AddThemeColorOverride("font_color", canRevealIdentity
            ? MinimalTheme.Text
            : MinimalTheme.TextSecondary);

        var count = ReadInt(_card, "count", 1);
        var hasCurrentPower = canRevealIdentity && _card.ContainsKey("currentPower");
        var currentPower = ReadInt(_card, "currentPower", 0);
        var printedPower = ReadInt(_card, "power", currentPower);
        _countBadge.Visible = count > 1 || hasCurrentPower;
        _countLabel.Text = (hasCurrentPower ? currentPower : count).ToString();
        _countLabel.SelfModulate = !hasCurrentPower || currentPower == printedPower ? Colors.White
            : currentPower > printedPower ? MinimalTheme.Selectable : new Color("ff9c99");
        _countBadge.TooltipText = hasCurrentPower ? "当前战力" : "卡牌数量";
        var damage = ReadInt(_card, "damage", 0);
        _damageLabel.Visible = hasCurrentPower && damage > 0;
        _damageLabel.Text = $"伤 {damage}";
        var isStandby = ReadBool(_card, "isStandby", false);
        _exhaustedLabel.Visible = canRevealIdentity && (ReadBool(_card, "isExhausted", false) || isStandby);
        _exhaustedLabel.Text = isStandby ? "待命" : "休眠";
        _stateBorder.AddThemeStyleboxOverride("panel", MinimalTheme.Outline(_state));
        Modulate = _state == OfficialCardVisualState.Disabled
            ? new Color(0.66f, 0.68f, 0.72f, 0.72f)
            : Colors.White;
        FocusMode = IsInteractive(_state) ? FocusModeEnum.All : FocusModeEnum.None;
        MouseFilter = IsInteractive(_state) ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        TooltipText = canRevealIdentity
            ? ReadString(_card, "previewSummary", _fallbackLabel.Text)
            : "隐藏卡牌";
        if (_exhaustedLabel.Visible) TooltipText += $"\n当前状态：{_exhaustedLabel.Text}";
    }

    public void Clear()
    {
        _card = new Godot.Collections.Dictionary();
        _state = OfficialCardVisualState.Disabled;
        _hasPendingDisplay = false;
        if (!IsNodeReady())
        {
            return;
        }

        _cardTexture.Texture = null;
        _cardTexture.Visible = false;
        _fallbackBackground.Visible = true;
        _fallbackLabel.Visible = true;
        _fallbackLabel.Text = "CARD";
        _countBadge.Visible = false;
        _countLabel.Text = string.Empty;
        _exhaustedLabel.Visible = false;
        _damageLabel.Visible = false;
        _stateBorder.AddThemeStyleboxOverride("panel", MinimalTheme.Outline(_state));
        TooltipText = string.Empty;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Ignore;
        Modulate = new Color(0.66f, 0.68f, 0.72f, 0.72f);
    }

    private void Preview()
    {
        if (TryGetVisibleCard(out var card)) PreviewRequested?.Invoke(card);
    }

    private void OnGuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true }
            && TryGetVisibleCard(out var inspect))
        {
            AcceptEvent(); InspectionRequested?.Invoke(inspect); return;
        }
        var mouseActivated = input is InputEventMouseButton
        {
            ButtonIndex: MouseButton.Left,
            Pressed: true
        };
        var keyboardActivated = input.IsActionPressed("ui_accept");
        if (!mouseActivated && !keyboardActivated)
        {
            return;
        }

        AcceptEvent();
        Activate();
    }

    public void Activate()
    {
        if (IsInteractive(_state) && _card.Count > 0)
        {
            EmitSignal(SignalName.Activated, _card);
        }
    }

    private static bool IsInteractive(OfficialCardVisualState state)
    {
        return state is OfficialCardVisualState.Normal
            or OfficialCardVisualState.Selectable
            or OfficialCardVisualState.Selected
            or OfficialCardVisualState.LegalTarget
            or OfficialCardVisualState.HostileTarget;
    }

    private static Texture2D? LoadTexture(string path, bool rotated)
    {
        return CardTextureLoader.Load(path, rotated);
    }

    private static string ReadString(
        Godot.Collections.Dictionary dictionary,
        string key,
        string fallback = "")
    {
        return dictionary.TryGetValue(key, out var value)
            ? value.AsString()
            : fallback;
    }

    private static bool ReadBool(
        Godot.Collections.Dictionary dictionary,
        string key,
        bool fallback)
    {
        return dictionary.TryGetValue(key, out var value)
            ? value.AsBool()
            : fallback;
    }

    private static int ReadInt(
        Godot.Collections.Dictionary dictionary,
        string key,
        int fallback)
    {
        return dictionary.TryGetValue(key, out var value)
            ? value.AsInt32()
            : fallback;
    }

    private static StyleBoxFlat CountBadgeStyle()
    {
        var style = MinimalTheme.Panel(new Color(0.07f, 0.08f, 0.1f, 0.96f));
        style.BorderColor = MinimalTheme.TextSecondary;
        style.SetContentMargin(Side.Left, 7);
        style.SetContentMargin(Side.Right, 7);
        style.SetContentMargin(Side.Top, 3);
        style.SetContentMargin(Side.Bottom, 3);
        return style;
    }
}
