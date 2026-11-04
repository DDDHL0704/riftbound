using System.Text.Json;
using System.Text.Json.Serialization;
using Riftbound.Contracts;

namespace Riftbound.Engine;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActivatedPlayGrant(int TurnNumber, string ControllerId, string CardNo, string StackItemId,
    BorrowedExhaustContext? Borrowed, SimpleActivationContext? Printed)
{
    public string Kind { get; init; } = "NEXT_UNIT_READY";
    public long GrantedTick { get; init; }
    public string? SpellPlayId { get; init; }
}

public sealed partial class CoreRuleEngine
{
    internal const string ActivatedPlayGrantPrefix = "ACTIVATED_NEXT_UNIT_READY:";
    internal const string ActivatedSpellDamageGrantPrefix = "ACTIVATED_NEXT_SPELL_DAMAGE:";
    private const string ActivatedPlayGrantConsumedKey = "activatedPlayGrantsConsumed";

    private static string ActivatedPlayGrantMarker(ActivatedPlayGrant grant)
        => (grant.Kind == "NEXT_SPELL_DAMAGE" ? ActivatedSpellDamageGrantPrefix : ActivatedPlayGrantPrefix)
            + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(grant));

    private static ActivatedPlayGrant? ReadActivatedPlayGrant(string marker)
    {
        var prefix = marker.StartsWith(ActivatedSpellDamageGrantPrefix, StringComparison.Ordinal)
            ? ActivatedSpellDamageGrantPrefix : ActivatedPlayGrantPrefix;
        if (!marker.StartsWith(prefix, StringComparison.Ordinal)) return null;
        try
        {
            var grant = JsonSerializer.Deserialize<ActivatedPlayGrant>(Convert.FromBase64String(marker[prefix.Length..]));
            return grant is not null && ActivatedPlayGrantMarker(grant) == marker ? grant : null;
        }
        catch (Exception error) when (error is JsonException or FormatException or ArgumentException) { return null; }
    }

    private static StackItemState? ActivatedPlayGrantItem(ActivatedPlayGrant grant)
    {
        if (grant.Borrowed is { Borrower.Object.ObjectId.Length: > 0, Borrower.CardNo.Length: > 0,
                Borrower.PhysicalCardNo.Length: > 0, Donor.CardNo.Length: > 0 } borrowed && grant.Printed is null)
            return new StackItemState(grant.StackItemId, grant.ControllerId, borrowed.Borrower.Object.ObjectId,
                BorrowedExhaustRules.Effect, grant.CardNo, [], timingContext: TimingStates.NeutralOpen) {
                    BorrowedExhaust = borrowed, TargetGenerations = new Dictionary<string, long>() };
        if (grant.Borrowed is null && grant.Printed is { Source.ObjectId.Length: > 0 } printed
            && P4ActivatedAbilityCatalog.TryGetByAbilityId(printed.AbilityId, out var ability)
            && (grant.Kind == "NEXT_UNIT_READY" ? ability.SimpleEffect?.GrantsNextUnitReady == true
                : grant.Kind == "NEXT_SPELL_DAMAGE" && ability.SimpleEffect?.GrantsNextSpellDamage == true))
        {
            var item = SimpleActivationStackItem(printed, grant.ControllerId, grant.CardNo, ability, []);
            return item.StackItemId == grant.StackItemId ? item : null;
        }
        return null;
    }

    private static bool ValidActivatedPlayGrant(MatchState state, ActivatedPlayGrant grant)
    {
        if (grant.Kind is not ("NEXT_UNIT_READY" or "NEXT_SPELL_DAMAGE")
            || grant.Kind == "NEXT_UNIT_READY" && (grant.GrantedTick != 0 || grant.SpellPlayId is not null)
            || grant.Kind == "NEXT_SPELL_DAMAGE" && !ValidActivatedSpellDamageBinding(state, grant)
            || grant.TurnNumber != state.TurnNumber || string.IsNullOrWhiteSpace(grant.ControllerId)
            || string.IsNullOrWhiteSpace(grant.CardNo) || !state.Seats.ContainsKey(grant.ControllerId)
            || ActivatedPlayGrantItem(grant) is not { } item) return false;
        if (item.BorrowedExhaust is { } borrowed)
            return item.StackItemId == $"STACK-{borrowed.AcquiredTick}-{item.SourceObjectId}-BORROWED"
                && BorrowedExhaustRules.Spec(borrowed.Donor.CardNo, borrowed.AbilityIndex)?.Kind == (grant.Kind == "NEXT_UNIT_READY" ? "GRANT_NEXT_UNIT_READY" : "GRANT_NEXT_SPELL_DAMAGE")
                && BorrowedExhaustRules.Valid(state, item);
        return ValidSimpleActivation(state, item);
    }

    internal static bool ValidActivatedPlayGrants(MatchState state)
    {
        var markers = (state.UntilEndOfTurnEffects ?? Array.Empty<string>())
            .Where(m => m is not null && (m.StartsWith(ActivatedPlayGrantPrefix, StringComparison.Ordinal)
                || m.StartsWith(ActivatedSpellDamageGrantPrefix, StringComparison.Ordinal))).ToArray();
        return markers.Distinct(StringComparer.Ordinal).Count() == markers.Length
            && markers.All(m => ReadActivatedPlayGrant(m) is { } grant && ValidActivatedPlayGrant(state, grant))
            && markers.Select(m => ReadActivatedPlayGrant(m)!.StackItemId).Distinct(StringComparer.Ordinal).Count() == markers.Length;
    }

    private static StackResolutionResult ResolveActivatedNextUnitReady(MatchState state, StackItemState item)
    {
        // Encourage belongs to the effect, so the skill may be paid for even
        // when it will do nothing. Responses can satisfy it before resolution.
        if (!BorrowedEncourageActive(state, item.ControllerId, item.SourceObjectId)) return NoopStackResolutionResult(state);
        var grant = new ActivatedPlayGrant(state.TurnNumber, item.ControllerId, item.CardNo!, item.StackItemId,
            item.BorrowedExhaust, item.SimpleActivation);
        if (!ValidActivatedPlayGrant(state, grant)) throw new InvalidOperationException("Invalid activated play grant source.");
        return NoopStackResolutionResult(state) with {
            UntilEndOfTurnEffects = AddUntilEndOfTurnEffect(state.UntilEndOfTurnEffects, ActivatedPlayGrantMarker(grant)),
            Events = [new("NEXT_UNIT_READY_GRANTED", "本回合打出的下一名单位以活跃状态进场",
                new Dictionary<string, object?> { ["playerId"] = item.ControllerId,
                    ["sourceObjectId"] = ResolutionResult.VisibleObjectIdForViewer(state, item.SourceObjectId, ProjectionViewers.Spectator) })] };
    }

    private static GameEvent CaptureActivatedNextUnitReady(GameEvent ev, Dictionary<string, CardObjectState> cards,
        CardObjectState unit, string player, IReadOnlyList<string> effects)
    {
        if (ev.Kind is not ("UNIT_PLAYED_TO_BASE" or "UNIT_PLAYED_TO_BATTLEFIELD" or "UNIT_TOKEN_CREATED")) return ev;
        var grants = effects.Where(m => ReadActivatedPlayGrant(m) is { Kind: "NEXT_UNIT_READY" } grant && grant.ControllerId == player
            && RuleChoices.Value?.ConsumedNextUnitPlayGrants.Contains(m) != true).ToArray();
        if (grants.Length == 0) return ev;
        RuleChoices.Value?.ConsumedNextUnitPlayGrants.UnionWith(grants);
        cards[unit.ObjectId] = cards[unit.ObjectId] with { IsExhausted = false };
        return ev with { Payload = new Dictionary<string, object?>(ev.Payload) { ["isExhausted"] = false,
            ["activatedNextUnitReady"] = true,
            [ActivatedPlayGrantConsumedKey] = grants.Select(m => ReadActivatedPlayGrant(m)!.StackItemId).ToArray() } };
    }

    private static ResolutionResult RecordActivatedNextUnitReady(ResolutionResult result)
    {
        var consumed = result.Events.SelectMany(e => e.Payload.GetValueOrDefault(ActivatedPlayGrantConsumedKey) as string[] ?? [])
            .ToHashSet(StringComparer.Ordinal);
        if (consumed.Count == 0) return result;
        // Departure receipts can update a marker during this instruction. Its
        // activation identity stays stable, so a consumed grant cannot return.
        var state = result.State with { UntilEndOfTurnEffects = result.State.UntilEndOfTurnEffects
            .Where(m => ReadActivatedPlayGrant(m) is not { } grant || !consumed.Contains(grant.StackItemId)).ToArray() };
        var events = result.Events.Select(e => e.Payload.ContainsKey(ActivatedPlayGrantConsumedKey)
            ? e with { Payload = e.Payload.Where(p => p.Key != ActivatedPlayGrantConsumedKey).ToDictionary(p => p.Key, p => p.Value) } : e).ToArray();
        return result with { State = state, Events = events, Snapshots = ResolutionResult.BuildSnapshots(state), Prompts = BuildCorePrompts(state) };
    }

    private static MatchState RecordCeasedActivatedPlayGrantSources(MatchState before, MatchState state, IReadOnlySet<string> departed)
    {
        var effects = state.UntilEndOfTurnEffects.Select(marker => {
            if (ReadActivatedPlayGrant(marker) is not { } grant
                || !ValidActivatedPlayGrant(before with { Tick = state.Tick }, grant)) return marker;
            if (grant.Borrowed is { } borrowed)
            {
                BorrowedTextSource Capture(BorrowedTextSource text) => text.CeasedSource is null && departed.Contains(text.Object.ObjectId)
                    ? text with { CeasedSource = SelfAbilitySourceRules.Capture(before, state.Tick, text.Object,
                        text.PhysicalCardNo, text.FaceRevision, borrowed.AcquiredTick, text.TokenFactoryCardNo) } : text;
                return ActivatedPlayGrantMarker(grant with { Borrowed = borrowed with {
                    Borrower = Capture(borrowed.Borrower), Donor = Capture(borrowed.Donor) } });
            }
            if (grant.Printed is { CeasedSource: null, SourceTokenFactoryCardNo: not null } printed
                && departed.Contains(printed.Source.ObjectId))
                return ActivatedPlayGrantMarker(grant with { Printed = printed with {
                    CeasedSource = new(printed.SourceTokenFactoryCardNo, state.Tick) { SourceFaceRevision = printed.SourceFaceRevision } } });
            return marker;
        }).ToArray();
        return state with { UntilEndOfTurnEffects = effects };
    }
}
