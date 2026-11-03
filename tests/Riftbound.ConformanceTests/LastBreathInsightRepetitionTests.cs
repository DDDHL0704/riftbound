using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

// Amplifiers and any established unit/equipment traits are explicit public
// fixtures. This does not admit RAD017 or claim its real play/copy is covered.
public sealed class LastBreathInsightRepetitionTests
{
    private const string Visionary = "UNL-062/219";
    private const string Kill = "OGN·229/298";

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task ActuallyPlayedAndCopiedInsightHasIndependentPrivateChoicesAndResponses(int amplifiers, bool copy)
    {
        var initial = Position(amplifiers, copy);
        if (copy) initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key,
            p => p.Key == "UNIT" || p.Key.StartsWith("AMP-", StringComparison.Ordinal)
                ? p.Value with { OwnerId = "P2" } : p.Value) };
        var game = await Prepare(initial, copy); await Destroy(game);
        var count = (amplifiers + 1) * (copy ? 2 : 1);
        Assert.Equal(count, Captures(game.State).Length);
        Assert.Equal(copy ? 2 : 1, Captures(game.State).Select(c => c.Origin?.Equipment.ObjectId ?? "PRINTED").Distinct().Count());
        Assert.All(Captures(game.State), c => { Assert.Equal(2, c.Count); Assert.Equal("P1", c.ControllerId); });
        Assert.Contains("UNIT", game.State.PlayerZones[copy ? "P2" : "P1"].Graveyard);
        await Resolve(game, count);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SimultaneousEquipmentRemovalUsesOneFrameRegardlessOfRemovalOrder(bool amplifierFirst)
    {
        var initial = Put(Position(1), "MASS", "OGN·022/298", "P1", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { Tags = initial.CardObjects["UNIT"].Tags
                .Append(CardObjectTags.EquipmentCard).Distinct().Order(StringComparer.Ordinal).ToArray() } } };
        // The source's established characteristics are a fixture, so put it
        // directly on the field rather than claiming its printed play adds them.
        initial = RadUnitEffectTestFixture.Place(initial, "UNIT", "P1", "BATTLEFIELD", "BF");
        var order = amplifierFirst ? new[] { "AMP-0", "UNIT" } : new[] { "UNIT", "AMP-0" };
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Battlefields = ["BF", ..order] } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "OGN·022/298", [])); await game.Top();
        Assert.Equal(order, game.Events().Where(e => e.Event.Kind is "UNIT_DESTROYED" or "EQUIPMENT_DESTROYED")
            .Select(e => (string)e.Event.Payload["targetObjectId"]!));
        Assert.Equal(2, Captures(game.State).Length);
        Assert.All(Captures(game.State), c => Assert.Contains("AMP-0", c.Repetition!.Frame.PlayerZones["P1"].Battlefields));
        await Resolve(game, 2);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReplacementPaymentRemovesItsAmplifierBeforeInsightCaptures(bool pay)
    {
        var initial = Put(Put(Position(2), "MORDE", "RAD·020/167", "P1", "BASE"), "MASS", "UNL-180/219", "P1", "HAND");
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key, p => p.Key.StartsWith("AMP-", StringComparison.Ordinal)
            ? p.Value with { Power = 3, Tags = p.Value.Tags.Append(CardObjectTags.UnitCard).Distinct().Order(StringComparer.Ordinal).ToArray() } : p.Value) };
        var game = await Prepare(initial);
        await game.Act("P1", new PlayCardCommand("MASS", "UNL-180/219", [])); await game.Top();
        var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", [pay ? "SACRIFICE_RECALL:MORDE:AMP-0" : "DECLINE"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(pay ? 2 : 3, Captures(game.State).Length);
        Assert.All(Captures(game.State), c => {
            Assert.Equal(!pay, c.Repetition!.Frame.PlayerZones["P1"].Battlefields.Contains("AMP-0"));
            Assert.Equal(pay, c.Repetition.Frame.PlayerZones["P1"].Graveyard.Contains("AMP-0"));
        });
        await Resolve(game, pay ? 2 : 3);
    }

    [Fact]
    public async Task UnamplifiedCopiedContextsKeepOriginalHistoryValidationForCoordinatedBindingChanges()
    {
        var initial = Position(0, true); var game = await Prepare(initial, true); await Destroy(game);
        var copied = game.State.TriggerQueue.Single(i => i.InsightContext!.Origin is not null);
        var printed = game.State.TriggerQueue.Single(i => i.InsightContext!.Origin is null);
        foreach (var swap in new[] { false, true })
        {
            var bad = game.State with { TriggerQueue = game.State.TriggerQueue.Select(i => i.TriggerId == copied.TriggerId
                ? i with { InsightContext = i.InsightContext! with { Origin = null } }
                : swap && i.TriggerId == printed.TriggerId
                    ? i with { InsightContext = i.InsightContext! with { Origin = copied.InsightContext!.Origin } } : i).ToArray() };
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(bad));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, game.Commands(), bad,
                new CoreRuleEngine(), default, game.Events());
            Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        }
        await Resolve(game, 2);
    }

    [Fact]
    public async Task RecoveryRejectsMissingForgedOrDuplicatedOccurrencesAcrossQueueAndStack()
    {
        var game = await Prepare(Position(1, true), true); await Destroy(game);
        var item = game.State.TriggerQueue.First(i => i.InsightContext!.Origin is not null);
        var context = item.InsightContext!; var repetition = context.Repetition!; var frame = repetition.Frame;
        foreach (var invalid in new[] {
            context with { Repetition = null },
            context with { Repetition = repetition with { Occurrence = -1 } },
            context with { Repetition = repetition with { Occurrence = 2 } },
            context with { Repetition = repetition with { Frame = frame with { CardObjects = frame.CardObjects
                .Where(p => p.Key != "AMP-0").ToDictionary(p => p.Key, p => p.Value) } } },
            context with { Repetition = repetition with { Frame = frame with { PlayerZones = new Dictionary<string, PlayerZones>(frame.PlayerZones) {
                ["P1"] = frame.PlayerZones["P1"] with { MainDeck = ["P1-D0"] } } } } },
            context with { Repetition = repetition with { Frame = frame with { CardObjects = new Dictionary<string, CardObjectState>(frame.CardObjects) {
                ["UNIT"] = frame.CardObjects["UNIT"] with { ObjectGeneration = context.SourceGeneration + 1 } } } } },
            context with { Origin = context.Origin! with { CapturedTick = game.State.Tick + 1 } } })
        {
            var bad = game.State with { TriggerQueue = game.State.TriggerQueue.Select(i => i.TriggerId == item.TriggerId
                ? i with { InsightContext = invalid } : i).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(bad), e => e.Contains("captured Insight", StringComparison.Ordinal)
                || e.Contains("captured last-breath Insight", StringComparison.Ordinal));
        }
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [..game.State.TriggerQueue, item] }),
            e => e.Contains("duplicate captured last-breath Insight", StringComparison.Ordinal));
        await Order(game);
        var stack = game.State.StackItems.Single(i => i.StackItemId == "ordered-" + item.TriggerId);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { TriggerQueue = [item] }),
            e => e.Contains("duplicate captured last-breath Insight", StringComparison.Ordinal));
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = game.State.StackItems.Select(i =>
            i.StackItemId == stack.StackItemId ? i with { InsightContext = context with { Repetition = null } } : i).ToArray() }),
            e => e.Contains("captured last-breath Insight stack identity", StringComparison.Ordinal));
        await Resolve(game, 4);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PublicQueueAndStackViewsRejectAuthorityFramesForBothPlayersAndSpectators(bool ordered)
    {
        var game = await Prepare(Position(1)); await Destroy(game);
        if (ordered) await Order(game);
        var context = Captures(game.State)[0]; Assert.NotNull(context.Repetition);
        foreach (var viewer in new[] { "P1", "P2", "spectator" })
        {
            var state = game.State; var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, 0,
                snapshots[p], state.Tick, 0, prompts[p]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
            var snapshot = viewer == "spectator" ? spectator.SpectatorSnapshot : snapshots[viewer];
            Assert.DoesNotContain("repetition", JsonSerializer.Serialize(snapshot), StringComparison.OrdinalIgnoreCase);
            var rows = ordered ? snapshot.Stack.Cast<Dictionary<string, object?>>()
                : Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object?>>>(snapshot.Timing["triggerQueue"]);
            var row = rows.First(i => i.ContainsKey("lastBreathInsight")); row["lastBreathInsight"] = context;
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator),
                e => e.Contains("captured last-breath Insight", StringComparison.Ordinal));
        }
        await Resolve(game, 2);
    }

    private static MatchState Position(int amplifiers, bool copy = false)
    {
        var initial = Put(Put(Create("OGN·278/298"), "UNIT", Visionary, "P1", "HAND"), "KILL", Kill, "P1", "HAND");
        for (var i = 0; i < amplifiers; i++) initial = LastBreathRepetitionTests.AddAmplifier(initial, "AMP-" + i, "P1", "BF");
        if (copy) initial = Put(initial, "SONG", "SFD·059/221", "P1", "BASE");
        return initial;
    }
    private static async Task<Driver> Prepare(MatchState initial, bool copy = false)
    {
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Visionary, [], Destination: "BATTLEFIELD:BF"));
        if (copy) { await game.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await game.Top(); }
        return game;
    }
    private static async Task Destroy(Driver game)
    { await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"])); await game.Top(); }
    private static InsightTriggerContext[] Captures(MatchState state) => state.TriggerQueue.Select(i => i.InsightContext)
        .Concat(state.StackItems.Select(i => i.InsightContext)).Where(c => c?.Kind == "LAST_BREATH").Select(c => c!).ToArray();
    private static Task Order(Driver game) => game.State.TriggerQueue.Count == 0 ? Task.CompletedTask
        : game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(i => i.TriggerId).ToArray()));
    private static async Task Resolve(Driver game, int count)
    {
        await Order(game); var hand = game.State.PlayerZones["P1"].Hand.ToArray(); var choices = new HashSet<string>();
        for (var i = 0; i < count; i++)
        {
            var deck = game.State.PlayerZones["P1"].MainDeck.ToArray(); var top = deck.Take(2).ToArray();
            await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("P1", choice.PlayerId); Assert.Equal(top, choice.ContextObjectIds); Assert.True(choices.Add(choice.ChoiceId));
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[2]]));
            var selected = i % 2 == 0 ? top.Take(1).ToArray() : Array.Empty<string>();
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
            var kept = top.Except(selected).Reverse().ToArray();
            if (kept.Length == 2) {
                var order = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
                Assert.Equal("INSIGHT_ORDER", order.ChoiceWindow);
                await game.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, kept));
            }
            Assert.Equal(kept.Concat(deck.Skip(2)), game.State.PlayerZones["P1"].MainDeck.Take(deck.Length - selected.Length));
            Assert.Equal(hand, game.State.PlayerZones["P1"].Hand);
        }
        Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue); Assert.Null(game.State.PendingCardChoice);
        await game.Replay();
    }
}
