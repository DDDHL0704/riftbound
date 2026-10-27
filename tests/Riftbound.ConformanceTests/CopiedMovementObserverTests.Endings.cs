using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedMovementObserverTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ThreeActualMovesProduceEachOrdinalSkillAndAnOrdinaryVictory(int copies)
    {
        var initial = Position("OGN·205/298", copies);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { MovementCountThisTurn = 0 } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 8 - copies - 1 } };
        initial = HandDiscardObserverTests.Put(initial, "SECOND", "OGS·011/024", "P2", "HAND");
        initial = HandDiscardObserverTests.Put(initial, "THIRD", "UNL-202/219", "P2", "HAND");
        var game = new Driver(initial);
        try
        {
            await Assemble(game, copies); await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
            Assert.Empty(game.State.StackItems); Assert.Equal(1, game.State.CardObjects["HOST"].MovementCountThisTurn);
            await game.Act("P2", new PlayCardCommand("SECOND", "OGS·011/024", ["HOST"])); await game.Top();
            Assert.Empty(game.State.StackItems); Assert.Equal(2, game.State.CardObjects["HOST"].MovementCountThisTurn);
            await game.Act("P2", new PlayCardCommand("THIRD", "UNL-202/219", ["HOST", "MOVER0"],
                MovementDestinations: ["BATTLEFIELD:F2", "BATTLEFIELD:F1"])); await game.Top();
            Assert.Equal(3, game.State.CardObjects["HOST"].MovementCountThisTurn);
            Assert.Equal(copies + 1, game.State.TriggerQueue.Count + game.State.StackItems.Count);
            await Finish(game); Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal("P2", game.State.WinnerPlayerId);
            Assert.Equal(8, game.State.PlayerScores["P2"]); Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Equal(copies + 1, game.Events().Count(e => e.Event.Kind == "SCORE_GAINED"
                && Equals(e.Event.Payload.GetValueOrDefault("reason"), "UNIT_MOVED_ORDINAL_SCORE")));
            await game.Replay();
        }
        finally { Save(game, initial, "three-moves-victory-" + copies); }
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SurrenderDuringCopiedMovementDiscardKeepsTheCommittedDrawAndFreezesCapturedEffects(string player)
    {
        var initial = Position("UNL-080/219", 1); var game = new Driver(initial);
        try
        {
            await Assemble(game, 1); await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).Reverse().ToArray()));
            await game.Top();
            Assert.NotNull(game.State.PendingHandChoice); Assert.Equal(5, game.State.PlayerZones["P2"].Hand.Count);
            var pending = game.State.PendingHandChoice!; Assert.NotNull(pending.MovementObserver!.Origin);
            var frozenChoice = MatchStateHasher.HashValue(pending); var frozenStack = MatchStateHasher.HashValue(game.State.StackItems);
            var hand = game.State.PlayerZones["P2"].Hand.ToArray(); var sourcePower = game.State.CardObjects["HOST"].Power;
            await game.Act(player, new SurrenderCommand());
            Assert.Equal(MatchStatuses.Finished, game.State.Status); Assert.Equal(player == "P1" ? "P2" : "P1", game.State.WinnerPlayerId);
            Assert.Equal(frozenChoice, MatchStateHasher.HashValue(game.State.PendingHandChoice));
            Assert.Equal(frozenStack, MatchStateHasher.HashValue(game.State.StackItems)); Assert.Empty(game.State.TriggerQueue);
            Assert.All(ResolutionResult.BuildPrompts(game.State).Values, p => Assert.Equal(new[] { "WAIT" }, p.EnabledActions()));
            Assert.Equal(hand, game.State.PlayerZones["P2"].Hand); Assert.Equal(sourcePower, game.State.CardObjects["HOST"].Power);
            Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DISCARDED");
            var command = new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, [pending.LegalObjectIds[0]]);
            var raw = JsonSerializer.SerializeToNode(command, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            raw["promptId"] = ResolutionResult.BuildPrompts(game.State)["P2"].PromptId; raw["snapshotTick"] = game.State.Tick;
            var before = MatchStateHasher.Hash(game.State); var commands = MatchStateHasher.HashValue(game.Commands()); var events = MatchStateHasher.HashValue(game.Events());
            var rejected = await Assert.ThrowsAsync<MatchSessionException>(() => game.ActRetained(new("P2", "late-discard-after-surrender", JsonSerializer.SerializeToElement(raw)), false));
            Assert.Equal(ErrorCodes.MatchFinished, rejected.Code); Assert.Equal(before, MatchStateHasher.Hash(game.State));
            Assert.Equal(commands, MatchStateHasher.HashValue(game.Commands())); Assert.Equal(events, MatchStateHasher.HashValue(game.Events()));
            await game.Replay();
        }
        finally { Save(game, initial, "pending-discard-surrender-" + player); }
    }
}
