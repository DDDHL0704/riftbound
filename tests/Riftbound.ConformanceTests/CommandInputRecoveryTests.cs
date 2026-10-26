using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Npgsql;
using Riftbound.Contracts;
using Riftbound.Engine;
using Riftbound.Persistence;
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
    public async Task DirectScenarioReplayRejectsTypedInputForADifferentRecordedScenario()
    {
        var run = await Run.Create("binding-scenario-recorded-type");
        var seeded = await run.Session.SeedScenarioAsync("p1", "original-scenario", "pay-cost-window", null, default);
        Assert.True(seeded.Accepted, seeded.ErrorMessage);
        AssertRecovered(await run.Recovery("original-scenario"));
        var original = Frame(run);
        var commands = original.Commands.ToArray();
        var typed = Assert.IsType<TypedCommandInput>(commands[^1].TypedCommand);
        commands[^1] = commands[^1] with { TypedCommand = typed with {
            Value = JsonSerializer.SerializeToElement(new { scenarioId = "typed-power-payment" }, Web) } };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(original.ReplayInitialState!, commands,
            original.AuthoritativeState!, new CoreRuleEngine(), default, original.Events);
        Assert.False(replay.IsMatch);
        Assert.Contains(replay.Errors, error => error.Contains("invalid typed command input", StringComparison.Ordinal));
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPostgresWriterPreservesAbsentTypedAndCanonicalRawInputs(bool rawPresent)
    {
        var run = await Run.Create("binding-postgres-payload-" + rawPresent);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var first = run.State.PlayerZones[player].Hand[0];
        var input = new MulliganCommand([first]);
        var node = JsonSerializer.SerializeToNode(input, Web)!.AsObject();
        node["promptId"] = run.Session.PromptFor(player).PromptId;
        node["snapshotTick"] = run.State.Tick;
        JsonElement? raw = rawPresent ? JsonSerializer.SerializeToElement(node) : null;
        const string intent = "persisted-mulligan";
        var accepted = await run.Session.SubmitAsync(player, intent, input, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var entry = run.Entries[^1];
        Assert.Equal(rawPresent, entry.RawCommand is not null);
        Assert.Equal(!rawPresent, entry.TypedCommand is not null);
        if (!rawPresent)
        {
            Assert.Equal(nameof(MulliganCommand), entry.TypedCommand!.Kind);
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(input, Web),
                JsonNode.Parse(entry.TypedCommand.Value.GetRawText())));
        }
        var original = Frame(run);
        var count = run.Entries.Count;
        var journalBefore = JsonSerializer.Serialize(run.Entries, Web);
        var stateHash = MatchStateHasher.Hash(run.State);

        using var command = new NpgsqlCommand();
        typeof(PostgresMatchJournal).GetMethod("AddCommandPayload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [command, entry]);
        var payload = Assert.IsType<string>(command.Parameters["payload"].Value);
        run.Save("actual-postgres-payload", new { rawPresent, entry, payload, noDatabaseExecutionClaimed = true });
        using var document = JsonDocument.Parse(payload);
        var hasTyped = document.RootElement.TryGetProperty("typedCommand", out var typed);
        if (!rawPresent)
        {
            Assert.True(hasTyped, "The actual journal writer must preserve real typed input when rawCommand is absent.");
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(entry.TypedCommand, Web),
                JsonNode.Parse(typed.GetRawText())));
        }
        else if (hasTyped) Assert.Equal(JsonValueKind.Null, typed.ValueKind);

        var payloads = new List<(string Label, string Json)> { ("actual", payload) };
        if (rawPresent)
        {
            var absent = JsonNode.Parse(payload)!.AsObject();
            absent.Remove("typedCommand");
            payloads.Add(("canonical-typed-absent", absent.ToJsonString()));
            var explicitNull = JsonNode.Parse(payload)!.AsObject();
            explicitNull["typedCommand"] = null;
            payloads.Add(("canonical-typed-null", explicitNull.ToJsonString()));
        }
        foreach (var stored in payloads)
        {
            var readTyped = typeof(PostgresMatchRecoveryStore).GetMethod("ReadTypedCommand", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [stored.Json]);
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(entry.TypedCommand, Web),
                JsonSerializer.SerializeToNode(readTyped, Web)));
            var decoded = JsonSerializer.Deserialize<RecoveredCommand>(stored.Json, Web)!;
            var persisted = original with { Commands = original.Commands.Select(recorded =>
                recorded.ClientIntentId == intent ? decoded : recorded).ToArray() };
            Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(original.Commands, Web),
                JsonSerializer.SerializeToNode(persisted.Commands, Web)));
            var replay = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(persisted, new CoreRuleEngine(), default);
            Assert.Empty(replay);
            var store = new RawEnvelopeFrameStore(persisted);
            var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, store);
            var cold = await registry.GetOrCreateAsync(run.State.RoomId, default);
            var duplicate = await cold.SubmitAsync(player, intent, new MulliganCommand([first]), raw, default);
            Assert.True(duplicate.Accepted, duplicate.ErrorMessage);
            Assert.Empty(duplicate.Events);
            Assert.Equal(1, store.Loads);
            Assert.Equal(count, run.Entries.Count);
            Assert.Equal(stateHash, MatchStateHasher.Hash(duplicate.State));
            Assert.Equal(journalBefore, JsonSerializer.Serialize(run.Entries, Web));
            run.Save("actual-postgres-recovery-" + stored.Label, new { rawPresent, stored.Json, persisted, replayErrors = replay,
                duplicate, journalBytesUnchanged = true, noDatabaseExecutionClaimed = true });
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalAcceptedIntentSurvivesTheDocumentedJsonbNumberOutputForm(bool jsonbOutputForm)
    {
        var run = await Run.Create("binding-jsonb-numeric-input-" + jsonbOutputForm);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var input = new MulliganCommand([run.State.PlayerZones[player].Hand[0]]);
        var node = JsonSerializer.SerializeToNode(input, Web)!.AsObject();
        node["promptId"] = run.Session.PromptFor(player).PromptId;
        node["snapshotTick"] = run.State.Tick;
        var envelope = node.ToJsonString(Web);
        var raw = JsonDocument.Parse(envelope[..^1] + ",\"clientNote\":{\"reading\":1.230e-5}}").RootElement.Clone();
        const string intent = "jsonb-numeric-original-input";
        var accepted = await run.Session.SubmitAsync(player, intent, input, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        var count = run.Entries.Count;
        var journalBefore = JsonSerializer.Serialize(run.Entries, Web);
        var stateHash = MatchStateHasher.Hash(run.State);
        var warm = await run.Session.SubmitAsync(player, intent, input, raw, default);
        Assert.True(warm.Accepted, warm.ErrorMessage);
        Assert.Equal(count, run.Entries.Count);

        using var command = new NpgsqlCommand();
        typeof(PostgresMatchJournal).GetMethod("AddCommandPayload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [command, run.Entries[^1]]);
        var payload = Assert.IsType<string>(command.Parameters["payload"].Value);
        Assert.Contains("1.230e-5", payload, StringComparison.Ordinal);
        // PostgreSQL 16 documents this exact jsonb output form. This is an explicit
        // stored-form fixture using the real codec, not execution against SQL.
        // https://www.postgresql.org/docs/16/datatype-json.html#JSON-INPUT-OUTPUT
        var stored = jsonbOutputForm ? payload.Replace("1.230e-5", "0.00001230", StringComparison.Ordinal) : payload;
        var restoredRaw = Assert.IsType<JsonElement>(typeof(PostgresMatchRecoveryStore)
            .GetMethod("ReadRawCommand", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [stored]));
        Assert.Equal(raw.GetProperty("clientNote").GetProperty("reading").GetDecimal(),
            restoredRaw.GetProperty("clientNote").GetProperty("reading").GetDecimal());
        var decoded = JsonSerializer.Deserialize<RecoveredCommand>(stored, Web)!;
        Assert.Equal(restoredRaw.GetRawText(), decoded.RawCommand!.Value.GetRawText());
        var original = Frame(run);
        var persisted = original with { Commands = original.Commands.Select(recorded =>
            recorded.ClientIntentId == intent ? decoded : recorded).ToArray() };
        var errors = MatchRecoveryValidator.Validate(persisted.RoomId, persisted.LastEventSequence,
            persisted.Commands, persisted.Events, persisted.PlayerViews, persisted.AuthoritativeState,
            persisted.CurrentTick, persisted.SpectatorReplayFrame);
        Assert.Empty(errors);
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(persisted, new CoreRuleEngine(), default);
        Assert.Empty(audit);
        var store = new RawEnvelopeFrameStore(persisted);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, store);
        var cold = await registry.GetOrCreateAsync(run.State.RoomId, default);
        var duplicate = await cold.SubmitAsync(player, intent, input, raw, default);
        run.Save("documented-jsonb-number-output", new { jsonbOutputForm, raw, payload, stored, restoredRaw,
            original, persisted, errors, audit, duplicate,
            originalJournalUnchanged = journalBefore == JsonSerializer.Serialize(run.Entries, Web),
            noDatabaseExecutionClaimed = true });
        Assert.Equal(1, store.Loads);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(journalBefore, JsonSerializer.Serialize(run.Entries, Web));
        Assert.Equal(stateHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Empty(duplicate.Events);
        Assert.True(duplicate.Accepted,
            "The unchanged original wire must retain its accepted intent across the documented jsonb number output form: " + duplicate.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidNumericWireStillConflictsBeforePayloadValidation(bool cold)
    {
        var run = await Run.Create("binding-invalid-numeric-priority-" + cold);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var input = new MulliganCommand([run.State.PlayerZones[player].Hand[0]]);
        var node = JsonSerializer.SerializeToNode(input, Web)!.AsObject();
        node["promptId"] = run.Session.PromptFor(player).PromptId;
        var tick = run.State.Tick;
        node["snapshotTick"] = tick;
        var text = node.ToJsonString(Web);
        var raw = JsonDocument.Parse(text).RootElement.Clone();
        var accepted = await run.Session.SubmitAsync(player, "numeric-priority", input, raw, default);
        Assert.True(accepted.Accepted, accepted.ErrorMessage);
        if (cold) AssertRecovered(await run.Recovery("before-invalid-numeric"));
        var count = run.Entries.Count;
        var hash = MatchStateHasher.Hash(run.State);
        var tickText = tick.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var invalid = JsonDocument.Parse(text.Replace("\"snapshotTick\":" + tickText,
            "\"snapshotTick\":" + tickText + "e0", StringComparison.Ordinal)).RootElement.Clone();
        var rejected = await run.Session.SubmitAsync(player, "numeric-priority", input, invalid, default);
        Assert.Equal(ErrorCodes.ClientIntentConflict, rejected.ErrorCode);
        Assert.Empty(rejected.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(hash, MatchStateHasher.Hash(run.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualTypedUnknownInputKeepsItsIntentAcrossDocumentedJsonbNumericStorage(bool jsonbOutputForm)
    {
        var run = await Run.Create("binding-typed-jsonb-numeric-" + jsonbOutputForm);
        var player = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        var argument = JsonDocument.Parse("{\"reading\":1.230e-5}").RootElement.Clone();
        var input = new UnsupportedCommand("NUMERIC_UNKNOWN", argument);
        const string intent = "typed-jsonb-number";
        var rejected = await run.Session.SubmitAsync(player, intent, input, null, default);
        Assert.Equal(ErrorCodes.UnsupportedCommand, rejected.ErrorCode);
        Assert.Null(run.Entries[^1].RawCommand);
        Assert.NotNull(run.Entries[^1].TypedCommand);
        var original = Frame(run);
        var before = JsonSerializer.Serialize(run.Entries, Web);
        var count = run.Entries.Count;
        using var command = new NpgsqlCommand();
        typeof(PostgresMatchJournal).GetMethod("AddCommandPayload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [command, run.Entries[^1]]);
        var payload = Assert.IsType<string>(command.Parameters["payload"].Value);
        Assert.Contains("1.230e-5", payload, StringComparison.Ordinal);
        var stored = jsonbOutputForm ? payload.Replace("1.230e-5", "0.00001230", StringComparison.Ordinal) : payload;
        var decoded = JsonSerializer.Deserialize<RecoveredCommand>(stored, Web)!;
        Assert.Null(decoded.RawCommand);
        var readTyped = Assert.IsType<TypedCommandInput>(typeof(PostgresMatchRecoveryStore)
            .GetMethod("ReadTypedCommand", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [stored]));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(readTyped, Web),
            JsonSerializer.SerializeToNode(decoded.TypedCommand, Web)));
        var persisted = original with { Commands = original.Commands.Select(recorded =>
            recorded.ClientIntentId == intent ? decoded : recorded).ToArray() };
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(persisted, new CoreRuleEngine(), default);
        Assert.Empty(audit);
        var store = new RawEnvelopeFrameStore(persisted);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, store);
        var restored = await registry.GetOrCreateAsync(run.State.RoomId, default);
        var duplicate = await restored.SubmitAsync(player, intent, input, null, default);
        run.Save("typed-documented-jsonb-number-output", new { jsonbOutputForm, payload, stored, persisted, audit, duplicate,
            noDatabaseExecutionClaimed = true });
        Assert.Equal(ErrorCodes.UnsupportedCommand, duplicate.ErrorCode);
        Assert.Empty(duplicate.Events);
        Assert.Equal(count, run.Entries.Count);
        Assert.Equal(before, JsonSerializer.Serialize(run.Entries, Web));
        var changed = await restored.SubmitAsync(player, intent, new UnsupportedCommand("NUMERIC_UNKNOWN",
            JsonDocument.Parse("{\"reading\":0.00001231}").RootElement.Clone()), null, default);
        Assert.Equal(ErrorCodes.ClientIntentConflict, changed.ErrorCode);
        Assert.Empty(changed.Events);
        Assert.Equal(count, run.Entries.Count);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task OriginalInputRetainsItsReceiptAcrossDocumentedJsonbNestedDuplicateKeyStorage(
        bool rawPresent, bool jsonbOutputForm)
    {
        var run = await Run.Create($"binding-jsonb-nested-duplicates-{rawPresent}-{jsonbOutputForm}");
        Assert.Equal(12, run.Entries.Sum(entry => entry.Events.Count));
        var actor = Assert.IsType<string>(ResolutionResult.OpeningMulliganPlayerId(run.State));
        const string duplicateObject = "{\"trace\":1,\"trace\":2}";
        using var argument = JsonDocument.Parse(duplicateObject);
        GameCommand input = rawPresent ? new MulliganCommand([run.State.PlayerZones[actor].Hand[0]])
            : new UnsupportedCommand("NESTED_DUPLICATE_UNKNOWN", argument.RootElement.Clone());
        JsonElement? raw = null;
        if (rawPresent)
        {
            var node = JsonSerializer.SerializeToNode(input, input.GetType(), Web)!.AsObject();
            node["promptId"] = run.Session.PromptFor(actor).PromptId;
            node["snapshotTick"] = run.State.Tick;
            var text = node.ToJsonString(Web);
            using var wire = JsonDocument.Parse(text[..^1] + ",\"clientNote\":" + duplicateObject + "}");
            raw = wire.RootElement.Clone();
        }
        const string intent = "original-nested-duplicate-input";
        var original = await run.Session.SubmitAsync(actor, intent, input, raw, default);
        Assert.Equal(rawPresent, original.Accepted);
        if (!rawPresent) Assert.Equal(ErrorCodes.UnsupportedCommand, original.ErrorCode);
        var journalBefore = JsonSerializer.Serialize(run.Entries, Web);
        var stateHash = MatchStateHasher.Hash(run.State);
        var warm = await run.Session.SubmitAsync(actor, intent, input, raw, default);
        Assert.Equal(original.Accepted, warm.Accepted);
        Assert.Equal(original.ErrorCode, warm.ErrorCode);

        using var command = new NpgsqlCommand();
        typeof(PostgresMatchJournal).GetMethod("AddCommandPayload", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [command, run.Entries[^1]]);
        var payload = Assert.IsType<string>(command.Parameters["payload"].Value);
        Assert.Contains(duplicateObject, payload, StringComparison.Ordinal);
        // PostgreSQL documents that jsonb keeps only the last duplicate object key.
        // This exact stored-form fixture uses the real writer/reader without a database run.
        // https://www.postgresql.org/docs/16/datatype-json.html
        var stored = jsonbOutputForm ? payload.Replace(duplicateObject, "{\"trace\":2}", StringComparison.Ordinal) : payload;
        var decoded = JsonSerializer.Deserialize<RecoveredCommand>(stored, Web)!;
        var readRaw = typeof(PostgresMatchRecoveryStore).GetMethod("ReadRawCommand", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [stored]);
        var readTyped = typeof(PostgresMatchRecoveryStore)
            .GetMethod("ReadTypedCommand", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [stored]);
        Assert.Equal(rawPresent, readRaw is JsonElement);
        if (rawPresent)
        {
            Assert.Null(readTyped);
            Assert.Null(decoded.TypedCommand);
            Assert.Equal(Assert.IsType<JsonElement>(readRaw).GetRawText(), decoded.RawCommand!.Value.GetRawText());
        }
        else
        {
            Assert.Equal(MatchStateHasher.HashValue(Assert.IsType<TypedCommandInput>(readTyped).Value),
                MatchStateHasher.HashValue(decoded.TypedCommand!.Value));
        }
        var frame = Frame(run);
        var persisted = frame with { Commands = frame.Commands.Select(recorded =>
            recorded.ClientIntentId == intent ? decoded : recorded).ToArray() };
        var errors = MatchRecoveryValidator.Validate(persisted.RoomId, persisted.LastEventSequence,
            persisted.Commands, persisted.Events, persisted.PlayerViews, persisted.AuthoritativeState,
            persisted.CurrentTick, persisted.SpectatorReplayFrame);
        Assert.Empty(errors);
        var audit = await MatchActionLogReplayer.ValidateRecoveryFrameAsync(persisted, new CoreRuleEngine(), default);
        Assert.Empty(audit);
        var registry = new InMemoryMatchSessionRegistry(new CoreRuleEngine(), run, new RawEnvelopeFrameStore(persisted));
        var cold = await registry.GetOrCreateAsync(run.State.RoomId, default);
        var duplicate = await cold.SubmitAsync(actor, intent, input, raw, default);
        run.Save("documented-jsonb-nested-duplicate-output", new { rawPresent, jsonbOutputForm, raw, input,
            payload, stored, frame, persisted, errors, audit, original, warm, duplicate, noDatabaseExecutionClaimed = true });
        Assert.Empty(duplicate.Events);
        Assert.Equal(stateHash, MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(journalBefore, JsonSerializer.Serialize(run.Entries, Web));
        Assert.Equal(original.Accepted, duplicate.Accepted);
        Assert.Equal(original.ErrorCode, duplicate.ErrorCode);
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
