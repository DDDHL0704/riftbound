using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed record DeathRevealContext(string CardNo, string ControllerId, long SourceGeneration);
public sealed record FaceDownLookPermission(string ViewerId, string SubjectId, int TurnNumber, string SourceId, DeathRevealContext Source);

public sealed partial class CoreRuleEngine
{
    internal const string DeathRevealEffect = "LAST_BREATH_REVEAL_HAND_LOOK_FACEDOWN";
    private static readonly RevealedHandChoiceSpec DeathRevealChoiceSpec = new(RevealedHandAction.RevealOnly, Optional: true);
    private static readonly Lazy<IReadOnlyList<string>> DeathRevealSources = new(() =>
        OfficialCardSourceIdentityGroups.BuildByRepresentativeCardNo(["UNL-053/219"])["UNL-053/219"]);

    private static bool IsDeathRevealSource(string? cardNo)
        => DeathRevealSources.Value.Contains(OfficialCardSourceIdentityGroups.NormalizeCardNo(cardNo), StringComparer.Ordinal);

    internal static bool ValidDeathReveal(DeathRevealContext? context, string effect, string controller, string? cardNo = null)
        => context is not null && effect == DeathRevealEffect && context.SourceGeneration >= 0
            && context.ControllerId == controller && IsDeathRevealSource(context.CardNo)
            && (cardNo is null || cardNo == context.CardNo);

    internal static bool TryGetRevealedHandChoiceSpec(StackItemState item, out RevealedHandChoiceSpec spec)
    {
        if (ValidDeathReveal(item.DeathRevealContext, item.EffectKind, item.ControllerId, item.CardNo))
        { spec = DeathRevealChoiceSpec; return true; }
        if (item.DeathRevealContext is null && CardBehaviorRegistry.TryGetByEffectKind(item.EffectKind, out var behavior)
            && behavior.CardNo == item.CardNo && behavior.HandChoice is not null
            && (!behavior.PlaysSourceToBaseAsUnit || item.SourceConfirmed))
        { spec = behavior.HandChoice; return true; }
        spec = null!; return false;
    }

    private static ResolutionResult QueueDeathRevealTriggers(ResolutionResult result)
    {
        if (!result.Accepted || result.State.Status != MatchStatuses.InProgress) return result;
        var queue = result.State.TriggerQueue.ToList(); var events = result.Events.ToList();
        for (var i = 0; i < result.Events.Count; i++)
        {
            var ev = result.Events[i];
            if (ev.Kind != "UNIT_DESTROYED" || !ev.Payload.TryGetValue("deathRevealContext", out var raw)
                || raw is not DeathRevealContext context || !ev.Payload.TryGetValue("targetObjectId", out var target) || target is not string id) continue;
            var trigger = new TriggerQueueItemState($"death-reveal-{result.State.Tick}-{i}-{id}", context.ControllerId,
                id, DeathRevealEffect, "UNIT_DESTROYED") { DeathRevealContext = context };
            queue.Add(trigger); events.Add(BuildTriggerQueuedEvent(trigger));
        }
        if (queue.Count == result.State.TriggerQueue.Count) return result;
        var state = result.State with { TriggerQueue = queue };
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static MatchState GrantFaceDownLook(MatchState state, StackItemState item, string subject, List<GameEvent> events)
    {
        var permission = new FaceDownLookPermission(item.ControllerId, subject, state.TurnNumber, item.SourceObjectId, item.DeathRevealContext!);

        events.Add(new("FACEDOWN_LOOK_GRANTED", "本回合可查看所选对手场上正面朝下的卡牌", new Dictionary<string, object?> {
            ["playerId"] = item.ControllerId, ["subjectPlayerId"] = subject, ["turnNumber"] = state.TurnNumber, ["sourceObjectId"] = item.SourceObjectId }));
        var experience = GainExperience(state.PlayerExperience, item.ControllerId, 1, item, events);
        return state with { PlayerExperience = experience,
            UntilEndOfTurnEffects = MarkPlayersWhoGainedExperienceThisTurn(state.UntilEndOfTurnEffects, events),
            FaceDownLookPermissions = state.FaceDownLookPermissions.Append(permission).Distinct().ToArray() };
    }

    internal static bool CanInspectFaceDown(MatchState state, string objectId, string viewer)
    {
        if (!state.Seats.ContainsKey(viewer) || !state.CardObjects.TryGetValue(objectId, out var card) || !card.IsFaceDown
            || !IsObjectOnField(state.PlayerZones, objectId) || string.IsNullOrWhiteSpace(card.ControllerId)) return false;
        var controller = card.ControllerId;
        return controller == viewer || state.FaceDownLookPermissions.Any(p => p.TurnNumber == state.TurnNumber
            && p.ViewerId == viewer && p.SubjectId == controller);
    }

    internal static bool ValidFaceDownLookPermission(MatchState state, FaceDownLookPermission p)
        => p.TurnNumber == state.TurnNumber && state.Seats.ContainsKey(p.ViewerId) && state.Seats.ContainsKey(p.SubjectId)
            && p.ViewerId != p.SubjectId && !string.IsNullOrWhiteSpace(p.SourceId)
            && ValidDeathReveal(p.Source, DeathRevealEffect, p.ViewerId);
}
