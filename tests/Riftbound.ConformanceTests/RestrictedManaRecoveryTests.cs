using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.RestrictedManaPaymentTests;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedManaRecoveryTests
{
    [Fact]
    public async Task RealAcquisitionColdRestoresAndAnInternallyCoherentForgedBalanceFailsOriginalReplay()
    {
        var initial = RestrictedManaLifecycleTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
        var frame = Frame(driver.State, initial, driver);
        Assert.Empty(frame.ValidationErrors);
        // This local mechanism fixture starts at an explicit board. The separate
        // finite-QA acceptance covers empty-room birth rather than relabeling it.
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
        var restored = MatchSession.Restore(frame, new CoreRuleEngine(), NoopMatchJournal.Instance);
        Assert.Equal(MatchStateHasher.HashValue(frame.PlayerViews["P1"].Snapshot), MatchStateHasher.HashValue(restored.SnapshotFor("P1")));
        // Every view and hash agrees with the forged +3. Only the immutable
        // original command/effects can prove that printed Lux actually earned +2.
        var forged = Pool(driver.State, driver.State.RunePools["P1"] with { SpellOnlyMana = 3 });
        var coherent = Frame(forged, initial, driver);
        Assert.Empty(coherent.ValidationErrors);
        var refused = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(refused.IsMatch); Assert.NotEmpty(refused.Errors);
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
    }

    [Theory]
    [InlineData("P1", "missing")]
    [InlineData("P2", "missing")]
    [InlineData("spectator", "missing")]
    [InlineData("P1", "-1")]
    [InlineData("P2", "-1")]
    [InlineData("spectator", "-1")]
    [InlineData("P1", "3")]
    [InlineData("P2", "3")]
    [InlineData("spectator", "3")]
    [InlineData("P1", "\"2\"")]
    [InlineData("P2", "2.5")]
    [InlineData("spectator", "null")]
    public async Task PrivatePeerAndSpectatorViewsCannotLoseForgeOrCoerceTheBalance(string viewer, string value)
    {
        var initial = RestrictedManaLifecycleTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        var frame = Frame(driver.State, initial, driver);
        var views = frame.PlayerViews.ToDictionary(p => p.Key, p => p.Value);
        var spectator = frame.SpectatorReplayFrame!;
        if (viewer == "spectator") spectator = spectator with { SpectatorSnapshot = Mutate(spectator.SpectatorSnapshot, value) };
        else views[viewer] = views[viewer] with { Snapshot = Mutate(views[viewer].Snapshot, value) };
        var errors = MatchRecoveryValidator.Validate(frame.RoomId, frame.LastEventSequence, frame.Commands, frame.Events,
            views, frame.AuthoritativeState, frame.CurrentTick, spectator);
        Assert.NotEmpty(errors);
        Assert.Equal(2, driver.State.RunePools["P1"].SpellOnlyMana);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, int.MaxValue)]
    public async Task MalformedAuthoritativeBalancesRejectEvenWhenProjectionsMatch(int restricted, int mana)
    {
        var initial = RestrictedManaLifecycleTests.Position();
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        var invalid = Pool(driver.State, new RunePool(mana, 10, spellOnlyMana: restricted));
        Assert.NotEmpty(Frame(invalid, initial, driver).ValidationErrors);
    }

    [Fact]
    public void NegativeAndOverflowingTypedPowerCannotDisappearDuringConstruction()
    {
        var negative = new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Red] = -1 });
        Assert.Equal(-1, negative.PowerByTrait[RuneTrait.Red]); Assert.False(negative.HasSupportedAmounts);
        var overflow = new RunePool(0, int.MaxValue, new Dictionary<string, int> { [RuneTrait.Red] = 1 });
        Assert.Equal(1, overflow.PowerByTrait[RuneTrait.Red]); Assert.False(overflow.HasSupportedAmounts);
        Assert.False(PaymentCostRules.AuthorizePayment(new("invalid-typed-pool", "ACTIVATE_ABILITY", "P1"), overflow).Accepted);
    }

    private static MatchRecoveryFrame Frame(MatchState state, MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var sequence = driver.Events().LongLength;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, sequence,
            snapshots[id], state.Tick, sequence, prompts[id]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, driver.Commands(), driver.Events(), views, state, state.Tick, spectator);
        return new(state.RoomId, state.Tick, sequence, driver.Commands(), driver.Events(), views, errors, state, initial, spectator);
    }
    private static SnapshotDto Mutate(SnapshotDto snapshot, string value)
    {
        var json = JsonSerializer.SerializeToNode(snapshot, Web)!;
        var pool = json["players"]!["P1"]!["runePool"]!.AsObject();
        if (value == "missing") pool.Remove("spellOnlyMana"); else pool["spellOnlyMana"] = JsonNode.Parse(value);
        return json.Deserialize<SnapshotDto>(Web)!;
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
