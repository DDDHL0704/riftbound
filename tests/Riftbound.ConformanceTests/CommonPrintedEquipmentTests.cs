using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class CommonPrintedEquipmentTests
{
    private static MatchState PrivateZone(MatchState state, string id, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value with {
            Hand = pair.Value.Hand.Where(value => value != id).ToArray(),
            Base = pair.Value.Base.Where(value => value != id).ToArray(),
            Battlefields = pair.Value.Battlefields.Where(value => value != id).ToArray(),
            Graveyard = pair.Value.Graveyard.Where(value => value != id).ToArray() });
        zones["P1"] = zone == "HAND" ? zones["P1"] with { Hand = [..zones["P1"].Hand, id] }
            : zones["P1"] with { Graveyard = [..zones["P1"].Graveyard, id] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { ControllerId = "P1", IsExhausted = false } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                [id] = new("P1", zone) } };
    }

    private static async Task<Driver> Play(string no, Func<MatchState, MatchState>? arrange = null)
    {
        var initial = PrintedExhaustAbilityTests.Position(no);
        initial = PrivateZone(initial, "HOST", "HAND");
        initial = arrange?.Invoke(initial) ?? initial;
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        return game;
    }

    private static async Task Activate(Driver game, string[] targets, string[]? costs = null)
        => await game.Act("P1", new ActivateAbilityCommand("HOST", PrintedExhaustAbilityTests.Ability(game.State),
            targets, OptionalCosts: costs));

    [Theory]
    [InlineData("ALLY")]
    [InlineData("GEAR")]
    public async Task SatchelReturnsAnotherFriendlyUnitOrEquipmentAfterPayingItsOwnExhaustion(string target)
    {
        var game = await Play("OGN·181/298");
        await Activate(game, [target]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.DoesNotContain(target, game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones["P1"].Hand);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task SatchelReturnsAReallyHiddenCardWithoutRevealingItsFace()
    {
        var game = await Play("OGN·181/298", state =>
            AttackDamageDistributionTests.Spell(LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F"),
                "SECRET", "OGN·053/298", "P1"));
        await game.Act("P1", new HideCardCommand("SECRET", "OGN·053/298", "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.True(game.State.CardObjects["SECRET"].IsFaceDown);
        var generation = game.State.CardObjects["SECRET"].ObjectGeneration;
        await Activate(game, ["SECRET"]);
        await game.Top();
        Assert.Contains("SECRET", game.State.PlayerZones["P1"].Hand);
        Assert.False(game.State.CardObjects["SECRET"].IsFaceDown);
        Assert.True(game.State.CardObjects["SECRET"].ObjectGeneration > generation);
        var returned = game.Events().Last(entry => entry.Event.Kind == "CARD_RETURNED_TO_HAND").Event;
        Assert.DoesNotContain("OGN·053/298", System.Text.Json.JsonSerializer.Serialize(returned));
        Assert.DoesNotContain("SECRET", System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("ENEMY")]
    public async Task SatchelRejectsItsOwnSourceAndEnemyObjectsWithoutChangingState(string target)
    {
        var game = await Play("OGN·181/298");
        var before = game.State;
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("bad-satchel", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", PrintedExhaustAbilityTests.Ability(before), [target]), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public async Task TreasureDestroysOnResolutionThenItsIndependentLeaveSkillDrawsAndCallsAnExhaustedRune()
    {
        var game = await Play("OGN·186/298");
        var before = game.State;
        await Activate(game, []);
        Assert.Equal(before.RunePools["P1"].PowerByTrait["purple"] - 1, game.State.RunePools["P1"].PowerByTrait["purple"]);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Equal(before.PlayerZones["P1"].Hand.Count, game.State.PlayerZones["P1"].Hand.Count);
        await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(before.PlayerZones["P1"].Hand.Count, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal("LEAVE", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        Assert.Equal(before.PlayerZones["P1"].Hand.Count + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(before.PlayerZones["P1"].RuneDeck.Count - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
        var called = before.PlayerZones["P1"].RuneDeck[0];
        Assert.Contains(called, game.State.PlayerZones["P1"].Base);
        Assert.True(game.State.CardObjects[called].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task SatchelReturningTreasureAlsoTriggersDrawAndRuneCall()
    {
        var game = await Play("OGN·181/298", state => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = Printed("GEAR", "OGN·186/298", "P1") } });
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        var runes = game.State.PlayerZones["P1"].RuneDeck.Count;
        await Activate(game, ["GEAR"]);
        await game.Top();
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Hand);
        Assert.Equal("LEAVE", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        await game.Top();
        Assert.Equal(hand + 2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(runes - 1, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ArmoryCreatesThreeMinionsAndChoosesEachDestinationThroughTheSharedTokenFlow()
    {
        var game = await Play("SFD·168/221", state => LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "F"));
        await Activate(game, []);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        await game.Top();
        string[] destinations = ["BASE", "BATTLEFIELD:F", "BASE"];
        for (var i = 0; game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } choice; i++)
        {
            Assert.True(i < 3);
            Assert.Contains(choice.Request.Options, option => option.Id == destinations[i]);
            await game.Act(choice.Request.PlayerId,
                new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [destinations[i]]));
        }
        var tokens = game.State.CardObjects.Values.Where(c => c.TokenFactoryCardNo is not null).ToArray();
        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, c => { Assert.Equal(1, c.Power); Assert.True(c.IsExhausted); });
        Assert.Equal(2, tokens.Count(c => game.State.ObjectLocations[c.ObjectId].Zone == "BASE"));
        Assert.Single(tokens, c => game.State.ObjectLocations[c.ObjectId].BattlefieldObjectId == "F");
        await game.Replay();
    }

    [Fact]
    public async Task AssemblyRackPaysManaRedPowerAndGraveyardUnitBeforeCreatingOneRobotAtBase()
    {
        var game = await Play("SFD·019/221", state => PrivateZone(state, "ALLY", "GRAVEYARD"));
        var before = game.State;
        await Activate(game, [], ["RECYCLE_GRAVEYARD_CARD:ALLY"]);
        Assert.Equal(before.RunePools["P1"].Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.RunePools["P1"].PowerByTrait["red"] - 1, game.State.RunePools["P1"].PowerByTrait["red"]);
        Assert.DoesNotContain("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].MainDeck);
        await game.Top();
        var robot = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        Assert.Equal(3, robot.Power);
        Assert.True(robot.IsExhausted);
        Assert.Contains(robot.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Null(game.State.PendingRuleChoice);
        await game.Replay();
    }
    [Fact]
    public async Task TreasureStillDrawsWhenItsControllerHasNoRuneLeftToCall()
    {
        var game = await Play("OGN·186/298", state => {
            var runes = state.PlayerZones["P1"].RuneDeck.ToHashSet(StringComparer.Ordinal);
            return state with {
                CardObjects = state.CardObjects.Where(pair => !runes.Contains(pair.Key)).ToDictionary(),
                ObjectLocations = state.ObjectLocations.Where(pair => !runes.Contains(pair.Key)).ToDictionary(),
                PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                    ["P1"] = state.PlayerZones["P1"] with { RuneDeck = [] } } };
        });
        var hand = game.State.PlayerZones["P1"].Hand.Count;
        await Activate(game, []);
        await game.Top();
        await game.Top();
        Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.PlayerZones["P1"].RuneDeck);
        var call = game.Events().Last(entry => entry.Event.Kind == "RUNES_CALLED").Event;
        Assert.Equal(0, call.Payload["count"]);
        await game.Replay();
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("red")]
    [InlineData("not-graveyard")]
    [InlineData("equipment")]
    [InlineData("duplicate")]
    public async Task AssemblyRackRejectsMissingResourcesAndInvalidRecyclingAtomically(string branch)
    {
        var game = await Play("SFD·019/221", state => PrivateZone(PrivateZone(state, "ALLY", "GRAVEYARD"), "GEAR", "GRAVEYARD"));
        var before = game.State;
        var id = PrintedExhaustAbilityTests.Ability(before);
        if (branch is "mana" or "red") before = before with {
            RunePools = new Dictionary<string, RunePool>(before.RunePools) {
                ["P1"] = branch == "mana" ? new(0, 0, new Dictionary<string, int> { ["red"] = 20 })
                    : new(20, 0, new Dictionary<string, int> { ["blue"] = 20 }) } };
        string[] costs = branch switch {
            "equipment" => ["RECYCLE_GRAVEYARD_CARD:GEAR"],
            "not-graveyard" => ["RECYCLE_GRAVEYARD_CARD:HOST"],
            "duplicate" => ["RECYCLE_GRAVEYARD_CARD:ALLY", "RECYCLE_GRAVEYARD_CARD:ALLY"],
            _ => ["RECYCLE_GRAVEYARD_CARD:ALLY"] };
        var result = await new CoreRuleEngine().ResolveAsync(before, new("bad-rack-" + branch, "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [], OptionalCosts: costs), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(result.State));
    }

}
