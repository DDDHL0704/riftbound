using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class VenSignatureFlowSpellTests
{
    private static readonly string[] Cards = ["VEN·140", "VEN·148", "VEN·154"];
    public static IEnumerable<object[]> Plays() => Cards.SelectMany(no => new[] { false, true }
        .Select(flow => new object[] { no, flow }));

    [Theory]
    [InlineData("VEN·140", 37843, 1, 3, 1)]
    [InlineData("VEN·148", 37851, 2, 5, 2)]
    [InlineData("VEN·154", 37857, 2, 5, 2)]
    public async Task OfficialPrintsKeepTheirHandAndFlowCosts(string no, long id, int mana, int flowMana, int flowPower)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(1, card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost); Assert.Equal(flowMana, behavior.FlowCost!.Mana);
        Assert.Equal(flowPower, behavior.FlowCost.Power); Assert.Empty(behavior.FlowCost.Traits);
        Assert.False(behavior.CanPlayDuringPriority); Assert.False(behavior.CanPlayDuringSpellDuel);
    }

    [Theory]
    [MemberData(nameof(Plays))]
    public async Task HandAndFlowPayWaitForResponsesThenApplyThePrintedInstructions(string no, bool flow)
    {
        var initial = Position(no, flow); var command = Play(no); var quote = Quote(initial, command);
        Assert.True(quote.CanPay, quote.Message);
        var mana = flow ? no == "VEN·140" ? 3 : 5 : no == "VEN·140" ? 1 : 2;
        var power = flow && no != "VEN·140" ? 2 : 1;
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", command);
        Assert.Single(driver.State.StackItems);
        Assert.Equal(20 - mana, driver.State.RunePools["P1"].Mana);
        Assert.Equal(initial.RunePools["P1"].TotalPower - power, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        await driver.Top();
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        if (no == "VEN·140") {
            Assert.Equal(2, driver.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(0, driver.State.CardObjects["ALLY"].Damage);
            Assert.Equal("FIELD", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
            Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
            var events = driver.Events().Select(e => e.Event).ToArray();
            Assert.True(Array.FindIndex(events, e => e.Kind == "DAMAGE_APPLIED")
                < Array.FindIndex(events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD"));
        } else if (no == "VEN·148") {
            Assert.Equal("FIELD", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
            Assert.Equal(6, driver.State.CardObjects["ALLY"].Power);
            Assert.Equal(5, driver.State.CardObjects["FRIEND"].Power);
            Assert.Equal(3, driver.State.CardObjects["ENEMY"].Power);
        } else {
            Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
            Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Base);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DESTROYED"
                && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShurikenFlipMayDeclineTheEnemyTargetAndOnlyMoveItsFriendlyUnit(bool flow)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position("VEN·140", flow));
        await driver.Act("P1", Play("VEN·140") with { TargetObjectIds = ["ALLY"] }); await driver.Top();
        Assert.Equal("FIELD", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)]
    public async Task ShadowDashCountsFriendlyUnitsAtTheExactDestinationAndOnlyBuffsAPair(int count, bool flow)
    {
        var driver = new VenBasicFlowSpellTests.Driver(Position("VEN·148", flow, count));
        await driver.Act("P1", Play("VEN·148")); await driver.Top();
        Assert.Equal("FIELD", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(count == 2 ? 5 : 4, driver.State.CardObjects["FRIEND"].Power);
        Assert.Equal(count == 2 ? 6 : 5, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(3, driver.State.CardObjects["ENEMY"].Power);
        if (count == 3) Assert.Equal(4, driver.State.CardObjects["THIRD"].Power);
    }

    public static IEnumerable<object[]> Rejections() => Plays().SelectMany(row => new[] {
        "first", "second", "count", "mana", "power", "turn", "source-zone"
    }.Select(branch => new object[] { row[0], row[1], branch }));

    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task InvalidTargetsUnpaidCostsAndUnavailableOriginsLeaveAllStateAndViewsUnchanged(string no, bool flow, string branch)
    {
        var state = Position(no, flow); var command = Play(no);
        Assert.True(Quote(state, command).CanPay);
        if (branch == "first") command = command with {
            TargetObjectIds = no == "VEN·148" ? ["ALLY", "ENEMY"] : ["ENEMY", "ALLY"] };
        if (branch == "second") command = command with {
            TargetObjectIds = no == "VEN·148" ? ["FIELD", "FRIEND"] : ["ALLY", "FRIEND"] };
        if (branch == "count") command = command with { TargetObjectIds = [] };
        if (branch == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(0, 20) } };
        if (branch == "power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 0) } };
        if (branch == "turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (branch == "source-zone") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = [], Banished = ["SPELL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["SPELL"] = new("P1", "BANISHED") } };
        await Reject(state, command, branch switch {
            "first" or "second" or "count" => ErrorCodes.InvalidTarget,
            "mana" or "power" => ErrorCodes.InsufficientCost,
            "source-zone" => ErrorCodes.CardNotInHand,
            _ => ErrorCodes.PhaseNotAllowed });
    }

    [Theory]
    [InlineData(false, 5)] [InlineData(false, 6)]
    [InlineData(true, 5)] [InlineData(true, 6)]
    public async Task PublicExecutionRequiresAStrictlyWeakerEnemyBeforeAnyPayment(bool flow, int might)
    {
        var state = Position("VEN·154", flow);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ENEMY"] = state.CardObjects["ENEMY"] with { Power = might } } };
        Assert.False(Quote(state, Play("VEN·154")).IsValid);
        await Reject(state, Play("VEN·154"), ErrorCodes.InvalidTarget);
    }

    [Theory]
    [InlineData("VEN·140", "ALLY")]
    [InlineData("VEN·140", "ENEMY")]
    [InlineData("VEN·148", "ALLY")]
    [InlineData("VEN·148", "ENEMY")]
    [InlineData("VEN·154", "ALLY")]
    public async Task RealReturnResponsesRecheckEachInstructionWithoutCancellingIndependentInstructions(string no, string returned)
    {
        var player = returned == "ENEMY" ? "P2" : "P1";
        var initial = AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", player);
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play(no));
        if (player == "P2") await driver.Act("P1", new PassPriorityCommand());
        await driver.Act(player, new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await driver.Top();
        Assert.Contains(returned, driver.State.PlayerZones[player].Hand);
        await driver.Top();
        if (no == "VEN·140") {
            Assert.Equal(returned == "ALLY" ? 2 : 0, driver.State.CardObjects["ENEMY"].Damage);
            Assert.Equal(returned == "ALLY" ? "HAND" : "BATTLEFIELD", driver.State.ObjectLocations["ALLY"].Zone);
        } else if (no == "VEN·148") {
            Assert.Equal(returned == "ENEMY" ? 5 : 4, driver.State.CardObjects["FRIEND"].Power);
            Assert.Equal(returned == "ENEMY" ? 6 : 4, driver.State.CardObjects["ALLY"].Power);
            if (returned == "ALLY") Assert.Equal("FIELD", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        } else {
            Assert.DoesNotContain("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
            Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        }
    }

    [Theory]
    [InlineData("ALLY")]
    [InlineData("ENEMY")]
    public async Task PublicExecutionDoesNotBindANewIncarnationThatReusesEitherObjectId(string id)
    {
        var cast = await OfficialGraveyardRecastTests.Act(Position("VEN·154"), "P1", Play("VEN·154"));
        var state = cast.State with { CardObjects = new Dictionary<string, CardObjectState>(cast.State.CardObjects) {
            [id] = cast.State.CardObjects[id] with { ObjectGeneration = cast.State.CardObjects[id].ObjectGeneration + 1 } } };
        var result = await OfficialGraveyardRecastTests.Top(state);
        Assert.DoesNotContain("ENEMY", result.State.PlayerZones["P2"].Graveyard);
        Assert.Equal(18, result.State.RunePools["P1"].Mana);
    }

    [Theory]
    [InlineData(3, true)] [InlineData(5, false)] [InlineData(6, false)]
    public void PublicExecutionPromptsOfferOnlyPairsThatMeetTheCurrentMightComparison(int enemyMight, bool legal)
    {
        var initial = Position("VEN·154");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Power = enemyMight } } };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!,
            c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            candidate.Metadata!["sourceRequirements"]).ToArray();
        Assert.Equal(legal, requirements.Length > 0);
        if (!legal) return;
        var selections = Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(
            Assert.Single(requirements)["legalTargetSelections"]).ToArray();
        Assert.Contains(selections, ids => ids.SequenceEqual(["ALLY", "ENEMY"]));
        Assert.All(selections, ids => Assert.True(Quote(initial, Play("VEN·154") with { TargetObjectIds = ids }).IsValid));
    }

    [Theory]
    [InlineData("VEN·140")] [InlineData("VEN·148")] [InlineData("VEN·154")]
    public async Task OnlyShurikenFlipRestrictsTheChosenEnemyToABattlefield(string no)
    {
        var initial = LocalDestructionRecallTests.At(Position(no), "ENEMY", "P2", "BASE");
        var command = Play(no);
        if (no == "VEN·140") {
            Assert.False(Quote(initial, command).IsValid);
            await Reject(initial, command, ErrorCodes.InvalidTarget);
            return;
        }
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", command); await driver.Top();
        if (no == "VEN·148") Assert.Equal("FIELD", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        else Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
    }

    [Theory]
    [MemberData(nameof(Plays))]
    public async Task AChosenEnemyPaysWardInAdditionToHandOrFlowCosts(string no, bool flow)
    {
        var initial = Position(no, flow);
        var basePower = Quote(initial, Play(no)).Cost!.GenericPower;
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, "法盾2"] } } };
        var quote = Quote(initial, Play(no));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(basePower + 2, quote.Cost!.GenericPower);
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play(no));
        Assert.Equal(initial.RunePools["P1"].TotalPower - quote.Cost.GenericPower
            - quote.Cost.PowerByTrait.Values.Sum(), driver.State.RunePools["P1"].TotalPower);
        await driver.Top();
    }

    [Theory]
    [MemberData(nameof(Plays))]
    public async Task EchoRechecksEachExecutionAndKeepsItsChosenMovementTargetSeparateFromTheDamageTarget(string no, bool flow)
    {
        var initial = Position(no, flow);
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        var command = Play(no) with { OptionalCosts = ["ECHO"], RepeatChoices = [new("", Play(no).TargetObjectIds,
            Destination: no == "VEN·140" ? "BATTLEFIELD:OTHER" : "")] };
        await driver.Act("P1", command); await driver.Top();
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "SPELL_EXECUTION_COMPLETED"));
        if (no == "VEN·140") {
            Assert.Equal("OTHER", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
            Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
            Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "DAMAGE_APPLIED"));
        } else if (no == "VEN·148") {
            Assert.Equal(7, driver.State.CardObjects["ALLY"].Power);
            Assert.Equal(6, driver.State.CardObjects["FRIEND"].Power);
            Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
        } else Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EchoPreservesTheFriendlyBaseDestinationWhenOnlyTheFirstTargetMoves(bool flow)
    {
        var initial = LocalDestructionRecallTests.At(Position("VEN·140", flow), "ALLY", "P1", "BATTLEFIELD", "FIELD");
        initial = initial with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        var command = Play("VEN·140") with { Destination = "BASE:P1", OptionalCosts = ["ECHO"],
            RepeatChoices = [new("", ["ALLY", "ENEMY"], Destination: "BATTLEFIELD:OTHER")] };
        await driver.Act("P1", command);
        Assert.Equal(new[] { "BASE:P1", "BATTLEFIELD:OTHER" }, Assert.Single(driver.State.StackItems).RepeatExecutions!.Select(e => e.Destination));
        await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BASE");
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_MOVED_TO_BATTLEFIELD");
    }

    [Theory]
    [MemberData(nameof(Plays))]
    public async Task CounteredSpellsKeepTheirPaymentAndPerformNoInstructions(string no, bool flow)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no, flow), "COUNTER", "OGN·045/298", "P2");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play(no));
        var paid = driver.State.RunePools["P1"];
        var target = Assert.Single(driver.State.StackItems).StackItemId;
        await driver.Act("P1", new PassPriorityCommand());
        await driver.Act("P2", new PlayCardCommand("COUNTER", "OGN·045/298", [target]));
        await driver.Top();
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(paid, driver.State.RunePools["P1"]);
        Assert.Contains("SPELL", flow ? driver.State.PlayerZones["P1"].Banished : driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(0, driver.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(initial.CardObjects["ALLY"].Power, driver.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "DAMAGE_APPLIED" or "UNIT_MOVED_TO_BATTLEFIELD" or "UNIT_DESTROYED");
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task PublicExecutionRechecksCurrentMightAfterARealBuffResponse(string buffed)
    {
        var player = buffed == "ALLY" ? "P1" : "P2";
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·154"), "BUFF", "OGN·207/298", player);
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play("VEN·154"));
        if (player == "P2") await driver.Act("P1", new PassPriorityCommand());
        await driver.Act(player, new PlayCardCommand("BUFF", "OGN·207/298", [buffed]));
        await driver.Top();
        Assert.Equal(initial.CardObjects[buffed].Power + 3, driver.State.CardObjects[buffed].Power);
        await driver.Top();
        Assert.Equal(buffed == "ALLY", driver.State.PlayerZones["P2"].Graveyard.Contains("ENEMY"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ShadowDashStillBuffsThePairWhenMovementIsProhibitedAndExpiresAtTurnEnd(bool flow)
    {
        var initial = Position("VEN·148", flow);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { UntilEndOfTurnEffects = ["MOVEMENT_PROHIBITED:P1"] } } };
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play("VEN·148")); await driver.Top();
        Assert.Equal("OTHER", driver.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        Assert.Equal(6, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(5, driver.State.CardObjects["FRIEND"].Power);
        await driver.Act("P1", new EndTurnCommand());
        Assert.Equal(5, driver.State.CardObjects["ALLY"].Power);
        Assert.Equal(4, driver.State.CardObjects["FRIEND"].Power);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ShurikenFlipMovesBeforeLethalCleanupAndTheCrabRevealsBeforeHerderDraws(bool flow)
    {
        // China official post-event ruling CN-NEWS-5bb5334bbf2c8df2, question 1.
        var initial = Position("VEN·140", flow);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "SFD·048/221", "P1") with { IsExhausted = false },
            ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "UNL-053/219", "P2") } };
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BATTLEFIELD", "FIELD");
        var driver = new VenBasicFlowSpellTests.Driver(initial);
        await driver.Act("P1", Play("VEN·140") with { Destination = "BATTLEFIELD:OTHER" });
        await driver.Top();
        var events = driver.Events().Select(e => e.Event).ToArray();
        Assert.True(Array.FindIndex(events, e => e.Kind == "UNIT_MOVED_TO_BATTLEFIELD")
            < Array.FindIndex(events, e => e.Kind == "UNIT_DESTROYED"));
        Assert.Equal("OTHER", driver.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Contains("ENEMY", driver.State.PlayerZones["P2"].Graveyard);
        if (driver.State.TriggerQueue.Count > 0)
        {
            var ordering = Assert.Single(driver.PromptFor("P1").Candidates!, c => c.Action == CommandTypes.OrderTriggers);
            var ids = JsonSerializer.SerializeToElement(ordering.Metadata).GetProperty("orderedTriggerIds")
                .EnumerateArray().Select(id => id.GetString()!).ToArray();
            await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: ids));
        }
        Assert.NotNull(driver.State.StackItems[^1].DeathRevealContext);
        await driver.Top();
        Assert.Equal("P2", driver.State.PendingCardChoice!.PlayerId);
        Assert.DoesNotContain(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
        await driver.Act("P2", new ChooseCardsCommand(driver.State.PendingCardChoice.ChoiceId, "REVEALED_HAND_EFFECT", []));
        Assert.NotNull(driver.State.StackItems[^1].MovementObserver);
        await driver.Top();
        Assert.Contains("P1-DRAW-1", driver.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
    }

    private static async Task Reject(MatchState state, PlayCardCommand command, string expectedError)
    {
        var hash = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-signature", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(expectedError, result.ErrorCode);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    private static PlayCardCommand Play(string no) => new("SPELL", no,
        no == "VEN·148" ? ["FIELD", "ENEMY"] : ["ALLY", "ENEMY"],
        Destination: no == "VEN·140" ? "BATTLEFIELD:FIELD" : "");

    private static PlayCostQuoteDto Quote(MatchState state, PlayCardCommand command)
        => new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));

    private static MatchState Position(string no, bool flow = false, int friendlyCount = 2)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string number, string player, string zone, int? power = null, string? field = null)
        {
            var card = OfficialGarenConquestBattlefieldCountTests.Printed(id, number, player);
            cards[id] = power is { } might ? card with { Power = might, IsExhausted = false }
                : field == id ? card with { ControllerId = player } : card;
            locations[id] = new(player, zone, field);
        }
        Put("SPELL", no, "P1", flow ? "GRAVEYARD" : "HAND");
        Put("FIELD", "OGN·280/298", "P1", "BATTLEFIELD", field: "FIELD");
        Put("OTHER", "OGN·280/298", "P2", "BATTLEFIELD", field: "OTHER");
        Put("ALLY", "OGN·219/298", "P1", "BASE", 5);
        Put("FRIEND", "OGN·219/298", "P1", "BATTLEFIELD", 4, "FIELD");
        Put("ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", 3, "OTHER");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => player + "-DRAW-" + i).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck,
                Base = player == "P1" ? ["ALLY"] : [],
                Battlefields = player == "P1" ? ["FIELD", "FRIEND"] : ["OTHER", "ENEMY"],
                Hand = player == "P1" && !flow ? ["SPELL"] : [],
                Graveyard = player == "P1" && flow ? ["SPELL"] : [] };
        }
        var state = new MatchState("VEN-SIGNATURE-FLOW", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20, new Dictionary<string, int> {
                [RuneTrait.Red] = 10, [RuneTrait.Green] = 10, [RuneTrait.Orange] = 10, [RuneTrait.Yellow] = 10 }), ["P2"] = new(20, 20) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
        if (no == "VEN·148" && friendlyCount >= 2)
            state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "FIELD");
        if (no == "VEN·148" && friendlyCount == 3) {
            state = OfficialHoldSequenceTests.AddUnit(state, "THIRD", "OGN·219/298", "FIELD", "P1");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["THIRD"] = state.CardObjects["THIRD"] with { Power = 4 } } };
        }
        return state;
    }
}
