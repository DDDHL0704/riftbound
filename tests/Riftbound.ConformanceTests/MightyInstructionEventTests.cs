using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MightyInstructionEventTests
{
    internal static MatchState OpenActionPosition(string fiora = "SFD·180/221")
    {
        var state = MightyTransitionTests.UnitSource(fiora, alreadyMighty: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Power = 3, Tags = [..state.CardObjects["HOST"].Tags, CardObjectTags.Boon] } } };
        state = PowerfulConsumerMightTests.Hand(state, "OGN·153/298");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(5, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 2, [RuneTrait.Yellow] = 1 }) } };
    }

    [Theory]
    [InlineData("SFD·180/221")] [InlineData("SFD·180a/221")]
    public async Task ConsumeThenRegrantBoonRaisesOneRealMightyEventEvenWithTheSameFinalMight(string fiora)
    {
        var state = OpenActionPosition(fiora); Assert.Equal(5, ArmamentMightTests.Might(state));
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"]));
        var done = await Top(played.State);
        Assert.Equal(5, ArmamentMightTests.Might(done.State)); Assert.False(done.State.CardObjects["HOST"].IsExhausted);
        Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["HOST"].Tags);
        var context = Assert.Single(done.State.StackItems).MightySkill!;
        Assert.NotNull(context); Assert.Equal(4, context.BeforeMight); Assert.Equal(5, context.AfterMight);
        Assert.Equal("HOST", context.Unit.ObjectId); Assert.NotNull(done.State.PendingPayment);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ChoosingNoBoonConsumptionStillBuffsAllFriendlyUnitsWithoutARepeatedMightyEvent()
    {
        var state = OpenActionPosition();
        var done = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·153/298", []))).State);
        Assert.Equal(5, ArmamentMightTests.Might(done.State)); Assert.True(done.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(4, done.State.CardObjects["FIORA"].Power); Assert.Contains(CardObjectTags.Boon, done.State.CardObjects["FIORA"].Tags);
        Assert.Empty(done.State.StackItems); Assert.Null(done.State.PendingPayment);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task GrantedEchoProducesTwoDistinctCrossingsInsideOneResolution()
    {
        var state = OpenActionPosition() with { UntilEndOfTurnEffects = ["BATTLEFIELD_HELD_NEXT_SPELL_GAINS_ECHO:P1:1"] };
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(10, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 4, [RuneTrait.Yellow] = 2 }) } };
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"], OptionalCosts: ["ECHO"]));
        Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        var done = await Top(played.State); Assert.Equal(5, ArmamentMightTests.Might(done.State));
        Assert.Equal(2, done.State.TriggerQueue.Count); Assert.Equal(2, done.State.TriggerQueue.Select(i => i.TriggerId).Distinct().Count());
        Assert.All(done.State.TriggerQueue, i => { Assert.Equal(4, i.MightySkill!.BeforeMight); Assert.Equal(5, i.MightySkill.AfterMight); });
        Assert.Equal(2, done.Events.Count(e => e.Kind == "BOON_CONSUMED"));
        Assert.DoesNotContain(done.Events, e => e.Kind.StartsWith("INTERNAL_", StringComparison.Ordinal));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
        var ordered = await Act(done.State, "P1", new OrderTriggersCommand(OrderedTriggerIds: done.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var index = 0; index < 2; index++) {
            var pending = ordered.State.PendingPayment!;
            ordered = await Act(ordered.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_POWER:yellow:1"]));
            Assert.Single(ordered.Events, e => e.Kind == "COST_PAID");
            Assert.Equal(1 - index, ordered.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
            Restore(ordered.State); ArmamentMightTests.PublicRecovery(ordered.State);
        }
        var first = await Top(ordered.State); var second = await Top(first.State);
        Assert.Empty(second.State.StackItems); Assert.False(second.State.CardObjects["HOST"].IsExhausted);
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
    }

    [Theory]
    [InlineData("enemy")] [InlineData("unbuffed")] [InlineData("equipment")]
    [InlineData("hidden")] [InlineData("duplicate")]
    public async Task IllegalConsumptionTargetsAreRejectedWithoutChangingTheMatch(string invalid)
    {
        var state = OpenActionPosition(); var targets = invalid == "duplicate" ? new[] { "HOST", "HOST" } : new[] { invalid == "equipment" ? "GEAR" : "HOST" };
        if (invalid == "enemy") state = LocalDestructionRecallTests.At(state, "HOST", "P2", "BASE");
        if (invalid is "unbuffed" or "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = invalid == "hidden",
                Tags = invalid == "unbuffed" ? state.CardObjects["HOST"].Tags.Where(t => t != CardObjectTags.Boon).ToArray() : state.CardObjects["HOST"].Tags } } };
        var done = await new CoreRuleEngine().ResolveAsync(state, new("bad", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "OGN·153/298", targets), default);
        Assert.False(done.Accepted); Assert.Empty(done.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(done.State));
    }

    [Theory]
    [InlineData("SFD·205/221")] [InlineData("SFD·251/221")]
    public async Task TheLegendAlsoSeesTheRebuffCrossingAndKeepsExhaustionOptional(string card)
    {
        var state = OpenActionPosition(); var legend = MightyTransitionTests.LegendSource(card);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = legend.CardObjects["LEGEND"] },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var done = await Top((await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"]))).State);
        Assert.Equal(2, done.State.TriggerQueue.Count); Assert.Contains(done.State.TriggerQueue, t => t.MightySkill!.Source.ObjectId == "LEGEND");
        Assert.False(done.State.CardObjects["LEGEND"].IsExhausted); Assert.Equal(3, done.State.PlayerZones["P1"].RuneDeck.Count);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ATargetReturnedInResponseIsSkippedWhileTheUntargetedBoonStillResolves()
    {
        var state = OfficialCopyIdentityTests.AddSpell(OpenActionPosition(), "RETURN", "OGN·104/298");
        var played = await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"]));
        var returned = await Top((await Act(played.State, "P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]))).State);
        var done = await Top(returned.State);
        Assert.Contains("HOST", done.State.PlayerZones["P1"].Hand); Assert.Equal(1, done.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(4, done.State.CardObjects["FIORA"].Power); Assert.Empty(done.State.StackItems);
        Assert.DoesNotContain(done.Events, e => e.Kind == "BOON_CONSUMED");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("before")] [InlineData("after")] [InlineData("private")]
    public async Task RecoveryRejectsForgedInstructionCrossingInputs(string altered)
    {
        var done = await Top((await Act(OpenActionPosition(), "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"]))).State);
        var item = Assert.Single(done.State.StackItems); var c = item.MightySkill!;
        c = altered switch {
            "before" => c with { BeforeMight = 5 }, "after" => c with { AfterMight = 4 },
            _ => c with { Before = c.Before with { PlayerZones = new Dictionary<string, PlayerZones>(c.Before.PlayerZones) {
                ["P2"] = c.Before.PlayerZones["P2"] with { Hand = ["PRIVATE"] } } } } };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(done.State with { StackItems = [item with { MightySkill = c }] }),
            e => e.Contains("Mighty transition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoricalInstructionFramesContainNoPrivatePileOrderOrInternalEventPayload()
    {
        var done = await Top((await Act(OpenActionPosition(), "P1", new PlayCardCommand("SPELL", "OGN·153/298", ["HOST"]))).State);
        var c = Assert.Single(done.State.StackItems).MightySkill!;
        foreach (var frame in new[] { c.Before, c.After })
            Assert.All(frame.PlayerZones.Values, z => { Assert.Empty(z.Hand); Assert.Empty(z.MainDeck); Assert.Empty(z.RuneDeck); });
        Assert.DoesNotContain(done.Events, e => e.Kind.StartsWith("INTERNAL_", StringComparison.Ordinal));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
