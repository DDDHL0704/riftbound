using System.Text;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class KennenConquestFlowTests
{
    [Theory]
    [InlineData("VEN·113", 37812)]
    [InlineData("VEN·113a", 37813)]
    public async Task BothOfficialPrintingsAreAdmittedWithTheirExactCostsAndCompleteEffects(string no, long id)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("万雷倾泻", card.SubTitle);
        Assert.Equal(3, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(4, card.Power);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.True(behavior.GrantsPrintedCostFlowOnConquest);
        Assert.Equal(2, behavior.BurnCount); Assert.Equal(3, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(1, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.Equal(new[] { RuneTrait.Purple }, PrintedPowerCostRules.ForCard(no).Traits);
    }

    [Theory]
    [InlineData("VEN·113")]
    [InlineData("VEN·113a")]
    public async Task PlayConfirmsTheHeroBeforeItsIndependentBurnAndDoesNotDraw(string no)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(no, inHand: true));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(3, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.True(Assert.Single(driver.State.StackItems).SourceConfirmed);
        Assert.Equal(27, driver.State.RunePools["P1"].Mana); Assert.Equal(29, driver.State.RunePools["P1"].Power);
        await driver.Top();
        Assert.Equal(new[] { "P1-D3" }, driver.State.PlayerZones["P1"].MainDeck);
        Assert.Contains("P1-D1", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("P1-D2", driver.State.PlayerZones["P1"].Graveyard);
        Assert.All(new[] { "P1-D1", "P1-D2" }, id => Assert.Equal(1, driver.State.CardObjects[id].ObjectGeneration));
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        Assert.Equal(2, driver.Events().Where(e => e.Event.Kind == "CARDS_BURNED").Sum(e => Convert.ToInt32(e.Event.Payload["count"])));
    }

    [Theory]
    [InlineData(1, 1, 3)]
    [InlineData(0, 8, 0)]
    public async Task BurnUsesTheSharedBurnoutRulesIncludingRepeatedBurnOfTheSamePhysicalCard(int deckCount, int score, long generation)
    {
        var initial = Position(inHand: true);
        var removed = initial.PlayerZones["P1"].MainDeck.Skip(deckCount).Append("SPELL").ToHashSet(StringComparer.Ordinal);
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                MainDeck = initial.PlayerZones["P1"].MainDeck.Take(deckCount).ToArray(), Graveyard = [] } },
            CardObjects = initial.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary(),
            ObjectLocations = initial.ObjectLocations.Where(p => !removed.Contains(p.Key)).ToDictionary() };
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·113", [])); await driver.Top();
        Assert.Equal(score, driver.State.PlayerScores["P2"]);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        if (deckCount == 0) { Assert.Equal("P2", driver.State.WinnerPlayerId); Assert.Equal(MatchStatuses.Finished, driver.State.Status); }
        else {
            Assert.Equal(generation, driver.State.CardObjects["P1-D1"].ObjectGeneration);
            Assert.Equal(new[] { "P1-D1" }, driver.State.PlayerZones["P1"].Graveyard);
            Assert.Null(driver.State.WinnerPlayerId);
        }
    }

    [Theory]
    [InlineData("VEN·113")]
    [InlineData("VEN·113a")]
    public async Task ConquestConfirmsOneGraveyardSpellThenGrantsFlowOnlyAfterResponses(string no)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(no)); await Conquer(driver);
        var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
        Assert.Equal(new[] { "SPELL" }, choice.LegalObjectIds); Assert.Equal(1, choice.RequiredCount);
        Assert.DoesNotContain("FLOW_COST:", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await Choose(driver, "SPELL");
        Assert.Empty(FlowGrants(driver.State)); await driver.Top(); Assert.Single(FlowGrants(driver.State));
        var command = new PlayCardCommand("SPELL", "OGN·048/298", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana);
        Assert.Contains("FLOW_COST:", JsonSerializer.Serialize(driver.PromptFor("P1")));
        await driver.Act("P1", command);
        Assert.NotNull(Assert.Single(driver.State.StackItems).FlowPlay!.Grant);
        await driver.Top(); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
        Assert.Equal(new[] { "P1-D1" }, driver.State.PlayerZones["P1"].Hand);
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("unit")]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("wrong-player")]
    public async Task IllegalConquestTargetsCannotMutateTheState(string branch)
    {
        var initial = Add(Position(), "FOREIGN", "OGN·048/298", "P2", "GRAVEYARD");
        initial = Add(initial, "DEAD-UNIT", "OGN·219/298", "P1", "GRAVEYARD");
        var driver = new VenBasicFlowSpellTests.Driver(initial); await Conquer(driver);
        var choice = driver.State.PendingCardChoice!;
        var ids = branch switch { "opponent" => new[] { "FOREIGN" }, "unit" => ["DEAD-UNIT"],
            "empty" => [], "duplicate" => ["SPELL", "SPELL"], _ => ["SPELL"] };
        await Reject(driver.State, branch == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids));
    }

    [Fact]
    public async Task NoGraveyardSpellMeansThereIsNoConquestSkillToConfirm()
    {
        var initial = Position();
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { Graveyard = [], Banished = ["SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SPELL"] = new("P1", "BANISHED") } };
        var driver = new VenBasicFlowSpellTests.Driver(initial); await Conquer(driver);
        Assert.Null(driver.State.PendingCardChoice); Assert.Empty(driver.State.StackItems); Assert.Empty(FlowGrants(driver.State));
    }

    [Theory]
    [InlineData("VEN·113", false)]
    [InlineData("VEN·113a", true)]
    public async Task PrintedAndGrantedFlowCostsRemainSelectableAndQuotedIndependently(string no, bool printed)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(no, spellNo: "VEN·003"));
        await Conquer(driver); await Choose(driver, "SPELL"); await driver.Top();
        var cost = printed ? "FLOW_COST:4:1:red" : "FLOW_COST:2:1:red";
        var command = new PlayCardCommand("SPELL", "VEN·003", ["GEAR"], OptionalCosts: [cost]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(printed ? 4 : 2, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Red]);
        var prompt = JsonSerializer.Serialize(driver.PromptFor("P1"));
        Assert.Contains("FLOW_COST:4:1:red", prompt); Assert.Contains("FLOW_COST:2:1:red", prompt);
        await driver.Act("P1", command);
        Assert.Equal(printed, Assert.Single(driver.State.StackItems).FlowPlay!.Grant is null);
        await driver.Top(); Assert.Contains("GEAR", driver.State.PlayerZones["P2"].Graveyard);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
    }

    [Fact]
    public async Task EqualPrintedAndGrantedResourceCostsOfferOnlyOneFlowChoice()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(spellNo: "VEN·049"));
        await Conquer(driver); await Choose(driver, "SPELL"); await driver.Top();
        var action = Assert.Single(driver.PromptFor("P1").Candidates!, a => a.Action == CommandTypes.PlayCard);
        var metadata = JsonSerializer.SerializeToElement(action.Metadata);
        var requirement = Assert.Single(metadata.GetProperty("sourceRequirements").EnumerateArray(),
            r => r.GetProperty("sourceObjectId").GetString() == "SPELL");
        Assert.Single(requirement.GetProperty("flowCostChoices").EnumerateArray());
        await driver.Act("P1", new PlayCardCommand("SPELL", "VEN·049", [], OptionalCosts: ["FLOW_COST:2:0:"]));
        await driver.Top(); Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
    }

    [Fact]
    public async Task GrantedFlowRetainsTheOriginalExtraCostAndCounteredSpellStillBanishes()
    {
        var initial = Add(Position(), "COUNTER", "OGN·045/298", "P2", "HAND");
        initial = Add(initial, "HELPER", "OGN·219/298", "P1", "BASE");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await Conquer(driver); await Choose(driver, "SPELL"); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:HELPER"]));
        Assert.True(driver.State.CardObjects["HELPER"].IsExhausted);
        var item = Assert.Single(driver.State.StackItems); Assert.NotNull(item.FlowPlay);
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [item.StackItemId]));
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished); Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        Assert.True(driver.State.CardObjects["HELPER"].IsExhausted);
    }

    [Theory]
    [InlineData("invented")]
    [InlineData("two")]
    [InlineData("duplicate")]
    [InlineData("timing")]
    [InlineData("unaffordable")]
    public async Task FlowSelectionPreservesTimingAndPaymentAndRejectsInvalidChoices(string branch)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position(spellNo: "VEN·003"));
        await Conquer(driver); await Choose(driver, "SPELL"); await driver.Top();
        var state = driver.State;
        var choices = branch switch { "invented" => new[] { "FLOW_COST:0:0:" },
            "two" => ["FLOW_COST:2:1:red", "FLOW_COST:4:1:red"],
            "duplicate" => ["FLOW_COST:2:1:red", "FLOW_COST:2:1:red"], _ => ["FLOW_COST:2:1:red"] };
        if (branch == "timing") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "unaffordable") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(state, "P1", new PlayCardCommand("SPELL", "VEN·003", ["GEAR"], OptionalCosts: choices));
    }

    [Fact]
    public async Task TargetThatChangesGenerationBeforeResolutionDoesNotReceiveFlow()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position());
        await Conquer(driver); await Choose(driver, "SPELL");
        var state = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
            ["SPELL"] = driver.State.CardObjects["SPELL"] with { ObjectGeneration = driver.State.CardObjects["SPELL"].ObjectGeneration + 2 } } };
        var done = await OfficialGraveyardRecastTests.Top(state); Assert.Empty(FlowGrants(done.State));
        OfficialInsightAndSpellLockTests.Restore(done.State);
    }

    [Fact]
    public async Task SourceCanBeDestroyedInResponseWithoutRevokingTheSkillOrPermission()
    {
        var initial = Add(Position(), "PING", "OGN·133/298", "P2", "HAND");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["UNIT"] = initial.CardObjects["UNIT"] with { Damage = 3 } } };
        var driver = new VenBasicFlowSpellTests.Driver(initial); await Conquer(driver); await Choose(driver, "SPELL");
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("PING", "OGN·133/298", []));
        await driver.Top(); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard);
        await driver.Top(); Assert.Single(FlowGrants(driver.State));
        await driver.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await driver.Top();
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
    }

    [Fact]
    public async Task GrantExpiresAtTheEndOfTheTurnAndCannotFollowAReenteredGraveyardObject()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position());
        await Conquer(driver); await Choose(driver, "SPELL"); await driver.Top();
        var returned = driver.State with { CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
            ["SPELL"] = driver.State.CardObjects["SPELL"] with { ObjectGeneration = driver.State.CardObjects["SPELL"].ObjectGeneration + 2 } } };
        await Reject(returned, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        await driver.Act("P1", new EndTurnCommand()); Assert.Empty(FlowGrants(driver.State));
    }

    [Fact]
    public async Task CombatConquestUsesTheSameTargetConfirmationAndResponseWindow()
    {
        var initial = OfficialLegendConquestTests.CombatPosition("OGN·269/298", power: 4);
        initial = Add(initial, "CONQUEST-SPELL", "OGN·048/298", "P1", "GRAVEYARD");
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { LegendZone = [] } },
            CardObjects = initial.CardObjects.Where(p => p.Key != "LEGEND").ToDictionary(p => p.Key,
                p => p.Key == "A" ? OfficialGarenConquestBattlefieldCountTests.Printed("A", "VEN·113a", "P1") : p.Value),
            ObjectLocations = initial.ObjectLocations.Where(p => p.Key != "LEGEND").ToDictionary() };
        var result = await OfficialLegendConquestTests.CombatConquer(initial);
        Assert.Equal(new[] { "CONQUEST-SPELL" }, result.State.PendingCardChoice!.LegalObjectIds);
        Assert.Empty(FlowGrants(result.State)); OfficialInsightAndSpellLockTests.Restore(result.State);
        result = await OfficialGraveyardRecastTests.Act(result.State, "P1",
            new ChooseCardsCommand(result.State.PendingCardChoice.ChoiceId, result.State.PendingCardChoice.ChoiceWindow, ["CONQUEST-SPELL"]));
        Assert.Empty(FlowGrants(result.State));
        result = await OfficialGraveyardRecastTests.Top(result.State);
        Assert.Single(FlowGrants(result.State)); OfficialInsightAndSpellLockTests.Restore(result.State);
    }

    [Fact]
    public async Task ReckonersArenaActivatesTheSameConquestSkillDuringHoldBeforeChanneling()
    {
        var initial = OfficialHoldSequenceTests.AddUnit(OfficialHoldSequenceTests.State("OGN·286/298"), "UNIT", "VEN·113", "F", "P1");
        initial = Add(initial, "SPELL", "OGN·048/298", "P1", "GRAVEYARD");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", new PassPriorityCommand());
        Assert.NotNull(Assert.Single(driver.State.StackItems).HeldContext);
        await driver.Top(); Assert.NotNull(driver.State.PendingCardChoice);
        Assert.Equal("CHANNEL", driver.State.TurnStartStep); Assert.Empty(FlowGrants(driver.State));
        await Choose(driver, "SPELL"); await driver.Top();
        Assert.Single(FlowGrants(driver.State));
        Assert.Contains(driver.Events(), e => e.Event.Kind == "FLOW_GRANTED");
    }

    [Fact]
    public async Task AttachedPrintedTextCreatesIndependentConquestSkillsForTwoSpells()
    {
        var initial = Add(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        initial = Add(initial, "SPELL2", "OGN·004/298", "P1", "GRAVEYARD");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"]));
        await driver.Top(); Assert.NotNull(driver.State.CardObjects["SONG"].AttachedRuleText);
        await Conquer(driver); Assert.Equal(2, driver.State.TriggerQueue.Count);
        var ids = driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        await driver.Act("P1", new OrderTriggersCommand(ids, ids));
        await Choose(driver, "SPELL"); await Choose(driver, "SPELL2");
        await driver.Top(); await driver.Top(); Assert.Equal(2, FlowGrants(driver.State).Length);
        await driver.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await driver.Top();
        await driver.Act("P1", new PlayCardCommand("SPELL2", "OGN·004/298", ["UNIT"])); await driver.Top();
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
        Assert.Contains("SPELL2", driver.State.PlayerZones["P1"].Banished);
    }

    [Fact]
    public async Task APreviouslyGrantedReactionSpellCanLeaveTheGraveyardBeforeAnotherGrantResolves()
    {
        var initial = Add(Position(), "SONG", "SFD·059/221", "P1", "BASE");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", new AssembleEquipmentCommand("SONG", "UNIT", ["ASSEMBLE_GREEN"])); await driver.Top();
        await Conquer(driver);
        var ids = driver.State.TriggerQueue.Select(t => t.TriggerId).ToArray();
        await driver.Act("P1", new OrderTriggersCommand(ids, ids));
        await Choose(driver, "SPELL"); await Choose(driver, "SPELL"); await driver.Top();
        Assert.Single(FlowGrants(driver.State)); Assert.Single(driver.State.StackItems);
        await driver.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", [])); await driver.Top();
        Assert.Contains("SPELL", driver.State.PlayerZones["P1"].Banished);
        await driver.Top(); Assert.Single(FlowGrants(driver.State));
        Assert.Single(driver.Events(), e => e.Event.Kind == "FLOW_GRANTED");
    }

    [Fact]
    public async Task RecoveryRejectsForgedContextTargetBindingAndPermission()
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position()); await Conquer(driver);
        var item = Assert.Single(driver.State.StackItems); var context = item.ConquestFlow!;
        foreach (var changed in new[] { item with { ConquestFlow = null }, item with { ConquestFlow = context with { CardNo = "OGN·219/298" } },
            item with { ConquestFlow = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { ConquestFlow = context with { BattlefieldId = "missing" } } })
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [changed] }), e => e.Contains("conquest flow", StringComparison.Ordinal));
        await Choose(driver, "SPELL");
        var confirmed = Assert.Single(driver.State.StackItems);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [confirmed with { TargetGenerations = new Dictionary<string, long>() }] }),
            e => e.Contains("conquest flow", StringComparison.Ordinal));
        await driver.Top();
        var marker = Assert.Single(FlowGrants(driver.State));
        var grant = JsonSerializer.Deserialize<TurnFlowGrant>(Convert.FromBase64String(marker["TURN_FLOW_GRANT|".Length..]))!;
        foreach (var forged in new[] { grant with { TurnNumber = grant.TurnNumber + 1 }, grant with { ControllerId = "missing" },
            grant with { Spell = grant.Spell with { Generation = grant.Spell.Generation + 1 } }, grant with { CardNo = "OGN·219/298" },
            grant with { StackItemId = "invented" } })
        {
            var replaced = "TURN_FLOW_GRANT|" + Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(forged)));
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { UntilEndOfTurnEffects = [replaced] }),
                e => e.Contains("flow grant", StringComparison.Ordinal));
        }
        await driver.Act("P1", new PlayCardCommand("SPELL", "OGN·048/298", []));
        var flowItem = Assert.Single(driver.State.StackItems);
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(driver.State with { StackItems = [flowItem with { FlowPlay = flowItem.FlowPlay! with { Grant = "forged" } }] }),
            e => e.Contains("flow play receipt", StringComparison.Ordinal));
    }

    internal static string[] FlowGrants(MatchState state) => state.UntilEndOfTurnEffects.Where(e => e.StartsWith("TURN_FLOW_GRANT|", StringComparison.Ordinal)).ToArray();
    internal static async Task Conquer(VenBasicFlowSpellTests.Driver driver)
    {
        await driver.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await driver.Act("P1", new PassFocusCommand()); await driver.Act("P2", new PassFocusCommand());
    }
    internal static Task Choose(VenBasicFlowSpellTests.Driver driver, params string[] ids)
        => driver.Act("P1", new ChooseCardsCommand(driver.State.PendingCardChoice!.ChoiceId, driver.State.PendingCardChoice.ChoiceWindow, ids));
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-kennen", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
    internal static MatchState Position(string no = "VEN·113", bool inHand = false, string spellNo = "OGN·048/298")
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", no, "P1", inHand ? "HAND" : "BASE"); Put("SPELL", spellNo, "P1", "GRAVEYARD");
        Put("BF", "OGN·296/298", "P2", "BATTLEFIELD", "BF"); Put("GEAR", "SFD·022/221", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => player + "-D" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck, Base = player == "P1" ? inHand ? [] : ["UNIT"] : ["GEAR"],
                Hand = player == "P1" && inHand ? ["UNIT"] : [], Graveyard = player == "P1" ? ["SPELL"] : [],
                Battlefields = player == "P2" ? ["BF"] : [] };
        }
        return new MatchState("KENNEN-CONQUEST-FLOW", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(30, 30), ["P2"] = new(30, 30) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
    internal static MatchState Add(MatchState state, string id, string no, string owner, string zone)
    {
        var zones = state.PlayerZones.ToDictionary(); var z = zones[owner];
        zones[owner] = zone switch { "HAND" => z with { Hand = z.Hand.Append(id).ToArray() },
            "BASE" => z with { Base = z.Base.Append(id).ToArray() }, _ => z with { Graveyard = z.Graveyard.Append(id).ToArray() } };
        return state with { PlayerZones = zones,
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) } };
    }
}
