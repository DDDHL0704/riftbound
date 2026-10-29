using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ZedSilentShadowTests
{
    private const string Ability = "NAMED-UNIT-SWAP:0";
    private static readonly string[] Printings = ["VEN·112", "VEN·112a"];

    [Theory]
    [InlineData("VEN·112", 37810)]
    [InlineData("VEN·112a", 37811)]
    public async Task BothOfficialPrintingsRetainTheirCostConquestAndPaidSwiftMovement(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("落影无声", card.SubTitle);
        Assert.Equal(5, card.Energy); Assert.Equal(0, card.ReturnEnergy ?? 0); Assert.Equal(5, card.Power);
        var conquest = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Kind == TriggerKinds.UnitConquestCreateUnitToken);
        Assert.Equal("影分身", conquest.CreatedTokenName); Assert.Equal(0, conquest.CreatedTokenPower);
        var swap = Assert.Single(ActivatedAbilityParser.Parse(card.CardEffect));
        Assert.Equal(ActivatedAbilityKinds.SwiftSwapSelfWithNamedControlledUnit, swap.Kind);
        Assert.Equal(1, swap.ManaCost); Assert.Equal(1, swap.PowerCost); Assert.Equal(RuneTrait.Purple, swap.PowerCostTrait);
        Assert.Equal("影分身", swap.TargetUnitName);
    }

    [Theory]
    [InlineData("VEN·112")]
    [InlineData("VEN·112a")]
    public async Task PlayingTheHeroPaysFiveManaWithoutCreatingAnEntryToken(string no)
    {
        var state = Position(no);
        state = ToHand(state, "HOST");
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P1", new PlayCardCommand("HOST", no, []));
        Assert.Equal(15, driver.State.RunePools["P1"].Mana);
        Assert.Equal(20, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Base);
        Assert.Empty(driver.State.StackItems);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "UNIT_TOKEN_CREATED");
    }

    [Theory]
    [InlineData("VEN·112")]
    [InlineData("VEN·112a")]
    public async Task NaturalConquestCreatesADormantZeroMightCloneOnlyAfterBothPlayersRespond(string no)
    {
        var state = ConquestTokenTriggerTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1") } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand()); await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(1, driver.State.PlayerScores["P1"]);
        Assert.NotNull(Assert.Single(driver.State.StackItems).ConquestToken);
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        var clone = Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        Assert.Equal(0, clone.Power); Assert.True(clone.IsExhausted);
        Assert.Equal("P1", clone.OwnerId); Assert.Equal("P1", clone.ControllerId);
        Assert.Contains(clone.ObjectId, driver.State.PlayerZones["P1"].Base);
    }

    [Theory]
    [InlineData("VEN·112")]
    [InlineData("VEN·112a")]
    public async Task DestroyingZedInResponseDoesNotRemoveHisAlreadyEarnedConquestSkill(string no)
    {
        var state = ConquestTokenTriggerTests.ReactionPosition(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = OfficialGarenConquestBattlefieldCountTests.Printed("UNIT", no, "P1") with { Damage = 4 } } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand()); await driver.Act("P2", new PassFocusCommand());
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·133/298", [])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
        await driver.Top();
        Assert.Single(driver.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
    }

    [Theory]
    [InlineData("VEN·112")]
    [InlineData("VEN·112a")]
    public async Task RepeatedControlWithoutAnotherConquestDoesNotCreateAClone(string no)
    {
        var result = await ConquestLifecycleRegressionTests.Conquer(ConquestTokenTriggerTests.Position(no) with {
            UntilEndOfTurnEffects = [BattlefieldTaskMarkers.ScoreGainedThisTurn("BF", "P1")] });
        Assert.DoesNotContain(result.Events, e => e.Kind == "BATTLEFIELD_CONQUERED");
        Assert.Empty(result.State.StackItems);
        Assert.DoesNotContain(result.State.CardObjects.Values, c => c.TokenFactoryCardNo == "VEN·T05");
    }

    [Fact]
    public async Task AnOpponentsRealMoveOpensTheDuelInWhichZedCanPayHisSwiftSkill()
    {
        var state = OfficialHoldSequenceTests.AddUnit(Position(), "DEF", "OGN·001/298", "OTHER", "P2");
        state = LocalDestructionRecallTests.At(state, "DEF", "P2", "BASE") with {
            ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P2", new MoveUnitCommand("DEF", "BASE", "BATTLEFIELD:OTHER", []));
        await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(TimingStates.SpellDuelOpen, driver.State.TimingState);
        Assert.Equal("P1", driver.State.FocusPlayerId);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability, ["TARGET"])); await driver.Top();
        Assert.Equal("P2", driver.State.TurnPlayerId);
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", driver.State.ObjectLocations["TARGET"].BattlefieldObjectId);
    }

    [Fact]
    public async Task APrintedTextCopyEarnsItsOwnConquestTokenInsteadOfReplacingThePrintedTrigger()
    {
        var state = ArmamentMightTests.Position("SFD·059/221", "VEN·112", 5, attached: true);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = state.CardObjects["OTHER"] with { CardNo = "OGN·296/298" } } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        await driver.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await driver.Act("P1", new PassFocusCommand()); await driver.Act("P2", new PassFocusCommand());
        Assert.Equal(2, driver.State.TriggerQueue.Count(t => t.ConquestToken is not null)
            + driver.State.StackItems.Count(t => t.ConquestToken is not null));
        if (driver.PromptFor("P1").EnabledActions().Contains(CommandTypes.OrderTriggers))
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        while (driver.State.PriorityPlayerId is not null) await driver.Top();
        Assert.Equal(2, driver.State.CardObjects.Values.Count(c => c.TokenFactoryCardNo == "VEN·T05"));
    }

    [Fact]
    public async Task APrintedTextCopyOffersAndResolvesAnIndependentSwiftAbilityInstance()
    {
        var state = ArmamentMightTests.Position("SFD·059/221", "VEN·112", 5, attached: true);
        state = OfficialHoldSequenceTests.AddUnit(state, "TARGET", "VEN·T05", "OTHER", "P1");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T05", out var factory));
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = factory.CreateObject("TARGET", "P1", "P1"),
            ["OTHER"] = state.CardObjects["OTHER"] with { ControllerId = "P1" } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 20 }) } };
        var driver = new VenBasicFlowSpellTests.Driver(state);
        var candidate = driver.PromptFor("P1").Candidates!.First(c => c.Action == CommandTypes.ActivateAbility && c.Enabled);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"])
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "HOST") && Equals(r.GetValueOrDefault("abilityId"), Ability)).ToArray();
        Assert.Equal(2, requirements.Length);
        var copy = Assert.Single(requirements, r => r.GetValueOrDefault("abilityInstanceId") is not null);
        await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability, ["TARGET"],
            AbilityInstanceId: Assert.IsType<string>(copy["abilityInstanceId"])));
        Assert.NotNull(Assert.Single(driver.State.StackItems).NamedUnitSwap!.Origin);
        await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("BASE", driver.State.ObjectLocations["TARGET"].Zone);
        Assert.Equal("HOST", driver.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Theory]
    [InlineData("VEN·112", false)]
    [InlineData("VEN·112a", false)]
    [InlineData("VEN·112", true)]
    [InlineData("VEN·112a", true)]
    public async Task SwiftSwapUsesPreciseBattlefieldsAndEquipmentFollowsItsOriginalHost(string no, bool samePlace)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(no, samePlace));
        Assert.Contains(Ability, JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability, ["TARGET"]));
        Assert.Equal(19, driver.State.RunePools["P1"].Mana);
        Assert.Equal(19, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        Assert.Equal("F", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        await driver.Top();
        Assert.Equal(samePlace ? "F" : "OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("F", driver.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal("TARGET", driver.State.CardObjects["GEAR"].AttachedToObjectId);
        Assert.Equal(driver.State.ObjectLocations["TARGET"], driver.State.ObjectLocations["GEAR"]);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "EQUIPMENT_ATTACHED");
        Assert.Equal(samePlace ? 0 : 2, driver.Events().Count(e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
    }

    [Fact]
    public async Task TheSkillCanBePaidAgainInTheSameTurnWithoutExhaustingEitherUnit()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(samePlace: true));
        for (var i = 0; i < 2; i++) {
            await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability, ["TARGET"])); await driver.Top();
        }
        Assert.Equal(18, driver.State.RunePools["P1"].Mana);
        Assert.Equal(18, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Purple]);
        Assert.False(driver.State.CardObjects["HOST"].IsExhausted);
        Assert.False(driver.State.CardObjects["TARGET"].IsExhausted);
    }

    public static IEnumerable<object[]> Rejections() => Printings.SelectMany(no => new[] {
        "name", "enemy", "face-down", "hand", "self", "two", "missing", "no-mana", "no-purple", "instance", "wrong-turn", "response"
    }.Select(scenario => new object[] { no, scenario }));
    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task IllegalTargetsTimingInstancesAndUnpaidCostsLeaveAllStateAndViewsUnchanged(string no, string scenario)
    {
        var state = Position(no); var command = new ActivateAbilityCommand("HOST", Ability, ["TARGET"]);
        if (scenario == "name") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = OfficialGarenConquestBattlefieldCountTests.Printed("TARGET", "OGN·001/298", "P1") } };
        if (scenario == "enemy") state = LocalDestructionRecallTests.At(state, "TARGET", "P2", "BATTLEFIELD", "OTHER");
        if (scenario == "hand") state = ToHand(state, "TARGET");
        if (scenario == "face-down") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["TARGET"] = state.CardObjects["TARGET"] with { IsFaceDown = true } } };
        if (scenario is "self" or "two" or "missing") command = command with { TargetObjectIds = scenario switch {
            "self" => ["HOST"], "two" => ["TARGET", "GEAR"], _ => ["missing"] } };
        if (scenario == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(0, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 20 }) } };
        if (scenario == "no-purple") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 20 }) } };
        if (scenario == "instance") command = command with { AbilityInstanceId = "invented" };
        if (scenario == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (scenario == "response") state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1" };
        var before = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-zed-swap", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }

    [Theory]
    [InlineData("source-generation")]
    [InlineData("target-generation")]
    [InlineData("control")]
    [InlineData("name")]
    public async Task ResolutionRechecksBothObjectIdentitiesAndTheControlledNameWithoutRefunding(string scenario)
    {
        var activated = await OfficialGraveyardRecastTests.Act(Position(), "P1",
            new ActivateAbilityCommand("HOST", Ability, ["TARGET"]));
        var state = activated.State;
        if (scenario == "control") state = LocalDestructionRecallTests.At(state, "TARGET", "P2", "BATTLEFIELD", "OTHER");
        else {
            var id = scenario == "source-generation" ? "HOST" : "TARGET";
            var changed = state.CardObjects[id];
            changed = scenario == "name" ? changed with { CardNo = "OGN·001/298" }
                : changed with { ObjectGeneration = changed.ObjectGeneration + 1 };
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = changed } };
        }
        var done = await OfficialGraveyardRecastTests.Top(state);
        Assert.Equal("F", done.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal("OTHER", done.State.ObjectLocations["TARGET"].BattlefieldObjectId);
        Assert.Equal(19, done.State.RunePools["P1"].Mana);
        Assert.DoesNotContain(done.Events, e => e.Kind is "UNIT_MOVED_TO_BASE" or "UNIT_MOVED_TO_BATTLEFIELD");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("generation")]
    [InlineData("index")]
    [InlineData("card")]
    [InlineData("tick")]
    [InlineData("instance")]
    public async Task RestorationRejectsForgedSwiftMovementContexts(string scenario)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position());
        await driver.Act("P1", new ActivateAbilityCommand("HOST", Ability, ["TARGET"]));
        var item = Assert.Single(driver.State.StackItems); var c = item.NamedUnitSwap!;
        c = scenario switch {
            "generation" => c with { Source = c.Source with { Generation = 999 } },
            "index" => c with { AbilityIndex = 999 }, "card" => c with { CardNo = "OGN·001/298" },
            "tick" => c with { AcquiredTick = 0 }, "instance" => c with { InstanceId = "invented" }, _ => c };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(driver.State with {
            StackItems = [item with { NamedUnitSwap = scenario == "missing" ? null : c }] }));
    }

    private static MatchState ToHand(MatchState state, string id)
    {
        var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value with {
            Base = p.Value.Base.Where(x => x != id).ToArray(),
            Battlefields = p.Value.Battlefields.Where(x => x != id).ToArray() });
        zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Append(id).ToArray() };
        return state with { PlayerZones = zones, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            [id] = new("P1", "HAND") } };
    }

    private static MatchState Position(string no = "VEN·112", bool samePlace = false)
    {
        var state = AzirMovementAuditTests.AzirPosition(samePlace, no);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo("VEN·T05", out var factory));
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", no, "P1"),
            ["TARGET"] = factory.CreateObject("TARGET", "P1", "P1", isExhausted: false) },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 20 }) } };
    }
}
