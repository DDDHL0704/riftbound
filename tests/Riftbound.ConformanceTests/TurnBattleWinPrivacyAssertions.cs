using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class TurnBattleWinPrivacyAssertions
{
    private static readonly Lazy<string> Printed = new(() => OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult()
        .Cards.Single(card => card.CardNo == "UNL-095/219").CardEffect);

    internal static void CheckHistoricalModifiers(JsonNode snapshot, MatchState state)
    {
        if (snapshot["timing"]?["continuousEffects"] is not JsonArray effects) return;
        const string prefix = "TURN_BATTLE_WIN_REWARD:";
        var receipts = state.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(prefix, StringComparison.Ordinal))
            .Select(effect => JsonSerializer.Deserialize<TurnBattleWinReward>(Convert.FromBase64String(effect[prefix.Length..]))!).ToArray();
        if (receipts.Length == 0) return;
        foreach (var row in effects.OfType<JsonObject>())
        {
            var source = row["sourceObjectId"]?.GetValue<string>();
            if (source is null || !state.PlayerZones.Values.Any(zones => zones.Hand.Concat(zones.MainDeck).Concat(zones.RuneDeck).Contains(source))) continue;
            var targetId = row["targetObjectId"]?.GetValue<string>();
            if (targetId is null || !state.CardObjects.TryGetValue(targetId, out var target)) continue;
            var identity = row["effectId"]?.GetValue<string>();
            var modifier = target.UntilEndOfTurnPowerModifiers.SingleOrDefault(entry => entry.EffectId == identity);
            if (modifier is null) continue;
            var matching = receipts.Where(receipt => receipt.Source.ObjectId == source && receipt.Unit == new ObjectBinding(targetId, target.ObjectGeneration)
                && identity == $"POWER:{targetId}:{receipt.CreatingStackItemId}:{receipt.CreatingEffectKind}:3:{modifier.AppliedOrder}").ToArray();
            if (matching.Length == 0) continue;
            var witness = Assert.Single(matching);
            Assert.Equal("UNL-095/219", witness.CardNo);
            Assert.Contains("让一名友方单位本回合内{{S}}+3。当该单位在本回合赢得一场战斗时，获得2经验。", Printed.Value);
            Assert.Equal(2, witness.ExperienceCount); Assert.InRange(witness.CreatedAtTick, 1, state.Tick);
            Assert.InRange(witness.Source.Generation, 0, state.CardObjects[source].ObjectGeneration - 1);
            Assert.Contains(state.PlayerZones.Values, zones => zones.Base.Concat(zones.Battlefields).Contains(targetId));
            Assert.False(target.IsFaceDown); Assert.Equal(source, modifier.SourceObjectId); Assert.Equal(witness.CardNo, modifier.SourceCardNo);
            Assert.Equal(witness.CreatingEffectKind, modifier.EffectKind); Assert.Equal(3, modifier.PowerDelta);
            Assert.Equal(3, modifier.RequestedPowerDelta); Assert.Equal(0, modifier.MinimumPower); Assert.Null(modifier.SnapshotMight);
            Assert.Equal("CoreRuleEngine.ApplyPowerModifier", modifier.SourcePath); Assert.Equal("UNTIL_END_OF_TURN", modifier.Duration);
            Assert.Equal(modifier.SourceCardNo, row["sourceCardNo"]!.GetValue<string>());
            Assert.Equal(modifier.EffectKind, row["effectKind"]!.GetValue<string>()); Assert.Equal(3, row["powerDelta"]!.GetValue<int>());
            Assert.Equal(modifier.BasePower, row["basePower"]!.GetValue<int>());
            Assert.Equal(modifier.EffectivePower, row["effectivePower"]!.GetValue<int>());
            Assert.Equal(modifier.AppliedOrder, row["appliedOrder"]!.GetValue<int>());
            // Only this original public +3 provenance is exempted. Recovery also
            // binds the receipt to its accepted spell; every other reference to
            // the current private card remains subject to the ordinary guard.
            row["sourceObjectId"] = "CHECKED_PUBLIC_DELAYED_SPELL_MODIFIER";
        }
    }
}
