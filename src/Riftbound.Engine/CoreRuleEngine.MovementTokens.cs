using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record MovementTokenInstruction(string FactoryCardNo, int Count, ObjectLocationState Location);

    // CN 359.3.f.2 checks "here" when instructions execute; 359.3.f.3 explicitly
    // records Lillia's origin when movement triggers, even if she later leaves.
    private static bool TryMovementTokenInstruction(MatchState state, StackItemState item, out MovementTokenInstruction instruction)
    {
        instruction = null!;
        if (item.MovementObserver is not { Kind: TriggerKinds.UnitMovedCreateUnitToken } context
            || !ValidMovementObserver(context, item.EffectKind, item.ControllerId, item.SourceObjectId, item.CardNo)
            || !UnitMovedTriggerSpecRules.TryGetTrigger(context.CardNo, UnitMovedTriggerSpecRules.IsMovementUnitTokenTrigger, out var spec)) return false;
        ObjectLocationState? location;
        if (spec.CreatedTokenDestination == TriggerTokenDestinations.MovementOrigin)
            location = context.Unit.Origin;
        else
        {
            if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source)
                || source.ObjectGeneration != context.SourceGeneration || !IsFaceUpNonStandbyUnit(source)
                || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return false;
            location = state.ObjectLocations.GetValueOrDefault(source.ObjectId);
        }
        if (location is null || location.Zone is not ("BASE" or "BATTLEFIELD")) return false;
        if (location.Zone == "BASE")
        {
            if (location.PlayerId != item.ControllerId || location.BattlefieldObjectId is not null) return false;
        }
        else if (BattlefieldLocalRules.Battlefield(state, location.BattlefieldObjectId) is null
            || BattlefieldLocalRules.PreventsUnitPlay(state, "BATTLEFIELD:" + location.BattlefieldObjectId)) return false;
        instruction = new(spec.CreatedTokenName == "精灵" ? P6TokenFactoryCatalog.FaerieTokenCardNo : "OGN·271/298",
            spec.CreatedTokenCount!.Value, location);
        return true;
    }
}
