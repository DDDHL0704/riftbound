using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;
using static Riftbound.ConformanceTests.OfficialTokenReplacementTests;

namespace Riftbound.ConformanceTests;

public sealed class OfficialLeblancCreationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MixedTriggerFamiliesConfirmInInsertionOrder(bool costFirst)
    {
        var s = (await OpenCost(Position())).State;
        var cost = s.StackItems.Single();
        s = s with { PendingCardChoice = null, StackItems = [],
            TriggerQueue = [new("COST", "P1", "LEGEND", cost.EffectKind, "BATTLEFIELD_CONQUERED") { HeldContext = cost.HeldContext },
                new("FIELD", "P1", "F", "FIELD_TARGETED_TRIGGER", "SECOND_CARD_DRAWN") { FieldContext = new("UNL-074/219", "SECOND_DRAW", 0) }],
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["F"] = new("F", cardNo: "UNL-074/219", power: 3, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = s.PlayerZones["P1"].Base.Append("F").ToArray() } } };
        var ordered = await Act(s, "P1", new OrderTriggersCommand(OrderedTriggerIds: costFirst ? ["FIELD", "COST"] : ["COST", "FIELD"]));
        Assert.Equal(costFirst ? "TRIGGER_COST_CONFIRMATION" : "TRIGGER_CONFIRMATION", ordered.State.PendingCardChoice!.ChoiceWindow);
        Assert.Null(ordered.State.PriorityPlayerId); Restore(ordered.State);
        var next = await Choose(ordered.State, costFirst ? "H2" : "UNIT");
        Assert.Equal(costFirst ? "TRIGGER_CONFIRMATION" : "TRIGGER_COST_CONFIRMATION", next.State.PendingCardChoice!.ChoiceWindow);
        Assert.Null(next.State.PriorityPlayerId); Restore(next.State);
        next = await Choose(next.State, costFirst ? "UNIT" : "H2");
        Assert.Null(next.State.PendingCardChoice); Assert.Equal("P1", next.State.PriorityPlayerId); Restore(next.State);
    }

    [Fact]
    public async Task BattlefieldProhibitionStopsEntryWithoutSpendingReplacementUse()
    {
        var s = Position(1);
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BF"] = s.CardObjects["BF"] with { CardNo = "SFD·216/221" } } };
        var done = await Top((await Choose((await OpenCost(s)).State, "H2")).State);
        Assert.Empty(Tokens(done.State)); Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Contains("H2", done.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", done.State.CardObjects["Z1"].UntilEndOfTurnEffects); Restore(done.State);
    }

    [Fact]
    public async Task NoCopyTargetDoesNotPreventOuterCreationAndCannotInventAnInnerTarget()
    {
        var paid = await Choose((await OpenCost(WithGust(Position()))).State, "H2");
        Assert.Empty(Tokens(paid.State));
        var passed = await Act(paid.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        var returned = await Top(response.State); Assert.Contains("UNIT", returned.State.PlayerZones["P1"].Hand);
        var done = await Top(returned.State);
        var token = Assert.Single(Tokens(done.State)); Assert.Equal(0, token.Power);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingCardChoice);
        Assert.Contains(done.Events, e => e.Kind == "TRIGGER_NOT_CONFIRMED"); Restore(done.State);
    }

    [Fact]
    public async Task InnerCopyPaysEnemyWardAndRechecksTheCapturedBattlefieldAtResolution()
    {
        var pending = await OpenCost(Position(1)); var s = pending.State;
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { Battlefields = s.PlayerZones["P2"].Battlefields.Append("WARD").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["WARD"] = new("WARD", cardNo: "UNL·T02", ownerId: "P2", controllerId: "P2", power: 1, tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["WARD"] = new("P2", "BATTLEFIELD", "BF") } };
        var created = await Choose((await Top((await Choose(s, "H2")).State)).State); // Keep Zilean unused.
        Assert.Contains("WARD", created.State.PendingCardChoice!.LegalObjectIds);
        var selected = await Choose(created.State, "WARD"); Restore(selected.State);
        var payment = selected.State.PendingPayment!;
        var confirmed = await Act(selected.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        Assert.Equal(29, confirmed.State.RunePools["P1"].TotalPower); Restore(confirmed.State);
        var moved = confirmed.State with { ObjectLocations = new Dictionary<string, ObjectLocationState>(confirmed.State.ObjectLocations) {
            ["WARD"] = new("P2", "BATTLEFIELD", "OTHER") } };
        var done = await Top(moved);
        Assert.Equal(0, Assert.Single(Tokens(done.State)).Power); Restore(done.State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ConquestPaysChosenCostThenCreatesBeforeSelectingCopy(int replacements)
    {
        var conquered = await ConquestLifecycleRegressionTests.Conquer(Position(replacements));
        Assert.Empty(Tokens(conquered.State)); Assert.Equal(3, conquered.State.PlayerZones["P1"].Hand.Count);
        Assert.False(conquered.State.CardObjects["LEGEND"].IsExhausted); Assert.Single(conquered.State.StackItems); Restore(conquered.State);
        var pending = conquered;
        Assert.Equal("TRIGGER_COST_CONFIRMATION", pending.State.PendingCardChoice!.ChoiceWindow);
        Assert.DoesNotContain("H2", JsonSerializer.Serialize(pending.Prompts["P2"])); Restore(pending.State);
        var paid = await Choose(pending.State, "H2");
        Assert.Contains("H1", paid.State.PlayerZones["P1"].Hand); Assert.Contains("H2", paid.State.PlayerZones["P1"].Graveyard);
        Assert.True(paid.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Single(paid.Events, e => e.Kind == "CARD_DISCARDED"); Restore(paid.State);
        Assert.Empty(Tokens(paid.State)); Assert.Null(paid.State.PendingCardChoice); Assert.Equal("P1", paid.State.PriorityPlayerId);
        paid = await Top(paid.State);
        for (var i = replacements; i > 0; i--) paid = await Choose(paid.State, "Z" + i);
        Assert.Equal(replacements + 1, Tokens(paid.State).Length);
        Assert.All(Tokens(paid.State), t => { Assert.Equal(0, t.Power); Assert.False(t.IsExhausted);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, t.Tags); Assert.Equal("BF", paid.State.ObjectLocations[t.ObjectId].BattlefieldObjectId); });
        var copy = paid.State.PendingCardChoice!;
        Assert.Equal("TRIGGER_CONFIRMATION", copy.ChoiceWindow); Assert.Equal(["UNIT"], copy.LegalObjectIds);
        Assert.Null(paid.State.PriorityPlayerId); Restore(paid.State);
        var confirmed = await Choose(paid.State, "UNIT");
        Assert.Null(confirmed.State.PendingCardChoice); Assert.Equal("P1", confirmed.State.PriorityPlayerId); Restore(confirmed.State);
        var done = await Top(confirmed.State);
        Assert.All(Tokens(done.State), t => { Assert.Equal("OGN·096/298", t.CardNo); Assert.Equal(1, t.Power); Assert.Contains(CardObjectTags.Ephemeral, t.Tags); });
        Assert.Equal(2, done.State.PlayerZones["P1"].Hand.Count); Restore(done.State);
    }

    [Fact]
    public async Task DeclinePaysNothingAndDoesNotCreateOrConsumeReplacement()
    {
        var pending = await OpenCost(Position(1));
        var done = await Choose(pending.State);
        Assert.Empty(Tokens(done.State)); Assert.Equal(3, done.State.PlayerZones["P1"].Hand.Count);
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted);
        Assert.DoesNotContain("TOKEN_ENTRY_REPLACEMENT_USED", done.State.CardObjects["Z1"].UntilEndOfTurnEffects); Restore(done.State);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("exhausted")]
    [InlineData("controller")]
    public async Task UnavailableConfirmationCostDoesNotPublishAConfirmedSkill(string reason)
    {
        var s = Position();
        if (reason == "empty") s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P1"] = s.PlayerZones["P1"] with { Hand = [], Graveyard = s.PlayerZones["P1"].Hand } } };
        else s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["LEGEND"] = s.CardObjects["LEGEND"] with { IsExhausted = reason == "exhausted", ControllerId = reason == "controller" ? "P2" : "P1" } } };
        var done = await ConquestLifecycleRegressionTests.Conquer(s);
        Assert.Null(done.State.PendingCardChoice); Assert.Empty(Tokens(done.State));
        Assert.DoesNotContain(done.Events, e => e.Kind == "CARD_DISCARDED");
    }

    [Fact]
    public async Task OpponentCanReturnCopySourceInReflexiveResponseWindow()
    {
        var initial = WithGust(Position());
        var paid = await Top((await Choose((await OpenCost(initial)).State, "H2")).State);
        var confirmed = await Choose(paid.State, "UNIT");
        var passed = await Act(confirmed.State, "P1", new PassPriorityCommand());
        var response = await Act(passed.State, "P2", new PlayCardCommand("GUST", "OGN·169/298", ["UNIT"]));
        var returned = await Top(response.State); Assert.Contains("UNIT", returned.State.PlayerZones["P1"].Hand); Restore(returned.State);
        var done = await Top(returned.State);
        var image = Assert.Single(Tokens(done.State)); Assert.Equal(0, image.Power); Assert.Equal(P6TokenFactoryCatalog.ImageTokenCardNo, image.CardNo);
        Assert.Contains(CardObjectTags.Ephemeral, image.Tags); Restore(done.State);
    }

    [Fact]
    public async Task OfficialZileanCopyThenMirrorUsesOnlyTwoNewUnusedSources()
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { CardNo = "UNL-086/219", Power = 5 } } };
        var paid = await Top((await Choose((await OpenCost(initial)).State, "H2")).State);
        paid = await Choose(paid.State, "UNIT"); // Original Zilean replaces the entry.
        var copied = await Top((await Choose(paid.State, "UNIT")).State);
        var newSources = Tokens(copied.State).Select(c => c.ObjectId).ToArray(); Assert.Equal(2, newSources.Length); Restore(copied.State);
        var s = OfficialCopyIdentityTests.AddSpell(copied.State, "MIRROR", "UNL-200/219");
        var cast = await Act(s, "P1", new PlayCardCommand("MIRROR", "UNL-200/219", ["UNIT"]));
        var opened = await Top(cast.State);
        Assert.Equal(newSources.Order(), opened.State.PendingCardChoice!.LegalObjectIds.Order());
        var done = await Choose((await Choose(opened.State, newSources[1])).State, newSources[0]);
        Assert.Equal(5, Tokens(done.State).Length); done = await Top(done.State); Restore(done.State);
    }

    [Fact]
    public async Task HoldUsesSameCostAndCopyFlowBeforeNormalDraw()
    {
        var s = Position();
        s = s with { Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed,
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["BF"] = s.CardObjects["BF"] with { ControllerId = "P1" } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = ["RUNE", "RUNE2"], Battlefields = ["UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") } };
        var start = await Act(s, "P1", new PassPriorityCommand());
        Assert.Single(start.State.StackItems); Assert.Equal(3, start.State.PlayerZones["P1"].Hand.Count);
        var paid = await Top((await Choose(start.State, "H2")).State); Restore(paid.State);
        Assert.Equal(MatchPhases.TurnStart, paid.State.Phase); Assert.Equal(2, paid.State.PlayerZones["P1"].Hand.Count);
        var done = await Top((await Choose(paid.State, "UNIT")).State);
        Assert.Equal(MatchPhases.Main, done.State.Phase); Assert.Equal(3, done.State.PlayerZones["P1"].Hand.Count);
        Assert.Single(Tokens(done.State)); Restore(done.State);
    }

    [Fact]
    public async Task CostAndTargetRejectWrongPlayerStaleChoiceAndWrongLocation()
    {
        var pending = await OpenCost(Position(1)); var choice = pending.State.PendingCardChoice!;
        var rejected = await new CoreRuleEngine().ResolveAsync(pending.State, new("wrong", "P2", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(pending.State), MatchStateHasher.Hash(rejected.State));
        var paid = await Choose(pending.State, "H2"); Restore(paid.State);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [paid.State.StackItems.Single() with { TriggerCost = null }] }), e => e.Contains("unconfirmed trigger cost"));
        paid = await Top(paid.State);
        var item = paid.State.StackItems.Single();
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(paid.State with { StackItems = [item with {
            TriggerCost = item.TriggerCost! with { Discarded = new("H1", 0) } }] }), e => e.Contains("cost receipt"));
        var created = await Choose(paid.State); var targetChoice = created.State.PendingCardChoice!;
        rejected = await new CoreRuleEngine().ResolveAsync(created.State, new("wrong-target", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(targetChoice.ChoiceId, targetChoice.ChoiceWindow, ["Z1"]), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(created.State), MatchStateHasher.Hash(rejected.State));
        rejected = await new CoreRuleEngine().ResolveAsync(created.State, new("old", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H2"]), default);
        Assert.False(rejected.Accepted); Restore(created.State);
        var copying = created.State.StackItems.Single();
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(created.State with { PendingCardChoice = null, PriorityPlayerId = "P1" }), e => e.Contains("reflexive copy"));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(created.State with { StackItems = [copying with {
            ReflexiveCopy = copying.ReflexiveCopy! with { CopySource = new("UNIT", 0), TargetConfirmed = true } }] }), e => e.Contains("reflexive copy"));
    }

    [Fact]
    public async Task ImagesQueueEntryObserversAndCopyTogetherAndKeepStunAfterCopy()
    {
        var s = Position(1);
        s = s with { PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
            ["P2"] = s.PlayerZones["P2"] with { Battlefields = ["BF", "VFIELD", "VEX"] } },
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["VFIELD"] = new("VFIELD", cardNo: "OGN·294/298", ownerId: "P2", controllerId: "P2", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
                ["VEX"] = new("VEX", cardNo: "UNL-150/219", power: 4, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) {
                ["VFIELD"] = new("P2", "BATTLEFIELD", "VFIELD"), ["VEX"] = new("P2", "BATTLEFIELD", "VFIELD") } };
        var paid = await Top((await Choose((await OpenCost(s)).State, "H2")).State);
        Assert.Empty(paid.State.TriggerQueue);
        var created = await Choose(paid.State, "Z1");
        Assert.Equal(3, created.State.TriggerQueue.Count); Assert.Null(created.State.PendingCardChoice); Restore(created.State);
        var ordered = await Act(created.State, "P1", new OrderTriggersCommand(OrderedTriggerIds:
            created.State.TriggerQueue.OrderBy(t => t.UnitEntryContext is null ? 1 : 0).Select(t => t.TriggerId).ToArray()));
        Assert.Equal("TRIGGER_CONFIRMATION", ordered.State.PendingCardChoice!.ChoiceWindow);
        var confirmed = await Choose(ordered.State, "UNIT");
        for (var i = 0; i < 3; i++) confirmed = await Top(confirmed.State);
        Assert.All(Tokens(confirmed.State), t => { Assert.Equal(1, t.Power); Assert.Contains("STUNNED", t.UntilEndOfTurnEffects);
            Assert.Contains("MOVEMENT_PROHIBITED:P1", t.UntilEndOfTurnEffects); }); Restore(confirmed.State);
    }

    private static MatchState WithGust(MatchState state) => state with {
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Hand = ["GUST"] } },
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GUST"] = new("GUST", cardNo: "OGN·169/298", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) } };

    internal static Task<ResolutionResult> OpenCost(MatchState state) => ConquestLifecycleRegressionTests.Conquer(state);
    internal static MatchState Position(int replacements = 0)
    {
        var s = ConquestLifecycleRegressionTests.State("OGN·296/298");
        var cards = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: "UNL-199/219", ownerId: "P1", controllerId: "P1"),
            ["UNIT"] = s.CardObjects["UNIT"] with { CardNo = "OGN·096/298", Power = 1 } };
        var locations = new Dictionary<string, ObjectLocationState> { ["BF"] = new("P2", "BATTLEFIELD", "BF") };
        var fields = new List<string>();
        if (replacements > 0) {
            cards["OTHER"] = new("OTHER", cardNo: "OGN·297/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]); fields.Add("OTHER"); locations["OTHER"] = new("P1", "BATTLEFIELD", "OTHER");
            for (var i = 1; i <= replacements; i++) {
                var id = "Z" + i; cards[id] = new(id, cardNo: "UNL-086/219", power: 5, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.UnitCard]);
                fields.Add(id); locations[id] = new("P1", "BATTLEFIELD", "OTHER"); } }
        return s with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["LEGEND"], Battlefields = fields } } };
    }
}
