using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadDisarmUnitTests
{
    public static IEnumerable<object[]> Cards() => new object[][] {
        ["RAD·050/167", 40122L, "神秘的时间法师", 5, 4, 2],
        ["RAD·054/167", 40126L, "饰品商贩", 3, 3, 1],
        ["RAD·055/167", 40127L, "叮响巡警", 3, 2, 1],
        ["RAD·093/167", 40171L, "处置专家", 4, 3, 2],
        ["RAD·097/167", 40175L, "浅滩海妖", 2, 1, 1]
    };
    public static IEnumerable<object[]> Numbers() => Cards().Select(row => new[] { row[0] });
    public static IEnumerable<object[]> InvalidPlays() => Numbers().SelectMany(row =>
        new[] { "no-mana", "extra-target", "wrong-turn" }.Select(branch => new[] { row[0], branch }));
    public static IEnumerable<object[]> WardRows() => Cards().SelectMany(row =>
        new[] { true, false }.Select(pay => new object[] { row[0], row[5], pay }));

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task OfficialPrintingPaysItsCostAndResolvesItsCompletePlayText(
        string no, long id, string name, int mana, int might, int disarm)
    {
        var source = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, source.Id); Assert.Equal(name, source.CardName);
        Assert.Equal(mana, source.Energy); Assert.Equal(might, source.Power); Assert.Null(source.ReturnEnergy);
        Assert.True(DisarmSourceSpecifications.TryGet(no, out var specification));
        Assert.Equal(source.CardEffect, specification.EffectiveText);
        Assert.Equal(disarm, specification.PrintedValue + specification.FriendlyGrants.Sum(g => g.Value));
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        if (no == "RAD·097/167") Assert.Contains("游走", game.State.CardObjects["UNIT"].Tags);
        if (no == "RAD·050/167")
        {
            Assert.Single(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
            await game.Top();
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow);
            Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, choice.LegalObjectIds);
            Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(game.State)["P2"]));
            await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-3"]));
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"]));
            Assert.Equal(new[] { "P1-DRAW-2", "P1-DRAW-3", "P1-DRAW-1" }, game.State.PlayerZones["P1"].MainDeck);
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task IllegalUnitPlayDoesNotSpendResourcesOrChangePrivateOrPublicState(string no, string branch)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (branch == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no,
            branch == "extra-target" ? ["ALLY"] : [], Destination: "BASE"));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-disarm", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    internal static MatchState Board(string no, bool copied = false)
    {
        var state = AttackDamageDistributionTests.Position(no, copied);
        var printed = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { Tags = printed.Tags } } };
    }

    private static MatchState AddPatroller(MatchState state, string id, string player = "P1", string zone = "BASE", bool hidden = false)
    {
        var card = OfficialGarenConquestBattlefieldCountTests.Printed(id, "RAD·055/167", player) with { IsFaceDown = hidden };
        var zones = state.PlayerZones.ToDictionary();
        zones[player] = zone == "BASE" ? zones[player] with { Base = [..zones[player].Base, id] }
            : zones[player] with { Hand = [..zones[player].Hand, id] };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = card },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(player, zone) } };
    }

    private static async Task Attack(Driver game)
        => await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));

    private static async Task Choose(Driver game, string target = "ENEMY1")
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
    }

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task AttackChoosesOneLocalEnemyThenLowersMightAfterBothPlayersRespond(
        string no, long id, string name, int mana, int might, int disarm)
    {
        _ = id; _ = name; _ = mana; _ = might;
        var initial = Board(no); var before = initial.CardObjects["ENEMY1"].Power;
        var game = new Driver(initial); await Attack(game);
        Assert.True(game.State.CardObjects["HOST"].IsAttacking);
        Assert.Equal(before, game.State.CardObjects["ENEMY1"].Power);
        Assert.Single(game.State.StackItems);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(new[] { "ENEMY1", "ENEMY2", "ENEMY3" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        await Choose(game); Assert.Equal(before, game.State.CardObjects["ENEMY1"].Power);
        await game.Top();
        Assert.Equal(before - disarm, game.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(before, game.State.CardObjects["ENEMY2"].Power);
        Assert.Single(game.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [MemberData(nameof(WardRows))]
    public async Task EveryUnitPaysTheChosenTargetsWardOrDeclinesItsSkill(string no, int amount, bool pay)
    {
        var initial = Board(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Tags = [..initial.CardObjects["ENEMY1"].Tags, "法盾2"] } } };
        var game = new Driver(initial); await Attack(game); await Choose(game);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(2, payment.PowerCost);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await game.Top();
        Assert.Equal(20 - (pay ? 2 : 0), game.State.RunePools["P1"].TotalPower);
        Assert.Equal(initial.CardObjects["ENEMY1"].Power - (pay ? amount : 0), game.State.CardObjects["ENEMY1"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·050/167")] [InlineData("RAD·054/167")]
    [InlineData("RAD·055/167")] [InlineData("RAD·093/167")] [InlineData("RAD·097/167")]
    public async Task AReturnedSourceInvalidatesHereAndKeepsItsHandIdentityPrivate(string no)
    {
        var game = new Driver(AttackDamageDistributionTests.Spell(Board(no), "RETURN", "OGN·104/298", "P1"));
        var before = game.State.CardObjects["ENEMY1"].Power;
        await Attack(game); await Choose(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("\"HOST\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal(before, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
    }

    [Fact]
    public async Task PatrollersAddTheirValuesAcrossLocationsButStillCreateExactlyOneSkill()
    {
        var initial = AddPatroller(Board("RAD·055/167"), "OTHER-PATROLLER");
        var game = new Driver(initial); await Attack(game);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal(2, item.FieldContext!.Disarm!.Contributions.Sum(g => g.Value));
        Assert.Contains("缴械2", game.State.CardObjects["HOST"].Tags);
        await Choose(game); await game.Top();
        Assert.Equal(initial.CardObjects["ENEMY1"].Power - 2, game.State.CardObjects["ENEMY1"].Power);
        Assert.Single(game.State.CardObjects["ENEMY1"].UntilEndOfTurnPowerModifiers); await game.Replay();
    }

    [Theory]
    [InlineData("P2", "BASE", false)] [InlineData("P1", "HAND", false)] [InlineData("P1", "BASE", true)]
    public async Task EnemyHiddenAndOutOfPlayPatrollersDoNotGrantDisarm(string player, string zone, bool hidden)
    {
        var game = new Driver(AddPatroller(Board("RAD·055/167"), "OTHER-PATROLLER", player, zone, hidden));
        await Attack(game); Assert.Equal(1, Assert.Single(game.State.StackItems).FieldContext!.Disarm!.Contributions.Sum(g => g.Value));
        await Choose(game); await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task APatrollerGrantsAnotherMechanicalUnitItsOwnAttackSkill()
    {
        var initial = AddPatroller(Board("OGN·088/298"), "PATROLLER");
        var game = new Driver(initial); await Attack(game);
        Assert.Equal("HOST", Assert.Single(game.State.StackItems).SourceObjectId);
        await Choose(game); await game.Top();
        Assert.Equal(initial.CardObjects["ENEMY1"].Power - 1, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
    }

    [Fact]
    public async Task APatrollerAlsoGrantsDisarmToAnOfficialRobotToken()
    {
        var initial = AddPatroller(Board("OGN·088/298"), "PATROLLER");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("SFD·T01", out var robot));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = robot.CreateObject("HOST", "P1", "P1") } };
        var game = new Driver(initial); await Attack(game);
        Assert.Equal("SFD·T01", Assert.Single(game.State.StackItems).CardNo);
        await Choose(game); await game.Top();
        Assert.Equal(initial.CardObjects["ENEMY1"].Power - 1, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
    }

    [Fact]
    public async Task APatrollerDoesNotGrantDisarmToANonMechanicalUnit()
    {
        var game = new Driver(AddPatroller(Board("OGN·219/298"), "PATROLLER")); await Attack(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.DoesNotContain("缴械", game.State.CardObjects["HOST"].Tags); await game.Replay();
    }

    [Fact]
    public async Task RemovingTheGrantingPatrollerAfterConfirmationDoesNotCancelTheCapturedValue()
    {
        var initial = AttackDamageDistributionTests.Spell(AddPatroller(Board("RAD·055/167"), "OTHER-PATROLLER"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); await Attack(game); await Choose(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["OTHER-PATROLLER"])); await game.Top();
        Assert.Contains("缴械", game.State.CardObjects["HOST"].Tags);
        await game.Top(); Assert.Equal(initial.CardObjects["ENEMY1"].Power - 2, game.State.CardObjects["ENEMY1"].Power); await game.Replay();
    }

    [Fact]
    public async Task CopiedPrintedDisarmAddsToTheOneCombinedSkill()
    {
        var initial = Board("RAD·054/167", copied: true);
        var game = new Driver(initial); await Attack(game);
        Assert.Equal(2, Assert.Single(game.State.StackItems).FieldContext!.Disarm!.Contributions.Sum(g => g.Value));
        await Choose(game); await game.Top(); Assert.Equal(initial.CardObjects["ENEMY1"].Power - 2, game.State.CardObjects["ENEMY1"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("value")] [InlineData("duplicate")] [InlineData("future")] [InlineData("identity")]
    public async Task RecoveryRejectsForgedDisarmContributions(string corruption)
    {
        var game = new Driver(Board("RAD·054/167")); await Attack(game);
        var item = Assert.Single(game.State.StackItems); var origin = item.FieldContext!.Disarm!;
        var contribution = Assert.Single(origin.Contributions);
        var forged = corruption switch {
            "value" => origin with { Contributions = [contribution with { Value = 9 }] },
            "duplicate" => origin with { Contributions = [contribution, contribution] },
            "future" => origin with { CapturedTick = game.State.Tick + 1 },
            _ => origin with { Contributions = [contribution with { CardNo = "RAD·097/167" }] }
        };
        Assert.NotEmpty(MatchRecoveryValidator.ValidateComponentStateAndViews(game.State with {
            StackItems = [item with { FieldContext = item.FieldContext with { Disarm = forged } }] }));
    }

    [Fact]
    public async Task DisarmPreservesNegativeMightAndExposesZeroEffectiveMight()
    {
        var initial = Board("RAD·050/167");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY1"] = initial.CardObjects["ENEMY1"] with { Power = 1 } } };
        var game = new Driver(initial); await Attack(game); await Choose(game); await game.Top();
        // CN 143.2.c/143.2.c.1 retains the negative value for later modifiers.
        Assert.Equal(-1, game.State.CardObjects["ENEMY1"].Power);
        Assert.Equal(0, CurrentPowerTargetTests.DisplayPower(game.State, "ENEMY1")); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DisposalExpertChecksOtherControlledUnitsAcrossTheWholeField(bool otherUnit)
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·093/167");
        if (otherUnit) initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        else initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = ["GEAR"], Graveyard = ["GRAVE", "ALLY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["ALLY"] = new("P1", "GRAVEYARD") } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·093/167", [], Destination: "BASE"));
        Assert.Equal(otherUnit, game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }

    [Fact]
    public async Task TheSirensPrintedRoamAllowsMovementBetweenBattlefields()
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·097/167");
        initial = LocalDestructionRecallTests.At(initial, "UNIT", "P1", "BATTLEFIELD", "BF");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = [] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["UNIT"] = initial.CardObjects["UNIT"] with { Tags = [..initial.CardObjects["UNIT"].Tags, "游走"] } } };
        var game = new Driver(initial);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD:BF", "BATTLEFIELD:OTHER", ["ROAM"]));
        Assert.Equal("OTHER", game.State.ObjectLocations["UNIT"].BattlefieldObjectId);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); await game.Replay();
    }
}
