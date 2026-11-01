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
    [InlineData("RAD·157/167", 1)]
    [InlineData("OGN·296/298", 0)]
    public async Task BandleScoutAcademyCreatesSleepingGoldOnlyForStandbyAtItsField(string field, int expected)
    {
        var state = Put(Create(field), "CARD", "OGN·135/298", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new HideCardCommand("CARD", "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        var gold = game.State.CardObjects.Values.Where(c => c.Tags.Contains("金币")).ToArray();
        Assert.Equal(expected, gold.Length);
        Assert.All(gold, c => { Assert.True(c.IsExhausted); Assert.Contains(c.ObjectId, game.State.PlayerZones["P1"].Base); });
        await Reject(game.State, "P1", new HideCardCommand("CARD", "OGN·135/298", "BATTLEFIELD:BF", ["STANDBY_A"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·163/167", "BATTLEFIELD:BF", 1)]
    [InlineData("RAD·163/167", "BASE", 0)]
    [InlineData("OGN·296/298", "BATTLEFIELD:BF", 0)]
    public async Task WindsweptPlazaDiscountsOnlyTheFirstPermanentPlayedHere(string field, string destination, int discount)
    {
        var state = Put(Create(field), "CARD", "OGN·219/298", "P1", "HAND");
        state = Put(state, "SECOND", "OGN·219/298", "P1", "HAND");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·219/298", out var unit));
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", unit.CardNo, [], Destination: destination));
        Assert.Equal(30 - unit.ManaCost + discount, game.State.RunePools["P1"].Mana);
        await game.Act("P1", new PlayCardCommand("SECOND", unit.CardNo, [], Destination: destination));
        Assert.Equal(30 - 2 * unit.ManaCost + discount, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RakelstakeInsightsOnceOnTheFirstUnitOrEquipmentAndKeepsTheChoicePrivate(bool recycle)
    {
        var state = Put(Create("RAD·165/167"), "CARD", "OGN·219/298", "P1", "HAND");
        state = Put(state, "SECOND", "RAD·002/167", "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("CARD", "OGN·219/298", [], Destination: "BATTLEFIELD:BF"));
        Assert.Single(game.State.StackItems);
        await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        Assert.Equal(new[] { "P1-D0" }, choice.LegalObjectIds);
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-D1"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycle ? ["P1-D0"] : []));
        Assert.Equal(recycle ? "P1-D1" : "P1-D0", game.State.PlayerZones["P1"].MainDeck[0]);
        await game.Act("P1", new PlayCardCommand("SECOND", "RAD·002/167", [], Destination: "BATTLEFIELD:BF"));
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RuinedMonasteryCallsASleepingRuneOnlyAfterAcceptingTheDiscard(bool accept)
    {
        var state = ConquestLifecycleRegressionTests.State("RAD·166/167", 1);
        state = Put(state, "R", "OGN·007/298", "P1", "RUNE_DECK");
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
        if (accept) Assert.True(game.State.CardObjects["R"].IsExhausted);
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
    public async Task FirelightsHideoutGrantsOptionalHasteToExactlyTheNextUnit()
    {
        var state = ConquestLifecycleRegressionTests.State("RAD·162/167", 0);
        state = Put(state, "FIRST", "OGN·219/298", "P1", "HAND");
        state = Put(state, "SECOND", "OGN·219/298", "P1", "HAND");
        state = state with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = RunePool.Empty } };
        var game = new Driver(state); await Conquer(game);
        Assert.Contains(game.State.UntilEndOfTurnEffects, e => e.StartsWith("RAD162_NEXT_HASTE:P1:"));
        await game.Act("P1", new PlayCardCommand("FIRST", "OGN·219/298", [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.False(game.State.CardObjects["FIRST"].IsExhausted);
        Assert.DoesNotContain(game.State.UntilEndOfTurnEffects, e => e.StartsWith("RAD162_NEXT_HASTE:P1:"));
        await Reject(game.State, "P1", new PlayCardCommand("SECOND", "OGN·219/298", [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await game.Act("P1", new PlayCardCommand("SECOND", "OGN·219/298", []));
        Assert.True(game.State.CardObjects["SECOND"].IsExhausted);
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
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·159/167", 1)]
    [InlineData("OGN·296/298", 0)]
    public async Task BlackRoseSanctumCreatesOneMinionPerPlayerPerTurnForLocalDeaths(string field, int expected)
    {
        var state = Create(field) with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "SECOND", "OGN·219/298", "P1", "BATTLEFIELD", "BF");
        state = Put(state, "KILL1", "OGN·229/298", "P2", "HAND");
        state = Put(state, "KILL2", "OGN·229/298", "P2", "HAND");
        var game = new Driver(state);
        foreach (var (spell, target) in new[] { ("KILL1", "ALLY"), ("KILL2", "SECOND") }) {
            await game.Act("P2", new PlayCardCommand(spell, "OGN·229/298", [target]));
            await game.Top();
            Assert.Contains(target, game.State.PlayerZones["P1"].Graveyard);
            var minions = game.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.MinionTokenFamily)).ToArray();
            Assert.Equal(expected, minions.Length);
            Assert.All(minions, c => { Assert.Equal(1, c.Power); Assert.Contains(c.ObjectId, game.State.PlayerZones["P1"].Base); });
        }
        await game.Replay();
    }

    private static async Task Conquer(Driver game)
    {
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
    }
}
