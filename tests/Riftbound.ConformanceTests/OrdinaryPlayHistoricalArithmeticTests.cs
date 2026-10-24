using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OrdinaryPlayHistoricalArithmeticTests
{
    [Fact]
    public async Task LaterBoonChangesCurrentPowerWithoutRewritingTheAppliedTemporaryModifiersHistory()
    {
        var (initial, driver) = await Applied(false);
        var applied = Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(4, applied.BasePower); Assert.Equal(7, applied.ResultingPower);
        await driver.Act("P1", new PlayCardCommand("BOON", "OGN·053/298", ["ALLY"])); await driver.Top();
        Assert.Equal(8, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["ALLY"].Tags);
        Assert.Equal(applied, Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers));
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(5, driver.State.CardObjects["ALLY"].Power);
        Assert.Contains(CardObjectTags.Boon, driver.State.CardObjects["ALLY"].Tags);
        Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); Export(nameof(LaterBoonChangesCurrentPowerWithoutRewritingTheAppliedTemporaryModifiersHistory), initial, driver);
    }

    [Fact]
    public async Task AnotherTemporaryModifierAfterBoonKeepsItsOwnNewBaseAndBothOriginalHistories()
    {
        var (initial, driver) = await Applied(true);
        var first = Assert.Single(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await driver.Act("P1", new PlayCardCommand("BOON", "OGN·053/298", ["ALLY"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("SECOND", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"])); await driver.Top();
        Assert.Equal(11, driver.State.CardObjects["ALLY"].Power);
        var modifiers = driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers;
        Assert.Equal(first, modifiers[0]); Assert.Equal(2, modifiers.Count);
        Assert.Equal(5, modifiers[1].BasePower); Assert.Equal(11, modifiers[1].ResultingPower); Assert.Equal(2, modifiers[1].AppliedOrder);
        await driver.Act("P1", new PlayCardCommand("RETURN-SECOND", "OGN·104/298", ["SECOND"])); await driver.Top();
        Assert.Contains("SECOND", driver.State.PlayerZones["P1"].Hand);
        Assert.Equal(first, driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers[0]);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(5, driver.State.CardObjects["ALLY"].Power); Assert.Empty(driver.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifiers);
        await driver.Replay(); Export(nameof(AnotherTemporaryModifierAfterBoonKeepsItsOwnNewBaseAndBothOriginalHistories), initial, driver);
    }

    [Fact]
    public async Task ForgedLaterPowerAndBoonWithoutTheirOriginalActionsFailTheIndependentFullLogReplay()
    {
        var (initial, driver) = await Applied(false); var hash = MatchStateHasher.Hash(driver.State);
        foreach (var forgeBoon in new[] { false, true }) {
            var forged = driver.State with { CardObjects = driver.State.CardObjects.ToDictionary(p => p.Key, p => p.Key == "ALLY"
                ? p.Value with { Power = 8, Tags = forgeBoon ? [..p.Value.Tags, CardObjectTags.Boon] : p.Value.Tags } : p.Value) };
            // Provenance checks cover the unchanged historical application;
            // later current state must independently match the original actions.
            FullGameEndToEndTests.AssertNoHiddenZoneLeak(new(true, null, forged, [],
                ResolutionResult.BuildSnapshots(forged), ResolutionResult.BuildPrompts(forged)));
            var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
                new CoreRuleEngine(), default, driver.Events());
            Assert.False(replay.IsMatch);
        }
        Assert.Equal(hash, MatchStateHasher.Hash(driver.State)); await driver.Replay();
        Export(nameof(ForgedLaterPowerAndBoonWithoutTheirOriginalActionsFailTheIndependentFullLogReplay), initial, driver);
    }

    private static async Task<(MatchState, Driver)> Applied(bool second)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(VenUnitEntryBasicTests.Position("VEN·026"), "RETURN", "OGN·104/298");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "BOON", "OGN·053/298");
        if (second) {
            initial = OfficialCopyIdentityTests.AddSpell(initial, "RETURN-SECOND", "OGN·104/298");
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SECOND"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECOND", "VEN·026", "P1") },
                PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [..p.Value.Hand, "SECOND"] } : p.Value),
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SECOND"] = new("P1", "HAND") } };
        }
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top(); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.Equal(7, driver.State.CardObjects["ALLY"].Power);
        return (initial, driver);
    }

    private static void Export(string name, MatchState initial, Driver driver)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_ENTRY_ARITHMETIC_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new {
            initial, final = driver.State, commands = driver.Commands(), events = driver.Events()
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
