using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    private static DrawApplicationResult ResolveOtherPlayersDrawOrCallRune(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        IReadOnlyDictionary<string, int> scores, StackItemState item, CardBehaviorDefinition behavior,
        long rng, List<GameEvent> events)
    {
        for (var repetition = 0; repetition < item.EffectRepeatCount; repetition++) {
            var decisions = PlayerIdsStartingAfter(state, item.ControllerId)
                .Where(player => player != item.ControllerId).Select(player => {
                    var choice = RuleChoices.Value!.Choose(player,
                        "选择卡牌：你与施法者各抽一张牌；或选择符文：你与施法者各召出一枚休眠符文。",
                        [new("CARDS", "卡牌"), new("RUNES", "符文")], kind: "OTHER_PLAYER_DRAW_OR_RUNE",
                        preview: state with { PlayerZones = zones, CardObjects = cards, PlayerScores = scores, RngCursor = rng });
                    return (Player: player, Choice: choice);
                }).ToArray();
            // The first instruction gathers every other player's choice. Apply
            // the printed draw instruction before the printed rune instruction.
            foreach (var decision in decisions)
                events.Add(new("OTHER_PLAYER_EFFECT_CHOICE", "其他玩家已选择卡牌或符文", new Dictionary<string, object?> {
                    ["playerId"] = decision.Player, ["sourceObjectId"] = item.SourceObjectId,
                    ["selection"] = decision.Choice }));
            foreach (var decision in decisions.Where(decision => decision.Choice == "CARDS"))
                foreach (var player in new[] { item.ControllerId, decision.Player }) {
                    var draw = ApplyDrawToPlayer(state with { PlayerZones = zones, CardObjects = cards,
                            PlayerScores = scores, RngCursor = rng },
                        zones, cards, scores, player, behavior.DrawCount, rng, events, deferNormalVictory: true);
                    scores = draw.PlayerScores; rng = draw.RngCursor;
                    if (draw.WinnerPlayerId is not null) return draw;
                }
            foreach (var decision in decisions.Where(decision => decision.Choice == "RUNES"))
                foreach (var player in new[] { item.ControllerId, decision.Player }) {
                    var called = CallRunes(zones, cards, player, behavior.RuneCallCount);
                    events.Add(new("RUNES_CALLED", "召出休眠符文", new Dictionary<string, object?> {
                        ["playerId"] = player, ["sourceObjectId"] = item.SourceObjectId,
                        ["count"] = called.CalledRuneObjectIds.Count,
                        ["runeObjectIds"] = called.CalledRuneObjectIds.ToArray() }));
                }
        }
        return new(scores, null, rng);
    }
}
