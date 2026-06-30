using System;
using Godot;

namespace Riftbound.GodotClient;

internal sealed class CardControlRenderer
{
    private static readonly Vector2 HandCardFrameSize = new(74, 104);
    private static readonly Vector2 HandCardContentSize = new(70, 100);
    private static readonly Vector2 SignatureCardFrameSize = new(92, 128);
    private static readonly Vector2 SignatureCardContentSize = new(88, 124);
    private static readonly Vector2 RuneCardFrameSize = new(43, 60);
    private static readonly Vector2 RuneCardContentSize = new(39, 56);
    private static readonly Vector2 BattlefieldCardFrameSize = new(136, 96);
    private static readonly Vector2 BattlefieldCardContentSize = new(132, 92);
    private static readonly Vector2 StandbyCardFrameSize = new(52, 72);
    private static readonly Vector2 StandbyCardContentSize = new(48, 68);
    private static readonly Vector2 PileCardFrameSize = new(80, 112);
    private static readonly Vector2 PileCardContentSize = new(76, 108);
    private const int RuneDeckSize = 12;

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
        if (sections.Count == 1
            && sections[0].TryGetValue("kind", out var kind)
            && kind.AsString() == "wireTable")
        {
            snapshotRows.AddChild(WireTableNode(sections[0]));
            return;
        }

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

    private Control WireTableNode(Godot.Collections.Dictionary table)
    {
        var rows = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        rows.AddChild(WireHandRail(Player(table, "opponent"), "opponent"));
        rows.AddChild(WirePlayerHome(Player(table, "opponent"), "opponent"));
        rows.AddChild(WireBattlefield(Lanes(table)));
        rows.AddChild(WirePlayerHome(Player(table, "self"), "self"));
        rows.AddChild(WireHandRail(Player(table, "self"), "self"));

        return WireFrame(rows, new Vector2(1280, 620), borderWidth: 2);
    }

    private Control WireHandRail(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 88),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        if (side == "opponent")
        {
            row.AddChild(WireHandBody(player, side));
            row.AddChild(WireRuneTrack(player, reverse: true));
            row.AddChild(WireStack("符文", Count(player, "runeDeckCount"), new Vector2(60, 0)));
        }
        else
        {
            row.AddChild(WireStack("符文", Count(player, "runeDeckCount"), new Vector2(60, 0)));
            row.AddChild(WireRuneTrack(player, reverse: false));
            row.AddChild(WireHandBody(player, side));
        }

        return WireFrame(row, new Vector2(0, 96));
    }

    private Control WireHandBody(Godot.Collections.Dictionary player, string side)
    {
        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        if (side == "opponent")
        {
            body.AddChild(WireHandPiles(player, side));
            body.AddChild(WireHiddenHand(player));
        }
        else
        {
            body.AddChild(WireCardFlow(Cards(player, "hand"), HandCardFrameSize, HandCardContentSize, minSlots: 1));
            body.AddChild(WireHandPiles(player, side));
        }

        return body;
    }

    private Control WireHandPiles(Godot.Collections.Dictionary player, string side)
    {
        var piles = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(188, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        if (side == "opponent")
        {
            piles.AddChild(WirePublicPile(player, "graveyard", "已打出"));
            piles.AddChild(WireStack("牌库", Count(player, "mainDeckCount"), new Vector2(80, 0)));
        }
        else
        {
            piles.AddChild(WireStack("牌库", Count(player, "mainDeckCount"), new Vector2(80, 0)));
            piles.AddChild(WirePublicPile(player, "graveyard", "已打出"));
        }

        return piles;
    }

    private Control WireHiddenHand(Godot.Collections.Dictionary player)
    {
        var count = Count(player, "handHiddenCount");
        var cards = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        for (var index = 0; index < Math.Max(1, count); index++)
        {
            cards.AddChild(BackCard("手牌", HandCardFrameSize));
        }

        return WireFrame(Scrollable(cards), new Vector2(0, 0));
    }

    private Control WireRuneTrack(Godot.Collections.Dictionary player, bool reverse)
    {
        var runes = Cards(player, "baseRunes");
        var slots = new Control[RuneDeckSize];
        for (var slot = 0; slot < slots.Length; slot++)
        {
            slots[slot] = EmptySlot(RuneCardFrameSize);
        }

        for (var index = 0; index < runes.Count && index < RuneDeckSize; index++)
        {
            var slotIndex = reverse ? RuneDeckSize - 1 - index : index;
            slots[slotIndex] = CardNode(runes[index], RuneCardFrameSize, RuneCardContentSize);
        }

        var track = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(588, 0),
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        foreach (var slot in slots)
        {
            track.AddChild(slot);
        }

        return WireFrame(track, new Vector2(588, 0));
    }

    private Control WirePlayerHome(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 108),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };

        if (side == "opponent")
        {
            row.AddChild(WireBaseArea(player, side));
            row.AddChild(WireSignatureSlot(player, "hero"));
            row.AddChild(WireSignatureSlot(player, "legend"));
        }
        else
        {
            row.AddChild(WireSignatureSlot(player, "legend"));
            row.AddChild(WireSignatureSlot(player, "hero"));
            row.AddChild(WireBaseArea(player, side));
        }

        return WireFrame(row, new Vector2(0, 116));
    }

    private Control WireSignatureSlot(Godot.Collections.Dictionary player, string key)
    {
        var cards = Cards(player, key);
        return WireFrame(
            WireCardFlow(cards, SignatureCardFrameSize, SignatureCardContentSize, minSlots: 1),
            new Vector2(100, 0));
    }

    private Control WireBaseArea(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var baseCards = WireCardFlow(Cards(player, "base"), SignatureCardFrameSize, SignatureCardContentSize, minSlots: 1);
        var banish = WirePublicPile(player, "banished", "放逐");

        if (side == "opponent")
        {
            row.AddChild(banish);
            row.AddChild(baseCards);
        }
        else
        {
            row.AddChild(baseCards);
            row.AddChild(banish);
        }

        return WireFrame(row, new Vector2(0, 0));
    }

    private Control WireBattlefield(Godot.Collections.Array<Godot.Collections.Dictionary> lanes)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 228),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddChild(WireSite(lanes.Count > 0 ? lanes[0] : new Godot.Collections.Dictionary()));
        row.AddChild(WireLaneGrid(lanes));
        row.AddChild(WireSite(lanes.Count > 1 ? lanes[1] : new Godot.Collections.Dictionary()));
        return WireFrame(row, new Vector2(0, 236));
    }

    private Control WireLaneGrid(Godot.Collections.Array<Godot.Collections.Dictionary> lanes)
    {
        var grid = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        for (var index = 0; index < 2; index++)
        {
            grid.AddChild(WireLane(lanes.Count > index ? lanes[index] : new Godot.Collections.Dictionary()));
        }

        return grid;
    }

    private Control WireLane(Godot.Collections.Dictionary lane)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        column.AddChild(WireUnitZone(lane, "opponent"));
        column.AddChild(WireUnitZone(lane, "self"));
        return WireFrame(column, new Vector2(0, 0));
    }

    private Control WireUnitZone(Godot.Collections.Dictionary lane, string side)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var units = WireCardFlow(
            Cards(lane, side == "self" ? "selfUnits" : "opponentUnits"),
            HandCardFrameSize,
            HandCardContentSize,
            minSlots: 3);
        var standbyCards = Cards(lane, side == "self" ? "selfStandby" : "opponentStandby");
        var standby = standbyCards.Count == 0
            ? null
            : WireFrame(
                WireCardFlow(standbyCards, StandbyCardFrameSize, StandbyCardContentSize, minSlots: 0),
                new Vector2(64, 0));

        if (side == "self" && standby is not null)
        {
            row.AddChild(standby);
            row.AddChild(units);
        }
        else
        {
            row.AddChild(units);
            if (standby is not null)
            {
                row.AddChild(standby);
            }
        }

        return WireFrame(row, new Vector2(0, 0));
    }

    private Control WireSite(Godot.Collections.Dictionary lane)
    {
        var siteCards = Cards(lane, "site");
        return WireFrame(
            WireCardFlow(siteCards, BattlefieldCardFrameSize, BattlefieldCardContentSize, minSlots: 1),
            new Vector2(148, 0));
    }

    private Control WirePublicPile(Godot.Collections.Dictionary player, string key, string label)
    {
        var cards = Cards(player, key);
        if (cards.Count == 0)
        {
            return WireFrame(EmptySlot(PileCardFrameSize), new Vector2(92, 0));
        }

        return WireFrame(CardNode(cards[^1], PileCardFrameSize, PileCardContentSize), new Vector2(92, 0));
    }

    private Control WireStack(string label, int count, Vector2 minSize)
    {
        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddChild(LabelNode(label));
        box.AddChild(LabelNode(count.ToString()));
        return WireFrame(box, minSize);
    }

    private Control WireCardFlow(
        Godot.Collections.Array<Godot.Collections.Dictionary> cards,
        Vector2 frameSize,
        Vector2 contentSize,
        int minSlots)
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        foreach (var card in cards)
        {
            row.AddChild(CardNode(card, frameSize, contentSize));
        }

        var emptySlots = cards.Count == 0 ? Math.Max(1, minSlots) : Math.Max(0, minSlots - cards.Count);
        for (var index = 0; index < emptySlots; index++)
        {
            row.AddChild(EmptySlot(frameSize));
        }

        return WireFrame(Scrollable(row), new Vector2(0, 0));
    }

    private Control CardNode(
        Godot.Collections.Dictionary card,
        Vector2 frameSize,
        Vector2 contentSize)
    {
        var frame = WireFrame(new Control { CustomMinimumSize = contentSize }, frameSize);
        frame.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        frame.TooltipText = PreviewSummary(card);
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
            var texture = new TextureRect
            {
                CustomMinimumSize = contentSize,
                Texture = ImageTexture.CreateFromImage(image),
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            ((PanelContainer)frame).AddChild(texture);
            return frame;
        }

        ((PanelContainer)frame).AddChild(LabelNode(
            card.TryGetValue("label", out var label) ? label.AsString() : "Card",
            contentSize));
        return frame;
    }

    private static Control EmptySlot(Vector2 size)
    {
        return WireFrame(new Control { CustomMinimumSize = size }, size);
    }

    private static Control BackCard(string label, Vector2 size)
    {
        return WireFrame(LabelNode(label, size), size);
    }

    private static ScrollContainer Scrollable(Control content)
    {
        return new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 0)
        }.WithChild(content);
    }

    private static PanelContainer WireFrame(Control child, Vector2 minimumSize, int borderWidth = 1)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var style = new StyleBoxFlat
        {
            BgColor = Colors.White,
            BorderColor = Colors.Black
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(0);
        frame.AddThemeStyleboxOverride("panel", style);
        frame.AddChild(child);
        return frame;
    }

    private static Label LabelNode(string text, Vector2? minimumSize = null)
    {
        var label = new Label
        {
            Text = text,
            CustomMinimumSize = minimumSize ?? Vector2.Zero,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeColorOverride("font_color", Colors.Black);
        return label;
    }

    private static Godot.Collections.Dictionary Player(Godot.Collections.Dictionary table, string side)
    {
        return table.TryGetValue(side, out var value)
            ? value.AsGodotDictionary()
            : new Godot.Collections.Dictionary();
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> Lanes(Godot.Collections.Dictionary table)
    {
        return table.TryGetValue("lanes", out var value)
            ? value.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
    }

    private static Godot.Collections.Array<Godot.Collections.Dictionary> Cards(Godot.Collections.Dictionary section, string key)
    {
        return section.TryGetValue(key, out var value)
            ? value.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
    }

    private static int Count(Godot.Collections.Dictionary section, string key)
    {
        return section.TryGetValue(key, out var value)
            ? Math.Max(0, value.AsInt32())
            : 0;
    }

    private Control SectionNode(Godot.Collections.Dictionary section)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 0)
        };
        var rows = new VBoxContainer();
        rows.AddChild(LabelNode(section.TryGetValue("title", out var title) ? title.AsString() : "Section"));

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
            CustomMinimumSize = new Vector2(0, 128)
        };
        row.AddChild(LabelNode(ZoneLabel(zone), new Vector2(112, 0)));

        var cards = zone.TryGetValue("cards", out var cardsValue)
            ? cardsValue.As<Godot.Collections.Array<Godot.Collections.Dictionary>>()
            : [];
        row.AddChild(WireCardFlow(cards, HandCardFrameSize, HandCardContentSize, minSlots: 1));
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
}

internal static class GodotControlExtensions
{
    public static T WithChild<T>(this T parent, Control child)
        where T : Control
    {
        parent.AddChild(child);
        return parent;
    }
}
