using System.Runtime.CompilerServices;
using System.Text.Json;
using Riftbound.Contracts;

namespace Riftbound.Engine;

// These are confirmed resource-child operations and completed instruction
// payments, not authorization to install an outer-action preview. Consumers
// apply only their differences, including the debit before a later resource gain.
internal sealed record RuleChoiceResourceStep(string PlayerId, string ChoiceId,
    JsonElement Before, JsonElement After, IReadOnlyList<GameEvent> Events);

public sealed partial class CoreRuleEngine
{
    private static readonly JsonSerializerOptions ResourceStepJson = new(JsonSerializerDefaults.Web);
    private static readonly ConditionalWeakTable<RuleChoiceFrame, List<RuleChoiceResourceStep>> ResourceSteps = new();
    private sealed record ResourceStepCapture(RuleChoiceFrame Frame, string PlayerId, string ChoiceId, JsonElement Before);

    private static ResourceStepCapture? BeginRuleChoiceResourceStep(MatchState before, string player, string choice)
        => RuleChoices.Value is { } frame ? new(frame, player, choice,
            JsonSerializer.SerializeToElement(before, ResourceStepJson)) : null;

    private static void CompleteRuleChoiceResourceStep(ResourceStepCapture? capture, MatchState after,
        IReadOnlyList<GameEvent> events)
    {
        if (capture is null) return;
        ResourceSteps.GetOrCreateValue(capture.Frame).Add(new(capture.PlayerId, capture.ChoiceId, capture.Before,
            JsonSerializer.SerializeToElement(after, ResourceStepJson), events.ToArray()));
    }

    private static IReadOnlyList<RuleChoiceResourceStep> GetCompletedRuleChoiceResources(RuleChoiceFrame frame)
        => ResourceSteps.TryGetValue(frame, out var steps) ? steps.ToArray() : [];

    // Re-derive the ledger from the original command/answers, never from a saved
    // ledger or displayed preview. ResolveAsync/ValidRuleChoice own its use.
    internal static bool TryReplayRuleChoiceResources(RuleChoiceContinuation pending,
        out IReadOnlyList<RuleChoiceResourceStep> steps)
    {
        steps = [];
        if (pending.Origin is null || pending.Intent is null || pending.Answers is null || pending.Request is null
            || pending.Command.ValueKind != JsonValueKind.Object) return false;
        var frame = new RuleChoiceFrame(pending.Origin, pending.Answers);
        var previous = RuleChoices.Value; RuleChoices.Value = frame;
        try
        {
            _ = new CoreRuleEngine().ResolveCoreAsync(pending.Origin, pending.Intent,
                GameCommandJsonMapper.Map(pending.Command), default).GetAwaiter().GetResult();
            return false;
        }
        catch (RuleChoiceRequired required)
        {
            if (MatchStateHasher.HashValue(required.Request) != MatchStateHasher.HashValue(pending.Request)) return false;
            steps = GetCompletedRuleChoiceResources(frame); return true;
        }
        catch (Exception failure) when (failure is InvalidOperationException or ArgumentException or JsonException or KeyNotFoundException)
        { return false; }
        finally { RuleChoices.Value = previous; }
    }

    private static void ApplyRuleChoiceTapRuneResource(MatchState context, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player,
        string rune, List<GameEvent> events)
    {
        var capture = BeginRuleChoiceResourceStep(context, player, "TAP_RUNE:" + rune);
        var result = ResolveTapRune(context, new("rule-choice-resource", player, CommandTypes.TapRune), new(rune));
        if (!result.Accepted) throw new InvalidOperationException("Invalid generated rune resource: " + result.ErrorMessage);
        ReplaceResourceValues(zones, result.State.PlayerZones);
        ReplaceResourceValues(cards, result.State.CardObjects);
        ReplaceResourceValues(pools, result.State.RunePools);
        events.AddRange(result.Events);
        CompleteRuleChoiceResourceStep(capture, result.State, result.Events);
    }

    private static void ApplyRuleChoiceRecycleRuneResource(MatchState context, Dictionary<string, PlayerZones> zones,
        Dictionary<string, CardObjectState> cards, Dictionary<string, RunePool> pools, string player,
        string rune, List<GameEvent> events, string paymentWindow)
    {
        var capture = BeginRuleChoiceResourceStep(context, player, "RECYCLE_RUNE:" + rune);
        var eventStart = events.Count;
        var locations = context.ObjectLocations.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var paid = ApplyRecycleRunePaymentResourceActions(pools, zones, cards, locations, player, [rune], events, paymentWindow);
        ReplaceResourceValues(pools, paid);
        var after = context with { PlayerZones = zones, CardObjects = cards, RunePools = pools, ObjectLocations = locations };
        CompleteRuleChoiceResourceStep(capture, after, events.Skip(eventStart).ToArray());
    }

    private static void ReplaceResourceValues<T>(Dictionary<string, T> target, IReadOnlyDictionary<string, T> source)
    {
        if (ReferenceEquals(target, source)) return;
        target.Clear(); foreach (var pair in source) target[pair.Key] = pair.Value;
    }
}
