using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldEffectPlayTriggerTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActualHuntingRhythmPlayToBattlefieldConfirmsMerfolkDamageAfterEntry()
    {
        // Explicit tick0 position. Subsequent entry, confirmation and damage
        // must all come from ordinary commands, without editing accepted state.
        var initial = ArmamentMightTests.Position(unit: "UNL-003/219", power: 2);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY", "OGN·096/298", "F", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" } } };
        initial = AttackDamageDistributionTests.Spell(initial, "HUNT", "UNL-184/219", "P1");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", "UNL-184/219", ["HOST"]));
        await driver.Top();
        output.WriteLine("EFFECT_PLAY_BOUNDARY " + JsonSerializer.Serialize(new {
            location = driver.State.ObjectLocations["HOST"],
            pendingEffectPlay = driver.State.PendingEffectPlay,
            pendingChoice = driver.State.PendingCardChoice?.ChoiceWindow,
            legalChoices = driver.State.PendingCardChoice?.LegalObjectIds,
            stackSources = driver.State.StackItems.Select(item => item.SourceObjectId).ToArray()
        }));
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.Equal("P1", pending.PlayerId);
        Assert.Equal("BANISHED", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal(initial.CardObjects["HOST"].ObjectGeneration + 1, pending.Sources["HOST"]);
        Assert.False(pending.Optional);
        Assert.True(pending.IgnoreBaseMana);
        Assert.True(pending.IgnoreBasePower);
        Assert.False(pending.IgnoreAllCosts);
        Assert.DoesNotContain(driver.State.StackItems, item => item.SourceObjectId == "HOST");
        await driver.Act("P1", new PlayCardCommand("HOST", "UNL-003/219", [], Destination: "BATTLEFIELD:F"));
        Assert.Equal("BATTLEFIELD", driver.State.ObjectLocations["HOST"].Zone);
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > initial.CardObjects["HOST"].ObjectGeneration);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY" }, choice.LegalObjectIds);
        var ability = Assert.Single(driver.State.StackItems, item => item.SourceObjectId == "HOST");
        Assert.True(ability.SourceConfirmed);
        Assert.Equal(new ObjectBinding("HOST", driver.State.CardObjects["HOST"].ObjectGeneration), ability.PermanentPlaySource);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await driver.Top();
        Assert.Equal(2, driver.State.CardObjects["ENEMY"].Damage);
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }
}
