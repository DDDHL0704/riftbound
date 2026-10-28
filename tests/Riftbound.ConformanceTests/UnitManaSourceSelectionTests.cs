using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class UnitManaSourceSelectionTests
{
    [Theory]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:4", 4, 0)]
    [InlineData("MANA_SOURCE:UNRESTRICTED:4", 0, 4)]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2", 2, 2)]
    public async Task UnitPlayUsesTheSelectedManaSourcesInPreviewCommitAndReplay(string selection, int ordinaryAfter, int restrictedAfter)
    {
        var initial = PlayPosition();
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [selection], Destination: "BASE");
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(ordinaryAfter, quote.Cost!.RemainingMana);
        Assert.Equal(restrictedAfter, quote.Cost.RemainingUnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(ordinaryAfter, game.State.RunePools["P1"].Mana);
        Assert.Equal(restrictedAfter, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID");
        await game.Replay();
    }

    [Theory]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2", 2, 0)]
    [InlineData("MANA_SOURCE:UNRESTRICTED:2", 0, 2)]
    public async Task UnitActivatedAbilityUsesTheSelectedManaSource(string selection, int ordinaryAfter, int restrictedAfter)
    {
        var state = ArmamentMightTests.Position(unit: P4ActivatedAbilityCatalog.ViCardNo, power: 3) with { Tick = 0 };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", P4ActivatedAbilityCatalog.ViCardNo, "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(2, 1, unitPlayOrUnitAbilityOnlyMana: 2) } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("HOST", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [], [selection]));
        Assert.Equal(ordinaryAfter, game.State.RunePools["P1"].Mana);
        Assert.Equal(restrictedAfter, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        await game.Top();
        Assert.Equal(6, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:3")]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:5")]
    [InlineData("MANA_SOURCE:SPELL_ONLY:4")]
    [InlineData("MANA_SOURCE:DUEL_ONLY:4")]
    [InlineData("MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:0,UNRESTRICTED:4")]
    [InlineData("MANA_SOURCE:UNRESTRICTED:2,UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2")]
    [InlineData("MANA_SOURCE:UNRESTRICTED:2147483648")]
    [InlineData("MANA_SOURCE:UNRESTRICTED:2,UNRESTRICTED:2")]
    public async Task InvalidOrIneligibleSelectionCannotChangeState(string selection)
    {
        var initial = PlayPosition();
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: [selection], Destination: "BASE");
        var before = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("invalid-mana-source", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    private static MatchState PlayPosition()
    {
        var state = RestrictedPowerPlayTests.Position("SFD·143/221", false);
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(4, 1, spellOnlyMana: 4, unitPlayOrUnitAbilityOnlyMana: 4) } };
    }
}
