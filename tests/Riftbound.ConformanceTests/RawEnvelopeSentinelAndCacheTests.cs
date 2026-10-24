using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Riftbound.Api.Hubs;
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

public sealed class RawEnvelopeSentinelAndCacheTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    [Fact]
    public async Task NativeDecoderSentinelCannotPersistAnUnrecoverableRawTypeMismatch()
    {
        var run = await Run.Create("envelope-decoder-sentinel");
        var before = await run.Recovery("before"); AssertRecovery(before);
        var id = run.State.PlayerZones["p1"].Hand.First(); var card = run.State.CardObjects[id];
        var raw = JsonSerializer.SerializeToElement(new { cmdType = "PLAY_CARD", sourceObjectId = id,
            cardNo = card.CardNo, targetObjectIds = Array.Empty<string>(), repeatChoices = false });
        var mapped = Assert.IsType<UnsupportedCommand>(GameCommandJsonMapper.Map(raw));
        Assert.Equal("MALFORMED_PLAY_CARD", mapped.CmdType);
        var count = run.Entries.Count; var hash = MatchStateHasher.Hash(run.State);
        var first = await run.Send("decoder-sentinel", raw);
        var after = await run.Recovery("after");
        run.Save("sentinel-observation", new { raw, mapped, first, before, after,
            commandsBefore = count, commandsAfter = run.Entries.Count, unchangedState = hash == MatchStateHasher.Hash(run.State),
            note = "Actual hand source and native Mapper sentinel; no direct typed/raw mismatch was manufactured." });
        Assert.False(first.Accepted); Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode);
        Assert.Equal(count, run.Entries.Count); Assert.Empty(first.Events); Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        AssertRecovery(after);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":null}")]
    public async Task ExistingCanonicalIntentConflictKeepsPriorityOverNewEnvelopeValidation(string changedJson)
    {
        var run = await Run.Create("envelope-cache-priority-" + changedJson.Length);
        var before = await run.Recovery("before"); AssertRecovery(before);
        var canonical = JsonSerializer.SerializeToElement(new { cmdType = "AUDIT_UNKNOWN" });
        var first = await run.Send("already-cached", canonical);
        Assert.False(first.Accepted); Assert.Equal(ErrorCodes.UnsupportedCommand, first.ErrorCode);
        var count = run.Entries.Count; var hash = MatchStateHasher.Hash(run.State);
        using var document = JsonDocument.Parse(changedJson); var changed = document.RootElement.Clone();
        var warm = await run.Send("already-cached", changed);
        var after = await run.Recovery("after"); AssertRecovery(after);
        var cold = await run.Send("already-cached", changed);
        run.Save("cache-priority-observation", new { canonical, changed, first, warm, cold, before, after,
            commandsBeforeConflict = count, commandsAfterConflict = run.Entries.Count });
        Assert.Equal(ErrorCodes.ClientIntentConflict, warm.ErrorCode); Assert.Equal(warm.ErrorCode, cold.ErrorCode);
        Assert.Empty(warm.Events); Assert.Empty(cold.Events); Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(cold.State));
    }

    [Fact]
    public async Task UncachedMalformedEnvelopeLeavesItsIntentAvailableForTheOriginalCorrectedWire()
    {
        var run = await Run.Create("envelope-corrected-intent");
        var before = await run.Recovery("before"); AssertRecovery(before);
        using var document = JsonDocument.Parse("{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":null}");
        var malformed = document.RootElement.Clone(); var count = run.Entries.Count;
        var first = await run.Send("correctable-intent", malformed); var countAfterFirst = run.Entries.Count;
        var corrected = JsonSerializer.SerializeToElement(new { cmdType = "AUDIT_UNKNOWN" });
        var second = await run.Send("correctable-intent", corrected);
        var after = await run.Recovery("after");
        run.Save("corrected-intent-observation", new { malformed, corrected, first, second, before, after,
            commandsBefore = count, countAfterFirst, commandsAfter = run.Entries.Count });
        Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode); Assert.Equal(count, countAfterFirst);
        Assert.Equal(ErrorCodes.UnsupportedCommand, second.ErrorCode); Assert.Equal(count + 1, run.Entries.Count);
        AssertRecovery(after);
    }

    private static void AssertRecovery(RecoveryProof proof)
    {
        Assert.Empty(proof.Errors); Assert.Empty(proof.AuditErrors); Assert.True(proof.Replay.IsMatch);
        Assert.Null(proof.RegistryFailure); Assert.Equal(1, proof.StoreLoads);
    }
    private static string Hash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
