using System;
using System.Collections.Generic;
using Godot;
using CardArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using CardDictionary = Godot.Collections.Dictionary;

namespace Riftbound.GodotClient.Ui;

public sealed class MatchTableRenderer
{
    private const string OfficialCardScenePath = "res://scenes/components/OfficialCardView.tscn";

    private readonly MatchScreen _screen;
    private readonly Action<CardDictionary> _cardActivated;
    private readonly PackedScene _officialCardScene;
    private readonly Label _opponentSummary;
    private readonly Label _selfSummary;
    private readonly Label _selfHandCount;
    private readonly Container _opponentHand;
    private readonly Container _opponentPublicZones;
    private readonly Container _selfPublicZones;
    private readonly Container _selfHand;
    private readonly BattlefieldNodes[] _battlefields;
    private readonly Dictionary<string, CardBinding> _cardBindings = new(StringComparer.Ordinal);

    private Vector2 _handCardSize = new(112, 156);
    private Vector2 _tableCardSize = new(84, 117);
    private Vector2 _compactCardSize = new(58, 81);
    private Vector2 SiteCardSize => new(_tableCardSize.Y, _tableCardSize.X);

    public MatchTableRenderer(MatchScreen screen, Action<CardDictionary> cardActivated)
    {
        _screen = screen;
        _cardActivated = cardActivated;
        _officialCardScene = GD.Load<PackedScene>(OfficialCardScenePath)
            ?? throw new InvalidOperationException($"Unable to load {OfficialCardScenePath}.");
        _opponentSummary = screen.GetNode<Label>("%OpponentSummary");
        _selfSummary = screen.GetNode<Label>("%SelfSummary");
        _selfHandCount = screen.GetNode<Label>("%SelfHandCount");
        _opponentHand = screen.GetNode<Container>("%OpponentHand");
        _opponentPublicZones = screen.GetNode<Container>("%OpponentPublicZones");
        _selfPublicZones = screen.GetNode<Container>("%SelfPublicZones");
        _selfHand = screen.GetNode<Container>("%SelfHand");
        _battlefields =
        [
            ReadBattlefieldNodes(screen, "BattlefieldOne", "%BattlefieldOneState"),
            ReadBattlefieldNodes(screen, "BattlefieldTwo", "%BattlefieldTwoState")
        ];
    }

    public void Render(CardDictionary wireTable)
    {
        ConfigureCardSizes();
        _cardBindings.Clear();

        var opponent = ReadDictionary(wireTable, "opponent");
        var self = ReadDictionary(wireTable, "self");
        RenderPlayerSummary(_opponentSummary, "对手", opponent, showHiddenHand: true);
        RenderPlayerSummary(_selfSummary, "我方", self, showHiddenHand: false);
        RenderOpponentHand(opponent);
        RenderPublicZones(_opponentPublicZones, opponent);
        RenderPublicZones(_selfPublicZones, self);
        RenderSelfHand(self);

        var lanes = ReadCards(wireTable, "lanes");
        for (var index = 0; index < _battlefields.Length; index++)
        {
            RenderBattlefield(
                _battlefields[index],
                index < lanes.Count ? lanes[index] : new CardDictionary());
        }
    }

    public void Clear()
    {
        _cardBindings.Clear();
        _opponentSummary.Text = "对手 · 等待数据";
        _selfSummary.Text = "我方 · 等待数据";
        _selfHandCount.Text = "0 张";
        ClearChildren(_opponentHand);
        ClearChildren(_opponentPublicZones);
        ClearChildren(_selfPublicZones);
        ClearChildren(_selfHand);
        _opponentHand.AddChild(SecondaryLabel("手牌未就绪"));
        _opponentPublicZones.AddChild(SecondaryLabel("公开区未就绪"));
        _selfPublicZones.AddChild(SecondaryLabel("公开区未就绪"));
        _selfHand.AddChild(SecondaryLabel("手牌未就绪"));

        foreach (var battlefield in _battlefields)
        {
            ClearChildren(battlefield.OpponentUnits);
            ClearChildren(battlefield.OfficialSite);
            ClearChildren(battlefield.SelfUnits);
            ClearChildren(battlefield.Standby);
            battlefield.OpponentUnits.AddChild(SecondaryLabel("暂无单位"));
            battlefield.OfficialSite.AddChild(SecondaryLabel("未放置场地"));
            battlefield.SelfUnits.AddChild(SecondaryLabel("暂无单位"));
            battlefield.Standby.AddChild(SecondaryLabel("备战区为空"));
            battlefield.State.Text = "等待战场数据";
        }
    }

    public void ClearPromptStates()
    {
        foreach (var binding in _cardBindings.Values)
        {
            binding.View.Display(binding.Card, binding.RestingState);
        }
    }

    public void SetObjectState(string objectId, OfficialCardVisualState state)
    {
        if (!string.IsNullOrWhiteSpace(objectId)
            && _cardBindings.TryGetValue(objectId, out var binding))
        {
            binding.View.Display(binding.Card, state);
        }
    }

    private void ConfigureCardSizes()
    {
        var compactViewport = _screen.GetViewportRect().Size.Y <= 760;
        _handCardSize = compactViewport ? new Vector2(88, 123) : new Vector2(112, 156);
        _tableCardSize = compactViewport ? new Vector2(64, 89) : new Vector2(84, 117);
        _compactCardSize = compactViewport ? new Vector2(48, 67) : new Vector2(58, 81);
    }

    private void RenderOpponentHand(CardDictionary opponent)
    {
        ClearChildren(_opponentHand);
        var hiddenCount = Math.Max(
            ReadInt(opponent, "handHiddenCount"),
            ReadCards(opponent, "hand").Count);
        if (hiddenCount == 0)
        {
            _opponentHand.AddChild(SecondaryLabel("手牌为空"));
            return;
        }

        AddCard(_opponentHand, NeutralHiddenCard(hiddenCount), _compactCardSize);
    }

    private void RenderSelfHand(CardDictionary self)
    {
        ClearChildren(_selfHand);
        var cards = ReadCards(self, "hand");
        _selfHandCount.Text = $"{cards.Count} 张";
        if (cards.Count == 0)
        {
            _selfHand.AddChild(SecondaryLabel("手牌为空"));
            return;
        }

        foreach (var card in cards)
        {
            AddCard(_selfHand, card, _handCardSize);
        }
    }

    private void RenderPublicZones(Container parent, CardDictionary player)
    {
        ClearChildren(parent);
        foreach (var (key, label) in PublicZones)
        {
            var cards = ReadCards(player, key);
            var zone = new VBoxContainer
            {
                Name = $"{key}Zone",
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            zone.AddThemeConstantOverride("separation", 3);
            zone.AddChild(SecondaryLabel(cards.Count == 0 ? $"{label} 0" : label));
            parent.AddChild(zone);

            if (cards.Count > 0)
            {
                AddCard(zone, cards[cards.Count - 1], _compactCardSize, cards.Count);
            }
        }
    }

    private void RenderBattlefield(BattlefieldNodes nodes, CardDictionary lane)
    {
        RenderCardZone(nodes.OpponentUnits, ReadCards(lane, "opponentUnits"), "暂无对手单位", _tableCardSize);
        RenderCardZone(nodes.OfficialSite, ReadCards(lane, "site"), "未放置场地", SiteCardSize);
        RenderCardZone(nodes.SelfUnits, ReadCards(lane, "selfUnits"), "暂无我方单位", _tableCardSize);
        RenderStandby(nodes.Standby, lane);

        var controlled = !string.IsNullOrWhiteSpace(ReadString(lane, "controllerId"));
        var scored = ReadBool(lane, "scoredThisTurn", false);
        nodes.State.Text = controlled
            ? scored ? "已控制 · 本回合已得分" : "已控制 · 等待计分"
            : "尚未控制";
        nodes.State.AddThemeColorOverride(
            "font_color",
            scored ? MinimalTheme.Selected : MinimalTheme.TextSecondary);
    }

    private void RenderStandby(Container parent, CardDictionary lane)
    {
        ClearChildren(parent);
        var opponentCards = ReadCards(lane, "opponentStandby");
        var selfCards = ReadCards(lane, "selfStandby");
        var hiddenCount = ReadInt(lane, "hiddenStandbyCount");
        if (opponentCards.Count == 0 && selfCards.Count == 0 && hiddenCount == 0)
        {
            parent.AddChild(SecondaryLabel("备战区为空"));
            return;
        }

        parent.AddChild(SecondaryLabel("对手备战"));
        var renderedHidden = 0;
        foreach (var card in opponentCards)
        {
            if (IsHidden(card))
            {
                renderedHidden++;
            }

            AddCard(parent, card, _compactCardSize);
        }

        var additionalHidden = Math.Max(0, hiddenCount - renderedHidden);
        if (additionalHidden > 0)
        {
            AddCard(parent, NeutralHiddenCard(additionalHidden), _compactCardSize);
        }

        parent.AddChild(SecondaryLabel("我方备战"));
        foreach (var card in selfCards)
        {
            AddCard(parent, card, _compactCardSize);
        }
    }

    private void RenderCardZone(
        Container parent,
        CardArray cards,
        string emptyLabel,
        Vector2 cardSize)
    {
        ClearChildren(parent);
        if (cards.Count == 0)
        {
            parent.AddChild(SecondaryLabel(emptyLabel));
            return;
        }

        foreach (var card in cards)
        {
            AddCard(parent, card, cardSize);
        }
    }

    private void AddCard(
        Container parent,
        CardDictionary card,
        Vector2 size,
        int count = 1)
    {
        var hidden = !ReadBool(card, "visible", true) || ReadBool(card, "faceDown", false);
        var safeCard = hidden ? NeutralHiddenCard(card, count) : card.Duplicate(true);
        if (count > 1)
        {
            safeCard["count"] = count;
        }

        var restingState = hidden ? OfficialCardVisualState.Hidden : OfficialCardVisualState.Normal;
        var view = _officialCardScene.Instantiate<OfficialCardView>();
        view.CustomMinimumSize = size;
        view.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        view.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        view.Activated += activatedCard => _cardActivated(activatedCard);
        parent.AddChild(view);
        view.Display(safeCard, restingState);

        if (!hidden)
        {
            var objectId = ReadString(safeCard, "objectId");
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                _cardBindings[objectId] = new CardBinding(view, safeCard, restingState);
            }
        }
    }

    private static CardDictionary NeutralHiddenCard(int count)
    {
        return new CardDictionary
        {
            ["visible"] = false,
            ["faceDown"] = true,
            ["count"] = Math.Max(1, count)
        };
    }

    private static CardDictionary NeutralHiddenCard(CardDictionary source, int count)
    {
        return new CardDictionary
        {
            ["visible"] = false,
            ["faceDown"] = true,
            ["count"] = Math.Max(1, Math.Max(count, ReadInt(source, "count", 1)))
        };
    }

    private static bool IsHidden(CardDictionary card)
    {
        return !ReadBool(card, "visible", true) || ReadBool(card, "faceDown", false);
    }

    private static void RenderPlayerSummary(
        Label target,
        string sideLabel,
        CardDictionary player,
        bool showHiddenHand)
    {
        var parts = new List<string>
        {
            sideLabel,
            $"分数 {ReadInt(player, "score")}",
            $"主牌 {ReadInt(player, "mainDeckCount")}",
            $"符文 {ReadInt(player, "runeDeckCount")}/12"
        };
        if (showHiddenHand)
        {
            parts.Add($"手牌 {Math.Max(ReadInt(player, "handHiddenCount"), ReadCards(player, "hand").Count)}");
        }

        target.Text = string.Join("  ·  ", parts);
    }

    private static BattlefieldNodes ReadBattlefieldNodes(
        MatchScreen screen,
        string battlefieldName,
        string statePath)
    {
        var root = $"MatchLayout/Battlefields/{battlefieldName}/LaneContent";
        return new BattlefieldNodes(
            screen.GetNode<Container>($"{root}/OpponentUnits"),
            screen.GetNode<Container>($"{root}/CenterRow/OfficialSite"),
            screen.GetNode<Container>($"{root}/SelfUnits"),
            screen.GetNode<Container>($"{root}/CenterRow/Standby"),
            screen.GetNode<Label>(statePath));
    }

    private static void ClearChildren(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.Free();
        }
    }

    private static Label SecondaryLabel(string text)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
        };
        label.AddThemeColorOverride("font_color", MinimalTheme.TextSecondary);
        label.AddThemeFontSizeOverride("font_size", 14);
        return label;
    }

    private static CardDictionary ReadDictionary(CardDictionary source, string key)
    {
        return source.TryGetValue(key, out var value)
            ? value.AsGodotDictionary()
            : new CardDictionary();
    }

    private static CardArray ReadCards(CardDictionary source, string key)
    {
        return source.TryGetValue(key, out var value)
            ? value.As<CardArray>()
            : [];
    }

    private static string ReadString(CardDictionary source, string key)
    {
        return source.TryGetValue(key, out var value) ? value.AsString() : string.Empty;
    }

    private static int ReadInt(CardDictionary source, string key, int fallback = 0)
    {
        return source.TryGetValue(key, out var value) ? value.AsInt32() : fallback;
    }

    private static bool ReadBool(CardDictionary source, string key, bool fallback)
    {
        return source.TryGetValue(key, out var value) ? value.AsBool() : fallback;
    }

    private static readonly (string Key, string Label)[] PublicZones =
    [
        ("legend", "传奇"),
        ("hero", "英雄"),
        ("base", "基地"),
        ("baseRunes", "基地符文"),
        ("graveyard", "弃牌"),
        ("banished", "放逐")
    ];

    private sealed record BattlefieldNodes(
        Container OpponentUnits,
        Container OfficialSite,
        Container SelfUnits,
        Container Standby,
        Label State);

    private sealed record CardBinding(
        OfficialCardView View,
        CardDictionary Card,
        OfficialCardVisualState RestingState);
}
