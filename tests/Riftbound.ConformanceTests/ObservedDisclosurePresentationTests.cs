using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ObservedDisclosurePresentationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualObservationChoiceCarriesItsPublicOrPrivateModeAndMatchingOwnerText(bool reveal)
    {
        var (initial, driver) = await Open(reveal);
        var choice = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("DECK_OBSERVATION_BANISH", choice.Request.Kind);
        Assert.Equal(reveal, choice.Request.Preview!.DeckObservationView!.Public);
        var prompts = ResolutionResult.BuildPrompts(driver.State);
        var candidate = Assert.Single(prompts["P1"].Candidates!, c => c.Action == CommandTypes.PayCost);
        Assert.Contains(reveal ? "展示" : "查看", prompts["P1"].Reason);
        Assert.Contains(reveal ? "展示" : "查看", Assert.Single(candidate.SelectionSteps!).Label);
        Assert.DoesNotContain(prompts["P2"].Candidates!, c => c.Action == CommandTypes.PayCost && c.Enabled);
        foreach (var snapshot in ResolutionResult.BuildSnapshots(driver.State).Values.Append(
                     ResolutionResult.BuildSpectatorSnapshot(driver.State)))
        {
            var payment = JsonSerializer.SerializeToElement(snapshot.Timing).GetProperty("pendingPayment");
            Assert.True(payment.TryGetProperty("deckObservationKind", out var mode), "Observed choice snapshot has no explicit LOOK or REVEAL disclosure mode.");
            Assert.Equal(reveal ? "REVEAL" : "LOOK", mode.GetString());
        }
        ArmamentMightTests.PublicRecovery(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    public static IEnumerable<object[]> ViewAttacks()
    {
        foreach (var reveal in new[] { false, true })
        foreach (var viewer in new[] { "P1", "P2", "SPECTATOR" })
        foreach (var mutation in new[] { "MISSING", "FLIP", "INVALID", "NUMBER", "NULL" })
            yield return [reveal, viewer, mutation];
    }

    [Theory]
    [MemberData(nameof(ViewAttacks))]
    public async Task IndependentRecoveryRejectsForgedObservationDisclosureOnEitherPlayerAndSpectator(bool reveal, string viewer, string mutation)
    {
        var (_, driver) = await Open(reveal);
        var state = driver.State;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state);
        Assert.Empty(MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator));
        var original = viewer == "SPECTATOR" ? spectator.SpectatorSnapshot : snapshots[viewer];
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var node = JsonSerializer.SerializeToNode(original, options)!;
        var payment = node["timing"]!["pendingPayment"]!.AsObject();
        if (mutation == "MISSING") payment.Remove("deckObservationKind");
        else payment["deckObservationKind"] = mutation switch {
            "FLIP" => JsonValue.Create(reveal ? "LOOK" : "REVEAL"),
            "INVALID" => JsonValue.Create("PUBLIC"), "NUMBER" => JsonValue.Create(1),
            "NULL" => null, _ => throw new InvalidOperationException(mutation) };
        var forged = JsonSerializer.Deserialize<SnapshotDto>(node.ToJsonString(), options)!;
        if (viewer == "SPECTATOR") spectator = spectator with { SpectatorSnapshot = forged };
        else views[viewer] = views[viewer] with { Snapshot = forged };
        var errors = MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick, spectator);
        Assert.Contains(errors, error => error.Contains("deck observation disclosure kind", StringComparison.Ordinal));
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> Open(bool reveal)
    {
        var initial = reveal ? PakaaProtectorRevealTests.ControlledJournalPosition(DeckObservationPlayTests.Nocturne)
            : DeckObservationPlayTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        if (reveal) await PakaaProtectorRevealTests.Move(driver);
        else await driver.Act("P1", new PlayCardCommand("TRICK", "OGN·183/298", []));
        await driver.Top();
        return (initial, driver);
    }
}
