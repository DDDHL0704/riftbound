using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Run = Riftbound.ConformanceTests.RawEnvelopeTestRun;

namespace Riftbound.ConformanceTests;

public sealed class CommandInputRecoveryTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnExistingWireReceiptStillRejectsContradictoryTypedArguments(bool cold)
    {
        var run = await Run.Create("binding-cached-wire-" + cold);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var hand = run.State.PlayerZones[player].Hand;
        var command = new MulliganCommand([hand[0]]);
        var raw = JsonSerializer.SerializeToElement(command, Web);
        var accepted = await run.Session.SubmitAsync(player, "wire-receipt", command, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        if (cold) AssertRecovered(await run.Recovery("before-wire-conflict"));
        var changed = await run.Session.SubmitAsync(player, "wire-receipt", new MulliganCommand([hand[1]]), raw, default);
        Assert.False(changed.Accepted);
        Assert.Equal(ErrorCodes.InvalidPayload, changed.ErrorCode);
        Assert.Empty(changed.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
        AssertRecovered(await run.Recovery("after-wire-conflict"));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("unknown-kind")]
    [InlineData("wrong-kind")]
    [InlineData("changed-choice")]
    [InlineData("two-sources")]
    public async Task ActualTypedHistoryCannotRecoverWithMissingOrChangedExecutionInput(string attack)
    {
        var run = await Run.Create("binding-metadata-" + attack);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var hand = run.State.PlayerZones[player].Hand;
        var command = new MulliganCommand([hand[0]]);
        var accepted = await run.Session.SubmitAsync(player, "typed-metadata", command, null, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        AssertRecovered(await run.Recovery("original-typed-history"));
        var original = Frame(run);
        var node = JsonSerializer.SerializeToNode(original, Web)!.AsObject();
        var row = node["commands"]!.AsArray()[^1]!.AsObject();
        if (attack == "removed") row.Remove("typedCommand");
        else if (attack == "unknown-kind") row["typedCommand"]!["kind"] = "Untrusted.Runtime.Type";
        else if (attack == "wrong-kind") row["typedCommand"]!["kind"] = nameof(SurrenderCommand);
        else if (attack == "changed-choice") row["typedCommand"]!["value"]!["handObjectIds"] = new JsonArray(hand[1]);
        else row["rawCommand"] = JsonSerializer.SerializeToNode(command, Web);
        var mutated = node.Deserialize<MatchRecoveryFrame>(Web)!;
        var errors = MatchRecoveryValidator.Validate(mutated.RoomId, mutated.LastEventSequence, mutated.Commands,
            mutated.Events, mutated.PlayerViews, mutated.AuthoritativeState, mutated.CurrentTick, mutated.SpectatorReplayFrame);
        mutated = mutated with { ValidationErrors = errors };
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(mutated, new CoreRuleEngine(), default);
        run.Save("metadata-attack", new { attack, original, mutated, errors, audit });
        if (attack == "changed-choice")
        {
            Assert.Empty(errors);
            Assert.NotEmpty(audit);
        }
        else Assert.NotEmpty(errors);
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        var store = new RawEnvelopeFrameStore(mutated);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, store);
        var failure = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync(run.State.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
        Assert.Equal(1, store.Loads);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
    }

    [Fact]
    public async Task AnUnknownTypedRejectionCannotLoseItsOriginalPayloadDuringRecovery()
    {
        var run = await Run.Create("binding-unknown-typed-input");
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var command = new UnsupportedCommand("BINDING_UNKNOWN", JsonSerializer.SerializeToElement(new { actualArgument = "preserved" }));
        var rejected = await run.Session.SubmitAsync(player, "unknown-typed-input", command, null, default);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        AssertRecovered(await run.Recovery("original-unknown-input"));
        var original = Frame(run);
        var commands = original.Commands.ToArray();
        commands[^1] = commands[^1] with { TypedCommand = null };
        var errors = MatchRecoveryValidator.Validate(original.RoomId, original.LastEventSequence, commands,
            original.Events, original.PlayerViews, original.AuthoritativeState, original.CurrentTick, original.SpectatorReplayFrame);
        Assert.Contains(errors, error => error.Contains("raw command is required", StringComparison.Ordinal));
        var store = new RawEnvelopeFrameStore(original with { Commands = commands, ValidationErrors = errors });
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, store);
        var failure = await Assert.ThrowsAsync<MatchSessionException>(async () =>
            await registry.GetOrCreateAsync(original.RoomId, default));
        Assert.Equal(ErrorCodes.RecoveryInconsistent, failure.Code);
        Assert.Equal(1, store.Loads);
    }

    [Fact]
    public async Task ActualTypedPaymentKeepsItsSpendAndReceiptAcrossColdRecovery()
    {
        var run = await Run.Create("binding-typed-payment");
        var seeded = await run.Session.SeedScenarioAsync("p1", "payment-scenario", "pay-cost-window", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        Assert.Null(run.Entries[^1].RawCommand);
        Assert.NotNull(run.Entries[^1].TypedCommand);
        AssertRecovered(await run.Recovery("seeded-payment"));
        var pending = Assert.IsType<PendingPaymentState>(run.State.PendingPayment);
        var command = new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_MANA:1"]);
        var paid = await run.Session.SubmitAsync("p1", "typed-payment", command, null, default);
        Assert.True(paid.Accepted, paid.ErrorMessage);
        Assert.Equal(1, Assert.Single(paid.Events, e => e.Kind == "COST_PAID").Payload["mana"]);
        Assert.Equal(0, run.State.RunePools["p1"].Mana);
        Assert.Null(run.State.PendingPayment);
        Assert.Null(run.Entries[^1].RawCommand);
        var count = run.Entries.Count;
        var duplicate = await run.Session.SubmitAsync("p1", "typed-payment", command, null, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Equal(count, run.Entries.Count);
        AssertRecovered(await run.Recovery("paid"));
        duplicate = await run.Session.SubmitAsync("p1", "typed-payment", command, null, default);
        Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
        Assert.Empty(duplicate.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(0, duplicate.State.RunePools["p1"].Mana);
        var conflict = await run.Session.SubmitAsync("p1", "typed-payment",
            command with { PaymentChoiceIds = [] }, null, default);
        Assert.Equal(ErrorCodes.ClientIntentConflict, conflict.ErrorCode);
        Assert.Empty(conflict.Events);
        Assert.Equal(count, run.Entries.Count);
    }

    [Theory]
    [InlineData(" UNKNOWN")]
    [InlineData("UNKNOWN ")]
    public async Task ADirectMalformedTypeReturnsARejectionAndLeavesTheIntentCorrectable(string type)
    {
        var run = await Run.Create("binding-malformed-type-" + (type.StartsWith(' ') ? "leading" : "trailing"));
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var count = run.Entries.Count;
        var bad = await run.Session.SubmitAsync(player, "correctable-type", new UnsupportedCommand(type), null, default);
        Assert.Equal(ErrorCodes.InvalidPayload, bad.ErrorCode);
        Assert.Empty(bad.Events);
        Assert.Equal(count, run.Entries.Count);
        var corrected = await run.Session.SubmitAsync(player, "correctable-type", new MulliganCommand([]), null, default);
        Assert.True(corrected.Accepted, corrected.ErrorMessage);
        AssertRecovered(await run.Recovery("corrected-type"));
    }

    private static MatchRecoveryFrame Frame(Run run)
    {
        var last = run.Entries[^1];
        var commands = run.Entries.Select(e => JsonSerializer.Deserialize<RecoveredCommand>(JsonSerializer.Serialize(e, Web), Web)!).ToArray();
        var events = run.Entries.SelectMany(e => e.Events.Select((value, i) =>
            new RecoveredEvent(e.StartedEventSequence + i + 1, e.CompletedTick, i, value))).ToArray();
        var views = last.Snapshots.ToDictionary(p => p.Key, p => new RecoveredPlayerView(p.Key,
            last.CompletedTick, last.CompletedEventSequence, p.Value, last.CompletedTick, last.CompletedEventSequence, last.Prompts[p.Key]));
        var initial = MatchState.Create(run.State.RoomId) with { Seed = run.Entries[0].OpeningSeed };
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(run.State.RoomId, last.CompletedTick,
            last.CompletedEventSequence, events.Select(e => e.Event).ToArray(), run.State);
        return new MatchRecoveryFrame(run.State.RoomId, last.CompletedTick, last.CompletedEventSequence,
            commands, events, views, [], run.State, initial, spectator) { OpeningSeed = run.Entries[0].OpeningSeed };
    }

    private static void AssertRecovered(RawEnvelopeRecoveryProof proof)
    {
        Assert.Empty(proof.Errors);
        Assert.Empty(proof.AuditErrors);
        Assert.True(proof.Replay.IsMatch);
        Assert.Null(proof.RegistryFailure);
        Assert.Equal(1, proof.StoreLoads);
    }
}
