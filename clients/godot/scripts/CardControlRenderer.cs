using System;
using System.IO;
using Godot;

namespace Riftbound.GodotClient;

internal sealed class CardControlRenderer
{
    private static readonly Vector2 HandCardFrameSize = new(52, 72);
    private static readonly Vector2 HandCardContentSize = new(48, 68);
    private static readonly Vector2 SignatureCardFrameSize = new(64, 86);
    private static readonly Vector2 SignatureCardContentSize = new(60, 82);
    private static readonly Vector2 RuneCardFrameSize = new(30, 42);
    private static readonly Vector2 RuneCardContentSize = new(26, 38);
    private static readonly Vector2 LaneUnitCardFrameSize = new(46, 58);
    private static readonly Vector2 LaneUnitCardContentSize = new(42, 54);
    private static readonly Vector2 BattlefieldCardFrameSize = new(104, 72);
    private static readonly Vector2 BattlefieldCardContentSize = new(100, 68);
    private static readonly Vector2 StandbyCardFrameSize = new(38, 52);
    private static readonly Vector2 StandbyCardContentSize = new(34, 48);
    private static readonly Vector2 PileCardFrameSize = new(56, 78);
    private static readonly Vector2 PileCardContentSize = new(52, 74);
    private const int RuneDeckSize = 12;

    private readonly Action<Godot.Collections.Dictionary> _cardInspected;
    private readonly Func<string, bool> _isPromptSource;

    public CardControlRenderer(
        Action<Godot.Collections.Dictionary> cardInspected,
        Func<string, bool> isPromptSource)
    {
        _cardInspected = cardInspected;
        _isPromptSource = isPromptSource;
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
            CustomMinimumSize = new Vector2(0, 820),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        rows.AddThemeConstantOverride("separation", 2);
        rows.AddChild(WireResourceRail(Player(table, "opponent"), "opponent"));
        rows.AddChild(WirePlayBand(Player(table, "opponent"), "opponent", Lanes(table)));
        rows.AddChild(WireSiteDivider(Lanes(table)));
        rows.AddChild(WirePlayBand(Player(table, "self"), "self", Lanes(table)));
        rows.AddChild(WireResourceRail(Player(table, "self"), "self"));

        return WireFrame(rows, new Vector2(0, 820), borderWidth: 2, RunestoneSurface.Table);
    }

    private Control WireResourceRail(Godot.Collections.Dictionary player, string side)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 94),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 4);

        row.AddChild(WirePlayerBadge(player, side));
        row.AddChild(WireStack("牌库", Count(player, "mainDeckCount"), new Vector2(58, 0)));
        row.AddChild(WirePublicPile(player, "graveyard", "已打出"));
        row.AddChild(side == "opponent"
            ? WireHiddenHand(player)
            : WireCardFlow(Cards(player, "hand"), HandCardFrameSize, HandCardContentSize, minSlots: 1));
        row.AddChild(WireRuneTrack(player, reverse: side == "opponent"));
        row.AddChild(WireStack("符文", Count(player, "runeDeckCount"), new Vector2(58, 0)));

        return WireFrame(row, new Vector2(0, 100), surface: RunestoneSurface.Rail);
    }

    private Control WirePlayerBadge(Godot.Collections.Dictionary player, string side)
    {
        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        box.AddThemeConstantOverride("separation", 1);

        var name = side == "self" ? "P1 我方" : "P2 对手";
        var label = LabelNode(name, new Vector2(70, 0));
        label.AddThemeColorOverride("font_color", RunestoneTheme.Ivory);
        box.AddChild(label);

        var score = LabelNode($"分数 {Count(player, "score")}", new Vector2(70, 0));
        score.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
        box.AddChild(score);

        return WireFrame(box, new Vector2(74, 0), surface: RunestoneSurface.Stack);
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

    private Control WirePlayBand(
        Godot.Collections.Dictionary player,
        string side,
        Godot.Collections.Array<Godot.Collections.Dictionary> lanes)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 148),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 4);

        if (side == "opponent")
        {
            row.AddChild(WireHomeCluster(player, side));
            row.AddChild(WireFormationSlots(lanes, side));
        }
        else
        {
            row.AddChild(WireFormationSlots(lanes, side));
            row.AddChild(WireHomeCluster(player, side));
        }

        return WireFrame(row, new Vector2(0, 152), surface: RunestoneSurface.Rail);
    }

    private Control WireHomeCluster(Godot.Collections.Dictionary player, string side)
    {
        var cluster = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(250, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        cluster.AddThemeConstantOverride("separation", 4);

        if (side == "opponent")
        {
            cluster.AddChild(WirePublicPile(player, "banished", "放逐"));
            cluster.AddChild(WireSignatureSlot(player, "base"));
            cluster.AddChild(WireSignatureSlot(player, "hero"));
            cluster.AddChild(WireSignatureSlot(player, "legend"));
        }
        else
        {
            cluster.AddChild(WireSignatureSlot(player, "legend"));
            cluster.AddChild(WireSignatureSlot(player, "hero"));
            cluster.AddChild(WireSignatureSlot(player, "base"));
            cluster.AddChild(WirePublicPile(player, "banished", "放逐"));
        }

        return WireFrame(cluster, new Vector2(258, 0), surface: RunestoneSurface.Zone);
    }

    private Control WireSignatureSlot(Godot.Collections.Dictionary player, string key)
    {
        var cards = Cards(player, key);
        return WireFrame(
            WireCardFlow(cards, SignatureCardFrameSize, SignatureCardContentSize, minSlots: 1),
            new Vector2(72, 0),
            surface: RunestoneSurface.Zone);
    }

    private Control WireFormationSlots(Godot.Collections.Array<Godot.Collections.Dictionary> lanes, string side)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 4);
        for (var index = 0; index < 2; index++)
        {
            row.AddChild(WireLaneUnitPanel(lanes.Count > index ? lanes[index] : new Godot.Collections.Dictionary(), side));
        }

        return WireFrame(row, new Vector2(0, 0), surface: RunestoneSurface.Zone);
    }

    private Control WireLaneUnitPanel(Godot.Collections.Dictionary lane, string side)
    {
        var column = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        var zoneSide = side == "self" ? "self" : "opponent";
        column.AddChild(BandLabel($"战场 {LaneNumber(lane)} · {(side == "self" ? "我方单位" : "对手单位")}"));
        column.AddChild(WireUnitZone(lane, zoneSide));
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
            LaneUnitCardFrameSize,
            LaneUnitCardContentSize,
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

    private Control WireSiteDivider(Godot.Collections.Array<Godot.Collections.Dictionary> lanes)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 98),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(DividerLine());
        for (var index = 0; index < 2; index++)
        {
            row.AddChild(WireSiteSocket(lanes.Count > index ? lanes[index] : new Godot.Collections.Dictionary()));
            if (index == 0)
            {
                var seal = LabelNode("◆", new Vector2(26, 0));
                seal.AddThemeColorOverride("font_color", RunestoneTheme.Brass);
                row.AddChild(seal);
            }
        }

        row.AddChild(DividerLine());
        return WireFrame(row, new Vector2(0, 102), surface: RunestoneSurface.Zone);
    }

    private Control WireSiteSocket(Godot.Collections.Dictionary lane)
    {
        var row = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        row.AddThemeConstantOverride("separation", 4);
        var label = LabelNode($"战场 {LaneNumber(lane)}\n据点", new Vector2(54, 0));
        label.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
        row.AddChild(label);
        row.AddChild(WireCardFlow(Cards(lane, "site"), BattlefieldCardFrameSize, BattlefieldCardContentSize, minSlots: 1));
        return WireFrame(row, new Vector2(0, 0), surface: RunestoneSurface.Stack);
    }

    private static Control DividerLine()
    {
        return WireFrame(
            new Control
            {
                CustomMinimumSize = new Vector2(0, 2),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            },
            new Vector2(0, 0),
            surface: RunestoneSurface.Stack);
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

    private static Label BandLabel(string text)
    {
        var label = LabelNode(text, new Vector2(0, 18));
        label.AddThemeColorOverride("font_color", RunestoneTheme.MutedInk);
        return label;
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
        var objectId = card.TryGetValue("objectId", out var objectValue) ? objectValue.AsString() : string.Empty;
        var promptSource = !string.IsNullOrWhiteSpace(objectId) && _isPromptSource(objectId);
        Control content;
        var surface = RunestoneSurface.Card;
        if (!visible || faceDown)
        {
            content = CardBackContent("暗牌", contentSize);
            surface = RunestoneSurface.CardBack;
        }
        else
        {
            var imagePath = card.TryGetValue("imagePath", out var imagePathValue) ? imagePathValue.AsString() : string.Empty;
            content = VisibleCardContent(card, contentSize, imagePath);
        }

        var frame = WireFrame(content, frameSize, borderWidth: promptSource ? 3 : 2, surface: promptSource ? RunestoneSurface.Result : surface);
        AttachCardMotion(frame, frameSize, promptSource);
        frame.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        frame.TooltipText = promptSource
            ? $"服务端候选来源\n{PreviewSummary(card)}"
            : PreviewSummary(card);
        frame.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                FlashCardPress(frame);
                _cardInspected(card);
            }
        };
        return frame;
    }

    private static void AttachCardMotion(PanelContainer frame, Vector2 frameSize, bool promptSource)
    {
        frame.PivotOffset = frameSize * 0.5f;
        Tween? hoverTween = null;
        var hoverScale = promptSource ? 1.08f : 1.06f;
        var restScale = Vector2.One;
        var restPosition = Vector2.Zero;
        var hovered = false;

        frame.Ready += () =>
        {
            frame.PivotOffset = frame.Size * 0.5f;
            restPosition = frame.Position;
            if (promptSource)
            {
                StartPromptPulse(frame);
            }
        };
        frame.Resized += () => frame.PivotOffset = frame.Size * 0.5f;
        frame.TreeExiting += () => hoverTween?.Kill();
        frame.MouseEntered += () =>
        {
            hoverTween?.Kill();
            if (!hovered)
            {
                restPosition = frame.Position;
            }

            hovered = true;
            frame.ZIndex = promptSource ? 40 : 30;
            hoverTween = frame.CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.TweenProperty(frame, "scale", Vector2.One * hoverScale, 0.11d)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            hoverTween.TweenProperty(frame, "position", restPosition + new Vector2(0, -5), 0.11d)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        };
        frame.MouseExited += () =>
        {
            hoverTween?.Kill();
            hovered = false;
            frame.ZIndex = 0;
            hoverTween = frame.CreateTween();
            hoverTween.SetParallel(true);
            hoverTween.TweenProperty(frame, "scale", restScale, 0.14d)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
            hoverTween.TweenProperty(frame, "position", restPosition, 0.14d)
                .SetTrans(Tween.TransitionType.Cubic)
                .SetEase(Tween.EaseType.Out);
        };
    }

    private static void StartPromptPulse(Control frame)
    {
        var tween = frame.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(frame, "modulate", new Color(1f, 0.94f, 0.78f, 1f), 0.62d)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(frame, "modulate", Colors.White, 0.62d)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private static void FlashCardPress(Control frame)
    {
        var tween = frame.CreateTween();
        tween.TweenProperty(frame, "modulate", new Color(1f, 0.92f, 0.72f, 1f), 0.05d)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(frame, "modulate", Colors.White, 0.16d)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
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
        string imagePath)
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
        box.AddChild(CardArtPanel(card, imagePath, new Vector2(contentSize.X, artHeight)));

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
        string imagePath,
        Vector2 size)
    {
        var frame = new PanelContainer
        {
            CustomMinimumSize = size,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        frame.AddThemeStyleboxOverride("panel", RunestoneTheme.FrameStyle(RunestoneSurface.Slot));

        var texture = LoadTextureFromImagePath(imagePath);
        if (texture is not null)
        {
            frame.AddChild(new TextureRect
            {
                Texture = texture,
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

    public static Texture2D? LoadTextureFromImagePath(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(imagePath);
        var image = new Image();
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        var error = extension switch
        {
            ".png" => image.LoadPngFromBuffer(bytes),
            ".jpg" or ".jpeg" => image.LoadJpgFromBuffer(bytes),
            ".webp" => image.LoadWebpFromBuffer(bytes),
            _ => Error.Unavailable
        };

        if (error != Error.Ok)
        {
            image.Dispose();
            return null;
        }

        var texture = ImageTexture.CreateFromImage(image);
        image.Dispose();
        return texture;
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
        if (!string.IsNullOrWhiteSpace(effect) && contentSize.X >= 100f && contentSize.Y >= 120f)
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

    private static string PlayerName(Godot.Collections.Dictionary player, string side)
    {
        var fallback = side == "self" ? "P1 我方" : "P2 对手";
        var score = Count(player, "score");
        return $"{fallback}\n分数 {score}";
    }

    private static int LaneNumber(Godot.Collections.Dictionary lane)
    {
        return Count(lane, "index") + 1;
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
