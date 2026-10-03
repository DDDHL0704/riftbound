using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

internal static class CombatTestDriver
{
    internal static IReadOnlyList<CombatDamageAssignmentDto> Assignments(ActionPromptDto prompt)
    {
        var metadata = JsonSerializer.SerializeToElement(prompt.View!.Metadata);
        var pool = metadata.GetProperty("assignableDamagePool");
        var targets = metadata.GetProperty("legalTargets");
        var lethal = metadata.GetProperty("lethalDamageThreshold");
        var result = new List<CombatDamageAssignmentDto>();
        var assignedByTarget = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var source in pool.EnumerateObject())
        {
            var remaining = source.Value.GetInt32();
            var legal = targets.GetProperty(source.Name).EnumerateArray().Select(value => value.GetString()!).ToArray();
            for (var i = 0; i < legal.Length && remaining > 0; i++)
            {
                var amount = i == legal.Length - 1 ? remaining : Math.Min(remaining, Math.Max(0, lethal.GetProperty(legal[i]).GetInt32() - assignedByTarget.GetValueOrDefault(legal[i])));
                if (amount <= 0) continue;
                result.Add(new(source.Name, legal[i], amount));
                remaining -= amount;
                assignedByTarget[legal[i]] = assignedByTarget.GetValueOrDefault(legal[i]) + amount;
            }
        }
        return result;
    }

    internal static async Task<ResolutionResult> FinishAsync(ResolutionResult current, CoreRuleEngine? engine = null)
    {
        engine ??= new CoreRuleEngine();
        var events = current.Events.ToList();
        for (var step = 0; step < 20 && current.State.BattleState.IsActive; step++)
        {
            var prompt = current.Prompts.Values.FirstOrDefault(prompt => prompt.Actionable
                && (prompt.Actions.Contains(CommandTypes.AssignCombatDamage) || prompt.Actions.Contains(CommandTypes.PassPriority)));
            if (prompt is null) break;
            GameCommand command = prompt.Actions.Contains(CommandTypes.AssignCombatDamage)
                ? new AssignCombatDamageCommand(current.State.BattleState.BattleId!, current.State.BattleState.BattlefieldObjectId!, Assignments(prompt))
                : new PassPriorityCommand();
            var kind = command is AssignCombatDamageCommand ? CommandTypes.AssignCombatDamage : CommandTypes.PassPriority;
            current = await engine.ResolveAsync(current.State, new($"finish-combat-{current.State.Tick}-{step}", prompt.PlayerId, kind), command, default);
            Assert.True(current.Accepted, current.ErrorMessage);
            events.AddRange(current.Events);
        }
        return current with { Events = events };
    }

    // A source may distribute its damage over multiple targets; compare its total
    // and the power calculation rather than incorrectly assuming one damage event.
    internal static CombatDamageSummary DamageFrom(ResolutionResult result, string sourceId)
    {
        var events = result.Events.Where(e => e.Kind == "DAMAGE_APPLIED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), sourceId)
            && e.Payload.ContainsKey("combatRole")).ToArray();
        Assert.NotEmpty(events);
        foreach (var key in new[] { "basePower", "combatPower", "keywordBonus", "staticPowerBonus" })
            Assert.All(events, e => Assert.Equal(events[0].Payload.GetValueOrDefault(key), e.Payload.GetValueOrDefault(key)));
        var payload = events[0].Payload.ToDictionary(e => e.Key, e => e.Value);
        payload["damage"] = events.Sum(e => Convert.ToInt32(e.Payload["damage"]));
        return new(payload);
    }

    internal sealed record CombatDamageSummary(IReadOnlyDictionary<string, object?> Payload);

    internal static CombatDamageSummary DamageMatching(ResolutionResult result, Func<GameEvent, bool> predicate, int basePower)
    {
        var sources = result.Events.Where(predicate)
            .Where(e => Equals(e.Payload.GetValueOrDefault("basePower"), basePower))
            .Select(e => Assert.IsType<string>(e.Payload["sourceObjectId"])).Distinct(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(sources);
        var summaries = sources.Select(source => DamageFrom(result, source)).ToArray();
        foreach (var key in new[] { "basePower", "combatPower", "keywordBonus", "staticPowerBonus", "damage" })
            Assert.All(summaries, summary => Assert.Equal(summaries[0].Payload.GetValueOrDefault(key), summary.Payload.GetValueOrDefault(key)));
        return summaries[0];
    }
}
