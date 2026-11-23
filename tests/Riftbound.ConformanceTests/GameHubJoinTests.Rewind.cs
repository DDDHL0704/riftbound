using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Fact]
    public async Task RewindAgreementIsBroadcastOnceAndCannotBeSubmittedForAnotherSeat()
    {
        const string room = "RB-REWIND";
        var registry = new InMemoryMatchSessionRegistry(new PlaceholderRuleEngine(), NoopMatchJournal.Instance);
        var alice = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "alice", registry, authenticatedHandle: "alice");
        var bob = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "bob", registry, authenticatedHandle: "bob");
        await alice.JoinRoom(room, "alice");
        await bob.JoinRoom(room, "bob");
        Assert.True((await alice.Ready(room, "alice", "ready")).Accepted);
        Assert.True((await bob.Ready(room, "bob", "ready")).Accepted);
        Assert.True((await alice.EndTurn(room, "alice", "end")).Accepted);
        var session = (await registry.FindAsync(room, default))!;
        var target = session.SnapshotFor("bob").RewindTargets!.Last().Step;
        Assert.True((await alice.SubmitIntent(room, "alice", "request",
            JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.RequestRewind, targetStep = target }))).Accepted);

        var request = session.SnapshotFor("bob").RewindRequest!;
        var accept = JsonSerializer.SerializeToElement(new { cmdType = CommandTypes.AcceptRewind, requestStep = request.RequestStep });
        var spoof = await alice.SubmitIntent(room, "bob", "accept", accept);
        Assert.Equal(ErrorCodes.IdentityMismatch, spoof.ErrorCode);
        Assert.Equal(request, session.SnapshotFor("bob").RewindRequest);

        var bobClients = new RecordingHubClients();
        var answerHub = CreateHub(bobClients, new RecordingGroupManager(), "bob", registry, authenticatedHandle: "bob");
        var agreed = await answerHub.SubmitIntent(room, "bob", "accept", accept);
        Assert.True(agreed.Accepted, agreed.Message);
        Assert.Equal(session.SnapshotFor("alice").Tick, agreed.ServerTick);
        Assert.Contains(EventsFor(bobClients), item => item.Kind == "MATCH_REWOUND");
        var count = bobClients.GroupClient.Snapshots.Count;
        var retry = await answerHub.SubmitIntent(room, "bob", "accept", accept);
        Assert.Equal(agreed.ServerTick, retry.ServerTick);
        Assert.Equal(agreed.Followup?.EventCount, retry.Followup?.EventCount);
        Assert.Equal(count, bobClients.GroupClient.Snapshots.Count);
        Assert.Single(EventsFor(bobClients), item => item.Kind == "MATCH_REWOUND");
    }
}
