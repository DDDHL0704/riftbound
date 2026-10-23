using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OptionalEntryTargetLifecycleTests
{
    [Theory]
    [InlineData("SFD·013/221", "red")]
    [InlineData("SFD·067/221", "blue")]
    [InlineData("VEN·120", "yellow")]
    public async Task ExhaustingTheLastColoredPowerOnEntryCannotAlsoPayWardOrUndoThePermanentEntry(string no, string color)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(20, 0, new Dictionary<string, int> { [color] = 1 }) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [..initial.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] } } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        if (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        if (driver.State.PendingPayment is { } payment) {
            await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
            await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        }
        Assert.Empty(driver.State.StackItems); Assert.Null(driver.State.PendingCardChoice); Assert.Null(driver.State.PendingPayment);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(20 - (no == "VEN·120" ? 4 : 3), driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221", 2)]
    [InlineData("SFD·067/221", 1)]
    public async Task ThePrintedDamageOrReductionAppliesStateBasedDestructionToAnAlreadyWoundedUnit(string no, int damage)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = no == "SFD·013/221" ? initial.CardObjects["ENEMY"] with { Damage = damage }
                : OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·010/298", "P2") with { Damage = damage } } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top();
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_DESTROYED"); await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·120", "OGN·219/298")]
    [InlineData("SFD·067/221", "OGN·010/298")]
    [InlineData("SFD·067/221", "OGN·271/298")]
    public async Task ARealBattleUsesZeroDamageContributionFromThePrintedStunOrNonpositiveMight(string no, string enemyNo)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", enemyNo, "P2") } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"])); await driver.Top();
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        await driver.Act("P1", new MoveUnitCommand("ALLY", "BASE", "BATTLEFIELD:OTHER", []));
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        await driver.Act(driver.State.FocusPlayerId!, new PassFocusCommand());
        try {
            await driver.Act("P1", new DeclareBattleCommand("OTHER", ["ALLY"], ["ENEMY"], ["COMBAT_ASSIGNMENT"]));
        } catch {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_OPTIONAL_ENTRY_EVIDENCE") is { Length: > 0 } root) {
                Directory.CreateDirectory(root);
                var sequence = driver.Events().Length; var snapshots = ResolutionResult.BuildSnapshots(driver.State);
                var prompts = ResolutionResult.BuildPrompts(driver.State);
                var views = driver.State.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, driver.State.Tick, sequence, snapshots[p], driver.State.Tick, sequence, prompts[p]));
                var spectator = MatchReplayRedactor.BuildSpectatorFrame(driver.State.RoomId, driver.State.Tick, sequence, [], driver.State);
                var errors = MatchRecoveryValidator.Validate(driver.State.RoomId, sequence, driver.Commands(), driver.Events(), views, driver.State, driver.State.Tick, spectator);
                var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State, new CoreRuleEngine(), default, driver.Events());
                File.WriteAllText(Path.Combine(root, "battle-prefix-failure.json"), JsonSerializer.Serialize(new {
                    initial, final = driver.State, commands = driver.Commands(), events = driver.Events(), errors, replay
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            }
            throw;
        }
        for (var i = 0; i < 2 && driver.State.BattleState.IsActive; i++) {
            var player = driver.State.ActivePlayerId;
            await driver.Act(player, new AssignCombatDamageCommand("battle:OTHER", "OTHER",
                player == "P1" ? [new("ALLY", "ENEMY", 4)] : []));
        }
        Assert.False(driver.State.BattleState.IsActive);
        Assert.DoesNotContain("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        Assert.Equal(0, driver.State.CardObjects["ALLY"].Damage);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "ENEMY"));
        await driver.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task ACounteredPaidEntrySkillLeavesTheEnteredSourceAndItsPaidCostIntact(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(OptionalEntryTargetConfirmationTests.Position(no), "COUNTER", "SFD·045/221", "P2");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var skill = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "SFD·045/221", [skill.StackItemId])); await driver.Top();
        Assert.Empty(driver.State.StackItems); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(5, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(20 - (no == "VEN·120" ? 4 : 3), driver.State.RunePools["P1"].Mana);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        var countered = Assert.Single(driver.Events(), e => e.Event.Kind == "STACK_ITEM_COUNTERED");
        Assert.Equal("ABILITY", countered.Event.Payload["stackObjectKind"]);
        Assert.Equal(true, countered.Event.Payload["abilitySourcePreserved"]); await driver.Replay();
    }

    [Theory]
    [InlineData("SFD·013/221")]
    [InlineData("SFD·067/221")]
    [InlineData("VEN·120")]
    public async Task ThePaidSkillStillResolvesAfterItsPublicSourceReturnsToHand(string no)
    {
        var initial = OptionalEntryTargetConfirmationTests.AddSpell(OptionalEntryTargetConfirmationTests.Position(no), "RETURN", "OGN·104/298");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BATTLEFIELD:BF"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var generation = driver.State.CardObjects["UNIT"].ObjectGeneration;
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["UNIT"].ObjectGeneration > generation);
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(no == "SFD·013/221" ? 2 : 0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(no == "SFD·067/221" ? 2 : 4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(no == "VEN·120", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await driver.Replay();
    }

    public static IEnumerable<object[]> WardCases() => OptionalEntryTargetBoundaryTests.Cards.SelectMany(no => new[] { false, true }.Select(pay => new object[] { no, pay }));

    [Theory]
    [MemberData(nameof(WardCases))]
    public async Task WardIsASeparatePostEntryPaymentAndDecliningItDoesNotRefundTheOptionalEntryCost(string no, bool pay)
    {
        var initial = OptionalEntryTargetConfirmationTests.Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [..initial.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] } } };
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost); Assert.Equal(5, driver.State.RunePools["P1"].TotalPower);
        await OptionalEntryTargetBoundaryTests.Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base); Assert.Empty(driver.State.StackItems);
        Assert.Equal(20 - (no == "VEN·120" ? 4 : 3), driver.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 4 : 5, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(pay && no == "SFD·013/221" ? 2 : 0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(pay && no == "SFD·067/221" ? 2 : 4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(pay && no == "VEN·120", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await driver.Replay();
    }

    public static IEnumerable<object[]> OrderedCases() => OptionalEntryTargetBoundaryTests.Cards.SelectMany(no => new[] {
        new object[] { no, false, false }, new object[] { no, true, false }, new object[] { no, true, true } });

    [Theory]
    [MemberData(nameof(OrderedCases))]
    public async Task ThePaidConditionControlsOnlyThePrintedSkillWhileRengarRemainsIndependent(string no, bool paid, bool legendFirst)
    {
        var driver = new Driver(RengarUnitPlayConfirmationTests.WithLegend(OptionalEntryTargetConfirmationTests.Position(no)));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: paid ? OptionalEntryTargetConfirmationTests.Costs(no) : [], Destination: "BASE"));
        if (paid) {
            var own = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "UNIT");
            var legend = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: legendFirst ? [legend.TriggerId, own.TriggerId] : [own.TriggerId, legend.TriggerId]));
        }
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.SourceObjectId == "LEGEND" ? "UNIT" : "ENEMY"]));
        Assert.Equal(paid ? 2 : 1, driver.State.StackItems.Count);
        if (paid) {
            await driver.Top();
            Assert.Equal(legendFirst ? 1 : 0, driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifier);
        }
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(1, driver.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifier);
        Assert.Equal(paid && no == "SFD·013/221" ? 2 : 0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(paid && no == "SFD·067/221" ? 2 : 4, driver.State.CardObjects["ENEMY"].Power);
        Assert.Equal(paid && no == "VEN·120", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        await driver.Replay();
    }

    public static IEnumerable<object[]> DepartureCases() => OptionalEntryTargetBoundaryTests.Cards.SelectMany(no => new[] { false, true }.Select(reenter => new object[] { no, reenter }));

    [Theory]
    [MemberData(nameof(DepartureCases))]
    public async Task AConfirmedTargetThatLeavesAndOptionallyReentersCannotBeHitAsTheOldObject(string no, bool reenter)
    {
        var initial = AttackDamageDistributionTests.Spell(OptionalEntryTargetConfirmationTests.Position(no), "RETURN", reenter ? "UNL-184/219" : "OGN·104/298", "P2");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], OptionalCosts: OptionalEntryTargetConfirmationTests.Costs(no), Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var old = driver.State.CardObjects["ENEMY"].ObjectGeneration;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("RETURN", reenter ? "UNL-184/219" : "OGN·104/298", ["ENEMY"])); await driver.Top();
        Assert.True(driver.State.CardObjects["ENEMY"].ObjectGeneration > old);
        if (reenter) {
            Assert.NotNull(driver.State.PendingEffectPlay);
            await driver.Act("P2", new PlayCardCommand("ENEMY", "OGN·219/298", [], Destination: "BATTLEFIELD:OTHER"));
            Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Battlefields);
        } else Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Hand);
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(4, driver.State.CardObjects["ENEMY"].Power); Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.DoesNotContain("STUNNED", driver.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(5, driver.State.RunePools["P1"].TotalPower);
        await driver.Replay();
    }
}
