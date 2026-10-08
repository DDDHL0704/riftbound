using System;
using System.Linq;
using Godot;

namespace Riftbound.GodotClient.Ui;

// Stable hand order is independent of the raised card's draw/input order.
public partial class FanHandContainer : Container
{
    private Control? _raised;
    public override Vector2 _GetMinimumSize()
    {
        var cards = GetChildren().OfType<OfficialCardView>().ToArray();
        return cards.Length == 0 ? new Vector2(180, 28)
            : new Vector2(Math.Max(180, 44 * (cards.Length - 1) + cards[0].CustomMinimumSize.X + 16), cards.Max(c => c.CustomMinimumSize.Y) + 40);
    }
    public void Register(OfficialCardView card, int index)
    {
        card.SetMeta("handIndex", index);
        card.MouseEntered += () => Raise(card);
        card.FocusEntered += () => Raise(card);
        card.MouseExited += () => { if (_raised == card && !card.HasFocus()) Raise(null); };
        card.FocusExited += () => { if (_raised == card) Raise(null); };
        UpdateMinimumSize(); QueueSort();
    }
    private void Raise(Control? card)
    {
        _raised = card;
        if (card is not null) MoveChild(card, GetChildCount() - 1);
        QueueSort();
    }
    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren) return;
        var cards = GetChildren().OfType<Control>().Where(c => c.Visible)
            .OrderBy(c => c.HasMeta("handIndex") ? c.GetMeta("handIndex").AsInt32() : 0).ToArray();
        if (cards.Length == 0) return;
        var cardSize = cards[0].GetCombinedMinimumSize();
        var step = cards.Length <= 1 ? 0 : Math.Min(cardSize.X * .82f, (Size.X - cardSize.X - 16) / (cards.Length - 1));
        step = Math.Max(44, step);
        var left = (Size.X - cardSize.X - step * (cards.Length - 1)) / 2;
        for (var i = 0; i < cards.Length; i++)
        {
            var card = cards[i]; var raised = card == _raised;
            var spread = cards.Length <= 1 ? 0 : (i - (cards.Length - 1) / 2f) / Math.Max(1, (cards.Length - 1) / 2f);
            FitChildInRect(card, new Rect2(left + i * step, raised ? 14 : 20 + Math.Abs(spread) * 6, cardSize.X, cardSize.Y));
            card.PivotOffset = cardSize / 2;
            card.RotationDegrees = raised ? 0 : spread * 3;
            card.Scale = raised ? new Vector2(1.1f, 1.1f) : Vector2.One;
            card.ZIndex = raised ? 20 : 0;
        }
    }
}
