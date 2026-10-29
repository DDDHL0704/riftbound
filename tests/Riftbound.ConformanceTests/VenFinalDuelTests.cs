using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenFinalDuelTests
{
    [Fact]
    public async Task OfficialFinalDuelHasNoCastTargetsAndRequiresEightManaAndThreeOrangePower()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == "VEN·090");
        Assert.Equal(37786, card.Id); Assert.Equal(8, card.Energy); Assert.Equal(3, card.ReturnEnergy);
        var state = Position();
        await Reject(state, "P1", new PlayCardCommand("UNIT", "VEN·090", ["ALLY"]));
        foreach (var pool in new[] { new RunePool(7, 3), new RunePool(8, 2), new RunePool(8, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 3 }) })
            await Reject(state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } }, "P1", new PlayCardCommand("UNIT", "VEN·090", []));
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·090", []));
        Assert.Equal(12, game.State.RunePools["P1"].Mana); Assert.Equal(17, game.State.RunePools["P1"].TotalPower);
        Assert.Null(game.State.PendingRuleChoice); Assert.Single(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("ALLY", "ENEMY")] [InlineData("ALLY", "FOE")]
    [InlineData("FRIEND", "ENEMY")] [InlineData("FRIEND", "FOE")]
    public async Task EachControllerKeepsOneUnitAcrossBaseAndBattlefieldsBeforeTheOthersAreDestroyed(string friendly, string enemy)
    {
        var state = AddUnit(AddUnit(Position(), "FRIEND", "P1", "BF"), "FOE", "P2", "OTHER");
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·090", [])); await game.Top();
        Assert.Equal("P1", game.State.PendingRuleChoice!.Request.PlayerId);
        Assert.Equal("MASS_DESTRUCTION_SURVIVOR", game.State.PendingRuleChoice.Request.Kind);
        Assert.Equal(new[] { "ALLY", "FRIEND" }, game.State.PendingRuleChoice.Request.Options.Select(o => o.Id));
        await Choose(game, friendly);
        Assert.Equal("P2", game.State.PendingRuleChoice!.Request.PlayerId);
        foreach (var id in new[] { "ALLY", "FRIEND", "ENEMY", "FOE" }) Assert.DoesNotContain(id, game.State.PlayerZones.Values.SelectMany(z => z.Graveyard));
        await Choose(game, enemy);
        foreach (var id in new[] { "ALLY", "FRIEND", "ENEMY", "FOE" }) {
            var kept = id == friendly || id == enemy; var owner = state.CardObjects[id].OwnerId!;
            Assert.Equal(!kept, game.State.PlayerZones[owner].Graveyard.Contains(id));
            Assert.Equal(kept ? 0 : 1, game.State.CardObjects[id].ObjectGeneration);
        }
        Assert.Contains("GEAR", game.State.PlayerZones["P1"].Base); Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "UNIT_DESTROYED")); await game.Replay();
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task APlayerWithoutUnitsMakesNoSelectionAndCannotPreventTheOtherPlayersDestruction(string emptyPlayer)
    {
        var state = AddUnit(AddUnit(Position(), "FRIEND", "P1", "BF"), "FOE", "P2", "OTHER");
        var removed = state.PlayerZones[emptyPlayer].Base.Concat(state.PlayerZones[emptyPlayer].Battlefields)
            .Where(id => state.CardObjects[id].Tags.Contains(CardObjectTags.UnitCard)).ToArray();
        state = state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == emptyPlayer ? p.Value with {
                Base = p.Value.Base.Except(removed).ToArray(), Battlefields = p.Value.Battlefields.Except(removed).ToArray(),
                Banished = [..p.Value.Banished, ..removed] } : p.Value),
            ObjectLocations = state.ObjectLocations.ToDictionary(p => p.Key, p => removed.Contains(p.Key) ? new ObjectLocationState(emptyPlayer, "BANISHED") : p.Value) };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·090", [])); await game.Top();
        var other = emptyPlayer == "P1" ? "P2" : "P1";
        Assert.Equal(other, game.State.PendingRuleChoice!.Request.PlayerId);
        await Choose(game, other == "P1" ? "ALLY" : "ENEMY");
        Assert.Null(game.State.PendingRuleChoice); Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED"); await game.Replay();
    }

    [Fact]
    public async Task TheChoiceDoesNotTargetUnitsOrChargeTheirSpellshieldAndFaceDownCardsStayUntouched()
    {
        var state = AddUnit(AddUnit(Position(), "FOE", "P2", "OTHER"), "SECRET", "P2", "OTHER");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(8, 3) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { Tags = [..state.CardObjects["ALLY"].Tags, CardObjectTags.Spellshield] },
                ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] },
                ["FOE"] = state.CardObjects["FOE"] with { Tags = [..state.CardObjects["FOE"].Tags, CardObjectTags.Spellshield] },
                ["SECRET"] = state.CardObjects["SECRET"] with { IsFaceDown = true } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·090", [])); await game.Top();
        await Choose(game, "ALLY");
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "SECRET");
        await Choose(game, "FOE");
        Assert.True(game.State.CardObjects["SECRET"].IsFaceDown); Assert.Contains("SECRET", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base); Assert.Contains("FOE", game.State.PlayerZones["P2"].Battlefields);
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Fact]
    public async Task IllegalSurvivorsAndTheWrongPlayerCannotAdvanceOrChangeThePendingResolution()
    {
        var game = new Driver(Position()); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·090", [])); await game.Top();
        var pending = game.State.PendingRuleChoice!;
        foreach (var (player, selection) in new[] { ("P2", "ALLY"), ("P1", "ENEMY"), ("P1", "GEAR"), ("P1", "GRAVE") })
            await Reject(game.State, player, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [selection]));
        await Choose(game, "ALLY"); await Choose(game, "ENEMY"); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    private static MatchState Position() => VenUnitEntryBasicTests.Position("VEN·090");
    private static MatchState AddUnit(MatchState state, string id, string player, string field)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", player) },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == player ? p.Value with { Battlefields = [..p.Value.Battlefields, id] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, "BATTLEFIELD", field) } };
    private static Task Choose(Driver game, string selection)
    { var p = game.State.PendingRuleChoice!; return game.Act(p.Request.PlayerId, new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [selection])); }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-final-duel", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
