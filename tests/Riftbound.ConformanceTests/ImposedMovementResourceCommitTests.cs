using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class ImposedMovementResourceCommitTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Theory]
    [InlineData("standard", false, false)]
    [InlineData("standard", false, true)]
    [InlineData("standard", true, false)]
    [InlineData("standard", true, true)]
    [InlineData("effect", false, false)]
    [InlineData("effect", false, true)]
    [InlineData("effect", true, false)]
    [InlineData("effect", true, true)]
    public async Task ConfirmedRuneResourcesSettleBeforeMovementAndSurviveDeclineOrConcession(
        string action, bool recycle, bool concede)
    {
        // A finite rule position, not an ordinary deck opening or SQL run.
        var initial = Position(action);
        var driver = await Open(initial, action, $"immediate-{action}-{recycle}-{concede}");
        var original = driver.State.PendingRuleChoice!;
        var originHash = MatchStateHasher.Hash(original.Origin);
        var commandHash = MatchStateHasher.HashValue(new { original.Intent, original.Command });
        var pool = driver.State.RunePools["P1"];
        var result = await Decide(driver, (recycle ? "RECYCLE_RUNE:" : "TAP_RUNE:") + "RUNE");
        driver.Export("confirmed-resource-before-parent.json", new { initial, driver.State, result.Events,
            commands = driver.Commands(), allEvents = driver.Events(), scope = "Explicit finite position; no ordinary birth or external services." });
        Assert.Equal(pool.Mana + (recycle ? 0 : 1), driver.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower + (recycle ? 1 : 0), driver.State.RunePools["P1"].TotalPower);
        Assert.Single(result.Events, e => e.Kind == (recycle ? "RUNE_RECYCLED" : "RUNE_TAPPED"));
        Assert.Equal(originHash, MatchStateHasher.Hash(driver.State.PendingRuleChoice!.Origin));
        Assert.Equal(commandHash, MatchStateHasher.HashValue(new {
            driver.State.PendingRuleChoice.Intent, driver.State.PendingRuleChoice.Command }));
        if (recycle)
        {
            Assert.Equal("RUNE_DECK", driver.State.ObjectLocations["RUNE"].Zone);
            Assert.Contains("RUNE", driver.State.PlayerZones["P1"].RuneDeck);
            Assert.DoesNotContain("RUNE", driver.State.PlayerZones["P1"].Base);
        }
        else Assert.True(driver.State.CardObjects["RUNE"].IsExhausted);
        Assert.DoesNotContain(result.Events, e => e.Kind is "UNIT_MOVED_TO_BATTLEFIELD" or "COST_PAID");
        var finish = concede ? await driver.Act("P1", new SurrenderCommand()) : await Decide(driver, "DECLINE");
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(pool.Mana + (recycle ? 0 : 1), driver.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower + (recycle ? 1 : 0), driver.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain(finish.Events, e => e.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED" or "UNIT_MOVED_TO_BATTLEFIELD");
        Assert.Single(driver.Events(), e => e.Event.Kind == (recycle ? "RUNE_RECYCLED" : "RUNE_TAPPED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaidStandardGroupUsesActualJournalCoordinatesForItsBattleRoles(bool gold)
    {
        var initial = Position("standard");
        if (gold) initial = AddBase(initial, "GOLD", P4ActivatedAbilityCatalog.GoldTokenUnlCardNo);
        var journal = new Journal();
        var session = new MatchSession(initial, new CoreRuleEngine(), journal);
        MatchState State() => journal.Entries.Count == 0 ? initial : journal.Entries[^1].AuthoritativeState;
        async Task Submit(GameCommand command)
        {
            var raw = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
            raw["promptId"] = session.PromptFor("P1").PromptId; raw["snapshotTick"] = State().Tick;
            var payload = JsonSerializer.SerializeToElement(raw);
            var result = await session.SubmitAsync("P1", "role-coordinate-" + journal.Entries.Count,
                GameCommandJsonMapper.Map(payload), payload, default);
            Assert.True(result.Accepted, result.ErrorMessage);
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
        }
        await Submit(new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        var request = State().PendingRuleChoice!.Request;
        var resource = gold ? request.Options.Single(o => o.Id.StartsWith("RESOURCE:GOLD:", StringComparison.Ordinal)).Id : "RECYCLE_RUNE:RUNE";
        await Submit(new PayCostCommand(request.Id, "RULE_REPLACEMENT", [resource]));
        await Submit(new PayCostCommand(State().PendingRuleChoice!.Request.Id, "RULE_REPLACEMENT", ["PAY"]));
        var state = State(); var sequence = journal.Entries[^1].CompletedEventSequence;
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId, e.ClientIntentId, e.CommandType, e.RawCommand,
            e.StartedTick, e.CompletedTick, e.StartedEventSequence, e.CompletedEventSequence, e.Accepted, e.ErrorMessage)
            { TypedCommand = e.TypedCommand, ErrorCode = e.ErrorCode, RuleInputs = e.RuleInputs }).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev, i) => new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, ev))).ToArray();
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence,
            snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, commands, events, views, errors, state, initial, spectator);
        await Save("paid-battle-coordinates-" + (gold ? "gold" : "rune"), new {
            initial, journal.Entries, frame, actualRecoveryErrors = errors,
            scope = "Actual accepted commands from an explicit finite position; no ordinary opening or external services." });
        Assert.Empty(errors);
        Assert.Empty(await MatchActionLogReplayer.ValidateRecoveryFrameAsync(frame, new CoreRuleEngine(), default));
        var roles = Assert.Single(events, e => e.Event.Kind == "BATTLE_ROLES_ESTABLISHED");
        Assert.Equal(state.Tick, roles.Tick);
        Assert.Equal("FIELD2", state.ObjectLocations["A"].BattlefieldObjectId);
        Assert.Equal("FIELD2", state.ObjectLocations["B"].BattlefieldObjectId);
    }

    private static async Task Save(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        var assembly = typeof(CoreRuleEngine).Assembly;
        var path = Path.Combine(folder, name + ".json");
        await using var file = new FileStream(path, FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(file, new {
            actualEngine = new { assembly.Location, mvid = assembly.ManifestModule.ModuleVersionId,
                informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(assembly.Location))).ToLowerInvariant() }, value }, Web);
    }

    private sealed class Journal : IMatchJournal
    {
        internal List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
