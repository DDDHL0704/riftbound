using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private const string DeploymentPlayMoveEffect = "MOVE_FRIENDLY_TO_PLAYED_EQUIPMENT_BATTLEFIELD";
    private const string DeploymentPlayTiming = "FRIENDLY_DEPLOYMENT_PLAYED";
    private static readonly Lazy<IReadOnlyDictionary<string, UnitEventAbilitySpec>> DeploymentPlayLegends = new(() =>
        RadiantCardSources.GetAdmittedCards().Where(c => c.CardName == "德玛西亚皇子" && c.Hero == "嘉文四世"
            && c.CardEffect.StartsWith("当你打出一件拥有{{部署}}的装备时，你可以选择让我变为休眠状态并支付{{A}}，以此将一名友方单位移动到该战场。", StringComparison.Ordinal))
            .ToDictionary(c => c.CardNo, _ => new UnitEventAbilitySpec(DeploymentPlayTiming, DeploymentPlayMoveEffect,
                Optional: true, LeadingPowerCost: 1, LeadingSourceExhaust: true), StringComparer.Ordinal));

    private static bool HasPrintedDeployment(string cardNo, string? tokenFactory)
        => tokenFactory is not null
            ? P6TokenFactoryCatalog.TryGetByCardNo(tokenFactory, out var token)
                && token.Tags.Contains(CardObjectTags.EquipmentCard) && token.Tags.Contains(CardEquipmentKeywordNames.Deploy)
            : CardBehaviorRegistry.TryGetByCardNo(cardNo, out var gear) && IsDeployedEquipment(gear);

    private static UnitEventObservation[] CaptureDeploymentPlayedLegends(GameEvent ev,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        CardObjectState entered, string player)
    {
        if (entered.IsFaceDown || !entered.Tags.Contains(CardObjectTags.EquipmentCard)
            || !entered.Tags.Contains(CardEquipmentKeywordNames.Deploy)
            || !TryBattlefieldPermanentPlay(ev, out var actor, out var field) || actor != player
            || !cards.TryGetValue(field, out var battlefield) || !zones.TryGetValue(player, out var own)) return [];
        return own.LegendZone.Order(StringComparer.Ordinal).Where(id => cards.TryGetValue(id, out var source)
                && !source.IsFaceDown && source.ControllerId == player && source.CardNo is not null
                && DeploymentPlayLegends.Value.ContainsKey(source.CardNo))
            .Select(id => new UnitEventObservation(id, player, new(cards[id].CardNo!, cards[id].ObjectGeneration,
                cards[id].CopyFaceTransitions?.Count ?? 0, 0, DeploymentPlayTiming,
                Other: new(new(entered.ObjectId, entered.ObjectGeneration), entered.CardNo ?? entered.TokenFactoryCardNo!,
                    entered.CopyFaceTransitions?.Count ?? 0, entered.TokenFactoryCardNo),
                DeploymentBattlefield: new(field, battlefield.ObjectGeneration)))).ToArray();
    }

    private static bool ValidDeploymentPlayBattlefield(MatchState state, UnitEventContext context)
        => context.DeploymentBattlefield is { Generation: >= 0 } field
            && context.Other is { } gear && field.ObjectId != gear.Binding.ObjectId
            && HasPrintedDeployment(gear.CardNo, gear.TokenFactoryCardNo)
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId) is { } current
            && current.ObjectGeneration >= field.Generation;

    private static string? DeploymentPlayDestination(MatchState state, UnitEventContext context)
        => context.DeploymentBattlefield is { } field
            && BattlefieldLocalRules.Battlefield(state, field.ObjectId)?.ObjectGeneration == field.Generation
                ? "BATTLEFIELD:" + field.ObjectId : null;
}
