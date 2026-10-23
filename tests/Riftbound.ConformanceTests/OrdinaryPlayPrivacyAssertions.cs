using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class OrdinaryPlayPrivacyAssertions
{
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadDefaultAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));

    internal static void CheckHistoricalModifiers(JsonNode snapshot, MatchState state)
    {
        if (snapshot["timing"]?["continuousEffects"] is not JsonArray effects) return;
        var checkedRows = new List<JsonObject>();
        foreach (var row in effects.OfType<JsonObject>())
        {
            var sourceId = row["sourceObjectId"]?.GetValue<string>();
            if (sourceId is null || !state.PlayerZones.Values.Any(z => z.Hand.Concat(z.MainDeck).Concat(z.RuneDeck).Contains(sourceId))) continue;
            var no = row["sourceCardNo"]?.GetValue<string>();
            if (no is null || !Cards.Value.TryGetValue(no, out var card)) continue;
            if (!card.CardCategoryNameList.Contains("单位")) continue;
            var clause = Regex.Match(card.CardEffect,
                @"^当你打出我时，(?:给予|让)一名单位(?:在)?本回合(?:内)?\{\{S\}\}\+(?<amount>[1-9]\d*)。$",
                RegexOptions.CultureInvariant);
            if (!clause.Success) continue;
            var amount = int.Parse(clause.Groups["amount"].Value);
            var targetId = row["targetObjectId"]!.GetValue<string>();
            var identity = row["effectId"]!.GetValue<string>();
            Assert.True(state.CardObjects.TryGetValue(targetId, out var target));
            Assert.False(target.IsFaceDown);
            Assert.Contains(CardObjectTags.UnitCard, target.Tags);
            Assert.Contains(state.PlayerZones.Values, z => z.Base.Concat(z.Battlefields).Contains(targetId));
            Assert.True(state.ObjectLocations.TryGetValue(targetId, out var location) && location.Zone is "BASE" or "BATTLEFIELD");
            Assert.Single(effects.OfType<JsonObject>(), e => e["effectId"]?.GetValue<string>() == identity);
            Assert.Single(state.CardObjects.Values.SelectMany(c => c.UntilEndOfTurnPowerModifiers), m => m.EffectId == identity);
            var modifier = Assert.Single(target.UntilEndOfTurnPowerModifiers, m => m.EffectId == identity);
            Assert.Equal(sourceId, modifier.SourceObjectId); Assert.Equal(no, modifier.SourceCardNo);
            Assert.Equal(targetId, modifier.TargetObjectId); Assert.Equal("CoreRuleEngine.ApplyPowerModifier", modifier.SourcePath);
            Assert.Equal(amount, modifier.PowerDelta); Assert.Equal(amount, modifier.RequestedPowerDelta);
            Assert.True(modifier.AppliedOrder > 0);
            Assert.False(string.IsNullOrWhiteSpace(modifier.EffectKind));
            Assert.Equal("UNTIL_END_OF_TURN", modifier.Duration);
            Assert.Equal(0, modifier.MinimumPower); Assert.Null(modifier.SnapshotMight); Assert.Null(modifier.ChosenNegativeMight);
            var ordered = target.UntilEndOfTurnPowerModifiers.OrderBy(m => m.AppliedOrder).ToArray();
            Assert.Equal(Enumerable.Range(1, ordered.Length), ordered.Select(m => m.AppliedOrder!.Value));
            Assert.Equal(target.UntilEndOfTurnPowerModifier, ordered.Sum(m => m.PowerDelta));
            Assert.Equal(ordered[^1].ResultingPower, target.Power);
            Assert.Equal(modifier.BasePower + ordered.Where(m => m.AppliedOrder <= modifier.AppliedOrder).Sum(m => m.PowerDelta), modifier.EffectivePower);
            Assert.Equal(modifier.EffectivePower, modifier.ResultingPower);
            var play = Assert.Single(state.CardPlays.Resolved, p => p.Source.ObjectId == sourceId && p.CardNo == no
                && identity == $"POWER:{targetId}:{p.PlayId}:{modifier.EffectKind}:{amount}:{modifier.AppliedOrder}");
            var finalized = Assert.Single(state.CardPlays.Finalized, p => p.PlayId == play.PlayId);
            Assert.Equal(play.Source, finalized.Source); Assert.Equal(play.CardNo, finalized.CardNo);
            Assert.Equal(play.Types, finalized.Types); Assert.Equal(1, play.Types);
            Assert.Equal(play.PlayerId, finalized.PlayerId); Assert.Equal(play.FaceRevision, finalized.FaceRevision);
            Assert.InRange(finalized.Tick, 1, play.Tick);
            Assert.InRange(play.Tick, 1, state.Tick);
            Assert.True(play.Source.Generation < state.CardObjects[sourceId].ObjectGeneration);
            Assert.Equal("OBJECT", row["scope"]!.GetValue<string>());
            Assert.Equal(ContinuousEffectLayers.PowerModifier, row["layer"]!.GetValue<string>());
            Assert.Equal("UNTIL_END_OF_TURN", row["duration"]!.GetValue<string>());
            Assert.Equal(modifier.SourcePath, row["sourcePath"]!.GetValue<string>());
            // The effect kind is an opaque original-ledger identity. The
            // printed amount/semantics above never come from the registry.
            Assert.Equal(modifier.EffectKind, row["effectKind"]!.GetValue<string>());
            Assert.Equal(amount, row["powerDelta"]!.GetValue<int>());
            Assert.Equal(modifier.BasePower, row["basePower"]!.GetValue<int>());
            Assert.Equal(modifier.EffectivePower, row["effectivePower"]!.GetValue<int>());
            Assert.Equal(amount, row["requestedPowerDelta"]!.GetValue<int>());
            Assert.Equal(amount, row["appliedPowerDelta"]!.GetValue<int>());
            Assert.Equal(0, row["minimumPower"]!.GetValue<int>());
            Assert.Equal(modifier.ResultingPower, row["resultingPower"]!.GetValue<int>());
            Assert.Equal(modifier.AppliedOrder, row["appliedOrder"]!.GetValue<int>());
            Assert.Null(row["snapshotMight"]); Assert.Null(row["sourceDependencyObjectIds"]);
            // Exempt only the verified original public provenance field. The
            // current private object and all other references remain checked.
            checkedRows.Add(row);
        }
        foreach (var row in checkedRows) row["sourceObjectId"] = "CHECKED_PUBLIC_PLAY_MODIFIER";
    }
}
