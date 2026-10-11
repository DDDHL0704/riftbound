using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Preserve historical effect/cost assertions after exercising the current
// 377.3 response window through production commands.
internal static class LegendAttachmentTestDriver
{
    public static async Task<ResolutionResult> FinishAsync(ResolutionResult activation, MatchSession? session = null)
    {
        Assert.True(activation.Accepted, activation.ErrorMessage);
        var item = Assert.Single(activation.State.StackItems, i => i.LegendAttachment is not null);
        Assert.DoesNotContain(activation.Events, e => e.Kind is "EQUIPMENT_ATTACHED" or "EQUIPMENT_DETACHED");
        var result = activation; var events = activation.Events.ToList();
        for (var n = 0; result.State.StackItems.Any(i => i.StackItemId == item.StackItemId); n++)
        {
            Assert.True(n < 8, "Legend attachment did not resolve after response passes.");
            var player = result.State.PriorityPlayerId; Assert.False(string.IsNullOrWhiteSpace(player));
            var command = new PassPriorityCommand();
            result = session is null
                ? await new CoreRuleEngine().ResolveAsync(result.State, new("legend-attach-pass-" + result.State.Tick, player!, CommandTypes.PassPriority), command, default)
                : await session.SubmitAsync(player!, "legend-attach-pass-" + result.State.Tick, command,
                    JsonSerializer.SerializeToElement(command, new JsonSerializerOptions(JsonSerializerDefaults.Web)), default);
            Assert.True(result.Accepted, result.ErrorMessage); events.AddRange(result.Events);
        }
        return result with { Events = events };
    }
}
