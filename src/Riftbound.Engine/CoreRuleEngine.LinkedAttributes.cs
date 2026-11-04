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

    private static string AttributeInstance(string source, UnitEventContext context)
        => context.Origin is { } origin ? $"ATTACHED:{origin.Equipment.ObjectId}:{origin.Equipment.Generation}:{origin.Revision}"
            : $"PRINTED:{source}:{context.SourceGeneration}";

    internal static bool ValidLinkedAttribute(MatchState state, string source, LinkedAttributeDeclaration declaration)
        => declaration is not null && declaration.Ability is { } ability && UnitEventSpec(ability.CardNo)?.Effect == "DECLARE_ATTRIBUTE"
            && OfficialAttributes.Value.Contains(declaration.Attribute)
            && declaration.CreatedTick >= ability.CapturedTick && declaration.CreatedTick <= state.Tick
            && ValidUnitEvent(state, ability, UnitEventEffect, source, ability.CardNo);

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

    private static StackResolutionResult ResolveAttributeDeclaration(MatchState state, StackItemState item)
    {
        var context = item.UnitEvent!;
        var attribute = RuleChoices.Value!.Choose(item.ControllerId, "宣告一种属性标签。",
            OfficialAttributes.Value.Order(StringComparer.Ordinal).Select(tag => new RuleChoiceOption(tag, tag)).ToArray(),
            kind: "DECLARE_ATTRIBUTE", preview: state);
        var cards = state.CardObjects.ToDictionary();
        if (cards.TryGetValue(item.SourceObjectId, out var source) && source.ObjectGeneration == context.SourceGeneration
            && IsObjectOnField(state.PlayerZones, source.ObjectId))
        {
            var instance = AttributeInstance(source.ObjectId, context);
            cards[source.ObjectId] = source with { LinkedAttributes = (source.LinkedAttributes ?? [])
                .Where(d => AttributeInstance(source.ObjectId, d.Ability) != instance)
                .Append(new(attribute, context, RuleChoices.Value.CompletionTick)).ToArray() };
        }
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [
            new("ATTRIBUTE_DECLARED", "已宣告属性标签", new Dictionary<string, object?> {
                ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator),
                ["attribute"] = attribute })] };
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
