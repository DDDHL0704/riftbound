using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BombRebuildContext(
    string InstanceId,
    string CreatingStackItemId,
    string CardNo,
    string ControllerId,
    ObjectBinding Source,
    int BombCount,
    int TurnNumber,
    string TurnPlayerId,
    int ExecutionIndex,
    long CreatedAtTick,
    string BombTokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo);

public sealed partial class CoreRuleEngine
{
    // This is deliberately a mechanism effect.  A card implementation records
    // the number of bombs it destroyed and calls TryScheduleBombRebuild; it does
    // not get to choose a battlefield or create token objects itself.
    internal const string BombRebuildEffect = "RAD_BOMB_REBUILD";
    internal const string BombRebuildPrefix = "BOMB_REBUILD:";
    internal const string BombRebuildChoiceWindow = "BOMB_REBUILD_BATTLEFIELD";
    private const string BombRebuildTriggerKind = "TURN_END_STEP_STARTED";

    internal static string BombRebuildMarker(BombRebuildContext context)
        => BombRebuildPrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(context));

    internal static BombRebuildContext? ReadBombRebuild(string marker)
    {
        try
        {
            if (!marker.StartsWith(BombRebuildPrefix, StringComparison.Ordinal)) return null;
            var context = JsonSerializer.Deserialize<BombRebuildContext>(
                Convert.FromBase64String(marker[BombRebuildPrefix.Length..]));
            return context is not null && BombRebuildMarker(context) == marker ? context : null;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }

    internal static bool ValidBombRebuild(MatchState state, BombRebuildContext context)
        => context.Source is { Generation: >= 0 }
            && !string.IsNullOrWhiteSpace(context.InstanceId)
            && !string.IsNullOrWhiteSpace(context.CreatingStackItemId)
            && !string.IsNullOrWhiteSpace(context.CardNo)
            && !string.IsNullOrWhiteSpace(context.ControllerId)
            && !string.IsNullOrWhiteSpace(context.TurnPlayerId)
            && context.BombCount > 0
            && context.ExecutionIndex >= 0
            && context.CreatedAtTick > 0
            && context.CreatedAtTick <= state.Tick
            && context.TurnNumber == state.TurnNumber
            && context.TurnPlayerId == state.TurnPlayerId
            && context.InstanceId == $"{context.CreatingStackItemId}:BOMB-REBUILD:{context.ExecutionIndex}"
            && context.BombTokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo
            && state.Seats.ContainsKey(context.ControllerId)
            && state.Seats.ContainsKey(context.TurnPlayerId)
            && state.CardObjects.TryGetValue(context.Source.ObjectId, out var source)
            && source.ObjectGeneration >= context.Source.Generation
            && (source.ObjectGeneration != context.Source.Generation || source.CardNo == context.CardNo);

    // Called by a card's destruction-cost resolution.  The helper only records
    // a delayed instruction; the end-step choice and token creation happen in
    // the normal trigger/stack pipeline below.
    internal static bool TryScheduleBombRebuild(
        MatchState state,
        StackItemState item,
        int bombCount,
        List<string> effects,
        List<GameEvent> events)
    {
        if (bombCount <= 0 || state.EndStep is not null || state.Phase == MatchPhases.TurnEnd
            || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
            || source.ObjectGeneration < 0
            || string.IsNullOrWhiteSpace(item.CardNo)
            || !state.Seats.ContainsKey(item.ControllerId)) return false;

        var context = new BombRebuildContext(
            $"{item.StackItemId}:BOMB-REBUILD:{item.CompletedRepeatExecutions}",
            item.StackItemId,
            item.CardNo,
            item.ControllerId,
            new(item.SourceObjectId, source.ObjectGeneration),
            bombCount,
            state.TurnNumber,
            state.TurnPlayerId,
            item.CompletedRepeatExecutions,
            RuleChoices.Value?.CompletionTick ?? state.Tick);
        if (!ValidBombRebuild(state, context)) return false;
        var marker = BombRebuildMarker(context);
        if (effects.Contains(marker, StringComparer.Ordinal)) return false;
        effects.Add(marker);
        events.Add(new("BOMB_REBUILD_SCHEDULED", "记录回合结束时重建炸弹的数量", new Dictionary<string, object?>
        {
            ["sourceObjectId"] = item.SourceObjectId,
            ["controllerId"] = item.ControllerId,
            ["bombCount"] = bombCount,
            ["turnNumber"] = state.TurnNumber
        }));
        return true;
    }

    internal static IReadOnlyList<string> BombRebuildBattlefieldTargets(MatchState state, string controllerId)
    {
        if (!state.Seats.ContainsKey(controllerId)) return [];
        return state.PlayerZones
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .SelectMany(entry => entry.Value.Battlefields)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Where(id => state.CardObjects.TryGetValue(id, out var card)
                && !card.IsFaceDown && IsBattlefieldCardObject(card)
                && BattlefieldLocalRules.Battlefield(state, id) is not null)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<TriggerQueueItemState> CaptureBombRebuildEndAbilities(MatchState state)
    {
        foreach (var marker in state.UntilEndOfTurnEffects.Where(e => e.StartsWith(BombRebuildPrefix, StringComparison.Ordinal)))
        {
            var context = ReadBombRebuild(marker)
                ?? throw new InvalidOperationException("Invalid bomb rebuild instruction.");
            if (!ValidBombRebuild(state, context))
                throw new InvalidOperationException("Invalid bomb rebuild birth context.");
            yield return new(
                context.InstanceId + ":END",
                context.ControllerId,
                context.Source.ObjectId,
                BombRebuildEffect,
                BombRebuildTriggerKind,
                TimingStates.NeutralClosed)
            {
                SourceCardNo = context.CardNo,
                BombRebuild = context
            };
        }
    }

    internal static bool ValidBombRebuildTrigger(MatchState state, TriggerQueueItemState trigger)
        => trigger.BombRebuild is { } context
            && trigger.EffectKind == BombRebuildEffect
            && trigger.TriggeredByEventKind == BombRebuildTriggerKind
            && trigger.TriggerId == context.InstanceId + ":END"
            && trigger.SourceObjectId == context.Source.ObjectId
            && trigger.ControllerId == context.ControllerId
            && trigger.SourceCardNo == context.CardNo
            && ValidBombRebuild(state, context);

    internal static bool ValidBombRebuildStack(MatchState state, StackItemState item)
    {
        if (item.BombRebuild is not { } context
            || item.EffectKind != BombRebuildEffect
            || item.StackItemId != "ordered-" + context.InstanceId + ":END"
            || item.SourceObjectId != context.Source.ObjectId
            || item.ControllerId != context.ControllerId
            || item.CardNo != context.CardNo
            || item.OptionalCosts.Count != 0
            || item.EffectRepeatCount != 1
            || item.DamageAmount != 0
            || !ValidBombRebuild(state, context)) return false;

        if (item.TargetGenerations is null) return item.TargetObjectIds.Count == 0;
        if (item.TargetObjectIds.Count != 1 || item.TargetGenerations.Count != 1
            || !new HashSet<string>(item.TargetGenerations.Keys, StringComparer.Ordinal)
                .SetEquals(item.TargetObjectIds)
            || item.TargetGenerations.Values.Any(generation => generation < 0)
            || !BombRebuildBattlefieldTargets(state, item.ControllerId)
                .Contains(item.TargetObjectIds[0], StringComparer.Ordinal)
            || !state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var battlefield))
            return false;
        return battlefield.ObjectGeneration == item.TargetGenerations[item.TargetObjectIds[0]];
    }

    private static ResolutionResult PrepareBombRebuildTarget(ResolutionResult result, StackItemState item)
    {
        var legal = BombRebuildBattlefieldTargets(result.State, item.ControllerId);
        if (legal.Count == 0)
            return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = new PendingCardChoiceState(
            BombRebuildChoiceWindow + ":" + item.StackItemId,
            BombRebuildChoiceWindow,
            item.ControllerId,
            1,
            1,
            legal,
            legal,
            "选择一处战场，将记录数量的炸弹指示物全部打出到此处",
            item.SourceObjectId,
            item.EffectKind)
        {
            ResolvingStackItemId = item.StackItemId
        };
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidBombRebuildChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(item => item.StackItemId == choice.ResolvingStackItemId) is { BombRebuild: { } context } item
            && ValidBombRebuildStack(state, item)
            && item.TargetGenerations is null
            && choice.ChoiceId == BombRebuildChoiceWindow + ":" + item.StackItemId
            && choice.ChoiceWindow == BombRebuildChoiceWindow
            && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 1
            && choice.MaxCount == 1
            && choice.ContextObjectIds.SequenceEqual(choice.LegalObjectIds)
            && choice.LegalObjectIds.SequenceEqual(BombRebuildBattlefieldTargets(state, context.ControllerId));

    private static ResolutionResult ResolveBombRebuildChoice(
        MatchState state,
        PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidBombRebuildChoice(state, choice) || selected.Count != 1)
            return RejectWithCorePrompts(state, "炸弹重建战场选择已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var battlefieldId = selected[0];
        var next = state with
        {
            Tick = state.Tick + 1,
            PendingCardChoice = null,
            ActivePlayerId = item.ControllerId,
            PriorityPlayerId = item.ControllerId,
            PassedPriorityPlayerIds = [],
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId
                ? i with
                {
                    TargetObjectIds = [battlefieldId],
                    TargetGenerations = new Dictionary<string, long>
                    {
                        [battlefieldId] = state.CardObjects[battlefieldId].ObjectGeneration
                    }
                }
                : i).ToArray()
        };
        return new(true, null, next, [new("BOMB_REBUILD_TARGET_CONFIRMED", "炸弹重建战场已确认，可以响应", new Dictionary<string, object?>
        {
            ["sourceObjectId"] = item.SourceObjectId,
            ["battlefieldObjectId"] = battlefieldId,
            ["bombCount"] = item.BombRebuild!.BombCount
        })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveBombRebuild(MatchState state, StackItemState item)
    {
        if (!ValidBombRebuildStack(state, item))
            throw new InvalidOperationException("Invalid bomb rebuild stack item.");
        var battlefieldId = item.TargetObjectIds.SingleOrDefault();
        if (battlefieldId is null)
            return NoopStackResolutionResult(state);

        var context = item.BombRebuild!;
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        var locations = ReconcileObjectLocations(state.ObjectLocations, zones);
        var events = new List<GameEvent>
        {
            BuildTriggerResolvedEvent(new(item.StackItemId, item.ControllerId, item.SourceObjectId,
                item.EffectKind, BombRebuildTriggerKind) { SourceCardNo = context.CardNo, BombRebuild = context }),
            new("BOMB_REBUILD_RESOLVED", "回合结束重建炸弹", new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId,
                ["sourceObjectId"] = context.Source.ObjectId,
                ["battlefieldObjectId"] = battlefieldId,
                ["bombCount"] = context.BombCount
            })
        };
        locations = CreateBattlefieldEquipmentTokenBatch(
            zones,
            cards,
            context.ControllerId,
            context.Source.ObjectId,
            context.CardNo,
            "回合结束重建炸弹",
            "炸弹",
            [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy],
            context.BombCount,
            isExhausted: false,
            battlefieldId,
            events,
            locations).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return NoopStackResolutionResult(state) with
        {
            PlayerZones = zones,
            CardObjects = cards,
            ObjectLocations = locations,
            Events = events
        };
    }
}
