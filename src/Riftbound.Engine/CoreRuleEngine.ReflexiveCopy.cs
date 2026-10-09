using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record ObjectBinding(string ObjectId, long Generation);
public sealed record ReflexiveCopyContext(string CardNo, ObjectBinding? CopySource, IReadOnlyList<ObjectBinding> Recipients,
    string? BattlefieldId = null, bool TargetConfirmed = true);

public sealed partial class CoreRuleEngine
{
    internal const string ReflexiveCopyEffect = "REFLEXIVE_COPY_UNIT";

    internal static bool ValidReflexiveCopy(ReflexiveCopyContext context, string effect, string? cardNo = null)
        => effect == ReflexiveCopyEffect && (cardNo is null || cardNo == context.CardNo)
            && (LegendCardHasIdentity(context.CardNo, LeblancLegendIdentityId)
                || CardBehaviorRegistry.TryGetByCardNo(context.CardNo, out var behavior) && behavior.CreatedBaseUnitTokenCopiesFirstTarget)
            && (LegendCardHasIdentity(context.CardNo, LeblancLegendIdentityId)
                ? !string.IsNullOrWhiteSpace(context.BattlefieldId) && (context.TargetConfirmed ? context.CopySource is not null : context.CopySource is null)
                : context.BattlefieldId is null && context.TargetConfirmed)
            && (context.CopySource is null || context.CopySource.Generation >= 0 && !string.IsNullOrWhiteSpace(context.CopySource.ObjectId))
            && context.Recipients is { Count: > 0 } && context.Recipients.All(r => r is not null && r.Generation >= 0 && !string.IsNullOrWhiteSpace(r.ObjectId))
            && context.Recipients.Select(r => r.ObjectId).Distinct().Count() == context.Recipients.Count;

    internal static bool ValidCopyRecipients(MatchState state, ReflexiveCopyContext context)
        => (context.CopySource is not { } source || !state.CardObjects.TryGetValue(source.ObjectId, out var target)
            || source.Generation <= target.ObjectGeneration)
            && context.Recipients.All(r => !state.CardObjects.TryGetValue(r.ObjectId, out var card)
            || r.Generation <= card.ObjectGeneration && (r.Generation != card.ObjectGeneration
                || card.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo && card.CardNo == P6TokenFactoryCatalog.ImageTokenCardNo));

    internal static bool ValidReflexiveCopyStack(MatchState state, StackItemState item)
        => item.ReflexiveCopy is not { BattlefieldId: not null } copy
            || (copy.TargetConfirmed
                ? copy.CopySource is { } source && item.TargetObjectIds.SequenceEqual([source.ObjectId])
                    && item.TargetGenerations is { Count: 1 } generations && generations.TryGetValue(source.ObjectId, out var generation)
                    && generation == source.Generation && !copy.Recipients.Any(r => r.ObjectId == source.ObjectId)
                : state.PriorityPlayerId is null && (item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                    || state.PendingPayment is { PaymentWindow: TriggerTargetCostWindow } payment && payment.ResolvingStackItemId == item.StackItemId
                        && item.TargetObjectIds.Count == 1 && item.TargetGenerations is { Count: 1 } bound
                        && bound.ContainsKey(item.TargetObjectIds[0]) && !copy.Recipients.Any(r => r.ObjectId == item.TargetObjectIds[0])));

    private static void AddReflexiveCopyEvent(List<GameEvent> events, string player, string sourceId,
        string cardNo, CardObjectState? copySource, IReadOnlyList<CardObjectState> recipients, string? battlefieldId = null)
    {
        var context = new ReflexiveCopyContext(cardNo,
            copySource is null ? null : new(copySource.ObjectId, copySource.ObjectGeneration),
            recipients.Select(r => new ObjectBinding(r.ObjectId, r.ObjectGeneration)).ToArray(), battlefieldId, battlefieldId is null);
        events.Add(new("REFLEXIVE_COPY_CREATED", "映像已进场，复制效果等待双方响应", new Dictionary<string, object?> {
            ["playerId"] = player, ["sourceObjectId"] = sourceId, ["reflexiveCopyContext"] = context }));
    }

    private static ResolutionResult QueueReflexiveCopies(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Kind != "REFLEXIVE_COPY_CREATED" || ev.Payload.GetValueOrDefault("reflexiveCopyContext") is not ReflexiveCopyContext context) continue;
            var trigger = new TriggerQueueItemState($"copy-{result.State.Tick}-{index}",
                (string)ev.Payload["playerId"]!, (string)ev.Payload["sourceObjectId"]!, ReflexiveCopyEffect, "UNIT_TOKEN_CREATED") { ReflexiveCopy = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static string[] ReflexiveCopyTargets(MatchState state, StackItemState item)
        => HeldUnitsAt(state, item.ReflexiveCopy!.BattlefieldId!)
            .Where(id => !item.ReflexiveCopy.Recipients.Any(r => r.ObjectId == id)
                && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, id)).ToArray();

    private static ResolutionResult PrepareReflexiveCopyConfirmation(ResolutionResult result)
    {
        var state = result.State;
        var item = state.StackItems.FirstOrDefault(i => i.ReflexiveCopy is { TargetConfirmed: false });
        if (item is null) return result;
        var legal = ReflexiveCopyTargets(state, item);
        if (legal.Length == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var choice = new PendingCardChoiceState("COPY-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION",
            item.ControllerId, 1, 1, legal, [item.ReflexiveCopy!.BattlefieldId!],
            "映像已经进场：选择该战场另一名单位作为复制对象，支付适用的法盾费用后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };
        state = state with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidReflexiveCopyChoice(MatchState state, PendingCardChoiceState choice)
        => state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId) is { ReflexiveCopy: { TargetConfirmed: false } copy } item
            && ValidReflexiveCopy(copy, item.EffectKind, item.CardNo)
            && choice.ChoiceId == "COPY-TARGET:" + item.StackItemId && choice.PlayerId == item.ControllerId
            && choice.SourceObjectId == item.SourceObjectId && choice.EffectKind == item.EffectKind
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(ReflexiveCopyTargets(state, item))
            && choice.ContextObjectIds.SequenceEqual([copy.BattlefieldId!]);

    private static StackResolutionResult ResolveReflexiveCopy(MatchState state, StackItemState item)
    {
        var context = item.ReflexiveCopy!;
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        var events = new List<GameEvent>();
        CardObjectState? copySource = null;
        if (context.CopySource is { } source && cards.TryGetValue(source.ObjectId, out var candidate)
            && candidate.ObjectGeneration == source.Generation && IsObjectOnField(state.PlayerZones, source.ObjectId)
            && IsFaceUpNonStandbyUnit(candidate)
            && (context.BattlefieldId is null || HeldUnitsAt(state, context.BattlefieldId).Contains(source.ObjectId))
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, item.ControllerId, source.ObjectId)) copySource = candidate;
        foreach (var recipient in context.Recipients)
        {
            if (!cards.TryGetValue(recipient.ObjectId, out var image) || image.ObjectGeneration != recipient.Generation
                || !IsObjectOnField(state.PlayerZones, recipient.ObjectId)) continue;
            // This is an effect on the existing object, not a new entry: damage,
            // granted abilities, status, owner/controller and attachments survive.
            var copied = copySource is null ? null : CopyCharacteristics.CreateImage(image.ObjectId, image.ControllerId!, copySource);
            cards[image.ObjectId] = image with {
                CardNo = copied?.CardNo ?? image.CardNo,
                ManaCost = copied?.ManaCost ?? image.ManaCost,
                Power = image.Power + (copied?.Power ?? 0),
                Tags = image.Tags.Where(t => t != P6TokenFactoryCatalog.CopySourceRequiredTag)
                    .Concat(copied?.Tags ?? []).Append(CardObjectTags.Ephemeral).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            };
            events.Add(new("UNIT_COPY_RESOLVED", copySource is null ? "复制来源已离场，映像获得瞬息" : "映像复制所选单位并获得瞬息",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId,
                    ["targetObjectId"] = image.ObjectId, ["copiedCardNo"] = copied?.CardNo, ["power"] = cards[image.ObjectId].Power }));
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = events };
    }
}
