using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadReadyRestrictionSpellTests
{
    private const string No = "RAD·034/167";

    [Fact]
    public async Task TheOfficialPrintingCombinesReadyMovementProhibitionAndDraw()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(39527, card.Id);
        Assert.Equal("气敛神聚", card.CardName);
        Assert.Equal(1, card.Energy);
        Assert.Null(card.ReturnEnergy);
        Assert.Equal("{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n让一名友方单位变为活跃状态。其在本回合内无法移动。抽一张牌。", card.CardEffect);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResolvingOnAnActiveOrExhaustedUnitStillDrawsAndForbidsMovementUntilTurnEnd(bool exhausted)
    {
        var initial = VenUnitEntryBasicTests.Position(No);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { IsExhausted = exhausted } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", No, ["ALLY"]));
        Assert.Equal(19, game.State.RunePools["P1"].Mana);
        Assert.Equal(exhausted, game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(MovementRestrictionRules.AllPlayers, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains(MovementRestrictionRules.AllPlayers, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.Equal(new[] { "P1-DRAW-1" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(2, game.State.PlayerZones["P1"].MainDeck.Count);
        await Reject(game.State, "P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(MovementRestrictionRules.AllPlayers, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:BF", []));
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        await game.Replay();
    }

    [Theory]
    [InlineData("P1", "OGN·168/298")]
    [InlineData("P2", "OGN·043/298")]
    public async Task AReactionPreventsAnAlreadyDeclaredMovementEffectFromEitherPlayer(string actor, string movement)
    {
        var initial = RadUnitEffectTestFixture.Place(VenUnitEntryBasicTests.Position(No), "ALLY", "P1", "BATTLEFIELD", "BF");
        initial = HandDiscardObserverTests.Put(initial, "MOVE", movement, actor, "HAND")
            with { TurnPlayerId = actor, ActivePlayerId = actor };
        var game = new Driver(initial);
        await game.Act(actor, new PlayCardCommand("MOVE", movement, ["ALLY"]));
        if (actor == "P2") await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("UNIT", No, ["ALLY"]));
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top();
        Assert.Single(game.State.StackItems);
        Assert.Contains(MovementRestrictionRules.AllPlayers, game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        await game.Top();
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["ALLY"].Zone);
        Assert.Equal("BF", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Contains("MOVE", game.State.PlayerZones[actor].Graveyard);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("enemy")]
    [InlineData("equipment")]
    [InlineData("graveyard-target")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("graveyard-source")]
    public async Task IllegalCostsSourcesAndTargetsCannotReadyRestrictOrDraw(string reason)
    {
        var state = VenUnitEntryBasicTests.Position(No);
        if (reason == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        if (reason == "graveyard-source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        var targets = reason switch {
            "enemy" => new[] { "ENEMY" }, "equipment" => ["GEAR"], "graveyard-target" => ["GRAVE"],
            "missing" => [], "extra" => ["ALLY", "ENEMY"], _ => ["ALLY"] };
        await Reject(state, "P1", new PlayCardCommand("UNIT", No, targets));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-centering", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
