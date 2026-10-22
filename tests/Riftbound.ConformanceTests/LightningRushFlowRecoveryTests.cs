using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushFlowRecoveryTests
{
    [Theory]
    [InlineData("source")]
    [InlineData("null-source")]
    [InlineData("generation")]
    [InlineData("card")]
    [InlineData("null-card")]
    [InlineData("owner")]
    [InlineData("null-owner")]
    [InlineData("hand-origin")]
    [InlineData("confirmed-permanent")]
    [InlineData("printed-mana")]
    [InlineData("zone")]
    public async Task IndependentRecoveryRejectsForgedFlowIdentityOwnerGenerationAndCardPlayShape(string branch)
    {
        var driver = await Pending(); var state = driver.State;
        var item = Assert.Single(state.StackItems); var flow = item.FlowPlay!;
        flow = branch switch {
            "source" => flow with { Source = new("SPELL3", 1) }, "null-source" => flow with { Source = null! },
            "generation" => flow with { Source = flow.Source with { Generation = 999 } },
            "card" => flow with { CardNo = "OGN·048/298" }, "null-card" => flow with { CardNo = null! },
            "owner" => flow with { OwnerId = "P2" }, "null-owner" => flow with { OwnerId = null! }, _ => flow };
        item = item with { FlowPlay = flow, PlayedFromHand = branch == "hand-origin",
            SourceConfirmed = branch == "confirmed-permanent",
            PlayCost = branch == "printed-mana" ? item.PlayCost! with { CardMana = 2 } : item.PlayCost };
        var locations = state.ObjectLocations.ToDictionary(); if (branch == "zone") locations["RUSH"] = new("P1", "GRAVEYARD");
        var forged = state with { StackItems = [item], ObjectLocations = locations };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), error => error.Contains("flow play receipt", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("cardNo")]
    [InlineData("ownerId")]
    public async Task OmittingRequiredSerializedFlowFieldsCannotBecomeADefaultPermission(string field)
    {
        var driver = await Pending();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var raw = JsonSerializer.SerializeToNode(driver.State, json)!.AsObject();
        Assert.True(raw["stackItems"]![0]!["flowPlay"]!.AsObject().Remove(field));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MatchState>(raw.ToJsonString(), json));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("false")]
    [InlineData("malformed")]
    [InlineData("private-receipt")]
    public async Task BothPrivateViewersAndTheSpectatorRejectForgedPublicFlowFlagsOrExposedGenerationReceipts(string branch)
    {
        var driver = await Pending(); var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        foreach (var snapshot in snapshots.Values.Append(spectator.SpectatorSnapshot)) {
            var row = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(true, row["playedWithFlow"]);
            if (branch == "missing") row.Remove("playedWithFlow");
            else if (branch == "private-receipt") row["flowPlay"] = state.StackItems[0].FlowPlay;
            else row["playedWithFlow"] = branch == "false" ? false : "true";
            Assert.Contains(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator), error => error.Contains("flow play", StringComparison.Ordinal));
            row["playedWithFlow"] = true; row.Remove("flowPlay");
        }
    }

    [Fact]
    public async Task RemovingAnOtherwisePlausibleFlowReceiptStillCannotRewriteTheOriginalAcceptedPlay()
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []));
        var state = driver.State;
        var forged = state with { StackItems = [state.StackItems[0] with { FlowPlay = null }] };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged, new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch);
        Assert.Contains(replay.Errors, error => error.Contains("replayed final state hash", StringComparison.Ordinal));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task<StandbyDefendObserverTests.Driver> Pending()
    {
        var driver = new StandbyDefendObserverTests.Driver(LightningRushDeckDrawFlowTests.Position(graveyard: true));
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []));
        Assert.NotNull(Assert.Single(driver.State.StackItems).FlowPlay);
        return driver;
    }
}
