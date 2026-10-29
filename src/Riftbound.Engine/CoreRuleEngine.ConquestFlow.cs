using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConquestFlowContext(string CardNo, long SourceGeneration, string BattlefieldId,
    long CapturedTick, long FaceRevision, string? TokenFactoryCardNo = null, RuleTextInstanceBinding? Origin = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TurnFlowGrant(int TurnNumber, string ControllerId, ObjectBinding Source,
    ConquestFlowContext Skill, ObjectBinding Spell, string CardNo, string StackItemId);

public sealed partial class CoreRuleEngine
{
    internal const string ConquestFlowEffect = "UNIT_CONQUEST_GRANT_PRINTED_COST_FLOW";
    internal const string TurnFlowGrantPrefix = "TURN_FLOW_GRANT|";

    private static bool HasConquestFlow(string? no)
        => CardBehaviorRegistry.TryGetByCardNo(no ?? "", out var b) && b.GrantsPrintedCostFlowOnConquest;

    private static FlowCostSpec PrintedFlowCost(string no)
    {
        CardBehaviorRegistry.TryGetByCardNo(no, out var b);
        var power = PrintedPowerCostRules.ForCard(no);
        return new(b.ManaCost, power.Amount, power.Amount == 0 ? [] : power.Traits);
    }

    private static string FlowGrantMarker(TurnFlowGrant grant)
        => TurnFlowGrantPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(grant)));

    private static TurnFlowGrant? ReadFlowGrant(string marker)
    {
        try
        {
            if (!marker.StartsWith(TurnFlowGrantPrefix, StringComparison.Ordinal)) return null;
            var grant = JsonSerializer.Deserialize<TurnFlowGrant>(Convert.FromBase64String(marker[TurnFlowGrantPrefix.Length..]));
            return grant is not null && FlowGrantMarker(grant) == marker ? grant : null;
        }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { return null; }
    }

    internal static bool ValidConquestFlow(MatchState state, ConquestFlowContext? c, string effect, string source,
        string? no = null)
        => c is null ? effect != ConquestFlowEffect : effect == ConquestFlowEffect
            && HasConquestFlow(c.CardNo) && (no is null || c.CardNo == no)
            && c.SourceGeneration >= 0 && c.CapturedTick > 0 && c.CapturedTick <= state.Tick && c.FaceRevision >= 0
            && !string.IsNullOrWhiteSpace(c.BattlefieldId) && state.BattlefieldStates.ContainsKey(c.BattlefieldId)
            && (c.Origin is { } origin
                ? AttachedRuleTextRules.ValidCaptured(state, origin, source, c.SourceGeneration, c.CardNo)
                : PredictBindingMatches(state, new(source, c.SourceGeneration), c.CardNo, c.FaceRevision, c.CapturedTick, c.TokenFactoryCardNo));

    private static bool ValidFlowGrant(MatchState state, TurnFlowGrant g)
        => g.TurnNumber == state.TurnNumber && !string.IsNullOrWhiteSpace(g.ControllerId) && state.Seats.ContainsKey(g.ControllerId)
            && g.Source is { Generation: >= 0 } && g.Skill is not null && g.Source.Generation == g.Skill.SourceGeneration
            && ValidConquestFlow(state, g.Skill, ConquestFlowEffect, g.Source.ObjectId)
            && g.Spell is { Generation: >= 0 } && !string.IsNullOrWhiteSpace(g.Spell.ObjectId)
            && g.Source.ObjectId != g.Spell.ObjectId && !string.IsNullOrWhiteSpace(g.StackItemId)
            && g.StackItemId.StartsWith($"ordered-conquest-flow-{g.Skill.CapturedTick}-{g.Skill.BattlefieldId}-{g.Source.ObjectId}-", StringComparison.Ordinal)
            && state.CardObjects.TryGetValue(g.Spell.ObjectId, out var spell) && spell.ObjectGeneration >= g.Spell.Generation
            && spell.OwnerId == g.ControllerId
            && (spell.ObjectGeneration != g.Spell.Generation || spell.CardNo == g.CardNo)
            && CardBehaviorRegistry.TryGetByCardNo(g.CardNo ?? "", out var b) && IsSpellPlayBehavior(b);

    internal static bool ValidTurnFlowGrants(MatchState state)
    {
        if (state.UntilEndOfTurnEffects is null) return false;
        var markers = state.UntilEndOfTurnEffects.Where(e => e?.StartsWith(TurnFlowGrantPrefix, StringComparison.Ordinal) == true).ToArray();
        return markers.Distinct(StringComparer.Ordinal).Count() == markers.Length
            && markers.All(m => ReadFlowGrant(m) is { } grant && ValidFlowGrant(state, grant));
    }

    private static IEnumerable<TurnFlowGrant> CurrentFlowGrants(MatchState state, string player, string source)
        => state.UntilEndOfTurnEffects.Where(e => e.StartsWith(TurnFlowGrantPrefix, StringComparison.Ordinal))
            .Select(ReadFlowGrant).OfType<TurnFlowGrant>().Where(g => g.ControllerId == player && g.Spell.ObjectId == source
                && g.TurnNumber == state.TurnNumber && state.CardObjects.TryGetValue(source, out var spell)
                && spell.ObjectGeneration == g.Spell.Generation && spell.CardNo == g.CardNo);

    private static IEnumerable<TriggerQueueItemState> CaptureConquestFlow(MatchState state, string player,
        string battlefield, IReadOnlyList<string> units, long tick, string eventKind = "BATTLEFIELD_CONQUERED")
    {
        foreach (var id in units.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (!state.CardObjects.TryGetValue(id, out var source) || source.ControllerId != player || source.IsFaceDown
                || !source.Tags.Contains(CardObjectTags.UnitCard) || BattlefieldLocalRules.AtUnit(state, id)?.ObjectId != battlefield) continue;
            foreach (var instance in AttachedRuleTextRules.Instances(state, source).Where(i => HasConquestFlow(i.CardNo)))
            {
                var repeats = ResolveUnitConquestEffectRepeatCount(state.PlayerZones, state.CardObjects, state.ObjectLocations,
                    player, battlefield, eventKind);
                for (var i = 0; i < repeats; i++)
                    yield return new TriggerQueueItemState($"conquest-flow-{tick}-{battlefield}-{id}-{i}-{instance.Id}", player, id,
                        ConquestFlowEffect, eventKind) { SourceCardNo = instance.CardNo,
                        ConquestFlow = new(instance.CardNo, source.ObjectGeneration, battlefield, tick,
                            source.CopyFaceTransitions?.Count ?? 0, source.TokenFactoryCardNo, instance.Origin) };
            }
        }
    }

    private static string[] ConquestFlowTargets(MatchState state, string player)
        => state.PlayerZones[player].Graveyard.Where(id => state.CardObjects.TryGetValue(id, out var c)
            && c.OwnerId == player && c.ControllerId == player && !c.IsFaceDown && c.Tags.Contains(CardObjectTags.SpellCard)
            && CardBehaviorRegistry.TryGetByCardNo(c.CardNo ?? "", out var b) && IsSpellPlayBehavior(b))
            .Order(StringComparer.Ordinal).ToArray();

    private static PendingCardChoiceState ConquestFlowChoice(MatchState state, StackItemState item)
        => new("CONQUEST-FLOW-TARGET:" + item.StackItemId, "TRIGGER_CONFIRMATION", item.ControllerId, 1, 1,
            ConquestFlowTargets(state, item.ControllerId), [item.ConquestFlow!.BattlefieldId],
            "选择你废牌堆中的一个法术，本回合获得等同于其费用的流转；确认后双方可以响应。",
            item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareConquestFlowTarget(ResolutionResult result, StackItemState item)
    {
        var choice = ConquestFlowChoice(result.State, item);
        if (choice.LegalObjectIds.Count == 0) return PrepareTriggerConfirmation(DiscardUnconfirmedTrigger(result, item));
        var state = result.State with { PendingCardChoice = choice, ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static bool ValidConquestFlowChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.ConquestFlow is null || item.TargetGenerations is not null
            || !ValidConquestFlow(state, item.ConquestFlow, item.EffectKind, item.SourceObjectId, item.CardNo)) return false;
        var expected = ConquestFlowChoice(state, item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.LegalObjectIds.Count > 0
            && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds) && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static bool ValidConquestFlowStack(MatchState state, StackItemState item)
        => ValidConquestFlow(state, item.ConquestFlow, item.EffectKind, item.SourceObjectId, item.CardNo)
            && (item.ConquestFlow is null || !item.SourceConfirmed && item.PlayCost is null && (item.TargetGenerations is { } generations
                ? item.TargetObjectIds.Count == 1 && generations.Count == 1
                    && generations.TryGetValue(item.TargetObjectIds[0], out var generation) && generation >= 0
                    && state.CardObjects.TryGetValue(item.TargetObjectIds[0], out var target) && generation <= target.ObjectGeneration
                : item.TargetObjectIds.Count == 0 && state.PriorityPlayerId is null
                    && (state.PendingCardChoice is not null || state.TriggerQueue.Count > 0)));

    private static StackResolutionResult ResolveConquestFlow(MatchState state, StackItemState item)
    {
        if (item.TargetObjectIds.SingleOrDefault() is not { } target || !ConquestFlowTargets(state, item.ControllerId).Contains(target)
            || item.TargetGenerations?.GetValueOrDefault(target, -1) != state.CardObjects[target].ObjectGeneration)
            return NoopStackResolutionResult(state);
        var grant = new TurnFlowGrant(state.TurnNumber, item.ControllerId, new(item.SourceObjectId, item.ConquestFlow!.SourceGeneration),
            item.ConquestFlow, new(target, state.CardObjects[target].ObjectGeneration), state.CardObjects[target].CardNo!, item.StackItemId);
        var marker = FlowGrantMarker(grant);
        return NoopStackResolutionResult(state) with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, marker),
            Events = [new("FLOW_GRANTED", "法术在本回合内获得等同于其费用的流转", new Dictionary<string, object?> {
                ["controllerId"] = item.ControllerId, ["sourceObjectId"] = item.SourceObjectId, ["targetObjectId"] = target,
                ["cost"] = PrintedFlowCost(grant.CardNo), ["duration"] = "UNTIL_END_OF_TURN" })] };
    }
}
