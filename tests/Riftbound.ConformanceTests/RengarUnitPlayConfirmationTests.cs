using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class RengarUnitPlayConfirmationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("UNL-111/219")]
    [InlineData("UNL-003/219")]
    [InlineData("OGN·067/298")]
    public async Task AUnitEntersBeforeTheMandatoryIndependentLegendTargetIsChosen(string cardNo)
    {
        // Explicit midgame witness of the same missing-target branch found in
        // the native original opening. Neither this fixture nor its seed is
        // substituted into that accepted native history.
        var initial = WithLegend(PermanentPlayTargetTests.Position(cardNo));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", cardNo, [], Destination: "BASE"));
        await BanishEffectPlayTests.Replay(initial, driver);
        output.WriteLine("RENGAR_POST_ENTRY_TARGET " + JsonSerializer.Serialize(new {
            cardNo, unitEntered = driver.State.PlayerZones["P1"].Base.Contains("UNIT"),
            pendingChoice = driver.State.PendingCardChoice?.ChoiceWindow,
            stackSources = driver.State.StackItems.Select(i => new { i.SourceObjectId, i.CardNo, i.EffectKind }),
            originalActionEventStateReplayMatched = true
        }));
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RENGAR_CONFIRMATION_EVIDENCE") is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, cardNo.Replace('·', '-').Replace('/', '-') + ".json"),
                JsonSerializer.Serialize(new { initial, final = driver.State, commands = driver.Commands(), events = driver.Events() },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN");
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal("P1", choice.PlayerId);
        Assert.Equal("LEGEND", choice.SourceObjectId);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Contains("UNIT", choice.LegalObjectIds); Assert.Contains("HOST", choice.LegalObjectIds);
        Assert.Null(driver.State.PriorityPlayerId);
        var power = driver.State.CardObjects["UNIT"].Power;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        var ability = Assert.Single(driver.State.StackItems);
        Assert.Equal("LEGEND", ability.SourceObjectId);
        Assert.Equal(driver.State.CardObjects["UNIT"].ObjectGeneration, ability.TargetGenerations!["UNIT"]);
        Assert.Equal(power, driver.State.CardObjects["UNIT"].Power);
        await driver.Top();
        Assert.Equal(power + 1, driver.State.CardObjects["UNIT"].Power);
        var modifier = Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
        Assert.Equal("LEGEND", modifier.SourceObjectId);
        Assert.Equal("CoreRuleEngine.ResolveRengarUnitPlayedPowerTrigger", modifier.SourcePath);
        Assert.Empty(driver.State.StackItems);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("UNL-183/219")]
    [InlineData("UNL-227/219")]
    [InlineData("UNL-227*/219")]
    public async Task ADeclaredTargetIsRecheckedAfterEntryForEachPrintedLegend(string legend)
    {
        var initial = WithLegend(PermanentPlayTargetTests.Position("UNL-111/219"));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { CardNo = legend } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "UNL-111/219", ["UNIT"], Destination: "BASE"));
        Assert.Null(driver.State.PendingCardChoice);
        var item = Assert.Single(driver.State.StackItems);
        Assert.Equal("LEGEND", item.SourceObjectId);
        Assert.Equal(legend, item.CardNo);
        Assert.Equal(["UNIT"], item.TargetObjectIds);
        Assert.Equal(driver.State.CardObjects["UNIT"].ObjectGeneration, item.TargetGenerations!["UNIT"]);
        var power = driver.State.CardObjects["UNIT"].Power;
        await driver.Top();
        Assert.Equal(power + 1, driver.State.CardObjects["UNIT"].Power);
        Assert.Equal(legend, Assert.Single(driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers).SourceCardNo);
        await BanishEffectPlayTests.Replay(initial, driver);
    }

    internal static MatchState WithLegend(MatchState state) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = new("LEGEND", cardNo: "UNL-183/219", ownerId: "P1", controllerId: "P1") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["LEGEND"] } },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["LEGEND"] = new("P1", "LEGEND") }
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheControllerOrdersThePrintedAbilityAndIndependentLegendBeforeEitherTargetIsConfirmed(bool legendFirst)
    {
        var (initial, driver) = await OpenConcurrentPlay();
        await BanishEffectPlayTests.Replay(initial, driver);
        Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Battlefields);
        var finalized = Assert.Single(driver.State.CardPlays.Finalized);
        var resolved = Assert.Single(driver.State.CardPlays.Resolved);
        Assert.Equal(finalized.PlayId, resolved.PlayId);
        Assert.Equal(driver.State.CardObjects["SHADOW"].ObjectGeneration, resolved.Source.Generation);
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Null(driver.State.PriorityPlayerId);
        Assert.Equal(2, driver.State.TriggerQueue.Count);
        var legend = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        var own = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "SHADOW");
        var order = legendFirst ? new[] { legend.TriggerId, own.TriggerId } : new[] { own.TriggerId, legend.TriggerId };
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: order));
        Assert.Equal(2, driver.State.StackItems.Count);
        Assert.Equal(legendFirst ? "SHADOW" : "LEGEND", driver.State.PendingCardChoice!.SourceObjectId);
        for (var index = 0; index < 2; index++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Contains("ENEMY", choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        }
        Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(2, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        await driver.Top();
        await driver.Top();
        Assert.Empty(driver.State.StackItems);
        if (legendFirst)
        {
            Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
            Assert.Equal(3, driver.State.CardObjects["ENEMY"].Power);
            Assert.Equal(2, driver.State.CardObjects["ENEMY"].Damage);
        }
        else
        {
            Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN"
                && e.Event.Payload.GetValueOrDefault("targetObjectId") as string == "ENEMY");
        }
        await BanishEffectPlayTests.Replay(initial, driver);
    }
    internal static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> OpenConcurrentPlay()
    {
        var initial = WithLegend(OfficialHoldSequenceTests.AddUnit(StandbySourceRulesTests.Position("UNL-003/219"),
            "ENEMY", "UNL-076/219", "F", "P2"));
        initial = LocalDestructionRecallTests.At(initial, "ENEMY", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = 2 } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new HideCardCommand("SHADOW", "UNL-003/219", "BATTLEFIELD:F", ["STANDBY_A"]));
        await driver.Act("P1", new EndTurnCommand());
        await driver.Act("P2", new MoveUnitCommand("ENEMY", "BASE", "BATTLEFIELD:F", []));
        await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new PlayCardCommand("SHADOW", "UNL-003/219", []));
        return (initial, driver);
    }

}
