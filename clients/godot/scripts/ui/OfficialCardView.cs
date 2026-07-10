using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class OfficialCardView : PanelContainer
{
    [Signal]
    public delegate void ActivatedEventHandler(Godot.Collections.Dictionary card);

    private TextureRect _cardTexture = null!;
    private ColorRect _fallbackBackground = null!;
    private Label _fallbackLabel = null!;
    private Panel _stateBorder = null!;
    private PanelContainer _countBadge = null!;
    private Label _countLabel = null!;
    private Godot.Collections.Dictionary _card = new();
    private OfficialCardVisualState _state = OfficialCardVisualState.Disabled;
    private bool _hasPendingDisplay;

    public bool PreserveOfficialAspect => true;

    public override void _Ready()
    {
        _cardTexture = GetNode<TextureRect>("%CardTexture");
        _fallbackBackground = GetNode<ColorRect>("%FallbackBackground");
        _fallbackLabel = GetNode<Label>("%FallbackLabel");
        _stateBorder = GetNode<Panel>("%StateBorder");
        _countBadge = GetNode<PanelContainer>("%CountBadge");
        _countLabel = GetNode<Label>("%CountLabel");

        GuiInput += OnGuiInput;
        _cardTexture.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        MinimalTheme.Apply(this);
        AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.AppBackground));
        _countBadge.AddThemeStyleboxOverride("panel", CountBadgeStyle());
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
        var canRevealIdentity = visible && !faceDown && _state != OfficialCardVisualState.Hidden;
        var texture = canRevealIdentity
            ? LoadTexture(ReadString(_card, "imagePath"))
            : null;

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
        _countBadge.Visible = count > 1;
        _countLabel.Text = count.ToString();
        _stateBorder.AddThemeStyleboxOverride("panel", MinimalTheme.Outline(_state));
        Modulate = _state == OfficialCardVisualState.Disabled
            ? new Color(0.66f, 0.68f, 0.72f, 0.72f)
            : Colors.White;
        FocusMode = IsInteractive(_state) ? FocusModeEnum.All : FocusModeEnum.None;
        MouseFilter = IsInteractive(_state) ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        TooltipText = canRevealIdentity
            ? ReadString(_card, "previewSummary", _fallbackLabel.Text)
            : "隐藏卡牌";
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
        _stateBorder.AddThemeStyleboxOverride("panel", MinimalTheme.Outline(_state));
        TooltipText = string.Empty;
        FocusMode = FocusModeEnum.None;
        MouseFilter = MouseFilterEnum.Ignore;
        Modulate = new Color(0.66f, 0.68f, 0.72f, 0.72f);
    }

    private void OnGuiInput(InputEvent input)
    {
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

    private static Texture2D? LoadTexture(string path)
    {
        return CardControlRenderer.LoadTextureFromImagePath(path);
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
