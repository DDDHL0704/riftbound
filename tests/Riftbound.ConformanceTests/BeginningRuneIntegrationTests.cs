using System.IO.Compression;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class BeginningRuneIntegrationTests
{
    private static MatchState StartWithAnOpponentRune(MatchState state)
        => state with {
            Phase = MatchPhases.TurnStart, TimingState = TimingStates.NeutralClosed, TurnStartStep = null,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OPP-RUNE"] = new("OPP-RUNE", cardNo: "OGN·007/298", ownerId: "P2", controllerId: "P2",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["OPP-RUNE"] = new("P2", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Base = state.PlayerZones["P2"].Base.Append("OPP-RUNE").ToArray() } } };

    [Theory]
    [InlineData("VEN·005", 2)]
    [InlineData("VEN·006", 4)]
    public async Task TemporaryMightAndGankingExpireAtTheEndOfTheTurn(string cardNo, int printedMight)
    {
        var opened = await Act(BeginningRuneConditionTests.Position(cardNo, 0, 1), "P1", new PassPriorityCommand());
        var resolved = await Top(opened.State);
        var ended = await Act(resolved.State, "P1", new EndTurnCommand());
        Assert.Equal(printedMight, ended.State.CardObjects["SOURCE"].Power);
        Assert.DoesNotContain("ROAM", ended.State.CardObjects["SOURCE"].UntilEndOfTurnEffects);
        Restore(ended.State);
    }

    [Theory]
    [InlineData("VEN·005", false)]
    [InlineData("VEN·006", true)]
    public async Task OnlyTheGrantedGankingPermitsMovingBetweenBattlefields(string cardNo, bool allowed)
    {
        var opened = await Act(BeginningRuneConditionTests.Position(cardNo, 0, 1), "P1", new PassPriorityCommand());
        var resolved = await Top(opened.State);
        var command = new MoveUnitCommand("SOURCE", "BATTLEFIELD:F", "BATTLEFIELD:OTHER", ["ROAM"]);
        var result = await new CoreRuleEngine().ResolveAsync(resolved.State, new("roam", "P1", command.CmdType), command, default);
        Assert.Equal(allowed, result.Accepted);
        if (allowed) Assert.Equal("OTHER", result.State.ObjectLocations["SOURCE"].BattlefieldObjectId);
        else Assert.Equal(MatchStateHasher.Hash(resolved.State), MatchStateHasher.Hash(result.State));
        Restore(result.State);
    }

    [Fact]
    public async Task BeginningMightCrossingQueuesTheExistingMightyConsumer()
    {
        var state = BeginningRuneConditionTests.Position("VEN·006", 0, 1);
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["F"] = state.CardObjects["F"] with { CardNo = "OGN·278/298" },
                ["MIGHTY-LEGEND"] = new("MIGHTY-LEGEND", cardNo: "SFD·205/221", ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["MIGHTY-LEGEND"] = new("P1", "LEGEND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["MIGHTY-LEGEND"] } } };
        var opened = await Act(state, "P1", new PassPriorityCommand());
        Assert.Equal(4, CurrentPowerTargetTests.Snapshot(opened.State).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("SOURCE").GetProperty("effectivePower").GetInt32());
        var resolved = await Top(opened.State);
        var contexts = resolved.State.StackItems.Select(item => item.MightySkill)
            .Concat(resolved.State.TriggerQueue.Select(item => item.MightySkill)).Where(context => context is not null).ToArray();
        if (contexts.Length == 0 && Environment.GetEnvironmentVariable("RIFTBOUND_BEGINNING_RUNE_MIGHTY_DIAGNOSTICS") is { } folder) {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "crossing.json"), JsonSerializer.Serialize(new {
                initial = state, opened = opened.State, resolved = resolved.State, events = resolved.Events,
                powerBefore = CurrentPowerTargetTests.Snapshot(opened.State), powerAfter = CurrentPowerTargetTests.Snapshot(resolved.State)
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        var mighty = Assert.Single(contexts)!;
        Assert.Equal("SOURCE", mighty.Unit.ObjectId);
        Assert.Equal(4, mighty.BeforeMight);
        Assert.Equal(6, mighty.AfterMight);
        Assert.Contains(resolved.Events, e => e.Kind == "TRIGGER_QUEUED" && Equals(e.Payload.GetValueOrDefault("effectKind"), "MIGHTY_TRANSITION_CHANNEL"));
        Restore(resolved.State); ArmamentMightTests.PublicRecovery(resolved.State);
    }

    [Theory]
    [InlineData("VEN·005", 3)]
    [InlineData("VEN·006", 6)]
    public async Task RealOpponentReactionChangesRuneCountWithoutCancelingTheEarnedTrigger(string cardNo, int expectedMight)
    {
        var initial = OfficialHoldSequenceTests.AddUnit(BeginningRuneConditionTests.Position(cardNo, 1, 2),
            "REACTOR", "SFD·125/221", "OTHER", "P2");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FLASH"] = new("FLASH", cardNo: "OGS·011/024", ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.SpellCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FLASH"] = new("P2", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P2"] = initial.PlayerZones["P2"] with { Hand = ["FLASH"] } },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P2"] = new(10, 10, powerByTrait: new Dictionary<string, int> { ["purple"] = 5, ["red"] = 5 }) } };
        var engine = new CoreRuleEngine(); var journal = new Journal(); var session = new MatchSession(initial, engine, journal);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web); var current = initial;
        async Task<ResolutionResult> Submit(string actor, GameCommand command)
        {
            var result = await session.SubmitAsync(actor, "rune-reaction-" + journal.Entries.Count, command,
                JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(result.Accepted, result.ErrorMessage); current = result.State;
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType,
                e.RawCommand, e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) {
                    ErrorCode = e.ErrorCode, TypedCommand = e.TypedCommand, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, index) =>
                new RecoveredEvent(e.StartedEventSequence + index + 1, e.CompletedTick, index, ev))).ToArray();
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, current, engine, default, events);
            Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
            Restore(current); ArmamentMightTests.PublicRecovery(current);
            return result;
        }
        await Submit("P1", new PassPriorityCommand());
        Assert.Single(current.StackItems);
        if (current.PriorityPlayerId == "P1") await Submit("P1", new PassPriorityCommand());
        Assert.Equal("P2", current.PriorityPlayerId);
        var cast = await Submit("P2", new PlayCardCommand("FLASH", "OGS·011/024", ["REACTOR"],
            OptionalCosts: ["RECYCLE_RUNE:P2-CONTROLLED-RUNE-1"]));
        Assert.Contains(cast.Events, e => e.Kind == "RUNE_RECYCLED");
        Assert.Contains("P2-CONTROLLED-RUNE-1", current.PlayerZones["P2"].RuneDeck);
        Assert.Single(current.PlayerZones["P1"].Base, id => id.StartsWith("P1-CONTROLLED-RUNE-", StringComparison.Ordinal));
        Assert.Single(current.PlayerZones["P2"].Base, id => id.StartsWith("P2-CONTROLLED-RUNE-", StringComparison.Ordinal));
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Contains("REACTOR", current.PlayerZones["P2"].Base);
        Assert.Single(current.StackItems);
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        await Submit(current.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal(expectedMight, current.CardObjects["SOURCE"].Power);
        Assert.Equal(MatchPhases.Main, current.Phase);
    }

    [Theory]
    [InlineData("VEN·005", 2, 1)]
    [InlineData("VEN·006", 4, 2)]
    public async Task ARealAttachedTextCopyQueuesAndResolvesIndependently(string cardNo, int printedMight, int bonus)
    {
        var state = ArmamentMightTests.Position("SFD·059/221", cardNo, printedMight);
        state = (await AttachedRuleTextTests.Attach(state)).State;
        state = StartWithAnOpponentRune(state);
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
    [InlineData("VEN·005", 2, 1)]
    [InlineData("VEN·006", 4, 2)]
    public async Task CopiedTriggerSurvivesAnActualEquipmentDetachInResponse(string cardNo, int printedMight, int bonus)
    {
        var state = ArmamentMightTests.Position("SFD·059/221", cardNo, printedMight);
        state = (await AttachedRuleTextTests.Attach(state)).State;
        state = StartWithAnOpponentRune(AttachedRuleTextTests.WithSpell(state, "P1", "SFD·011/221"));
        var opened = await Act(state, "P1", new PassPriorityCommand());
        var copied = Assert.Single(opened.State.TriggerQueue, trigger => trigger.BeginningRune?.Origin is not null);
        var printed = Assert.Single(opened.State.TriggerQueue, trigger => trigger.BeginningRune?.Origin is null);
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand([copied.TriggerId, printed.TriggerId]));
        Assert.NotNull(ordered.State.StackItems.Last().BeginningRune!.Origin);
        var spell = await Act(ordered.State, "P1", new PlayCardCommand("SPELL", "SFD·011/221", ["HOST", "GEAR"]));
        var detached = await Top(spell.State);
        Assert.Null(detached.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Null(detached.State.CardObjects["GEAR"].AttachedRuleText);
        ArmamentMightTests.PublicRecovery(detached.State);
        var first = await Top(detached.State);
        Assert.Equal(printedMight + bonus, first.State.CardObjects["HOST"].Power);
        var second = await Top(first.State);
        Assert.Equal(printedMight + 2 * bonus, second.State.CardObjects["HOST"].Power);
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
    }

    [Theory]
    [InlineData("VEN·005", 2, 1, false)]
    [InlineData("VEN·005", 2, 1, true)]
    [InlineData("VEN·006", 4, 2, false)]
    [InlineData("VEN·006", 4, 2, true)]
    public async Task ActualImageAndItsEphemeralTriggerKeepIndependentOrders(
        string cardNo, int printedMight, int bonus, bool runeFirst)
    {
        var copied = await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position(cardNo));
        const string image = "C-TOKEN-001";
        Assert.Equal(cardNo, copied.State.CardObjects[image].CardNo);
        Assert.Equal(printedMight, copied.State.CardObjects[image].Power);
        var opened = await Act(StartWithAnOpponentRune(copied.State), "P1", new PassPriorityCommand());
        var rune = Assert.Single(opened.State.TriggerQueue, trigger => trigger.BeginningRune is not null);
        var ephemeral = Assert.Single(opened.State.TriggerQueue, trigger => trigger.EphemeralStart is not null);
        var order = runeFirst ? new[] { rune.TriggerId, ephemeral.TriggerId } : [ephemeral.TriggerId, rune.TriggerId];
        var ordered = await Act(opened.State, "P1", new OrderTriggersCommand(order));
        Assert.Equal(runeFirst, ordered.State.StackItems.Last().BeginningRune is not null);
        var first = await Top(ordered.State);
        if (runeFirst) Assert.Equal(printedMight + bonus, first.State.CardObjects[image].Power);
        else {
            Assert.False(first.State.CardObjects.ContainsKey(image));
            var remaining = Assert.Single(first.State.StackItems);
            var captured = remaining.BeginningRune!;
            var fakeFrame = captured.Frame with { CardObjects = new Dictionary<string, CardObjectState>(captured.Frame.CardObjects) {
                [image] = captured.Frame.CardObjects[image] with { TokenFactoryCardNo = "FORGED-TOKEN" } } };
            var forged = first.State with { StackItems = [remaining with { BeginningRune = captured with { Frame = fakeFrame } }] };
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        }
        var componentErrors = OfficialInsightAndSpellLockTests.Errors(first.State);
        if (componentErrors.Count > 0 && Environment.GetEnvironmentVariable("RIFTBOUND_BEGINNING_RUNE_DIAGNOSTICS") is { } folder) {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"{cardNo.Replace('·', '-')}-{runeFirst}.json"),
                JsonSerializer.Serialize(new { state = first.State, componentErrors }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        Assert.True(componentErrors.Count == 0, string.Join("; ", componentErrors));
        Restore(first.State); ArmamentMightTests.PublicRecovery(first.State);
        var second = await Top(first.State);
        Assert.False(second.State.CardObjects.ContainsKey(image));
        Restore(second.State); ArmamentMightTests.PublicRecovery(second.State);
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

    [Fact]
    public async Task PublicBeginningContextRejectsAnExtraPrivateFrame()
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
            payload["beginningRune"] = new Dictionary<string, object?> {
                ["cardNo"] = "VEN·006", ["might"] = 2, ["ganking"] = true,
                ["frame"] = new Dictionary<string, object?> { ["privateSourceId"] = "SHOULD-NOT-BE-PUBLIC" } };
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
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_BEGINNING_RUNE_EVIDENCE");
        var prefix = cardNo == "VEN·005" ? "ven005" : "ven006";
        var current = initial;
        void Export(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(evidence)) return;
            Directory.CreateDirectory(evidence);
            using var file = File.Open(Path.Combine(evidence, prefix + "-" + name + ".json.gz"), FileMode.CreateNew);
            using var zip = new GZipStream(file, CompressionLevel.Optimal);
            JsonSerializer.Serialize(zip, value, value.GetType(), json);
        }
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
            foreach (var id in current.Seats.Keys) {
                Assert.Equal(MatchStateHasher.HashValue(result.Snapshots[id]), MatchStateHasher.HashValue(session.SnapshotFor(id)));
                Assert.Equal(MatchStateHasher.HashValue(result.Prompts[id]), MatchStateHasher.HashValue(session.PromptFor(id)));
            }
            Export($"original-prefix-{commands.Length:D2}", new { initial, frame, replay, originalCommands = journal.Entries.Count });
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
        Export("forged-frame", new { original = opened.State, forged, rejectedReplay, componentValidatorAccepts = true });
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
