using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

internal sealed class ResourceNumericLimitException(string operation) : Exception(operation);

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlyDictionary<string, int>> DeckRevealMana = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards
            .Select(card => new { card.CardNo, Spec = StaticAbilityParser.Parse(card.CardEffect ?? "", [])
                .FirstOrDefault(spec => spec.Kind == StaticAbilityKinds.MainDeckRevealMana) })
            .Where(card => card.Spec is { Amount: > 0 })
            .ToDictionary(card => card.CardNo, card => card.Spec!.Amount, StringComparer.Ordinal));

    // CN370.1.b.1: the reveal itself includes the resource gain. It must be
    // available before any subsequent choice, including playing this same card.
    private static void ApplyDeckRevealResources(MatchState state,
        Dictionary<string, PlayerZones> zones, Dictionary<string, CardObjectState> cards,
        Dictionary<string, RunePool> pools, StackItemState parent, string player,
        IReadOnlyList<string> seen, int execution, bool selectionReveal,
        IReadOnlyDictionary<string, long>? simultaneousReveals, List<GameEvent> events)
    {
        var reveals = simultaneousReveals is null
            ? seen.Distinct(StringComparer.Ordinal).Select(id => (Player: player, Id: id))
            : state.Seats.Keys.OrderBy(id => id == state.TurnPlayerId ? 0 : 1).ThenBy(id => id, StringComparer.Ordinal)
                .SelectMany(owner => zones[owner].MainDeck.Where(id => simultaneousReveals.TryGetValue(id, out var generation)
                    && cards.TryGetValue(id, out var card) && card.ObjectGeneration == generation)
                    .Select(id => (Player: owner, Id: id)));
        foreach (var reveal in reveals)
        {
            if (!zones[reveal.Player].MainDeck.Contains(reveal.Id)
                || !cards.TryGetValue(reveal.Id, out var card) || card.OwnerId != reveal.Player
                || !DeckRevealMana.Value.TryGetValue(card.CardNo ?? "", out var amount)) continue;
            var hash = MatchStateHasher.HashValue(new { parent.StackItemId, execution, selectionReveal,
                PlayerId = reveal.Player, Source = new ObjectBinding(card.ObjectId, card.ObjectGeneration), amount });
            var key = "DECK-REVEAL-MANA:" + hash;
            // A simultaneous reveal is visited once per owner by observation
            // consumers. Its mandatory gains all precede optional replacements.
            if (simultaneousReveals is not null && RuleChoices.Value is { } frame
                && GetCompletedRuleChoiceResources(frame).Any(step => step.ChoiceId == key)) continue;
            var pool = pools.GetValueOrDefault(reveal.Player, RunePool.Empty);
            if (!pool.HasSupportedAmounts || (long)pool.TotalStoredMana + amount > int.MaxValue)
                throw new ResourceNumericLimitException("展示获得的法力资源");
            var before = state with { RunePools = pools.ToDictionary() };
            var capture = BeginRuleChoiceResourceStep(before, reveal.Player, key);
            pools[reveal.Player] = pool with { Mana = pool.Mana + amount };
            var gain = new GameEvent("MANA_GAINED", "展示牌堆中的卡牌，同时获得法力", new Dictionary<string, object?> {
                ["playerId"] = reveal.Player, ["sourceObjectId"] = "HIDDEN", ["sourceCardNo"] = card.CardNo,
                ["generatedMana"] = amount, ["resourceLifecycle"] = "rune-pool", ["deckRevealHash"] = hash });
            events.Add(gain);
            CompleteRuleChoiceResourceStep(capture, before with { RunePools = pools.ToDictionary() }, [gain]);
        }
    }
}
