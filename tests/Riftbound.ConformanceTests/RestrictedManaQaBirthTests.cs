using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Preflight of the two finite fixtures used by the independent Actions entry.
// Actual room birth and native commands; this local test makes no SQL/API claim.
public sealed class RestrictedManaQaBirthTests
{
    [Theory]
    [InlineData("ogs", "OGS·014/024")]
    [InlineData("sp6", "VEN·SP6")]
    public async Task OriginalRoomBirthAndEveryRestrictedResourceCheckpointColdReplay(string kind, string card)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESTRICTED_MANA_EVIDENCE") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            var assembly = typeof(CoreRuleEngine).Assembly;
            var sha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant();
            var copied = Path.Combine(AppContext.BaseDirectory, "Riftbound.Engine.dll");
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(copied))).ToLowerInvariant());
            await File.WriteAllTextAsync(Path.Combine(output, "test-runtime-" + kind + ".json"), JsonSerializer.Serialize(new {
                processId = Environment.ProcessId, baseDirectory = AppContext.BaseDirectory, assemblyPath = assembly.Location,
                mvid = assembly.ManifestModule.ModuleVersionId,
                informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = sha, ruleInputs = RuleInputIdentity.Current }, Web));
        }
        var journal = new Journal(); var room = "restricted-original-birth-" + kind;
        var session = new MatchSession(room, new CoreRuleEngine(), journal);
        await session.EnsurePlayerAsync("P1", default); await Cold();
        await session.EnsurePlayerAsync("P2", default); await Cold();
        var scenario = "restricted-mana-lux-" + kind;
        var seeded = await session.SeedScenarioAsync("P1", "restricted-original-seed", scenario,
            JsonSerializer.SerializeToElement(new { cmdType = "DEV_SEED_SCENARIO", scenarioId = scenario }), default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage); await Cold();
        Assert.Equal(new[] { "ROOM_SEAT", "ROOM_SEAT", "DEV_SEED_SCENARIO:" + scenario }, journal.Entries.Select(e => e.CommandType));
        Assert.Equal(card, State().CardObjects["QA-LUX"].CardNo);
        Assert.Equal("P2", State().CardObjects["QA-FIELD"].ControllerId);
        Assert.Equal(0, State().RunePools["P1"].SpellOnlyMana);
        await Act("P1", new ActivateAbilityCommand("QA-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        Assert.Equal(2, State().RunePools["P1"].SpellOnlyMana);
        var historyHash = MatchStateHasher.HashValue(journal.Entries);
        var forged = State() with { RunePools = new Dictionary<string, RunePool>(State().RunePools) {
            ["P1"] = State().RunePools["P1"] with { SpellOnlyMana = 3 } } };
        var coherent = Frame(forged);
        Assert.Empty(coherent.ValidationErrors);
        Assert.NotEmpty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(coherent, new CoreRuleEngine(), default));
        Assert.Equal(historyHash, MatchStateHasher.HashValue(journal.Entries));
        var first = new PlayCardCommand("QA-FIRST", "OGN·268/298", ["QA-FIELD"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(State(), "P1", PlayCostPreviewTests.Request(State(), first));
        Assert.True(quote.CanPay); Assert.Equal(2, quote.Cost!.RemainingMana); Assert.Equal(1, quote.Cost.RemainingSpellOnlyMana);
        await Act("P1", first); await Act("P1", new PassPriorityCommand());
        await Act("P2", new ActivateAbilityCommand("QA-PEER-LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await Act("P2", new PassPriorityCommand());
        var resource = State().PendingRuleChoice!.Request.Options.Single(o => o.Id.StartsWith("RESOURCE:QA-SECOND-LUX:"));
        await Choose(resource.Id);
        Assert.Equal(1, State().RunePools["P1"].SpellOnlyMana);
        Assert.Equal(3, State().PendingRuleChoice!.Request.Preview!.RunePools["P1"].SpellOnlyMana);
        Assert.False(State().CardObjects["QA-SECOND-LUX"].IsExhausted);
        Assert.Equal(2, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "MANA_GAINED"));
        await Choose("SPEND_POWER:0");
        Assert.Equal(3, State().RunePools["P1"].SpellOnlyMana); Assert.True(State().CardObjects["QA-SECOND-LUX"].IsExhausted);
        Assert.Equal(3, journal.Entries.SelectMany(e => e.Events).Count(e => e.Kind == "MANA_GAINED"));
        await Act("P1", new PlayCardCommand("QA-SECOND", "OGN·268/298", ["QA-FIELD"]));
        await Act("P1", new PassPriorityCommand()); await Act("P2", new PassPriorityCommand()); await Choose("SPEND_POWER:0");
        Assert.Equal(2, State().RunePools["P1"].SpellOnlyMana); Assert.Equal(2, State().RunePools["P2"].SpellOnlyMana);
        await Act("P1", new PlayCardCommand("QA-UNIT", "OGN·044/298", []), false);
        // The rejected unit is followed by legal EndTurn in the same session;
        // the subsequent Cold() audits the complete unchanged original history.
        await Act("P1", new EndTurnCommand());
        Assert.All(State().RunePools.Values, p => Assert.Equal(RunePool.Empty, p));
        Assert.Equal(MatchStatuses.InProgress, State().Status); Assert.Equal(MatchPhases.Main, State().Phase);
        Assert.Equal("P2", State().ActivePlayerId); Assert.Equal(0, State().CardObjects["QA-TARGET"].Damage);

        MatchState State() => journal.Entries[^1].AuthoritativeState;
        async Task Act(string player, GameCommand command, bool accepted = true)
        {
            var hash = MatchStateHasher.Hash(State());
            var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            node["promptId"] = session.PromptFor(player).PromptId; node["snapshotTick"] = State().Tick;
            if (!accepted) { node["manaPurpose"] = "PLAY_SPELL"; node["spellOnlyMana"] = 99; }
            var raw = JsonSerializer.SerializeToElement(node); command = GameCommandJsonMapper.Map(raw);
            var id = "restricted-birth-" + journal.Entries.Count;
            var result = await session.SubmitAsync(player, id, command, raw, default);
            Assert.Equal(accepted, result.Accepted);
            if (!accepted) { Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); }
            var count = journal.Entries.Count;
            var duplicate = await session.SubmitAsync(player, id, command, raw, default);
            Assert.Equal(accepted, duplicate.Accepted); Assert.Equal(count, journal.Entries.Count);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            if (accepted) await Cold();
        }
        Task Choose(string option)
        {
            var request = State().PendingRuleChoice!.Request;
            return Act(request.PlayerId, new PayCostCommand(request.Id, "RULE_REPLACEMENT", [option]));
        }
        MatchRecoveryFrame Frame(MatchState state)
        {
            var last = journal.Entries[^1];
            var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
                e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage) { RuleInputs = e.RuleInputs }).ToArray();
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
