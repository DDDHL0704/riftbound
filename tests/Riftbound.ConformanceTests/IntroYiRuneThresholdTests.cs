using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.OfficialGarenConquestBattlefieldCountTests;

namespace Riftbound.ConformanceTests;

public sealed class IntroYiRuneThresholdTests
{
    private const string Yi = "OGS·004/024";

    private static MatchState Position(int runes = 8)
    {
        var state = AttachedRuleTextTests.Position(Yi, 0);
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards["HOST"] = Printed("HOST", Yi, "P1");
        var ids = Enumerable.Range(1, runes).Select(i => "YI-RUNE-" + i).ToArray();
        foreach (var (id, index) in ids.Select((id, index) => (id, index)))
        {
            cards[id] = Printed(id, "UNL-R01", "P1") with { IsExhausted = index % 2 == 0 };
            locations[id] = new("P1", "BASE");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Key == "P1"
                ? pair.Value with { Base = pair.Value.Base.Concat(ids).ToArray() } : pair.Value) };
    }

    [Theory]
    [InlineData(7, 4)] [InlineData(8, 8)] [InlineData(9, 8)]
    public void CurrentPublicRunesIncludeActiveAndDormantRunes(int runes, int expected)
    {
        var state = Position(runes);
        Assert.Equal(expected, ArmamentMightTests.Might(state));
        Assert.Equal(4, state.CardObjects["HOST"].Power);
        OfficialGraveyardRecastTests.Restore(state);
        ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("enemy", 4)] [InlineData("rune-deck", 4)] [InlineData("enemy-owned", 8)]
    public void ThresholdUsesControlAndOnlyRunesInPlay(string branch, int expected)
    {
        var state = Position(); var id = "YI-RUNE-8";
        if (branch == "enemy") state = LocalDestructionRecallTests.At(state, id, "P2", "BASE");
        if (branch == "enemy-owned") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = state.CardObjects[id] with { OwnerId = "P2" } } };
        if (branch == "rune-deck") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(x => x != id).ToArray(), RuneDeck = [..state.PlayerZones["P1"].RuneDeck, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "RUNE_DECK") } };
        Assert.Equal(expected, ArmamentMightTests.Might(state));
        ArmamentMightTests.PublicRecovery(state);
    }

    [Fact]
    public async Task RealPlayRecycleAndRuneCallRecomputeTheBonusWithoutChangingPrintedPower()
    {
        var initial = CurrentControllerDamageBonusTests.Place(Position(), "HOST", "P1", "HAND");
        initial = AttackDamageDistributionTests.Spell(initial, "CALL", "OGN·134/298", "P1");
        var nextRune = initial.PlayerZones["P1"].RuneDeck[0];
        var game = new Driver(initial);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new PlayCardCommand("HOST", Yi, []));
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(8, ArmamentMightTests.Might(game.State));
        await game.Act("P1", new RecycleRuneCommand("YI-RUNE-8"));
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        Assert.Contains("YI-RUNE-8", game.State.PlayerZones["P1"].RuneDeck);
        await game.Act("P1", new PlayCardCommand("CALL", "OGN·134/298", []));
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Top();
        Assert.Equal(8, ArmamentMightTests.Might(game.State));
        Assert.Contains(nextRune, game.State.PlayerZones["P1"].Base);
        Assert.Contains("YI-RUNE-8", game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(7, 4)] [InlineData(8, 12)]
    public async Task ActualSongAssemblyCopiesTheConditionalBonusAndRealDepartureRemovesIt(int runes, int expected)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(runes), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(expected, ArmamentMightTests.Might(game.State));
        Assert.Equal("HOST", game.State.CardObjects["GEAR"].AttachedToObjectId);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(4, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task ARealControlChangeUsesTheNewControllersRunesImmediately()
    {
        var initial = AttackDamageDistributionTests.Spell(
            LocalDestructionRecallTests.At(Position(), "HOST", "P1", "BATTLEFIELD", "F"), "TAKE", "SFD·202/221", "P2");
        initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        Assert.Equal(8, ArmamentMightTests.Might(game.State));
        await game.Act("P2", new PlayCardCommand("TAKE", "SFD·202/221", ["HOST"])); await game.Top();
        Assert.Equal("P2", game.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(4, CurrentPowerTargetTests.DisplayPower(game.State, "HOST"));
        Assert.Equal(4, game.State.CardObjects["HOST"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(7, true)] [InlineData(8, false)]
    public void ServerTargetLegalityUsesTheLiveBonusAsWellAsThePublicProjection(int runes, bool legal)
    {
        // The printed total-might cap is 4; a single Yi is a legal subset only below eight runes.
        var state = AttackDamageDistributionTests.Spell(
            LocalDestructionRecallTests.At(Position(runes), "HOST", "P1", "BATTLEFIELD", "F"), "CAP", "OGN·256/298", "P1");
        var command = new PlayCardCommand("CAP", "OGN·256/298", ["HOST"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.Equal(legal, quote.IsValid);
    }
}
