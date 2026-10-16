using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record CreatedTokenReadyContext(string CardNo, ObjectBinding Source, IReadOnlyList<ObjectBinding> Units);

public sealed partial class CoreRuleEngine
{
    internal const string CreatedTokenReadyEffect = "CREATED_TOKEN_READY";

    private static int BaseUnitTokenInstructionCount(MatchState state, StackItemState item, CardBehaviorDefinition behavior)
        => behavior.CreatedBaseUnitTokenCount * Math.Max(1, item.EffectRepeatCount)
            * (behavior.CreatedBaseUnitTokenPerEquipment
                ? CountControlledPublicFieldEquipmentObjects(state.PlayerZones, state.CardObjects, item.ControllerId) : 1);

    internal static bool ValidCreatedTokenReady(MatchState state, CreatedTokenReadyContext context,
        string effect, string sourceId, string? cardNo = null)
        => effect == CreatedTokenReadyEffect && (cardNo is null || context.CardNo == cardNo)
            && CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior) && behavior.CreatedTokenReadyCount > 0
            && context.Source is { Generation: >= 0 } source && source.ObjectId == sourceId
            && state.CardObjects.TryGetValue(sourceId, out var parent) && source.Generation <= parent.ObjectGeneration
            && OfficialCardSourceIdentityGroups.NormalizeCardNo(parent.CardNo) == OfficialCardSourceIdentityGroups.NormalizeCardNo(context.CardNo)
            && context.Units is not null && context.Units.All(u => u is { Generation: >= 0 }
                && u.ObjectId.StartsWith(sourceId + "-TOKEN-", StringComparison.Ordinal)
                && (!state.CardObjects.TryGetValue(u.ObjectId, out var unit) || u.Generation <= unit.ObjectGeneration
                    && (u.Generation != unit.ObjectGeneration || unit.TokenFactoryCardNo == P6TokenFactoryCatalog.SandSoldierTokenCardNo)))
            && context.Units.Select(u => u.ObjectId).Distinct(StringComparer.Ordinal).Count() == context.Units.Count;

    private static bool CreatedTokenReadyHasPayment(StackItemState item)
        => item.CreatedTokenReady is { } c && CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var behavior)
            && behavior.CreatedTokenReadyPowerTrait.Length > 0;

    private static string[] CreatedTokenReadyTargets(MatchState state, StackItemState item)
        => item.CreatedTokenReady!.Units.Where(u => state.CardObjects.TryGetValue(u.ObjectId, out var card)
            && card.ObjectGeneration == u.Generation && IsObjectOnField(state.PlayerZones, u.ObjectId)
            && IsFaceUpNonStandbyUnit(card)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, u.ObjectId))
            .Select(u => u.ObjectId).Order(StringComparer.Ordinal).ToArray();

    internal static bool ValidCreatedTokenReadyStack(MatchState state, StackItemState item)
    {
        if (item.CreatedTokenReady is not { } c)
            return item.EffectKind != CreatedTokenReadyEffect;
        if (!ValidCreatedTokenReady(state, c, item.EffectKind, item.SourceObjectId, item.CardNo)) return false;
        CardBehaviorRegistry.TryGetByCardNo(c.CardNo, out var behavior);
        if (CreatedTokenReadyHasPayment(item)) return item.TargetObjectIds.Count == 0 && item.TargetGenerations is null;
        return item.TriggerCost is null && (item.TargetGenerations is { } generations
            ? item.TargetObjectIds.Count <= behavior.CreatedTokenReadyCount
                && generations.Count == item.TargetObjectIds.Count
                && item.TargetObjectIds.Distinct(StringComparer.Ordinal).Count() == item.TargetObjectIds.Count
                && item.TargetObjectIds.All(id => c.Units.Any(u => u.ObjectId == id && generations.GetValueOrDefault(id, -1) == u.Generation))
            : item.TargetObjectIds.Count == 0 && state.PriorityPlayerId is null);
    }

    private static void AddCreatedTokenReadyEvent(MatchState state, StackItemState item,
        IReadOnlyDictionary<string, CardObjectState> cards, IReadOnlyList<string> ids, List<GameEvent> events)
    {
        var context = new CreatedTokenReadyContext(item.CardNo,
            new(item.SourceObjectId, state.CardObjects[item.SourceObjectId].ObjectGeneration),
            ids.Select(id => new ObjectBinding(id, cards[id].ObjectGeneration)).ToArray());
        events.Add(new("TOKEN_READY_TRIGGER_CREATED", "本次指示物已进场，后续活跃技能等待确认",
            new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                ["createdTokenReady"] = context }));
    }

    private static ResolutionResult QueueCreatedTokenReady(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Kind != "TOKEN_READY_TRIGGER_CREATED" || ev.Payload.GetValueOrDefault("createdTokenReady") is not CreatedTokenReadyContext c) continue;
            events[index] = ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
                ["createdTokenReady"] = JsonSerializer.SerializeToElement(c, new JsonSerializerOptions(JsonSerializerDefaults.Web)) } };
            var trigger = new TriggerQueueItemState($"token-ready-{result.State.Tick}-{index}",
                (string)ev.Payload["playerId"]!, (string)ev.Payload["sourceObjectId"]!, CreatedTokenReadyEffect,
                "TOKEN_PLAY_INSTRUCTION_COMPLETED") { CreatedTokenReady = c };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var next = result.State with { TriggerQueue = queue };
        return result with { State = next, Events = events, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static PendingCardChoiceState CreatedTokenReadyChoice(MatchState state, StackItemState item)
    {
        var legal = CreatedTokenReadyTargets(state, item);
        CardBehaviorRegistry.TryGetByCardNo(item.CreatedTokenReady!.CardNo, out var behavior);
        return new("TOKEN-READY-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId,
            0, Math.Min(behavior.CreatedTokenReadyCount, legal.Length), legal, item.CreatedTokenReady.Units.Select(u => u.ObjectId).ToArray(),
            "选择本次打出的至多两名黄沙士兵；确认后双方可以响应，随后所选单位才变为活跃。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
    }

    private static ResolutionResult PrepareCreatedTokenReady(ResolutionResult result, StackItemState item)
    {
        var choice = CreatedTokenReadyChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0)
            return CompleteTriggerTargetConfirmation(result.State, item with { TargetGenerations = new Dictionary<string, long>() }, result.Events.ToList());
        var next = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = next, Snapshots = ResolutionResult.BuildSnapshots(next), Prompts = BuildCorePrompts(next) };
    }

    private static bool ValidCreatedTokenReadyChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.CreatedTokenReady is null || CreatedTokenReadyHasPayment(item) || item.TargetGenerations is not null
            || !ValidCreatedTokenReadyStack(state, item)) return false;
        var expected = CreatedTokenReadyChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow
            && choice.PlayerId == expected.PlayerId && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 0 && choice.MaxCount == expected.MaxCount
            && choice.LegalObjectIds.Count > 0 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    private static StackResolutionResult ResolveCreatedTokenReady(MatchState state, StackItemState item)
    {
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value); var events = new List<GameEvent>();
        CardBehaviorRegistry.TryGetByCardNo(item.CreatedTokenReady!.CardNo, out var behavior);
        var isPaid = CreatedTokenReadyHasPayment(item);
        if (isPaid && item.TriggerCost is null) return NoopStackResolutionResult(state);
        var recipients = isPaid ? item.CreatedTokenReady!.Units
            : item.CreatedTokenReady!.Units.Where(u => item.TargetObjectIds.Contains(u.ObjectId)).ToArray();
        foreach (var binding in recipients)
        {
            if (!cards.TryGetValue(binding.ObjectId, out var unit) || unit.ObjectGeneration != binding.Generation
                || !IsObjectOnField(state.PlayerZones, binding.ObjectId)
                || !isPaid && !CreatedTokenReadyTargets(state, item).Contains(binding.ObjectId)) continue;
            cards[binding.ObjectId] = ApplyReadyState(unit, behavior, item, binding.ObjectId, out var ready);
            if (ready is not null) events.Add(ready);
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
