using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Npgsql;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class RejectedCommandCacheRecoveryTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BothAuditModelsKeepTheActualNullableCodeEvenWithIgnoreNullSerialization(bool rejected)
    {
        var run = await Run.Create("metadata-present-" + rejected);
        if (rejected) await run.Reject("stale-ready", run.Wire(new ReadyCommand(), stale: true), ErrorCodes.PromptExpired);
        var entry = run.JournalEntries[^1];
        Assert.Equal(rejected ? ErrorCodes.PromptExpired : null, entry.ErrorCode);
        var recovered = run.Frame().Commands[^1];
        foreach (var value in new object[] { entry, recovered })
        foreach (var web in new[] { false, true })
        {
            var options = new JsonSerializerOptions(web ? JsonSerializerDefaults.Web : JsonSerializerDefaults.General) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var wire = JsonSerializer.SerializeToElement(value, value.GetType(), options);
            var property = web ? "errorCode" : "ErrorCode";
            Assert.True(wire.TryGetProperty(property, out var code));
            Assert.Equal(rejected ? JsonValueKind.String : JsonValueKind.Null, code.ValueKind);
            Assert.Equal(rejected ? ErrorCodes.PromptExpired : null, code.ValueKind == JsonValueKind.Null ? null : code.GetString());
            var decoded = JsonSerializer.Deserialize(wire.GetRawText(), value.GetType(), options)!;
            Assert.Equal(entry.ErrorCode, value.GetType().GetProperty("ErrorCode")!.GetValue(decoded));
        }
        await run.Registry(run.Frame());
    }

    public static IEnumerable<object[]> BadCodeMembers()
        => from model in new[] { "journal", "recovered" }
           from json in new[] { "MISSING", "1", "true", "{}", "[]" }
           select new object[] { model, json };

    [Theory]
    [MemberData(nameof(BadCodeMembers))]
    public async Task MissingOrWrongTypeMembersCannotDecodeAsCurrentAuditRecords(string model, string codeJson)
    {
        var run = await Run.Create("metadata-bad-" + model + "-" + codeJson);
        object value = model == "journal" ? run.JournalEntries[^1] : run.Frame().Commands[^1];
        var node = JsonSerializer.SerializeToNode(value, value.GetType(), Web)!.AsObject();
        if (codeJson == "MISSING") Assert.True(node.Remove("errorCode"));
        else node["errorCode"] = JsonNode.Parse(codeJson);
        var raw = node.ToJsonString();
        run.Save("bad-member", new { model, codeJson, raw });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(raw, value.GetType(), Web));
    }

    [Theory]
    [InlineData("null", null)] [InlineData("\"PROMPT_EXPIRED\"", ErrorCodes.PromptExpired)]
    [InlineData("\"FUTURE_CODE\"", "FUTURE_CODE")] [InlineData("\"\"", "")] [InlineData("\" exact \"", " exact ")]
    public void ActualPostgresPayloadReaderKeepsStringsAndExplicitNullWithoutInference(string codeJson, string? expected)
    {
        var method = typeof(PostgresMatchRecoveryStore).GetMethod("ReadCommandErrorCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(expected, method.Invoke(null, ["{\"errorCode\":" + codeJson + "}"]));
    }

    [Theory]
    [InlineData("{}")] [InlineData("[]")] [InlineData("null")]
    [InlineData("{\"errorCode\":1}")] [InlineData("{\"errorCode\":true}")]
    [InlineData("{\"errorCode\":{}}")] [InlineData("{\"errorCode\":[]}")]
    [InlineData("{\"ErrorCode\":\"PROMPT_EXPIRED\"}")]
    public void ActualPostgresPayloadReaderRejectsMissingOrWrongTypeWithoutACompatibilityFallback(string payload)
    {
        var method = typeof(PostgresMatchRecoveryStore).GetMethod("ReadCommandErrorCode", BindingFlags.NonPublic | BindingFlags.Static)!;
        var observed = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [payload]));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, Assert.IsType<MatchSessionException>(observed.InnerException).Code);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualPostgresPayloadWriterIncludesTheActualCodeWithoutAddingAColumn(bool rejected)
    {
        var run = await Run.Create("payload-writer-" + rejected);
        if (rejected) await run.Reject("stale-ready", run.Wire(new ReadyCommand(), stale: true), ErrorCodes.PromptExpired);
        var entry = run.JournalEntries[^1];
        using var command = new NpgsqlCommand();
        typeof(PostgresMatchJournal).GetMethod("AddCommandPayload", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [command, entry]);
        var payload = Assert.IsType<string>(command.Parameters["payload"].Value);
        using var doc = JsonDocument.Parse(payload);
        Assert.True(doc.RootElement.TryGetProperty("errorCode", out var code));
        Assert.Equal(entry.ErrorCode, code.ValueKind == JsonValueKind.Null ? null : code.GetString());
        var read = typeof(PostgresMatchRecoveryStore).GetMethod("ReadCommandErrorCode", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [payload]);
        Assert.Equal(entry.ErrorCode, read);
        run.Save("actual-postgres-payload-codec", new { rejected, payload, decoded = read, noDatabaseExecutionClaimed = true });
    }

    [Theory]
    [InlineData("FORGED_CODE")] [InlineData(null)] [InlineData("prompt_expired")]
    public async Task ChangingOnlyTheRejectedCodeCannotPassIndependentReplayOrTheActualRegistry(string? forgedCode)
    {
        var run = await Run.Create("forged-code-" + (forgedCode ?? "null"));
        var attempt = await run.Reject("stale-ready", run.Wire(new ReadyCommand(), stale: true), ErrorCodes.PromptExpired);
        // ROOM_SEAT uses a server-reserved intent. Exercise an actual client
        // command as the accepted duplicate control, without bypassing that guard.
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var deck = PreconstructedDeckCatalog.Build(catalog).Single(d => d.Id == "lillia-lowcurve").Decklist;
        await run.Act("valid-client-deck", new SubmitDeckCommand(deck.LegendCardNo, deck.ChampionCardNo, deck.MainDeck, deck.RuneDeck, deck.Battlefields));
        await run.VerifyCold(attempt);
        var original = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(run.Frame(), Web), Web)!;
        var rejected = Assert.Single(original.Commands, command => !command.Accepted);
        var forged = original with { Commands = original.Commands.Select(command => command.ClientIntentId == rejected.ClientIntentId
            ? command with { ErrorCode = forgedCode } : command).ToArray() };
        Assert.Equal(ViewHash(original.AuthoritativeState!), ViewHash(forged.AuthoritativeState!));
        Assert.Equal(ViewHash(original.Events), ViewHash(forged.Events)); Assert.Equal(ViewHash(original.ReplayInitialState!), ViewHash(forged.ReplayInitialState!));
        for (var i = 0; i < original.Commands.Count; i++) Assert.Equal(original.Commands[i], forged.Commands[i] with { ErrorCode = original.Commands[i].ErrorCode });
        var structure = MatchRecoveryValidator.Validate(forged.RoomId, forged.LastEventSequence, forged.Commands, forged.Events,
            forged.PlayerViews, forged.AuthoritativeState, forged.CurrentTick, forged.SpectatorReplayFrame);
        Assert.Empty(structure);
        var replay = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(forged, new CoreRuleEngine(), default);
        Assert.Contains(replay, error => error.Contains("error code", StringComparison.Ordinal));
        var failed = await Assert.ThrowsAsync<MatchSessionException>(async () => await run.Registry(forged));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, failed.Code);
        run.Save("code-only-forgery", new { original, forged, structuralErrors = structure, replayErrors = replay, actualRegistryCode = failed.Code });
        var recovered = await run.Registry(original);
        var retried = await Send(recovered, "P1", attempt.Intent, attempt.Raw);
        Assert.Equal(ErrorCodes.PromptExpired, retried.ErrorCode); Assert.False(retried.Accepted); Assert.Empty(retried.Events);
        Assert.Equal(4, run.JournalEntries.Count);
    }
}
