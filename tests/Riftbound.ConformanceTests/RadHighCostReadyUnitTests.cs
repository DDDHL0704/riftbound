using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadHighCostReadyUnitTests
{
    private const string Keeper = "RAD·030/167";
    private static MatchState Position(string played)
    {
        var state = VenUnitEntryBasicTests.Position(played);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", Keeper, "P1") with { IsExhausted = true } } };
    }

    [Theory]
    [InlineData("OGN·088/298", true)] [InlineData("SFD·168/221", true)] [InlineData("OGS·005/024", false)]
    public async Task PrintedSevenManaUnitOrEquipmentEarnsASeparateReadySkill(string card, bool qualifies)
    {
        var game = new Driver(Position(card));
        await game.Act("P1", new PlayCardCommand("UNIT", card, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(qualifies, game.State.StackItems.Any(i => i.HighCostPlayObserver is not null));
        if (qualifies) { await game.Top(); Assert.False(game.State.CardObjects["ALLY"].IsExhausted); }
        else Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·123/298", true)] [InlineData("OGN·233/298", false)]
    public async Task SpellsAreObservedOnlyAfterTheySuccessfullyFinishBeingPlayed(string card, bool qualifies)
    {
        var game = new Driver(Position(card));
        await game.Act("P1", new PlayCardCommand("UNIT", card, []));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.State.StackItems, i => i.HighCostPlayObserver is not null);
        await game.Top();
        Assert.Equal(qualifies, game.State.StackItems.Any(i => i.HighCostPlayObserver is not null));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        if (qualifies) { await game.Top(); Assert.False(game.State.CardObjects["ALLY"].IsExhausted); }
        await game.Replay();
    }

    [Fact]
    public async Task ACounteredSevenManaSpellNeverCompletesAndCannotReadyTheKeeper()
    {
        var state = AttackDamageDistributionTests.Spell(Position("OGN·123/298"), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "OGN·123/298", []));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.Source.ObjectId == "UNIT"); await game.Replay();
    }

    [Fact]
    public async Task ThePrintedCostQualifiesEvenWhenCurrentMightReducesPaymentToFive()
    {
        var state = Position("SFD·103/221");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("UNIT", "SFD·103/221", [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TheOpponentsHighCostPlayDoesNotReadyThisUnit()
    {
        var state = Position("OGN·088/298");
        state = HandDiscardObserverTests.Put(state, "P2-HIGH", "OGN·088/298", "P2", "HAND");
        var game = new Driver(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
        await game.Act("P2", new PlayCardCommand("P2-HIGH", "OGN·088/298", [], Destination: "BASE"));
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["ALLY"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AReturnedSourceCannotReadyItsNewHandIncarnation()
    {
        var state = AttackDamageDistributionTests.Spell(Position("OGN·088/298"), "RETURN", "VEN·052", "P1");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "VEN·052", ["ALLY"], Mode: "RETURN_FRIENDLY"));
        await game.Top(); Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand);
        var generation = game.State.CardObjects["ALLY"].ObjectGeneration;
        await game.Top(); Assert.Equal(generation, game.State.CardObjects["ALLY"].ObjectGeneration);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task RecoveryRejectsInventedCompletionCostsControllersAndSourceGenerations()
    {
        var game = new Driver(Position("OGN·088/298"));
        await game.Act("P1", new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE"));
        var item = Assert.Single(game.State.StackItems); var context = item.HighCostPlayObserver!;
        foreach (var forged in new[] {
            item with { HighCostPlayObserver = context with { CardPlay = null } },
            item with { HighCostPlayObserver = context with { CardPlay = context.CardPlay! with { CardNo = "OGS·005/024" } } },
            item with { HighCostPlayObserver = context with { Source = context.Source with { Generation = context.Source.Generation + 1 } } },
            item with { ControllerId = "P2" } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnUnaffordableAttemptCannotReadyTheKeeper()
    {
        var state = Position("OGN·088/298");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        var command = new PlayCardCommand("UNIT", "OGN·088/298", [], Destination: "BASE");
        var result = await new CoreRuleEngine().ResolveAsync(state, new("poor-high-cost", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task OfficialTextDeclaresASevenManaCardCompletionTrigger()
    {
        var card = (await OfficialCardCatalog.LoadRuntimeAsync()).Cards.Single(c => c.CardNo == Keeper);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect));
        Assert.Equal(TriggerKinds.ControllerHighCostCardReadySelf, spec.Kind); Assert.Equal(7, spec.MinimumCardMana);
        Assert.True(spec.ReadiesSource); Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
    }
}
