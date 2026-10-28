using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class BattleDamageAssignmentLifecycleTests
{
    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public async Task ShiranaMaySpendBoonOnControlledUnitOwnedByOpponent(string player)
    {
        var opponent = ThreeBattlefieldTimingProbe.Other(player);
        var initial = ThreeBattlefieldTimingProbe.Position(player, "OGN·282/298", boon: true);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { OwnerId = opponent, ControllerId = player } } };
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(initial));
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act(player, new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act(player, new PassFocusCommand());
        await game.Act(opponent, new PassFocusCommand());

        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(player, choice.PlayerId);
        Assert.Contains("ALLY", choice.LegalObjectIds);
        var paid = await game.Act(player, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        Assert.Single(paid.Events, e => e.Kind == "BOON_CONSUMED");
        Assert.DoesNotContain(CardObjectTags.Boon, game.State.CardObjects["ALLY"].Tags);
        Assert.Equal(opponent, game.State.CardObjects["ALLY"].OwnerId);
        Assert.Equal(player, game.State.CardObjects["ALLY"].ControllerId);
        var item = Assert.Single(game.State.StackItems, s => s.BattlefieldConquestConsumeBoon is not null);
        Assert.Equal("ALLY", item.TriggerCost!.ConsumedBoon!.ObjectId);
        AssertVayneJsonColdFrame(initial, game);

        var resolved = await game.Top();
        Assert.Single(resolved.Events, e => e.Kind == "CARD_DRAWN"
            && e.Payload.TryGetValue("playerId", out var id) && Equals(id, player));
        Assert.Contains("DRAW1", game.State.PlayerZones[player].Hand);
        AssertVayneJsonColdFrame(initial, game);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), game.State,
            new CoreRuleEngine(), default, JournalRoundTrip(game.Events()));
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }
}
