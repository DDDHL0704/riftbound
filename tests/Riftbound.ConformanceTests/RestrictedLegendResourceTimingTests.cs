using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedLegendResourceTimingTests
{
    [Theory]
    [InlineData("SFD·189/221", LegendActionAbilityCatalog.OrnnLegendAbilityId)]
    [InlineData("SFD·244/221", LegendActionAbilityCatalog.OrnnLegendAbilityId)]
    [InlineData("OGN·247/298", LegendActionAbilityCatalog.KaisaLegendAbilityId)]
    [InlineData("OGN·299/298", LegendActionAbilityCatalog.KaisaLegendAbilityId)]
    [InlineData("OGN·299*/298", LegendActionAbilityCatalog.KaisaLegendAbilityId)]
    public async Task ARestrictedResourceSkillDoesNotRequireAnAlreadyPaidCardOnTheStack(string no, string ability)
    {
        var initial = OfficialHoldSequenceTests.AddLegend(ImposedMovementContinuationTests.WithPool(
            ImposedMovementContinuationTests.Position("standard"), RunePool.Empty), no);
        var game = new Driver(initial); ResolutionResult? core = null; string? sessionFailure = null;
        try
        {
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(initial));
            ArmamentMightTests.PublicRecovery(initial);
            Assert.Empty(initial.StackItems); Assert.Null(initial.PriorityPlayerId);
            var command = new LegendActCommand("LEGEND", ability, [], []);
            core = await new CoreRuleEngine().ResolveAsync(initial, new("restricted-legend-bank", "P1", command.CmdType), command, default);
            try { await game.Act("P1", command); } catch (Exception e) { sessionFailure = e.ToString(); }
            Assert.True(core.Accepted, core.ErrorMessage); Assert.Null(sessionFailure);
            Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
            Assert.Empty(game.State.StackItems); Assert.Empty(game.State.TriggerQueue);
            Assert.Null(game.State.PriorityPlayerId); Assert.Equal("P1", game.State.FocusPlayerId);
            // Neither spell-only nor equipment-only power is unrestricted power.
            Assert.Equal(0, game.State.RunePools["P1"].Power);
            Assert.Single(core.Events, e => e.Kind == "POWER_GAINED");
            await game.Replay();
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_RESTRICTED_LEGEND_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, "neutral-" + no.Replace('/', '_') + ".json"), JsonSerializer.Serialize(new {
                    scope = "Exact five CN source printings; explicit turn3 neutral position, real Core and current prompt/tick Session command. No pending-card prerequisite in the source. Full journal and original failure retained; no ordinary opening or external acceptance.",
                    initial, core, sessionFailure, driverState = game.State, commands = game.Commands(), events = game.Events()
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
        }
    }
}
