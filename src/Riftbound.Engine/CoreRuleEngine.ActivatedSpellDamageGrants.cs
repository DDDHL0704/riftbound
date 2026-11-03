using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // CN 392/393: a resolved delayed passive survives its source. Confirmation
    // fixes the next spell; each of that spell's damage instructions benefits,
    // including Echo. A non-damage spell still consumes the opportunity.
    private static StackResolutionResult ResolveActivatedNextSpellDamage(MatchState state, StackItemState item)
    {
        var grant = new ActivatedPlayGrant(state.TurnNumber, item.ControllerId, item.CardNo!, item.StackItemId,
            item.BorrowedExhaust, item.SimpleActivation) { Kind = "NEXT_SPELL_DAMAGE", GrantedTick = state.Tick };
        if (!ValidActivatedPlayGrant(state, grant)) throw new InvalidOperationException("Invalid activated spell damage grant source.");
        return NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, ActivatedPlayGrantMarker(grant)),
            Events = [new("NEXT_SPELL_DAMAGE_GRANTED", "本回合下一个法术每段伤害+1",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) })] };
    }

    private static bool ValidActivatedSpellDamageBinding(MatchState state, ActivatedPlayGrant grant)
    {
        var acquired = grant.Borrowed?.AcquiredTick ?? grant.Printed?.AcquiredTick ?? -1;
        if (state.CardPlays is null || grant.GrantedTick < acquired || grant.GrantedTick > state.Tick) return false;
        var first = state.CardPlays.TurnNumber == state.TurnNumber ? state.CardPlays.Finalized
            .FirstOrDefault(e => e.PlayerId == grant.ControllerId && (e.Types & 4) != 0 && e.Tick > grant.GrantedTick) : null;
        return grant.SpellPlayId is null ? first is null : first?.PlayId == grant.SpellPlayId;
    }

    private static IReadOnlyList<string> BindActivatedNextSpellDamage(IReadOnlyList<string> effects, CardPlayHistoryEntry entry)
        => (entry.Types & 4) == 0 ? effects : effects.Select(marker =>
            ReadActivatedPlayGrant(marker) is { Kind: "NEXT_SPELL_DAMAGE", SpellPlayId: null } grant
                && grant.ControllerId == entry.PlayerId && entry.Tick > grant.GrantedTick
                ? ActivatedPlayGrantMarker(grant with { SpellPlayId = entry.PlayId }) : marker).ToArray();

    private static int PendingActivatedSpellDamageBonus(MatchState state, string player)
        => state.UntilEndOfTurnEffects.Count(marker => ReadActivatedPlayGrant(marker) is
            { Kind: "NEXT_SPELL_DAMAGE", SpellPlayId: null } grant && grant.ControllerId == player);

    private static int ActivatedSpellDamageBonus(MatchState state, StackItemState? item)
        => item is null || StackObjectRules.IsAbility(item) ? 0 : state.UntilEndOfTurnEffects.Count(marker =>
            ReadActivatedPlayGrant(marker) is { Kind: "NEXT_SPELL_DAMAGE", SpellPlayId: not null } grant
                && grant.SpellPlayId == item.StackItemId);
}
