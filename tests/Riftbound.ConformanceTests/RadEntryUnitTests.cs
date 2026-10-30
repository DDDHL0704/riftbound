using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadEntryUnitTests
{
    private static readonly string[] Numbers = ["RAD·031/167", "RAD·081/167", "RAD·095/167", "RAD·099/167", "RAD·129/167"];

    [Theory]
    [InlineData("RAD·031/167", 39524, 4, 2)]
    [InlineData("RAD·081/167", 39580, 6, 5)]
    [InlineData("RAD·095/167", 39597, 4, 4)]
    [InlineData("RAD·099/167", 39601, 3, 3)]
    [InlineData("RAD·129/167", 39634, 2, 1)]
    public async Task ExactOfficialPrintHasItsFullCostPowerAndBehavior(string no, long id, int mana, int might)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(might, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(mana, behavior.ManaCost); Assert.Equal(might, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(no).Amount);
        Assert.False(CardBehaviorRegistry.TryGetByCardNo(no.Replace("/167", ""), out _));
    }

    public static IEnumerable<object[]> Entries() => Numbers.SelectMany(no => new[] { "BASE", "BATTLEFIELD:BF" }.Select(d => new object[] { no, d }));
    [Theory]
    [MemberData(nameof(Entries))]
    public async Task PermanentEntersBeforeItsIndependentPlaySkillResolves(string no, string destination)
    {
        var initial = Position(no); var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Empty(driver.State.PlayerZones["P1"].Hand); Assert.True(driver.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", driver.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(initial.CardObjects["UNIT"].Power, driver.State.CardObjects["UNIT"].Power);
        if (no == "RAD·031/167" && destination != "BASE" || no == "RAD·081/167") {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.DoesNotContain("UNIT", choice.LegalObjectIds); Assert.Contains("ALLY", choice.LegalObjectIds);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        }
        if (driver.State.StackItems.Count > 0) await driver.Top();
        if (no == "RAD·129/167") {
            var choice = Assert.IsType<PendingCardChoiceState>(driver.State.PendingCardChoice);
            Assert.Equal("DECK_EFFECT", choice.ChoiceWindow);
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"]));
            Assert.Equal(new[] { "P1-DRAW-1" }, driver.State.PlayerZones["P1"].Hand);
            Assert.Single(driver.Events(), e => e.Event.Kind == "CARD_REVEALED");
        }
        if (no == "RAD·031/167") Assert.Equal(destination == "BASE" ? "BATTLEFIELD" : "BASE", driver.State.ObjectLocations["ALLY"].Zone);
        if (no == "RAD·081/167") {
            Assert.Equal(initial.CardObjects["ALLY"].Power + 3, driver.State.CardObjects["ALLY"].Power);
            Assert.DoesNotContain("UNIT", driver.State.PlayerZones["P1"].MainDeck);
            await driver.Act("P1", new EndTurnCommand());
            Assert.Equal(initial.CardObjects["ALLY"].Power, driver.State.CardObjects["ALLY"].Power);
        }
        if (no == "RAD·095/167") Assert.DoesNotContain(CardObjectTags.Ephemeral, driver.State.CardObjects["UNIT"].Tags);
        if (no == "RAD·099/167") foreach (var player in new[] { "P1", "P2" })
            Assert.Equal(new[] { player + "-DRAW-1" }, driver.State.PlayerZones[player].Hand);
        Assert.Empty(driver.State.StackItems); await driver.Replay();
    }

    public static IEnumerable<object[]> Rejections() => Numbers.SelectMany(no => new[] { "poor", "wrong-turn", "graveyard", "enemy-field", "alias" }.Select(b => new object[] { no, b }));
    [Theory]
    [MemberData(nameof(Rejections))]
    public async Task IllegalUnitPlaysDoNotSpendResourcesOrMutateThePosition(string no, string branch)
    {
        var state = Position(no);
        if (branch == "poor") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 0) } };
        if (branch == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (branch == "graveyard") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [], Graveyard = ["UNIT", "GRAVE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        await Reject(state, "P1", new PlayCardCommand("UNIT", branch == "alias" ? no.Replace("/167", "") : no, [],
            Destination: branch == "enemy-field" ? "BATTLEFIELD:OTHER" : "BASE"));
    }

    [Theory]
    [InlineData("RAD·031/167", "UNIT")] [InlineData("RAD·031/167", "ENEMY")]
    [InlineData("RAD·031/167", "GEAR")] [InlineData("RAD·031/167", "GRAVE")]
    [InlineData("RAD·081/167", "UNIT")] [InlineData("RAD·081/167", "ENEMY")]
    [InlineData("RAD·081/167", "GEAR")] [InlineData("RAD·081/167", "GRAVE")]
    public async Task PrintedOtherFriendlyUnitRestrictionRejectsIllegalTargets(string no, string target)
    {
        var driver = new Driver(Position(no));
        await driver.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BATTLEFIELD:BF"));
        var choice = driver.State.PendingCardChoice!;
        Assert.DoesNotContain(target, choice.LegalObjectIds);
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Top(); await driver.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FerrymanCanDeclineAndCannotChooseAUnitInAnotherLocation(bool decline)
    {
        var initial = Position("RAD·031/167");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "REMOTE", "OGN·219/298", "BF", "P1");
        initial = LocalDestructionRecallTests.At(initial, "REMOTE", "P1", "BASE");
        var driver = new Driver(initial);
        await driver.Act("P1", new PlayCardCommand("UNIT", "RAD·031/167", [], Destination: "BATTLEFIELD:BF"));
        var choice = driver.State.PendingCardChoice!;
        Assert.DoesNotContain("REMOTE", choice.LegalObjectIds);
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["REMOTE"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["ALLY"]));
        if (!decline) await driver.Top();
        Assert.Empty(driver.State.StackItems);
        Assert.Equal(decline ? "BATTLEFIELD" : "BASE", driver.State.ObjectLocations["ALLY"].Zone);
        await driver.Replay();
    }

    [Theory]
    [InlineData("UNIT")] [InlineData("ALLY")]
    public async Task FerrymanRevalidatesItsHereReferenceAfterAResponse(string returned)
    {
        var driver = new Driver(OfficialCopyIdentityTests.AddSpell(Position("RAD·031/167"), "RETURN", "OGN·104/298"));
        await driver.Act("P1", new PlayCardCommand("UNIT", "RAD·031/167", [], Destination: "BATTLEFIELD:BF"));
        var choice = driver.State.PendingCardChoice!;
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned])); await driver.Top();
        await driver.Top();
        Assert.Equal(returned == "UNIT" ? "BATTLEFIELD" : "HAND", driver.State.ObjectLocations["ALLY"].Zone);
        await driver.Replay();
    }

    [Theory]
    [InlineData("RAD·081/167", "none")] [InlineData("RAD·081/167", "target")]
    [InlineData("RAD·081/167", "source")] [InlineData("RAD·095/167", "none")]
    [InlineData("RAD·095/167", "source")]
    public async Task StandbyPlayUsesTheEnteredSourceAndLeavesIndependentTargetInstructionsIntact(string no, string response)
    {
        var state = StandbySourceRulesTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHADOW"] = OfficialGarenConquestBattlefieldCountTests.Printed("SHADOW", no, "P1"),
            ["HOST"] = OfficialGarenConquestBattlefieldCountTests.Printed("HOST", state.CardObjects["HOST"].CardNo!, "P1") } };
        state = AddToHand(state, "REPLY", "OGN·048/298", "P2");
        state = AddToHand(state, "RETURN", "OGN·104/298", "P1");
        // A physical rune pays the response after the new turn clears the pool.
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["RETURN-RUNE"] = new("RETURN-RUNE", tags: [CardObjectTags.RuneCard, "COLOR:blue"], cardNo: "UNL-R03", ownerId: "P1", controllerId: "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RETURN-RUNE"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "RETURN-RUNE"] } } };
        var driver = new Driver(state);
        await driver.Act("P1", new HideCardCommand("SHADOW", no, "BATTLEFIELD:F", ["STANDBY_A"]));
        await Reject(driver.State, "P1", new PlayCardCommand("SHADOW", no, []));
        await driver.Act("P1", new EndTurnCommand());
        await driver.TapRunes("P2", ["SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2"]);
        await driver.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await driver.Act("P2", new PassPriorityCommand());
        var power = driver.State.CardObjects["HOST"].Power;
        await driver.Act("P1", new PlayCardCommand("SHADOW", no, []));
        var generation = driver.State.CardObjects["SHADOW"].ObjectGeneration;
        Assert.DoesNotContain(CardObjectTags.Ephemeral, driver.State.CardObjects["SHADOW"].Tags);
        if (no == "RAD·081/167") {
            var choice = driver.State.PendingCardChoice!;
            await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["HOST"]));
        }
        if (response != "none") {
            await driver.Act("P1", new TapRuneCommand("RETURN-RUNE"));
            await driver.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [response == "source" ? "SHADOW" : "HOST"]));
            await driver.Top();
        }
        await driver.Top();
        if (no == "RAD·081/167") {
            Assert.Equal(response == "target" ? power : power + 3, driver.State.CardObjects["HOST"].Power);
            Assert.Equal(response == "source" ? "HAND" : "MAIN_DECK", driver.State.ObjectLocations["SHADOW"].Zone);
            if (response != "source") Assert.True(driver.State.CardObjects["SHADOW"].ObjectGeneration > generation);
        }
        else {
            Assert.Equal(response == "source" ? "HAND" : "BATTLEFIELD", driver.State.ObjectLocations["SHADOW"].Zone);
            Assert.Equal(response != "source", driver.State.CardObjects["SHADOW"].Tags.Contains(CardObjectTags.Ephemeral));
        }
        await driver.Top(); Assert.Empty(driver.State.StackItems);
        if (no == "RAD·095/167" && response == "none") {
            await driver.Act("P2", new EndTurnCommand());
            Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Battlefields);
            Assert.Single(driver.State.StackItems, item => item.EphemeralStart is not null);
            await driver.Top();
            Assert.Contains("SHADOW", driver.State.PlayerZones["P1"].Graveyard);
            Assert.DoesNotContain(CardObjectTags.Ephemeral, driver.State.CardObjects["SHADOW"].Tags);
        }
        await driver.Replay();
    }

    [Theory]
    [InlineData("P1-DRAW-1")] [InlineData("P1-DRAW-2")] [InlineData("")]
    public async Task CaptainRevealsAndDrawsAnEligibleUnitOrEquipmentAndRecyclesTheRest(string selected)
    {
        var driver = new Driver(Position("RAD·129/167"));
        await driver.Act("P1", new PlayCardCommand("UNIT", "RAD·129/167", [], Destination: "BASE"));
        Assert.Null(driver.State.PendingCardChoice); await driver.Top();
        var choice = driver.State.PendingCardChoice!;
        Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, choice.LegalObjectIds);
        foreach (var invalid in new[] { "P1-DRAW-3", "P1-DRAW-4", "P2-DRAW-1", "UNIT" })
            await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [invalid]));
        await Reject(driver.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"]));
        Assert.DoesNotContain("P1-DRAW-1", JsonSerializer.Serialize(ResolutionResult.BuildPrompts(driver.State)["P2"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, selected.Length == 0 ? [] : [selected]));
        Assert.Equal(selected.Length == 0 ? 0 : 1, driver.State.PlayerZones["P1"].Hand.Count);
        Assert.Equal(selected.Length == 0 ? 4 : 3, driver.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(selected.Length == 0 ? 0 : 1, driver.Events().Count(e => e.Event.Kind == "CARD_REVEALED"));
        Assert.Equal(selected.Length == 0 ? 0 : 1, driver.Events().Count(e => e.Event.Kind == "CARD_DRAWN"));
        Assert.Empty(driver.State.StackItems); await driver.Replay();
    }

    [Theory]
    [InlineData("OGN·048/298")] [InlineData("OGN·219/298")] [InlineData("OGN·150/298")]
    public async Task CaptainRejectsSpellsExcessManaAndExcessPrintedPower(string top)
    {
        var initial = Position("RAD·129/167");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["P1-DRAW-1"] = OfficialGarenConquestBattlefieldCountTests.Printed("P1-DRAW-1", top, "P1") } };
        var driver = new Driver(initial); await driver.Act("P1", new PlayCardCommand("UNIT", "RAD·129/167", [], Destination: "BASE"));
        await driver.Top(); var choice = driver.State.PendingCardChoice!;
        Assert.DoesNotContain("P1-DRAW-1", choice.LegalObjectIds);
        await Reject(driver.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1-DRAW-1"]));
        await driver.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [])); await driver.Replay();
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary(); var zones = state.PlayerZones.ToDictionary();
        var deck = new[] { "P1-DRAW-1", "P1-DRAW-2", "P1-DRAW-3", "P1-DRAW-4" };
        var prints = new[] { "OGN·037/298", "SFD·022/221", "OGN·219/298", "OGN·150/298" };
        for (var i = 0; i < deck.Length; i++) { cards[deck[i]] = OfficialGarenConquestBattlefieldCountTests.Printed(deck[i], prints[i], "P1"); locations[deck[i]] = new("P1", "MAIN_DECK"); }
        zones["P1"] = zones["P1"] with { MainDeck = deck };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static MatchState AddToHand(MatchState state, string id, string no, string owner) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-rad-entry", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }
}
