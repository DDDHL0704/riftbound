using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SivirOptionalRecycleTests
{
    [Theory]
    [InlineData("SFD·203/221", false)]
    [InlineData("SFD·203/221", true)]
    [InlineData("SFD·250/221", false)]
    [InlineData("SFD·250/221", true)]
    public async Task BothPrintingsAllowDecliningOrPayingBeforeTheGoldResponseWindow(string card, bool pay)
    {
        var initial = ResourcePosition();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SIVIR"] = initial.CardObjects["SIVIR"] with { CardNo = card } } };
        var recycled = await Act(initial, "P1", new RecycleRuneCommand("RUNE")); Restore(recycled.State);
        var choice = recycled.State.PendingCardChoice!;
        Assert.Equal(["SIVIR"], choice.LegalObjectIds);
        var confirmed = await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []));
        Assert.Equal(pay, confirmed.State.CardObjects["SIVIR"].IsExhausted);
        Assert.DoesNotContain(confirmed.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED"); Restore(confirmed.State);
        if (!pay) { Assert.Empty(confirmed.State.StackItems); return; }
        var item = Assert.Single(confirmed.State.StackItems);
        Assert.Equal(new ObjectBinding("SIVIR", 0), item.TriggerCost!.Source);
        var done = await Top(confirmed.State);
        var id = Assert.Single(done.State.PlayerZones["P1"].Base);
        Assert.True(done.State.CardObjects[id].IsExhausted); Assert.Contains(CardObjectTags.EquipmentCard, done.State.CardObjects[id].Tags);
        Assert.Single(done.Events, e => e.Kind == "LEGEND_TRIGGER_RESOLVED"); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Fact]
    public async Task BatchedRuneResourceRecycleCapturesOneIndependentTriggerPerRune()
    {
        var initial = ResourcePosition();
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = ["RUNE", "RUNE2"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["RUNE2"] = initial.CardObjects["RUNE"] with { ObjectId = "RUNE2" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["RUNE2"] = new("P1", "BASE") } };
        var recycled = await Act(initial, "P1", new RecycleRuneCommand("RUNE", ["RUNE", "RUNE2"]));
        Assert.Equal(2, recycled.State.TriggerQueue.Count); Assert.False(recycled.State.CardObjects["SIVIR"].IsExhausted); Restore(recycled.State);
        var ordered = await Act(recycled.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: recycled.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
        var choice = ordered.State.PendingCardChoice!;
        var paid = await Act(ordered.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SIVIR"]));
        Assert.Single(paid.State.StackItems); Assert.Null(paid.State.PendingCardChoice); Restore(paid.State);
        var done = await Top(paid.State);
        Assert.Single(done.State.PlayerZones["P1"].Base); Assert.Empty(done.State.StackItems); Restore(done.State);
    }

    [Theory]
    [InlineData("EXHAUSTED")]
    [InlineData("OPPONENT")]
    [InlineData("HIDDEN")]
    public async Task UnpayableOrInapplicableSourcesDoNotAutomaticallyCreateGold(string kind)
    {
        var initial = ResourcePosition(); var source = initial.CardObjects["SIVIR"];
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["SIVIR"] = kind == "EXHAUSTED" ? source with { IsExhausted = true }
                : kind == "HIDDEN" ? source with { IsFaceDown = true } : source with { OwnerId = "P2", ControllerId = "P2" } } };
        var result = await Act(initial, "P1", new RecycleRuneCommand("RUNE"));
        Assert.DoesNotContain(result.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED"); Assert.Empty(result.State.PlayerZones["P1"].Base);
        Assert.Null(result.State.PendingCardChoice); Assert.Empty(result.State.StackItems); Assert.Empty(result.State.TriggerQueue);
    }

    [Theory]
    [InlineData("MISSING")]
    [InlineData("FACE")]
    [InlineData("GENERATION")]
    [InlineData("SOURCE")]
    public async Task RecoveryIndependentlyRejectsMissingAndForgedSivirBindings(string mutation)
    {
        var opened = await Act(ResourcePosition(), "P1", new RecycleRuneCommand("RUNE"));
        var item = Assert.Single(opened.State.StackItems); var context = item.SivirRuneRecycle!;
        var badItem = item with {
            SivirRuneRecycle = mutation == "MISSING" ? null : mutation == "FACE" ? context with { CardNo = "SFD·065/221" }
                : mutation == "GENERATION" ? context with { SourceGeneration = 99 } : context,
            SourceObjectId = mutation == "SOURCE" ? "UNKNOWN" : item.SourceObjectId };
        var bad = opened.State with { StackItems = [badItem] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(bad));
    }

    [Fact]
    public async Task TargetConditionalCounterCannotChooseUntargetedSivirGoldTrigger()
    {
        var initial = EquipChainTests.Response(ResourcePosition(), "SFD·045/221", "P2");
        var opened = await Act(initial, "P1", new RecycleRuneCommand("RUNE")); var choice = opened.State.PendingCardChoice!;
        var paid = await Act(opened.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SIVIR"]));
        var target = Assert.Single(paid.State.StackItems).StackItemId;
        var passed = await Act(paid.State, "P1", new PassPriorityCommand());
        var command = new PlayCardCommand("RESPONSE", "SFD·045/221", [target]);
        var rejected = await new CoreRuleEngine().ResolveAsync(passed.State, new("counter", "P2", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(passed.State), MatchStateHasher.Hash(rejected.State));
        var done = await Act(passed.State, "P2", new PassPriorityCommand());
        Assert.True(done.State.CardObjects["SIVIR"].IsExhausted);
        Assert.Single(done.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED"); Assert.Empty(done.State.StackItems); Restore(done.State);
    }
    [Fact]
    public async Task ActualRuneResourceRecycleLetsSivirDeclineBeforeExhaustionOrGold()
    {
        var initial = ResourcePosition();
        var recycled = await Act(initial, "P1", new RecycleRuneCommand("RUNE"));
        Assert.Contains("RUNE", recycled.State.PlayerZones["P1"].RuneDeck);
        Assert.False(recycled.State.CardObjects["SIVIR"].IsExhausted);
        Assert.DoesNotContain(recycled.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        var choice = Assert.IsType<PendingCardChoiceState>(recycled.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow); Assert.Equal(0, choice.RequiredCount);
        var declined = await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.False(declined.State.CardObjects["SIVIR"].IsExhausted); Assert.Empty(declined.State.StackItems); Restore(declined.State);
    }

    [Fact]
    public async Task PrintedPredictRuneRecycleDoesNotAutomaticallyChooseSivirPayment()
    {
        var initial = ConformanceFixtureRunnerTests.SivirRuneRecycleState("SFD·203/221", "SIVIR");
        var opened = await Top((await Act(initial, "P1", new PlayCardCommand("P1-SCRYING-SHELL", "UNL-161/219", []))).State);
        var viewed = opened.State.PendingCardChoice!;
        var recycled = await Act(opened.State, "P1", new ChooseCardsCommand(viewed.ChoiceId, viewed.ChoiceWindow, viewed.LegalObjectIds));
        Assert.False(recycled.State.CardObjects["SIVIR"].IsExhausted);
        Assert.DoesNotContain(recycled.Events, e => e.Kind == "EQUIPMENT_TOKEN_CREATED");
        var choice = Assert.IsType<PendingCardChoiceState>(recycled.State.PendingCardChoice);
        var declined = await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.False(declined.State.CardObjects["SIVIR"].IsExhausted); Assert.Empty(declined.State.StackItems);
    }

    [Theory]
    [InlineData("SFD·203/221", false)]
    [InlineData("SFD·203/221", true)]
    [InlineData("SFD·250/221", false)]
    [InlineData("SFD·250/221", true)]
    public async Task BattlefieldConquestRuneRecycleCapturesSivirOutsideSpellResolution(string card, bool pay)
    {
        var initial = ConformanceFixtureRunnerTests.BattlefieldConquerRecycleRuneState();
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["SIVIR"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SIVIR"] = new("SIVIR", cardNo: card, ownerId: "P1", controllerId: "P1") } };
        var recycled = await Act(initial, "P1", new DeclareBattleCommand("P1-BATTLEFIELD-THUNDER-RUNE",
            ["P1-BATTLEFIELD-RECYCLE-ATTACKER"], ["P2-BATTLEFIELD-RECYCLE-DEFENDER"], ["COMBAT_ASSIGNMENT"]));
        Assert.Contains(recycled.Events, e => e.Kind == "CARDS_RECYCLED");
        Assert.DoesNotContain(recycled.Events.SelectMany(e => e.Payload.Keys), key => key == "sivirRuneObservation");
        Assert.False(recycled.State.CardObjects["SIVIR"].IsExhausted);
        var choice = Assert.IsType<PendingCardChoiceState>(recycled.State.PendingCardChoice);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
        var confirmed = await Act(recycled.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []));
        Assert.Equal(pay, confirmed.State.CardObjects["SIVIR"].IsExhausted);
        Assert.Empty(confirmed.State.PlayerZones["P1"].Base);
        if (pay) Assert.Single((await Top(confirmed.State)).State.PlayerZones["P1"].Base);
        else Assert.Empty(confirmed.State.StackItems);
    }

    internal static MatchState ResourcePosition()
    {
        var state = GrantedPredictTests.Hand("OGN·171/298") with { Tick = 0 };
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["SIVIR"], Base = ["RUNE"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects.Where(p => !state.PlayerZones["P1"].LegendZone.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value)) {
                ["SIVIR"] = new("SIVIR", cardNo: "SFD·203/221", ownerId: "P1", controllerId: "P1"),
                ["RUNE"] = new("RUNE", cardNo: "UNL-R01", tags: [CardObjectTags.RuneCard, "COLOR:blue"], ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations.Where(p => !state.PlayerZones["P1"].LegendZone.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value)) {
                ["SIVIR"] = new("P1", "LEGEND_ZONE"), ["RUNE"] = new("P1", "BASE") } };
    }
}
