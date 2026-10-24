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

public sealed class RawEnvelopeHubIngressTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    [Theory]
    [InlineData("array", "[]", true)]
    [InlineData("missing-type", "{}", true)]
    [InlineData("duplicate-type", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"cmdType\":\"AUDIT_UNKNOWN\"}", true)]
    [InlineData("numeric-prompt", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":7}", true)]
    [InlineData("string-tick", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"snapshotTick\":\"0\"}", true)]
    [InlineData("null-root", "null", true)]
    [InlineData("canonical-unknown", "{\"cmdType\":\"AUDIT_UNKNOWN\"}", false)]
    [InlineData("allowed-null-prompt", "{\"cmdType\":\"AUDIT_UNKNOWN\",\"promptId\":null}", false)]
    public async Task ActualAuthenticatedHubPreservesRecoverabilityAcrossMalformedAndCanonicalEnvelopes(string name, string json, bool malformed)
    {
        var run = await Run.Create("hub-envelope-" + name);
        var before = await run.Recovery("before");
        Assert.Empty(before.Errors); Assert.Empty(before.AuditErrors); Assert.True(before.Replay.IsMatch); Assert.Null(before.RegistryFailure);
        var clients = new HubClients();
        var hub = new GameHub(run.Registry, playerIdentity: new PlayerIdentityService(new InMemoryPlayerIdentityStore()))
            { Context = new Context(), Clients = clients, Groups = new Groups() };
        Assert.True((await hub.Authenticate("p1", "raw-envelope-test-key-1234")).Authenticated);
        using var document = JsonDocument.Parse(json); var raw = document.RootElement.Clone();
        var count = run.Entries.Count; var hash = MatchStateHasher.Hash(run.State);
        var first = await hub.SubmitIntent(run.State.RoomId, "p1", "native-hub-envelope", raw);
        var warm = await hub.SubmitIntent(run.State.RoomId, "p1", "native-hub-envelope", raw);
        var after = await run.Recovery("after");
        run.Save("hub-observation", new { name, malformed, raw, first, warm, before, after,
            commandsBefore = count, commandsAfter = run.Entries.Count, stateHashUnchanged = hash == MatchStateHasher.Hash(run.State),
            callerErrors = clients.CallerClient.Errors, nonErrorMessages = clients.CallerClient.OtherMessages,
            groupMessages = clients.GroupClient.OtherMessages.Concat(clients.GroupClient.Errors),
            actualAuthenticatedHubInvoked = true, networkTransportExecuted = false });
        Assert.False(first.Accepted); Assert.Equal(first.ErrorCode, warm.ErrorCode);
        Assert.Equal("REJECTED", first.State); Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        Assert.Equal(2, clients.CallerClient.Errors.Count); Assert.Empty(clients.CallerClient.OtherMessages);
        Assert.Empty(clients.GroupClient.Errors); Assert.Empty(clients.GroupClient.OtherMessages);
        if (malformed)
        {
            Assert.Equal(ErrorCodes.InvalidPayload, first.ErrorCode); Assert.Equal(count, run.Entries.Count);
        }
        else
        {
            Assert.Equal(ErrorCodes.UnsupportedCommand, first.ErrorCode); Assert.Equal(count + 1, run.Entries.Count);
        }
        Assert.Empty(after.Errors); Assert.Empty(after.AuditErrors); Assert.True(after.Replay.IsMatch); Assert.Null(after.RegistryFailure);
    }

    private static string Hash(object value) => MatchStateHasher.HashValue(JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
