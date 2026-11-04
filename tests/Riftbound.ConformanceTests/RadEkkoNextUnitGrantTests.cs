using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// RAD 021 / 139: self-replay uses the skill controller, and the granted
// end skill belongs to the played generation rather than the physical card.
public sealed class RadEkkoNextUnitGrantTests
{
    private const string Ekko = "RAD·021/167", NextUnit = "SFD·143/221";
    private const string Ability = "RAD_021_SWIFT_DISCARD_BANISH_REPLAY";

    private static MatchState WithLegend(MatchState state)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·139/167", "P1") with { IsEmpowered = true } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } };

    private static MatchState Position(bool inHand = false, string owner = "P1")
    {
        var state = WithLegend(VenUnitEntryBasicTests.Position(Ekko));
        state = HandDiscardObserverTests.Put(state, "COST", "UNL-009/219", "P1", "HAND");
        state = HandDiscardObserverTests.Put(state, "NEXT", NextUnit, "P1", "HAND");
        if (!inHand)
        {
            state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "UNIT").ToArray() } } };
            state = LocalDestructionRecallTests.At(state, "UNIT", "P1", "BASE");
        }
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = owner } } };
    }

    private static async Task Grant(Driver game)
    {
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.TimeAssassinLegendAbilityId, [], []));
        await game.Top();
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
    }

    private static ActivateAbilityCommand Rewind(string source = "UNIT")
        => new(source, Ability, [], OptionalCosts: ["DISCARD_HAND_CARD:COST"]);

    private static string[] Markers(MatchState state, string id)
        => state.CardObjects[id].UntilEndOfTurnEffects.Where(effect =>
            effect.StartsWith(CoreRuleEngine.TimeAssassinDestructionPrefix, StringComparison.Ordinal)).ToArray();

    private static async Task ReplayEkko(Driver game, string owner)
    {
        var oldGeneration = game.State.CardObjects["UNIT"].ObjectGeneration;
        var before = game.State.RunePools["P1"];
        await game.Act("P1", Rewind());
        Assert.Equal(before.Mana - 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - 1, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId);
        Assert.True(pending.IgnoreBaseMana);
        Assert.True(pending.IgnoreBasePower);
        Assert.False(pending.IgnoreAllCosts);
        Assert.Contains("UNIT", game.State.PlayerZones[owner].Banished);
        var resources = game.State.RunePools.ToDictionary(pair => pair.Key, pair => pair.Value);
        var play = new PlayCardCommand("UNIT", Ekko, []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await game.Act("P1", play);
        Assert.All(resources, pair => Assert.Equal(pair.Value, game.State.RunePools[pair.Key]));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Equal(oldGeneration + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].ControllerId);
        Assert.Equal(owner, game.State.CardObjects["UNIT"].OwnerId);
    }

    private static async Task DestroyAtEnd(Driver game, string id, string owner = "P1")
    {
        await game.Act("P1", new EndTurnCommand());
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(CoreRuleEngine.TimeAssassinEndDestructionKind, skill.EndAbility!.Kind);
        Assert.Equal(id, skill.SourceObjectId);
        await game.Top();
        Assert.Contains(id, game.State.PlayerZones[owner].Graveyard);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), id));
        await game.Replay();
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task AFreeSelfReplayConsumesItsControllersGrantEvenWhenTheCardIsBorrowed(string owner)
    {
        var game = new Driver(Position(owner: owner));
        await Grant(game);
        await ReplayEkko(game, owner);
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        var marker = CoreRuleEngine.ReadTimeAssassinDestructionMarker(Assert.Single(Markers(game.State, "UNIT")))!;
        Assert.Equal("P1", marker.ControllerId);
        Assert.Equal(game.State.CardObjects["UNIT"].ObjectGeneration, marker.Target.Generation);
        await DestroyAtEnd(game, "UNIT", owner);
    }

    [Fact]
    public async Task ADiscountedEkkoLosesItsOldGrantedDestructionWhenItRewinds()
    {
        var game = new Driver(Position(inHand: true));
        await Grant(game);
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("UNIT", Ekko, []));
        Assert.Equal(before.Mana - 3, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Single(Markers(game.State, "UNIT"));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await ReplayEkko(game, "P1");
        Assert.Empty(Markers(game.State, "UNIT"));
        await game.Act("P1", new EndTurnCommand());
        Assert.Empty(game.State.StackItems);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Fact]
    public async Task AnImageThatCeasesOnSelfBanishmentLeavesTheGrantForTheNextPhysicalUnit()
    {
        var state = WithLegend(OfficialCopyIdentityTests.AddSpell(OfficialCopyIdentityTests.Position(Ekko), "COST", "UNL-009/219")) with { Tick = 0 };
        state = HandDiscardObserverTests.Put(state, "NEXT", NextUnit, "P1", "HAND");
        var game = new Driver(state);
        await game.Act("P1", new PlayCardCommand("C", "UNL-200/219", ["TARGET"]));
        await game.Top();
        await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        await Grant(game);
        await game.Act("P1", Rewind(image.ObjectId));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Empty(game.State.StackItems);
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "TIME_ASSASSIN_UNIT_MARKED_FOR_DESTRUCTION");
        Assert.Single(game.Events(), entry => entry.Event.Kind == "TOKEN_CEASED_TO_EXIST"
            && Equals(entry.Event.Payload.GetValueOrDefault("targetObjectId"), image.ObjectId));
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, []));
        Assert.Equal(before.Mana - 2, game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
        Assert.Single(Markers(game.State, "NEXT"));
        await DestroyAtEnd(game, "NEXT");
    }

    [Fact]
    public async Task ReturningEkkoInResponsePreservesTheGrantAndCannotBanishItsHandGeneration()
    {
        var game = new Driver(HandDiscardObserverTests.Put(Position(), "RETURN", "OGN·104/298", "P1", "HAND"));
        await Grant(game);
        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", Rewind());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top();
        await game.Top();
        Assert.Null(game.State.PendingEffectPlay);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "UNIT_BANISHED");
        Assert.Contains("COST", game.State.PlayerZones["P1"].Graveyard);
        Assert.Single(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        await game.Act("P1", new PlayCardCommand("NEXT", NextUnit, []));
        Assert.Empty(CoreRuleEngine.TimeAssassinNextUnitReductions(game.State, "P1"));
        Assert.Single(Markers(game.State, "NEXT"));
        await DestroyAtEnd(game, "NEXT");
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
    }
}
