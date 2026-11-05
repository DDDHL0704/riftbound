using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BorrowedTextSource(ObjectBinding Object, string CardNo, string PhysicalCardNo, long FaceRevision,
    string? TokenFactoryCardNo, RuleTextInstanceBinding? Origin, CeasedSelfAbilitySource? CeasedSource = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BorrowedExhaustContext(BorrowedTextSource Borrower, BorrowedTextSource Donor,
    int AbilityIndex, long AcquiredTick, bool BorrowerWasEmpowered,
    IReadOnlyDictionary<string, string>? TargetTokenFactories = null, bool BorrowerWasOnBattlefield = false,
    bool? LastKnownSourceEmpowered = null, bool ArmamentWasPlayedThisTurn = false, ObjectBinding? DuelBattlefield = null,
    PaidUnitReference? ExhaustedCostUnit = null, ObjectBinding? BattlefieldSource = null,
    string? BattlefieldCardNo = null, string? BattlefieldAbilityKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Native = false);
internal sealed record BorrowedExhaustAbility(string Id, CardObjectState Donor, RuleTextInstance BorrowerText,
    RuleTextInstance DonorText, BorrowedExhaustSpec Spec, ObjectBinding? BattlefieldSource = null,
    string? BattlefieldCardNo = null, string? BattlefieldAbilityKind = null, bool Native = false);

internal static class BorrowedExhaustRules
{
    internal const string Effect = "BORROWED_EXHAUST_ACTIVATION";
    internal const string AttachAnyArmament = "BATTLEFIELD_LEGEND_ATTACH_ARMAMENT";
    internal const string ConversionChoicePrefix = "BORROWED_CONVERSION:";
    private static readonly Lazy<IReadOnlyDictionary<string, OfficialCard>> Cards = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards.ToDictionary(c => c.CardNo));
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<BorrowedExhaustSpec>>> Specs = new(() =>
        Cards.Value.ToDictionary(p => p.Key, p => BorrowedExhaustParser.Parse(p.Value.CardEffect)));
    internal static bool Inherits(string cardNo) => Cards.Value.TryGetValue(cardNo, out var card)
        && BorrowedExhaustParser.InheritsFriendlyExhaustAbilities(card.CardEffect);
    internal static BorrowedExhaustSpec? Spec(string cardNo, int index)
        => Specs.Value.GetValueOrDefault(cardNo, []).FirstOrDefault(s => s.Index == index);
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<BorrowedExhaustSpec>>> NativeSourceSpecs = new(() =>
    {
        var existing = P4ActivatedAbilityCatalog.GetAll().Where(a => !a.BorrowedSourceAlias)
            .SelectMany(P4ActivatedAbilityCatalog.SourceCardNosForAbility).ToHashSet(StringComparer.Ordinal);
        return Cards.Value.Values.Where(card => !existing.Contains(card.CardNo)
                && card.CardCategoryList.Any(category => category is "unit" or "hero_unit" or "exclusive_unit"
                    or "equipment" or "exclusive_equipment")
                && CardBehaviorRegistry.TryGetByCardNo(card.CardNo, out _))
            .ToDictionary(card => card.CardNo, card => (IReadOnlyList<BorrowedExhaustSpec>)
                Specs.Value.GetValueOrDefault(card.CardNo, []).Where(spec =>
                    spec.Kind is "MIGHT_UNIT" or "DAMAGE_UNIT" or "BOON_UNIT").ToArray(), StringComparer.Ordinal);
    });

    internal static IReadOnlyList<BorrowedExhaustSpec> NativeSpecs(string? cardNo)
        => cardNo is null ? [] : NativeSourceSpecs.Value.GetValueOrDefault(cardNo, []);

    private static bool SourceSupportsSpec(MatchState state, string player, CardObjectState source, BorrowedExhaustSpec spec)
        => (!(spec.RequiresEmpowered || spec.RemovesSourceEmpowerment) || source.IsEmpowered)
            && state.PlayerExperience.GetValueOrDefault(player) >= spec.MinimumExperience
            && (!spec.RequiresSourceBattlefield || state.PlayerZones.Values.Any(z => z.Battlefields.Contains(source.ObjectId)));

    private static bool ValidNativeSource(BorrowedExhaustContext context)
        => context.Borrower == context.Donor && context.BattlefieldSource is null
            && context.BattlefieldCardNo is null && context.BattlefieldAbilityKind is null
            && NativeSpecs(context.Borrower.CardNo).Any(spec => spec.Index == context.AbilityIndex);

    internal static BorrowedExhaustSpec? Spec(MatchState state, BorrowedExhaustContext context)
    {
        if (context.BattlefieldAbilityKind is null)
            return Spec(context.Donor.CardNo, context.AbilityIndex);
        if (context.BattlefieldAbilityKind == StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility
            && context.BattlefieldCardNo is { Length: > 0 }
            && BattlefieldStaticAbilitySpecRules.TryGetAbility(context.BattlefieldCardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility, out var experience))
        {
            return new BorrowedExhaustSpec(-1, "BATTLEFIELD_UNIT_GAIN_EXPERIENCE", 0, 0, false)
            {
                ExperienceGain = experience.Amount
            };
        }
        if (context.BattlefieldAbilityKind == StaticAbilityKinds.BattlefieldGrantLegendAttachArmament
            && context.BattlefieldCardNo is { Length: > 0 }
            && BattlefieldStaticAbilitySpecRules.TryGetAbility(context.BattlefieldCardNo,
                BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility, out _))
        {
            return new BorrowedExhaustSpec(-1, AttachAnyArmament, 0, 2, false)
            {
                TargetScope = "FRIENDLY_UNIT"
            };
        }
        return null;
    }
    internal static bool TargetsPlayer(BorrowedExhaustSpec spec) => spec.TargetScope is "PLAYER" or "OPPONENT";

    internal static IEnumerable<BorrowedExhaustAbility> Abilities(MatchState state, string player, CardObjectState source)
    {
        if (source.ControllerId != player || source.IsFaceDown || source.IsExhausted
            || !AttachedRuleTextRules.Field(state, source.ObjectId)) yield break;
        // Printed and acquired exhaust skills share payment, target binding,
        // responses and recovery. Native sources use the same captured text in
        // both roles and carry an explicit discriminator for recovery validation.
        foreach (var text in AttachedRuleTextRules.Instances(state, source))
        foreach (var spec in NativeSpecs(text.CardNo))
        {
            if (!SourceSupportsSpec(state, player, source, spec)) continue;
            var identity = $"NATIVE|{text.Id}|{text.CardNo}|{spec.Index}";
            yield return new("BORROWED:NATIVE:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                source, text, text, spec, Native: true);
        }
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
                if (!SourceSupportsSpec(state, player, source, spec)) continue;
                var identity = $"{borrower.Id}|{borrower.CardNo}|{text.Id}|{text.CardNo}|{spec.Index}";
                yield return new("BORROWED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                    donor, borrower, text, spec);
            }

            // Battlefield static abilities are acquired by the unit/legend that
            // currently has them, even though they are not printed on its card.
            // Keep the granting battlefield in the identity and capture it on
            // the stack item so a response cannot change the source retroactively.
            var donorText = AttachedRuleTextRules.Instances(state, donor).FirstOrDefault();
            if (donorText is null) continue;
            if (donor.Tags.Contains(CardObjectTags.UnitCard, StringComparer.Ordinal)
                && BattlefieldLocalRules.AtUnit(state, donor.ObjectId) is { } experienceBattlefield
                && BattlefieldStaticAbilitySpecRules.TryGetAbility(experienceBattlefield.CardNo,
                    BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility, out var experienceAbility)
                && experienceAbility.Amount > 0)
            {
                var dynamicSpec = new BorrowedExhaustSpec(-1, "BATTLEFIELD_UNIT_GAIN_EXPERIENCE", 0, 0, false)
                {
                    ExperienceGain = experienceAbility.Amount
                };
                var identity = $"{borrower.Id}|{borrower.CardNo}|{donorText.Id}|{donorText.CardNo}|BATTLEFIELD_UNIT_GAIN_EXPERIENCE|{experienceBattlefield.ObjectId}|{experienceBattlefield.ObjectGeneration}";
                yield return new("BORROWED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                    donor, borrower, donorText, dynamicSpec,
                    new(experienceBattlefield.ObjectId, experienceBattlefield.ObjectGeneration), experienceBattlefield.CardNo,
                    StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility);
            }
            if (donor.Tags.Contains(CardObjectTags.LegendCard, StringComparer.Ordinal)
                && BattlefieldLocalRules.ControlledBy(state, player)
                    .OrderBy(battlefield => battlefield.ObjectId, StringComparer.Ordinal)
                    .FirstOrDefault(battlefield =>
                    BattlefieldStaticAbilitySpecRules.TryGetAbility(battlefield.CardNo,
                        BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility, out _)) is { } attachBattlefield)
            {
                var dynamicSpec = new BorrowedExhaustSpec(-1, AttachAnyArmament, 0, 2, false)
                {
                    TargetScope = "FRIENDLY_UNIT"
                };
                var identity = $"{borrower.Id}|{borrower.CardNo}|{donorText.Id}|{donorText.CardNo}|BATTLEFIELD_LEGEND_ATTACH_ARMAMENT|{attachBattlefield.ObjectId}|{attachBattlefield.ObjectGeneration}";
                yield return new("BORROWED:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))),
                    donor, borrower, donorText, dynamicSpec,
                    new(attachBattlefield.ObjectId, attachBattlefield.ObjectGeneration), attachBattlefield.CardNo,
                    StaticAbilityKinds.BattlefieldGrantLegendAttachArmament);
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
            && Spec(state, context) is { Kind: "INSIGHT_DRAW_EXPERIENCE" } spec
            ? spec : null;

    private static bool ValidBattlefieldSource(MatchState state, BorrowedExhaustContext context)
    {
        if (context.BattlefieldAbilityKind is null)
            return context.BattlefieldSource is null && context.BattlefieldCardNo is null;
        if (context.BattlefieldSource is not { } binding || context.BattlefieldCardNo is not { Length: > 0 }) return false;
        var validCard = context.BattlefieldAbilityKind switch
        {
            StaticAbilityKinds.BattlefieldGrantUnitExperienceAbility =>
                BattlefieldStaticAbilitySpecRules.TryGetAbility(context.BattlefieldCardNo,
                    BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantUnitExperienceAbility, out _),
            StaticAbilityKinds.BattlefieldGrantLegendAttachArmament =>
                BattlefieldStaticAbilitySpecRules.TryGetAbility(context.BattlefieldCardNo,
                    BattlefieldStaticAbilitySpecRules.IsBattlefieldGrantLegendAttachArmamentAbility, out _),
            _ => false
        };
        if (!validCard || binding.Generation < 0 || string.IsNullOrWhiteSpace(binding.ObjectId)) return false;
        return !state.CardObjects.TryGetValue(binding.ObjectId, out var current)
            || (current.ObjectGeneration >= binding.Generation
                && (current.ObjectGeneration != binding.Generation || current.CardNo == context.BattlefieldCardNo));
    }
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
            || item.CardNo != c.Borrower.PhysicalCardNo
            || (c.Native ? !ValidNativeSource(c)
                : c.Borrower.Object.ObjectId == c.Donor.Object.ObjectId || !Inherits(c.Borrower.CardNo))
            || !ValidBattlefieldSource(state, c)
            || Spec(state, c) is not { } spec
            || !CoreRuleEngine.BorrowedEnemyTargetRequirementMet(state, item.ControllerId, spec)
            || !ValidSource(state, c.Borrower, c.AcquiredTick, spec.DestroysSourceAsCost || spec.DestroyedFriendlyCardTypeAsCost.Length > 0)
            || !ValidSource(state, c.Donor, c.AcquiredTick,
                spec.DestroyedFriendlyCardTypeAsCost.Length > 0 || c.Native && spec.DestroysSourceAsCost)
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
