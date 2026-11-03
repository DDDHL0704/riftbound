using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class LastBreathMightRepetitionTests
{
    private const string Hero = "SFD·167/221";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ActuallyPlayedAndEquippedPowerfulSourceEarnsIndependentDraws(int amplifiers)
    {
        var game = await Prepare(amplifiers);
        await Destroy(game);
        var captures = Captures(game.State);
        Assert.Equal(amplifiers + 1, captures.Length);
        Assert.All(captures, c => Assert.Equal(5, c.Might));
        Assert.Equal(amplifiers + 1, captures.Select(c => c.Repetition?.Occurrence ?? 0).Distinct().Count());
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        AssertPrivateRepetition(game.State);
        await Resolve(game, amplifiers + 1);
    }

    [Fact]
    public async Task ActuallyCopiedPowerfulTextAndAmplificationProduceFourSeparateSkills()
    {
        var game = await Prepare(1, copied: true);
        await Destroy(game);
        var captures = Captures(game.State);
        Assert.Equal(4, captures.Length);
        Assert.Equal(2, captures.Select(c => c.InstanceId).Distinct().Count());
        Assert.Equal(2, captures.Count(c => c.Origin is not null));
        await Resolve(game, 4);
    }

    [Fact]
    public async Task AmplifiersDoNotCreateASkillBelowTheActualFiveMightThreshold()
    {
        var game = await Prepare(2, sword: "SFD·022/221");
        await Destroy(game);
        Assert.Empty(Captures(game.State)); Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARD_DRAWN"); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReplacementCostsFinishBeforeAmplificationButKeepTheEarlierMightFrame(bool pay)
    {
        // Both amplifiers explicitly have established unit/equipment traits.
        // This is not a claim of actually animating or playing RAD017.
        var initial = Position(2);
        initial = Put(Put(initial, "MORDE", "RAD·020/167", "P1", "BASE"), "MASS", "UNL-180/219", "P1", "HAND");
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key, p => p.Key.StartsWith("AMP-", StringComparison.Ordinal)
            ? p.Value with { Power = 3, Tags = p.Value.Tags.Append(CardObjectTags.UnitCard)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() } : p.Value) };
        var game = await Prepare(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "UNL-180/219", [])); await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        Assert.Contains(request.Options, o => o.Id == "SACRIFICE_RECALL:MORDE:AMP-0");
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "SACRIFICE_RECALL:MORDE:AMP-0" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        var captures = Captures(game.State);
        Assert.Equal(pay ? 2 : 3, captures.Length);
        Assert.All(captures, c => {
            Assert.Equal(5, c.Might);
            Assert.Contains("AMP-0", c.Frame.PlayerZones["P1"].Battlefields);
            Assert.Equal(!pay, c.Repetition!.Frame.PlayerZones["P1"].Battlefields.Contains("AMP-0"));
            Assert.Equal(pay, c.Repetition.Frame.PlayerZones["P1"].Graveyard.Contains("AMP-0"));
        });
        var deaths = game.Events().Where(e => e.Event.Kind == "UNIT_DESTROYED").ToArray();
        if (pay) Assert.True(deaths.Single(e => Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "AMP-0")).Sequence
            < deaths.Single(e => Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "UNIT")).Sequence);
        AssertPrivateRepetition(game.State); await Resolve(game, pay ? 2 : 3);
    }

    [Fact]
    public async Task RecoveryRejectsMissingForgedAndDuplicateOccurrencesAndPublicAuthorityFrames()
    {
        var game = await Prepare(1); await Destroy(game);
        var item = game.State.TriggerQueue[0]; var capture = item.LastBreathMight!; var repetition = capture.Repetition!;
        foreach (var invalid in new[] {
            capture with { Repetition = null }, capture with { Might = 99 },
            capture with { Repetition = repetition with { Occurrence = 2 } },
            capture with { Repetition = repetition with { Frame = repetition.Frame with {
                CardObjects = repetition.Frame.CardObjects.Where(p => p.Key != "AMP-0").ToDictionary(p => p.Key, p => p.Value) } } },
            capture with { Repetition = repetition with { Frame = repetition.Frame with {
                PlayerZones = new Dictionary<string, PlayerZones>(repetition.Frame.PlayerZones) {
                    ["P2"] = repetition.Frame.PlayerZones["P2"] with { Hand = ["P2-D0"] } } } } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = game.State.TriggerQueue.Select(t =>
                t.TriggerId == item.TriggerId ? t with { LastBreathMight = invalid } : t).ToArray() }),
                e => e.Contains("captured last-breath Might", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, item] }),
            e => e.Contains("duplicate captured last-breath Might", StringComparison.Ordinal));
        var snapshots = ResolutionResult.BuildSnapshots(game.State); var prompts = ResolutionResult.BuildPrompts(game.State);
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        var views = game.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, game.State.Tick, 0,
            snapshots[p], game.State.Tick, 0, prompts[p]));
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot))
        {
            var rows = Assert.IsAssignableFrom<IEnumerable<object>>(snapshot.Timing["triggerQueue"]);
            var row = rows.OfType<Dictionary<string, object?>>().Single(p => Equals(p["triggerId"], item.TriggerId));
            var original = row["lastBreathMight"]; row["lastBreathMight"] = capture;
            Assert.Contains(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State, views, spectator),
                e => e.Contains("captured last-breath Might", StringComparison.Ordinal));
            row["lastBreathMight"] = original;
        }
        await Order(game);
        var stack = game.State.StackItems.Single(i => i.LastBreathMight!.Repetition!.Occurrence == repetition.Occurrence);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [item], StackItems = [stack] }),
            e => e.Contains("duplicate captured last-breath Might", StringComparison.Ordinal));
        AssertPrivateRepetition(game.State); await Resolve(game, 2);
    }

    private static MatchState Position(int amplifiers, string sword = "SFD·056/221", bool copied = false)
    {
        var initial = Put(Put(Put(Create("OGN·278/298"), "UNIT", Hero, "P1", "HAND"), "SWORD", sword, "P1", "BASE"),
            "KILL", Kill, "P1", "HAND");
        for (var i = 6; i < 16; i++) initial = Put(initial, "P1-D" + i, "OGN·219/298", "P1", "MAIN_DECK");
        for (var i = 0; i < amplifiers; i++) initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP-" + i, "P1", "BF");
        if (copied) initial = Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        return initial;
    }
    private static Task<Driver> Prepare(int amplifiers, string sword = "SFD·056/221", bool copied = false)
        => Prepare(Position(amplifiers, sword, copied), copied);
    private static async Task<Driver> Prepare(MatchState initial, bool copied = false)
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Hero, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new AssembleEquipmentCommand("SWORD", "UNIT",
            [initial.CardObjects["SWORD"].CardNo == "SFD·056/221" ? "ASSEMBLE_GREEN" : "ASSEMBLE_RED"])); await game.Top();
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        if (copied) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
        return game;
    }
    private static async Task Destroy(Driver game)
    { await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top(); }
    private static Task Order(Driver game) => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask
        : game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
    private static async Task Resolve(Driver game, int count)
    {
        var hand = game.State.PlayerZones["P1"].Hand.Count; await Order(game);
        for (var i = 0; i < count; i++) {
            var prefix = game.Events().Length; await game.Top();
            Assert.Equal(hand + 2 * (i + 1), game.State.PlayerZones["P1"].Hand.Count);
            Assert.Single(game.Events().Skip(prefix), e => e.Event.Kind == "TRIGGER_RESOLVED");
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }
    private static void AssertPrivateRepetition(MatchState state)
    {
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state).Values
            .Append(MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state).SpectatorSnapshot))
            Assert.DoesNotContain("repetition", JsonSerializer.Serialize(snapshot), StringComparison.OrdinalIgnoreCase);
    }
    private static LastBreathMightContext[] Captures(MatchState state) => state.TriggerQueue.Select(i => i.LastBreathMight)
        .Concat(state.StackItems.Select(i => i.LastBreathMight)).OfType<LastBreathMightContext>().ToArray();
}
