using System.Reflection;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class HereLocationControlTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("control-only", 2)]
    [InlineData("source-base", 0)]
    [InlineData("target-base", 0)]
    [InlineData("both-base", 2)]
    public async Task HereUsesTheCurrentSharedLocationAndTheAbilityControllerAfterActualControlChange(string response, int damage)
    {
        // Explicit tick0 midgame position. Standby preparation, banishment,
        // owner play, control and movement below are all ordinary commands;
        // this is not an empty-opening or complete-legend certification.
        var initial = BanishEffectPlayTests.Position(unit: "UNL-003/219");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·049/298", "F", "P2");
        initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 5 },
                ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        initial = AttackDamageDistributionTests.Spell(initial, "TAKE", "SFD·202/221", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "FLASH", "OGS·011/024", "P2");
        initial = await WithPrintedRunes(initial);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P2", new HideCardCommand("TAKE", "SFD·202/221", "BATTLEFIELD:F", ["STANDBY_A"]));
        await driver.Act("P2", new EndTurnCommand());
        Assert.Equal("P1", driver.State.TurnPlayerId);
        await Fund("P1");
        await driver.Act("P1", new RecycleRuneCommand(driver.State.PlayerZones["P1"].Base.First(id => id.StartsWith("FUND-", StringComparison.Ordinal))));
        await driver.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("HOST", "UNL-003/219", [], Destination: "BATTLEFIELD:F"));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var binding = Assert.Single(driver.State.StackItems).PermanentPlaySource!;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("TAKE", "SFD·202/221", ["HOST"])); await driver.Top();
        Assert.Equal("P2", driver.State.CardObjects["HOST"].ControllerId);
        Assert.Equal("P1", driver.State.CardObjects["HOST"].OwnerId);
        Assert.Equal("P1", Assert.Single(driver.State.StackItems).ControllerId);
        Assert.Equal(binding, Assert.Single(driver.State.StackItems).PermanentPlaySource);
        if (response != "control-only")
        {
            await driver.Act("P1", new PassPriorityCommand());
            await Fund("P2");
            var targets = response == "both-base" ? new[] { "HOST", "ENEMY" } : new[] { response == "source-base" ? "HOST" : "ENEMY" };
            await driver.Act("P2", new PlayCardCommand("FLASH", "OGS·011/024", targets)); await driver.Top();
            foreach (var id in targets)
            {
                Assert.Equal("BASE", driver.State.ObjectLocations[id].Zone);
                Assert.Equal("P2", driver.State.ObjectLocations[id].PlayerId);
            }
            Assert.Equal(binding.Generation, driver.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal(binding, Assert.Single(driver.State.StackItems).PermanentPlaySource);
        }
        await driver.Top();
        await BanishEffectPlayTests.Replay(initial, driver);
        output.WriteLine("HERE_CAUSAL_BOUNDARY " + JsonSerializer.Serialize(new {
            response, sourceBinding = binding, sourceOwner = driver.State.CardObjects["HOST"].OwnerId,
            sourceController = driver.State.CardObjects["HOST"].ControllerId,
            sourceLocation = driver.State.ObjectLocations["HOST"], targetLocation = driver.State.ObjectLocations["ENEMY"],
            actualDamage = driver.State.CardObjects["ENEMY"].Damage, expectedDamage = damage,
            commands = driver.Commands().Length, events = driver.Events().Length,
            originalActionEventStateReplayVerified = true
        }));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_HERE_CONTROL_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, response + ".json"), JsonSerializer.Serialize(new {
                initial, final = driver.State, commands = driver.Commands(), events = driver.Events(),
                expectedDamage = damage, originalReplayVerified = true
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.Equal(damage, driver.State.CardObjects["ENEMY"].Damage);
        if (damage == 2) Assert.Contains(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY")
            && Convert.ToInt32(e.Event.Payload.GetValueOrDefault("damage")) == 2);

        async Task Fund(string player)
        {
            foreach (var id in driver.State.PlayerZones[player].Base.Where(id => id.StartsWith("FUND-", StringComparison.Ordinal)
                && !driver.State.CardObjects[id].IsExhausted).ToArray()) await driver.Act(player, new TapRuneCommand(id));
        }
    }

    [Fact]
    public async Task HereDoesNotTreatTheTwoPlayersBasesAsTheSameLocationAfterActualMovement()
    {
        var initial = BanishEffectPlayTests.Position(unit: "UNL-003/219");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·049/298", "F", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        initial = AttackDamageDistributionTests.Spell(initial, "FLASH-P1", "OGS·011/024", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "FLASH-P2", "OGS·011/024", "P2");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("HOST", "UNL-003/219", [], Destination: "BATTLEFIELD:F"));
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var binding = Assert.Single(driver.State.StackItems).PermanentPlaySource!;
        await driver.Act("P1", new PlayCardCommand("FLASH-P1", "OGS·011/024", ["HOST"])); await driver.Top();
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("FLASH-P2", "OGS·011/024", ["ENEMY"])); await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("P1", driver.State.ObjectLocations["HOST"].PlayerId);
        Assert.Equal("BASE", driver.State.ObjectLocations["ENEMY"].Zone);
        Assert.Equal("P2", driver.State.ObjectLocations["ENEMY"].PlayerId);
        Assert.Equal(binding.Generation, driver.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(binding, Assert.Single(driver.State.StackItems).PermanentPlaySource);
        await driver.Top();
        await BanishEffectPlayTests.Replay(initial, driver);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED"
            && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ENEMY"));
        output.WriteLine("HERE_OPPOSITE_BASES " + JsonSerializer.Serialize(new {
            sourceBase = "P1", targetBase = "P2", expectedDamage = 0,
            actualDamage = driver.State.CardObjects["ENEMY"].Damage,
            commands = driver.Commands().Length, events = driver.Events().Length,
            originalActionEventStateReplayVerified = true
        }));
    }

    private static async Task<MatchState> WithPrintedRunes(MatchState initial)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync();
        var runes = catalog.Cards.Where(c => c.CardCategoryNameList.Contains("符文"))
            .GroupBy(c => c.CardColorList.Single()).Select(g => g.First()).ToArray();
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!
            .GetMethod("TryCreate", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var player in initial.Seats.Keys)
            foreach (var rune in runes)
                for (var copy = 0; copy < 2; copy++)
                {
                    var id = $"FUND-{player}-{rune.CardColorList.Single()}-{copy}";
                    object?[] args = [id, player, rune.CardNo, null]; Assert.True((bool)factory.Invoke(null, args)!);
                    initial = initial with {
                        CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [id] = Assert.IsType<CardObjectState>(args[3]) },
                        ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new(player, "BASE") },
                        PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                            [player] = initial.PlayerZones[player] with { Base = [..initial.PlayerZones[player].Base, id] } } };
                }
        return initial;
    }
}
