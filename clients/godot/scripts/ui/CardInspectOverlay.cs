using System.Collections.Generic;
using Godot;

namespace Riftbound.GodotClient.Ui;

public partial class CardInspectOverlay : Control
{
    private OfficialCardView _cardView = null!;
    private RichTextLabel _summary = null!;
    private Button _closeButton = null!;
    private Control? _focusReturn;
    private readonly List<Godot.Collections.Dictionary> _pileCards = [];
    private HBoxContainer _pileNavigation = null!;
    private OptionButton _pileChooser = null!;
    private Button _previous = null!;
    private Button _next = null!;
    private int _pileIndex;

    public override void _Ready()
    {
        _cardView = GetNode<OfficialCardView>("%InspectCard");
        _summary = GetNode<RichTextLabel>("%InspectSummary");
        _closeButton = GetNode<Button>("%CloseButton");
        _closeButton.Pressed += HideCard;
        _pileNavigation = new HBoxContainer { Visible = false };
        _previous = new Button { Text = "←", TooltipText = "上一张（左方向键）", CustomMinimumSize = new Vector2(44, 40) };
        _next = new Button { Text = "→", TooltipText = "下一张（右方向键）", CustomMinimumSize = new Vector2(44, 40) };
        _pileChooser = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, FitToLongestItem = false };
        _pileNavigation.AddChild(_previous);
        _pileNavigation.AddChild(_pileChooser);
        _pileNavigation.AddChild(_next);
        _summary.GetParent().GetParent().AddChild(_pileNavigation);
        _previous.Pressed += () => DisplayPileCard(_pileIndex - 1);
        _next.Pressed += () => DisplayPileCard(_pileIndex + 1);
        _pileChooser.ItemSelected += index => DisplayPileCard((int)index);

        ApplyTheme();
        HideCard();
    }

    public override void _Input(InputEvent input)
    {
        if (!Visible || _pileCards.Count == 0 || _pileChooser.GetPopup().Visible) return;
        if (input.IsActionPressed("ui_left") || input.IsActionPressed("ui_right"))
        {
            DisplayPileCard(_pileIndex + (input.IsActionPressed("ui_left") ? -1 : 1));
            GetViewport().SetInputAsHandled();
        }
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
        _summary?.AddThemeColorOverride("default_color", MinimalTheme.Text);
        _summary?.AddThemeFontSizeOverride("normal_font_size", 18);
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
        _pileChooser.GetPopup().Hide();
        _pileCards.Clear();
        _pileNavigation.Visible = false;
        GetNode<Label>("%InspectTitle").Text = "卡牌详情";
        _summary.Text = ReadString(card, "previewSummary", "可见卡牌");
        Visible = true;
        MoveToFront();
        _closeButton.GrabFocus();
    }

    public void ShowPile(string title, Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        if (!IsNodeReady()) return;
        var visibleCards = new List<Godot.Collections.Dictionary>();
        for (var index = cards.Count - 1; index >= 0; index--)
        {
            var card = cards[index];
            if (ReadBool(card, "visible", false) && !ReadBool(card, "faceDown", true))
                visibleCards.Add(card.Duplicate(true));
        }
        if (visibleCards.Count == 0) return;
        ShowCard(visibleCards[0]);
        _pileCards.AddRange(visibleCards);
        GetNode<Label>("%InspectTitle").Text = title;
        _pileChooser.Clear();
        for (var index = 0; index < _pileCards.Count; index++)
            _pileChooser.AddItem($"{index + 1} / {_pileCards.Count} · {ReadString(_pileCards[index], "cardName", "卡牌")}");
        _pileNavigation.Visible = true;
        DisplayPileCard(0);
    }

    public void HidePile()
    {
        if (_pileCards.Count > 0) HideCard();
    }

    private void DisplayPileCard(int index)
    {
        if (index < 0 || index >= _pileCards.Count) return;
        _pileIndex = index;
        _pileChooser.Select(index);
        _previous.Disabled = index == 0;
        _next.Disabled = index == _pileCards.Count - 1;
        _cardView.Display(_pileCards[index], OfficialCardVisualState.Normal);
        _summary.Text = ReadString(_pileCards[index], "previewSummary", "可见卡牌");
        ((ScrollContainer)_summary.GetParent()).ScrollVertical = 0;
    }

    public void HideCard()
    {
        if (IsNodeReady())
        {
            _cardView.Clear();
            _summary.Text = string.Empty;
            _pileCards.Clear();
            _pileNavigation.Visible = false;
            _pileChooser.GetPopup().Hide();
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
