namespace Riftbound.Engine;

public sealed record MovementScoreSpec(int Ordinal, int ScoreAmount);
internal static class UnitMovementRules
{
    internal static bool CannotTakeDamage(CardObjectState card)
        => !card.IsFaceDown && card.Tags.Contains(CardObjectTags.UnitCard)
            && CardBehaviorRegistry.TryGetByCardNo(card.CardNo??"",out var behavior) && behavior.DamageImmunityAfterMoves>0
            && card.MovementCountThisTurn>=behavior.DamageImmunityAfterMoves;
}
