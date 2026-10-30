using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class CopiedMovementObserverTests
{
    [Theory]
    [InlineData(1, false)] [InlineData(1, true)] [InlineData(2, false)] [InlineData(2, true)]
    public async Task EachCopiedHweiDiscardCreatesBothTypeSkillsAndConfirmsRealRuneTargets(int copies, bool reverse)
    {
        var initial = Position("UNL-080/219", copies);
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key.StartsWith("FOOD") ? OfficialGarenConquestBattlefieldCountTests.Printed(p.Key, "VEN·058", "P2") : p.Value) };
        foreach (var id in new[] { "R0", "R1" })
        {
            initial = HandDiscardObserverTests.Put(initial, id, "OGN·007/298", "P2", "BASE");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [id] = initial.CardObjects[id] with { IsExhausted = true } } };
        }
        var game = new Driver(initial);
        try
        {
            await Assemble(game, copies);
            var power = game.State.CardObjects["HOST"].Power;
            await game.Act("P2", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F2", []));
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
            for (var i = 0; i <= copies; i++)
            {
                await game.Top(); var pending = game.State.PendingHandChoice!;
                Assert.Equal("UNIT_MOVED_DRAW_DISCARD_TYPES", pending.ChoiceWindow);
                var source = pending.MovementObserver!;
                await game.Act("P2", new ChooseHandCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["FOOD" + i]));
                var inlines = game.State.TriggerQueue.ToArray(); Assert.Equal(2, inlines.Length);
                Assert.Equal(new[] { 1, 2 }, inlines.Select(t => t.MovementObserver!.DiscardInline!.BranchType).Order().ToArray());
                Assert.All(inlines, t => {
                    Assert.Equal(3, t.MovementObserver!.DiscardInline!.Types);
                    Assert.Equal(source.RuleTextInstanceId, t.MovementObserver.RuleTextInstanceId);
                    Assert.Equal(MatchStateHasher.HashValue(source.Origin), MatchStateHasher.HashValue(t.MovementObserver.Origin));
                });
                var ids = inlines.Select(t => t.TriggerId); if (reverse) ids = ids.Reverse();
                await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: ids.ToArray()));
                for (var branch = 0; branch < 2; branch++)
                {
                    if (game.State.PendingCardChoice is { } runes)
                    {
                        Assert.Equal("MOVEMENT_DISCARD_RUNE_TARGETS", runes.ChoiceWindow);
                        Assert.Contains("R0", runes.LegalObjectIds); Assert.Contains("R1", runes.LegalObjectIds);
                        await game.Act("P2", new ChooseCardsCommand(runes.ChoiceId, runes.ChoiceWindow, ["R0", "R1"]));
                    }
                    await game.Top();
                }
                Assert.Equal(power + 3 * (i + 1), game.State.CardObjects["HOST"].Power);
                Assert.False(game.State.CardObjects["R0"].IsExhausted); Assert.False(game.State.CardObjects["R1"].IsExhausted);
                if (i < copies)
                {
                    await game.Act("P2", new TapRuneCommand("R0", ["R0", "R1"]));
                }
            }
            Assert.Empty(game.State.TriggerQueue); Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingHandChoice);
            Assert.Equal(2 * (copies + 1), game.Events().Count(e => e.Event.Kind == "RUNE_READIED"));
            Assert.Equal(2 * (copies + 1), game.Events().Count(e => e.Event.Kind == "TRIGGER_RESOLVED"
                && Equals(e.Event.Payload.GetValueOrDefault("effectKind"), "MOVEMENT_DISCARD_TYPE_INLINE")));
            await game.Replay();
        }
        finally { Save(game, initial, $"dual-type-{copies}-{reverse}"); }
    }
}
