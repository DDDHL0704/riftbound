using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class RadSingularityAdmissionTests
{
    private const string Card = "RAD·069/167";
    private const string Threshold = TriggerKinds.ControllerDrawHandThresholdDiscardDestroyScore;

    [Fact]
    public async Task ActualPlayPaysNineManaAndTwoBlueAndObservesItsOwnEnteredIncarnation()
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39568, printed.Id); Assert.Equal(9, printed.Energy); Assert.Equal(2, printed.ReturnEnergy);
        var game = new Driver(Put(Create(), "SOURCE", Card, "P1", "HAND"));
        await game.Act("P1", new PlayCardCommand("SOURCE", Card, [], Destination: "BASE"));
        Assert.Equal(21, game.State.RunePools["P1"].Mana);
        Assert.Equal(28, game.State.RunePools["P1"].TotalPower);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Base);
        var context = Assert.Single(game.State.StackItems).OutsideHandPlayObserver!;
        Assert.Equal(game.State.CardObjects["SOURCE"].ObjectGeneration, context.Source.Generation);
        Assert.Equal(context.Source, context.Played);
        Assert.Equal($"PRINTED:SOURCE:{context.Source.Generation}", context.RuleTextInstanceId);
        await game.Top();
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(8, 2)] [InlineData(9, 1)]
    public async Task MissingPrintedResourcesRejectTheEntirePlay(int mana, int power)
    {
        var state = Put(Create(), "SOURCE", Card, "P1", "HAND") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(mana, power), ["P2"] = RunePool.Empty } };
        await Reject(state, "P1", new PlayCardCommand("SOURCE", Card, [], Destination: "BASE"));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ActualAssemblyCopiesBothSkillsAndBothThresholdInstancesSurviveTheFirstSelfDestruction(bool image, bool copiedFirst)
    {
        var game = new Driver(Put(WithOrianna(Position(image)), "SONG", "SFD·059/221", "P1", "BASE"));
        await Animate(game);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "SOURCE", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.Equal(Card, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await CastDraw(game);
        var thresholds = game.State.TriggerQueue.Where(i => i.EffectKind == Threshold).ToArray();
        Assert.Equal(2, thresholds.Length);
        Assert.Equal(2, thresholds.Select(i => i.DrawHandThreshold!.RuleTextInstanceId).Distinct().Count());
        Assert.Equal(2, game.State.TriggerQueue.Count(i => i.EffectKind == TriggerKinds.ControllerCardPlayDraw));
        var order = game.State.TriggerQueue.OrderBy(i => i.EffectKind == Threshold ? 0 : 1)
            .ThenBy(i => (i.DrawHandThreshold?.Origin is not null) == copiedFirst ? 0 : 1).ToArray();
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order.Select(i => i.TriggerId).ToArray()));
        await Confirm(game);
        Assert.Equal(copiedFirst, game.State.StackItems[^1].DrawHandThreshold!.Origin is not null);
        await game.Top();
        if (image)
        {
            Assert.DoesNotContain("SOURCE", game.State.CardObjects.Keys);
            foreach (var captured in game.State.StackItems.Where(i => i.OutsideHandPlayObserver is { Origin: null }
                || i.DrawHandThreshold is { Origin: null }))
            {
                var forged = captured.OutsideHandPlayObserver is { } observer
                    ? captured with { OutsideHandPlayObserver = observer with { SourceTokenFactoryCardNo = null } }
                    : captured with { DrawHandThreshold = captured.DrawHandThreshold! with { SourceTokenFactoryCardNo = null } };
                var errors = OfficialInsightAndSpellLockTests.Errors(game.State with {
                    StackItems = game.State.StackItems.Select(i => i == captured ? forged : i).ToArray() });
                Assert.Contains(errors, error => error.Contains("source object SOURCE is missing", StringComparison.Ordinal));
            }
        }
        await game.Top();
        Assert.Equal(2, game.State.PlayerScores["P2"]);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SONG", game.State.PlayerZones["P1"].Base);
        if (image) Assert.DoesNotContain("SOURCE", game.State.CardObjects.Keys);
        else Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); await game.Top();
        Assert.Equal(4, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Fact]
    public async Task ARealRecallResponseHidesTheNewHandIncarnationWithoutCancellingTheEarnedSkills()
    {
        var initial = Put(WithOrianna(Position(handCards: 5)), "RECALL", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); await Animate(game); await CastDraw(game);
        await OrderThresholdFirst(game); await Confirm(game);
        await game.Act("P1", new PlayCardCommand("RECALL", "OGN·104/298", ["SOURCE"]));
        await game.Top();
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Hand);
        var skill = Assert.Single(game.State.StackItems, i => i.EffectKind == Threshold);
        Assert.True(game.State.CardObjects["SOURCE"].ObjectGeneration > skill.DrawHandThreshold!.Source.Generation);
        await game.Top();
        Assert.Equal(1, game.State.PlayerScores["P2"]);
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Contains("SOURCE", game.State.PlayerZones["P1"].Graveyard);
        await game.Top(); Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    private static MatchState Position(bool image = false, int handCards = 6)
    {
        var state = Put(Put(Create(), "SOURCE", Card, "P1", "BASE"), "DRAW", "OGN·048/298", "P1", "HAND");
        state = Put(state, "SETUP", "OGN·219/298", "P1", "HAND");
        for (var i = 0; i < handCards; i++) state = Put(state, "H" + i, "OGN·219/298", "P1", "HAND");
        if (image) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SOURCE"] = state.CardObjects["SOURCE"] with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                CopyFaceTransitions = [new(0, 0, P6TokenFactoryCatalog.ImageTokenCardNo, Card)] } } };
        return state;
    }
    private static MatchState WithOrianna(MatchState state)
        => OriannaEquipmentConsumerFixture.WithResources(state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "RAD·145/167", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } } });
    private static async Task Animate(Driver game)
    {
        // Start with eight cards: the normal turn draw must not meet the
        // nine-other-card condition before the actual main-phase conversion.
        await OriannaEquipmentConsumerFixture.AnimateAtNextOwnMain(game, "SOURCE");
        Assert.Equal(9, game.State.PlayerZones["P1"].Hand.Count);
        // One real play spends a card, then the printed skill draws two while
        // only eight other cards remain. This reaches ten without an early
        // threshold, before Song copies the text or the tested spell is cast.
        await game.Act("P1", new PlayCardCommand("SETUP", "OGN·219/298", [], Destination: "BASE"));
        Assert.Equal(TriggerKinds.ControllerCardPlayDraw, Assert.Single(game.State.StackItems).EffectKind);
        await game.Top();
        Assert.Equal(10, game.State.PlayerZones["P1"].Hand.Count);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
    }
    private static async Task CastDraw(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("DRAW", "OGN·048/298", []));
        await game.Top();
    }
    private static Task OrderThresholdFirst(Driver game)
        => game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue
            .OrderByDescending(i => i.EffectKind == Threshold).Select(i => i.TriggerId).ToArray()));
    private static async Task Confirm(Driver game)
    {
        for (var n = 0; game.State.PendingCardChoice is { } choice; n++)
        {
            Assert.InRange(n, 0, 3);
            await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"]));
        }
    }
}
