using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;

namespace Riftbound.ConformanceTests;

public sealed class CommandPayloadBindingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PresentRawMustDescribeTheTypedActionBeforeEitherCanBeRecorded()
    {
        var (run, player, first, second) = await Start("binding-present-mismatch");
        var command = new MulliganCommand([first]);
        var raw = Wire(run, player, new MulliganCommand([second]));
        var state = run.State;
        var count = run.Entries.Count;
        var result = await run.Session.SubmitAsync(player, "binding-intent", command, raw, default);
        var recovery = await run.Recovery("mismatched");
        run.Save("binding-observation", new { command, raw, result, recovery,
            commandsBefore = count, commandsAfter = run.Entries.Count,
            sameStateReference = ReferenceEquals(state, run.State), journal = run.Entries });

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Same(state, run.State);
        AssertRecovered(recovery);

        var corrected = await run.Session.SubmitAsync(player, "binding-intent", command,
            Wire(run, player, command), default);
        Assert.True(corrected.Accepted, corrected.ErrorMessage);
        Assert.Equal(count + 1, run.Entries.Count);
        AssertRecovered(await run.Recovery("corrected"));
    }

    [Fact]
    public async Task NullableAbsentRawRetainsTheActualTypedActionThroughStrictColdRecovery()
    {
        var (run, player, first, _) = await Start("binding-absent-recovery");
        var command = new MulliganCommand([first]);
        var count = run.Entries.Count;
        var result = await run.Session.SubmitAsync(player, "typed-only", command, null, default);
        var stateHash = MatchStateHasher.Hash(run.State);
        var recovery = await run.Recovery("typed-only");
        run.Save("binding-observation", new { command, result, recovery, journal = run.Entries });

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(count + 1, run.Entries.Count);
        Assert.Null(run.Entries[^1].RawCommand);
        AssertRecovered(recovery);
        Assert.Equal(stateHash, MatchStateHasher.Hash(run.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AChangedTypedPayloadCannotReuseTheIntentWhenRawIsAbsent(bool cold)
    {
        var (run, player, first, second) = await Start("binding-absent-conflict-" + cold);
        var original = new MulliganCommand([first]);
        var accepted = await run.Session.SubmitAsync(player, "typed-conflict", original, null, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        if (cold)
        {
            var recovery = await run.Recovery("before-conflict");
            run.Save("cold-binding-observation", new { original, accepted, recovery, journal = run.Entries });
            AssertRecovered(recovery);
        }

        var changed = new MulliganCommand([second]);
        var result = await run.Session.SubmitAsync(player, "typed-conflict", changed, null, default);
        run.Save("binding-observation", new { cold, original, changed, accepted, result,
            commandsBeforeConflict = count, commandsAfterConflict = run.Entries.Count, journal = run.Entries });
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.ClientIntentConflict, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnEqualIndependentTypedPayloadKeepsTheOriginalReceiptWhenRawIsAbsent(bool cold)
    {
        var (run, player, first, _) = await Start("binding-absent-equal-" + cold);
        var original = new MulliganCommand([first]);
        var accepted = await run.Session.SubmitAsync(player, "typed-equal", original, null, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        if (cold)
        {
            var recovery = await run.Recovery("before-equal");
            run.Save("cold-binding-observation", new { original, accepted, recovery, journal = run.Entries });
            AssertRecovered(recovery);
        }

        var duplicate = new MulliganCommand(original.HandObjectIds.ToArray());
        var result = await run.Session.SubmitAsync(player, "typed-equal", duplicate, null, default);
        run.Save("binding-observation", new { cold, original, duplicate, accepted, result, journal = run.Entries });
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        Assert.Null(run.Entries[^1].RawCommand);
        if (cold) Assert.Empty(result.Events);
        else Assert.Equal(Hash(accepted.Events), Hash(result.Events));
    }

    [Fact]
    public async Task CanonicalWireKeepsItsOriginalActionAndDuplicateAcrossColdRecovery()
    {
        var (run, player, first, _) = await Start("binding-canonical-control");
        var command = new MulliganCommand([first]);
        var raw = Wire(run, player, command);
        var accepted = await run.Session.SubmitAsync(player, "wire-control", command, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        var recovery = await run.Recovery("wire-control");
        run.Save("binding-observation", new { command, raw, accepted, recovery, journal = run.Entries });
        AssertRecovered(recovery);
        var duplicate = await run.Session.SubmitAsync(player, "wire-control",
            new MulliganCommand(command.HandObjectIds.ToArray()), raw, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Empty(duplicate.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        Assert.Equal(raw.GetRawText(), run.Entries[^1].RawCommand!.Value.GetRawText());
    }

    private static async Task<(Run Run, string Player, string First, string Second)> Start(string name)
    {
        var run = await Run.Create(name);
        Assert.Equal(MatchPhases.Mulligan, run.State.Phase);
        AssertRecovered(await run.Recovery("original-birth"));
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var hand = run.State.PlayerZones[player].Hand;
        Assert.True(hand.Count >= 2);
        return (run, player, hand[0], hand[1]);
    }

    private static JsonElement Wire(Run run, string player, GameCommand command)
    {
        var node = JsonSerializer.SerializeToNode(command, command.GetType(), Web)!.AsObject();
        node["promptId"] = run.Session.PromptFor(player).PromptId;
        node["snapshotTick"] = run.State.Tick;
        return JsonSerializer.SerializeToElement(node);
    }

    private static void AssertRecovered(RawEnvelopeRecoveryProof proof)
    {
        Assert.Empty(proof.Errors);
        Assert.Empty(proof.AuditErrors);
        Assert.True(proof.Replay.IsMatch);
        Assert.Null(proof.RegistryFailure);
        Assert.Equal(1, proof.StoreLoads);
    }

    private static string Hash(object value) => MatchStateHasher.HashValue(
        JsonSerializer.SerializeToElement(value, value.GetType(), Web));
}
