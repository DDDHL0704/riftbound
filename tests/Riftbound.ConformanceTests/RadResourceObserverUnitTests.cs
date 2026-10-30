using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadResourceObserverUnitTests
{
    private const string Cluster = "RAD·016/167";
    private static MatchState Position(int field, int deck, bool battlefield = false)
    {
        var state = EndDeckPlayTests.RunePosition(Cluster, battlefield);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        cards["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", Cluster, "P1");
        var runes = Enumerable.Range(1, field + deck).Select(i => "RESET_RUNE_" + i).ToArray();
        foreach (var id in EndDeckPlayTests.RuneIds.Concat(state.PlayerZones["P1"].RuneDeck)) { cards.Remove(id); locations.Remove(id); }
        foreach (var (id, index) in runes.Select((id, index) => (id, index))) {
            cards[id] = new(id, isExhausted: index % 2 == 0, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", index < field ? "BASE" : "RUNE_DECK");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Except(EndDeckPlayTests.RuneIds).Concat(runes.Take(field)).ToArray(),
                RuneDeck = runes.Skip(field).ToArray() } } };
    }

    private static async Task Order(Driver game, IReadOnlyList<string> runes)
    {
        foreach (var id in runes.Take(Math.Max(0, runes.Count - 1))) {
            var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
            Assert.Equal("RUNE_RECYCLE_ORDER", choice.Request.Kind);
            await game.Act(choice.Request.PlayerId, new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", [id]));
        }
        Assert.Null(game.State.PendingRuleChoice);
    }

    [Theory]
    [InlineData(0, 0, false)] [InlineData(0, 2, true)] [InlineData(1, 0, false)]
    [InlineData(5, 0, true)] [InlineData(5, 2, false)] [InlineData(7, 0, false)]
    public async Task EndStepRecyclesAllRunesInOwnerOrderThenCallsAsManyAsPossible(int field, int deck, bool battlefield)
    {
        var initial = Position(field, deck, battlefield); var game = new Driver(initial);
        var recycled = Enumerable.Range(1, field).Select(i => "RESET_RUNE_" + i).Reverse().ToArray();
        var oldDeck = initial.PlayerZones["P1"].RuneDeck;
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal("P1", game.State.TurnPlayerId); Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        if (field > 1) {
            Assert.Equal(initial.PlayerZones["P1"].RuneDeck, game.State.PlayerZones["P1"].RuneDeck);
            Assert.Equal("P1", game.State.TurnPlayerId);
            await Order(game, recycled);
        }
        var combined = oldDeck.Concat(recycled).ToArray(); var called = combined.Take(4).ToArray();
        Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.Equal(combined.Skip(4), game.State.PlayerZones["P1"].RuneDeck);
        Assert.All(called, id => { Assert.Contains(id, game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects[id].IsExhausted);
            Assert.Equal(initial.CardObjects[id].ObjectGeneration + (recycled.Contains(id) ? 2 : 1), game.State.CardObjects[id].ObjectGeneration); });
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        var calls = game.Events().Where(e => e.Event.Kind == "RUNES_CALLED" && Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST")).ToArray();
        Assert.Equal(called.Length == 0 ? 0 : 1, calls.Length);
        if (called.Length > 0) Assert.Equal(called.Length, calls[0].Event.Payload["count"]);
        await game.Replay();
    }

    [Fact]
    public async Task RuneOrderingRejectsForeignMissingAndRepeatedChoicesWithoutCommittingTheInstruction()
    {
        var game = new Driver(Position(5, 0)); await game.Act("P1", new EndTurnCommand()); await game.Top();
        var request = game.State.PendingRuleChoice!.Request;
        foreach (var (player, id) in new[] { ("P2", "RESET_RUNE_1"), ("P1", "missing"), ("P1", "HOST") }) {
            var command = new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]);
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("invalid-rune-order", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        }
        await game.Act("P1", new PayCostCommand(request.Id, "RULE_REPLACEMENT", ["RESET_RUNE_5"]));
        Assert.DoesNotContain(game.State.PendingRuleChoice!.Request.Options, o => o.Id == "RESET_RUNE_5");
        await Order(game, ["RESET_RUNE_4", "RESET_RUNE_3", "RESET_RUNE_2", "RESET_RUNE_1"]);
        await game.Replay();
    }

    [Fact]
    public async Task RemovingTheSourceDuringResponseDoesNotCancelItsCapturedRuneInstruction()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(1, 0), "RETURN", "OGN·104/298");
        var game = new Driver(initial); await game.Act("P1", new EndTurnCommand());
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Equal("P2", game.State.TurnPlayerId);
        Assert.True(game.State.CardObjects["RESET_RUNE_1"].IsExhausted); await game.Replay();
    }

    private const string Oracle = "RAD·053/167";
    private static MatchState OraclePosition(bool exhausted = false) => ObserverPosition(Oracle, exhausted);
    private static MatchState ObserverPosition(string no, bool exhausted = false)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = exhausted } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = [], Base = [..state.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
    }

    private static async Task Insight(Driver game)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal(1, choice.MaxCount);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [choice.LegalObjectIds[0]]));
        Assert.Null(game.State.PendingCardChoice);
        Assert.Equal("P1-DRAW-2", game.State.PlayerZones["P1"].MainDeck[0]);
    }

    [Theory]
    [InlineData("move")] [InlineData("spell-cost")] [InlineData("assemble-cost")]
    public async Task ActualExhaustionFromMovementAndDifferentCostsEarnsOneInsight(string instruction)
    {
        var state = OraclePosition();
        if (instruction == "spell-cost") state = OfficialCopyIdentityTests.AddSpell(state, "COST", "OGN·048/298");
        if (instruction == "assemble-cost") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "RAD·150/167", "P1") } };
        var game = new Driver(state);
        if (instruction == "move") await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        else if (instruction == "spell-cost") await game.Act("P1", new PlayCardCommand("COST", "OGN·048/298", [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:UNIT"]));
        else await game.Act("P1", new AssembleEquipmentCommand("GEAR", "ALLY", ["EXHAUST_FRIENDLY_UNIT:UNIT"]));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Single(game.State.StackItems, i => i.InsightContext is { Kind: "EXHAUSTED" });
        await game.Top(); await Insight(game);
        while (game.State.StackItems.Count > 0) await game.Top();
        Assert.Single(game.Events(), e => e.Event.Kind == "INSIGHT_REQUESTED"); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ASpellThatExhaustsThenDestroysTheSourceKeepsOnlyTheActualTransition(bool exhausted)
    {
        var initial = OraclePosition(exhausted);
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Where(id => id != "UNIT").ToArray(),
                Battlefields = [..initial.PlayerZones["P1"].Battlefields, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "BATTLEFIELD", "BF") } };
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(initial, "OVERLOAD", "OGN·123/298"));
        await game.Act("P1", new PlayCardCommand("OVERLOAD", "OGN·123/298", [])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        if (exhausted) Assert.Empty(game.State.StackItems);
        else { Assert.Single(game.State.StackItems, i => i.InsightContext is { Kind: "EXHAUSTED" }); await game.Top(); await Insight(game); }
        await game.Replay();
    }

    [Fact]
    public async Task EnteringExhaustedDoesNotTriggerAndIllegalMovementDoesNotEarnAnInsight()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(Oracle));
        await game.Act("P1", new PlayCardCommand("UNIT", Oracle, []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(game.State.StackItems);
        var command = new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF");
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("sleeping-move", "P1", command.CmdType), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State)); await game.Replay();
    }

    [Fact]
    public async Task SourceReturnedDuringResponseKeepsItsInsightAndRejectsForeignPrivateChoices()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(OraclePosition(), "RETURN", "OGN·104/298"));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Top();
        var choice = game.State.PendingCardChoice!;
        foreach (var (player, id) in new[] { ("P2", "P1-DRAW-1"), ("P1", "P2-DRAW-1"), ("P1", "missing") }) {
            var command = new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]);
            var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("private-insight", player, command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        }
        await Insight(game); await game.Replay();
    }

    [Fact]
    public async Task ExhaustedOracleWithAnEmptyDeckCompletesWithoutOpeningAChoice()
    {
        var initial = OraclePosition(); var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary();
        foreach (var id in initial.PlayerZones["P1"].MainDeck) { cards.Remove(id); locations.Remove(id); }
        var game = new Driver(initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [] } } });
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF")); await game.Top();
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task OfficialOracleIdentityAndForgedCapturedInsightAreChecked()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Oracle);
        Assert.Equal(39549, card.Id); Assert.Equal(2, card.Energy); Assert.Equal(2, card.Power);
        Assert.Contains("查看你主牌堆顶部的一张牌", card.CardEffect);
        var game = new Driver(OraclePosition()); await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        var item = Assert.Single(game.State.StackItems);
        foreach (var context in new[] { item.InsightContext! with { Count = 2 }, item.InsightContext! with { Kind = "LAST_BREATH" },
            item.InsightContext! with { CardNo = Cluster }, item.InsightContext! with { SourceGeneration = -1 }, item.InsightContext! with { SourceFaceRevision = 99 },
            item.InsightContext! with { SourceCapturedTick = game.State.Tick + 1 } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { InsightContext = context }] }));
        await game.Top(); await Insight(game); await game.Replay();
    }

    [Fact]
    public async Task CopiedOracleTokenKeepsItsEarnedInsightAfterCeasingToExist()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(OraclePosition(), "COPY", "UNL-200/219");
        initial = OfficialCopyIdentityTests.AddSpell(initial, "RETURN", "OGN·104/298");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["UNIT"]));
        await game.Top(); await game.Top();
        var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null && c.CardNo == Oracle);
        await game.Act("P1", new MoveUnitCommand(token.ObjectId, "BASE", "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [token.ObjectId])); await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey(token.ObjectId)); await game.Top(); await Insight(game); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EachActuallyAttachedOracleTextEarnsItsOwnOrderedInsight(int copies)
    {
        var initial = CopiedDeathObserverTests.Position(Oracle, copies);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["COST"] = OfficialGarenConquestBattlefieldCountTests.Printed("COST", "OGN·048/298", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P2"] = initial.PlayerZones["P2"] with { Hand = [..initial.PlayerZones["P2"].Hand, "COST"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["COST"] = new("P2", "HAND") } };
        var game = new Driver(initial);
        for (var i = 0; i < copies; i++) {
            await game.Act("P2", new AssembleEquipmentCommand("GEAR" + i, "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
            Assert.Equal(Oracle, game.State.CardObjects["GEAR" + i].AttachedRuleText!.CardNo);
        }
        await game.Act("P2", new PlayCardCommand("COST", "OGN·048/298", [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:HOST"]));
        var earned = game.State.TriggerQueue.Select(t => t.InsightContext).Concat(game.State.StackItems.Select(i => i.InsightContext))
            .Where(c => c is { Kind: "EXHAUSTED" }).ToArray();
        Assert.Equal(copies + 1, earned.Length); Assert.Equal(copies, earned.Count(c => c!.Origin is not null));
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P2", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        for (var i = 0; i <= copies; i++) {
            await game.Top(); var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal(1, choice.MaxCount);
            await game.Act("P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        }
        await game.Top(); Assert.Empty(game.State.StackItems);
        Assert.Equal(copies + 1, game.Events().Count(e => e.Event.Kind == "INSIGHT_REQUESTED")); await game.Replay();
    }

    [Fact]
    public async Task OfficialClusterIdentityIncludesThePrintedPowerCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Cluster);
        Assert.Equal(39506, card.Id); Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power); Assert.Equal(2, card.ReturnEnergy);
    }

    private const string Inventor = "RAD·105/167";
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-observer", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    private static async Task ChooseInventorMode(Driver game, string mode)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("UNIT_EVENT_MODE_CONFIRMATION", choice.ChoiceWindow);
        Assert.Null(game.State.PriorityPlayerId); Assert.Equal(new[] { "CREATE_GOLD", "PAY_DRAW" }, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [mode]));
    }

    [Theory]
    [InlineData("CREATE_GOLD", true)] [InlineData("PAY_DRAW", true)] [InlineData("PAY_DRAW", false)]
    public async Task InventorChoosesAModeAndPaysBeforeResponsesButResolvesItsEffectAfterResponses(string mode, bool pay)
    {
        var game = new Driver(ObserverPosition(Inventor));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        var before = game.State.RunePools["P1"];
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
        await ChooseInventorMode(game, mode);
        if (mode == "PAY_DRAW") {
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            Assert.Equal(0, payment.ManaCost); Assert.Equal(1, payment.PowerCost);
            Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Purple]);
            Assert.Null(game.State.PriorityPlayerId);
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
            Assert.Equal(before.TotalPower - (pay ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        }
        Assert.Empty(game.State.PlayerZones["P1"].Hand);
        if (mode == "CREATE_GOLD" || pay) await game.Top();
        if (mode == "CREATE_GOLD") {
            var token = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null);
            Assert.Contains(CardObjectTags.EquipmentCard, token.Tags); Assert.Contains("金币", token.Tags);
            Assert.True(token.IsExhausted); Assert.Contains(token.ObjectId, game.State.PlayerZones["P1"].Base);
            Assert.Equal(before.TotalPower, game.State.RunePools["P1"].TotalPower);
            var ability = token.CardNo == P4ActivatedAbilityCatalog.GoldTokenUnlCardNo
                ? P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId : P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId;
            await Reject(game.State, "P1", new ActivateAbilityCommand(token.ObjectId, ability, []));
        } else Assert.Equal(pay ? new[] { "P1-DRAW-1" } : [], game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("none", false)] [InlineData("blue", false)] [InlineData("purple", true)] [InlineData("wild", true)]
    public async Task InventorDrawRequiresItsOwnTraitOrWildcardPowerAndRejectedPaymentsChangeNothing(string trait, bool canPay)
    {
        var initial = ObserverPosition(Inventor);
        var pool = trait == "wild" ? new RunePool(0, 1) : new RunePool(0, 0,
            trait == "none" ? null : new Dictionary<string, int> { [trait] = 1 });
        var game = new Driver(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } });
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
        await ChooseInventorMode(game, "PAY_DRAW"); var payment = game.State.PendingPayment!;
        await Reject(game.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        if (!canPay) await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [canPay ? "PAY" : "DECLINE"]));
        if (canPay) await game.Top();
        Assert.Equal(canPay ? new[] { "P1-DRAW-1" } : [], game.State.PlayerZones["P1"].Hand);
        Assert.Equal(canPay ? 0 : pool.TotalPower, game.State.RunePools["P1"].TotalPower); await game.Replay();
    }

    [Theory]
    [InlineData("CREATE_GOLD")] [InlineData("PAY_DRAW")]
    public async Task InventorLeavingDuringResponseKeepsTheConfirmedModeAndPaidCost(string mode)
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(ObserverPosition(Inventor), "RETURN", "OGN·104/298"));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF")); await ChooseInventorMode(game, mode);
        if (mode == "PAY_DRAW") {
            var payment = game.State.PendingPayment!;
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        }
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand); await game.Top();
        if (mode == "PAY_DRAW") Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        else Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo is not null && c.Tags.Contains("金币"));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InventorHastePaysThePrintedOptionalCostAndNeitherEntryStateCountsAsExhausting(bool haste)
    {
        var initial = VenUnitEntryBasicTests.Position(Inventor); var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Inventor, [], OptionalCosts: haste ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(haste ? 16 : 17, game.State.RunePools["P1"].Mana);
        Assert.Equal(haste ? 19 : 20, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task InventorRejectsIllegalModesAndForgedRecoveryWhileKeepingTheChoiceUsable()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Inventor);
        Assert.Equal(39607, card.Id); Assert.Equal(3, card.Energy); Assert.Equal(3, card.Power); Assert.Null(card.ReturnEnergy);
        var game = new Driver(ObserverPosition(Inventor));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF")); var choice = game.State.PendingCardChoice!;
        foreach (var selected in new string[][] { [], ["UNIT"], ["missing"], ["CREATE_GOLD", "PAY_DRAW"] })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected));
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["CREATE_GOLD"]));
        var item = Assert.Single(game.State.StackItems);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with {
            UnitEvent = item.UnitEvent! with { Mode = "missing" } }] }));
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingCardChoice = choice with { LegalObjectIds = ["CREATE_GOLD"] } }));
        await ChooseInventorMode(game, "CREATE_GOLD"); await game.Top(); await game.Replay();
    }

    private const string GuideRobot = "RAD·057/167";
    private const string Repairer = "RAD·120/167";

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 3)]
    public async Task GuideRobotMaySetItsBaseMightToThePrintedCostOfACompletedCard(bool accept, int expectedPower)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(ObserverPosition(GuideRobot), "PLAY", "OGN·048/298");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·048/298", []));
        await game.Top();
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(0, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
        Assert.Empty(payment.PowerCostByTrait);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [accept ? "PAY" : "DECLINE"]));
        if (accept) await game.Top();
        Assert.Equal(expectedPower, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Fact]
    public async Task GuideRobotDoesNotTriggerForAPlayThatNeverCompletes()
    {
        var initial = AttackDamageDistributionTests.Spell(
            OfficialCopyIdentityTests.AddSpell(ObserverPosition(GuideRobot), "PLAY", "OGN·123/298"),
            "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("PLAY", "OGN·123/298", []));
        var spell = Assert.Single(game.State.StackItems).StackItemId;
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingPayment);
        Assert.Equal(3, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(game.State.TriggerQueue, trigger => trigger.SourceCardNo == GuideRobot);
        await game.Replay();
    }

    private static MatchState RepairerPosition(string playedCard)
    {
        var initial = ObserverPosition(Repairer);
        var cards = initial.CardObjects.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        cards["HIGH"] = OfficialGarenConquestBattlefieldCountTests.Printed("HIGH", playedCard, "P1");
        locations["HIGH"] = new("P1", "HAND");
        var zones = initial.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value);
        zones["P1"] = zones["P1"] with {
            Hand = ["HIGH"],
            Base = zones["P1"].Base.Where(id => id != "UNIT").ToArray(),
            Graveyard = [..zones["P1"].Graveyard, "UNIT"]
        };
        locations["UNIT"] = new("P1", "GRAVEYARD");
        return initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    [Fact]
    public async Task RepairerCanBePaidWithYellowPowerToPlayFromItsGraveyardAfterASevenCostCard()
    {
        var game = new Driver(RepairerPosition("OGN·088/298"));
        await game.Act("P1", new PlayCardCommand("HIGH", "OGN·088/298", [], Destination: "BASE"));
        var item = Assert.Single(game.State.StackItems, item => item.HighCostPlayObserver?.CardNo == Repairer);
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(0, payment.ManaCost); Assert.Equal(0, payment.PowerCost);
        Assert.Equal(1, payment.PowerCostByTrait[RuneTrait.Yellow]);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        for (var i = 0; i < 2 && game.State.PendingEffectPlay is null && game.State.StackItems.Count > 0; i++)
        {
            Assert.NotNull(game.State.PriorityPlayerId);
            await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.NotNull(game.State.PendingEffectPlay);
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        await game.Act("P1", new PlayCardCommand("UNIT", Repairer, [], Destination: "BASE"));
        if (game.State.StackItems.Count > 0 && game.State.PriorityPlayerId is not null)
            await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingEffectPlay);
        await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298", false)]
    [InlineData("OGN·088/298", true)]
    public async Task RepairerOnlyObservesACompletedCardWithAtLeastSevenPrintedMana(string playedCard, bool qualifies)
    {
        var game = new Driver(RepairerPosition(playedCard));
        await game.Act("P1", new PlayCardCommand("HIGH", playedCard, [], Destination: "BASE"));
        var observed = game.State.StackItems.Any(item => item.HighCostPlayObserver?.CardNo == Repairer);
        Assert.Equal(qualifies, observed);
        if (qualifies)
        {
            var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
            await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        }
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }

    [Fact]
    public async Task RepairerOnTheFieldDoesNotUseItsGraveyardPlayTrigger()
    {
        var initial = ObserverPosition(Repairer);
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HIGH"] = OfficialGarenConquestBattlefieldCountTests.Printed("HIGH", "OGN·088/298", "P1")
        };
        var locations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
            ["HIGH"] = new("P1", "HAND")
        };
        var zones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Hand = ["HIGH"] }
        };
        var game = new Driver(initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones });
        await game.Act("P1", new PlayCardCommand("HIGH", "OGN·088/298", [], Destination: "BASE"));
        Assert.DoesNotContain(game.State.TriggerQueue, item => item.SourceCardNo == Repairer);
        Assert.DoesNotContain(game.State.StackItems, item => item.HighCostPlayObserver?.CardNo == Repairer);
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task RepairerRejectsWrongTraitPaymentAndForgedObserverSourceWithoutChangingState()
    {
        var initial = RepairerPosition("OGN·088/298");
        var cards = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HIGH"] = initial.CardObjects["HIGH"]
        };
        var game = new Driver(initial with {
            CardObjects = cards,
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
                ["P1"] = new RunePool(20, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 })
            }
        });
        await game.Act("P1", new PlayCardCommand("HIGH", "OGN·088/298", [], Destination: "BASE"));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        await Reject(game.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await Reject(game.State, "P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        var item = Assert.Single(game.State.StackItems);
        var forged = item with { HighCostPlayObserver = item.HighCostPlayObserver! with {
            Source = item.HighCostPlayObserver.Source with { Generation = item.HighCostPlayObserver.Source.Generation + 1 }
        } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [forged] }));
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["DECLINE"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(GuideRobot, 39553, 4, 3, TriggerKinds.ControllerCardPlayBasePowerSource)]
    [InlineData(Repairer, 39625, 2, 2, TriggerKinds.ControllerHighCostGraveyardPlaySource)]
    public async Task OfficialResourceObserverPrintingsAndTriggerSpecsRemainBound(string no, long id, int mana, int power, string kind)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power);
        var spec = Assert.Single(TriggerParser.Parse(card.CardEffect));
        Assert.Equal(kind, spec.Kind); Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(power, behavior.SourceUnitPower);
    }
}
