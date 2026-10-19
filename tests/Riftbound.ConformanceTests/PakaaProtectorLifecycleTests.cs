using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class PakaaProtectorLifecycleTests
{
    [Theory]
    [InlineData("DESTROY", true)] [InlineData("DESTROY", false)]
    [InlineData("REENTER", true)] [InlineData("REENTER", false)]
    [InlineData("CONTROL", true)] [InlineData("CONTROL", false)]
    public async Task ActualResponsesKeepCapturedDeckControllerAndLimitSelfMightToTheOriginalIncarnation(string response, bool unitTop)
    {
        var spell = response == "DESTROY" ? "UNL-142/219" : response == "REENTER" ? "UNL-184/219" : "SFD·202/221";
        var actor = response == "CONTROL" ? "P2" : "P1";
        var initial = AttachedRuleTextTests.WithSpell(PakaaProtectorRevealTests.ControlledJournalPosition(
            unitTop ? "OGN·103/298" : "OGN·104/298"), actor, spell);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { [actor] = new(20, 20) } };
        if (response == "DESTROY")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["REVIVE"] = new("REVIVE", cardNo: "OGN·103/298", ownerId: "P1", controllerId: "P1", manaCost: 1, power: 1, tags: [CardObjectTags.UnitCard]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["REVIVE"] = new("P1", "GRAVEYARD") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Graveyard = initial.PlayerZones["P1"].Graveyard.Append("REVIVE").ToArray() } } };
        if (response == "CONTROL")
            initial = initial with { ActivePlayerId = "P2", TurnPlayerId = "P2",
                CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                    ["F"] = initial.CardObjects["F"] with { ControllerId = "P2" },
                    ["PREPARE-DRAW"] = new("PREPARE-DRAW", cardNo: "OGN·103/298", ownerId: "P1", controllerId: "P1", manaCost: 1, power: 1, tags: [CardObjectTags.UnitCard]) },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["PREPARE-DRAW"] = new("P1", "MAIN_DECK") },
                PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = new[] { "PREPARE-DRAW" }.Concat(initial.PlayerZones["P1"].MainDeck).ToArray() } } };
        if (response == "CONTROL")
            initial = OfficialHoldSequenceTests.AddUnit(initial, "KEEPER", "OGN·049/298", "F", "P2");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        if (response == "CONTROL")
        {
            await driver.Act("P2", new HideCardCommand("SPELL", spell, "BATTLEFIELD:F", ["STANDBY_A"]));
            await driver.Act("P2", new EndTurnCommand());
            Assert.Equal("P1", driver.State.TurnPlayerId);
        }
        await PakaaProtectorRevealTests.Move(driver);
        var generation = driver.State.CardObjects["HOST"].ObjectGeneration;
        if (actor == "P2") await driver.Act("P1", new PassPriorityCommand());
        await driver.Act(actor, response == "CONTROL" ? new RevealCardCommand("SPELL", spell, ["HOST"])
            : response == "DESTROY" ? new PlayCardCommand("SPELL", spell, ["REVIVE"], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:HOST"])
            : new PlayCardCommand("SPELL", spell, ["HOST"]));
        await driver.Top();
        if (response == "DESTROY")
        {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand("REVIVE", "OGN·103/298", []));
        }
        if (response == "REENTER")
        {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P1", new PlayCardCommand("HOST", "VEN·033", [], Destination: "BATTLEFIELD:F"));
            Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > generation);
            Assert.Contains("HOST", driver.State.PlayerZones["P1"].Battlefields);
        }
        else if (response == "DESTROY")
        {
            Assert.Contains("HOST", driver.State.PlayerZones["P1"].Graveyard);
            Assert.True(driver.State.CardObjects["HOST"].ObjectGeneration > generation);
        }
        else
        {
            Assert.Equal(generation, driver.State.CardObjects["HOST"].ObjectGeneration);
            Assert.Equal("P2", driver.State.CardObjects["HOST"].ControllerId);
            Assert.Contains("HOST", driver.State.PlayerZones["P2"].Battlefields);
        }
        var original = Assert.Single(driver.State.StackItems, item => item.MovementObserver is not null);
        Assert.Equal("P1", original.ControllerId); Assert.Equal(generation, original.MovementObserver!.SourceGeneration);
        Assert.Equal(new[] { "D0", "D1" }, driver.State.PlayerZones["P1"].MainDeck);
        // Complete a retained revival parent before the already earned movement skill.
        for (var i = 0; i < 3 && driver.State.StackItems.Any(item => item.MovementObserver is not null); i++) await driver.Top();
        Assert.DoesNotContain(driver.State.StackItems, item => item.MovementObserver is not null);
        Assert.Single(driver.Events(), entry => entry.Event.Kind == "CARDS_REVEALED");
        Assert.Equal(new[] { "D1" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Equal(unitTop, driver.State.PlayerZones["P1"].Hand.Contains("D0"));
        Assert.Equal(!unitTop, driver.State.PlayerZones["P1"].Graveyard.Contains("D0"));
        Assert.DoesNotContain("D0", driver.State.PlayerZones["P2"].Hand);
        var host = driver.State.CardObjects["HOST"];
        Assert.Equal(response == "CONTROL" && !unitTop ? 6 : 4, host.Power);
        Assert.Equal(response == "CONTROL" && !unitTop ? 2 : 0, host.UntilEndOfTurnPowerModifier);
        ArmamentMightTests.PublicRecovery(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ActualUnitDrawFeedsTheSecondDrawObserverWhileNonunitTrashDoesNot(bool unitTop)
    {
        var initial = PakaaProtectorRevealTests.ControlledJournalPosition(unitTop ? "OGN·103/298" : "OGN·104/298");
        initial = initial with { DrawLedger = new(initial.TurnNumber, new Dictionary<string, int> { ["P1"] = 1 }),
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEM"] = new("GEM", cardNo: "UNL-074/219", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["GEM"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("GEM").ToArray() } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await PakaaProtectorRevealTests.Move(driver); await driver.Top();
        Assert.Equal(unitTop ? 2 : 1, driver.State.DrawLedger.Counts["P1"]);
        Assert.Equal(unitTop ? 1 : 0, driver.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        if (unitTop)
        {
            var pending = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", pending.ChoiceWindow);
            await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, pending.ChoiceWindow, ["HOST"]));
            await driver.Top();
        }
        else Assert.Null(driver.State.PendingCardChoice);
        Assert.Equal(6, ArmamentMightTests.Might(driver.State));
        Assert.DoesNotContain("\"D1\"", System.Text.Json.JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task NormalEndTurnClearsTheNonunitRevealMightAndItsLedger()
    {
        var initial = PakaaProtectorRevealTests.ControlledJournalPosition("OGN·104/298");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await PakaaProtectorRevealTests.Move(driver); await driver.Top();
        Assert.Equal(6, ArmamentMightTests.Might(driver.State));
        var modifier = Assert.Single(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Equal(2, modifier.PowerDelta);
        for (var i = 0; i < 12 && (driver.State.StackItems.Count > 0 || driver.State.FocusPlayerId is not null); i++)
            if (driver.State.PriorityPlayerId is { } priority) await driver.Act(priority, new PassPriorityCommand());
            else await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        Assert.Null(driver.State.FocusPlayerId); Assert.Empty(driver.State.StackItems);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", driver.State.TurnPlayerId);
        Assert.Equal(4, ArmamentMightTests.Might(driver.State));
        Assert.Equal(0, driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifier);
        Assert.Empty(driver.State.CardObjects["HOST"].UntilEndOfTurnPowerModifiers);
        Assert.Contains("D0", driver.State.PlayerZones["P1"].Graveyard);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ActualPrevious1034CatalogCannotBeRelabelledAsTheCurrent1035Inputs()
    {
        var current = RuleInputIdentity.Current;
        const string previous = "c4cc65cfcac0a6761466545876614a0440c70bdc199a81b9e9834f96ed4df1fc";
        Assert.NotEqual(previous, current.CatalogSha256);
        var identity = new RuleInputIdentity(1, "CN", "zh-CN", current.AuthoritySha256, previous, current.ImplementationSha256);
        var state = MatchReplayInitialStateBuilder.FromSeats("prior-1034-under-1035", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" })
            with { RuleInputs = identity };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("prior-catalog", "P1", "PASS"), new PassCommand(), default);
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Equal(ErrorCodes.RecoveryInconsistent, result.ErrorCode);
        Assert.Throws<MatchSessionException>(() => new MatchSession(state, new CoreRuleEngine(), NoopMatchJournal.Instance));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(state, [], state, new CoreRuleEngine(), default);
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
        Assert.Equal(replay.ExpectedStateHash, replay.ReplayedStateHash);
    }
}
