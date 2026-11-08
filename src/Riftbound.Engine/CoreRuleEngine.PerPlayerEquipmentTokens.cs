using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ControllerTokensPerAcceptingOpponent { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string EachPlayerEquipmentTokenEffect = "EACH_PLAYER_OPTIONAL_EQUIPMENT_TOKEN";

    private static StackResolutionResult ResolveEachPlayerEquipmentTokens(MatchState state, StackItemState item,
        UnitEventAbilitySpec spec)
    {
        if (!P6TokenFactoryCatalog.TryGetByCardNo(spec.CreatedTokenFactoryCardNo!, out var factory)
            || !factory.Tags.Contains(CardObjectTags.EquipmentCard))
            throw new InvalidOperationException("Invalid per-player equipment token factory.");
        var zones = NormalizeZonesForSeats(state);
        var cards = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value);
        var events = new List<GameEvent>();
        var acceptingOpponents = 0;
        // CN303.2.a /355.18: these independent optional actions belong to
        // resolution, in turn order from the current turn player. Each player
        // makes their own decision; subsequent players see earlier actions.
        var players = new[] { state.TurnPlayerId }.Concat(PlayerIdsStartingAfter(state, state.TurnPlayerId)
            .Where(player => player != state.TurnPlayerId));
        foreach (var player in players)
        {
            var selected = RuleChoices.Value!.Choose(player, "选择是否打出休眠的装备指示物。",
                [new("PLAY", "打出指示物"), new("DECLINE", "放弃")], kind: EachPlayerEquipmentTokenEffect,
                preview: state with { PlayerZones = zones, CardObjects = cards,
                    ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) });
            events.Add(new("PLAYER_EQUIPMENT_TOKEN_CHOICE", "玩家已选择是否打出装备指示物",
                new Dictionary<string, object?> { ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId,
                    ["selection"] = selected }));
            if (selected != "PLAY") continue;
            if (player != item.ControllerId) acceptingOpponents++;
            Create(player, spec.Amount);
        }
        // The second instruction counts opponents who accepted, independent
        // of whether the controller accepted their own first instruction.
        Create(item.ControllerId, acceptingOpponents * spec.ControllerTokensPerAcceptingOpponent);
        return NoopStackResolutionResult(state) with { PlayerZones = zones, CardObjects = cards, Events = events,
            ObjectLocations = ReconcileObjectLocations(state.ObjectLocations, zones) };

        void Create(string player, int amount)
        {
            for (var i = 0; i < amount; i++)
                CreateLegendEquipmentToken(zones, cards, player, item.SourceObjectId, item.EffectKind,
                    factory.TokenFamilyName, factory.Tags, true, events, item.UnitEvent!.CardNo);
        }
    }
}
