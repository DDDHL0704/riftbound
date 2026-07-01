using System;
using Godot;

namespace Riftbound.GodotClient;

internal sealed class CardControlRenderer
{
    private static readonly Vector2 HandCardFrameSize = new(58, 82);
    private static readonly Vector2 HandCardContentSize = new(54, 78);
    private static readonly Vector2 SignatureCardFrameSize = new(72, 100);
    private static readonly Vector2 SignatureCardContentSize = new(68, 96);
    private static readonly Vector2 RuneCardFrameSize = new(34, 48);
    private static readonly Vector2 RuneCardContentSize = new(30, 44);
    private static readonly Vector2 BattlefieldCardFrameSize = new(112, 80);
    private static readonly Vector2 BattlefieldCardContentSize = new(108, 76);
    private static readonly Vector2 StandbyCardFrameSize = new(42, 58);
    private static readonly Vector2 StandbyCardContentSize = new(38, 54);
    private static readonly Vector2 PileCardFrameSize = new(64, 90);
    private static readonly Vector2 PileCardContentSize = new(60, 86);
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
        rows.AddThemeConstantOverride("separation", 2);
        rows.AddChild(WireHandRail(Player(table, "opponent"), "opponent"));
        rows.AddChild(WirePlayerHome(Player(table, "opponent"), "opponent"));
        rows.AddChild(WireBattlefield(Lanes(table)));
        rows.AddChild(WirePlayerHome(Player(table, "self"), "self"));
        rows.AddChild(WireHandRail(Player(table, "self"), "self"));

        return WireFrame(rows, new Vector2(1280, 560), borderWidth: 2, RunestoneSurface.Table);
    }

    private Control WireHandRail(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 72),
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

        return WireFrame(row, new Vector2(0, 78), surface: RunestoneSurface.Rail);
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
            CustomMinimumSize = new Vector2(154, 0),
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

        return WireFrame(Scrollable(cards), new Vector2(0, 0), surface: RunestoneSurface.Rail);
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
            CustomMinimumSize = new Vector2(482, 0),
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        foreach (var slot in slots)
        {
            track.AddChild(slot);
        }

        return WireFrame(track, new Vector2(482, 0), surface: RunestoneSurface.Rail);
    }

    private Control WirePlayerHome(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 92),
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

        return WireFrame(row, new Vector2(0, 98), surface: RunestoneSurface.Rail);
    }

    private Control WireSignatureSlot(Godot.Collections.Dictionary player, string key)
    {
        var cards = Cards(player, key);
        return WireFrame(
            WireCardFlow(cards, SignatureCardFrameSize, SignatureCardContentSize, minSlots: 1),
            new Vector2(82, 0),
            surface: RunestoneSurface.Zone);
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

        return WireFrame(row, new Vector2(0, 0), surface: RunestoneSurface.Zone);
    }

    private Control WireBattlefield(Godot.Collections.Array<Godot.Collections.Dictionary> lanes)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 196),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddChild(WireSite(lanes.Count > 0 ? lanes[0] : new Godot.Collections.Dictionary()));
        row.AddChild(WireLaneGrid(lanes));
        row.AddChild(WireSite(lanes.Count > 1 ? lanes[1] : new Godot.Collections.Dictionary()));
        return WireFrame(row, new Vector2(0, 202), surface: RunestoneSurface.Zone);
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
        return WireFrame(column, new Vector2(0, 0), surface: RunestoneSurface.Zone);
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
                new Vector2(52, 0),
                surface: RunestoneSurface.Zone);

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

        return WireFrame(row, new Vector2(0, 0), surface: RunestoneSurface.Zone);
    }

    private Control WireSite(Godot.Collections.Dictionary lane)
    {
        var siteCards = Cards(lane, "site");
        return WireFrame(
            WireCardFlow(siteCards, BattlefieldCardFrameSize, BattlefieldCardContentSize, minSlots: 1),
            new Vector2(124, 0),
            surface: RunestoneSurface.Zone);
    }

    private Control WirePublicPile(Godot.Collections.Dictionary player, string key, string label)
    {
        var cards = Cards(player, key);
        if (cards.Count == 0)
        {
            return WireFrame(EmptySlot(PileCardFrameSize), new Vector2(76, 0), surface: RunestoneSurface.Stack);
        }

        return WireFrame(CardNode(cards[^1], PileCardFrameSize, PileCardContentSize), new Vector2(76, 0), surface: RunestoneSurface.Stack);
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
        return WireFrame(box, minSize, surface: RunestoneSurface.Stack);
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

        return WireFrame(Scrollable(row), new Vector2(0, 0), surface: RunestoneSurface.Zone);
    }

    private Control CardNode(
        Godot.Collections.Dictionary card,
        Vector2 frameSize,
        Vector2 contentSize)
    {
        var visible = !card.TryGetValue("visible", out var visibleValue) || visibleValue.AsBool();
        var faceDown = card.TryGetValue("faceDown", out var faceDownValue) && faceDownValue.AsBool();
        Control content;
        var surface = RunestoneSurface.Card;
        if (!visible || faceDown)
        {
            content = CardBackContent("暗牌", contentSize);
            surface = RunestoneSurface.CardBack;
        }
        else
        {
            var image = card.TryGetValue("image", out var imageValue) ? imageValue.As<Image>() : null;
            content = VisibleCardContent(card, contentSize, image);
        }

        var frame = WireFrame(content, frameSize, borderWidth: 2, surface: surface);
        frame.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        frame.TooltipText = PreviewSummary(card);
        frame.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _cardInspected(card);
            }
        };
        return frame;
    }

    private static Control EmptySlot(Vector2 size)
    {
        return WireFrame(new Control { CustomMinimumSize = size }, size, surface: RunestoneSurface.Slot);
    }

    private static Control BackCard(string label, Vector2 size)
    {
        return WireFrame(CardBackContent(label, size), size, borderWidth: 2, surface: RunestoneSurface.CardBack);
    }

    private static Control VisibleCardContent(
        Godot.Collections.Dictionary card,
        Vector2 contentSize,
        Image? image)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = contentSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 1);

        box.AddChild(CardTopBar(card, contentSize));

        var artHeight = MathF.Max(20f, contentSize.Y * (contentSize.Y >= 74f ? 0.5f : 0.42f));
        box.AddChild(CardArtPanel(card, image, new Vector2(contentSize.X, artHeight)));

        box.AddChild(CardFooter(card, contentSize));
        return box;
    }

    private static Control CardTopBar(Godot.Collections.Dictionary card, Vector2 contentSize)
    {
        var energy = Value(card, "energy");
        var power = Value(card, "power");
        if (energy < 0 && power < 0)
        {
            return new Control
            {
                CustomMinimumSize = new Vector2(contentSize.X, 1)
            };
        }

        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(contentSize.X, 14),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 1);
        if (energy >= 0)
        {
            row.AddChild(StatBadge("C", energy, RunestoneTheme.Brass, new Vector2(22, 14)));
        }

        row.AddChild(new Control
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });
        if (power >= 0)
        {
            row.AddChild(StatBadge("P", power, RunestoneTheme.Crimson, new Vector2(22, 14)));
        }

        return row;
    }

    private static Control CardArtPanel(
        Godot.Collections.Dictionary card,
        Image? image,
        Vector2 size)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = size,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        frame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Slot));

        if (image is not null)
        {
            frame.AddChild(new TextureRect
            {
                Texture = ImageTexture.CreateFromImage(image),
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = size,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            });
            return frame;
        }

        var placeholder = new VBoxContainer
        {
            CustomMinimumSize = size,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var title = CardTitle(card);
        var artLabel = LabelNode(string.IsNullOrWhiteSpace(title) ? "CARD" : title, new Vector2(size.X, 0));
        artLabel.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
        placeholder.AddChild(artLabel);
        frame.AddChild(placeholder);
        return frame;
    }

    private static Control CardFooter(Godot.Collections.Dictionary card, Vector2 contentSize)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(contentSize.X, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 0);

        var title = LabelNode(CompactCardTitle(CardTitle(card)), new Vector2(contentSize.X, 0));
        title.AddThemeColorOverride("font_color", RunestoneTheme.Ink);
        box.AddChild(title);

        var detail = CardDetailLine(card);
        if (!string.IsNullOrWhiteSpace(detail) && contentSize.Y >= 58f)
        {
            var detailLabel = LabelNode(detail, new Vector2(contentSize.X, 0));
            detailLabel.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
            box.AddChild(detailLabel);
        }

        var effect = card.TryGetValue("effectText", out var effectValue)
            ? CompactEffect(effectValue.AsString())
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(effect) && contentSize.X >= 58f && contentSize.Y >= 92f)
        {
            var effectLabel = LabelNode(effect, new Vector2(contentSize.X, 0));
            effectLabel.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
            box.AddChild(effectLabel);
        }

        return box;
    }

    private static Control StatBadge(string prefix, int value, Color border, Vector2 size)
    {
        var badge = new PanelContainer
        {
            CustomMinimumSize = size
        };
        badge.AddThemeStyleboxOverride("panel", RunestoneTheme.ButtonStyle(new Color(0.02f, 0.018f, 0.016f, 0.94f), border));
        var label = LabelNode($"{prefix}{value}", size);
        label.AddThemeColorOverride("font_color", RunestoneTheme.Ink);
        badge.AddChild(label);
        return badge;
    }

    private static Control TextCardFace(Godot.Collections.Dictionary card, Vector2 contentSize)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = contentSize,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 3);

        var stats = CardStatsLine(card);
        if (!string.IsNullOrWhiteSpace(stats))
        {
            var statsLabel = LabelNode(stats);
            statsLabel.AddThemeColorOverride("font_color", RunestoneTheme.Brass);
            box.AddChild(statsLabel);
        }

        box.AddChild(LabelNode(
            card.TryGetValue("cardName", out var cardName) && !string.IsNullOrWhiteSpace(cardName.AsString())
                ? cardName.AsString()
                : card.TryGetValue("label", out var label) ? label.AsString() : "Card",
            new Vector2(contentSize.X, 0)));

        var category = card.TryGetValue("category", out var categoryValue) ? categoryValue.AsString() : string.Empty;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryLabel = LabelNode(category, new Vector2(contentSize.X, 0));
            categoryLabel.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
            box.AddChild(categoryLabel);
        }

        var trait = card.TryGetValue("trait", out var traitValue) ? traitValue.AsString() : string.Empty;
        if (!string.IsNullOrWhiteSpace(trait))
        {
            var traitLabel = LabelNode(trait, new Vector2(contentSize.X, 0));
            traitLabel.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
            box.AddChild(traitLabel);
        }

        var effect = card.TryGetValue("effectText", out var effectValue)
            ? CompactEffect(effectValue.AsString())
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(effect))
        {
            var effectLabel = LabelNode(effect, new Vector2(contentSize.X, 0));
            effectLabel.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
            box.AddChild(effectLabel);
        }

        return box;
    }

    private static string CardTitle(Godot.Collections.Dictionary card)
    {
        if (card.TryGetValue("cardName", out var cardName) && !string.IsNullOrWhiteSpace(cardName.AsString()))
        {
            return cardName.AsString();
        }

        return card.TryGetValue("label", out var label) ? label.AsString() : "Card";
    }

    private static string CompactCardTitle(string title)
    {
        title = title.Replace('\n', ' ').Trim();
        return title.Length <= 12 ? title : $"{title[..11]}…";
    }

    private static string CardDetailLine(Godot.Collections.Dictionary card)
    {
        var category = card.TryGetValue("category", out var categoryValue) ? categoryValue.AsString() : string.Empty;
        var trait = card.TryGetValue("trait", out var traitValue) ? traitValue.AsString() : string.Empty;
        if (string.IsNullOrWhiteSpace(category))
        {
            return trait;
        }

        return string.IsNullOrWhiteSpace(trait) ? category : $"{category} · {trait}";
    }

    private static string CompactEffect(string effect)
    {
        var compact = effect
            .Replace("{{", string.Empty, StringComparison.Ordinal)
            .Replace("}}", string.Empty, StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Trim();
        return compact.Length <= 34 ? compact : $"{compact[..33]}…";
    }

    private static int Value(Godot.Collections.Dictionary card, string key)
    {
        return card.TryGetValue(key, out var value) ? value.AsInt32() : -1;
    }

    private static string CardStatsLine(Godot.Collections.Dictionary card)
    {
        var energy = card.TryGetValue("energy", out var energyValue) ? energyValue.AsInt32() : -1;
        var power = card.TryGetValue("power", out var powerValue) ? powerValue.AsInt32() : -1;
        return (energy, power) switch
        {
            (>= 0, >= 0) => $"C {energy}  /  P {power}",
            (>= 0, _) => $"C {energy}",
            (_, >= 0) => $"P {power}",
            _ => string.Empty
        };
    }

    private static Control CardBackContent(string label, Vector2 size)
    {
        var box = new VBoxContainer
        {
            CustomMinimumSize = size,
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 2);
        var topGlyph = LabelNode("◆", new Vector2(size.X, 0));
        topGlyph.AddThemeColorOverride("font_color", RunestoneTheme.Brass);
        box.AddChild(topGlyph);

        var seal = LabelNode("◇", new Vector2(size.X, 0));
        seal.AddThemeColorOverride("font_color", RunestoneTheme.Crimson);
        box.AddChild(seal);

        var labelNode = LabelNode(label, new Vector2(size.X, 0));
        labelNode.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
        box.AddChild(labelNode);

        var bottomGlyph = LabelNode("◆", new Vector2(size.X, 0));
        bottomGlyph.AddThemeColorOverride("font_color", RunestoneTheme.BrassDim);
        box.AddChild(bottomGlyph);
        return box;
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

    private static PanelContainer WireFrame(
        Control child,
        Vector2 minimumSize,
        int borderWidth = 1,
        RunestoneSurface surface = RunestoneSurface.Zone)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = minimumSize,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        frame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(surface, borderWidth));
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
        label.AddThemeColorOverride("font_color", RunestoneTheme.Ink);
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
        frame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Zone));
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
