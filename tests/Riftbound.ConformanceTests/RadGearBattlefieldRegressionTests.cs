using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadGearBattlefieldRegressionTests
{
    private const string Amplifier = "RAD·017/167";
    private const string SlotMachine = "RAD·111/167";
    private const string RuinedMonastery = "RAD·166/167";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task BlastAmplifierCopiesEveryCapturedLastBreathTriggerWithUniqueIds(bool hasAmplifier, int expected)
    {
        var state = LastBreathDrawBoundaryTests.Position();
        if (hasAmplifier)
        {
            state = LastBreathDrawBoundaryTests.Add(state, "AMP", Amplifier, "P1", "BASE");
            state = RadUnitEffectTestFixture.Place(state, "AMP", "P1", "BATTLEFIELD", "BF");
        }
        state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "BATTLEFIELD", "BF") with {
            TurnPlayerId = "P2", ActivePlayerId = "P2",
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P2"] = new(20, 20)
            }
        };
        if (hasAmplifier)
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["AMP"] = state.CardObjects["AMP"] with { AttachedToObjectId = "UNIT" }
            } };

        var game = new Driver(state);
        await game.Act("P2", new PlayCardCommand("KILL", Kill, ["UNIT"]));
        await game.Top();

        var queued = game.State.TriggerQueue
            .Where(item => item.EffectKind == TriggerKinds.UnitLastBreathDrawOne).ToArray();
        var stacked = game.State.StackItems
            .Where(item => item.EffectKind == TriggerKinds.UnitLastBreathDrawOne).ToArray();
        var triggers = queued.Select(item => item.LastBreathDraw!)
            .Concat(stacked.Select(item => item.LastBreathDraw!)).ToArray();
        Assert.True(triggers.Length == expected,
            $"zones={string.Join(';', game.State.PlayerZones.Select(p => p.Key + ':' + string.Join(',', p.Value.Base) + '/' + string.Join(',', p.Value.Battlefields) + '/' + string.Join(',', p.Value.Graveyard)))} events={string.Join(';', game.Events().Select(e => e.Event.Kind))}");
        Assert.Equal(expected, queued.Select(item => item.TriggerId).Concat(stacked.Select(item => item.StackItemId)).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(hasAmplifier ? new[] { 0, 1 } : new[] { 0 },
            triggers.Select(item => item.CopyOrdinal).Order());

        if (queued.Length > 1)
            await game.Act(queued[0].ControllerId,
                new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(item => item.TriggerId).ToArray()));
        for (var i = 0; i < expected; i++)
            await game.Top();
        Assert.Equal(expected, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task AutomatedSlotMachineQueuesOnceWithoutTheFaceDownBattlefieldCondition()
    {
        var state = BeginningDrawTests.Mushroom(false);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["POUCH"] = state.CardObjects["POUCH"] with { CardNo = SlotMachine }
            }
        };
        var opened = await BeginningDrawTests.Start(state);
        Assert.Equal(1,
            opened.State.TriggerQueue.Count(trigger => trigger.BeginningDraw?.Kind == "RAD111_SLOT_MACHINE")
            + opened.State.StackItems.Count(item => item.BeginningDraw?.Kind == "RAD111_SLOT_MACHINE"));
        Assert.DoesNotContain(opened.State.TriggerQueue, trigger => trigger.BeginningDraw?.Kind == "FACEDOWN_AT_START");
        Assert.DoesNotContain(opened.State.StackItems, item => item.BeginningDraw?.Kind == "FACEDOWN_AT_START");
    }

    [Fact]
    public async Task RuinedMonasteryAllowsDecliningItsOptionalDiscardWithOneCardInHand()
    {
        var opened = await ConquestLifecycleRegressionTests.Conquer(
            ConquestLifecycleRegressionTests.State(RuinedMonastery, handCount: 1));
        opened = await OfficialGraveyardRecastTests.Top(opened.State);
        var choice = Assert.IsType<PendingHandChoiceState>(opened.State.PendingHandChoice);
        Assert.Equal(0, choice.RequiredCount);
        Assert.Equal(1, choice.MaxCount);
        Assert.Single(choice.LegalObjectIds);

        var command = new ChooseHandCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []);
        var declined = await new CoreRuleEngine().ResolveAsync(opened.State,
            new PlayerIntent("rad166-decline", "P1", command.CmdType), command, CancellationToken.None);
        Assert.True(declined.Accepted, declined.ErrorMessage);
        Assert.Null(declined.State.PendingHandChoice);
        Assert.Contains(choice.LegalObjectIds[0], declined.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(declined.Events, e => e.Kind is "CARD_DISCARDED" or "RUNES_CALLED");
    }
}
