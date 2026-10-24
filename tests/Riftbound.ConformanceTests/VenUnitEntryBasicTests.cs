using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenUnitEntryBasicTests
{
    private static readonly string[] Cards = ["VEN·026", "VEN·048", "VEN·118"];

    [Theory]
    [InlineData("VEN·026", 37714, 4, 3)]
    [InlineData("VEN·048", 37739, 6, 5)]
    [InlineData("VEN·118", 37818, 6, 6)]
    public async Task ExactOfficialSourceKeepsEveryPrintedUnitCostPowerAndRule(string no, long id, int mana, int power)
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal(no switch {
            "VEN·026" => "当你打出我时，给予一名单位在本回合内{{S}}+3。",
            "VEN·048" => "当你打出我时，抽一张牌。",
            _ => "{{壁垒}}（我在战斗中首先承担伤害。）" }, card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(mana, behavior.ManaCost); Assert.Equal(power, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
    }

    public static IEnumerable<object[]> Entries() => Cards.SelectMany(no => new[] { "BASE", "BATTLEFIELD:BF" }.Select(dest => new object[] { no, dest }));
    [Theory]
    [MemberData(nameof(Entries))]
    public async Task AUnitPaysAndEntersBeforeItsPrintedTriggerMakesAnyChoiceOrResolves(string no, string destination)
    {
        var initial = Position(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", driver.State.ObjectLocations["UNIT"].Zone);
        Assert.True(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(no == "VEN·026" ? 16 : 14, driver.State.RunePools["P1"].Mana);
        Assert.Empty(driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.Events(), e => e.Event.Kind == (destination == "BASE" ? "UNIT_PLAYED_TO_BASE" : "UNIT_PLAYED_TO_BATTLEFIELD"));
        var kinds = driver.Events().Select(e => e.Event.Kind).ToArray();
        var entry = Array.IndexOf(kinds, destination == "BASE" ? "UNIT_PLAYED_TO_BASE" : "UNIT_PLAYED_TO_BATTLEFIELD");
        Assert.True(Array.IndexOf(kinds, "COST_PAID") < entry);
        if (no != "VEN·118") Assert.True(entry < Array.IndexOf(kinds, "TRIGGER_QUEUED"));
        if (no == "VEN·026") {
            Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow); Assert.Equal("UNIT", choice.SourceObjectId);
            Assert.Contains("UNIT", choice.LegalObjectIds); Assert.Contains("ALLY", choice.LegalObjectIds); Assert.Contains("ENEMY", choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
            Assert.Equal(3, driver.State.CardObjects["UNIT"].Power);
            await driver.Top(); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power);
        }
        if (no == "VEN·048") {
            Assert.Single(driver.State.StackItems); await driver.Top();
            Assert.Equal(new[] { "P1-DRAW-1" }, driver.State.PlayerZones["P1"].Hand);
            Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN");
            Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(driver.State)["P2"]));
        }
        if (no == "VEN·118") Assert.Contains(CardCombatKeywordNames.Bulwark, driver.State.CardObjects["UNIT"].Tags);
        Assert.Empty(driver.State.StackItems); await driver.Replay();
    }

    [Theory]
    [InlineData("UNIT")] [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task TheBandsPostEntryTargetCanBeItselfFriendOrEnemyAndTheBonusExpires(string target)
    {
        var initial = Position("VEN·026"); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!; var power = driver.State.CardObjects[target].Power;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await driver.Top(); Assert.Equal(power + 3, driver.State.CardObjects[target].Power);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(power, driver.State.CardObjects[target].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("equipment")] [InlineData("graveyard")] [InlineData("wrong-player")]
    [InlineData("missing")] [InlineData("extra")] [InlineData("wrong-window")]
    public async Task IllegalBandTargetLeavesThePendingPostEntryChoiceUsable(string branch)
    {
        var driver = new Driver(Position("VEN·026"));
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await Reject(driver.State, branch == "wrong-player" ? "P2" : "P1", new ChooseCardsCommand(choice.ChoiceId,
            branch == "wrong-window" ? "MAIN_DECK" : choice.ChoiceWindow,
            branch == "missing" ? [] : branch == "extra" ? ["UNIT", "ALLY"] :
                [branch == "equipment" ? "GEAR" : branch == "graveyard" ? "GRAVE" : "UNIT"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await driver.Top(); Assert.Equal(6, driver.State.CardObjects["UNIT"].Power); await driver.Replay();
    }

    [Fact]
    public async Task TheBandsChoiceExcludesHiddenAndOutOfPlayUnitsButIncludesBothBattlefields()
    {
        var state = Position("VEN·026"); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var zones = state.PlayerZones.ToDictionary();
        foreach (var player in new[] { "P1", "P2" }) {
            var id = player + "-FIELD"; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "OGN·219/298", player);
            locations[id] = new(player, "BATTLEFIELD", player == "P1" ? "BF" : "OTHER");
            zones[player] = zones[player] with { Battlefields = [..zones[player].Battlefields, id] };
        }
        cards["SECRET"] = OfficialGarenConquestBattlefieldCountTests.Printed("SECRET", "VEN·048", "P2") with { IsFaceDown = true };
        locations["SECRET"] = new("P2", "BATTLEFIELD", "OTHER"); zones["P2"] = zones["P2"] with { Battlefields = [..zones["P2"].Battlefields, "SECRET"] };
        var driver = new Driver(state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones });
        await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        Assert.Equal(new[] { "ALLY", "ENEMY", "P1-FIELD", "P2-FIELD", "UNIT" }, choice.LegalObjectIds.Order().ToArray());
        Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P1"]));
        foreach (var target in new[] { "SECRET", "P1-DRAW-1", "GRAVE" })
            await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2-FIELD"]));
        await driver.Top(); Assert.Equal(cards["P2-FIELD"].Power + 3, driver.State.CardObjects["P2-FIELD"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task TheBandsWardCostIsConfirmedAfterEntryAndDecliningOnlyRemovesItsSkill(bool pay)
    {
        var initial = Position("VEN·026");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(4, 1) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        Assert.Equal(0, driver.State.RunePools["P1"].Mana); Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        var payment = Assert.IsType<PendingPaymentState>(driver.State.PendingPayment);
        Assert.Equal(1, payment.PowerCost); Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        await Reject(driver.State, "P2", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await driver.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, [pay ? "PAY" : "DECLINE"]));
        if (pay) await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base); Assert.Empty(driver.State.StackItems);
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(initial.CardObjects["ENEMY"].Power + (pay ? 3 : 0), driver.State.CardObjects["ENEMY"].Power);
        await driver.Replay();
    }

    [Theory]
    [InlineData("VEN·026", false)] [InlineData("VEN·026", true)]
    [InlineData("VEN·048", false)] [InlineData("VEN·048", true)]
    public async Task TheControllerOrdersThePrintedEntrySkillAndRengarsIndependentTrigger(string no, bool legendFirst)
    {
        var initial = RengarUnitPlayConfirmationTests.WithLegend(Position(no)); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        var own = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "UNIT");
        var legend = Assert.Single(driver.State.TriggerQueue, t => t.SourceObjectId == "LEGEND");
        await driver.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: legendFirst ? [legend.TriggerId, own.TriggerId] : [own.TriggerId, legend.TriggerId]));
        while (driver.State.PendingCardChoice is { } choice)
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        Assert.Equal(2, driver.State.StackItems.Count);
        var power = initial.CardObjects["UNIT"].Power;
        Assert.Equal(power, driver.State.CardObjects["UNIT"].Power);
        await driver.Top();
        Assert.Equal(power + (legendFirst ? 1 : no == "VEN·026" ? 3 : 0), driver.State.CardObjects["UNIT"].Power);
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(power + (no == "VEN·026" ? 4 : 1), driver.State.CardObjects["UNIT"].Power);
        if (no == "VEN·048") Assert.Equal(new[] { "P1-DRAW-1" }, driver.State.PlayerZones["P1"].Hand);
        await driver.Act("P1", new EndTurnCommand()); Assert.Equal(power, driver.State.CardObjects["UNIT"].Power);
        await driver.Replay();
    }

    [Fact]
    public async Task TheDrakesQueuedDrawRemainsIndependentAfterTheDrakeLeavesPlay()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position("VEN·048"), "RETURN", "OGN·104/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·048", [], Destination: "BASE"));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await driver.Top();
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Hand); Assert.Single(driver.State.StackItems);
        await driver.Top(); Assert.Contains("P1-DRAW-1", driver.State.PlayerZones["P1"].Hand);
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN"); await driver.Replay();
    }

    [Theory]
    [InlineData("UNIT")] [InlineData("ALLY")]
    public async Task TheBandsConfirmedSkillSurvivesItsSourceAndRevalidatesItsTarget(string returned)
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position("VEN·026"), "RETURN", "OGN·104/298");
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·026", [], Destination: "BASE"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await driver.Top();
        Assert.Contains(returned, driver.State.PlayerZones["P1"].Hand); Assert.Single(driver.State.StackItems);
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        Assert.Equal(initial.CardObjects["ALLY"].Power + (returned == "UNIT" ? 3 : 0), driver.State.CardObjects["ALLY"].Power);
        await driver.Replay();
    }

    [Fact]
    public async Task TheDrakesDrawFromAnEmptyDeckCompletesTheFirstBurnoutAndDrawsOne()
    {
        var initial = Position("VEN·048"); var deck = initial.PlayerZones["P1"].MainDeck;
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = ["GRAVE", ..deck] } },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => deck.Contains(p.Key) ? new ObjectLocationState("P1", "GRAVEYARD") : p.Value) };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·048", [], Destination: "BASE")); await driver.Top();
        Assert.Single(driver.State.PlayerZones["P1"].Hand); Assert.Equal(3, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(1, driver.State.PlayerScores["P2"]); Assert.Single(driver.Events(), e => e.Event.Kind == "BURNOUT_APPLIED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_DRAWN"); Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        await driver.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheDrakesEmptyDeckDrawCompletesRecyclingOrRepeatedBurnoutBeforeEnding(bool noRecyclableCards)
    {
        var initial = Position("VEN·048"); var deck = initial.PlayerZones["P1"].MainDeck;
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { MainDeck = [], Graveyard = noRecyclableCards ? [] : ["GRAVE", ..deck] } },
            PlayerScores = new Dictionary<string, int> { ["P1"] = 0, ["P2"] = 7 },
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key,
                p => deck.Contains(p.Key) || p.Key == "GRAVE" ? new ObjectLocationState("P1", noRecyclableCards ? "BANISHED" : "GRAVEYARD") : p.Value) };
        if (noRecyclableCards) initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with { Banished = ["GRAVE", ..deck] } } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "VEN·048", [], Destination: "BASE")); await driver.Top();
        Assert.Equal(noRecyclableCards ? 0 : 1, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(noRecyclableCards ? 9 : 8, driver.State.PlayerScores["P2"]);
        Assert.Equal(noRecyclableCards ? 2 : 1, driver.Events().Count(e => e.Event.Kind == "BURNOUT_APPLIED"));
        Assert.Equal("P2", driver.State.WinnerPlayerId); Assert.Equal(MatchStatuses.Finished, driver.State.Status);
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Base);
        if (!noRecyclableCards) {
            var events = driver.Events().Select(e => e.Event.Kind).ToArray();
            Assert.True(Array.IndexOf(events, "CARD_DRAWN") < Array.IndexOf(events, "MATCH_WON"));
        }
        await driver.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheWarriorsPrintedBulwarkRequiresLethalBeforeAnOrdinaryAllyAndAllowsEitherBulwarkFirst(bool secondBulwark)
    {
        var state = Position("VEN·118"); var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        // PrintedCardFactory creates the outside-play face. This controlled
        // battle fixture also needs the registry tags installed on entry;
        // AUnitPaysAndEnters independently checks that real entry does so.
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("VEN·118", out var warrior));
        cards["UNIT"] = cards["UNIT"] with { Tags = [..cards["UNIT"].Tags, ..warrior.SourceUnitTags.Split('|')] };
        cards["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2");
        var defenders = new List<string> { "ALLY", "UNIT" };
        if (secondBulwark) {
            Assert.True(CardBehaviorRegistry.TryGetByCardNo("OGN·087/298", out var instructor));
            cards["OTHER-WALL"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER-WALL", "OGN·087/298", "P1") with { Tags = [CardObjectTags.UnitCard, ..instructor.SourceUnitTags.Split('|')] };
            Assert.Contains(CardCombatKeywordNames.Bulwark, cards["OTHER-WALL"].Tags); defenders.Add("OTHER-WALL");
        }
        Assert.Contains(CardCombatKeywordNames.Bulwark, cards["UNIT"].Tags);
        foreach (var id in defenders) locations[id] = new("P1", "BATTLEFIELD", "BF");
        locations["ENEMY"] = new("P2", "BATTLEFIELD", "BF");
        var zones = state.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Base = ["GEAR"], Hand = [], Battlefields = ["BF", ..defenders] };
        zones["P2"] = zones["P2"] with { Base = [], Battlefields = ["OTHER", "ENEMY"] };
        var driver = new Driver(state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones,
            ActivePlayerId = "P2", TurnPlayerId = "P2", UntilEndOfTurnEffects = [BattlefieldTaskMarkers.SpellDuelCompleted("BF")] });
        await driver.Act("P2", new DeclareBattleCommand("BF", ["ENEMY"], defenders, OptionalCosts: ["COMBAT_ASSIGNMENT"]));
        Assert.Equal(PromptTypes.AssignCombatDamage, ResolutionResult.BuildPrompts(driver.State)["P2"].View!.Type);
        await Reject(driver.State, "P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "UNIT", 5), new("ENEMY", "ALLY", 3)]));
        if (secondBulwark) {
            await Reject(driver.State, "P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "UNIT", 6), new("ENEMY", "ALLY", 2)]));
            await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "OTHER-WALL", 2), new("ENEMY", "UNIT", 6)]));
        }
        else await driver.Act("P2", new AssignCombatDamageCommand("battle:BF", "BF", [new("ENEMY", "UNIT", 6), new("ENEMY", "ALLY", 2)]));
        var damage = defenders.Select(id => new CombatDamageAssignmentDto(id, "ENEMY", cards[id].Power)).ToArray();
        await driver.Act("P1", new AssignCombatDamageCommand("battle:BF", "BF", damage));
        Assert.Contains("UNIT", driver.State.PlayerZones["P1"].Graveyard); Assert.DoesNotContain("ALLY", driver.State.PlayerZones["P1"].Graveyard);
        Assert.False(driver.State.BattleState.IsActive); await driver.Replay();
    }

    public static IEnumerable<object[]> Rejections() => Cards.SelectMany(no => new[] { "poor", "wrong-turn", "response", "graveyard", "opponent-owner", "enemy-field" }
        .Select(branch => new object[] { no, branch }));
    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task OrdinaryUnitPlayStillRequiresLegalTimingSourceLocationAndPayment(string no, string branch)
    {
        var state = Position(no); var destination = branch == "enemy-field" ? "BATTLEFIELD:OTHER" : "BASE";
        if (branch == "poor") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "response") {
            state = OfficialCopyIdentityTests.AddSpell(state, "RESPONSE", "OGN·048/298");
            var opened = await new CoreRuleEngine().ResolveAsync(state, new("open-response", "P1", CommandTypes.PlayCard), new PlayCardCommand("RESPONSE", "OGN·048/298", []), default);
            Assert.True(opened.Accepted, opened.ErrorMessage); state = opened.State;
        }
        if (branch == "graveyard") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = ["GRAVE", "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        if (branch == "opponent-owner") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-unit-entry", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    internal static MatchState Position(string no)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string cardNo, string owner, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, owner); locations[id] = new(owner, zone, field); }
        Put("UNIT", no, "P1", "HAND"); Put("ALLY", "OGN·219/298", "P1", "BASE"); Put("ENEMY", "OGN·219/298", "P2", "BASE");
        Put("GEAR", "SFD·022/221", "P1", "BASE"); Put("GRAVE", "OGN·219/298", "P1", "GRAVEYARD");
        Put("BF", "OGN·278/298", "P1", "BATTLEFIELD", "BF"); Put("OTHER", "OGN·278/298", "P2", "BATTLEFIELD", "OTHER");
        cards["BF"] = cards["BF"] with { ControllerId = "P1" }; cards["OTHER"] = cards["OTHER"] with { ControllerId = "P2" };
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var p in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 3).Select(i => p + "-DRAW-" + i).ToArray(); foreach (var id in deck) Put(id, "OGN·219/298", p, "MAIN_DECK");
            zones[p] = PlayerZones.Empty with { Base = p == "P1" ? ["ALLY", "GEAR"] : ["ENEMY"], Battlefields = p == "P1" ? ["BF"] : ["OTHER"],
                Hand = p == "P1" ? ["UNIT"] : [], Graveyard = p == "P1" ? ["GRAVE"] : [], MainDeck = deck };
        }
        return new MatchState("VEN-UNIT-ENTRY", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            playerZones: zones, cardObjects: cards, objectLocations: locations, runePools: new Dictionary<string, RunePool> { ["P1"] = new(20, 20), ["P2"] = new(20, 20) }, seed: 424242)
            with { CardPlays = new(5, [], []) };
    }
}
