using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public static partial class MatchRecoveryValidator
{

    private static void ValidateLocationDamageViews(IReadOnlyList<object?> items, MatchState state, List<string> errors, string label)
    {
        foreach (var item in state.StackItems)
        {
            var projected = items.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId);
            try
            {
                var json = JsonSerializer.SerializeToElement(projected);
                var context = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("damageDistribution", out var value)
                    ? value.Deserialize<DamageDistributionContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(context) != MatchStateHasher.HashValue(item.DamageDistribution))
                    errors.Add($"{label} distributed damage context differs from authoritative state");
            }
            catch (JsonException) { errors.Add($"{label} invalid distributed damage context"); }
        }
        foreach (var item in state.StackItems.Where(item => item?.LocationDamage is not null))
        {
            var projected = items.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId);
            try
            {
                var json = JsonSerializer.SerializeToElement(projected);
                var context = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("locationDamage", out var value)
                    ? value.Deserialize<LocationDamageContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(context) != MatchStateHasher.HashValue(item.LocationDamage))
                    errors.Add($"{label} per-location damage context differs from authoritative state");
            }
            catch (JsonException) { errors.Add($"{label} invalid per-location damage context"); }
        }
    }

    private static void ValidateBeginningRecycleViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        void Check(object? payload, BeginningRecycleContext? expected)
        {
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var actual = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("beginningRecycle", out var raw)
                    ? raw.Deserialize<BeginningRecycleContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add($"{label} beginning recycle context differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed beginning recycle context"); }
        }
        foreach (var item in state.StackItems)
            Check(snapshot.Stack.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId), item.BeginningRecycle);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue))
            foreach (var item in state.TriggerQueue)
                Check(queue.FirstOrDefault(payload => TryReadObjectString(payload, "triggerId", out var id) && id == item.TriggerId), item.BeginningRecycle);
    }

    private static void ValidateBeginningRuneViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        void Check(object? payload, BeginningRuneContext? expected)
        {
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var actual = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("beginningRune", out var raw)
                    ? raw.Deserialize<BeginningRunePublicContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(CoreRuleEngine.PublicBeginningRune(expected)))
                    errors.Add($"{label} beginning rune context differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed beginning rune context"); }
        }
        foreach (var item in state.StackItems)
            Check(snapshot.Stack.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId), item.BeginningRune);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue))
            foreach (var item in state.TriggerQueue)
                Check(queue.FirstOrDefault(payload => TryReadObjectString(payload, "triggerId", out var id) && id == item.TriggerId), item.BeginningRune);
    }

    private static void ValidateBeginningDrawViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        void Check(object? payload, BeginningDrawContext? expected)
        {
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var actual = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("beginningDraw", out var raw)
                    ? raw.Deserialize<BeginningDrawContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add($"{label} beginning draw context differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed beginning draw context"); }
        }
        foreach (var item in state.StackItems)
            Check(snapshot.Stack.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId), item.BeginningDraw);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue))
            foreach (var item in state.TriggerQueue)
                Check(queue.FirstOrDefault(payload => TryReadObjectString(payload, "triggerId", out var id) && id == item.TriggerId), item.BeginningDraw);
    }

    private static void ValidateEphemeralStartViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        void Check(object? payload, EphemeralStartContext? expected)
        {
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var actual = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("ephemeralStart", out var raw)
                    ? raw.Deserialize<EphemeralStartContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add($"{label} ephemeral start context differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed ephemeral start context"); }
        }
        foreach (var item in state.StackItems)
            Check(snapshot.Stack.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId), item.EphemeralStart);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue))
            foreach (var item in state.TriggerQueue)
                Check(queue.FirstOrDefault(payload => TryReadObjectString(payload, "triggerId", out var id) && id == item.TriggerId), item.EphemeralStart);
    }

    private static void ValidateBattlefieldStartViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        void Check(object? payload, BattlefieldStartContext? expected)
        {
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var actual = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("battlefieldStart", out var raw)
                    ? raw.Deserialize<BattlefieldStartContext>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add($"{label} battlefield start context differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed battlefield start context"); }
        }
        foreach (var item in state.StackItems)
        {
            var payload = snapshot.Stack.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId);
            Check(payload, item.BattlefieldStart);
            if (item.BattlefieldStart is not { DrawCount: > 0 }) continue;
            try {
                var json = JsonSerializer.SerializeToElement(payload);
                var receipt = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("triggerCost", out var raw)
                    ? raw.Deserialize<TriggerCostReceipt>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
                if (MatchStateHasher.HashValue(receipt) != MatchStateHasher.HashValue(item.TriggerCost))
                    errors.Add($"{label} battlefield destruction cost receipt differs from authoritative state");
            } catch (JsonException) { errors.Add($"{label} malformed battlefield destruction cost receipt"); }
        }
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue))
            foreach (var item in state.TriggerQueue)
                Check(queue.FirstOrDefault(payload => TryReadObjectString(payload, "triggerId", out var id) && id == item.TriggerId), item.BattlefieldStart);
    }

    private static void ValidateGrantedTagViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        if (snapshot.Players is null) return;
        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objects)
                || !TryReadObjectDictionaryValue(objects, out var cards)) continue;
            foreach (var (id, payload) in cards)
            {
                if (!state.CardObjects.TryGetValue(id, out var card) || card is null) continue;
                var hasMarks = TryReadObjectValue(payload, "grantedTags", out var marks);
                if (!TryReadObjectValue(payload, "damage", out _))
                {
                    if (hasMarks) errors.Add($"{label} hidden card {id} exposes granted tags");
                    continue;
                }
                var expected = card.GrantedTags;
                if (expected is null ? hasMarks && !IsNullSnapshotPayloadValue(marks)
                    : !TryReadObjectStringList(payload, "grantedTags", out var actual) || !actual.SequenceEqual(expected))
                    errors.Add($"{label} card {id} granted tags differ from authoritative state");
            }
        }
    }

    private static void ValidateDamageMarkerViews(SnapshotDto snapshot, MatchState state, List<string> errors, string label)
    {
        if (snapshot.Players is null) return;
        foreach (var playerPayload in snapshot.Players.Values)
        {
            if (!TryReadObjectValue(playerPayload, "objects", out var objects)
                || !TryReadObjectDictionaryValue(objects, out var cards)) continue;
            foreach (var (id, payload) in cards)
            {
                if (!state.CardObjects.TryGetValue(id, out var card) || card is null) continue;
                var hasMarks = TryReadObjectValue(payload, "damageMarkedByPlayerIds", out var marks);
                if (!TryReadObjectValue(payload, "damage", out _))
                {
                    if (hasMarks) errors.Add($"{label} hidden card {id} exposes damage marking players");
                    continue;
                }
                var expected = card.DamageMarkedByPlayerIds;
                if (expected is null ? hasMarks && !IsNullSnapshotPayloadValue(marks)
                    : !TryReadObjectStringList(payload, "damageMarkedByPlayerIds", out var actual) || !actual.SequenceEqual(expected))
                    errors.Add($"{label} card {id} damage marking players differ from authoritative state");
            }
        }
    }

    private static void ValidateRepeatDestinationViews(IReadOnlyList<object?> stackItems, MatchState state,
        List<string> errors, string label)
    {
        foreach (var item in state.StackItems.Where(item => item?.RepeatExecutions is not null))
        {
            var projected = stackItems.FirstOrDefault(payload => TryReadObjectString(payload, "stackItemId", out var id) && id == item.StackItemId);
            if (projected is null) continue; // Existing stack identity checks report missing items.
            try
            {
                var json = JsonSerializer.SerializeToElement(projected);
                var hasExecutions = json.TryGetProperty("repeatExecutions", out var executions);
                if (TryReadObjectString(projected, "sourceVisibility", out var visibility) && visibility == "HIDDEN")
                {
                    if (hasExecutions) errors.Add($"{label} hidden spell repeat destinations exposed");
                    continue;
                }
                var expected = item.RepeatExecutions!;
                if (!hasExecutions || executions.ValueKind != JsonValueKind.Array || executions.GetArrayLength() != expected.Count
                    || executions.EnumerateArray().Select((execution, index) => execution.ValueKind != JsonValueKind.Object
                        || !execution.TryGetProperty("destination", out var destination) || destination.ValueKind != JsonValueKind.String
                        || destination.GetString() != expected[index]?.Destination
                        || (execution.TryGetProperty("movementDeclarationPlayerId", out var declaration)
                            ? declaration.ValueKind == JsonValueKind.Null ? null : declaration.ValueKind == JsonValueKind.String
                                ? declaration.GetString() : "INVALID" : null) != expected[index]?.MovementDeclarationPlayerId).Any(mismatch => mismatch))
                    errors.Add($"{label} spell repeat destinations differ from authoritative state");
            }
            catch (JsonException) { errors.Add($"{label} invalid spell repeat destinations"); }
        }
    }

    private static ReflexiveDuelContext? ReadReflexiveDuelView(object? payload)
    {
        try {
            var json=JsonSerializer.SerializeToElement(payload);
            return json.ValueKind==JsonValueKind.Object && json.TryGetProperty("reflexiveDuel",out var raw) && raw.ValueKind!=JsonValueKind.Null
                ? raw.Deserialize<ReflexiveDuelContext>(new JsonSerializerOptions {PropertyNameCaseInsensitive=true}) : null;
        } catch(JsonException) {return null;}
    }
    private static bool ValidateReflexiveDuelView(object? payload,string? effect,List<string> errors)
    {
        var context=ReadReflexiveDuelView(payload);
        if(effect!=CoreRuleEngine.ReflexiveDuelEffect && context is null)return false;
        if(context is null || !CoreRuleEngine.ValidReflexiveDuel(context,effect??string.Empty))
        {errors.Add("invalid public reflexive movement duel");return false;}
        return true;
    }

    private static void CompareMovementObserverView(object? payload, MovementObserverContext? authority, List<string> errors, string label)
    {
        MovementObserverContext? actual = null;
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("movementObserver", out var raw) && raw.ValueKind != JsonValueKind.Null)
                actual = raw.Deserialize<MovementObserverContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        } catch (JsonException) { errors.Add("invalid public movement observer encoding"); }
        if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(CoreRuleEngine.PublicMovementObserver(authority)))
            errors.Add($"{label} captured movement observer differs from authoritative state");
    }

    private static void ValidateBattlefieldConquestMightPayload(object? payload, string? effect, List<string> errors)
    {
        var context = ReadBattlefieldConquestMightView(payload);
        if (CoreRuleEngine.IsCapturedConquestDrawKind(effect) || context is not null)
            if (context is null || !CoreRuleEngine.ValidCapturedConquestDrawKind(context, effect)
                || !CoreRuleEngine.ValidBattlefieldConquestMightFrame(context)) errors.Add("invalid public captured conquest Might");
    }

    private static void ValidateBattlefieldConquestMightStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, List<string> errors, string label)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            TryReadObjectString(payload, "effectKind", out var effect);
            ValidateBattlefieldConquestMightPayload(payload, effect, errors);
            var expected = authority.StackItems.FirstOrDefault(i => i.StackItemId == id);
            if (MatchStateHasher.HashValue(ReadBattlefieldConquestMightView(payload))
                != MatchStateHasher.HashValue(expected?.BattlefieldConquestMight))
                errors.Add($"{label} captured conquest Might differs from authoritative state");
        }
    }

    private static void ValidateLastBreathMightPayload(object? payload, string? effect, List<string> errors)
    {
        var context = ReadLastBreathMightView(payload);
        if (effect == TriggerKinds.UnitLastBreathPowerfulDraw || context is not null)
            if (context is null || effect != TriggerKinds.UnitLastBreathPowerfulDraw || !CoreRuleEngine.ValidLastBreathMightFrame(context))
                errors.Add("invalid public captured last-breath Might");
    }

    private static LastBreathMightContext? ReadLastBreathMightView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("lastBreathMight", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<LastBreathMightContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static void ValidateLastBreathMightStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, List<string> errors, string label)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = authority.StackItems.FirstOrDefault(i => i.StackItemId == id);
            if (MatchStateHasher.HashValue(ReadLastBreathMightView(payload)) != MatchStateHasher.HashValue(expected?.LastBreathMight))
                errors.Add($"{label} captured last-breath Might differs from authoritative state");
        }
    }

    private static string CapturedInsightLabel(InsightTriggerContext? context)
        => context is { Kind: "DUEL" } ? "captured duel Insight"
            : context is { Kind: "EXHAUSTED" } ? "captured exhausted Insight" : context is { Kind: "HELD" } ? "captured held Insight" : context is { Kind: "EMPOWERED" } ? "captured empowered Insight" : "captured last-breath Insight";

    private static InsightTriggerContext? CapturedInsight(InsightTriggerContext? context)
        => CoreRuleEngine.IsCapturedInsightKind(context?.Kind) ? context : null;

    private static InsightTriggerContext? ReadCapturedInsightView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind != JsonValueKind.Object) return null;
            var hasLastBreath = json.TryGetProperty("lastBreathInsight", out var lastBreath) && lastBreath.ValueKind != JsonValueKind.Null;
            var hasDuel = json.TryGetProperty("duelInsight", out var duel) && duel.ValueKind != JsonValueKind.Null;
            var hasEmpowered = json.TryGetProperty("empoweredInsight", out var empowered) && empowered.ValueKind != JsonValueKind.Null;
            var hasHeld = json.TryGetProperty("heldInsight", out var held) && held.ValueKind != JsonValueKind.Null;
            var hasExhausted = json.TryGetProperty("exhaustedInsight", out var exhausted) && exhausted.ValueKind != JsonValueKind.Null;
            if ((hasExhausted ? 1 : 0) + (hasHeld ? 1 : 0) + (hasLastBreath ? 1 : 0) + (hasDuel ? 1 : 0) + (hasEmpowered ? 1 : 0) != 1) return null;
            var context = (hasExhausted ? exhausted : hasHeld ? held : hasLastBreath ? lastBreath : hasDuel ? duel : empowered).Deserialize<InsightTriggerContext>(new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow });
            return context?.Kind == (hasExhausted ? "EXHAUSTED" : hasHeld ? "HELD" : hasLastBreath ? "LAST_BREATH" : hasDuel ? "DUEL" : "EMPOWERED") ? context : null;
        } catch (JsonException) { return null; }
    }

    private static bool ValidateCapturedInsightView(object? payload, string? effect, string? controller,
        string? source, List<string> errors)
    {
        var hasLastBreath = TryReadObjectValue(payload, "lastBreathInsight", out var lastBreath) && !IsNullSnapshotPayloadValue(lastBreath);
        var hasDuel = TryReadObjectValue(payload, "duelInsight", out var duel) && !IsNullSnapshotPayloadValue(duel);
        var hasEmpowered = TryReadObjectValue(payload, "empoweredInsight", out var empowered) && !IsNullSnapshotPayloadValue(empowered);
        var hasHeld = TryReadObjectValue(payload, "heldInsight", out var held) && !IsNullSnapshotPayloadValue(held);
        var hasExhausted = TryReadObjectValue(payload, "exhaustedInsight", out var exhausted) && !IsNullSnapshotPayloadValue(exhausted);
        if (!hasExhausted && !hasHeld && !hasLastBreath && !hasDuel && !hasEmpowered) return false;
        var context = ReadCapturedInsightView(payload);
        if ((hasExhausted ? 1 : 0) + (hasHeld ? 1 : 0) + (hasLastBreath ? 1 : 0) + (hasDuel ? 1 : 0) + (hasEmpowered ? 1 : 0) != 1
            || context is null || !CoreRuleEngine.IsCapturedInsightKind(context.Kind) || string.IsNullOrWhiteSpace(source)
            || !CoreRuleEngine.ValidInsightTrigger(context, effect ?? "", controller ?? "")
            || context.Origin is { } origin && (origin.Unit is null || origin.Unit.ObjectId != source))
        {
            errors.Add($"invalid public {(hasDuel ? "captured duel Insight" : hasEmpowered ? "captured empowered Insight" : "captured last-breath Insight")}");
            return false;
        }
        // This proves only the public shape. Exact typed-authority comparisons
        // below still bind the historical token, equipment, face and trigger.
        return context.SourceTokenFactoryCardNo is not null;
    }

    private static void ValidateCapturedInsightStackViews(IReadOnlyList<object?> payloads,
        MatchState authority, List<string> errors, string label)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            TryReadObjectString(payload, "sourceObjectId", out var source);
            TryReadObjectString(payload, "effectKind", out var effect);
            TryReadObjectString(payload, "controllerId", out var controller);
            ValidateCapturedInsightView(payload, effect, controller, source, errors);
            var expected = authority.StackItems.FirstOrDefault(i => i.StackItemId == id);
            if (MatchStateHasher.HashValue(ReadCapturedInsightView(payload)) != MatchStateHasher.HashValue(CapturedInsight(expected?.InsightContext)))
                errors.Add($"{label} {CapturedInsightLabel(expected?.InsightContext)} differs from authoritative state");
        }
    }

    private static DeathObserverContext? ReadDeathObserverView(object? payload)
    {
        try {
            var json=JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("deathObserver",out var raw) && raw.ValueKind!=JsonValueKind.Null
                ? raw.Deserialize<DeathObserverContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive=true }) : null;
        } catch (JsonException) { return null; }
    }
    // Retired development records cannot be reconstructed by parsing live objects.
    private static bool IsUncapturedDeathObserver(string? effect, string? id)
        => CoreRuleEngine.IsDeathObserverKind(effect ?? "")
            || id is not null && CoreRuleEngine.IsDeathObserverKind(id[(id.LastIndexOf('-') + 1)..]);

    private static bool ValidateDeathObserverView(object? payload,string? effect,string? controller,string? source,List<string> errors)
    {
        var c=ReadDeathObserverView(payload);
        TryReadObjectString(payload, "triggerId", out var id);
        if (c is null && IsUncapturedDeathObserver(effect, id)) errors.Add("obsolete uncaptured death observer");
        if (effect!=CoreRuleEngine.DeathObserverEffect && c is null) return false;
        if (c is null || !CoreRuleEngine.ValidDeathObserver(c,effect??"",controller??"",source??"")) { errors.Add("invalid public captured death observer"); return false; }
        return true;
    }

    private static void ValidateSourceReplayStackViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads) {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.SourceReplay;
            if (MatchStateHasher.HashValue(ReadSourceReplayView(payload)) != MatchStateHasher.HashValue(expected))
                errors.Add("public source replay progress differs from authoritative state");
        }
    }

    private static void ValidateResolutionDamageChoiceViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.ResolutionDamageChoice;
            try
            {
                var json = JsonSerializer.SerializeToElement(payload);
                if (json.ValueKind != JsonValueKind.Object) continue;
                var actual = json.TryGetProperty("resolutionDamageChoice", out var raw) && raw.ValueKind != JsonValueKind.Null
                    ? raw.Deserialize<ResolutionDamageChoiceContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add("public target controller damage alternative choice differs from authoritative state");
            }
            catch (JsonException) { errors.Add("invalid public target controller damage alternative choice"); }
        }
    }

    private static void ValidateFlowPlayViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.FlowPlay is not null;
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.ValueKind != JsonValueKind.Object) continue;
            var present = json.TryGetProperty("playedWithFlow", out var flag);
            if (present != expected || present && flag.ValueKind != JsonValueKind.True)
                errors.Add("public flow play marker differs from authoritative state");
            if (json.TryGetProperty("flowPlay", out _)) errors.Add("public flow play exposes private generation receipt");
        }
    }

    private static void ValidateDeckPlayPendingCardViews(IReadOnlyList<object?> payloads, MatchState state, List<string> errors)
    {
        foreach (var payload in payloads)
        {
            TryReadObjectString(payload, "stackItemId", out var id);
            var expected = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.DeckPlayPendingCard;
            var capturedReflexive = state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.DeckPlayReflexive;
            var expectedReflexive = capturedReflexive is null ? null : new DeckPlayReflexiveView(capturedReflexive.ExecutionIndex, capturedReflexive.Confirmed);
            try
            {
                var json = JsonSerializer.SerializeToElement(payload);
                if (json.ValueKind != JsonValueKind.Object) continue;
                var actual = json.TryGetProperty("deckPlayPendingCard", out var raw) && raw.ValueKind != JsonValueKind.Null
                    ? raw.Deserialize<DeckPlayPendingCardContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                    errors.Add("public deck pending card differs from authoritative state");
                var actualReflexive = json.TryGetProperty("deckPlayReflexive", out var reflexive) && reflexive.ValueKind != JsonValueKind.Null
                    ? reflexive.Deserialize<DeckPlayReflexiveView>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                if (MatchStateHasher.HashValue(actualReflexive) != MatchStateHasher.HashValue(expectedReflexive))
                    errors.Add("public deck play reflexive differs from authoritative state");
            }
            catch (JsonException) { errors.Add("invalid public deck pending card"); }
        }
    }

    private static void ValidateTurnUnitPlayBoonViews(SnapshotDto snapshot, MatchState state, List<string> errors)
    {
        void Check(IReadOnlyList<object?> payloads, bool queue)
        {
            foreach (var payload in payloads)
            {
                TryReadObjectString(payload, queue ? "triggerId" : "stackItemId", out var id);
                var expected = queue ? state.TriggerQueue.FirstOrDefault(i => i.TriggerId == id)?.TurnUnitPlayBoon
                    : state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.TurnUnitPlayBoon;
                try
                {
                    var json = JsonSerializer.SerializeToElement(payload);
                    if (json.ValueKind != JsonValueKind.Object) continue;
                    var actual = json.TryGetProperty("turnUnitPlayBoon", out var raw) && raw.ValueKind != JsonValueKind.Null
                        ? raw.Deserialize<TurnUnitPlayBoonContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                    if (MatchStateHasher.HashValue(actual) != MatchStateHasher.HashValue(expected))
                        errors.Add("public captured unit play boon differs from authoritative state");
                    var expectedNext = queue ? state.TriggerQueue.FirstOrDefault(i => i.TriggerId == id)?.NextUnitPlayGrant
                        : state.StackItems.FirstOrDefault(i => i.StackItemId == id)?.NextUnitPlayGrant;
                    var actualNext = json.TryGetProperty("nextUnitPlayGrant", out var nextRaw) && nextRaw.ValueKind != JsonValueKind.Null
                        ? nextRaw.Deserialize<NextUnitPlayGrant>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
                    if (MatchStateHasher.HashValue(actualNext) != MatchStateHasher.HashValue(expectedNext))
                        errors.Add("public captured next unit play grant differs from authoritative state");
                }
                catch (JsonException) { errors.Add("invalid public captured unit play boon"); }
            }
        }
        Check(snapshot.Stack, false);
        if (snapshot.Timing is not null && TryReadObjectList(snapshot.Timing, "triggerQueue", out var queue)) Check(queue, true);
    }

    private static void ValidateDelayedDestructionState(MatchState state, List<string> errors)
    {
        if (!CoreRuleEngine.ValidCurrentDelayedDestructions(state)) errors.Add("invalid delayed destruction instruction");
        foreach (var item in state.StackItems)
            if (item.DelayedDestruction is { } c ? c.Destroyed is null || item.EffectKind != CoreRuleEngine.DelayedDestructionEffect
                || !CoreRuleEngine.ValidDelayedDestruction(state, c, item.SourceObjectId, item.ControllerId, item.CardNo)
                : item.EffectKind == CoreRuleEngine.DelayedDestructionEffect) errors.Add("invalid captured delayed destruction stack");
        foreach (var item in state.TriggerQueue)
            if (item.DelayedDestruction is { } c ? c.Destroyed is null || item.EffectKind != CoreRuleEngine.DelayedDestructionEffect
                || item.TriggeredByEventKind != TriggerTimings.UnitDestroyed
                || !CoreRuleEngine.ValidDelayedDestruction(state, c, item.SourceObjectId, item.ControllerId, item.SourceCardNo)
                : item.EffectKind == CoreRuleEngine.DelayedDestructionEffect) errors.Add("invalid captured delayed destruction trigger");
    }

    private static DelayedDestructionContext? ReadDelayedDestructionView(object? payload)
    {
        try {
            var json = JsonSerializer.SerializeToElement(payload);
            return json.ValueKind == JsonValueKind.Object && json.TryGetProperty("delayedDestruction", out var raw)
                && raw.ValueKind != JsonValueKind.Null
                ? raw.Deserialize<DelayedDestructionContext>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) : null;
        } catch (JsonException) { return null; }
    }

    private static string? ValidateAuthoritativeStateRequiredText(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is required");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void ValidateAuthoritativeStateKnownText(
        string valueLabel,
        string? value,
        Func<string, bool> isKnownValue,
        List<string> errors,
        bool rejectRedactionSentinel = false)
    {
        var normalizedValue = ValidateAuthoritativeStateRequiredText(valueLabel, value, errors);
        if (normalizedValue is null)
        {
            return;
        }

        if (rejectRedactionSentinel)
        {
            RejectAuthoritativeStateRedactionSentinel(valueLabel, normalizedValue, errors);
        }

        if (!isKnownValue(normalizedValue))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} is invalid");
        }
    }

    private static string? ValidateAuthoritativeStateOptionalTextValue(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (value is null)
        {
            errors.Add($"authoritative state {valueLabel} value is required");
            return null;
        }

        if (value.Length == 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"authoritative state {valueLabel} is blank");
            return null;
        }

        var normalizedValue = value.Trim();
        if (!string.Equals(value, normalizedValue, StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} {normalizedValue} has surrounding whitespace");
        }

        return normalizedValue;
    }

    private static void RejectAuthoritativeStateRedactionSentinel(
        string valueLabel,
        string? value,
        List<string> errors)
    {
        if (string.Equals(value?.Trim(), "HIDDEN", StringComparison.Ordinal))
        {
            errors.Add($"authoritative state {valueLabel} must not be redacted");
        }
    }
}
