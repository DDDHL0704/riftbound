using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class CardInspectOverlay : Control
{
    private OfficialCardView _cardView = null!;
    private Label _summary = null!;
    private Button _closeButton = null!;
    private Control? _focusReturn;

    public override void _Ready()
    {
        _cardView = GetNode<OfficialCardView>("%InspectCard");
        _summary = GetNode<Label>("%InspectSummary");
        _closeButton = GetNode<Button>("%CloseButton");
        _closeButton.Pressed += HideCard;

        ApplyTheme();
        HideCard();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!Visible || !input.IsActionPressed("ui_cancel"))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        HideCard();
    }

    public void ApplyTheme()
    {
        MinimalTheme.Apply(this);
        GetNode<PanelContainer>("%InspectPanel")
            .AddThemeStyleboxOverride("panel", MinimalTheme.Panel(MinimalTheme.SurfaceRaised));
        GetNode<Label>("%InspectTitle").AddThemeFontSizeOverride("font_size", 24);
        GetNode<Label>("%InspectTitle").AddThemeColorOverride("font_color", MinimalTheme.Text);
        _summary?.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
    }

    public void ShowCard(Godot.Collections.Dictionary card)
    {
        if (!ReadBool(card, "visible", false) || ReadBool(card, "faceDown", true))
        {
            return;
        }

        if (!IsNodeReady())
        {
            return;
        }

        if (!Visible)
        {
            _focusReturn = GetViewport().GuiGetFocusOwner();
        }

        _cardView.Display(card.Duplicate(true), OfficialCardVisualState.Normal);
        _summary.Text = ReadString(card, "previewSummary", "可见卡牌");
        Visible = true;
        MoveToFront();
        _closeButton.GrabFocus();
    }

    public void HideCard()
    {
        if (IsNodeReady())
        {
            _cardView.Clear();
            _summary.Text = string.Empty;
        }

        Visible = false;
        var focusReturn = _focusReturn;
        _focusReturn = null;
        if (focusReturn is not null
            && GodotObject.IsInstanceValid(focusReturn)
            && focusReturn.IsInsideTree()
            && focusReturn.IsVisibleInTree()
            && focusReturn.FocusMode != FocusModeEnum.None)
        {
            focusReturn.GrabFocus();
        }
    }

    private static string ReadString(
        Godot.Collections.Dictionary dictionary,
        string key,
        string fallback)
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
}
