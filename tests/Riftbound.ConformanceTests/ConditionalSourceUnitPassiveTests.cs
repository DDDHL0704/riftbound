using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ConditionalSourceUnitPassiveTests
{
    private static MatchState Position(string no, string zone = "HAND")
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", no, "P1", zone);
        state = RadGearTestPosition.Put(state, "SPELL", "OGN·154/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "ENVOY", "UNL-092/219", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "PALACE", "UNL-088/219", "P1", "BASE");
        return RadGearTestPosition.Put(state, "DISCARD", "OGN·048/298", "P1", "HAND");
    }

    private static string History(string no, string player = "P1") => no switch {
        "OGN·019/298" => "DISCARDED_HAND_CARD_THIS_TURN:" + player,
        "UNL-004/219" => "PLAYED_FOUR_PLUS_COST_SPELL_THIS_TURN:" + player,
        _ => "GAINED_EXPERIENCE_THIS_TURN:" + player };

    private static void AssertPassive(MatchState state, string no, bool enabled)
    {
        var basis = no == "UNL-004/219" ? 1 : 4;
        Assert.Equal(basis, state.CardObjects["HOST"].Power);
        Assert.Equal(basis + (enabled ? no == "UNL-004/219" ? 4 : no == "UNL-108/219" ? 1 : 0 : 0),
            ArmamentMightTests.Might(state));
        var tags = state.CardObjects["HOST"].Tags;
        if (no != "UNL-004/219") Assert.Equal(enabled, tags.Contains(CardCombatKeywordNames.Roam));
        if (no == "OGN·019/298") Assert.Equal(enabled, tags.Contains(CardCombatKeywordNames.Assault));
        OfficialGraveyardRecastTests.Restore(state);
        ArmamentMightTests.PublicRecovery(state);
    }

    private static async Task Enable(Driver game, string no)
    {
        if (no == "OGN·019/298")
            await game.Act("P1", new ActivateAbilityCommand("PALACE", "CRUMBLING_PALACE_DISCARD_CREATE_WARHAWK", [],
                ["DISCARD_HAND_CARD:DISCARD"]));
        else if (no == "UNL-004/219")
            await game.Act("P1", new PlayCardCommand("SPELL", "OGN·154/298", ["ALLY"]));
        else
            await game.Act("P1", new PlayCardCommand("ENVOY", "UNL-092/219", []));
        while (game.State.StackItems.Count > 0 && game.State.PendingRuleChoice is null) await game.Top();
        while (game.State.PendingRuleChoice is { Request.Kind: "TOKEN_PLAY_LOCATION" } choice)
            await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["BASE"]));
        Assert.Contains(History(no), game.State.UntilEndOfTurnEffects);
    }

    // CN 364.3 / 365.1: these are continuous passives, not entry replacements.
    [Theory]
    [InlineData("OGN·019/298")]
    [InlineData("UNL-004/219")]
    [InlineData("UNL-108/219")]
    public async Task AlreadyPlayedUnitGainsItsPassiveFromARealActionAndLosesItAtTurnEnd(string no)
    {
        var game = new Driver(Position(no));
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        AssertPassive(game.State, no, false);
        await Enable(game, no);
        AssertPassive(game.State, no, true);
        await game.Act("P1", new EndTurnCommand());
        AssertPassive(game.State, no, false);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·019/298")]
    [InlineData("UNL-004/219")]
    [InlineData("UNL-108/219")]
    public async Task ConditionAlreadyMetOnEntryDoesNotBecomeAPermanentOrDoubleBonus(string no)
    {
        var initial = Position(no) with { UntilEndOfTurnEffects = [History(no)] };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        AssertPassive(game.State, no, true);
        await game.Act("P1", new EndTurnCommand());
        AssertPassive(game.State, no, false);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·019/298")]
    [InlineData("UNL-004/219")]
    [InlineData("UNL-108/219")]
    public async Task AnotherPlayersHistoryDoesNotEnableThePassive(string no)
    {
        var game = new Driver(Position(no) with { UntilEndOfTurnEffects = [History(no, "P2")] });
        await game.Act("P1", new PlayCardCommand("HOST", no, []));
        AssertPassive(game.State, no, false);
        await game.Replay();
    }

    [Fact]
    public async Task ExpiringTheConditionalLayerPreservesAnIndependentlyGrantedRoam()
    {
        var initial = Position("OGN·019/298", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with {
                Tags = [..initial.CardObjects["HOST"].Tags, CardCombatKeywordNames.Roam],
                GrantedTags = [CardCombatKeywordNames.Roam] } } };
        var game = new Driver(initial);
        await Enable(game, "OGN·019/298");
        Assert.Contains(CardCombatKeywordNames.Assault, game.State.CardObjects["HOST"].Tags);
        await game.Act("P1", new EndTurnCommand());
        Assert.Contains(CardCombatKeywordNames.Roam, game.State.CardObjects["HOST"].Tags);
        Assert.DoesNotContain(CardCombatKeywordNames.Assault, game.State.CardObjects["HOST"].Tags);
        await game.Replay();
    }

    [Fact]
    public async Task TurnEndRemovesDamageBeforeTheSalamanderLosesItsBonus()
    {
        var initial = Position("UNL-108/219", "BASE");
        initial = initial with { UntilEndOfTurnEffects = [History("UNL-108/219")],
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { Damage = 4 } } };
        var game = new Driver(initial);
        Assert.Equal(5, ArmamentMightTests.Might(initial));
        await game.Act("P1", new EndTurnCommand());
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Base);
        Assert.Equal(0, game.State.CardObjects["HOST"].Damage);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task PayingThreeForADiscountedFourCostSpellDoesNotEnableTheBeliever()
    {
        var initial = RadGearTestPosition.Put(Position("UNL-004/219"), "DISCOUNT", "OGN·084/298", "P1", "BASE");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HOST", "UNL-004/219", []));
        var mana = game.State.RunePools["P1"].Mana;
        await game.Act("P1", new PlayCardCommand("SPELL", "OGN·154/298", ["ALLY"]));
        Assert.Equal(mana - 3, game.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(History("UNL-004/219"), game.State.UntilEndOfTurnEffects);
        await game.Top();
        AssertPassive(game.State, "UNL-004/219", false);
        await game.Replay();
    }

    [Fact]
    public async Task DiscardingAfterEntryEnablesAnActualMoveBetweenBattlefields()
    {
        var initial = RadUnitEffectTestFixture.Place(Position("OGN·019/298", "BASE"), "HOST", "P1", "BATTLEFIELD", "BF");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsExhausted = false } } };
        await RadGearTestPosition.Reject(initial, "P1", new MoveUnitCommand("HOST", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        var game = new Driver(initial);
        await Enable(game, "OGN·019/298");
        var generation = game.State.CardObjects["HOST"].ObjectGeneration;
        await game.Act("P1", new MoveUnitCommand("HOST", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(generation, game.State.CardObjects["HOST"].ObjectGeneration);
        await game.Replay();
    }

    [Theory]
    [InlineData("powerDelta")]
    [InlineData("effectivePower")]
    [InlineData("sourceCardNo")]
    [InlineData("effectId")]
    [InlineData("sourcePath")]
    [InlineData("condition")]
    [InlineData("lifecycle")]
    public void CompleteRecoveryRejectsForgedConditionalPassiveLayer(string key)
    {
        var state = Position("UNL-004/219", "BASE") with { UntilEndOfTurnEffects = [History("UNL-004/219")] };
        ArmamentMightTests.PublicRecovery(state);
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id,
            id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var effects = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["continuousEffects"]);
            var contribution = Assert.Single(effects, effect => Equals(effect.GetValueOrDefault("effectKind"), "CONDITIONAL_SOURCE_UNIT"));
            var original = contribution[key];
            contribution[key] = key is "powerDelta" or "effectivePower" ? 99 : "FORGED";
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            contribution[key] = original;
        }
    }
}
