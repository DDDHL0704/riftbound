using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadBattlefieldEffectTests
{
    [Theory]
    [InlineData("RAD·164/167")]
    [InlineData("RAD·184/167")]
    public async Task StarlitHallBuffsHeroesOfBothPlayersOnlyWhileHere(string no)
    {
        var state = Create(no);
        state = Put(state, "HERO", "UNL-022/219", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "ENEMY-HERO", "UNL-022/219", "P2", "BATTLEFIELD", "BF");
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var auras = state.ContinuousEffects.Where(e => e.SourceObjectId == "BF" && e.PowerDelta != 0).ToArray();
        Assert.Equal(new[] { "ENEMY-HERO", "HERO" }, auras.Select(e => e.TargetObjectId).Order());
        Assert.All(auras, e => Assert.Equal(1, e.PowerDelta));
        state = RadUnitEffectTestFixture.Place(state, "HERO", "P1", "BASE");
        Assert.DoesNotContain(state.ContinuousEffects, e => e.SourceObjectId == "BF" && e.TargetObjectId == "HERO");
        var game = new Driver(Put(state, "CARD", "OGN·219/298", "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("CARD", "OGN·219/298", []));
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task BlackMarketAllowsTheTurnPlayerWithoutControlAndRejectsASecondUse(string actor)
    {
        var state = Create("RAD·158/167") with { TurnPlayerId = actor, ActivePlayerId = actor };
        var game = new Driver(state);
        var activation = new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []);
        await Reject(game.State, actor == "P1" ? "P2" : "P1", activation);
        await game.Act(actor, activation);
        Assert.Equal(28, game.State.RunePools[actor].TotalPower);
        Assert.Empty(game.State.PlayerZones[actor].Hand);
        await game.Top();
        Assert.Equal(new[] { actor + "-D0" }, game.State.PlayerZones[actor].Hand);
        await Reject(game.State, actor, activation);
        await game.Replay();
    }

    [Fact]
    public async Task BlackMarketRejectsInsufficientPowerWithoutSpendingMana()
    {
        var state = Create("RAD·158/167") with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 1), ["P2"] = RunePool.Empty } };
        await Reject(state, "P1", new ActivateAbilityCommand("BF", P4ActivatedAbilityCatalog.BlackMarketAbilityId, []));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuinedMonasteryCallsASleepingRuneOnlyAfterAcceptingTheDiscard(bool accept)
    {
        var state = ConquestLifecycleRegressionTests.State("RAD·166/167", 1);
        state = Put(state, "P1-MONASTERY-RUNE", "OGN·007/298", "P1", "RUNE_DECK");
        var game = new Driver(state);
        await Conquer(game);
        Assert.Single(game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        var choice = Assert.IsType<PendingHandChoiceState>(game.State.PendingHandChoice);
        await Reject(game.State, "P2", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["H1"]));
        await Reject(game.State, "P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["DRAW1"]));
        await game.Act("P1", new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, accept ? ["H1"] : []));
        Assert.Equal(accept ? 0 : 1, game.State.PlayerZones["P1"].RuneDeck.Count);
        Assert.Equal(accept ? 0 : 1, game.State.PlayerZones["P1"].Hand.Count);
        if (accept) Assert.True(game.State.CardObjects["P1-MONASTERY-RUNE"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CosmicVistaChecksTheRuneDeckAtHoldBeforeTheNormalChannel(bool empty)
    {
        var state = OfficialHoldSequenceTests.State("RAD·160/167");
        if (empty) state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { RuneDeck = [] } },
            CardObjects = state.CardObjects.Where(p => !state.PlayerZones["P1"].RuneDeck.Contains(p.Key)).ToDictionary(),
            ObjectLocations = state.ObjectLocations.Where(p => !state.PlayerZones["P1"].RuneDeck.Contains(p.Key)).ToDictionary() };
        var game = new Driver(state);
        await game.Act("P1", new PassPriorityCommand());
        if (empty) {
            Assert.Single(game.State.StackItems); Assert.Equal(1, game.State.PlayerScores["P1"]);
            await game.Top();
        }
        Assert.Equal(empty ? 2 : 1, game.State.PlayerScores["P1"]);
        Assert.Equal(MatchPhases.Main, game.State.Phase);
        await game.Replay();
    }

    [Fact]
    public async Task DurandsMonumentGrantsBulwarkOnlyToADefenderAtThisField()
    {
        var state = Create("RAD·161/167") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "REMOTE", "OGN·219/298", "P1", "BASE");
        var game = new Driver(state);
        await game.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:BF", []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(new[] { "ALLY" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["REMOTE"]));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Top();
        Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, e => e.StartsWith("COMBAT_KEYWORD:壁垒:"));
        while (game.State.FocusPlayerId is { } focus) await game.Act(focus, new PassFocusCommand());
        await game.Act("P2", new DeclareBattleCommand("BF", ["ENEMY"], ["ALLY"], ["COMBAT_ASSIGNMENT"]));
        for (var i = 0; game.State.PendingBattleResult is not null; i++) {
            Assert.InRange(i, 0, 7);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Contains(game.Events(), e => e.Event.Kind == "BATTLE_CLOSED");
        Assert.DoesNotContain(game.State.CardObjects.Values.SelectMany(c => c.UntilEndOfTurnEffects),
            e => e.StartsWith("COMBAT_KEYWORD:壁垒:") || e.StartsWith("BATTLE_LIMITED_GRANT:"));
        await game.Replay();
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }
}
