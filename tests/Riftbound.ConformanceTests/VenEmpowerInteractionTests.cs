using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenEmpowerInteractionTests
{
    [Theory]
    [InlineData("VEN·037", 37725, 4, 4)] [InlineData("VEN·082", 37776, 4, 4)]
    [InlineData("VEN·099", 37796, 3, 3)] [InlineData("VEN·104", 37801, 4, 4)]
    [InlineData("VEN·114", 37814, 6, 5)]
    public async Task PrintedUnitPaysAndEntersWithoutInventingAnUnmetTrigger(string no, long id, int mana, int might)
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, printed.Id); Assert.Equal(mana, printed.Energy); Assert.Equal(might, printed.Power); Assert.Null(printed.ReturnEnergy);
        var initial = VenUnitEntryBasicTests.Position(no);
        await Reject(initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } },
            "P1", new PlayCardCommand("UNIT", no, []));
        await Reject(initial, "P1", new PlayCardCommand("UNIT", no, ["ALLY"]));
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(might, game.State.CardObjects["UNIT"].Power); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(6, false)] [InlineData(6, true)] [InlineData(7, false)] [InlineData(7, true)]
    public async Task BarbaraChecksSevenRunesAtPlayAndEitherDisenchantsOrDestroysEnemyEquipment(int runes, bool empowered)
    {
        var initial = EmpowerTests.Runes(VenUnitEntryBasicTests.Position("VEN·037"), runes);
        initial = Move(initial, "GEAR", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { IsEmpowered = empowered } } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·037", []));
        if (runes == 6) { Assert.Empty(game.State.StackItems); return; }
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "GEAR" }, choice.LegalObjectIds);
        foreach (var invalid in new[] { "UNIT", "ALLY", "ENEMY", "GRAVE" })
            await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [invalid]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GEAR"]));
        Assert.Contains("GEAR", game.State.PlayerZones["P2"].Base); await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsEmpowered);
        Assert.Contains("GEAR", empowered ? game.State.PlayerZones["P2"].Base : game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Theory]
    [InlineData("UNIT")] [InlineData("ALLY")] [InlineData("ENEMY")] [InlineData("GEAR")]
    public async Task MerchantPaysControlledEmpowerRemovalBeforeResponsesAndCanEmpowerEitherSide(string target)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·082");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { IsEmpowered = true } } };
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·082", []));
        var cost = game.State.PendingCardChoice!; Assert.Equal("EMPOWER_REMOVAL_COST", cost.ChoiceWindow);
        Assert.Equal(new[] { "ALLY" }, cost.LegalObjectIds);
        await Reject(game.State, "P2", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["ALLY"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["ENEMY"]));
        await game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["ALLY"]));
        Assert.False(game.State.CardObjects["ALLY"].IsEmpowered);
        var choice = game.State.PendingCardChoice!; Assert.Contains(target, choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        Assert.False(game.State.CardObjects[target].IsEmpowered); await game.Top();
        Assert.True(game.State.CardObjects[target].IsEmpowered); await game.Replay();
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task MerchantUsesItsControllerForEmpowerObserversOnEitherSidesUnits(string target)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·082");
        var cards = initial.CardObjects.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        var locations = initial.ObjectLocations.ToDictionary();
        cards["ALLY"] = cards["ALLY"] with { IsEmpowered = true };
        foreach (var player in new[] { "P1", "P2" }) {
            var id = player + "-LEGEND";
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "VEN·151", player);
            zones[player] = zones[player] with { LegendZone = [id] };
            locations[id] = new(player, "LEGEND");
        }
        var game = new Driver(initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations });
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·082", []));
        var cost = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(cost.ChoiceId, cost.ChoiceWindow, ["ALLY"]));
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await game.Top(); Assert.True(game.State.CardObjects[target].IsEmpowered);
        var observer = Assert.Single(game.State.StackItems).EmpowerOtherObserver!;
        Assert.Equal("P1", observer.ControllerId); Assert.Equal(target, observer.Observed.ObjectId);
        Assert.Equal("P1-LEGEND", observer.Source.ObjectId);
        await game.Top(); Assert.True(game.State.CardObjects["P1-LEGEND"].IsEmpowered);
        Assert.False(game.State.CardObjects["P2-LEGEND"].IsEmpowered); await game.Replay();
    }

    [Fact]
    public async Task MerchantCanDeclineWithoutRemovingEmpowerAndCannotPayWithAnOpponentsStatus()
    {
        var state = VenUnitEntryBasicTests.Position("VEN·082");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsEmpowered = true },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsEmpowered = true } } };
        var game = new Driver(state); await game.Act("P1", new PlayCardCommand("UNIT", "VEN·082", []));
        var choice = game.State.PendingCardChoice!; Assert.DoesNotContain("ENEMY", choice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.True(game.State.CardObjects["ALLY"].IsEmpowered); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MatriarchOnlyTriggersOnBecomingEmpoweredAndPlaysACostLimitedGraveyardUnitForFree(bool decline)
    {
        var state = InBase("VEN·104");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GRAVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("GRAVE", "VEN·057", "P1") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(2, 0, new Dictionary<string, int> { ["purple"] = 1 }) } };
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", []));
        Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); await game.Top();
        Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
        var choice = game.State.PendingCardChoice!; Assert.Equal(new[] { "GRAVE" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["GRAVE"]));
        if (decline) { Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Graveyard); return; }
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await Reject(game.State, "P1", new PlayCardCommand("GRAVE", "VEN·057", [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("GRAVE", "VEN·057", [], Destination: "BASE"));
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Base); Assert.True(game.State.CardObjects["GRAVE"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData("OGN·219/298")] [InlineData("UNL-166/219")]
    public async Task MatriarchRejectsGraveyardUnitsAboveEitherPrintedCostLimit(string no)
    {
        var state = InBase("VEN·104");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GRAVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("GRAVE", no, "P1") } };
        var game = new Driver(state); await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task MatriarchsCapturedSkillSurvivesItsSourceReturningToHand()
    {
        var state = AttackDamageDistributionTests.Spell(InBase("VEN·104"), "BOUNCE", "OGN·104/298", "P1");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GRAVE"] = OfficialGarenConquestBattlefieldCountTests.Printed("GRAVE", "VEN·057", "P1") } };
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["GRAVE"]));
        await game.Act("P1", new PlayCardCommand("BOUNCE", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.NotNull(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("GRAVE", "VEN·057", []));
        Assert.Contains("GRAVE", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    [Theory]
    [InlineData(0, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(3, true)]
    public async Task KarloxBurnThenPlayHasIndependentTargetConfirmationAndResponses(int deckSize, bool decline)
    {
        var state = KarloxPosition("OGN·219/298", deckSize);
        var game = new Driver(state); await EmpowerKarlox(game, deckSize);
        var choice = game.State.PendingCardChoice!;
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Contains("OLD", choice.LegalObjectIds); Assert.Equal(deckSize + 1, choice.LegalObjectIds.Count);
        Assert.Null(game.State.PendingEffectPlay);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["OLD"]));
        if (!decline) {
            Assert.NotNull(game.State.PriorityPlayerId); Assert.Null(game.State.PendingEffectPlay);
            Assert.Contains("OLD", game.State.PlayerZones["P2"].Graveyard);
            await game.Top();
            var pending = game.State.PendingEffectPlay!; Assert.Equal(new[] { "OLD" }, pending.Sources.Keys);
            Assert.False(pending.IgnoreAllCosts); Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower);
            Assert.Equal("P2", pending.SourcePlayerId);
            await Reject(game.State, "P2", new PlayCardCommand("OLD", "OGN·219/298", []));
            await game.Act("P1", new PlayCardCommand("OLD", "OGN·219/298", []));
            Assert.Contains("OLD", game.State.PlayerZones["P1"].Base); Assert.DoesNotContain("OLD", game.State.PlayerZones["P2"].Graveyard);
            Assert.Equal("P2", game.State.CardObjects["OLD"].OwnerId); Assert.Equal("P1", game.State.CardObjects["OLD"].ControllerId);
            Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        }
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task IgnoringBaseCostsStillRequiresTheChosenUnitsMandatoryAdditionalCost()
    {
        var state = KarloxPosition("UNL-166/219", 0);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "UNL-137/219", "P1") } };
        var game = new Driver(state); await EmpowerKarlox(game, 0);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OLD"])); await game.Top();
        await Reject(game.State, "P1", new PlayCardCommand("OLD", "UNL-166/219", []));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
        await game.Act("P1", new PlayCardCommand("OLD", "UNL-166/219", [], OptionalCosts: ["DESTROY_FRIENDLY_TRAIT_UNIT:ALLY"]));
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("OLD", game.State.PlayerZones["P1"].Base); await game.Replay();
    }

    private static MatchState KarloxPosition(string graveyardCard, int deckSize)
    {
        var state = InBase("VEN·114");
        var removed = state.PlayerZones["P2"].MainDeck.Skip(deckSize).ToHashSet();
        return state with { CardObjects = state.CardObjects.Where(p => !removed.Contains(p.Key)).ToDictionary() .Concat(new[] {
                new KeyValuePair<string, CardObjectState>("OLD", OfficialGarenConquestBattlefieldCountTests.Printed("OLD", graveyardCard, "P2")) }).ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Graveyard = ["OLD"], MainDeck = state.PlayerZones["P2"].MainDeck.Take(deckSize).ToArray() } },
            ObjectLocations = state.ObjectLocations.Where(p => !removed.Contains(p.Key)).Concat(new[] {
                new KeyValuePair<string, ObjectLocationState>("OLD", new("P2", "GRAVEYARD")) }).ToDictionary(),
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(6, 0, new Dictionary<string, int> { ["purple"] = 2 }) } };
    }

    private static async Task EmpowerKarlox(Driver game, int deckSize)
    {
        await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [])); await game.Top();
        var choice = game.State.PendingCardChoice!; Assert.Equal("EMPOWER_OPPONENT", choice.ChoiceWindow);
        Assert.Equal(new[] { "P2" }, choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P1"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["P2"]));
        Assert.Equal(deckSize, game.State.PlayerZones["P2"].MainDeck.Count); await game.Top();
        Assert.Empty(game.State.PlayerZones["P2"].MainDeck);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task WhirlwindWarriorOnlyGrantsTemporaryEmpowerAfterAnActualStandbyPlay(bool decline, bool alreadyEmpowered)
    {
        var initial = AttackDamageDistributionTests.Spell(StandbySourceRulesTests.Position("VEN·099"), "REPLY", "OGN·048/298", "P2");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = initial.CardObjects["HOST"] with { IsEmpowered = alreadyEmpowered } } };
        var game = new Driver(initial);
        await game.Act("P1", new HideCardCommand("SHADOW", "VEN·099", "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", new[] { 1, 2, 3 }.Select(i => "SHADOW-P2-RUNE-" + i));
        foreach (var i in new[] { 4, 5 }) await game.Act("P2", new RecycleRuneCommand("SHADOW-P2-RUNE-" + i));
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", []));
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new RevealCardCommand("SHADOW", "VEN·099", [], "STANDBY_REACTION", ["STANDBY_REVEAL_0"], "BATTLEFIELD:F"));
        if (game.State.PendingCardChoice is null) await game.Top();
        var choice = game.State.PendingCardChoice!;
        Assert.Contains("HOST", choice.LegalObjectIds); Assert.DoesNotContain("SHADOW-OUTSIDE", choice.LegalObjectIds);
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["SHADOW-OUTSIDE"]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, decline ? [] : ["HOST"]));
        if (decline) { Assert.False(game.State.CardObjects["HOST"].IsEmpowered); return; }
        Assert.Equal(alreadyEmpowered, game.State.CardObjects["HOST"].IsEmpowered); await game.Top();
        Assert.True(game.State.CardObjects["HOST"].IsEmpowered);
        Assert.Contains(game.State.UntilEndOfTurnEffects, e => e.StartsWith("DELAYED_EMPOWER:"));
        // Close the original opponent reaction after this earned trigger.
        while (game.State.StackItems.Count > 0) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new EndTurnCommand());
        for (var i = 0; i < 20 && game.State.EndStep is not null; i++) await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.False(game.State.CardObjects["HOST"].IsEmpowered); await game.Replay();
    }

    private static MatchState InBase(string no)
        => Move(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", "BASE") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(20, 0, new Dictionary<string, int> { ["purple"] = 20 }), ["P2"] = new(20, 20) } };
    private static MatchState Move(MatchState state, string id, string controller, string zone)
    {
        var zones = state.PlayerZones.ToDictionary();
        foreach (var p in zones.Keys.ToArray()) zones[p] = zones[p] with { Base = zones[p].Base.Where(x => x != id).ToArray(),
            Hand = zones[p].Hand.Where(x => x != id).ToArray() };
        zones[controller] = zones[controller] with { Base = [..zones[controller].Base, id] };
        return state with { PlayerZones = zones, ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(controller, zone) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = state.CardObjects[id] with { ControllerId = controller } } };
    }
    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("bad-empower-interaction", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }
}
