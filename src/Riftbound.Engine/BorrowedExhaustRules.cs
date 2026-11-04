using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BorrowedTextSource(ObjectBinding Object, string CardNo, string PhysicalCardNo, long FaceRevision,
    string? TokenFactoryCardNo, RuleTextInstanceBinding? Origin, CeasedSelfAbilitySource? CeasedSource = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BorrowedExhaustContext(BorrowedTextSource Borrower, BorrowedTextSource Donor,
    int AbilityIndex, long AcquiredTick, bool BorrowerWasEmpowered,
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null, bool BorrowerWasOnBattlefield = false,
    bool? LastKnownSourceEmpowered = null, bool ArmamentWasPlayedThisTurn = false, ObjectBinding? DuelBattlefield = null, PaidUnitReference? ExhaustedCostUnit = null);
internal sealed record BorrowedExhaustAbility(string Id, CardObjectState Donor, RuleTextInstance BorrowerText,
    RuleTextInstance DonorText, BorrowedExhaustSpec Spec);

internal static class BorrowedExhaustRules
{
    internal const string Effect = "BORROWED_EXHAUST_ACTIVATION";
    internal const string ConversionChoicePrefix = "BORROWED_CONVERSION:";
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<BorrowedExhaustSpec>>> Specs = new(() =>
        Cards.Value.ToDictionary(p => p.Key, p => BorrowedExhaustParser.Parse(p.Value.CardEffect)));
    internal static bool Inherits(string cardNo) => Cards.Value.TryGetValue(cardNo, out var card)
        && BorrowedExhaustParser.InheritsFriendlyExhaustAbilities(card.CardEffect);
    internal static BorrowedExhaustSpec? Spec(string cardNo, int index)
        => Specs.Value.GetValueOrDefault(cardNo, []).FirstOrDefault(s => s.Index == index);
    internal static bool TargetsPlayer(BorrowedExhaustSpec spec) => spec.TargetScope is "PLAYER" or "OPPONENT";

    internal static IEnumerable<BorrowedExhaustAbility> Abilities(MatchState state, string player, CardObjectState source)
    {
        if (source.ControllerId != player || source.IsFaceDown || source.IsExhausted
            || !AttachedRuleTextRules.Field(state, source.ObjectId)) yield break;
        foreach (var (borrower, donor) in Providers(state, player, source))
        {
            if (!Cards.Value.TryGetValue(AttachedRuleTextRules.Face(donor) ?? "", out var printed)
                || !printed.CardCategoryList.Any(category => category is "legendary" or "unit" or "hero_unit"
                    or "exclusive_unit" or "equipment" or "exclusive_equipment")) continue;
            foreach (var text in AttachedRuleTextRules.Instances(state, donor))
            foreach (var spec in Specs.Value.GetValueOrDefault(text.CardNo, []))
            {
                // 727.1.b.1: enumerate the entire skill before checking its
                // dependency on the borrowing source, never the donor.
                if ((spec.RequiresEmpowered || spec.RemovesSourceEmpowerment) && !source.IsEmpowered) continue;
                if (state.PlayerExperience.GetValueOrDefault(player) < spec.MinimumExperience) continue;
                if (spec.RequiresSourceBattlefield && !state.PlayerZones.Values.Any(z => z.Battlefields.Contains(source.ObjectId))) continue;
                var identity = $"{borrower.Id}|{borrower.CardNo}|{text.Id}|{text.CardNo}|{spec.Index}";
                yield return new("BORROWED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                    donor, borrower, text, spec);
            }
        }
    }

    internal static IEnumerable<(RuleTextInstance Borrower, CardObjectState Donor)> Providers(
        MatchState state, string player, CardObjectState source)
    {
        if (source.ControllerId != player || source.IsFaceDown || source.IsExhausted
            || !AttachedRuleTextRules.Field(state, source.ObjectId)) yield break;
        foreach (var borrower in AttachedRuleTextRules.Instances(state, source).Where(i => Inherits(i.CardNo)))
        foreach (var donor in state.CardObjects.Values.OrderBy(c => c.ObjectId, StringComparer.Ordinal))
        {
            if (donor.ObjectId == source.ObjectId || donor.ControllerId != player || donor.IsFaceDown
                || !AttachedRuleTextRules.Field(state, donor.ObjectId)
                    && !state.PlayerZones.GetValueOrDefault(player, PlayerZones.Empty).LegendZone.Contains(donor.ObjectId)) continue;
            if (!Cards.Value.TryGetValue(AttachedRuleTextRules.Face(donor) ?? "", out var printed)
                || !printed.CardCategoryList.Any(category => category is "legendary" or "unit" or "hero_unit"
                    or "exclusive_unit" or "equipment" or "exclusive_equipment")) continue;
            yield return (borrower, donor);
        }
    }

    internal static BorrowedTextSource Capture(CardObjectState card, RuleTextInstance text)
        => new(new(card.ObjectId, card.ObjectGeneration), text.CardNo, AttachedRuleTextRules.Face(card)!, card.CopyFaceTransitions?.Count ?? 0,
            card.TokenFactoryCardNo, text.Origin);
    internal static BorrowedExhaustSpec? InsightSpec(MatchState state, StackItemState item)
        => item.BorrowedExhaust is { } context
            && Valid(state, item)
            && Spec(context.Donor.CardNo, context.AbilityIndex) is { Kind: "INSIGHT_DRAW_EXPERIENCE" } spec
            ? spec : null;
    internal static bool ValidSource(MatchState state, BorrowedTextSource source, long tick, bool ceasedAsCost = false)
        => source is { Object: not null } && SelfAbilitySourceRules.Valid(state, source.Object, source.PhysicalCardNo,
            source.FaceRevision, tick, source.TokenFactoryCardNo, source.CeasedSource,
            usesPrintedFace: true, ceasedAsCost: ceasedAsCost)
            && (source.Origin is null ? source.CardNo == source.PhysicalCardNo
                : AttachedRuleTextRules.ValidCaptured(state, source.Origin, source.Object.ObjectId, source.Object.Generation, source.CardNo));

    internal static bool Valid(MatchState state, StackItemState item)
    {
        if (item.BorrowedExhaust is not { } c) return item.EffectKind != Effect;
        if (item.EffectKind != Effect || c.Borrower is null || c.Donor is null
            || c.AcquiredTick <= 0 || c.AcquiredTick > state.Tick || !state.Seats.ContainsKey(item.ControllerId)
            || c.Borrower.Object is null || c.Donor.Object is null || c.Borrower.Object.ObjectId != item.SourceObjectId
            || c.Borrower.Object.ObjectId == c.Donor.Object.ObjectId || item.CardNo != c.Borrower.PhysicalCardNo || !Inherits(c.Borrower.CardNo)
            || Spec(c.Donor.CardNo, c.AbilityIndex) is not { } spec
            || !CoreRuleEngine.BorrowedEnemyTargetRequirementMet(state, item.ControllerId, spec)
            || !ValidSource(state, c.Borrower, c.AcquiredTick, spec.DestroysSourceAsCost || spec.DestroyedFriendlyCardTypeAsCost.Length > 0)
            || !ValidSource(state, c.Donor, c.AcquiredTick, spec.DestroyedFriendlyCardTypeAsCost.Length > 0)
            || spec.Kind == "RESOURCE" || spec.TargetScope == "UNLINKED_ATTRIBUTE"
            || (spec.RequiresEmpowered || spec.RemovesSourceEmpowerment) && !c.BorrowerWasEmpowered
            || spec.RequiresSourceBattlefield && !c.BorrowerWasOnBattlefield
            || spec.RequiresPlayedArmamentThisTurn != c.ArmamentWasPlayedThisTurn
            || (spec.EmpoweredMight.HasValue || spec.ReadyTargetIfSourceEmpowered) != c.LastKnownSourceEmpowered.HasValue
            || (spec.TargetScope == "FRIENDLY_DUEL_UNIT") != (c.DuelBattlefield is not null)
            || c.DuelBattlefield is { } field && (field.Generation < 0 || string.IsNullOrWhiteSpace(field.ObjectId)
                || !state.CardObjects.TryGetValue(field.ObjectId, out var battlefield)
                || battlefield.ObjectGeneration < field.Generation
                || !battlefield.Tags.Contains(P6TokenFactoryCatalog.BattlefieldCardTag))
            || !CoreRuleEngine.ValidExhaustedCostUnit(state, c.ExhaustedCostUnit, spec.MovesToExhaustedCostUnit,
                item.SourceObjectId, item.TargetObjectIds)
            || item.InsightContext is not null || item.InsightCompleted && spec.Kind != "INSIGHT_DRAW_EXPERIENCE"
            || item.EffectPlayCompleted && spec.Kind is not ("RETURN_UNIT_FREE_PLAY" or "DESTROY_DECK_PLAY")
            || (spec.Kind == "DESTROY_DECK_PLAY") != (item.DestroyDeckPlay is not null)
            || item.SourceConfirmed || item.EffectRepeatCount != 1 || item.DamageAmount != 0
            || item.OptionalCosts.Count != 0 || item.Destination.Length != 0
            || item.TargetObjectIds.Count != spec.Targets || item.TargetObjectIds.Distinct().Count() != spec.Targets
            || item.TargetGenerations is not { } bindings || bindings.Count != (TargetsPlayer(spec) ? 0 : spec.Targets)
            || TargetsPlayer(spec) && (c.TargetTokenFactories is not null
                || item.TargetObjectIds.Any(id => !state.Seats.ContainsKey(id) || spec.TargetScope == "OPPONENT" && id == item.ControllerId))
            || !TargetsPlayer(spec) && item.TargetObjectIds.Any(id => !bindings.TryGetValue(id, out var generation) || generation < 0
                || state.CardObjects.TryGetValue(id, out var target) && generation > target.ObjectGeneration)
            || c.TargetTokenFactories is { } factories && factories.Any(pair => !item.TargetObjectIds.Contains(pair.Key, StringComparer.Ordinal)
                || !P6TokenFactoryCatalog.IsTokenFactory(pair.Value)
                || state.CardObjects.TryGetValue(pair.Key, out var token) && (token.TokenFactoryCardNo ?? token.CardNo) != pair.Value)
            || !TargetsPlayer(spec) && item.TargetObjectIds.Any(id => state.CardObjects.TryGetValue(id, out var token)
                && token.ObjectGeneration == bindings[id] && TokenObjectRules.IsToken(token)
                && (c.TargetTokenFactories is null || !c.TargetTokenFactories.TryGetValue(id, out var factory)
                    || factory != (token.TokenFactoryCardNo ?? token.CardNo)))
            || !TargetsPlayer(spec) && item.TargetObjectIds.Any(id => !state.CardObjects.ContainsKey(id)
                && (c.TargetTokenFactories is null || !c.TargetTokenFactories.ContainsKey(id)))
            || (spec.Reaction ? item.TimingContext is not (TimingStates.NeutralOpen or TimingStates.SpellDuelOpen
                    or TimingStates.NeutralClosed or TimingStates.SpellDuelClosed)
                : spec.Swift ? item.TimingContext is not (TimingStates.NeutralOpen or TimingStates.SpellDuelOpen)
                : item.TimingContext != TimingStates.NeutralOpen)) return false;
        return true;
    }
}
