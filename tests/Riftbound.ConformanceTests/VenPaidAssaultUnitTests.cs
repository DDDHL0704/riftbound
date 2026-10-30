using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenPaidAssaultUnitTests
{
    [Theory]
    [InlineData("GRAVE", false)] [InlineData("OTHER_GRAVE", false)] [InlineData("GRAVE", true)]
    public async Task MonkPaysExtraManaThenBanishesEitherGraveyardsCardBeforeResponses(string grave, bool decline)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·101", [], OptionalCosts: ["SPEND_MANA:1"]));
        Assert.Equal(17, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        var target = game.State.PendingCardChoice!; Assert.Equal("TRIGGER_CONFIRMATION", target.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY"]));
        var cost = game.State.PendingCardChoice!; Assert.Equal("TRIGGER_COST_CONFIRMATION", cost.ChoiceWindow);
        Assert.Equal(new[] { "GRAVE", "OTHER_GRAVE" }, cost.LegalObjectIds);
        foreach (var id in new[] { "UNIT", "ALLY", "ENEMY", "GEAR" })
            await Reject(game.State, "P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, [id]));
        await Reject(game.State, "P2", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, [grave]));
        await game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, decline ? [] : [grave]));
        if (decline) { Assert.Empty(game.State.StackItems); Assert.Contains(grave, game.State.PlayerZones["P1"].Graveyard); }
        else {
            var owner = grave == "GRAVE" ? "P1" : "P2";
            Assert.Contains(grave, game.State.PlayerZones[owner].Banished); Assert.DoesNotContain(grave, game.State.PlayerZones[owner].Graveyard);
            Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
            Assert.NotNull(Assert.Single(game.State.StackItems).TriggerCost!.Banished);
            await game.Top(); Assert.Contains("COMBAT_KEYWORD:强攻2:1", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
            await game.Act("P1", new EndTurnCommand()); Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        }
        await game.Replay();
    }

    [Fact]
    public async Task DecliningThePlayFeeHasNoTriggerAndAnUnaffordableExtraFeeChangesNothing()
    {
        var initial = Position();
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(2, 0) } },
            "P1", new PlayCardCommand("UNIT", "VEN·101", [], OptionalCosts: ["SPEND_MANA:1"]));
        await Reject(initial, "P1", new PlayCardCommand("UNIT", "VEN·101", [], OptionalCosts: ["SPEND_MANA:2"]));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·101", []));
        Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Fact]
    public async Task PayingThePlayFeeWithNoGraveyardCardStillEntersButCannotConfirmTheSkill()
    {
        var initial = Position();
        initial = initial with { PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key,
            p => p.Value with { Banished = [..p.Value.Banished, ..p.Value.Graveyard], Graveyard = [] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["GRAVE"] = new("P1", "BANISHED"), ["OTHER_GRAVE"] = new("P2", "BANISHED") } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·101", [], OptionalCosts: ["SPEND_MANA:1"]));
        Assert.Equal(17, game.State.RunePools["P1"].Mana); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task ATargetLeavingInResponseDoesNotRefundTheBanishedCardOrBuffItsNewIncarnation()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "BOUNCE", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·101", [], OptionalCosts: ["SPEND_MANA:1"]));
        var target = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY"]));
        var cost = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["GRAVE"]));
        if (game.State.PriorityPlayerId == "P1") await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("BOUNCE", "OGN·104/298", ["ENEMY"])); await game.Top(); await game.Top();
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Banished); Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand);
        Assert.Empty(game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await game.Replay();
    }

    private static MatchState Position()
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·101");
        return initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["OTHER_GRAVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER_GRAVE", "SFD·022/221", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Graveyard = ["OTHER_GRAVE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["OTHER_GRAVE"] = new("P2", "GRAVEYARD") } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-assault", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
