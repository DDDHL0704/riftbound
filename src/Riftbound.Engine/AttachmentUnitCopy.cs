using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record CopyableUnitTraits(string CardNo, int ManaCost, int Power, IReadOnlyList<string> Tags);
public sealed record AttachmentUnitCopyEffect(ObjectBinding Equipment, long Revision, CopyableUnitTraits Traits,
    long CapturedTick, long FaceRevision);
public sealed record AttachmentUnitCopyLedger(CopyableUnitTraits Basis, long? BasisFaceRevision,
    long? BasisCapturedTick, IReadOnlyList<AttachmentUnitCopyEffect> Effects);

internal static class AttachmentUnitCopyRules
{
    internal static bool GrantsCopy(string? cardNo) => OfficialArmamentCatalog.Find(cardNo)?.CopiesChosenFriendlyUnit == true;

    internal static CopyableUnitTraits Traits(string cardNo)
    {
        CardObjectState printed;
        if (P6TokenFactoryCatalog.TryGetByCardNo(cardNo, out var token)) printed = token.CreateObject("traits", "P1", "P1");
        else if (PrintedCardFactory.TryCreate("traits", "P1", cardNo, out var card)) printed = card;
        else throw new InvalidOperationException("Copy source has no printed characteristics.");
        return new(cardNo, printed.ManaCost, printed.Power, printed.Tags.Concat(AttachedRuleTextRules.PrintedKeywords(cardNo))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    internal static CardObjectState Apply(CardObjectState card, CopyableUnitTraits traits, long tick)
    {
        var previous = Traits(AttachedRuleTextRules.Face(card)!);
        var preserved = card.Tags.Where(tag => !previous.Tags.Contains(tag) && !(card.RuleDerivedTags ?? []).Contains(tag))
            .Concat(card.GrantedTags ?? []);
        return CopyFaceTimeline.ChangeFace(card, traits.CardNo, tick) with {
            ManaCost = traits.ManaCost, Power = card.Power - previous.Power + traits.Power,
            Tags = preserved.Concat(traits.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            RuleDerivedTags = null
        };
    }

    internal static RuleTextInstance? CurrentText(CardObjectState host)
    {
        if (host.AttachmentUnitCopies is not { Effects: { Count: > 0 } effects }
            || effects.LastOrDefault() is not { Equipment: not null, Traits: not null } copy) return null;
        var origin = new RuleTextInstanceBinding(copy.Traits.CardNo, new(host.ObjectId, host.ObjectGeneration),
            copy.Equipment, copy.Revision, copy.CapturedTick, copy.FaceRevision);
        return new($"ATTACHED:{copy.Equipment.ObjectId}:{copy.Equipment.Generation}:{copy.Revision}", copy.Traits.CardNo, origin);
    }

    internal static bool Active(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState host, AttachmentUnitCopyEffect copy)
        => copy.Equipment is not null && cards.TryGetValue(copy.Equipment.ObjectId, out var gear)
            && gear.ObjectGeneration == copy.Equipment.Generation && gear.AttachedToObjectId == host.ObjectId
            && gear.AttachmentRevision == copy.Revision && GrantsCopy(gear.CardNo)
            && zones.Values.Any(z => z.Base.Contains(host.ObjectId) || z.Battlefields.Contains(host.ObjectId))
            && zones.Values.Any(z => z.Base.Contains(gear.ObjectId) || z.Battlefields.Contains(gear.ObjectId));

    internal static bool Valid(MatchState state, CardObjectState host)
    {
        if (host.AttachmentUnitCopies is not { } ledger) return true;
        bool ValidTraits(CopyableUnitTraits? frame) => frame is not null && CopyCharacteristics.HasFace(frame.CardNo)
            && frame.Tags is not null && Traits(frame.CardNo) is { } printed
            && frame.ManaCost == printed.ManaCost && frame.Power == printed.Power && frame.Tags.SequenceEqual(printed.Tags);
        if (!ValidTraits(ledger.Basis) || ledger.BasisCapturedTick is not >= 0 || ledger.BasisCapturedTick > state.Tick
            || CopyFaceTimeline.FaceAt(host, ledger.BasisFaceRevision, ledger.BasisCapturedTick) != ledger.Basis.CardNo
            || ledger.Effects is not { Count: > 0 }
            || ledger.Effects.Any(copy => copy is null || !ValidTraits(copy.Traits) || copy.Revision <= 0
                || copy.CapturedTick < 0 || copy.CapturedTick > state.Tick || copy.FaceRevision < 0
                || copy.Equipment is null || copy.Equipment.Generation < 0 || string.IsNullOrWhiteSpace(copy.Equipment.ObjectId)
                || copy.Equipment.ObjectId == host.ObjectId
                || !Active(state.PlayerZones, state.CardObjects, host, copy)
                || CopyFaceTimeline.FaceAt(host, copy.FaceRevision, copy.CapturedTick) != copy.Traits.CardNo)
            || ledger.Effects.Select(copy => copy.Equipment.ObjectId).Distinct().Count() != ledger.Effects.Count) return false;
        return AttachedRuleTextRules.Face(host) == ledger.Effects[^1].Traits.CardNo;
    }
}

public sealed partial class CoreRuleEngine
{
    private static void ApplyAttachmentUnitCopy(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, IReadOnlyDictionary<string, ObjectLocationState> locations,
        string gearId, string hostId, string actor, List<GameEvent> events)
    {
        var gear = cards[gearId]; var host = cards[hostId];
        if (!AttachmentUnitCopyRules.GrantsCopy(gear.CardNo)) return;
        var options = cards.Values.Where(card => card.ObjectId != hostId && IsFaceUpNonStandbyUnit(card)
                && IsObjectOnField(zones, card.ObjectId) && CopyCharacteristics.HasFace(AttachedRuleTextRules.Face(card))
                && EffectiveFieldControllerId(zones, card.ObjectId, card) == actor)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
            .Select(card => new RuleChoiceOption("COPY:" + card.ObjectId,
                "复制 " + (CardBehaviorRegistry.TryGetByCardNo(card.CardNo ?? "", out var behavior) ? behavior.DisplayName : card.CardNo),
                [card.ObjectId])).ToArray();
        if (options.Length == 0) return;
        var preview = state with { PlayerZones = zones, CardObjects = cards, ObjectLocations = locations };
        var selected = RuleChoices.Value!.Choose(actor, "武装已经贴附：选择另一名友方单位的可复制特质", options,
            kind: "ATTACHMENT_COPY_SELECTION", preview: preview);
        var target = cards[selected[5..]];
        using var instruction = new MightInstructionScope(state, zones, cards, events, locations);
        var traits = AttachmentUnitCopyRules.Traits(AttachedRuleTextRules.Face(target)!);
        var basis = host.AttachmentUnitCopies?.Basis ?? AttachmentUnitCopyRules.Traits(AttachedRuleTextRules.Face(host)!);
        var basisRevision = host.AttachmentUnitCopies is { } prior ? prior.BasisFaceRevision : host.CopyFaceTransitions?.Count ?? 0;
        var basisTick = host.AttachmentUnitCopies is { } existing ? existing.BasisCapturedTick : state.Tick + 1;
        var revision = checked(gear.AttachmentRevision + 1);
        cards[gearId] = gear with { AttachmentRevision = revision };
        var changed = AttachmentUnitCopyRules.Apply(host, traits, state.Tick + 1);
        var effect = new AttachmentUnitCopyEffect(new(gearId, gear.ObjectGeneration), revision, traits,
            state.Tick + 1, changed.CopyFaceTransitions?.Count ?? 0);
        cards[hostId] = changed with { AttachmentUnitCopies = new(basis, basisRevision, basisTick,
            (host.AttachmentUnitCopies?.Effects ?? []).Where(copy => copy.Equipment.ObjectId != gearId).Append(effect).ToArray()) };
        events.Add(new("UNIT_COPY_APPLIED", "贴附武装使单位获得所选可复制特质", new Dictionary<string,object?> {
            ["sourceObjectId"] = gearId, ["targetObjectId"] = hostId, ["copiedSourceObjectId"] = target.ObjectId,
            ["copiedCardNo"] = traits.CardNo, ["revision"] = revision }));
    }

    private static void EndDetachedUnitCopies(MatchState state, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, List<GameEvent> events)
    {
        foreach (var host in cards.Values.Where(card => card.AttachmentUnitCopies is not null).ToArray()) {
            var ledger = host.AttachmentUnitCopies!;
            var kept = ledger.Effects.Where(copy => AttachmentUnitCopyRules.Active(zones, cards, host, copy)).ToArray();
            if (kept.Length == ledger.Effects.Count) continue;
            var onField = IsObjectOnField(zones, host.ObjectId);
            if (!onField) kept = [];
            var traits = kept.LastOrDefault()?.Traits ?? ledger.Basis;
            var changed = AttachmentUnitCopyRules.Apply(host, traits, state.Tick + 1);
            cards[host.ObjectId] = changed with {
                AttachmentUnitCopies = kept.Length == 0 ? null : new(ledger.Basis, ledger.BasisFaceRevision, ledger.BasisCapturedTick, kept),
                CopyFaceTransitions = onField ? changed.CopyFaceTransitions : null };
            events.Add(new("UNIT_COPY_ENDED", "武装卸除后重算单位可复制特质", new Dictionary<string,object?> {
                ["targetObjectId"] = host.ObjectId, ["cardNo"] = traits.CardNo }));
        }
    }

    private static ResolutionResult EndDetachedUnitCopies(MatchState before, ResolutionResult result)
    {
        if (!result.Accepted || !result.State.CardObjects.Values.Any(card => card.AttachmentUnitCopies is not null)) return result;
        var cards = new Dictionary<string, CardObjectState>(result.State.CardObjects);
        var zones = new Dictionary<string, PlayerZones>(result.State.PlayerZones);
        var events = result.Events.ToList(); EndDetachedUnitCopies(before, zones, cards, events);
        if (cards.All(pair => pair.Value == result.State.CardObjects[pair.Key])) return result;
        var state = result.State with { CardObjects = cards };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }
}
