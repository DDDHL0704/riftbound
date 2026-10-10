using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialSettReplacementTests;
using static Riftbound.ConformanceTests.DeathObserverAuditTests;

namespace Riftbound.ConformanceTests;

public sealed class RetiredPaymentLedgerTests
{
    internal static string ForgedActionId(string id) => "TEMP_PAYMENT_RESOURCE:" + id;

    internal static void AssertNoLedger(MatchState state)
    {
        var json = JsonSerializer.SerializeToElement(state, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(json.TryGetProperty("temporaryPaymentResources", out _));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"forged\"")]
    [InlineData("[{\"resourceId\":\"forged\",\"remainingPower\":99}]")]
    public void RemovedLedgerCannotBeRestoredAsCurrentState(string payload)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var raw = JsonSerializer.SerializeToNode(NoReplacement(), options)!;
        raw["temporaryPaymentResources"] = JsonNode.Parse(payload);
        Assert.Throws<JsonException>(() => raw.Deserialize<MatchState>(options));
    }

    [Fact]
    public void CurrentStateAndEveryViewerOmitTheRetiredLedger()
    {
        var state = NoReplacement(); AssertNoLedger(state);
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state).Values.Append(ResolutionResult.BuildSpectatorSnapshot(state)))
            Assert.False(snapshot.Timing.ContainsKey("temporaryPaymentResources"));
        OfficialGraveyardRecastTests.Restore(state);
    }

    [Theory]
    [InlineData("TEMP_PAYMENT_RESOURCE:forged")]
    [InlineData("TEMP_PAYMENT_RESOURCE:")]
    public async Task RemovedResourceCannotPayAnOtherwiseAffordableSpell(string action)
    {
        var state = NoReplacement();
        var result = await new CoreRuleEngine().ResolveAsync(state, new("retired", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("AOE", "OGN·133/298", [], OptionalCosts: [action]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(false, "[]")]
    [InlineData(false, "null")]
    [InlineData(false, "[{\"remainingPower\":999}]")]
    [InlineData(true, "[]")]
    [InlineData(true, "null")]
    [InlineData(true, "[{\"remainingPower\":999}]")]
    public void RecoveryRejectsRemovedLedgerInPlayerAndSpectatorViews(bool spectator, string payload)
    {
        var state = NoReplacement();
        var snapshot = spectator ? ResolutionResult.BuildSpectatorSnapshot(state) : ResolutionResult.BuildSnapshots(state)["P1"];
        var timing = new Dictionary<string, object?>(snapshot.Timing) { ["temporaryPaymentResources"] = JsonSerializer.Deserialize<JsonElement>(payload) };
        snapshot = snapshot with { Timing = timing };
        var views = new Dictionary<string, RecoveredPlayerView>();
        var frame = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        if (spectator) frame = frame with { SpectatorSnapshot = snapshot };
        else views["P1"] = new("P1", state.Tick, 0, snapshot, state.Tick, 0, ResolutionResult.BuildPrompts(state)["P1"]);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, currentTick: state.Tick,
            spectatorReplayFrame: spectator ? frame : null);
        Assert.Contains(errors, error => error.Contains("obsolete temporary payment resources", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("resource")]
    [InlineData("choice")]
    [InlineData("stack")]
    public void RecoveryRejectsRetiredPaymentActionsEvenWithoutAnOldLedger(string slot)
    {
        var state = NoReplacement(); var action = ForgedActionId("invented");
        state = slot == "stack" ? state with { StackItems = [new("S", "P1", "AOE", "TEST", "OGN·133/298", optionalCosts: [action])] }
            : state with { PendingPayment = new("PAY", "TEST", "P1", powerCost: 1,
                legalPaymentChoiceIds: slot == "choice" ? [action] : ["SPEND_POWER:1"],
                paymentResourceActionIds: slot == "resource" ? [action] : []) };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), error => error.Contains("obsolete temporary payment resource action", StringComparison.Ordinal));
    }
}
