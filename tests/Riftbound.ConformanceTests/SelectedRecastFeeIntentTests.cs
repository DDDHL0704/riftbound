using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.StandbyDefendObserverTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class SelectedRecastFeeIntentTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("wire", null)] [InlineData("typed", null)]
    [InlineData("wrong-player", ErrorCodes.PhaseNotAllowed)]
    [InlineData("wrong-source", ErrorCodes.CardNotInHand)]
    [InlineData("wrong-card", ErrorCodes.InvalidTarget)]
    [InlineData("wrong-mode", ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("wrong-target", ErrorCodes.InvalidTarget)]
    [InlineData("wrong-destination", ErrorCodes.InvalidTarget)]
    [InlineData("zero", ErrorCodes.InvalidTarget)] [InlineData("false-price", ErrorCodes.InvalidTarget)]
    [InlineData("unknown-cost", ErrorCodes.UnsupportedCardBehavior)]
    [InlineData("stale-prompt", ErrorCodes.PromptExpired)] [InlineData("stale-tick", ErrorCodes.PromptExpired)]
    [InlineData("malformed-cost", ErrorCodes.InvalidPayload)] [InlineData("malformed-targets", ErrorCodes.InvalidPayload)]
    [InlineData("empty-source", ErrorCodes.InvalidPayload)] [InlineData("empty-card", ErrorCodes.InvalidPayload)]
    [InlineData("malformed-repeat", ErrorCodes.InvalidPayload)] [InlineData("malformed-movement", ErrorCodes.InvalidPayload)]
    [InlineData("typed-wire-mismatch", ErrorCodes.InvalidPayload)]
    [InlineData("outside-window", ErrorCodes.PhaseNotAllowed)]
    public async Task RefusalUsesCurrentServerChoicesAndRetainsNativeIngressAndOriginalHistoryGuards(string kind, string? error)
    {
        var initial = Position(); var game = new Driver(initial); var engine = new CoreRuleEngine(); var journal = new Journal();
        ResolutionResult? core = null, native = null; JsonElement? wire = null;
        var prefixCommands = Array.Empty<RecoveredCommand>(); var prefixEvents = Array.Empty<RecoveredEvent>();
        try
        {
            if (kind != "outside-window")
            {
                foreach (var i in Enumerable.Range(1, 6)) await game.Act("P1", new TapRuneCommand("R" + i));
                await game.Act("P1", new TapRuneCommand("BLUE"));
                foreach (var id in new[] { "R7", "R8", "RED" }) await game.Act("P1", new RecycleRuneCommand(id));
                await game.Act("P1", new PlayCardCommand("PARENT", "OGN·198/298", ["UNIT"])); await game.Top();
                Assert.NotNull(game.State.PendingEffectPlay); Assert.Equal(1, game.State.RunePools["P1"].Mana);
                Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
            }
            var command = new RefusePlayPaymentCommand("UNIT", "OGN·010/298", [], OptionalCosts: ["HASTE_READY"]);
            command = kind switch {
                "wrong-source" => command with { SourceObjectId = "MISSING" },
                "wrong-card" => command with { CardNo = "OGN·096/298" },
                "wrong-mode" => command with { Mode = "FORGED_MODE" },
                "wrong-target" => command with { TargetObjectIds = ["PARENT"] },
                "wrong-destination" => command with { Destination = "BATTLEFIELD:MISSING" },
                "zero" or "false-price" => command with { OptionalCosts = [] },
                "unknown-cost" => command with { OptionalCosts = ["FORGED_FEE"] },
                _ => command };
            var player = kind == "wrong-player" ? "P2" : "P1";
            prefixCommands = game.Commands(); prefixEvents = game.Events();
            var session = MatchSession.Restore(Clone(Frame(game.State, prefixCommands, prefixEvents)), engine, journal);
            var raw = JsonSerializer.SerializeToNode(command, Json)!.AsObject();
            raw["promptId"] = session.PromptFor(player).PromptId; raw["snapshotTick"] = game.State.Tick;
            switch (kind)
            {
                case "false-price": raw["manaCost"] = 99; raw["powerCost"] = 99; break;
                case "stale-prompt": raw["promptId"] = "expired-refusal"; break;
                case "stale-tick": raw["snapshotTick"] = game.State.Tick - 1; break;
                case "malformed-cost": raw["optionalCosts"] = new JsonArray(42); break;
                case "malformed-targets": raw["targetObjectIds"] = "UNIT"; break;
                case "empty-source": raw["sourceObjectId"] = ""; break;
                case "empty-card": raw["cardNo"] = ""; break;
                case "malformed-repeat": raw["repeatChoices"] = new JsonArray(42); break;
                case "malformed-movement": raw["movementDestinations"] = new JsonArray(42); break;
                case "typed-wire-mismatch": raw["optionalCosts"] = new JsonArray(); break;
            }
            wire = kind == "typed" ? null : JsonSerializer.SerializeToElement(raw, Json);
            GameCommand submitted = kind is "typed" or "typed-wire-mismatch" ? command : GameCommandJsonMapper.Map(wire!.Value);
            var before = MatchStateHasher.Hash(game.State); var pool = game.State.RunePools["P1"];
            var semantic = kind is "wire" or "typed" or "wrong-player" or "wrong-source" or "wrong-card" or "wrong-mode"
                or "wrong-target" or "wrong-destination" or "zero" or "false-price" or "unknown-cost" or "outside-window";
            if (semantic) core = await engine.ResolveAsync(game.State, new("fee-guard-core", player, submitted.CmdType), submitted, default);
            const string intent = "selected-fee-guard";
            native = await session.SubmitAsync(player, intent, submitted, wire, default);
            Assert.Equal(error is null, native.Accepted); Assert.Equal(error, native.ErrorCode);
            if (core is not null) { Assert.Equal(error is null, core.Accepted); Assert.Equal(error, core.ErrorCode); }
            Assert.Equal(before, MatchStateHasher.Hash(game.State));
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(native);
            if (error is not null)
            {
                Assert.Equal(before, MatchStateHasher.Hash(native.State)); Assert.Empty(native.Events);
                if (core is not null) { Assert.Equal(before, MatchStateHasher.Hash(core.State)); Assert.Empty(core.Events); }
            }
            else
            {
                Assert.Null(native.State.PendingEffectPlay); Assert.Contains("UNIT", native.State.PlayerZones["P1"].Graveyard);
                Assert.DoesNotContain(native.State.CardPlays.Finalized, p => p.Source.ObjectId == "UNIT");
                Assert.Contains("PARENT", native.State.PlayerZones["P1"].Graveyard);
                Assert.Equal(pool, native.State.RunePools["P1"]);
            }
            Assert.Equal(error == ErrorCodes.InvalidPayload ? 0 : 1, journal.Entries.Count);
            if (error != ErrorCodes.InvalidPayload)
            {
                var count = journal.Entries.Count;
                var duplicate = await session.SubmitAsync(player, intent, submitted, wire, default);
                Assert.Equal(native.Accepted, duplicate.Accepted); Assert.Equal(native.ErrorCode, duplicate.ErrorCode);
                Assert.Equal(MatchStateHasher.Hash(native.State), MatchStateHasher.Hash(duplicate.State)); Assert.Equal(count, journal.Entries.Count);
                if (error is null)
                {
                    var conflicting = command with { OptionalCosts = [] };
                    JsonElement? conflictWire = null;
                    if (wire is not null) { var conflict = JsonNode.Parse(wire.Value.GetRawText())!.AsObject(); conflict["optionalCosts"] = new JsonArray(); conflictWire = JsonSerializer.SerializeToElement(conflict, Json); }
                    var conflictResult = await session.SubmitAsync(player, intent, conflicting, conflictWire, default);
                    Assert.False(conflictResult.Accepted); Assert.Equal(ErrorCodes.ClientIntentConflict, conflictResult.ErrorCode);
                    Assert.Equal(count, journal.Entries.Count); Assert.Empty(conflictResult.Events);
                }
            }
            var commands = prefixCommands.Concat(Commands()).ToArray(); var events = prefixEvents.Concat(Events()).ToArray();
            var finalFrame = Clone(Frame(native.State, commands, events));
            var restored = await new InMemoryMatchSessionRegistry(engine, NoopMatchJournal.Instance, new Store(finalFrame))
                .GetOrCreateAsync(initial.RoomId, default);
            foreach (var owner in native.State.Seats.Keys)
            {
                Assert.Equal(WireHash(ResolutionResult.BuildSnapshots(native.State)[owner]), WireHash(restored.SnapshotFor(owner)));
                Assert.Equal(WireHash(ResolutionResult.BuildPrompts(native.State)[owner]), WireHash(restored.PromptFor(owner)));
            }
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, native.State, engine, default, events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_SELECTED_FEE_GUARD_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, kind + ".json"), JsonSerializer.Serialize(new {
                    scope = "Explicit turn-five normal-card component, zero initial pools and ten physical runes. Actual native resource/Harrowing prefix. Core/native chosen-fee refusal, independent expected ingress/identity failures, no rejected-request mutation, exact duplicates/conflicts and current-version typed/wire original-history production-registry cold replay. No canonical birth, SQL/SignalR/real restart/full L2/L3 or whole-card claim.",
                    initial, kind, expectedError = error, wire, core, native, commands = prefixCommands.Concat(Commands()), events = prefixEvents.Concat(Events()) }, Json));
            }
        }

        RecoveredCommand[] Commands() => journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        RecoveredEvent[] Events() => journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        string WireHash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, Json));
        MatchRecoveryFrame Clone(MatchRecoveryFrame frame) => JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        MatchRecoveryFrame Frame(MatchState state, RecoveredCommand[] commands, RecoveredEvent[] events)
        {
            var sequence = events.LastOrDefault()?.Sequence ?? 0;
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence, snapshots[id], state.Tick, sequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
            var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator); Assert.Empty(errors);
            return new(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        }
    }

    [Fact]
    public void WireAndTypedJournalRoundTripsPreserveEveryChosenPlayField()
    {
        var expected = new PlayCardCommand("SOURCE", "OGN·093/298", ["TARGET"], "MODE", ["HASTE_READY"], "BATTLEFIELD:FIELD",
            [new("OTHER", ["SECOND"], ["BASE"], "BASE")], ["BATTLEFIELD:DEST"]);
        var command = new RefusePlayPaymentCommand(expected.SourceObjectId, expected.CardNo, expected.TargetObjectIds,
            expected.Mode, expected.OptionalCosts, expected.Destination, expected.RepeatChoices, expected.MovementDestinations);
        var wire = GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(command, Json));
        Assert.Equal(CommandTypes.RefusePlayPayment, wire.CmdType);
        var captured = TypedCommandInput.Capture(wire); Assert.True(captured.TryRestore(out var restored));
        var play = Assert.IsType<RefusePlayPaymentCommand>(restored).AsPlayCard();
        Assert.Equal(MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(expected, Json)), MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(play, Json)));
    }

    private static MatchState Position()
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string zone, string owner = "P1")
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, zone); }
        Put("PARENT", "OGN·198/298", "HAND"); Put("UNIT", "OGN·010/298", "GRAVEYARD");
        Put("PRIVATE", "OGN·013/298", "HAND", "P2");
        foreach (var i in Enumerable.Range(1, 8)) Put("R" + i, "UNL-R05", "BASE");
        Put("RED", "UNL-R01", "BASE"); Put("BLUE", "UNL-R03", "BASE");
        return new("SELECTED-FEE-GUARD", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["PARENT"], Graveyard = ["UNIT"], Base = [..Enumerable.Range(1, 8).Select(i => "R" + i), "RED", "BLUE"] },
                ["P2"] = PlayerZones.Empty with { Hand = ["PRIVATE"] } }, cardObjects: cards, objectLocations: locations, seed: 424242);
    }
    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private sealed class Store(MatchRecoveryFrame frame) : IMatchRecoveryStore
    {
        public ValueTask<MatchRecoveryFrame?> LoadAsync(string roomId, CancellationToken token)
            => ValueTask.FromResult<MatchRecoveryFrame?>(roomId == frame.RoomId ? frame : null);
    }
}
