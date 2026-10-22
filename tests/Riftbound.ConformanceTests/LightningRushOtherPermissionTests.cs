using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class LightningRushOtherPermissionTests
{
    [Fact]
    public async Task FizzsSeparateGraveyardPlayInstructionKeepsItsOwnFreeManaAndRecycleInsteadOfAutomaticallyPayingFlow()
    {
        var initial = LightningRushDeckDrawFlowTests.Position(graveyard: true);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·140/221", out var fizz));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = new("F", cardNo: fizz.CardNo, ownerId: "P1", controllerId: "P1", manaCost: fizz.ManaCost,
                power: fizz.SourceUnitPower, tags: [CardObjectTags.UnitCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["F"] = new("P1", "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Hand = ["F"] } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("F", "SFD·140/221", [])); await driver.Top();
        Assert.NotNull(driver.State.PendingEffectPlay);
        var pool = driver.State.RunePools["P1"];
        await driver.Act("P1", new PlayCardCommand("RUSH", LightningRushDeckDrawFlowTests.LightningRush, []));
        var spell = Assert.Single(driver.State.StackItems);
        Assert.Null(spell.FlowPlay); Assert.NotNull(spell.AfterPlayRecycle); Assert.Equal(0, spell.PlayCost!.PaidMana);
        Assert.Equal(pool, driver.State.RunePools["P1"]);
        await driver.Top();
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Empty(driver.State.StackItems);
        Assert.Equal("RUSH", driver.State.PlayerZones["P1"].MainDeck[^1]);
        Assert.DoesNotContain("RUSH", driver.State.PlayerZones["P1"].Graveyard.Concat(driver.State.PlayerZones["P1"].Banished));
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARDS_RECYCLED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("VEN·156", true)]
    [InlineData("OGN·183/298", false)]
    [InlineData("SFD·122/221", false)]
    [InlineData("UNL-032/219", false)]
    public async Task ChangedSpellControllerUsesItsOwnDeckWithoutRemakingPrivateChoicesAndKeepsTheFlowOwnersBanish(string cardNo, bool flow)
    {
        var initial = LightningRushDeckDrawFlowTests.Position(cardNo, graveyard: flow);
        initial = AttackDamageDistributionTests.Spell(initial, "REVERSAL", "OGN·080/298", "P2");
        var ownDeck = initial.PlayerZones["P1"].MainDeck.ToArray();
        var evidence = Environment.GetEnvironmentVariable("RIFTBOUND_LIGHTNING_CONTROL_EVIDENCE");
        var driver = new StandbyDefendObserverTests.Driver(initial, evidence is null ? null : Path.Combine(evidence, cardNo.Replace('/', '_')));
        await driver.Act("P1", new PlayCardCommand("RUSH", cardNo, []));
        var spell = Assert.Single(driver.State.StackItems); var receipt = spell.FlowPlay;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("REVERSAL", "OGN·080/298", [spell.StackItemId]));
        try { await driver.Top(); }
        finally { driver.Export("actual-after-control-original-state.json", driver.State); }
        Assert.Null(driver.State.PendingRuleChoice);
        var current = Assert.Single(driver.State.StackItems);
        Assert.Equal("P2", current.ControllerId); Assert.Equal(receipt, current.FlowPlay);
        Assert.Equal(1, driver.State.CardObjects["RUSH"].ObjectGeneration);
        await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(ownDeck, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(flow ? new[] { "RUSH" } : [], driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(!flow, driver.State.PlayerZones["P1"].Graveyard.Contains("RUSH"));
        Assert.DoesNotContain("RUSH", driver.State.PlayerZones["P2"].Banished);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }
}
