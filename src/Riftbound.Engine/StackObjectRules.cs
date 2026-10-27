namespace Riftbound.Engine;

// A captured ability and its source card are independent objects. Unknown rule
// actions are not made counterable merely because they occupy the stack.
internal static class StackObjectRules
{
    internal static bool IsAbility(StackItemState item)
        => item.DeckPlayReflexive is not null || item.DamageDestruction is not null || item.GraveyardPlay is not null
            || item.EndAbility is not null || item.StandbyObserver is not null || item.EmpowerActivation is not null || item.AzirMovement is not null || item.Equip is not null || item.Tempered is not null
            || item.AttachmentObserver is not null || item.LegendAttachment is not null
            || item.UnitEntryContext is not null || item.ReflexiveCopy is not null || item.CreatedTokenReady is not null
            || item.DeathRevealContext is not null || item.RecastContext is not null
            || item.SpellContext is not null || item.FieldContext is not null || item.InsightContext is not null
            || item.SivirRuneRecycle is not null || item.ConquestToken is not null
            || item.HandDiscardObserver is not null || item.MovementObserver is not null || item.DeathObserver is not null
            || item.DamageDistribution is not null || item.LocationDamage is not null || item.ReflexiveDuel is not null
            || item.UnitConquest is not null || item.LegendConquest is not null
            || item.BeginningRecycle is not null || item.BeginningDraw is not null
            || item.EphemeralStart is not null || item.BattlefieldStart is not null || item.HeldContext is not null
            || P4ActivatedAbilityCatalog.TryGetByEffectKind(item.EffectKind, out _)
            || item.SourceConfirmed && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var permanent)
                && (permanent.PlaysSourceToBaseAsUnit || permanent.PlaysSourceToBaseAsEquipment);

    internal static bool IsSpell(StackItemState item)
        => !IsAbility(item) && !item.SourceConfirmed
            && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && !behavior.PlaysSourceToBaseAsUnit && !behavior.PlaysSourceToBaseAsEquipment;

    internal static bool CanChoose(MatchState state, string id, bool allowAbilities)
        => state.StackItems.FirstOrDefault(item => item.StackItemId == id) is { } item
            && item.DeckPlayReflexive is not { Confirmed: false }
            && (IsSpell(item) || allowAbilities && IsAbility(item));

    // A reentered card has the same transport ID but is a different game object
    // (124). Invalid targets that merely moved within the field still count
    // under 359.3.e.9.a; an object that departed to a nonfield zone does not.
    internal static bool ReferencesCurrentObject(MatchState state, StackItemState item, string id)
        => item.TargetObjectIds.Contains(id, StringComparer.Ordinal)
            && (item.TargetGenerations is null || item.TargetGenerations.TryGetValue(id, out var generation)
                && state.CardObjects.TryGetValue(id, out var card) && card.ObjectGeneration == generation);
}
