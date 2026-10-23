using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DravenBattleDeathScoreContext(ObjectBinding Source, string CardNo, string ControllerId,
    string BattlefieldId, long DeathTick, string DeathId, string Role);

public sealed partial class CoreRuleEngine
{
    internal const string DravenBattleDeathScoreEffect = "CAPTURED_DRAVEN_BATTLE_DEATH_OPPONENT_SCORE";

    // Capture before leaving play, after replacement applicability is known.
    // Admit this only from a committed WasDestroyed/WasUnit death. A recall
    // replacement must not publish the prospective observation (CN 808.1.d.1).
    // Pass the actual mutable location frame, including any movement performed
    // earlier in the same parent; do not reconstruct locality from zone lists.
    private static DravenBattleDeathScoreContext? CaptureDravenBattleDeathScore(MatchState state,
        IReadOnlyDictionary<string, PlayerZones> zones, IReadOnlyDictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, ObjectLocationState> locations, CardObjectState unit, string deathId, long deathTick)
    {
        if (unit.CardNo is not ("SFD·148/221" or "SFD·148a/221") || !IsFaceUpNonStandbyUnit(unit)
            || !IsObjectOnField(zones, unit.ObjectId) || !IsInitialBattleDuel(state, out var battle)
            || !battle.InitialRolesEstablished
            || !locations.TryGetValue(unit.ObjectId, out var at) || at.Zone != "BATTLEFIELD"
            || at.BattlefieldObjectId != battle.BattlefieldId
            || !cards.TryGetValue(unit.ObjectId, out var current) || current.ObjectGeneration != unit.ObjectGeneration)
            return null;
        var controller = EffectiveFieldControllerId(zones, unit.ObjectId, unit);
        if (!state.Seats.ContainsKey(controller)) return null;
        return new(new(unit.ObjectId, unit.ObjectGeneration), unit.CardNo!, controller, battle.BattlefieldId,
            deathTick, deathId, controller == battle.InitialAttackingPlayerId ? "ATTACKER" : "DEFENDER");
    }

    // Shared recovery must authenticate this context against the original death,
    // battle declaration and accepted action, rather than shape checks alone.
    internal static bool HasValidDravenBattleDeathScoreShape(DravenBattleDeathScoreContext? context)
        => context is not null && context.Source is not null && context.Source.Generation >= 0
            && !string.IsNullOrWhiteSpace(context.Source.ObjectId)
            && context.CardNo is "SFD·148/221" or "SFD·148a/221"
            && !string.IsNullOrWhiteSpace(context.ControllerId) && !string.IsNullOrWhiteSpace(context.BattlefieldId)
            && context.DeathTick > 0 && !string.IsNullOrWhiteSpace(context.DeathId)
            && context.Role is "ATTACKER" or "DEFENDER";

    private static TriggerQueueItemState BuildDravenBattleDeathScoreTrigger(DravenBattleDeathScoreContext context)
        => new($"draven-death-score-{context.DeathId}-{context.Source.ObjectId}", context.ControllerId,
            context.Source.ObjectId, DravenBattleDeathScoreEffect, "UNIT_DESTROYED")
        { SourceCardNo = context.CardNo, EnqueuedAtTick = context.DeathTick };

    // Opponent selection belongs to ordinary skill confirmation; do not award
    // the point directly inside damage, destruction, or the death observer.
    private static StackResolutionResult ResolveDravenBattleDeathScore(MatchState state, StackItemState item,
        DravenBattleDeathScoreContext context, string chosenOpponent)
    {
        if (!HasValidDravenBattleDeathScoreShape(context) || context.DeathTick > state.Tick
            || item.EffectKind != DravenBattleDeathScoreEffect || item.SourceObjectId != context.Source.ObjectId
            || item.ControllerId != context.ControllerId || item.CardNo != context.CardNo
            || chosenOpponent == context.ControllerId || !SeatPlayerIds(state).Contains(chosenOpponent, StringComparer.Ordinal))
            throw new InvalidOperationException("Invalid captured Draven battle-death score.");
        var scores = NormalizeScoresForSeats(state);
        scores[chosenOpponent] += 1;
        var events = new List<GameEvent>
        {
            new("UNIT_TRIGGER_RESOLVED", "德莱文在战斗中被摧毁，让选定对手获得一分", new Dictionary<string, object?>
            {
                ["playerId"] = context.ControllerId, ["sourceObjectId"] = context.Source.ObjectId,
                ["sourceCardNo"] = context.CardNo, ["battlefieldId"] = context.BattlefieldId,
                ["opponentId"] = chosenOpponent, ["trigger"] = DravenBattleDeathScoreEffect
            }),
            new("SCORE_GAINED", "德莱文的战斗死亡技能让对手获得一分", new Dictionary<string, object?>
            {
                ["playerId"] = chosenOpponent, ["sourceObjectId"] = context.Source.ObjectId,
                ["amount"] = 1, ["score"] = scores[chosenOpponent], ["reason"] = DravenBattleDeathScoreEffect
            })
        };
        // Shared parent completion performs normal victory before battle control.
        return NoopStackResolutionResult(state) with { PlayerScores = scores, Events = events };
    }
}
