using System.Text;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Older lifecycle fixtures start with combatants already placed on the field.
// Reconstruct their initial duel checkpoint, then submit actual focus and
// combat commands. Production movement is covered separately by BattleStartRoleTests.
internal static class BattleFocusTestDriver
{
    private const string Prefix = "BATTLE_RESPONSE_DECLARATION_CONTEXT:";

    internal static async Task<ResolutionResult> OpenAsync(CoreRuleEngine engine, MatchState state,
        PlayerIntent intent, DeclareBattleCommand command, CancellationToken cancellationToken)
    {
        var field = command.BattlefieldId!;
        var units = state.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.UnitCard)
            && !c.IsFaceDown && !c.Tags.Contains(CardObjectTags.Standby)
            && state.PlayerZones.Values.Any(z => z.Battlefields.Contains(c.ObjectId))
            && state.ObjectLocations.TryGetValue(c.ObjectId, out var location)
            && location.Zone == "BATTLEFIELD" && location.BattlefieldObjectId == field).ToArray();
        var attackers = units.Where(c => (c.ControllerId ?? c.OwnerId) == intent.PlayerId).Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        var defenders = units.Where(c => (c.ControllerId ?? c.OwnerId) != intent.PlayerId).Select(c => c.ObjectId).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(attackers); Assert.NotEmpty(defenders);
        var context = new {
            BattlefieldId = field, AttackerObjectIds = attackers, DefenderObjectIds = defenders,
            OptionalCosts = command.OptionalCosts ?? [], BattlefieldTargetObjectIds = command.BattlefieldTargetObjectIds ?? [],
            InitialFocusPlayerId = intent.PlayerId, InitialAttackingPlayerId = intent.PlayerId
        };
        var marker = Prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(context)));
        state = state with {
            Seats = state.Seats.Keys.ToDictionary(id => id, id => id, StringComparer.Ordinal),
            ObjectLocations = state.ObjectLocations.Where(e => e.Value.Zone != "BATTLEFIELD"
                || state.PlayerZones.Values.Any(z => z.Battlefields.Contains(e.Key)))
                .ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal),
            TimingState = TimingStates.SpellDuelOpen, FocusPlayerId = intent.PlayerId, ActivePlayerId = intent.PlayerId,
            PriorityPlayerId = null, PassedPriorityPlayerIds = [], PassedFocusPlayerIds = [],
            CardObjects = state.CardObjects.ToDictionary(e => e.Key, e => units.Any(c => c.ObjectId == e.Key)
                ? e.Value with { IsAttacking = false, IsDefending = false } : e.Value, StringComparer.Ordinal),
            UntilEndOfTurnEffects = state.UntilEndOfTurnEffects.Where(e => !e.StartsWith(Prefix)
                && e != BattlefieldTaskMarkers.SpellDuelCompleted(field)).Append(marker).ToArray()
        };
        var focusIntent = intent with { CommandType = CommandTypes.PassFocus };
        return await engine.ResolveAsync(state, focusIntent, new PassFocusCommand(), cancellationToken);
    }

    internal static void AssertCombatStarted(MatchState state)
    {
        var marker = Assert.Single(state.UntilEndOfTurnEffects, e => e.StartsWith(Prefix));
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[Prefix.Length..])));
        Assert.True(document.RootElement.GetProperty("CombatStarted").GetBoolean());
        OfficialGraveyardRecastTests.Restore(state); ArmamentMightTests.PublicRecovery(state);
    }

    internal static async Task<ResolutionResult> PassAsync(CoreRuleEngine engine, MatchState state,
        PlayerIntent intent, PassPriorityCommand command, CancellationToken cancellationToken)
    {
        if (state.FocusPlayerId is null || state.StackItems.Count != 0)
            return await engine.ResolveAsync(state, intent, command, cancellationToken);
        var marker = state.UntilEndOfTurnEffects.Single(e => e.StartsWith(Prefix));
        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(marker[Prefix.Length..])));
        var context = document.RootElement;
        var result = await engine.ResolveAsync(state, intent with {
            PlayerId = state.FocusPlayerId, CommandType = CommandTypes.PassFocus
        }, new PassFocusCommand(), cancellationToken);
        if (!result.Accepted || result.State.FocusPlayerId is not null || !result.State.BattleState.IsActive)
            return result;
        var battle = result.State.BattleState;
        var declared = await engine.ResolveAsync(result.State,
            new PlayerIntent(intent.IntentId + ":combat-stage", context.GetProperty("InitialAttackingPlayerId").GetString()!, CommandTypes.DeclareBattle),
            new DeclareBattleCommand(battle.BattlefieldObjectId!, battle.AttackerObjectIds, battle.DefenderObjectIds,
                context.GetProperty("OptionalCosts").EnumerateArray().Select(e => e.GetString()!).ToArray(),
                context.GetProperty("BattlefieldTargetObjectIds").EnumerateArray().Select(e => e.GetString()!).ToArray()), cancellationToken);
        return declared with { Events = result.Events.Concat(declared.Events).ToArray() };
    }
}
