using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BeginningRuneIntegrationTests
{
    [Theory]
    [InlineData("VEN·005", 2, 1)]
    [InlineData("VEN·006", 4, 2)]
    public async Task ARealAttachedTextCopyQueuesAndResolvesIndependently(string cardNo, int printedMight, int bonus)
    {
        var state = ArmamentMightTests.Position("SFD·059/221", cardNo, printedMight);
        state = (await AttachedRuleTextTests.Attach(state)).State;
        state = state with {
            Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed, TurnStartStep = null,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OPP-RUNE"] = new("OPP-RUNE", cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OPP-RUNE"] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = state.PlayerZones["P2"].Base.Append("OPP-RUNE").ToArray() } } };
        var opened = await Act(state, "P1", new PassPriorityCommand());
        Assert.Equal(2, opened.State.TriggerQueue.Count);
        Assert.Single(opened.State.TriggerQueue, trigger => trigger.BeginningRune!.Origin is not null);
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(opened.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        Assert.Equal(2, ordered.State.StackItems.Count);
        ArmamentMightTests.PublicRecovery(ordered.State);
        var first = await Top(ordered.State);
        Assert.Equal(printedMight + bonus, first.State.CardObjects["HOST"].Power);
        ArmamentMightTests.PublicRecovery(first.State);
        var second = await Top(first.State);
        Assert.Equal(printedMight + 2 * bonus, second.State.CardObjects["HOST"].Power);
        Assert.Equal(cardNo == "VEN·006", second.State.CardObjects["HOST"].UntilEndOfTurnEffects.Contains("ROAM"));
        Restore(second.State);
        ArmamentMightTests.PublicRecovery(second.State);
    }

    [Theory]
    [InlineData("printing")]
    [InlineData("source-generation")]
    [InlineData("future-tick")]
    [InlineData("rune-condition")]
    [InlineData("private-frame")]
    [InlineData("missing-context")]
    public async Task RecoveryRejectsTamperedCapturedBeginningCondition(string mode)
    {
        var opened = await Act(BeginningRuneConditionTests.Position("VEN·005", 0, 1), "P1", new PassPriorityCommand());
        var state = opened.State;
        var item = Assert.Single(state.StackItems);
        var context = Assert.IsType<BeginningRuneContext>(item.BeginningRune);
        var altered = mode switch {
            "printing" => context with { CardNo = "VEN·006" },
            "source-generation" => context with { Source = context.Source with { Generation = context.Source.Generation + 1 } },
            "future-tick" => context with { CapturedTick = state.Tick + 1 },
            "rune-condition" => context with { Frame = context.Frame with {
                CardObjects = new Dictionary<string, CardObjectState>(context.Frame.CardObjects) {
                    ["P2-CONTROLLED-RUNE-0"] = context.Frame.CardObjects["P2-CONTROLLED-RUNE-0"] with { ControllerId = "P1" } } } },
            "private-frame" => context with { Frame = context.Frame with {
                PlayerZones = new Dictionary<string, PlayerZones>(context.Frame.PlayerZones) {
                    ["P1"] = context.Frame.PlayerZones["P1"] with { Hand = ["PRIVATE"] } } } },
            _ => context
        };
        var forged = state with { StackItems = [item with { BeginningRune = mode == "missing-context" ? null : altered }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        Restore(state);
    }

    [Fact]
    public async Task PlayerAndSpectatorViewsRejectAlteredPublicBeginningContext()
    {
        var opened = await Act(BeginningRuneConditionTests.Position("VEN·006", 0, 1), "P1", new PassPriorityCommand());
        var state = opened.State;
        var snapshots = ResolutionResult.BuildSnapshots(state);
        var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var payload = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            var original = payload["beginningRune"];
            payload["beginningRune"] = new BeginningRunePublicContext("VEN·006", 99, true);
            Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(state, views, spectator));
            payload["beginningRune"] = original;
        }
    }

    [Theory]
    [InlineData("VEN·005", 3)]
    [InlineData("VEN·006", 6)]
    public async Task OriginalControlledCommandsColdRestoreAndReplayEveryPrefix(string cardNo, int finalMight)
    {
        var initial = BeginningRuneConditionTests.Position(cardNo, 0, 1);
        var engine = new CoreRuleEngine();
        var journal = new Journal();
        var session = new MatchSession(initial, engine, journal);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var current = initial;
        async Task<ResolutionResult> Submit(string player, GameCommand command)
        {
            var result = await session.SubmitAsync(player, "beginning-rune-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage);
            current = result.State;
            var commands = Commands(); var events = Events();
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            var views = current.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, current.Tick, events.Length,
                result.Snapshots[id], current.Tick, events.Length, result.Prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(current.RoomId, current.Tick, events.Length, [], current);
            var errors = MatchRecoveryValidator.Validate(current.RoomId, events.Length, commands, events, views, current, current.Tick, spectator);
            var frame = new MatchRecoveryFrame(current.RoomId, current.Tick, events.Length, commands, events, views, errors, current, initial, spectator) {
                OpeningSeed = initial.Seed };
            Assert.True(frame.IsConsistent, string.Join("; ", errors));
            session = MatchSession.Restore(frame, engine, journal);
            return result;
        }
        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
            e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, index) =>
            new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, ev))).ToArray();

        var opened = await Submit("P1", new PassPriorityCommand());
        var item = Assert.Single(opened.State.StackItems);
        var context = item.BeginningRune!;
        var rune = context.Frame.CardObjects["P2-CONTROLLED-RUNE-0"];
        var changedFrame = context.Frame with { CardObjects = new Dictionary<string, CardObjectState>(context.Frame.CardObjects) {
            [rune.ObjectId] = rune with { IsExhausted = !rune.IsExhausted } } };
        var forged = opened.State with { StackItems = [item with { BeginningRune = context with { Frame = changedFrame } }] };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(forged));
        var rejectedReplay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), forged, engine, default, Events());
        Assert.False(rejectedReplay.IsMatch);
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(finalMight, current.CardObjects["SOURCE"].Power);
        Assert.Equal(MatchPhases.Main, current.Phase);
    }

    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
