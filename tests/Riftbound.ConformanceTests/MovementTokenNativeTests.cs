using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.LocalDestructionRecallTests;

namespace Riftbound.ConformanceTests;

public sealed class MovementTokenNativeTests
{
    [Theory]
    [InlineData("OGN·222/298", "accept")] [InlineData("OGN·222/298", "decline")]
    [InlineData("SFD·179/221", "accept")] [InlineData("SFD·179/221", "decline")]
    [InlineData("UNL-082/219", "accept")] [InlineData("UNL-082/219", "decline")]
    [InlineData("UNL-082a/219", "accept")] [InlineData("UNL-082a/219", "decline")]
    public async Task ProductionReplacementChoiceRestoresAndReplaysMovementTokenCreation(string card, string branch)
    {
        var s = MovementTokenTests.Position(card);
        foreach (var id in new[] { "Z1", "Z2" }) s = At(DeathObserverAuditTests.Source(s, "UNL-086/219", id), id, "P2", "BATTLEFIELD", "BF");
        var moved = await MovementObserverAuditTests.Move(s);
        var initial = OfficialInsightAndSpellLockTests.Restore(moved.State);
        var journal = new Journal(); var engine = new CoreRuleEngine(); var session = new MatchSession(initial, engine, journal);
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_MOVEMENT_TOKEN_EVIDENCE");
        var directory = root is null ? null : Path.Combine(root, card.Replace("·", "-").Replace("/", "-"), branch);
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string name, object value) { if (directory is null) return; Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, name), JsonSerializer.Serialize(value, value.GetType(), json)); }
        async Task<ResolutionResult> Submit(string player, GameCommand command, JsonElement? raw = null)
        {
            var r = await session.SubmitAsync(player, "movement-token-" + journal.Entries.Count, command,
                raw ?? JsonSerializer.SerializeToElement(command, command.GetType(), json), default);
            Assert.True(r.Accepted, r.ErrorMessage); OfficialGraveyardRecastTests.Restore(r.State); return r;
        }
        var result = await Submit(initial.PriorityPlayerId!, new PassPriorityCommand());
        result = await Submit(result.State.PriorityPlayerId!, new PassPriorityCommand());
        var pending = result.State.PendingCardChoice!; Assert.NotNull(pending); Assert.Empty(MovementTokenTests.Tokens(result.State));
        var prompt = session.PromptFor("P2"); Export("prompt.json", prompt); Export("snapshot.json", result.Snapshots["P2"]);
        GameCommand choice = new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, branch == "accept" ? ["Z2"] : []);
        JsonElement? native = null;
        if (directory is not null && File.Exists(Path.Combine(directory, "native-command.json")))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "native-command.json")));
            native = doc.RootElement.Clone(); choice = GameCommandJsonMapper.Map(native.Value);
            Assert.Equal(prompt.PromptId, native.Value.GetProperty("promptId").GetString()); Assert.Equal(result.State.Tick, native.Value.GetProperty("snapshotTick").GetInt64());
        }
        result = await Submit("P2", choice, native);
        for (var index = 0; result.State.PendingCardChoice is { } next && index < 8; index++)
            result = await Submit("P2", new ChooseCardsCommand(next.ChoiceId, next.ChoiceWindow, branch == "accept" ? ["Z1"] : []));
        Assert.Null(result.State.PendingCardChoice);
        Assert.Empty(result.State.StackItems);
        var tokens = MovementTokenTests.Tokens(result.State); Assert.Equal(MovementTokenTests.Count(card) + (branch == "accept" ? 2 : 0), tokens.Length);
        Assert.All(tokens, t => { Assert.True(t.IsExhausted); Assert.Equal("P2", t.ControllerId); Assert.Equal(MovementTokenTests.Sprite(card) ? "BF" : "BF2", result.State.ObjectLocations[t.ObjectId].BattlefieldObjectId); });
        var commands = journal.Entries.Select(e => new RecoveredCommand(e.PlayerId,e.ClientIntentId,e.CommandType,e.RawCommand,e.StartedTick,e.CompletedTick,e.StartedEventSequence,e.CompletedEventSequence,e.Accepted,e.ErrorMessage)).ToArray();
        var events = journal.Entries.SelectMany(e => e.Events.Select((ev,i) => new RecoveredEvent(e.StartedEventSequence+i+1,e.CompletedTick,i,ev))).ToArray();
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial,commands,result.State,engine,default,events);
        Assert.True(replay.IsMatch,string.Join("; ",replay.Errors)); Export("replay.json",new { nativeCommandUsed=native.HasValue,replay,commands=commands.Length });
    }
    private sealed class Journal : IMatchJournal
    {
        public List<MatchJournalEntry> Entries { get; } = [];
        public ValueTask RecordAsync(MatchJournalEntry entry,CancellationToken token) { Entries.Add(entry); return ValueTask.CompletedTask; }
    }
}
