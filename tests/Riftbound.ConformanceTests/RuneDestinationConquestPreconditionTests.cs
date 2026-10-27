using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RuneDestinationConquestPreconditionTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task EffectRecycleRequiresNewControlAndNeverProducesPaymentPower(int runeCount, bool alreadyControlled)
    {
        // Shared explicit SQL fixture; this is not an ordinary opening or SQL execution.
        var initial = RuneRecycleDestinationSqlAcceptance.CreateInitialState(
            $"rune-destination-conquest-{runeCount}-{alreadyControlled}", runeCount, alreadyControlled ? "P1" : "P2");
        Assert.Equal("P1", initial.CardObjects["THUNDER"].OwnerId);
        Assert.Equal(alreadyControlled ? "P1" : "P2", initial.CardObjects["THUNDER"].ControllerId);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var declared = await driver.Act("P1", new DeclareBattleCommand("THUNDER", ["ATTACKER"], ["DEFENDER"],
            ["COMBAT_ASSIGNMENT"], BattlefieldTargetObjectIds: []));
        Assert.Equal(new[] { "D1" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(initial.RunePools["P1"], driver.State.RunePools["P1"]);
        Assert.DoesNotContain(declared.Events, e => e.Kind is "MANA_GAINED" or "POWER_GAINED");
        if (alreadyControlled)
        {
            Assert.DoesNotContain(declared.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Equal(new[] { "BOTTOM" }, driver.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal("BASE", driver.State.ObjectLocations["RUNE"].Zone);
            Assert.True(driver.State.CardObjects["RUNE"].IsExhausted);
            Assert.Null(driver.State.PendingCardChoice);
        }
        else
        {
            Assert.Single(declared.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
            Assert.Equal(new[] { "BOTTOM", "RUNE" }, driver.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal("RUNE_DECK", driver.State.ObjectLocations["RUNE"].Zone);
            Assert.False(driver.State.CardObjects["RUNE"].IsExhausted);
            if (runeCount == 2)
            {
                Assert.Contains("RUNE2", driver.State.PlayerZones["P1"].Base);
                Assert.True(driver.State.CardObjects["RUNE2"].IsExhausted);
            }
            // The current exact-print erratum requires recycling without a
            // rune-choice window. This separate observer may still be declined.
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            Assert.False(driver.State.CardObjects["SIVIR"].IsExhausted);
            Assert.Equal(new[] { "BOTTOM", "RUNE" }, driver.State.PlayerZones["P1"].RuneDeck);
        }
        Assert.Empty(driver.State.StackItems);
        Assert.Empty(driver.State.TriggerQueue);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
        if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is { Length: > 0 } folder)
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder, $"rune-destination-precondition-{runeCount}-{alreadyControlled}.json"),
                JsonSerializer.Serialize(new { initial, commands = driver.Commands(), events = driver.Events(), driver.State,
                    scope = "Explicit finite SQL fixture shared with the real leaf; in-memory commands and original replay only, no SQL/network/restart or ordinary-opening claim." },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
    }
}
