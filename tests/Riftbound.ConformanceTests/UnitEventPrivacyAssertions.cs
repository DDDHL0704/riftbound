using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class UnitEventPrivacyAssertions
{
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));

    // Callers supply confirmed skills captured from the original accepted state.
    // Only that proven public incarnation is exempted, never its current hand object.
    internal static void CheckHistoricalModifiers(JsonNode snapshot, MatchState state, IReadOnlyList<StackItemState> captured)
    {
        if (captured.Count == 0 || snapshot["timing"]?["continuousEffects"] is not JsonArray effects) return;
        foreach (var row in effects.OfType<JsonObject>())
        {
            var sourceId = row["sourceObjectId"]?.GetValue<string>();
            var no = row["sourceCardNo"]?.GetValue<string>();
            if (sourceId is null || no is null || !Cards.Value.TryGetValue(no, out var card)
                || !state.PlayerZones.Values.Any(z => z.Hand.Concat(z.MainDeck).Concat(z.RuneDeck).Contains(sourceId))) continue;
            var clause = Regex.Match(card.CardEffect,
                @"(?:^|\n)当我变为休眠状态时，给予一名友方单位在本回合内\{\{S\}\}\+(?<amount>[1-9]\d*)。(?:\r?\n|$)",
                RegexOptions.CultureInvariant);
            if (!clause.Success) continue;
            var amount = int.Parse(clause.Groups["amount"].Value);
            var targetId = row["targetObjectId"]!.GetValue<string>();
            var identity = row["effectId"]!.GetValue<string>();
            var target = state.CardObjects[targetId];
            var modifier = Assert.Single(target.UntilEndOfTurnPowerModifiers, m => m.EffectId == identity);
            var matching = captured.Where(item => item.SourceObjectId == sourceId && item.CardNo == no
                && item.EffectKind == "CAPTURED_UNIT_EVENT_ABILITY"
                && item.UnitEvent is { Timing: "BECAME_EXHAUSTED" } context && context.CardNo == no
                && item.TargetObjectIds.SequenceEqual([targetId])
                && item.TargetGenerations?.GetValueOrDefault(targetId, -1) == target.ObjectGeneration
                && identity == $"POWER:{targetId}:{item.StackItemId}:{item.EffectKind}:{amount}:{modifier.AppliedOrder}").ToArray();
            if (matching.Length == 0) continue;
            var witness = Assert.Single(matching);
            Assert.InRange(witness.UnitEvent!.CapturedTick, 1, state.Tick);
            Assert.InRange(witness.UnitEvent.SourceGeneration, 0, state.CardObjects[sourceId].ObjectGeneration - 1);
            Assert.False(target.IsFaceDown); Assert.Contains(CardObjectTags.UnitCard, target.Tags);
            Assert.Contains(state.PlayerZones.Values, z => z.Base.Concat(z.Battlefields).Contains(targetId));
            Assert.Equal(sourceId, modifier.SourceObjectId); Assert.Equal(no, modifier.SourceCardNo);
            Assert.Equal(targetId, modifier.TargetObjectId); Assert.Equal(witness.EffectKind, modifier.EffectKind);
            Assert.Equal("CoreRuleEngine.ApplyPowerModifier", modifier.SourcePath);
            Assert.Equal("UNTIL_END_OF_TURN", modifier.Duration);
            Assert.Equal(amount, modifier.PowerDelta); Assert.Equal(amount, modifier.RequestedPowerDelta);
            Assert.Equal(0, modifier.MinimumPower); Assert.Null(modifier.SnapshotMight); Assert.Null(modifier.ChosenNegativeMight);
            Assert.True(modifier.AppliedOrder > 0);
            var ordered = target.UntilEndOfTurnPowerModifiers.OrderBy(m => m.AppliedOrder).ToArray();
            Assert.Equal(Enumerable.Range(1, ordered.Length), ordered.Select(m => m.AppliedOrder!.Value));
            Assert.Equal(target.UntilEndOfTurnPowerModifier, ordered.Sum(m => m.PowerDelta));
            Assert.Equal(modifier.BasePower + ordered.Where(m => m.AppliedOrder <= modifier.AppliedOrder).Sum(m => m.PowerDelta), modifier.EffectivePower);
            Assert.Equal(modifier.EffectivePower, modifier.ResultingPower);
            Assert.Equal("OBJECT", row["scope"]!.GetValue<string>());
            Assert.Equal(ContinuousEffectLayers.PowerModifier, row["layer"]!.GetValue<string>());
            Assert.Equal(modifier.Duration, row["duration"]!.GetValue<string>());
            Assert.Equal(modifier.SourcePath, row["sourcePath"]!.GetValue<string>());
            Assert.Equal(modifier.EffectKind, row["effectKind"]!.GetValue<string>());
            Assert.Equal(amount, row["powerDelta"]!.GetValue<int>());
            Assert.Equal(amount, row["requestedPowerDelta"]!.GetValue<int>());
            Assert.Equal(amount, row["appliedPowerDelta"]!.GetValue<int>());
            Assert.Equal(modifier.BasePower, row["basePower"]!.GetValue<int>());
            Assert.Equal(modifier.EffectivePower, row["effectivePower"]!.GetValue<int>());
            Assert.Equal(modifier.ResultingPower, row["resultingPower"]!.GetValue<int>());
            Assert.Equal(modifier.AppliedOrder, row["appliedOrder"]!.GetValue<int>());
            Assert.Equal(0, row["minimumPower"]!.GetValue<int>());
            Assert.Null(row["snapshotMight"]); Assert.Null(row["sourceDependencyObjectIds"]);
            row["sourceObjectId"] = "CHECKED_PUBLIC_UNIT_EVENT_MODIFIER";
        }
    }
}
