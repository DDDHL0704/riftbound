using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Exact finite QA fixtures for the separate Actions SQL/native entry. Every
// checkpoint replays original empty-room birth; this test starts no SQL/API.
public sealed class ImposedMovementQaBirthTests
{
    [Theory]
    [InlineData("standard-pay")]
    [InlineData("standard-decline")]
    [InlineData("effect-pay")]
    [InlineData("effect-decline")]
    public async Task OriginalRoomBirthPreservesPaymentAcquisitionDeclineAndOriginalReplay(string kind)
    {
        var journal = new Journal(); var room = "imposed-birth-" + kind;
        var session = new MatchSession(room, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await Cold();
        await session.EnsurePlayerAsync("P2", default); await Cold();
        var scenario = "imposed-movement-" + kind;
        var seeded = await session.SeedScenarioAsync("P1", "imposed-original-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage); await Cold();
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:" + scenario }, journal.Entries.Select(e => e.CommandType));
        Assert.Equal("P2", State().CardObjects["QA-PATROL"].ControllerId);
        if (kind.StartsWith("effect", StringComparison.Ordinal))
        {
            await Act("P1", new PlayCardCommand("QA-SPELL", "UNL-054/219", ["QA-D", "QA-D2"], Destination: "BATTLEFIELD:QA-FIELD2"));
            Assert.Equal(new RunePool(66, 0), State().RunePools["P1"]);
            await Act("P1", new PassPriorityCommand()); await Act("P2", new PassPriorityCommand());
            Assert.Equal("P2", State().PendingRuleChoice!.Origin.PriorityPlayerId);
        }
        else await Act("P1", new MoveUnitCommand("QA-A", "BASE", "BATTLEFIELD:QA-FIELD2", SourceObjectIds: ["QA-A", "QA-B"]));
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", State().PendingRuleChoice!.Request.Kind);
        await Decide("PAY", false);
        await Decide(State().PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-LUX:", StringComparison.Ordinal)).Id);
        // CN430.2: the confirmed resource child settles before the still-open
        // movement payment; its original action checkpoint stays unchanged.
        Assert.Equal(new RunePool(66, 0, spellOnlyMana: 2), State().RunePools["P1"]);
        Assert.True(State().CardObjects["QA-LUX"].IsExhausted);
        Assert.DoesNotContain(State().PendingRuleChoice!.Request.Options, o => o.Id == "PAY");
        await Decide(State().PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-STELE:", StringComparison.Ordinal)).Id);
        Assert.Equal("RESOURCE_CONVERSION_AMOUNT", State().PendingRuleChoice!.Request.Kind);
        // This exact intermediate range is the real process-restart checkpoint
        // of the remote entry, not an already committed acquisition snapshot.
        await Decide(RangeOption(65));
        var preview = State().PendingRuleChoice!.Request.Preview!;
        Assert.Equal(2, preview.RunePools["P1"].SpellOnlyMana); Assert.Equal(0, preview.RunePools["P1"].TotalPower);
        Assert.Equal(66, preview.RunePools["P1"].Mana); Assert.False(preview.CardObjects["QA-STELE"].IsExhausted);
        await Quantity(65, "RESOURCE_CONVERSION_AMOUNT");
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", State().PendingRuleChoice!.Request.Kind);
        Assert.Equal(65, State().PendingRuleChoice!.Request.Preview!.RunePools["P1"].TotalPower);
        Assert.Equal(1, State().PendingRuleChoice!.Request.Preview!.RunePools["P1"].Mana);
        Assert.Equal(new RunePool(1, 65, spellOnlyMana: 2), State().RunePools["P1"]);
        var pay = kind.EndsWith("-pay", StringComparison.Ordinal); await Decide(pay ? "PAY" : "DECLINE");
        Assert.Null(State().PendingRuleChoice); Assert.Equal(pay ? 64 : 65, State().RunePools["P1"].TotalPower);
        Assert.Equal(2, State().RunePools["P1"].SpellOnlyMana); Assert.Equal(1, State().RunePools["P1"].Mana);
        Assert.True(State().CardObjects["QA-LUX"].IsExhausted); Assert.True(State().CardObjects["QA-STELE"].IsExhausted);
        Assert.Equal("BASE", State().ObjectLocations["QA-RUNE"].Zone);
        var effect = kind.StartsWith("effect", StringComparison.Ordinal);
        var ids = effect ? new[] { "QA-D", "QA-D2" } : ["QA-A", "QA-B"];
        Assert.All(ids, id => Assert.Equal(pay ? "QA-FIELD2" : effect ? "QA-FIELD1" : null, State().ObjectLocations[id].BattlefieldObjectId));
        Assert.DoesNotContain(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "RUNE_RECYCLED");
        Assert.Single(journal.Entries.SelectMany(e => e.Events), e => e.Kind == "MANA_GAINED");
        Assert.Equal(pay ? 1 : 0, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "COST_PAID"
            && e.Payload.GetValueOrDefault("reason") as string == "MAGESEEKER_PATROL_MOVE_TAX_STATIC"));
        if (effect)
        {
            await Act("P1", new PlayCardCommand("QA-BULLET", "OGN·268/298", ["QA-FIELD1"]));
            await Act("P1", new PassPriorityCommand()); await Act("P2", new PassPriorityCommand());
            await Decide("SPEND_POWER:CHOOSE_AMOUNT"); await Quantity(0, "INSTRUCTION_POWER_PAYMENT");
            Assert.Null(State().PendingRuleChoice); Assert.Equal(pay ? 64 : 65, State().RunePools["P1"].TotalPower);
            Assert.Equal(1, State().RunePools["P1"].SpellOnlyMana);
            Assert.All(new[] { "QA-D", "QA-D2" }, id => Assert.Equal(0, State().CardObjects[id].Damage));
        }
        var historyHash = MatchStateHasher.HashValue(journal.Entries);
        var forged = State() with { RunePools = new Dictionary<string, RunePool>(State().RunePools) {
            ["P1"] = State().RunePools["P1"] with { Power = 99 } } };
        var coherent = Frame(forged); Assert.Empty(coherent.ValidationErrors);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(coherent, new CoreRuleEngine(), default));
        Assert.Equal(historyHash, MatchStateHasher.HashValue(journal.Entries));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output); var assembly = typeof(CoreRuleEngine).Assembly;
            await File.WriteAllTextAsync(Path.Combine(output, "birth-runtime-" + kind + ".json"), JsonSerializer.Serialize(new {
                processId = Environment.ProcessId, assemblyPath = assembly.Location, mvid = assembly.ManifestModule.ModuleVersionId,
                informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant(),
                ruleInputs = RuleInputIdentity.Current, originalBirth = journal.Entries.Take(3).Select(e => e.CommandType),
                commands = journal.Entries.Count, everyAcceptedCheckpointColdReplayed = true, coherentForgeryRefused = true }, Web));
        }

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        string RangeOption(int amount) => State().PendingRuleChoice!.Request.Options.Single(option => {
            var parts = option.Id.Split(':'); return int.Parse(parts[^2]) <= amount && amount <= int.Parse(parts[^1]); }).Id;
        async Task Quantity(int amount, string expectedKind)
        {
            var steps = 0;
            while (State().PendingRuleChoice is { Request: var request } && request.Kind == expectedKind
                && request.Options.All(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)))
            { Assert.True(++steps <= 31); await Decide(RangeOption(amount)); }
        }
        Task Decide(string option, bool accepted = true)
        {
            var request = State().PendingRuleChoice!.Request;
            return Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]), accepted);
        }
        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var hash = MatchStateHasher.Hash(State()); var id = "imposed-birth-" + journal.Entries.Count;
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = State().Tick;
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync(player, id, command, raw, default);
            Assert.Equal(accepted, result.Accepted);
            if (!accepted) { Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); }
            var count = journal.Entries.Count;
            var duplicate = await session.SubmitAsync(player, id, command, raw, default);
            Assert.Equal(accepted, duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            Assert.DoesNotContain("QA-PRIVATE", JsonSerializer.Serialize(result.Snapshots["P1"]));
            if (accepted) await Cold();
        }
        MatchRecoveryFrame Frame(MatchState state)
        {
            var last = journal.Entries[^1];
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
                { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
            var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
            var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
            var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, last.CompletedEventSequence,
                snapshots[id], state.Tick, last.CompletedEventSequence, prompts[id]));
            var spectator = MatchReplayRedactor.BuildSpectatorFrame(room, state.Tick, last.CompletedEventSequence, [], state);
            var errors = MatchRecoveryValidator.Validate(room, last.CompletedEventSequence, commands, events, views, state, state.Tick, spectator);
            var initial = MatchState.Create(room) with { Seed = journal.Entries[0].OpeningSeed };
            return new(room, state.Tick, last.CompletedEventSequence, commands, events, views, errors, state, initial, spectator)
                { OpeningSeed = journal.Entries[0].OpeningSeed };
        }
        async Task Cold()
        {
            var frame = Frame(State()); Assert.Empty(frame.ValidationErrors);
            Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
            session = MatchSession.Restore(frame, new CoreRuleEngine(), journal);
        }
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
