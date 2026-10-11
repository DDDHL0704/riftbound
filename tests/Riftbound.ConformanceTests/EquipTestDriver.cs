using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

// Historical attachment/cost tests retain their assertions at resolution, after
// explicitly exercising the two response opportunities introduced by DOC04 377/818.
internal static class EquipTestDriver
{
    public static async Task<ResolutionResult> FinishAsync(ResolutionResult activation, MatchSession? session = null)
    {
        if (!activation.Accepted) return activation;
        var equip = Assert.Single(activation.State.StackItems, i => i.EffectKind == "ASSEMBLE_EQUIPMENT");
        Assert.Null(activation.State.CardObjects[equip.SourceObjectId].AttachedToObjectId);
        Assert.DoesNotContain(activation.Events, e => e.Kind == "EQUIPMENT_ATTACHED");
        var result = activation; var events = activation.Events.ToList();
        for (var count = 0; result.State.StackItems.Any(i => i.StackItemId == equip.StackItemId); count++)
        {
            Assert.True(count < 20, "Equip did not resolve within the expected response sequence.");
            var player = result.State.PriorityPlayerId; Assert.False(string.IsNullOrWhiteSpace(player));
            var command = new PassPriorityCommand();
            result = session is null
                ? await new CoreRuleEngine().ResolveAsync(result.State, new("equip-pass-" + result.State.Tick, player!, CommandTypes.PassPriority), command, default)
                : await session.SubmitAsync(player!, "equip-pass-" + result.State.Tick, command,
                    JsonSerializer.SerializeToElement(command, command.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(result.Accepted, result.ErrorMessage); events.AddRange(result.Events);
        }
        return result with { Events = events };
    }
}
