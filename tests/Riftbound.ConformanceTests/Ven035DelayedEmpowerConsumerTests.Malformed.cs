using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class Ven035DelayedEmpowerConsumerTests
{
    [Theory]
    [InlineData("CONTROLLER")]
    [InlineData("SOURCE")]
    [InlineData("UNIT")]
    public async Task MalformedDelayedReceiptIdentitiesAreRecoveryErrorsRatherThanExceptions(string attack)
    {
        var initial = Position("P1"); var game = new Driver(initial);
        MatchState? forged = null; string? error = null;
        try
        {
            var unit = await PrepareTarget(game, "P1", false); await game.Act("P1", new RecycleRuneCommand("STATUS-RUNE-1"));
            await game.Act("P1", new PlayCardCommand("SPELL", No, [unit], Mode: Empower)); await game.Top();
            const string prefix = "DELAYED_EMPOWER:";
            var marker = Assert.Single(game.State.UntilEndOfTurnEffects, e => e.StartsWith(prefix, StringComparison.Ordinal));
            var receipt = JsonSerializer.Deserialize<DelayedEmpowerContext>(Convert.FromBase64String(marker[prefix.Length..]))!;
            receipt = attack switch {
                "CONTROLLER" => receipt with { ControllerId = null! },
                "SOURCE" => receipt with { Source = receipt.Source with { ObjectId = null! } },
                _ => receipt with { Unit = receipt.Unit with { ObjectId = null! },
                    InstanceId = $"{receipt.CreatingStackItemId}:END-EMPOWER:{receipt.ExecutionIndex}:" } };
            var wire = prefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(receipt));
            forged = game.State with { UntilEndOfTurnEffects = game.State.UntilEndOfTurnEffects.Select(e => e == marker ? wire : e).ToArray() };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(forged), e => e.Contains("delayed empower", StringComparison.Ordinal));
            await game.Replay(); Restore(game.State);
        }
        catch (Exception e) { error = e.GetType().Name + ": " + e.Message; throw; }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN035_EVIDENCE") is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(Path.Combine(folder, "malformed-delay-" + attack + ".json"),
                    JsonSerializer.Serialize(new { initial, original = game.State, forged, error,
                        commands = game.Commands(), events = game.Events(),
                        scope = "Existing legitimate paid prefix; separate malformed component-state validation, not a Session/API submission." }, Json));
            }
        }
    }
}
