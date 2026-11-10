using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LinkedAttributeDeclaration(string Attribute, UnitEventContext Ability, long CreatedTick);

public sealed partial class CoreRuleEngine
{
    private static readonly Lazy<IReadOnlySet<string>> OfficialAttributes = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .SelectMany(card => card.Tag.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Concat(card.Hero.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .ToHashSet(StringComparer.Ordinal));

    private const string PlayAttributeDeclarationTiming = "PLAY_DECLARATION";

    private static bool DeclaresAttributeAsPlayed(string cardNo)
        => CardBehaviorRegistry.TryGetByCardNo(cardNo, out var definition) && definition.DeclaresAttributeAsPlayed;

    internal static string LinkedAttributeDeclarationHash(string source, LinkedAttributeDeclaration declaration)
        => MatchStateHasher.HashValue(new { source, declaration });

    private static bool ValidPlayAttributeContext(MatchState state, string source, UnitEventContext context)
        => DeclaresAttributeAsPlayed(context.CardNo) && context.SourceGeneration >= 0 && context.FaceRevision >= 0
            && context.CapturedTick > 0 && context.CapturedTick <= state.Tick
            && MatchStateHasher.HashValue(context) == MatchStateHasher.HashValue(new UnitEventContext(context.CardNo,
                context.SourceGeneration, context.FaceRevision, context.CapturedTick,
                PlayAttributeDeclarationTiming, context.TokenFactoryCardNo))
            && PredictBindingMatches(state, new(source, context.SourceGeneration), context.CardNo,
                context.FaceRevision, context.CapturedTick, context.TokenFactoryCardNo);

    private static string AttributeInstance(string source, UnitEventContext context)
        => context.Origin is { } origin ? $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : $"PRINTED:{source}:{context.SourceGeneration}";

    internal static bool ValidLinkedAttribute(MatchState state, string source, LinkedAttributeDeclaration declaration)
        => declaration is not null && declaration.Ability is { } ability && DeclaresAttributeAsPlayed(ability.CardNo)
            && OfficialAttributes.Value.Contains(declaration.Attribute)
            && declaration.CreatedTick == ability.CapturedTick && declaration.CreatedTick <= state.Tick
            && ValidPlayAttributeContext(state, source, ability);

    internal static bool ValidLinkedAttributes(MatchState state, CardObjectState card)
        => card.LinkedAttributes is null || card.LinkedAttributes.Count > 0
            && IsObjectOnField(state.PlayerZones, card.ObjectId)
            && card.LinkedAttributes.All(d => d?.Ability is { } ability && ability.SourceGeneration == card.ObjectGeneration
                && ValidLinkedAttribute(state, card.ObjectId, d))
            && card.LinkedAttributes.Select(d => AttributeInstance(card.ObjectId, d.Ability)).Distinct().Count() == card.LinkedAttributes.Count;

    private static LinkedAttributeDeclaration? CurrentLinkedAttribute(MatchState state, string source, string? instance = null)
        => state.CardObjects.TryGetValue(source, out var card) ? card.LinkedAttributes?.FirstOrDefault(d =>
            d?.Ability is { } ability && ability.SourceGeneration == card.ObjectGeneration && ValidLinkedAttribute(state, source, d)
            && (instance is null || AttributeInstance(source, d.Ability) == instance)
            && AttachedRuleTextRules.Instances(state, card).Any(text => text.CardNo == d.Ability.CardNo
                && text.Id == AttributeInstance(source, d.Ability)
                && (text.Origin?.FaceRevision ?? card.CopyFaceTransitions?.Count ?? 0) == d.Ability.FaceRevision)) : null;

    private static bool HasAttribute(CardObjectState card, string attribute)
        => card.Tags.Contains(attribute, StringComparer.Ordinal) || card.Tags.Contains("HERO:" + attribute, StringComparer.Ordinal);

    private static bool IsLinkedAttributeTarget(MatchState state, string player, string target, LinkedAttributeDeclaration declaration)
        => state.CardObjects.TryGetValue(target, out var card) && IsFaceUpNonStandbyUnit(card)
            && IsObjectOnField(state.PlayerZones, target) && HasAttribute(card, declaration.Attribute)
            && TargetProtectionRules.IsLegalActivatedSkillTarget(state, player, target);

    // CN355.1: a printed "as you play" declaration is part of the play task.
    // All source entry paths call this before publishing their play observers;
    // it never creates a trigger, target confirmation, or response window.
    private static void DeclarePrintedPlayAttribute(Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, CardBehaviorDefinition behavior, StackItemState item,
        List<GameEvent> events)
    {
        if (!behavior.DeclaresAttributeAsPlayed || item.SourceConfirmed) return;
        if (!IsObjectOnField(zones, item.SourceObjectId))
            throw new InvalidOperationException("Printed play declaration has no entered source.");
        var broker = RuleChoices.Value!;
        var attribute = broker.Choose(item.ControllerId, "宣告一种属性标签。",
            OfficialAttributes.Value.Order(StringComparer.Ordinal).Select(tag => new RuleChoiceOption(tag, tag)).ToArray(),
            kind: "DECLARE_ATTRIBUTE", preview: broker.Origin);
        var source = cards[item.SourceObjectId];
        // Like captured unit-entry observers, bind the incoming field object,
        // before the shared continuity finalizer advances the card's identity.
        var generation = source.ObjectGeneration;
        if (broker.Origin.CardObjects.TryGetValue(source.ObjectId, out var prior)
            && !IsObjectOnField(broker.Origin.PlayerZones, source.ObjectId))
            generation = Math.Max(generation, checked(prior.ObjectGeneration + 1));
        var context = new UnitEventContext(behavior.CardNo, generation,
            source.CopyFaceTransitions?.Count ?? 0, broker.CompletionTick, PlayAttributeDeclarationTiming,
            source.TokenFactoryCardNo);
        var declaration = new LinkedAttributeDeclaration(attribute, context, broker.CompletionTick);
        cards[source.ObjectId] = source with { LinkedAttributes = [declaration] };
        events.Add(new("ATTRIBUTE_DECLARED", "打出时已宣告属性标签", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["sourceGeneration"] = context.SourceGeneration,
            ["faceRevision"] = context.FaceRevision, ["createdTick"] = declaration.CreatedTick,
            ["attribute"] = attribute, ["declarationHash"] = LinkedAttributeDeclarationHash(source.ObjectId, declaration) }));
    }

    private static bool ValidActivationAttribute(MatchState state, SimpleActivationContext context, bool required)
        => required ? context.LinkedAttribute is { } declaration
            && ValidLinkedAttribute(state, context.Source.ObjectId, declaration)
            && declaration.Ability.SourceGeneration == context.Source.Generation
            && declaration.Ability.FaceRevision == context.SourceFaceRevision
            && AttributeInstance(context.Source.ObjectId, declaration.Ability) == context.RuleTextInstanceId
            && declaration.CreatedTick <= context.AcquiredTick
            : context.LinkedAttribute is null;

    private static StackResolutionResult ResolveLinkedAttributeReduction(MatchState state, StackItemState item)
    {
        var declaration = item.SimpleActivation!.LinkedAttribute!;
        var target = item.TargetObjectIds.Single();
        if (!StackObjectRules.ReferencesCurrentObject(state, item, target)
            || !IsLinkedAttributeTarget(state, item.ControllerId, target, declaration)) return NoopStackResolutionResult(state);
        var cards = state.CardObjects.ToDictionary();
        var behavior = new CardBehaviorDefinition(item.CardNo, "关联属性战力技能", 0, item.EffectKind, 0, 1,
            PowerModifierAmount: -2);
        cards[target] = ApplyPowerModifier(state, cards[target], behavior, item, target, -2, out var ev);
        ev = ev with { Payload = new Dictionary<string, object?>(ev.Payload) {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) } };
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [ev] };
    }
}
