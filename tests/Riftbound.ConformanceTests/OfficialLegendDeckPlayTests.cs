using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialLegendDeckPlayTests
{
    internal static MatchState Position(string no = "SFD·187/221", bool exhausted = false, params string[] top)
    {
        if (top.Length == 0) top = ["SFD·125/221", "SFD·022/221"];
        var s = OfficialLegendConquestTests.Position(no, exhausted, mana: 20);
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects);
        var ids = top.Select((_, i) => "DECK" + i).ToArray();
        foreach (var (cardNo, i) in top.Select((v, i) => (v, i))) {
            var b = CardBehaviorRegistry.GetAll().First(b => b.CardNo == cardNo);
            cards[ids[i]] = new(ids[i], cardNo: cardNo, ownerId: "P1", controllerId: "P1", tags:
                [b.PlaysSourceToBaseAsUnit ? CardObjectTags.UnitCard : b.PlaysSourceToBaseAsEquipment ? CardObjectTags.EquipmentCard : CardObjectTags.SpellCard]);
        }
        cards["TAIL"] = new("TAIL", cardNo: "SFD·125/221", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
        return s with { CardObjects = cards, RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(20,10) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { MainDeck = ids.Append("TAIL").ToArray() } } };
    }
    internal static Task<ResolutionResult> Confirm(MatchState state, bool pay) => Act(state, "P1", new ChooseCardsCommand(
        state.PendingCardChoice!.ChoiceId, state.PendingCardChoice.ChoiceWindow, pay ? ["LEGEND"] : []));
    internal static async Task<ResolutionResult> Open(MatchState s) {
        var captured = await ConquestLifecycleRegressionTests.Conquer(s);
        var paid = await Confirm(captured.State, true);
        return await Top(paid.State);
    }
    [Theory]
    [InlineData("SFD·187/221")]
    [InlineData("SFD·243/221")]
    public async Task ConquestFirstOffersCostAndDoesNotRevealOrPlay(string no)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position(no));
        Assert.NotNull(opened.State.PendingCardChoice);
        Assert.False(opened.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(["DECK0", "DECK1", "TAIL"], opened.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(opened.Events, e => e.Kind == "CARDS_REVEALED"); Restore(opened.State);
    }
    [Fact]
    public async Task ExhaustedLegendStillCapturesTriggerThenCannotConfirm()
    {
        var done = await ConquestLifecycleRegressionTests.Conquer(Position(exhausted: true));
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_QUEUED");
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Equal(["DECK0", "DECK1", "TAIL"], done.State.PlayerZones["P1"].MainDeck); Restore(done.State);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExhaustionIsChosenBeforeResponsesAndRevealComesLater(bool pay)
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(Position());
        var confirmed = await Confirm(opened.State, pay); Restore(confirmed.State);
        Assert.Equal(pay, confirmed.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain(confirmed.Events, e => e.Kind == "CARDS_REVEALED");
        Assert.DoesNotContain("DECK0", JsonSerializer.Serialize(confirmed.Snapshots["P2"]));
        if (!pay) { Assert.Empty(confirmed.State.StackItems); return; }
        var shown = await Top(confirmed.State); Restore(shown.State);
        Assert.Equal(["DECK0", "DECK1"], shown.State.PendingEffectPlay!.Sources.Keys);
        Assert.Contains("DECK0", JsonSerializer.Serialize(shown.Snapshots["P1"]));
        Assert.Contains("DECK1", JsonSerializer.Serialize(shown.Snapshots["P2"]));
        Assert.DoesNotContain("TAIL", JsonSerializer.Serialize(shown.Snapshots["P2"]));
        Assert.Contains("DECK0", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(shown.State)));
        Assert.DoesNotContain("TAIL", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(shown.State)));
        Assert.Equal(2, Assert.Single(shown.Events, e => e.Kind == "CARDS_REVEALED").Payload["count"]);
    }

    [Theory]
    [InlineData("SFD·125/221", false)]
    [InlineData("SFD·042/221", false)]
    [InlineData("OGN·048/298", true)]
    public async Task CanChooseSecondUnitEquipmentOrSpellAndPaysNormalCost(string no, bool spell)
    {
        var opened = await Open(Position(top: ["SFD·125/221", no]));
        var command = new PlayCardCommand("DECK1", no, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(opened.State, "P1", PlayCostPreviewTests.Request(opened.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.True(quote.Cost!.Mana > 0);
        var done = await Act(opened.State, "P1", command); Restore(done.State);
        Assert.Equal(opened.State.RunePools["P1"].Mana - quote.Cost.Mana, done.State.RunePools["P1"].Mana);
        Assert.Null(done.State.PendingEffectPlay); Assert.Equal(["TAIL", "DECK0"], done.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(1, Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED").Payload["count"]);
        Assert.DoesNotContain("DECK0", JsonSerializer.Serialize(done.Snapshots["P2"]));
        if (spell) {
            Assert.Single(done.State.StackItems); Assert.DoesNotContain("DECK1", done.State.PlayerZones["P1"].Graveyard);
            var resolved = await Top(done.State); Assert.Contains("DECK1", resolved.State.PlayerZones["P1"].Graveyard); Restore(resolved.State);
        } else {
            Assert.Contains("DECK1", done.State.PlayerZones["P1"].Base);
            if (no == "SFD·125/221") Assert.True(done.State.CardObjects["DECK1"].IsExhausted);
        }
    }

    [Fact]
    public async Task DecliningAfterRevealRecyclesAllAndWithdrawsPublicVisibility()
    {
        var opened = await Open(Position()); var p = opened.State.PendingEffectPlay!;
        var done = await Act(opened.State, "P1", new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []));
        Assert.Equal("TAIL", done.State.PlayerZones["P1"].MainDeck[0]); Assert.Equal(3, done.State.PlayerZones["P1"].MainDeck.Count);
        Assert.True(done.State.CardObjects["LEGEND"].IsExhausted); Assert.Empty(done.State.StackItems); Restore(done.State);
        Assert.DoesNotContain("DECK0", JsonSerializer.Serialize(done.Snapshots["P2"]));
        Assert.Equal(opened.State.RngCursor + 1, done.State.RngCursor);
        Assert.DoesNotContain("DECK0", JsonSerializer.Serialize(ResolutionResult.BuildSpectatorSnapshot(done.State)));
        Assert.DoesNotContain("cardIds", Assert.Single(done.Events, e => e.Kind == "CARDS_RECYCLED").Payload.Keys);
    }

    [Fact]
    public async Task NoFreePlayAndNoChoiceFromBelowRevealedCards()
    {
        var opened = await Open(Position(top: ["SFD·143/221", "SFD·022/221"]));
        foreach (var command in new[] { new PlayCardCommand("TAIL", "SFD·125/221", []), new PlayCardCommand("DECK0", "SFD·143/221", []) }) {
            var s = opened.State with { RunePools = new Dictionary<string,RunePool>(opened.State.RunePools) { ["P1"] = new(20,0) } };
            var bad = await new CoreRuleEngine().ResolveAsync(s, new("bad", "P1", CommandTypes.PlayCard), command, default);
            Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(bad.State));
        }
        var emptyPool = opened.State with { RunePools = new Dictionary<string,RunePool>(opened.State.RunePools) { ["P1"] = RunePool.Empty } };
        var rejected = await new CoreRuleEngine().ResolveAsync(emptyPool, new("poor", "P1", CommandTypes.PlayCard), new PlayCardCommand("DECK1", "SFD·022/221", []), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(emptyPool), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task NormalSpellProhibitionAndExtraCostsStillApply()
    {
        var opened = await Open(Position(top: ["OGN·048/298", "OGN·208/298"]));
        var s = opened.State with { UntilEndOfTurnEffects = [CardPermissionKeywordRules.SpellPlayProhibitionPrefix + "P1"] };
        foreach (var command in new[] { new PlayCardCommand("DECK0", "OGN·048/298", []), new PlayCardCommand("DECK1", "OGN·208/298", []) }) {
            var quote = new CoreRuleEngine().PreviewPlayCard(s, "P1", PlayCostPreviewTests.Request(s, command)); Assert.False(quote.CanPay);
            var bad = await new CoreRuleEngine().ResolveAsync(s, new("blocked", "P1", command.CmdType), command, default);
            Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(bad.State));
        }
    }

    [Fact]
    public async Task PaidAbilityUsesResolutionTimeDeckAndSurvivesSourceChange()
    {
        var captured = await ConquestLifecycleRegressionTests.Conquer(Position()); var paid = await Confirm(captured.State, true);
        var s = paid.State with { CardObjects = new Dictionary<string, CardObjectState>(paid.State.CardObjects) {
            ["LEGEND"] = paid.State.CardObjects["LEGEND"] with { ControllerId = "P2", ObjectGeneration = 2 } },
            PlayerZones = new Dictionary<string,PlayerZones>(paid.State.PlayerZones) { ["P1"] = paid.State.PlayerZones["P1"] with { MainDeck = ["TAIL", "DECK1", "DECK0"] } } };
        var opened = await Top(s); Assert.Equal(["TAIL", "DECK1"], opened.State.PendingEffectPlay!.ViewedCardIds); Restore(opened.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShortDeckFinishesWithoutInventingCards(int count)
    {
        var s = Position(); s = s with { PlayerZones = new Dictionary<string,PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { MainDeck = count == 0 ? [] : ["DECK0"] } } };
        var opened = await Open(s);
        if (count == 0) { Assert.Null(opened.State.PendingEffectPlay); Assert.Empty(opened.State.StackItems); }
        else { Assert.Equal(["DECK0"], opened.State.PendingEffectPlay!.ViewedCardIds); }
        Restore(opened.State);
    }

    [Fact]
    public async Task UnitMayEnterControlledBattlefieldAndPlayAbilityWaitsForResponses()
    {
        var opened = await Open(Position(top: ["SFD·058/221", "SFD·022/221"]));
        var done = await Act(opened.State, "P1", new PlayCardCommand("DECK0", "SFD·058/221", [], Destination: "BATTLEFIELD:BF"));
        Assert.Equal("BF", done.State.ObjectLocations["DECK0"].BattlefieldObjectId);
        Assert.Single(done.State.StackItems); Assert.True(done.State.StackItems[0].SourceConfirmed); Restore(done.State);
        var resolved = await Top(done.State); Assert.NotNull(resolved.State.PendingCardChoice); Restore(resolved.State);
    }

    [Fact]
    public async Task RecoveryRejectsFreePlayWrongSourcesMissingReceiptAndOtherActor()
    {
        var opened = await Open(Position()); var p = opened.State.PendingEffectPlay!;
        foreach (var forged in new[] { p with { IgnoreBaseMana = true }, p with { PlayerId = "P2" },
            p with { Sources = new Dictionary<string,long>() }, p with { Parent = p.Parent with { TriggerCost = null } },
            p with { Parent = p.Parent with { LegendConquest = null } }, p with { ViewedCardIds = ["TAIL"] } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(opened.State with { PendingEffectPlay = forged }));
        var bad = await new CoreRuleEngine().ResolveAsync(opened.State, new("other", "P2", CommandTypes.ChooseCards), new ChooseCardsCommand(p.ChoiceId, "EFFECT_PLAY", []), default);
        Assert.False(bad.Accepted); Assert.Equal(MatchStateHasher.Hash(opened.State), MatchStateHasher.Hash(bad.State));
    }

    [Fact]
    public async Task EchoStillPaysExtraCostAndChildSpellResolvesAfterRecycling()
    {
        var opened = await Open(Position(top: ["OGN·048/298", "SFD·042/221"]));
        var state = opened.State with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var engine = new CoreRuleEngine(); var plain = new PlayCardCommand("DECK0", "OGN·048/298", []);
        var echo = plain with { OptionalCosts = ["ECHO"] };
        var baseQuote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, plain));
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, echo));
        Assert.True(quote.CanPay, quote.Message); Assert.True(quote.Cost!.Mana > baseQuote.Cost!.Mana);
        var played = await Act(state, "P1", echo);
        Assert.Equal(["TAIL", "DECK1"], played.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(20 - quote.Cost.Mana, played.State.RunePools["P1"].Mana); Restore(played.State);
        var done = await Top(played.State);
        Assert.Equal(2, done.Events.Count(e => e.Kind == "SPELL_EXECUTION_COMPLETED"));
        Assert.Equal(["TAIL", "DECK1"], done.State.PlayerZones["P1"].Hand); Restore(done.State);
    }

}
