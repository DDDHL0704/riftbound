using Riftbound.Contracts;
using Riftbound.CardCatalog;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

// CN UNL-003/219, current core 359.3.f: "here" is checked at execution,
// independently of the battlefield condition captured when the unit was played.
public sealed class BattlefieldPlayDamageTests
{
    internal static async Task<MatchState> Ready(bool enemy = true)
    {
        var state = StandbySourceRulesTests.Position("UNL-003/219");
        state = OfficialHoldSequenceTests.AddUnit(state, "ENEMY-BASE", "OGN·096/298", "F", "P2");
        state = LocalDestructionRecallTests.At(state, "ENEMY-BASE", "P2", "BASE");
        state = await StandbySourceRulesTests.ReadyShadow("UNL-003/219", state);
        // The explicit response-window fixture begins after standby preparation.
        // Putting the opponent here before preparation changes the turn sequence.
        return enemy ? OfficialHoldSequenceTests.AddUnit(state, "ENEMY-HERE", "OGN·096/298", "F", "P2") : state;
    }

    private static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> Attack(
        string? response = null, string responsePlayer = "P2", bool ward = false)
    {
        var initial = StandbySourceRulesTests.Position("UNL-003/219");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY-HERE", "OGN·096/298", "F", "P2");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY-HERE", "P2", "BASE");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ENEMY-BASE", "OGN·096/298", "F", "P2");
        initial = LocalDestructionRecallTests.At(initial, "ENEMY-BASE", "P2", "BASE");
        if (ward)
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY-HERE"] = initial.CardObjects["ENEMY-HERE"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        if (response is not null)
        {
            initial = AttackDamageDistributionTests.Spell(initial, "RESPONSE", response, responsePlayer);
            // Printed runes in the explicit tick0 fixture fund actual resource
            // commands after the turn changes; no mid-journal resource edits.
            var catalog = await OfficialCardCatalog.LoadDefaultAsync();
            var runes = catalog.Cards.Where(c => c.CardCategoryNameList.Contains("符文"))
                .GroupBy(c => c.CardColorList.Single()).Select(g => g.First()).ToArray();
            foreach (var player in new[] { "P1", "P2" })
                foreach (var rune in runes)
                    for (var copy = 0; copy < 2; copy++)
                    {
                        var id = $"FUND-{player}-{rune.CardColorList.Single()}-{copy}";
                        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
                        object?[] args = [id, player, rune.CardNo, null];
                        Assert.True((bool)factory.GetMethod("TryCreate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, args)!);
                        initial = initial with {
                            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { [id] = Assert.IsType<CardObjectState>(args[3]) },
                            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [id] = new(player, "BASE") },
                            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                                [player] = initial.PlayerZones[player] with { Base = [..initial.PlayerZones[player].Base, id] } } };
                    }
        }
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new HideCardCommand("SHADOW", "UNL-003/219", "BATTLEFIELD:F", ["STANDBY_A"]));
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal("P2", driver.State.TurnPlayerId);
        await driver.Act("P2", new MoveUnitCommand("ENEMY-HERE", "BASE", "BATTLEFIELD:F", []));
        await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new PlayCardCommand("SHADOW", "UNL-003/219", []));
        return (initial, driver);
    }

    private static async Task Fund(StandbyDefendObserverTests.Driver driver, string player)
    {
        var ids = driver.State.PlayerZones[player].Base.Where(id => id.StartsWith("FUND-", StringComparison.Ordinal)).ToArray();
        foreach (var id in ids) await driver.Act(player, new TapRuneCommand(id));
        foreach (var id in ids) await driver.Act(player, new RecycleRuneCommand(id));
    }

    private static Task<ResolutionResult> Choose(StandbyDefendObserverTests.Driver driver)
        => driver.Act("P1", new ChooseCardsCommand(driver.State.PendingCardChoice!.ChoiceId,
            driver.State.PendingCardChoice.ChoiceWindow, ["ENEMY-HERE"]));

    private static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Fact]
    public async Task StandbyDamageChoosesOnlyCurrentEnemyHereAndColdReplays()
    {
        var (initial, driver) = await Attack();
        Assert.False(driver.State.CardObjects["SHADOW"].IsFaceDown);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "ENEMY-HERE" }, choice.LegalObjectIds);
        Assert.Equal(1, choice.RequiredCount);
        Assert.Null(driver.State.PriorityPlayerId);
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY-HERE"]));
        Assert.Equal(0, driver.State.CardObjects["ENEMY-HERE"].Damage);
        var result = await driver.Top();
        Assert.Equal(2, result.State.CardObjects["ENEMY-HERE"].Damage);
        Assert.Equal(0, result.State.CardObjects["ENEMY-BASE"].Damage);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task NoEnemyHereDoesNotLeaveADeadConfirmationAfterTheUnitEnters()
    {
        var initial = await Ready(enemy: false);
        var result = await Act(initial, "P1", new PlayCardCommand("SHADOW", "UNL-003/219", []));
        Assert.Contains("SHADOW", result.State.PlayerZones["P1"].Battlefields);
        Assert.Null(result.State.PendingCardChoice);
        Assert.DoesNotContain(result.State.StackItems, i => i.SourceObjectId == "SHADOW");
        Assert.Contains(result.Events, e => e.Kind == "TRIGGER_NOT_CONFIRMED");
        Restore(result.State);
        ArmamentMightTests.PublicRecovery(result.State);
    }

    [Theory]
    [InlineData("SHADOW", "P1")]
    [InlineData("ENEMY-HERE", "P2")]
    public async Task ActualResponseMovingSourceOrTargetToBaseInvalidatesHere(string movedId, string player)
    {
        var (initial, driver) = await Attack("OGS·011/024", player);
        await Choose(driver);
        var binding = Assert.Single(driver.State.StackItems).PermanentPlaySource;
        if (player == "P2") await driver.Act("P1", new PassPriorityCommand());
        await Fund(driver, player);
        await driver.Act(player, new PlayCardCommand("RESPONSE", "OGS·011/024", [movedId]));
        await driver.Top();
        Assert.Equal("BASE", driver.State.ObjectLocations[movedId].Zone);
        Assert.Equal(binding, Assert.Single(driver.State.StackItems).PermanentPlaySource);
        await driver.Top();
        Assert.Equal(0, driver.State.CardObjects["ENEMY-HERE"].Damage);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task ActualSourceReturnToHandKeepsTheAbilityButCannotReuseItsOldHere()
    {
        var (initial, driver) = await Attack("OGN·169/298");
        await Choose(driver);
        var binding = Assert.Single(driver.State.StackItems).PermanentPlaySource!;
        await driver.Act("P1", new PassPriorityCommand());
        await Fund(driver, "P2");
        await driver.Act("P2", new PlayCardCommand("RESPONSE", "OGN·169/298", ["SHADOW"]));
        var returned = await driver.Top();
        Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["SHADOW"].ObjectGeneration > binding.Generation);
        Assert.Equal(binding, Assert.Single(driver.State.StackItems).PermanentPlaySource);
        var currentPrivate = driver.State with { StackItems = [Assert.Single(driver.State.StackItems) with {
            PermanentPlaySource = binding with { Generation = driver.State.CardObjects["SHADOW"].ObjectGeneration } }] };
        Assert.ThrowsAny<Exception>(() => FullGameEndToEndTests.AssertNoHiddenZoneLeak(returned with {
            State = currentPrivate, Snapshots = ResolutionResult.BuildSnapshots(currentPrivate) }));
        await driver.Top();
        Assert.Equal(0, driver.State.CardObjects["ENEMY-HERE"].Damage);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WardIsConfirmedAfterEntryAndDecliningDoesNotUndoTheUnit(bool pay)
    {
        var (initial, driver) = await Attack("OGS·011/024", ward: true);
        await Choose(driver);
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, driver.State.CardObjects["ENEMY-HERE"].Damage);
        if (pay) await Fund(driver, "P1");
        await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await driver.Top();
        Assert.Equal(pay ? 2 : 0, driver.State.CardObjects["ENEMY-HERE"].Damage);
        Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Empty(driver.State.StackItems);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("HOST")]
    [InlineData("ENEMY-BASE")]
    [InlineData("UNKNOWN")]
    public async Task IllegalConfirmationDoesNotChangeStateOrHistory(string target)
    {
        var (_, driver) = await Attack();
        var before = driver.State;
        var hash = MatchStateHasher.Hash(before);
        var choice = before.PendingCardChoice!;
        var rejected = await new CoreRuleEngine().ResolveAsync(before, new("bad", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
        Assert.Equal(0, before.CardObjects["ENEMY-HERE"].Damage);
    }

    [Fact]
    public async Task AnActualCounterCancelsConfirmedDamageWithoutRemovingTheEnteredUnit()
    {
        var (initial, driver) = await Attack("SFD·045/221");
        await Choose(driver);
        var item = Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PassPriorityCommand());
        await Fund(driver, "P2");
        await driver.Act("P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [item.StackItemId]));
        var countered = await driver.Top();
        Assert.DoesNotContain(countered.State.StackItems, i => i.StackItemId == item.StackItemId);
        Assert.Contains("SHADOW", countered.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(0, countered.State.CardObjects["ENEMY-HERE"].Damage);
        Assert.Contains(countered.Events, e => e.Kind == "STACK_ITEM_COUNTERED");
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("other")]
    [InlineData("future")]
    [InlineData("previous")]
    public async Task IndependentRecoveryRejectsForgedHereSourceBindings(string kind)
    {
        var (_, driver) = await Attack();
        var item = Assert.Single(driver.State.StackItems);
        var binding = item.PermanentPlaySource!;
        var fake = kind switch {
            "missing" => null,
            "other" => binding with { ObjectId = "HOST" },
            "future" => binding with { Generation = binding.Generation + 1 },
            _ => binding with { Generation = binding.Generation - 1 }
        };
        var errors = MinimumMightAuditTests.PublicErrors(driver.State with {
            StackItems = [item with { PermanentPlaySource = fake }] });
        Assert.Contains(errors, e => e.Contains("battlefield-only permanent play trigger origin", StringComparison.Ordinal));
    }
}
