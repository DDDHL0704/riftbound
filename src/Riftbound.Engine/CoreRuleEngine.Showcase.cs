using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record ShowcaseSpec(string Filter = "ANY", bool ReadyEntry = false,
    bool InsightFromPowerCost = false, int DrawAfter = 0, int GenericPower = 0);

public sealed record CeasedShowcaseToken(ObjectBinding Object, string FactoryCardNo, long Tick,
    string CardNo, long FaceRevision, long CapturedTick);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShowcaseReceipt(
    [property: JsonRequired] ObjectBinding Object,
    [property: JsonRequired] string CardNo,
    [property: JsonRequired] string PlayerId,
    [property: JsonRequired] long Tick,
    [property: JsonRequired] long FaceRevision,
    [property: JsonRequired] int PowerCost,
    [property: JsonRequired] bool FromHand,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TokenFactoryCardNo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CeasedShowcaseToken? CeasedToken = null);

public sealed partial class CoreRuleEngine
{
    internal const string ShowcaseChoicePrefix = "SHOWCASE:";
    private static readonly Lazy<IReadOnlySet<string>> PrintedStandbyCards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Where(c => c.CardEffect.Split('\n').Any(line => line.TrimStart().StartsWith("{{待命}}", StringComparison.Ordinal)))
            .Select(c => c.CardNo).ToHashSet(StringComparer.Ordinal));

    internal static IReadOnlyList<string> ShowcaseChoices(MatchState state, string player,
        string? source, CardBehaviorDefinition behavior)
    {
        if (behavior.Showcase is not { } spec || !state.PlayerZones.TryGetValue(player, out var zones)) return [];
        return state.CardObjects.Values.Where(card => card.ObjectId != source && !card.IsFaceDown
            && !string.IsNullOrWhiteSpace(card.CardNo)
            && (zones.Hand.Contains(card.ObjectId) || PublicStaticAuraSourceObjectIds(state.PlayerZones).Contains(card.ObjectId)
                && EffectivePublicStaticAuraSourceControllerId(state.PlayerZones, card.ObjectId, card) == player)
            && (spec.Filter == "ANY" && !IsTokenObject(card) || spec.Filter == "MECHANICAL" && card.Tags.Contains("机械")
                || spec.Filter == "STANDBY" && !IsTokenObject(card) && (card.Tags.Contains("待命") || AttachedRuleTextRules.Instances(state, card).Any(instance => PrintedStandbyCards.Value.Contains(instance.CardNo)))))
            .Select(card => ShowcaseChoicePrefix + card.ObjectId).Order(StringComparer.Ordinal).ToArray();
    }

    private static bool TrySelectShowcase(MatchState state, string player, string source,
        CardBehaviorDefinition behavior, IReadOnlyList<string>? selections,
        out ShowcaseReceipt? receipt, out IReadOnlyList<string> remaining)
    {
        var choices = (selections ?? []).Where(c => c?.Trim().StartsWith(ShowcaseChoicePrefix, StringComparison.Ordinal) == true).ToArray();
        remaining = (selections ?? []).Where(c => c?.Trim().StartsWith(ShowcaseChoicePrefix, StringComparison.Ordinal) != true).ToArray();
        receipt = null;
        if (choices.Length == 0) return true;
        if (choices.Length != 1 || !ShowcaseChoices(state, player, source, behavior).Contains(choices[0])) return false;
        var card = state.CardObjects[choices[0][ShowcaseChoicePrefix.Length..]];
        receipt = new(new(card.ObjectId, card.ObjectGeneration), card.CardNo!, player, state.Tick + 1,
            card.CopyFaceTransitions?.Count ?? 0, PrintedPowerCostRules.ForCard(card.CardNo!).Amount,
            state.PlayerZones[player].Hand.Contains(card.ObjectId),
            IsTokenObject(card) ? card.TokenFactoryCardNo ?? card.CardNo : null);
        return true;
    }

    private static CardBehaviorDefinition ShowcaseDefinition(CardBehaviorDefinition behavior, StackItemState item)
        => behavior.Showcase is not { } spec ? behavior : behavior with {
            InsightCount = item.Showcase is not null && spec.InsightFromPowerCost ? item.Showcase.PowerCost : 0,
            DrawAfterInsightCount = item.Showcase is not null ? spec.DrawAfter : 0 };

    internal static bool ValidShowcaseReceipt(MatchState state, ShowcaseReceipt? receipt, string sourceCardNo, string source)
    {
        if (receipt is null) return true;
        if (!(CardBehaviorRegistry.TryGetByCardNo(sourceCardNo, out var behavior) && behavior.Showcase is not null
            && receipt.Object is { Generation: >= 0 } binding && binding.ObjectId != source
            && !string.IsNullOrWhiteSpace(binding.ObjectId) && !string.IsNullOrWhiteSpace(receipt.PlayerId)
            && state.Seats.ContainsKey(receipt.PlayerId) && receipt.Tick > 0 && receipt.Tick <= state.Tick
            && receipt.FaceRevision >= 0 && !string.IsNullOrWhiteSpace(receipt.CardNo)
            && receipt.PowerCost == PrintedPowerCostRules.ForCard(receipt.CardNo).Amount)) return false;
        if (receipt.TokenFactoryCardNo is { } factory
            && (receipt.FromHand || behavior.Showcase.Filter != "MECHANICAL" || !P6TokenFactoryCatalog.IsTokenFactory(factory))) return false;
        if (!state.CardObjects.TryGetValue(binding.ObjectId, out var card))
            return receipt.TokenFactoryCardNo is not null && receipt.CeasedToken is { } ceased
                && ceased.Object == binding && ceased.FactoryCardNo == receipt.TokenFactoryCardNo
                && ceased.Tick >= receipt.Tick && ceased.Tick <= state.Tick && ceased.CardNo == receipt.CardNo
                && ceased.FaceRevision == receipt.FaceRevision && ceased.CapturedTick == receipt.Tick;
        return receipt.CeasedToken is null && card.ObjectGeneration >= binding.Generation
            && (IsTokenObject(card) ? card.TokenFactoryCardNo ?? card.CardNo : null) == receipt.TokenFactoryCardNo
            && (card.ObjectGeneration != binding.Generation || CopyFaceTimeline.Valid(state, card)
                && CopyFaceTimeline.FaceAt(card, receipt.FaceRevision, receipt.Tick) == receipt.CardNo);
    }

    private static MatchState RecordCeasedShowcaseTokens(MatchState before, MatchState state, IReadOnlySet<string> departed)
    {
        ShowcaseReceipt? Capture(ShowcaseReceipt? receipt)
        {
            if (receipt is null || receipt.CeasedToken is not null || !departed.Contains(receipt.Object.ObjectId)
                || receipt.TokenFactoryCardNo is not { } factory) return receipt;
            var card = before.CardObjects[receipt.Object.ObjectId];
            if (card.ObjectGeneration != receipt.Object.Generation || (card.TokenFactoryCardNo ?? card.CardNo) != factory) return receipt;
            return receipt with { CeasedToken = new(receipt.Object, factory, state.Tick,
                CopyFaceTimeline.FaceAt(card, receipt.FaceRevision, receipt.Tick) ?? string.Empty, receipt.FaceRevision, receipt.Tick) };
        }
        return state with {
            CardPlays = state.CardPlays with {
                Finalized = state.CardPlays.Finalized.Select(p => p with { Showcase = Capture(p.Showcase) }).ToArray(),
                Resolved = state.CardPlays.Resolved.Select(p => p with { Showcase = Capture(p.Showcase) }).ToArray() },
            StackItems = state.StackItems.Select(i => i with { Showcase = Capture(i.Showcase) }).ToArray() };
    }

    internal static bool ValidShowcase(MatchState state, StackItemState item)
    {
        if (!ValidShowcaseReceipt(state, item.Showcase, item.CardNo, item.SourceObjectId)) return false;
        if (item.Showcase is not { } receipt) return !item.SourceConfirmed
            || !CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var behavior) || behavior.Showcase is null
            || item.EffectKind != behavior.EffectKind;
        return CardBehaviorRegistry.TryGetByCardNo(item.CardNo, out var definition) && item.EffectKind == definition.EffectKind
            && !item.OptionalCosts.Any(c => c.StartsWith(ShowcaseChoicePrefix, StringComparison.Ordinal))
            && state.CardPlays.Finalized.Any(play => play.PlayId == item.StackItemId && play.CardNo == item.CardNo
                && play.PlayerId == receipt.PlayerId && play.Source.ObjectId == item.SourceObjectId && play.Showcase == receipt);
    }
}
