using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Riftbound.Api.Hubs;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class GameHubJoinTests
{
    [Fact]
    public void HubPromptOmitsDiagnosticsAndKeepsAllOtherFields()
    {
        var state = PlayCostPreviewTests.Position(new(8, 4));
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var message = new WsServerMessage(MessageType.PROMPT, state.RoomId, "P1", state.Tick, prompt);
        var normal = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        normal.Converters.Add(new JsonStringEnumConverter());
        var hub = new JsonSerializerOptions(normal);
        PromptWireProjection.Configure(hub);

        var expected = JsonSerializer.SerializeToNode(message, normal)!;
        var expectedPrompt = expected["payload"]!.AsObject();
        Assert.True(expectedPrompt.Remove("inspection"));
        Assert.True(expectedPrompt.Remove("objectContexts"));
        Assert.True(expectedPrompt.Remove("serverFlow"));
        Assert.True(JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(message, hub)));
    }

    [Fact]
    public async Task PromptDiagnosticsOnlyReturnsExistingSeatProjection()
    {
        var state = PlayCostPreviewTests.Position(new(8, 4));
        var session = new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance);
        var hub = CreateHub(new RecordingHubClients(), new RecordingGroupManager(), "prompt-diagnostics",
            new FixedMatchSessionRegistry(session));
        Assert.Null(await hub.RequestPromptDiagnostics(state.RoomId));
        hub.Context.Items["riftbound:authenticatedHandle"] = "SPECTATOR";
        Assert.Null(await hub.RequestPromptDiagnostics(state.RoomId));
        hub.Context.Items["riftbound:authenticatedHandle"] = "P1";
        Assert.Null(await hub.RequestPromptDiagnostics("MISSING"));

        var full = session.PromptFor("P1");
        var diagnostics = Assert.IsType<PromptDiagnosticsDto>(await hub.RequestPromptDiagnostics(state.RoomId));
        Assert.Equal(full.PromptId, diagnostics.PromptId);
        Assert.Equal(full.SnapshotTick, diagnostics.SnapshotTick);
        Assert.Equal(JsonSerializer.Serialize(full.Inspection), JsonSerializer.Serialize(diagnostics.Inspection));
        Assert.Equal(JsonSerializer.Serialize(full.ObjectContexts), JsonSerializer.Serialize(diagnostics.ObjectContexts));
        Assert.Equal(JsonSerializer.Serialize(full.ServerFlow), JsonSerializer.Serialize(diagnostics.ServerFlow));
        Assert.DoesNotContain("P2-CARD", JsonSerializer.Serialize(diagnostics));
    }
}
