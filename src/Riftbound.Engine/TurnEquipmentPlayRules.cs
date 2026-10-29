using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnEquipmentPlayGrant(string Id, int TurnNumber, string ControllerId,
    CardPlayHistoryEntry SourcePlay, ObjectBinding DestroyedEquipment, int MaximumManaCost, long CreatedTick);

internal static class TurnEquipmentPlayRules
{
    internal const string ChoicePrefix = "EQUIPMENT_PLAY_PERMISSION:";

    internal static string GrantId(string playId, long tick)
        => "equipment-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(playId + ":" + tick)))[..24];

    internal static bool Valid(MatchState state, TurnEquipmentPlayGrant? grant)
        => grant is { SourcePlay: { Source: { Generation: >= 0 } } play, DestroyedEquipment: { Generation: >= 0 } gear }
            && grant.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(grant.ControllerId)
            && grant.ControllerId == play.PlayerId && grant.CreatedTick > play.Tick && grant.CreatedTick <= state.Tick
            && grant.Id == GrantId(play.PlayId, grant.CreatedTick)
            && state.CardPlays.TurnNumber == state.TurnNumber && state.CardPlays.Resolved.Contains(play)
            && CardBehaviorRegistry.TryGetByCardNo(play.CardNo, out var source)
            && source.DestroyedEquipmentGrantsTurnPlayMaximumMana > 0
            && source.DestroyedEquipmentGrantsTurnPlayMaximumMana == grant.MaximumManaCost
            && !string.IsNullOrWhiteSpace(gear.ObjectId)
            && (!state.CardObjects.TryGetValue(gear.ObjectId, out var current) || current.ObjectGeneration > gear.Generation);

    internal static bool ValidAll(MatchState state)
        => state.EquipmentPlayGrants is not { } grants || grants.Count > 0
            && grants.All(g => Valid(state, g))
            && grants.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() == grants.Count;

    internal static IReadOnlyList<TurnEquipmentPlayGrant> Available(MatchState state, string player,
        CardBehaviorDefinition behavior, string? sourceId)
        => state.PendingEffectPlay is not null || !behavior.PlaysSourceToBaseAsEquipment || sourceId is null
            || !state.PlayerZones.TryGetValue(player, out var zones) || !zones.Hand.Contains(sourceId)
            || !state.CardObjects.TryGetValue(sourceId, out var card) || card.CardNo != behavior.CardNo
            ? [] : (state.EquipmentPlayGrants ?? []).Where(g => g.ControllerId == player
                && g.MaximumManaCost >= behavior.ManaCost && Valid(state, g)).ToArray();

    internal static IReadOnlyList<ActionPromptChoiceDto> Choices(MatchState state, string player,
        CardBehaviorDefinition behavior, string? sourceId)
        => Available(state, player, behavior, sourceId).Select(g => new ActionPromptChoiceDto(ChoicePrefix + g.Id,
            "使用本回合的装备打出权限", $"无视此装备的基础法力费用；仍需支付符能和额外费用。每份权限可使用一次，限法力费用不高于 {g.MaximumManaCost} 的手牌装备。" )).ToArray();

    internal static bool TryApply(MatchState state, string player, string sourceId,
        IReadOnlyList<string>? choices, CardBehaviorDefinition printed, out CardBehaviorDefinition behavior)
    {
        behavior = printed;
        var selected = (choices ?? []).Where(c => c.StartsWith(ChoicePrefix, StringComparison.Ordinal)).ToArray();
        if (selected.Length == 0) return true;
        if (selected.Length != 1 || Available(state, player, printed, sourceId)
            .FirstOrDefault(g => ChoicePrefix + g.Id == selected[0]) is not { } grant) return false;
        behavior = printed with { AppliedEquipmentPlayGrant = grant.Id };
        return true;
    }

    internal static IReadOnlyList<TurnEquipmentPlayGrant>? Consume(MatchState state, CardBehaviorDefinition behavior)
    {
        if (behavior.AppliedEquipmentPlayGrant is not { } id) return state.EquipmentPlayGrants;
        var remaining = state.EquipmentPlayGrants!.Where(g => g.Id != id).ToArray();
        return remaining.Length == 0 ? null : remaining;
    }
}

public sealed partial class CoreRuleEngine
{
    private static IReadOnlyList<TurnEquipmentPlayGrant>? GrantEquipmentPlayAfterDestruction(MatchState state,
        StackItemState item, CardBehaviorDefinition behavior, IReadOnlyList<string> destroyedIds, List<GameEvent> events)
    {
        if (behavior.DestroyedEquipmentGrantsTurnPlayMaximumMana <= 0
            || destroyedIds.FirstOrDefault(id => state.CardObjects.TryGetValue(id, out var card)
                && card.Tags.Contains(CardObjectTags.EquipmentCard)) is not { } destroyed
            || state.CardPlays.Resolved.FirstOrDefault(p => p.PlayId == item.StackItemId && p.CardNo == item.CardNo
                && p.PlayerId == item.ControllerId && p.Source.ObjectId == item.SourceObjectId) is not { } play)
            return null;
        var tick = state.Tick + 1;
        var grant = new TurnEquipmentPlayGrant(TurnEquipmentPlayRules.GrantId(item.StackItemId, tick), state.TurnNumber,
            item.ControllerId, play, new(destroyed, state.CardObjects[destroyed].ObjectGeneration),
            behavior.DestroyedEquipmentGrantsTurnPlayMaximumMana, tick);
        events.Add(new("TURN_EQUIPMENT_PLAY_GRANTED", "实际摧毁装备后获得本回合的装备打出权限", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["permissionId"] = grant.Id, ["maximumManaCost"] = grant.MaximumManaCost }));
        return (state.EquipmentPlayGrants ?? []).Append(grant).ToArray();
    }
}
