using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;
using RecoveryProof = Riftbound.ConformanceTests.RawEnvelopeRecoveryProof;
using HubClients = Riftbound.ConformanceTests.RawEnvelopeTestHubClients;
using Context = Riftbound.ConformanceTests.RawEnvelopeTestHubContext;
using Groups = Riftbound.ConformanceTests.RawEnvelopeTestGroups;

namespace Riftbound.ConformanceTests;

public sealed class RawEnvelopeIngressTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> InvalidEnvelopes()
    {
        (string Name, string Json)[] values = [
            ("root-null", "null"), ("root-array", "[]"), ("root-array-items", "[1,2]"),
            ("root-string", "\"wire\""), ("root-number", "7"), ("root-true", "true"), ("root-false", "false"),
            ("type-missing", "{}"), ("type-null", "{\"cmdType\":null}"),
            ("type-number", "{\"cmdType\":7}"), ("type-boolean", "{\"cmdType\":true}"),
            ("type-array", "{\"cmdType\":[]}"), ("type-object", "{\"cmdType\":{}}"),
            ("type-empty", "{\"cmdType\":\"\"}"), ("type-blank", "{\"cmdType\":\" \"}"),
            ("type-leading", "{\"cmdType\":\" END_TURN\"}"), ("type-trailing", "{\"cmdType\":\"END_TURN \"}"),
            ("name-empty", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"\":1}"),
            ("name-blank", "{\"cmdType\":\"AUDIT_UNKNOWN\",\" \":1}"),
            ("name-trim", "{\"cmdType\":\"AUDIT_UNKNOWN\",\" extra \":1}"),
            ("name-duplicate", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"extra\":1,\"extra\":2}"),
            ("type-duplicate", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"cmdType\":\"AUDIT_UNKNOWN\"}"),
            ("prompt-number", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":7}"),
            ("prompt-boolean", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":true}"),
            ("prompt-array", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":[]}"),
            ("prompt-object", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":{}}"),
            ("prompt-blank", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":\" \"}"),
            ("prompt-trim", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":\" stale \"}"),
            ("tick-negative", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":-1}"),
            ("tick-string", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":\"0\"}"),
            ("tick-null", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":null}"),
            ("tick-boolean", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":false}"),
            ("tick-array", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":[]}"),
            ("tick-object", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":{}}"),
            ("tick-fraction", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":1.5}"),
            ("tick-overflow", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":9223372036854775808}")
        ];
        foreach (var (name, json) in values) yield return [name, json];
    }

    public static IEnumerable<object[]> CanonicalUnknownEnvelopes()
    {
        yield return ["minimal", "{\"cmdType\":\"AUDIT_UNKNOWN\"}"];
        yield return ["prompt-null", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":null}"];
        yield return ["prompt-empty", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":\"\"}"];
        yield return ["tick-zero", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":0}"];
        yield return ["tick-max", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":9223372036854775807}"];
        yield return ["extra-value", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"extra\":{\"arbitrary\":[1,null,true]}}"];
    }

    [Theory]
    [MemberData(nameof(InvalidEnvelopes))]
    public async Task MalformedNativeEnvelopeIsRejectedWithoutPoisoningTheOriginalJournal(string name, string json)
    {
        var run = await Run.Create("invalid-envelope-" + name, "P1", "P2");
        var before = await run.Recovery("before");
        Assert.Empty(before.Errors); Assert.Empty(before.AuditErrors); Assert.True(before.Replay.IsMatch);
        Assert.Null(before.RegistryFailure);
        using var document = JsonDocument.Parse(json);
        var raw = document.RootElement.Clone();
        var count = run.Entries.Count; var state = run.State;
        var first = await run.Send("malformed", raw);
        var warm = await run.Send("malformed", raw);
        Assert.Equal(first.Accepted, warm.Accepted); Assert.Equal(first.ErrorCode, warm.ErrorCode);
        Assert.Equal(first.ErrorMessage, warm.ErrorMessage); Assert.Empty(warm.Events);
        var after = await run.Recovery("after");
        run.Save("observation", new { name, raw, mappedType = GameCommandJsonMapper.Map(raw).CmdType,
            first, warm, commandsBefore = count, commandsAfter = run.Entries.Count,
            sameStateReference = ReferenceEquals(state, run.State), unchangedStateHash = MatchStateHasher.Hash(state) == MatchStateHasher.Hash(run.State),
            before, after, originalJournal = run.Entries.ToArray(),
            note = "Actual structural errors are supplied to native registry; no trusted Restore bypass or synthetic raw. C# nullable-absent raw is outside this native-wire case." });
        Assert.False(first.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode);
        Assert.Equal(count, run.Entries.Count);
        Assert.Same(state, run.State); Assert.Empty(first.Events);
        Assert.Equal(Hash(ResolutionResult.BuildSnapshots(state)), Hash(first.Snapshots));
        Assert.Equal(Hash(ResolutionResult.BuildPrompts(state)), Hash(first.Prompts));
        Assert.Empty(after.Errors); Assert.Empty(after.AuditErrors); Assert.True(after.Replay.IsMatch);
        Assert.Null(after.RegistryFailure);
    }

    [Theory]
    [MemberData(nameof(CanonicalUnknownEnvelopes))]
    public async Task CanonicalUnknownOrStaleEnvelopeKeepsItsExistingRejectedAuditAndCache(string name, string json)
    {
        var run = await Run.Create("canonical-envelope-" + name, "P1", "P2");
        using var document = JsonDocument.Parse(json);
        var raw = document.RootElement.Clone(); var count = run.Entries.Count; var hash = MatchStateHasher.Hash(run.State);
        var first = await run.Send("canonical", raw);
        Assert.False(first.Accepted); Assert.Contains(first.ErrorCode, new[] { ErrorCodes.UnsupportedCommand, ErrorCodes.PromptExpired });
        Assert.Empty(first.Events); Assert.Equal(count + 1, run.Entries.Count);
        var warm = await run.Send("canonical", raw);
        Assert.Equal(first.ErrorCode, warm.ErrorCode); Assert.Empty(warm.Events); Assert.Equal(count + 1, run.Entries.Count);
        var recovery = await run.Recovery("canonical-after");
        Assert.Empty(recovery.Errors); Assert.Empty(recovery.AuditErrors); Assert.True(recovery.Replay.IsMatch);
        Assert.Null(recovery.RegistryFailure);
        var cold = await run.Send("canonical", raw);
        Assert.False(cold.Accepted); Assert.Empty(cold.Events); Assert.Equal(count + 1, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(cold.State));
        Assert.Equal(Hash(ResolutionResult.BuildSnapshots(run.State)), Hash(cold.Snapshots));
        Assert.Equal(Hash(ResolutionResult.BuildPrompts(run.State)), Hash(cold.Prompts));
        run.Save("observation", new { name, raw, first, warm, cold, recovery,
            note = "Cold exact error-code preservation is a separately confirmed OPEN contract; this control checks journal/cache/recovery, not closure of that gap." });
    }

    private static string Hash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
