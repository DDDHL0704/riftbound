using System.Text.Json.Serialization;
using Riftbound.CardCatalog;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceReadiedContext(string CardNo, ObjectBinding Source, long CapturedTick, long FaceRevision,
    RuleTextInstanceBinding? Origin = null, string? SourceTokenFactoryCardNo = null, string? Mode = null);

public sealed partial class CoreRuleEngine
{
    internal const string SourceReadiedEffect = "SOURCE_READIED_CHOOSE_TURN_KEYWORD";
    internal const string ReadiedModeWindow = "READIED_MODE_CONFIRMATION";
    private const string ReadiedObservationKey = "sourceReadiedObservations";
    private sealed record ReadiedObservation(string ControllerId, SourceReadiedContext Context);
    private static readonly Lazy<IReadOnlyDictionary<string, TriggerSpec>> SourceReadiedSpecs = new(() =>
        OfficialCardCatalog.LoadRuntimeAsync().GetAwaiter().GetResult().Cards
            .Select(c => (c.CardNo, Spec: RuleTextParser.Parse(c).Triggers.FirstOrDefault(t => t.Kind == TriggerKinds.SourceReadiedChooseKeyword)))
            .Where(c => c.Spec is not null).ToDictionary(c => c.CardNo, c => c.Spec!, StringComparer.Ordinal));

    // Capture each ready instruction before later instructions can move or copy its source.
    private static ReadiedObservation[] CaptureReadiedObject(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState prior)
        => !prior.IsExhausted || prior.IsFaceDown || !IsObjectOnField(zones, prior.ObjectId)
            ? [] : AttachedRuleTextRules.Instances(zones, cards, prior)
                .Where(i => SourceReadiedSpecs.Value.ContainsKey(i.CardNo))
                .Select(i => new ReadiedObservation(EffectiveFieldControllerId(zones, prior.ObjectId, prior)!,
                    new(i.CardNo, new(prior.ObjectId, prior.ObjectGeneration), 0, prior.CopyFaceTransitions?.Count ?? 0,
                        i.Origin, prior.TokenFactoryCardNo))).ToArray();

    private static void AddReadiedObservation(IReadOnlyDictionary<string, PlayerZones> zones,
        IReadOnlyDictionary<string, CardObjectState> cards, CardObjectState prior, List<GameEvent> events)
    {
        var captured = CaptureReadiedObject(zones, cards, prior);
        if (captured.Length > 0) events.Add(new("INTERNAL_SOURCE_READIED", "记录活跃指令", new Dictionary<string, object?> {
            [ReadiedObservationKey] = captured }));
    }

    private static ResolutionResult QueueSourceReadied(ResolutionResult result)
    {
        if (!result.Accepted) return result;
        var events = new List<GameEvent>(); var queue = result.State.TriggerQueue.ToList();
        foreach (var (ev, index) in result.Events.Select((e, i) => (e, i)))
        {
            if (ev.Payload.GetValueOrDefault(ReadiedObservationKey) is not ReadiedObservation[] captured) { events.Add(ev); continue; }
            if (ev.Kind != "INTERNAL_SOURCE_READIED") events.Add(ev with { Payload = ev.Payload
                .Where(p => p.Key != ReadiedObservationKey).ToDictionary(p => p.Key, p => p.Value) });
            foreach (var (observation, instance) in captured.Select((c, i) => (c, i)))
            {
                var context = observation.Context with { CapturedTick = result.State.Tick };
                var trigger = new TriggerQueueItemState($"readied-{result.State.Tick}-{index}-{instance}", observation.ControllerId,
                    context.Source.ObjectId, SourceReadiedEffect, "UNIT_READY") {
                        SourceCardNo = context.CardNo, SourceReadied = context };
                queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
            }
        }
        return result with { State = result.State with { TriggerQueue = queue }, Events = events };
    }

    internal static bool ValidSourceReadied(MatchState state, SourceReadiedContext? context, string effect,
        string source, string controller, string? no)
    {
        if (context is null) return effect != SourceReadiedEffect;
        if (effect != SourceReadiedEffect || no != context.CardNo || !state.Seats.ContainsKey(controller)
            || !SourceReadiedSpecs.Value.TryGetValue(context.CardNo, out var spec)
            || context.Source is not { Generation: >= 0 } || context.Source.ObjectId != source
            || context.CapturedTick <= 0 || context.CapturedTick > state.Tick || context.FaceRevision < 0
            || context.Mode is not null && !spec.Modes!.Any(m => m.Id == context.Mode)) return false;
        return context.Origin is { } origin
            ? AttachedRuleTextRules.ValidCaptured(state, origin, source, context.Source.Generation, context.CardNo)
            : PredictBindingMatches(state, context.Source, context.CardNo, context.FaceRevision, context.CapturedTick, context.SourceTokenFactoryCardNo);
    }

    internal static bool ValidSourceReadiedStack(MatchState state, StackItemState item)
        => ValidSourceReadied(state, item.SourceReadied, item.EffectKind, item.SourceObjectId, item.ControllerId, item.CardNo)
            && (item.SourceReadied is null || !item.SourceConfirmed && item.TargetObjectIds.Count == 0 && item.TargetGenerations is null
                && item.OptionalCosts.Count == 0 && item.DamageAmount == 0 && item.EffectRepeatCount == 1);

    private static PendingCardChoiceState ReadiedModeChoice(StackItemState item)
        => new("READIED-MODE:" + item.StackItemId, ReadiedModeWindow, item.ControllerId, 1, 1,
            SourceReadiedSpecs.Value[item.CardNo].Modes!.Select(m => m.Id).ToArray(), [item.SourceObjectId],
            "选择来源本回合获得的关键词；确认后双方可以响应。", item.SourceObjectId, item.EffectKind) { ResolvingStackItemId = item.StackItemId };

    private static ResolutionResult PrepareReadiedMode(ResolutionResult result, StackItemState item)
    {
        var state = result.State with { PendingCardChoice = ReadiedModeChoice(item), ActivePlayerId = item.ControllerId, PriorityPlayerId = null };
        return result with { State = state, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    internal static bool ValidReadiedModeChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(i => i.StackItemId == choice.ResolvingStackItemId);
        if (item?.SourceReadied is not { Mode: null } || !ValidSourceReadiedStack(state, item)) return false;
        var expected = ReadiedModeChoice(item);
        return choice.ChoiceId == expected.ChoiceId && choice.ChoiceWindow == expected.ChoiceWindow && choice.PlayerId == expected.PlayerId
            && choice.SourceObjectId == expected.SourceObjectId && choice.EffectKind == expected.EffectKind
            && choice.RequiredCount == 1 && choice.MaxCount == 1 && choice.LegalObjectIds.SequenceEqual(expected.LegalObjectIds)
            && choice.ContextObjectIds.SequenceEqual(expected.ContextObjectIds);
    }

    internal static string ReadiedModeLabel(string id) => id switch { "ASSAULT_2" => "本回合获得强攻2", "WARD_2" => "本回合获得法盾2", "ROAM" => "本回合获得游走", _ => id };

    private static ResolutionResult ResolveReadiedMode(MatchState state, PendingCardChoiceState choice, IReadOnlyList<string> selected)
    {
        if (!ValidReadiedModeChoice(state, choice)) return RejectWithCorePrompts(state, "活跃技能模式已失效。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(i => i.StackItemId == choice.ResolvingStackItemId);
        var changed = item with { SourceReadied = item.SourceReadied! with { Mode = selected.Single() } };
        var next = RestoreAfterTriggerConfirmation(state with { Tick = state.Tick + 1, PendingCardChoice = null,
            StackItems = state.StackItems.Select(i => i.StackItemId == item.StackItemId ? changed : i).ToArray() }, item);
        return PrepareTriggerConfirmation(new(true, null, next, [new("TRIGGER_MODE_CONFIRMED", "已确认活跃技能模式", new Dictionary<string, object?> {
            ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(next, item.SourceObjectId, "__spectator__"),
            ["mode"] = selected.Single() })], ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next)));
    }

    private static StackResolutionResult ResolveSourceReadied(MatchState state, StackItemState item)
    {
        if (!ValidSourceReadiedStack(state, item) || item.SourceReadied is not { Mode: { } mode } context)
            throw new InvalidOperationException("Invalid source readied mode.");
        if (!state.CardObjects.TryGetValue(item.SourceObjectId, out var source) || source.ObjectGeneration != context.Source.Generation
            || source.IsFaceDown || !IsObjectOnField(state.PlayerZones, source.ObjectId)) return NoopStackResolutionResult(state);
        var keyword = SourceReadiedSpecs.Value[context.CardNo].Modes!.Single(m => m.Id == mode).GrantedKeyword!;
        var effect = keyword == CardCombatKeywordNames.Roam ? MoveUnitRoamOptionalCost
            : TemporaryCombatKeywordRules.AppliedEffectId(source, TemporaryCombatKeywordRules.Prefix + keyword);
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        cards[source.ObjectId] = source with { UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(source.UntilEndOfTurnEffects, effect) };
        return NoopStackResolutionResult(state) with { CardObjects = cards, Events = [new("STATUS_EFFECT_APPLIED", "活跃技能给予本回合关键词", new Dictionary<string, object?> {
            ["sourceObjectId"] = source.ObjectId, ["targetObjectId"] = source.ObjectId, ["effectId"] = effect, ["keyword"] = keyword })] };
    }
}
