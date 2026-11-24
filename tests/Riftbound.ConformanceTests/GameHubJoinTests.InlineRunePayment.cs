using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubmitIntentAcceptsTapThenRecycleInOnePaymentAgainstTheCurrentPrompt(bool activateAbility)
    {
        var state = activateAbility
            ? AbilityRunePaymentTests.Runes(LegendAttachmentTests.PositionFor("VEN·149"))
            : PlayRuneTapPaymentTests.Position() with
            {
                RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }
            };
        var journal = new RecordingMatchJournal();
        var session = new MatchSession(state, new CoreRuleEngine(), journal);
        var clients = new RecordingHubClients();
        var hub = CreateHub(clients, new RecordingGroupManager(), "payment-test",
            new FixedMatchSessionRegistry(session), authenticatedHandle: "P1");
        var prompt = session.PromptFor("P1");
        var snapshotBefore = JsonSerializer.Serialize(session.SnapshotFor("P1"));
        var runes = state.PlayerZones["P1"].Base.Where(id =>
            state.CardObjects[id].Tags.Contains(CardObjectTags.RuneCard, StringComparer.Ordinal)).ToArray();
        var taps = runes.Select(id => "TAP_RUNE:" + id).ToArray();
        var recycled = activateAbility ? runes : runes[..1];
        var recycles = recycled.Select(id => "RECYCLE_RUNE:" + id).ToArray();

        JsonElement Command(string[] optionalCosts)
        {
            GameCommand command = activateAbility
                ? new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", [], optionalCosts)
                : PlayCostPreviewTests.Command(optionalCosts);
            var body = JsonSerializer.SerializeToNode(command, command.GetType(),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            body["promptId"] = prompt.PromptId;
            body["snapshotTick"] = prompt.SnapshotTick;
            return JsonSerializer.SerializeToElement(body);
        }

        var rejected = await hub.SubmitIntent(state.RoomId, "P1", "wrong-order", Command([.. recycles, .. taps]));
        Assert.False(rejected.Accepted);
        Assert.Equal(state.Tick, rejected.ServerTick);
        Assert.Empty(clients.GroupClient.EventMessages);
        Assert.Empty(clients.GroupClient.Snapshots);
        Assert.Empty(clients.GroupClient.Prompts);
        Assert.Equal(snapshotBefore, JsonSerializer.Serialize(session.SnapshotFor("P1")));
        Assert.Equal(prompt.PromptId, session.PromptFor("P1").PromptId);

        var receipt = await hub.SubmitIntent(state.RoomId, "P1", "one-payment", Command([.. taps, .. recycles]));
        Assert.True(receipt.Accepted, receipt.Message);
        var entry = Assert.Single(journal.Entries, item => item.Accepted);
        Assert.Equal(state.Tick + 1, entry.CompletedTick);
        Assert.Equal(runes.Concat(recycled),
            entry.Events.Where(e => e.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED")
                .Select(e => Assert.IsType<string>(e.Payload["sourceObjectId"])));
        Assert.Equal(
            Enumerable.Repeat("RUNE_TAPPED", taps.Length).Concat(Enumerable.Repeat("RUNE_RECYCLED", recycled.Length)),
            entry.Events.Where(e => e.Kind is "RUNE_TAPPED" or "RUNE_RECYCLED").Select(e => e.Kind));
        Assert.All(recycled, id => Assert.Contains(id, entry.AuthoritativeState.PlayerZones["P1"].RuneDeck));
        Assert.Equal(0, entry.AuthoritativeState.RunePools["P1"].TotalStoredMana);
        Assert.Equal(0, entry.AuthoritativeState.RunePools["P1"].TotalStoredPower);
    }
}
