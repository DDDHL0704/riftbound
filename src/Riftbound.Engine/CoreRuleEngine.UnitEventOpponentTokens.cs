using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

public sealed partial record UnitEventAbilitySpec
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UnitTokenPlayedByChosenOpponent { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RequiresChosenOpponent { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ReturnAllUnitsMaximumMight { get; init; }
}

public sealed partial record UnitEventContext
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ChosenOpponentId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? OpponentConfirmedAtTick { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string UnitEventOpponentWindow = "UNIT_EVENT_CHOSEN_OPPONENT";
    internal const string ReturnLowMightUnitsEffect = "RETURN_ALL_LOW_MIGHT_UNITS";

    private static bool UnitEventRequiresOpponent(UnitEventAbilitySpec? spec)
        => spec is { RequiresChosenOpponent: true } or { UnitTokenPlayedByChosenOpponent: true };

    private static bool NeedsUnitEventOpponent(StackItemState item)
        => item.UnitEvent is { ChosenOpponentId: null } context
            && UnitEventRequiresOpponent(SelectedUnitEventSpec(context));

    internal static bool ValidUnitEventOpponentChoice(MatchState state, PendingCardChoiceState choice)
    {
        var item = state.StackItems.FirstOrDefault(item => item.StackItemId == choice.ResolvingStackItemId);
        return item is not null && NeedsUnitEventOpponent(item)
            && ValidUnitEvent(state, item.UnitEvent, item.EffectKind, item.SourceObjectId, item.CardNo, item.ControllerId)
            && choice.ChoiceWindow == UnitEventOpponentWindow && choice.ChoiceId == "UNIT-OPPONENT:" + item.StackItemId
            && choice.PlayerId == item.ControllerId && choice.SourceObjectId == item.SourceObjectId
            && choice.EffectKind == item.EffectKind && choice.RequiredCount == 1 && choice.MaxCount == 1
            && choice.LegalObjectIds.SequenceEqual(SeatPlayerIds(state).Where(player => player != item.ControllerId))
            && choice.ContextObjectIds.SequenceEqual([item.SourceObjectId])
            && state.PriorityPlayerId is null && state.ActivePlayerId == item.ControllerId;
    }

    private static bool ValidUnitEventOpponent(MatchState state, UnitEventContext context, UnitEventAbilitySpec spec, string? controller)
        => !UnitEventRequiresOpponent(spec) ? context.ChosenOpponentId is null && context.OpponentConfirmedAtTick is null
            : context.ChosenOpponentId is null ? context.OpponentConfirmedAtTick is null
            : state.Seats.ContainsKey(context.ChosenOpponentId) && context.ChosenOpponentId != controller
                && context.OpponentConfirmedAtTick is { } tick && tick >= context.CapturedTick && tick <= state.Tick;

    internal static string UnitEventOpponentConfirmationHash(StackItemState item)
        => MatchStateHasher.HashValue(new { item.StackItemId, item.ControllerId, item.SourceObjectId,
            Capture = ObservedUnitEventCaptureHash(item.UnitEvent!, item.SourceObjectId, item.ControllerId),
            item.UnitEvent!.ChosenOpponentId, item.UnitEvent.OpponentConfirmedAtTick });

    private static ResolutionResult ResolveUnitEventOpponentChoice(MatchState state, PendingCardChoiceState choice,
        IReadOnlyList<string> selected)
    {
        if (!ValidUnitEventOpponentChoice(state, choice) || selected.Count != 1
            || !choice.LegalObjectIds.Contains(selected[0], StringComparer.Ordinal))
            return RejectWithCorePrompts(state, "独立技能的对手选择与原始技能不匹配。", ErrorCodes.InvalidTarget);
        var item = state.StackItems.Single(item => item.StackItemId == choice.ResolvingStackItemId);
        var confirmed = item with { UnitEvent = item.UnitEvent! with {
            ChosenOpponentId = selected[0], OpponentConfirmedAtTick = state.Tick + 1 } };
        var next = state with { Tick = state.Tick + 1, PendingCardChoice = null,
            StackItems = state.StackItems.Select(stack => stack.StackItemId == item.StackItemId ? confirmed : stack).ToArray(),
            ActivePlayerId = state.StackItems[^1].ControllerId, PriorityPlayerId = state.StackItems[^1].ControllerId,
            PassedPriorityPlayerIds = [] };
        return new(true, null, next, [new("UNIT_EVENT_OPPONENT_CONFIRMED", "已确认独立技能的对手，双方可以响应",
            new Dictionary<string, object?> { ["stackItemId"] = item.StackItemId,
                ["controllerId"] = item.ControllerId, ["opponentId"] = selected[0],
                ["opponentConfirmationHash"] = UnitEventOpponentConfirmationHash(confirmed) })],
            ResolutionResult.BuildSnapshots(next), BuildCorePrompts(next));
    }

    // The player instructed to play a token owns its entry choices and all
    // applicable replacement effects, even when the skill belongs to an opponent.
    private static string TokenEntryPlayer(StackItemState item)
        => item.UnitEvent is { ChosenOpponentId: { } opponent } context
            && SelectedUnitEventSpec(context)?.UnitTokenPlayedByChosenOpponent == true ? opponent : item.ControllerId;
}
