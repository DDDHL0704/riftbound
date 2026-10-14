using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DestroyDeckPlayContext(ObjectBinding Source, string CardNo, string AbilityId, ObjectBinding Target, LastKnownMightFrame DeclaredFrame,
    bool Prepared = false, int? KilledMight = null, LastKnownMightFrame? KilledFrame = null);

public sealed partial class CoreRuleEngine
{
    private static bool DestroyDeckPlayDefinition(StackItemState item, out P4ActivatedAbilityDefinition ability)
        => P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out ability!)
            && ability.Kind == ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest
            && item.DestroyDeckPlay is { } context && context.CardNo == item.CardNo
            && ability.SourceCardNo == context.CardNo && ability.AbilityId == context.AbilityId;

    private static StackResolutionResult ResolveDestroyDeckPlay(MatchState state, StackItemState item)
    {
        if (item.EffectPlayCompleted) return NoopStackResolutionResult(state);
        if (!DestroyDeckPlayDefinition(item, out var ability)) return NoopStackResolutionResult(state);
        var context = item.DestroyDeckPlay!;
        // Target masking may remove a departed live target during resumption;
        // the historical reference still identifies the original instruction.
        item = item with { TargetObjectIds = [context.Target.ObjectId],
            TargetGenerations = new Dictionary<string,long> { [context.Target.ObjectId] = context.Target.Generation } };
        var events = new List<GameEvent>();
        var owners = new List<string>();
        if (!context.Prepared)
        {
            var zones = NormalizeZonesForSeats(state);
            var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            var pools = state.RunePools.ToDictionary(p => p.Key, p => p.Value);
            LastKnownMightFrame? frame = null;
            int? killedMight = null;
            if (cards.TryGetValue(context.Target.ObjectId, out var target) && target.ObjectGeneration == context.Target.Generation
                && IsLegalDestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRestTarget(zones, cards, item.ControllerId, item.SourceObjectId, target.ObjectId))
            {
                var before = CapturePublicMightFrame(state, zones, cards);
                var might = ResolveCurrentFieldUnitPower(MightFrameState(before), target);
                if (TryDestroyTarget(state, zones, cards, pools, target.ObjectId, out var removal))
                {
                    events.AddRange(BuildFieldRemovalEvents(ability.DisplayName, item, target.ObjectId, removal));
                    if (removal.WasDestroyed && removal.WasUnit)
                    {
                        frame = before; killedMight = might; owners.Add(removal.OwnerPlayerId);
                    }
                }
            }
            context = context with { Prepared = true, KilledFrame = frame, KilledMight = killedMight };
            item = item with { DestroyDeckPlay = context };
            state = state with { PlayerZones = zones, CardObjects = cards, RunePools = pools,
                ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones),
                StackItems = state.StackItems.Select(s => s.StackItemId == item.StackItemId ? item : s).ToArray() };
        }
        TryGetEffectPlayDefinition(item, out var definition);
        var result = BeginDeckPlaySequence(state, item, definition);
        return result with { Events = events.Concat(result.Events).ToArray(),
            DestroyedUnitOwnerIds = owners.Concat(result.DestroyedUnitOwnerIds).Distinct().ToArray() };
    }

    internal static bool ValidDestroyDeckPlayContext(MatchState state, StackItemState item)
    {
        if (item.DestroyDeckPlay is not { } context)
            return !P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out var plain)
                || plain.Kind != ActivatedAbilityKinds.DestroyFriendlyUnitLookTopPlayPowerPlusOneRecycleRest;
        try
        {
            if (!DestroyDeckPlayDefinition(item, out _) || context.Source is not { Generation: >= 0 }
                || item.EffectRepeatCount != 1 || item.OptionalCosts.Count != 0 || item.DamageAmount != 0 || item.Destination.Length != 0
                || context.Source.ObjectId != item.SourceObjectId || context.Target is not { Generation: >= 0 }
                || !state.Seats.ContainsKey(item.ControllerId) || item.TargetObjectIds.Count != 1
                || item.TargetObjectIds[0] != context.Target.ObjectId
                || item.TargetGenerations is null || item.TargetGenerations.Count != 1
                || item.TargetGenerations.GetValueOrDefault(context.Target.ObjectId, -1) != context.Target.Generation
                || !state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration < context.Source.Generation
                || !ValidDestroyDeckMightFrame(state, context.DeclaredFrame)
                || !context.DeclaredFrame.CardObjects.TryGetValue(context.Source.ObjectId, out var declaredSource)
                || declaredSource.ObjectGeneration != context.Source.Generation || declaredSource.CardNo != context.CardNo
                || !declaredSource.Tags.Contains(CardObjectTags.EquipmentCard)
                || !context.DeclaredFrame.PlayerZones[item.ControllerId].Base.Contains(declaredSource.ObjectId)
                || EffectiveFieldControllerId(context.DeclaredFrame.PlayerZones, declaredSource.ObjectId, declaredSource) != item.ControllerId
                || !context.DeclaredFrame.CardObjects.TryGetValue(context.Target.ObjectId, out var declaredTarget)
                || declaredTarget.ObjectGeneration != context.Target.Generation || !IsFaceUpNonStandbyUnit(declaredTarget)
                || !IsObjectOnField(context.DeclaredFrame.PlayerZones, declaredTarget.ObjectId)
                || EffectiveFieldControllerId(context.DeclaredFrame.PlayerZones, declaredTarget.ObjectId, declaredTarget) != item.ControllerId
                || !context.Prepared && (context.KilledMight is not null || context.KilledFrame is not null)
                || (context.KilledMight is null) != (context.KilledFrame is null)) return false;
            if (context.KilledFrame is not { } frame) return true;
            if (!context.Prepared || context.KilledMight < 0 || !ValidDestroyDeckMightFrame(state, frame)
                || frame.CardObjects is null || frame.ObjectLocations is null || frame.BattleDeclarations is null
                || !frame.CardObjects.TryGetValue(context.Target.ObjectId, out var target)
                || target.ObjectGeneration != context.Target.Generation || !IsFaceUpNonStandbyUnit(target)
                || !IsObjectOnField(frame.PlayerZones, target.ObjectId)
                || EffectiveFieldControllerId(frame.PlayerZones, target.ObjectId, target) != item.ControllerId
                || ResolveCurrentFieldUnitPower(MightFrameState(frame), target) != context.KilledMight
                || frame.PlayerZones.Any(p => !state.Seats.ContainsKey(p.Key) || p.Value is null
                    || p.Value.Hand.Count != 0 || p.Value.MainDeck.Count != 0 || p.Value.RuneDeck.Count != 0)
                || frame.CardObjects.Any(p => p.Value is null || p.Key != p.Value.ObjectId || p.Value.IsFaceDown
                    || (state.CardObjects.TryGetValue(p.Key, out var current) ? current.ObjectGeneration < p.Value.ObjectGeneration : !IsTokenObject(p.Value)))) return false;
            return state.CardObjects.TryGetValue(target.ObjectId, out var departed)
                ? departed.ObjectGeneration > context.Target.Generation : IsTokenObject(target);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException
            or NullReferenceException or OverflowException) { return false; }
    }

    private static bool ValidDestroyDeckMightFrame(MatchState state, LastKnownMightFrame frame)
    {
        if (!ValidMightFrameExperience(frame) || frame.CardObjects is null || frame.ObjectLocations is null
            || frame.BattleDeclarations is null || frame.PlayerZones.Count != state.Seats.Count
            || frame.PlayerZones.Any(p => !state.Seats.ContainsKey(p.Key) || p.Value is null
                || p.Value.Hand.Count != 0 || p.Value.MainDeck.Count != 0 || p.Value.RuneDeck.Count != 0)
            || frame.BattleDeclarations.Any(s => s is null || !s.StartsWith("BATTLE_RESPONSE_DECLARATION_CONTEXT:"))) return false;
        var publicIds = frame.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Graveyard)
            .Concat(z.Banished).Concat(z.LegendZone).Concat(z.ChampionZone)).ToHashSet();
        return publicIds.SetEquals(frame.CardObjects.Keys) && publicIds.SetEquals(frame.ObjectLocations.Keys)
            && frame.CardObjects.All(p => p.Value is { IsFaceDown: false } card && card.ObjectId == p.Key
                && (!IsTokenObject(card) || P6TokenFactoryCatalog.TryGetByCardNo(card.TokenFactoryCardNo ?? card.CardNo ?? "", out _))
                && (state.CardObjects.TryGetValue(p.Key, out var current) ? current.ObjectGeneration >= card.ObjectGeneration : IsTokenObject(card)));
    }

    internal static bool ValidHistoricalDestroyDeckTarget(MatchState state, StackItemState item, string id)
        => item.DestroyDeckPlay is { } context && context.Target.ObjectId == id && !state.CardObjects.ContainsKey(id)
            && ValidDestroyDeckPlayContext(state, item) && context.DeclaredFrame.CardObjects.TryGetValue(id, out var target)
            && IsTokenObject(target);
}
