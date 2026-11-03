using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class ImposedMovementSourceTests
{
    [Theory]
    [InlineData(false, false, 1)] [InlineData(false, true, 1)]
    [InlineData(true, false, 1)] [InlineData(true, true, 1)]
    [InlineData(false, false, 2)] [InlineData(false, true, 2)]
    [InlineData(true, false, 2)] [InlineData(true, true, 2)]
    public async Task CopiedFeesFollowTheHostThroughActualStandardAndEffectPaymentAndReplay(
        bool effect, bool friendlyHost, int copies)
    {
        var initial = await CopiedFeeBoundary(friendlyHost, copies);
        var fee = friendlyHost ? 0 : copies + 1;
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(effect ? 4 : 0, fee + (effect ? 1 : 0)) } };
        var game = new CopiedFeeCheckpoint(initial);
        var candidate = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!.Single(c => c.Action == CommandTypes.MoveUnit);
        var costs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["powerPerExtraUnitByDestination"]);
        Assert.Equal(fee, costs["BATTLEFIELD:FIELD2"]);
        if (effect)
        {
            await game.Act("P1", new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"));
            await game.Top();
            if (fee > 0)
            {
                var request = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice).Request;
                Assert.Equal("P1", request.PlayerId);
                Assert.Equal(fee, request.PowerCost);
                await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["PAY"]));
            }
        }
        else
            await game.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        foreach (var id in effect ? new[] { "D", "D2" } : ["A", "B"])
            Assert.Equal("FIELD2", game.State.ObjectLocations[id].BattlefieldObjectId);
        var payments = game.Events().Where(e => e.Event.Kind == "COST_PAID"
            && Equals(e.Event.Payload.GetValueOrDefault("reason"), "MAGESEEKER_PATROL_MOVE_TAX_STATIC")).ToArray();
        Assert.Equal(fee > 0 ? 1 : 0, payments.Length);
        if (fee > 0) Assert.Equal(fee, payments[0].Event.Payload["powerCost"]);
        await game.Replay();
    }

    // Assemble each text instance using real commands, then vary control/owner
    // at an explicit checkpoint. This does not claim native control changes.
    private static async Task<MatchState> CopiedFeeBoundary(bool friendlyHost, int copies)
    {
        var initial = ImposedMovementContinuationTests.Position("standard") with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(copies, copies) } };
        for (var i = 0; i < copies; i++)
            initial = RadGearTestPosition.Put(initial, "COPY-SONG-" + i, "SFD·059/221", "P2", "BASE");
        var setup = new StandbyDefendObserverTests.Driver(initial);
        for (var i = 0; i < copies; i++)
        {
            await setup.Act("P2", new AssembleEquipmentCommand("COPY-SONG-" + i, "PATROL", ["ASSEMBLE_GREEN"]));
            await setup.Top();
            Assert.Equal("UNL-163/219", setup.State.CardObjects["COPY-SONG-" + i].AttachedRuleText!.CardNo);
        }
        await RideTheWindCurrentMovementTests.Replay(initial, setup);
        var state = setup.State;
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        void Control(string id, string controller, string owner)
        {
            cards[id] = cards[id] with { ControllerId = controller, OwnerId = owner };
            foreach (var player in zones.Keys.ToArray())
                zones[player] = zones[player] with { Battlefields = zones[player].Battlefields.Where(item => item != id).ToArray() };
            zones[controller] = zones[controller] with { Battlefields = [..zones[controller].Battlefields, id] };
            locations[id] = new(controller, "BATTLEFIELD", "FIELD2");
        }
        Control("PATROL", friendlyHost ? "P1" : "P2", friendlyHost ? "P2" : "P1");
        for (var i = 0; i < copies; i++)
            Control("COPY-SONG-" + i, friendlyHost ? "P2" : "P1", friendlyHost ? "P1" : "P2");
        cards["FIELD2"] = cards["FIELD2"] with { ControllerId = friendlyHost ? "P1" : "P2" };
        return state with { ActivePlayerId = "P1", TurnPlayerId = "P1", PriorityPlayerId = null,
            CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }

    // A control boundary starts after real assembly, at its original tick.
    // Validate component state/views and replay commands from that checkpoint;
    // do not present this as a room-birth or persisted-journal certification.
    private sealed class CopiedFeeCheckpoint : IMatchJournal
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private readonly MatchState initial;
        private readonly List<MatchJournalEntry> entries = [];
        private MatchSession session;
        public MatchState State { get; private set; }
        public CopiedFeeCheckpoint(MatchState initial)
        {
            this.initial = initial; State = initial;
            session = new(initial, new CoreRuleEngine(), this);
        }
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token)
        { entries.Add(entry); return ValueTask.CompletedTask; }
        private RecoveredCommand[] Commands() => entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId,
            e.CommandType, e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence,
            e.Accepted, e.ErrorMessage) { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        public RecoveredEvent[] Events() => entries.SelectMany(e => e.Events.Select((value, index) =>
            new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, value))).ToArray();
        public async Task Act(string player, GameCommand command)
        {
            var intent = "copied-fee-" + entries.Count;
            var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                JsonSerializer.Serialize(command, command.GetType(), Json), Json)!;
            fields["promptId"] = JsonSerializer.SerializeToElement(session.PromptFor(player).PromptId);
            fields["snapshotTick"] = JsonSerializer.SerializeToElement(State.Tick);
            var raw = JsonSerializer.SerializeToElement(fields);
            var result = await session.SubmitAsync(player, intent, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            State = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State, Json), Json)!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(State));
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            OfficialGraveyardRecastTests.Restore(State);
            ArmamentMightTests.PublicRecovery(State);
            var sequence = entries[^1].CompletedEventSequence;
            var views = State.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, State.Tick, sequence,
                ResolutionResult.BuildSnapshots(State)[id], State.Tick, sequence, ResolutionResult.BuildPrompts(State)[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(State.RoomId, State.Tick, sequence, [], State);
            var errors = MatchRecoveryValidator.ValidateComponentStateAndViews(State);
            Assert.Empty(errors);
            session = MatchSession.Restore(new(State.RoomId, State.Tick, sequence, Commands(), Events(), views,
                errors, State, initial, spectator) { OpeningSeed = initial.Seed }, new CoreRuleEngine(), this);
            var count = entries.Count;
            Assert.True((await session.SubmitAsync(player, intent, command, raw, default)).Accepted);
            Assert.Equal(count, entries.Count);
        }
        public async Task Top()
        {
            await Act(State.PriorityPlayerId!, new PassPriorityCommand());
            await Act(State.PriorityPlayerId!, new PassPriorityCommand());
        }
        public async Task Replay()
        {
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, Commands(), State,
                new CoreRuleEngine(), default, Events());
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
    }
}
