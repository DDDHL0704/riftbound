using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial class CoreRuleEngine
{
    // A replacement adds new objects to the same event; it does not turn them
    // into the original instruction's objects or apply itself recursively.
    private static RuneCallResult ApplyRuneCall(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, string playerId, int originalCount, bool originalExhausted, int maximumCount = int.MaxValue)
    {
        if (originalCount <= 0 || !zones.TryGetValue(playerId, out var own)) return new([], []);

        var extraCount = 0;
        foreach (var id in GetControlledFieldUnitObjectIds(zones, cards, playerId))
        {
            if (!cards.TryGetValue(id, out var source)) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(zones, cards, source))
            {
                if (CardStaticAbilitySpecRules.TryGetStaticAbility(instance.CardNo,
                    ability => ability.Kind == StaticAbilityKinds.ControllerRuneCallExtraExhausted && ability.Amount > 0,
                    out var ability)) extraCount = checked(extraCount + ability.Amount);
            }
        }

        var originals = TakeControlledRuneDeckPrefix(cards, playerId, own.RuneDeck, Math.Min(originalCount, maximumCount));
        var extras = TakeControlledRuneDeckPrefix(cards, playerId,
            own.RuneDeck.Skip(originals.Length).ToArray(), Math.Min(extraCount, maximumCount - originals.Length));
        var all = originals.Concat(extras).ToArray();
        if (all.Length == 0) return new([], []);
        zones[playerId] = own with { RuneDeck = own.RuneDeck.Skip(all.Length).ToArray(), Base = own.Base.Concat(all).ToArray() };

        // A missing card state represents a ready rune. Preserve that representation
        // for ordinary CHANNEL runes while readying any existing rune state.
        foreach (var id in originals)
            if (originalExhausted || cards.ContainsKey(id)) SetExhausted(id, originalExhausted);
        foreach (var id in extras) SetExhausted(id, true);
        return new(originals, extras);

        void SetExhausted(string id, bool exhausted)
        {
            var rune = cards.TryGetValue(id, out var existing) ? existing : new CardObjectState(id);
            cards[id] = rune with { IsExhausted = exhausted };
        }
    }
}
