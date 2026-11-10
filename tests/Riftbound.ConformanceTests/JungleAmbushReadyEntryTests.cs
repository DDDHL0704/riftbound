using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class JungleAmbushReadyEntryTests
{
    private const string Spell = "SFD·004/221", Unit = "OGN·219/298";
    private static MatchState Position()
    {
        var state = RadGearTestPosition.Put(AloneEnemyBattlefieldTestPosition.Create(), "AMBUSH", Spell, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "FIRST", Unit, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "SECOND", Unit, "P1", "HAND");
        state = RadGearTestPosition.Put(state, "RETURN", "OGN·104/298", "P1", "HAND");
        for (var i = 0; i < 4; i++) state = RadGearTestPosition.Put(state, "READY-RUNE-" + i, "UNL-R01", "P1", "BASE");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
    }

    private static async Task Cast(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("AMBUSH", Spell, []));
        await game.Top();
    }

    internal static async Task FinishTurn(Driver game)
    {
        ResolutionResult Current() => new(true, null, game.State, [],
            ResolutionResult.BuildSnapshots(game.State), ResolutionResult.BuildPrompts(game.State));
        async ValueTask<ResolutionResult> Submit(string player, GameCommand command) {
            await game.Act(player, command); return Current();
        }
        await game.Act(game.State.TurnPlayerId, new EndTurnCommand());
        await TurnSequenceTestDriver.Complete(Current(), Submit);
    }

    [Fact]
    public async Task TheActualSpellCreatesExhaustedGoldAndMakesEveryLaterFriendlyUnitEnterReady()
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("AMBUSH", Spell, []));
        Assert.DoesNotContain(game.State.CardObjects.Values, card => TokenObjectRules.IsToken(card));
        await game.Top();
        var gold = Assert.Single(game.State.CardObjects.Values, card =>
            P6TokenFactoryCatalog.TryGetByCardNo(card.TokenFactoryCardNo ?? "", out var factory)
            && factory.TokenFamilyName == "金币");
        Assert.True(gold.IsExhausted); Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        var prefix = game.Events().Length;
        foreach (var id in new[] { "FIRST", "SECOND" }) {
            await game.Act("P1", new PlayCardCommand(id, Unit, []));
            Assert.False(game.State.CardObjects[id].IsExhausted);
        }
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.Events().Skip(prefix), entry => entry.Event.Kind == "UNIT_READIED");
        await game.Replay();
    }

    [Fact]
    public async Task ARealReturnAndReplayUsesTheSameTurnPermissionForTheNewFieldIncarnation()
    {
        var game = new Driver(Position()); await Cast(game);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        var generation = game.State.CardObjects["FIRST"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["FIRST"])); await game.Top();
        Assert.Contains("FIRST", game.State.PlayerZones["P1"].Hand);
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.Equal(generation + 2, game.State.CardObjects["FIRST"].ObjectGeneration);
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task ActualTurnEndExpiresThePermissionBeforeAnotherTurnCanPlayTheSameOrdinaryUnit()
    {
        var game = new Driver(Position()); await Cast(game);
        await FinishTurn(game); await FinishTurn(game);
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Equal(MatchPhases.Main, game.State.Phase);
        await game.TapRunes("P1", Enumerable.Range(0, 4).Select(i => "READY-RUNE-" + i));
        await game.Act("P1", new PlayCardCommand("FIRST", Unit, []));
        Assert.True(game.State.CardObjects["FIRST"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task AnUnaffordableCastCannotCreateGoldOrAReadyEntryPermission()
    {
        var state = Position(); state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = state.CardObjects.ToDictionary(pair => pair.Key, pair =>
                pair.Value.Tags.Contains(CardObjectTags.RuneCard) ? pair.Value with { IsExhausted = true } : pair.Value) };
        await RadGearTestPosition.Reject(state, "P1", new PlayCardCommand("AMBUSH", Spell, []));
    }

    [Fact]
    public async Task RecoveryCannotEraseThePermissionEarnedByAnActualResolvedSpell()
    {
        var game = new Driver(Position()); await Cast(game);
        var forged = game.State with { UntilEndOfTurnEffects = [] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged)); await game.Replay();
    }
}
