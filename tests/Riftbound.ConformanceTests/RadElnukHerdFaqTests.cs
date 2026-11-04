using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadElnukHerdFaqTests
{
    private const string Herd = "RAD·081/167";
    private const string Fissure = "VEN·066";

    [Fact]
    public async Task AReplayedHerdIsNotRecycledByItsOlderStandbySkill()
    {
        var initial = StandbySourceRulesTests.Position(Herd);
        initial = AttackDamageDistributionTests.Spell(initial, "FISSURE", Fissure, "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "REPLY", "OGN·048/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = OfficialGarenConquestBattlefieldCountTests.Printed("F", "OGN·278/298", "P1") with { ControllerId = "P1" },
            ["SHADOW"] = OfficialGarenConquestBattlefieldCountTests.Printed("SHADOW", Herd, "P1"),
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", initial.CardObjects["HOST"].CardNo!, "P1") } };
        var game = new Driver(initial) { RecoverAfterEveryCommand = false };
        await game.Act("P1", new HideCardCommand("SHADOW", Herd, "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new HideCardCommand("FISSURE", Fissure, "BATTLEFIELD:F", ["STANDBY_A"]));
        Assert.All(new[] { "SHADOW", "FISSURE" }, id => Assert.True(game.State.CardObjects[id].IsFaceDown));
        await game.ColdCheckpoint();
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", ["SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2"]);
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());

        await game.Act("P1", new PlayCardCommand("SHADOW", Herd, []));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        var oldSkill = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SHADOW");
        Assert.NotNull(oldSkill.PlayedFromStandby);
        var oldGeneration = game.State.CardObjects["SHADOW"].ObjectGeneration;
        await game.ColdCheckpoint();

        await game.Act("P1", new PlayCardCommand("FISSURE", Fissure, ["SHADOW"]));
        Assert.NotNull(Assert.Single(game.State.StackItems, item => item.SourceObjectId == "FISSURE").PlayedFromStandby);
        await game.Top();
        Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Banished);
        Assert.Equal(oldGeneration + 1, game.State.CardObjects["SHADOW"].ObjectGeneration);
        Assert.NotNull(game.State.PendingEffectPlay);
        await game.ColdCheckpoint();
        var replay = new PlayCardCommand("SHADOW", Herd, [], Destination: "BATTLEFIELD:F");
        await Reject(game.State, "P2", replay);
        await Reject(game.State, "P1", replay with { Destination = "BASE" });
        await game.Act("P1", replay);
        Assert.Equal(oldGeneration + 2, game.State.CardObjects["SHADOW"].ObjectGeneration);
        choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var targetPower = game.State.CardObjects["SHADOW-INSIDE"].Power;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHADOW-INSIDE"]));
        var newSkill = Assert.Single(game.State.StackItems, item => item.SourceObjectId == "SHADOW"
            && item.StackItemId != oldSkill.StackItemId);
        Assert.Null(newSkill.PlayedFromStandby);
        Assert.Contains(game.State.StackItems, item => item.StackItemId == oldSkill.StackItemId);
        await game.ColdCheckpoint();

        await game.Top();
        Assert.Equal(targetPower + 3, game.State.CardObjects["SHADOW-INSIDE"].Power);
        Assert.Equal(oldSkill.StackItemId, game.State.StackItems[^1].StackItemId);
        await game.Top();
        // DOC-17 Q2 is unambiguous. Q1's instruction for the old target HOST
        // conflicts with the printed text and remains pending in BACKLOG.
        Assert.Equal(oldGeneration + 2, game.State.CardObjects["SHADOW"].ObjectGeneration);
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["SHADOW"].Zone);
        Assert.Equal("F", game.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Battlefields);
        Assert.DoesNotContain("SHADOW", game.State.PlayerZones["P1"].MainDeck);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "CARDS_RECYCLED"
            && Equals(entry.Event.Payload.GetValueOrDefault("sourceObjectId"), "SHADOW"));
        await game.ColdCheckpoint();
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Contains("SHADOW", game.State.PlayerZones["P1"].Battlefields);
        await game.ColdCheckpoint();
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("invalid-herd-faq", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }
}
