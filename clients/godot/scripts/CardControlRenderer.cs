using System;
using Godot;

namespace Riftbound.GodotClient;

internal sealed class CardControlRenderer
{
    private static readonly Vector2 HandCardFrameSize = new(92, 128);
    private static readonly Vector2 HandCardContentSize = new(84, 120);
    private static readonly Vector2 ZoneCardFrameSize = new(64, 90);
    private static readonly Vector2 ZoneCardContentSize = new(58, 82);
    private readonly Action<Godot.Collections.Dictionary> _cardInspected;

    public CardControlRenderer(Action<Godot.Collections.Dictionary> cardInspected)
    {
        _cardInspected = cardInspected;
    }

    public void RenderHandCards(
        HBoxContainer handRow,
        Godot.Collections.Array<Godot.Collections.Dictionary> cards)
    {
        ClearChildren(handRow);
        foreach (var card in cards)
        {
            handRow.AddChild(CardNode(card, HandCardFrameSize, HandCardContentSize));
        }
    }

    public void RenderSnapshotSections(
        VBoxContainer snapshotRows,
        Godot.Collections.Array<Godot.Collections.Dictionary> sections)
    {
        ClearChildren(snapshotRows);
        foreach (var section in sections)
        {
            snapshotRows.AddChild(SectionNode(section));
        }
    }

    public static string PreviewSummary(Godot.Collections.Dictionary card)
    {
        if (card.TryGetValue("previewSummary", out var summaryValue))
        {
            var summary = summaryValue.AsString();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return summary;
            }
        }

        return "Card";
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }

    private Control SectionNode(Godot.Collections.Dictionary section)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 0)
        };
        var rows = new VBoxContainer();
        rows.AddChild(new Label
        {
            Text = section.TryGetValue("title", out var title) ? title.AsString() : "Section"
        });

        var zones = section.TryGetValue("zones", out var zoneValue)
            ? zoneValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        foreach (var zone in zones)
        {
            rows.AddChild(ZoneNode(zone));
        }

        frame.AddChild(rows);
        return frame;
    }

    private Control ZoneNode(Godot.Collections.Dictionary zone)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 104)
        };
        row.AddChild(new Label
        {
            CustomMinimumSize = new Vector2(112, 0),
            Text = ZoneLabel(zone),
            VerticalAlignment = VerticalAlignment.Center
        });

        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 104),
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowAlways,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var cards = new HBoxContainer();
        var cardViews = zone.TryGetValue("cards", out var cardsValue)
            ? cardsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];

        if (cardViews.Count == 0)
        {
            cards.AddChild(new Label
            {
                CustomMinimumSize = new Vector2(88, 96),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Text = "empty"
            });
        }
        else
        {
            foreach (var card in cardViews)
            {
                cards.AddChild(CardNode(card, ZoneCardFrameSize, ZoneCardContentSize));
            }
        }

        scroll.AddChild(cards);
        row.AddChild(scroll);
        return row;
    }

    private static string ZoneLabel(Godot.Collections.Dictionary zone)
    {
        var label = zone.TryGetValue("label", out var labelValue) ? labelValue.AsString() : "Zone";
        if (zone.TryGetValue("count", out var countValue))
        {
            return $"{label} {countValue.AsInt32()}";
        }

        return label;
    }

    private Control CardNode(
        Godot.Collections.Dictionary card,
        Vector2 frameSize,
        Vector2 contentSize)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = frameSize,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = PreviewSummary(card)
        };
        frame.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _cardInspected(card);
            }
        };

        var image = card.TryGetValue("image", out var imageValue) ? imageValue.As<Image>() : null;
        if (image is not null)
        {
            frame.AddChild(new TextureRect
            {
                CustomMinimumSize = contentSize,
                Texture = ImageTexture.CreateFromImage(image),
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            });
            return frame;
        }

        frame.AddChild(new Label
        {
            CustomMinimumSize = contentSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Text = card.TryGetValue("label", out var label) ? label.AsString() : "Card"
        });
        return frame;
    }
}
