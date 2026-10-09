using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private sealed record LegendUnitToken(string AbilityId, string FactoryCardNo, bool EntersReady);

    private static bool TryGetLegendUnitToken(string effect, out LegendUnitToken token)
    {
        token = effect switch {
            LegendAbilityEffectKinds.CreateMinion => new(ViktorLegendAbilityId, "OGN·271/298", false),
            LegendAbilityEffectKinds.CreateSandSoldier => new(LegendActionAbilityCatalog.AzirLegendAbilityId, P6TokenFactoryCatalog.SandSoldierTokenCardNo, false),
            LegendAbilityEffectKinds.CreateFaerie => new(LilliaLegendAbilityId, P6TokenFactoryCatalog.FaerieTokenCardNo, true),
            _ => null!
        };
        return token is not null;
    }

    internal static string? LegendUnitTokenLabel(string effect) => TryGetLegendUnitToken(effect, out var token)
        && TryGetLegendAbility(token.AbilityId, out var ability) ? ability.DisplayName : null;

    internal static bool ValidLegendUnitToken(StackItemState item) => !TryGetLegendUnitToken(item.EffectKind, out var token)
        || LegendActionAbilityCatalog.IsSourceCardNoForAbility(token.AbilityId, item.CardNo)
            && item.TargetObjectIds.Count == 0 && item.EffectRepeatCount == 1 && item.RepeatExecutions is null
            && !item.SourceConfirmed && item.HeldContext is null && item.ReflexiveCopy is null;

    // Costs have already been committed by the shared activation path. Summoning
    // is not a resource ability: both players can respond before any token entry.
    private static ResolutionResult QueueLegendUnitToken(MatchState state, string player, string sourceId,
        string cardNo, LegendAbilityDefinition ability, List<GameEvent> events)
    {
        var item = new StackItemState($"STACK-{state.Tick + 1}-{sourceId}-TOKEN", player, sourceId,
            ability.EffectKind, cardNo);
        var next = state with { Tick = state.Tick + 1, ActivePlayerId = player, TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = player, PassedPriorityPlayerIds = [], StackItems = state.StackItems.Append(item).ToArray() };
        events.Add(new("STACK_ITEM_ADDED", $"{ability.DisplayName}加入结算链", new Dictionary<string, object?> {
            ["stackItemId"] = item.StackItemId, ["controllerId"] = player, ["sourceObjectId"] = sourceId,
            ["cardNo"] = cardNo, ["effectKind"] = ability.EffectKind, ["abilityId"] = ability.AbilityId,
            ["targetObjectIds"] = Array.Empty<string>() }));
        return new(true, null, next, events, ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    private static StackResolutionResult ResolveLegendUnitToken(MatchState state, StackItemState item, LegendUnitToken instruction)
        => CreateUnitTokenBatch(state, item, instruction.FactoryCardNo, 1 + AdditionalUnitTokens(item), instruction.EntersReady,
            abilityId: instruction.AbilityId);

}
