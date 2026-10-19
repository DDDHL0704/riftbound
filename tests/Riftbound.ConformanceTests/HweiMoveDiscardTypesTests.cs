using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class HweiMoveDiscardTypesTests
{
    internal const string Hwei = "UNL-080/219";
    internal const string Dual = "VEN·058";
    private static readonly Lazy<OfficialCardCatalog> Catalog = new(() => OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult());

    [Fact]
    public void WholePrintedAbilityHasOneImplementedMovementTriggerWithAllThreeInlineEffects()
    {
        var card = Catalog.Value.Cards.Single(c => c.CardNo == Hwei);
        var trigger = Assert.Single(RuleTextParser.Parse(card).Triggers);
        Assert.Equal(TriggerKinds.UnitMovedDrawDiscardTypes, trigger.Kind);
        Assert.Equal(TriggerTimings.UnitMoved, trigger.Timing);
        Assert.Equal(1, trigger.DrawCount); Assert.Equal(1, trigger.DiscardCount);
        Assert.Equal(2, trigger.RuneReadyCount); Assert.Equal(3, trigger.PowerDelta);
        Assert.Equal(TriggerDurations.UntilEndOfTurn, trigger.Duration);
    }

    [Theory]
    [InlineData("OGN·083/298", 4, 1, 5)]
    [InlineData("OGN·072/298", 2, 1, 5)]
    [InlineData("OGN·096/298", 1, 1, 8)]
    [InlineData(Dual, 3, 2, 8)]
    public async Task DrawsBeforeOfferingAllHandCardsThenCreatesEveryMatchingType(string discardedNo, int types, int branches, int might)
    {
        var initial = Position(discardedNo);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await Parent(driver);
        var pending = driver.State.PendingHandChoice!;
        Assert.Equal(initial.PlayerZones["P1"].Hand.Append("D0"), driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(new[] { "D1" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("D0", pending.LegalObjectIds); Assert.Equal(0, pending.DrawCount);
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.DoesNotContain('"' + "D0" + '"', JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        Assert.DoesNotContain('"' + "D0" + '"', JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P2"]));
        await Discard(driver, "HAND");
        var captured = driver.State.TriggerQueue.Select(t => t.MovementObserver?.DiscardInline)
            .Concat(driver.State.StackItems.Select(s => s.MovementObserver?.DiscardInline)).Where(c => c is not null).ToArray();
        Assert.Equal(branches, captured.Length); Assert.All(captured, c => Assert.Equal(types, c!.Types));
        Assert.All(captured, c => Assert.Equal(new ObjectBinding("HAND", 0), c!.DiscardedCard));
        await Finish(driver, false, 2);
        Assert.Equal(might, Might(driver.State)); Assert.Contains("HAND", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(types == 4 ? 2 : 1, driver.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Equal((types & 2) == 0, driver.State.CardObjects["R0"].IsExhausted);
        Assert.Equal((types & 2) == 0, driver.State.CardObjects["R1"].IsExhausted);
        var events = driver.Events().Select(e => e.Event.Kind).ToArray();
        Assert.True(Array.IndexOf(events, "CARD_DRAWN") < Array.IndexOf(events, "CARD_DISCARDED"));
        Assert.Equal(JsonSerializer.Serialize(initial.CardPlays), JsonSerializer.Serialize(driver.State.CardPlays));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task RealDualTypeHasBothControllerOrdersAndZeroOneOrTwoRuneTargets(bool reverse, int selected)
    {
        var initial = Position(); var driver = new StandbyDefendObserverTests.Driver(initial);
        await Parent(driver); await Discard(driver, "D0");
        Assert.Equal(new[] { 1, 2 }, driver.State.TriggerQueue.Select(t => t.MovementObserver!.DiscardInline!.BranchType).Order());
        var ordered = driver.State.TriggerQueue.OrderBy(t => t.MovementObserver!.DiscardInline!.BranchType).ToArray();
        if (reverse) Array.Reverse(ordered);
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ordered.Select(t => t.TriggerId).ToArray()));
        var branchesByStackId = driver.State.StackItems.ToDictionary(s => s.StackItemId, s => s.MovementObserver!.DiscardInline!.BranchType);
        await Finish(driver, reverse, selected);
        Assert.Equal(reverse ? new[] { 2, 1 } : new[] { 1, 2 }, driver.Events().Where(e => e.Event.Kind == "TRIGGER_RESOLVED")
            .Select(e => JsonSerializer.SerializeToElement(e.Event.Payload))
            .Where(p => p.GetProperty("effectKind").GetString() == "MOVEMENT_DISCARD_TYPE_INLINE")
            .Select(p => branchesByStackId[p.GetProperty("triggerId").GetString()!]));
        Assert.Equal(8, Might(driver.State));
        Assert.Equal(selected, new[] { "R0", "R1" }.Count(id => !driver.State.CardObjects[id].IsExhausted));
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Contains("D0", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    internal static async Task Parent(StandbyDefendObserverTests.Driver driver)
    {
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F"));
        Assert.Single(driver.State.StackItems);
        await driver.Top();
        Assert.Equal(TriggerKinds.UnitMovedDrawDiscardTypes, driver.State.PendingHandChoice!.EffectKind);
    }

    internal static Task<ResolutionResult> Discard(StandbyDefendObserverTests.Driver driver, string card)
    {
        var pending = driver.State.PendingHandChoice!;
        return driver.Act(pending.PlayerId, new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, [card]));
    }

    internal static async Task Finish(StandbyDefendObserverTests.Driver driver, bool reverse = false, int selected = 2)
    {
        for (var i = 0; i < 32 && (driver.State.StackItems.Count > 0 || driver.State.TriggerQueue.Count > 0 || driver.State.PendingCardChoice is not null); i++)
        {
            if (driver.State.TriggerQueue.Count > 0)
            {
                var ordered = driver.State.TriggerQueue.OrderBy(t => t.MovementObserver?.DiscardInline?.BranchType ?? 0).ToArray();
                if (reverse) Array.Reverse(ordered);
                await driver.Act(ordered[0].ControllerId, new OrderTriggersCommand(OrderedTriggerIds: ordered.Select(t => t.TriggerId).ToArray()));
            }
            else if (driver.State.PendingCardChoice is { } choice)
                await driver.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, choice.LegalObjectIds.Take(selected).ToArray()));
            else if (driver.State.PriorityPlayerId is { } priority) await driver.Act(priority, new PassPriorityCommand());
            else throw new InvalidOperationException("Unexpected Hwei pending window.");
        }
        Assert.Empty(driver.State.StackItems); Assert.Empty(driver.State.TriggerQueue); Assert.Null(driver.State.PendingCardChoice);
    }

    internal static MatchState Position(string handCard = Dual, int deckCount = 2, int runeCount = 2)
    {
        var cards = new Dictionary<string,CardObjectState> {
            ["HOST"] = Card("HOST", Hwei), ["HAND"] = Card("HAND", handCard),
            ["F"] = Card("F", "OGN·278/298"), ["OTHER"] = Card("OTHER", "OGN·294/298", "P2") };
        var positions = new Dictionary<string,ObjectLocationState> {
            ["HOST"] = new("P1", "BASE"), ["HAND"] = new("P1", "HAND"),
            ["F"] = new("P1", "BATTLEFIELD", "F"), ["OTHER"] = new("P2", "BATTLEFIELD", "OTHER") };
        var deck = Enumerable.Range(0, deckCount).Select(i => "D" + i).ToArray();
        foreach (var id in deck) { cards[id] = Card(id, id == "D0" ? Dual : "OGN·096/298"); positions[id] = new("P1", "MAIN_DECK"); }
        var runes = Enumerable.Range(0, runeCount).Select(i => "R" + i).ToArray();
        foreach (var id in runes) { cards[id] = Card(id, "SFD·R03") with { IsExhausted = true }; positions[id] = new("P1", "BASE"); }
        return new("CONTROLLED-HWEI-TYPES", 0, 5, "P1", new Dictionary<string,string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            cardObjects: cards, objectLocations: positions, runePools: new Dictionary<string,RunePool> { ["P1"] = new(20,20), ["P2"] = new(20,20) },
            playerZones: new Dictionary<string,PlayerZones> { ["P1"] = PlayerZones.Empty with { Hand = ["HAND"], MainDeck = deck, Base = runes.Prepend("HOST").ToArray(), Battlefields = ["F"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["OTHER"] } }) { CardPlays = new(5, [], []) };
    }

    internal static CardObjectState Card(string id, string cardNo, string owner = "P1")
    {
        var card = Catalog.Value.Cards.Single(c => c.CardNo == cardNo); var tags = new List<string>();
        if (card.CardCategoryNameList.Any(c => c.Contains("单位", StringComparison.Ordinal))) tags.Add(CardObjectTags.UnitCard);
        if (card.CardCategoryNameList.Any(c => c.Contains("装备", StringComparison.Ordinal))) tags.Add(CardObjectTags.EquipmentCard);
        if (card.CardCategoryNameList.Any(c => c.Contains("法术", StringComparison.Ordinal))) tags.Add(CardObjectTags.SpellCard);
        if (card.CardCategoryNameList.Contains("符文")) tags.Add(CardObjectTags.RuneCard);
        if (card.CardCategoryNameList.Contains("战场")) tags.Add(P6TokenFactoryCatalog.BattlefieldCardTag);
        tags.AddRange(card.CardColorList.Select(color => "COLOR:" + color));
        return new(id, cardNo: cardNo, power: card.Power.GetValueOrDefault(), manaCost: card.Energy.GetValueOrDefault(),
            ownerId: owner, controllerId: owner, tags: tags);
    }

    internal static int Might(MatchState state)
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty("P1").GetProperty("objects")
            .GetProperty("HOST").GetProperty("effectivePower").GetInt32();
}
