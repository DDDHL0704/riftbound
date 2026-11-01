using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

/// <summary>
/// End-to-end coverage for the RAD spell batch.  Each card has a resolving
/// path and a rejected or boundary path so that the registered behavior and
/// the authoritative command validation are exercised together.
/// </summary>
public sealed class RadSpellBatchTests
{
    private static MatchState SpellPosition(string cardNo)
        => AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position("VEN·026"), "SPELL", cardNo, "P1");

    private static MatchState AddUnitAt(MatchState state, string objectId, string cardNo, string playerId, string battlefieldId)
    {
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards[objectId] = OfficialGarenConquestBattlefieldCountTests.Printed(objectId, cardNo, playerId);
        locations[objectId] = new(playerId, "BATTLEFIELD", battlefieldId);
        var zones = state.PlayerZones.ToDictionary();
        zones[playerId] = zones[playerId] with { Battlefields = [.. zones[playerId].Battlefields, objectId] };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static MatchState AddEquipmentAt(MatchState state, string objectId, string cardNo, string playerId, string zone, string? battlefieldId = null)
    {
        var cards = state.CardObjects.ToDictionary();
        var locations = state.ObjectLocations.ToDictionary();
        cards[objectId] = OfficialGarenConquestBattlefieldCountTests.Printed(objectId, cardNo, playerId);
        locations[objectId] = new(playerId, zone, battlefieldId);
        var zones = state.PlayerZones.ToDictionary();
        zones[playerId] = zone == "BASE"
            ? zones[playerId] with { Base = [.. zones[playerId].Base, objectId] }
            : zones[playerId] with { Battlefields = [.. zones[playerId].Battlefields, objectId] };
        return state with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
    }

    private static async Task Reject(MatchState state, string playerId, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new("rad-spell-illegal", playerId, command.CmdType), command, default);
        Assert.False(result.Accepted, result.ErrorMessage);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    [Fact]
    public async Task OfficialSpellPrintsAreAdmittedWithTheirPrintedRules()
    {
        var expected = new Dictionary<string, (long Id, int Mana, string Name)>(StringComparer.Ordinal) {
            ["RAD·004/167"] = (39494, 3, "乱殴"), ["RAD·006/167"] = (39496, 4, "灼热焦土"),
            ["RAD·007/167"] = (39497, 3, "战争集结"), ["RAD·010/167"] = (39500, 2, "禁忌秘术"),
            ["RAD·032/167"] = (39525, 2, "战前仪式"), ["RAD·033/167"] = (39526, 1, "指令：攻击"),
            ["RAD·082/167"] = (39581, 4, "洪荒咆哮"), ["RAD·084/167"] = (39583, 4, "天崩地裂"),
            ["RAD·096/167"] = (39598, 3, "定点爆破"), ["RAD·098/167"] = (39600, 2, "海妖之歌"),
            ["RAD·100/167"] = (39602, 1, "过海费"), ["RAD·102/167"] = (39604, 3, "恶魔魅影"),
            ["RAD·127/167"] = (39632, 5, "辉映同心"), ["RAD·135/167"] = (39641, 8, "不朽铭记")
        };
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        foreach (var (cardNo, printed) in expected)
        {
            var card = Assert.Single(catalog.Cards, c => c.CardNo == cardNo);
            Assert.Equal(printed.Id, card.Id);
            Assert.Equal(printed.Mana, card.Energy);
            Assert.Equal(printed.Name, card.CardName);
            Assert.True(CardBehaviorRegistry.TryGetByCardNo(cardNo, out var behavior));
            Assert.Equal(printed.Mana, behavior.ManaCost);
        }
    }

    [Fact]
    public async Task AssaultUsesTheTargetCurrentPowerAndRejectsNonunits()
    {
        var game = new Driver(SpellPosition("RAD·004/167"));
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·004/167", ["ALLY"]));
        await game.Top();
        Assert.Contains(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, id => id.StartsWith("COMBAT_KEYWORD:强攻4:", StringComparison.Ordinal));
        await game.Act("P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.CardObjects["ALLY"].UntilEndOfTurnEffects, id => id.StartsWith("COMBAT_KEYWORD:强攻4:", StringComparison.Ordinal));
        await Reject(SpellPosition("RAD·004/167"), "P1", new PlayCardCommand("SPELL", "RAD·004/167", ["GEAR"]));
    }

    [Fact]
    public async Task WarGatheringCreatesThreeMinionsAtAnEnemyBattlefield()
    {
        var game = new Driver(SpellPosition("RAD·007/167"));
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·007/167", ["OTHER"]));
        await game.Top();
        var tokens = game.State.PlayerZones["P1"].Battlefields
            .Where(id => game.State.ObjectLocations.GetValueOrDefault(id)?.BattlefieldObjectId == "OTHER")
            .ToArray();
        Assert.Equal(3, tokens.Length);
        Assert.All(tokens, id => { Assert.Equal(1, game.State.CardObjects[id].Power); Assert.Equal("P1", game.State.CardObjects[id].ControllerId); });
        await Reject(SpellPosition("RAD·007/167"), "P1", new PlayCardCommand("SPELL", "RAD·007/167", ["BF"]));
    }

    [Fact]
    public async Task SpellLockMovesAUnitToTheEnemyBattlefieldAndRejectsFriendlyBattlefields()
    {
        var initial = SpellPosition("RAD·084/167");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·084/167", ["ALLY", "OTHER"]));
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        Assert.Contains(CardPermissionKeywordRules.SpellPlayProhibitionPrefix + "P2", game.State.UntilEndOfTurnEffects);
        await Reject(initial, "P1", new PlayCardCommand("SPELL", "RAD·084/167", ["ALLY", "BF"]));
    }

    [Fact]
    public async Task PrecisionBombMovesTheUnitAndCreatesADeployBombAtTheSecondTarget()
    {
        var game = new Driver(SpellPosition("RAD·096/167"));
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·096/167", ["ALLY", "OTHER"], Destination: "BATTLEFIELD:OTHER"));
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations["ALLY"].BattlefieldObjectId);
        var bomb = Assert.Single(game.State.PlayerZones["P1"].Battlefields, id => game.State.CardObjects[id].Tags.Contains(CardEquipmentKeywordNames.Deploy));
        Assert.Equal("OTHER", game.State.ObjectLocations[bomb].BattlefieldObjectId);
        await Reject(SpellPosition("RAD·096/167"), "P1", new PlayCardCommand("SPELL", "RAD·096/167", ["ALLY", "OTHER"], Destination: "BATTLEFIELD:UNKNOWN"));
    }

    [Fact]
    public async Task SirenSongMovesOnlyAnEnemyUnitThatMovedThisTurn()
    {
        var initial = SpellPosition("RAD·098/167") with {
            CardObjects = new Dictionary<string, CardObjectState>(SpellPosition("RAD·098/167").CardObjects) {
                ["ENEMY"] = SpellPosition("RAD·098/167").CardObjects["ENEMY"] with { MovementCountThisTurn = 1 } } };
        var moved = new Driver(initial);
        await moved.Act("P1", new PlayCardCommand("SPELL", "RAD·098/167", ["ENEMY"], Destination: "BATTLEFIELD:BF"));
        await moved.Top();
        Assert.Equal("BF", moved.State.ObjectLocations["ENEMY"].BattlefieldObjectId);
        await Reject(SpellPosition("RAD·098/167"), "P1", new PlayCardCommand("SPELL", "RAD·098/167", ["ENEMY"], Destination: "BATTLEFIELD:BF"));
    }

    [Fact]
    public async Task CrossingFeeCreatesOneDormantGoldAndStandbyOnOwnTurnCreatesThreeReadyGold()
    {
        var ordinary = new Driver(SpellPosition("RAD·100/167"));
        await ordinary.Act("P1", new PlayCardCommand("SPELL", "RAD·100/167", []));
        await ordinary.Top();
        var ordinaryGold = Assert.Single(ordinary.State.PlayerZones["P1"].Base, id => id == "SPELL-TOKEN-001");
        Assert.True(ordinary.State.CardObjects[ordinaryGold].IsExhausted);

        var standbyState = StandbySourceRulesTests.Position("RAD·100/167");
        var standby = new Driver(standbyState);
        await standby.Act("P1", new HideCardCommand("SHADOW", "RAD·100/167", "BATTLEFIELD:F", ["STANDBY_A"]));
        await standby.Act("P1", new EndTurnCommand());
        await standby.Act("P2", new EndTurnCommand());
        await standby.Act("P1", new RevealCardCommand("SHADOW", "RAD·100/167", [], "STANDBY_REACTION", [], "STACK"));
        await standby.Top();
        var ready = standby.State.PlayerZones["P1"].Base.Where(id => standby.State.CardObjects[id].CardNo == "SFD·T03").ToArray();
        Assert.Equal(3, ready.Length);
        Assert.All(ready, id => Assert.False(standby.State.CardObjects[id].IsExhausted));
        await Reject(SpellPosition("RAD·100/167"), "P1", new PlayCardCommand("SPELL", "RAD·100/167", ["ALLY"]));
    }

    [Fact]
    public async Task DemonShadeMovesToAnOpenBattlefieldAndOtherwiseGrantsRoam()
    {
        var openCards = StandbySourceRulesTests.Position("RAD·102/167").CardObjects.ToDictionary();
        openCards["OTHER"] = openCards["OTHER"] with { ControllerId = null };
        var open = StandbySourceRulesTests.Position("RAD·102/167") with { CardObjects = openCards };
        var openGame = new Driver(open);
        await openGame.Act("P1", new HideCardCommand("SHADOW", "RAD·102/167", "BATTLEFIELD:F", ["STANDBY_A"]));
        await openGame.Act("P1", new EndTurnCommand()); await openGame.Act("P2", new EndTurnCommand());
        await openGame.Act("P1", new RevealCardCommand("SHADOW", "RAD·102/167", ["HOST"], "STANDBY_REACTION", [], "STACK"));
        await openGame.Top();
        Assert.Equal("OTHER", openGame.State.ObjectLocations["HOST"].BattlefieldObjectId);

        var closedCards = open.CardObjects.ToDictionary();
        foreach (var field in new[] { "F", "OTHER" })
            closedCards[field] = closedCards[field] with { ControllerId = "P1" };
        var closed = open with { CardObjects = closedCards };
        closed = AddUnitAt(closed, "BLOCKER", "OGN·219/298", "P1", "OTHER");
        var closedGame = new Driver(closed);
        await closedGame.Act("P1", new HideCardCommand("SHADOW", "RAD·102/167", "BATTLEFIELD:F", ["STANDBY_A"]));
        await closedGame.Act("P1", new EndTurnCommand()); await closedGame.Act("P2", new EndTurnCommand());
        await closedGame.Act("P1", new RevealCardCommand("SHADOW", "RAD·102/167", ["HOST"], "STANDBY_REACTION", [], "STACK"));
        await closedGame.Top();
        Assert.Contains("ROAM", closedGame.State.CardObjects["HOST"].UntilEndOfTurnEffects);
        await Reject(open, "P1", new RevealCardCommand("SHADOW", "RAD·102/167", ["GEAR"], "STANDBY_REACTION", [], "STACK"));
    }

    [Fact]
    public async Task RadiantUnityCreatesTwoOrFourMinionsDependingOnShowcase()
    {
        var ordinary = new Driver(SpellPosition("RAD·127/167"));
        await ordinary.Act("P1", new PlayCardCommand("SPELL", "RAD·127/167", [])); await ordinary.Top();
        Assert.Equal(2, ordinary.State.PlayerZones["P1"].Base.Count(id => ordinary.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)));

        var shown = SpellPosition("RAD·127/167");
        var high = OfficialGarenConquestBattlefieldCountTests.Printed("HIGH", "OGN·001/298", "P1") with { ManaCost = 7 };
        shown = shown with { CardObjects = new Dictionary<string, CardObjectState>(shown.CardObjects) { ["HIGH"] = high },
            PlayerZones = new Dictionary<string, PlayerZones>(shown.PlayerZones) { ["P1"] = shown.PlayerZones["P1"] with { Hand = [.. shown.PlayerZones["P1"].Hand, "HIGH"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(shown.ObjectLocations) { ["HIGH"] = new("P1", "HAND") } };
        var shownGame = new Driver(shown);
        await shownGame.Act("P1", new PlayCardCommand("SPELL", "RAD·127/167", [], OptionalCosts: ["SHOWCASE:HIGH"])); await shownGame.Top();
        Assert.Equal(4, shownGame.State.PlayerZones["P1"].Base.Count(id => shownGame.State.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily))
            - shown.PlayerZones["P1"].Base.Count(id => shown.CardObjects[id].Tags.Contains(CardObjectTags.MinionTokenFamily)));
        await Reject(SpellPosition("RAD·127/167"), "P1", new PlayCardCommand("SPELL", "RAD·127/167", [], OptionalCosts: ["SHOWCASE:ALLY"]));
    }

    [Fact]
    public async Task BalefireDistributesFiveDamageOnlyAmongUnitsAtTheChosenBattlefield()
    {
        var initial = AddUnitAt(SpellPosition("RAD·006/167"), "ENEMY-F", "OGN·208/298", "P2", "OTHER");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·006/167", ["OTHER"]));
        await game.Top();
        var targetChoice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", targetChoice.ChoiceWindow);
        Assert.Equal(["ENEMY-F"], targetChoice.LegalObjectIds);
        await game.Act("P1", new ChooseCardsCommand(targetChoice.ChoiceId, targetChoice.ChoiceWindow, ["ENEMY-F"]));
        await game.Top();
        var allocation = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await Reject(game.State, "P1", new AssignEffectDamageCommand(allocation.ResolvingStackItemId!, [
            new("SPELL", "ENEMY-F", 4)]));
        await game.Act("P1", new AssignEffectDamageCommand(allocation.ResolvingStackItemId!, [
            new("SPELL", "ENEMY-F", 5)]));
        Assert.Equal(5, game.State.CardObjects["ENEMY-F"].Damage);
        await Reject(SpellPosition("RAD·006/167"), "P1", new PlayCardCommand("SPELL", "RAD·006/167", ["ALLY"]));
    }

    [Fact]
    public async Task ForbiddenTechniqueReadiesAUnitAndCanReplayItFromTheGraveyardForOneAndTwoRed()
    {
        var initial = SpellPosition("RAD·010/167");
        var zones = initial.PlayerZones.ToDictionary();
        zones["P1"] = zones["P1"] with { Hand = zones["P1"].Hand.Where(id => id != "SPELL").ToArray(), Graveyard = ["GRAVE", "SPELL"] };
        var locations = initial.ObjectLocations.ToDictionary(); locations["SPELL"] = new("P1", "GRAVEYARD");
        var cards = initial.CardObjects.ToDictionary();
        cards["HIGH"] = OfficialGarenConquestBattlefieldCountTests.Printed("HIGH", "OGN·041/298", "P1");
        locations["HIGH"] = new("P1", "HAND"); zones["P1"] = zones["P1"] with { Hand = [.. zones["P1"].Hand, "HIGH"] };
        var pools = initial.RunePools.ToDictionary(); pools["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Red] = 20 });
        initial = initial with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations, RunePools = pools };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("HIGH", "OGN·041/298", [], Destination: "BASE"));
        if (game.State.PendingCardChoice is { } trigger)
            await game.Act("P1", new ChooseCardsCommand(trigger.ChoiceId, trigger.ChoiceWindow, []));
        var payment = Assert.IsType<PendingPaymentState>(game.State.PendingPayment);
        Assert.Equal(1, payment.ManaCost);
        Assert.Equal(2, payment.PowerCostByTrait[RuneTrait.Red]);
        await game.Act("P1", new PayCostCommand(payment.PaymentId, payment.PaymentWindow, ["PAY"]));
        await game.Top();
        var effect = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(["SPELL"], effect.Sources.Keys);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·010/167", ["ALLY"], Destination: "BASE"));
        await game.Top();
        Assert.False(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Banished);

        var unaffordable = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(unaffordable, "P1", new PlayCardCommand("HIGH", "OGN·041/298", [], Destination: "BASE"));
    }

    [Fact]
    public async Task PreparationRitualShowcasesEquipmentReadiesAtMostTwoRunesAndRejectsForgedChoices()
    {
        var initial = SpellPosition("RAD·032/167");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        cards["GEAR"] = cards["GEAR"] with { Tags = [CardObjectTags.EquipmentCard], IsExhausted = false };
        zones["P1"] = zones["P1"] with { Base = zones["P1"].Base.Where(id => id != "GEAR").ToArray(), Hand = [.. zones["P1"].Hand, "GEAR"] };
        locations["GEAR"] = new("P1", "HAND");
        foreach (var id in new[] { "R1", "R2", "R3" })
        {
            cards[id] = new(id, cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1", isExhausted: true, tags: [CardObjectTags.RuneCard, "COLOR:red"]);
            locations[id] = new("P1", "BASE"); zones["P1"] = zones["P1"] with { Base = [.. zones["P1"].Base, id] };
        }
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·032/167", ["ALLY"], OptionalCosts: ["SHOWCASE:GEAR"]));
        await game.Top();
        var runeChoice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.RadShowcaseRuneChoiceWindow, runeChoice.ChoiceWindow);
        await Reject(game.State, "P1", new ChooseCardsCommand(runeChoice.ChoiceId, runeChoice.ChoiceWindow, ["R1", "R2", "R3"]));
        await game.Act("P1", new ChooseCardsCommand(runeChoice.ChoiceId, runeChoice.ChoiceWindow, ["R1", "R2"]));
        await game.Top();
        Assert.False(game.State.CardObjects["R1"].IsExhausted); Assert.False(game.State.CardObjects["R2"].IsExhausted); Assert.True(game.State.CardObjects["R3"].IsExhausted);
        Assert.Equal(2, game.State.CardObjects["ALLY"].UntilEndOfTurnPowerModifier);
        await Reject(SpellPosition("RAD·032/167"), "P1", new PlayCardCommand("SPELL", "RAD·032/167", ["ALLY"], OptionalCosts: ["SHOWCASE:ALLY"]));
    }

    [Fact]
    public async Task AttackOrderMovesAnUnattachedDeployEquipmentOrLooksAtFiveCards()
    {
        var moving = SpellPosition("RAD·033/167");
        var movingCards = moving.CardObjects.ToDictionary(); movingCards["GEAR"] = movingCards["GEAR"] with {
            Tags = [CardObjectTags.EquipmentCard, CardEquipmentKeywordNames.Deploy] };
        moving = moving with { CardObjects = movingCards };
        var game = new Driver(moving);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·033/167", ["GEAR", "OTHER"], Mode: "MOVE_DEPLOYED_EQUIPMENT", Destination: "BATTLEFIELD:OTHER"));
        await game.Top();
        Assert.Equal("BATTLEFIELD", game.State.ObjectLocations["GEAR"].Zone);
        Assert.Equal("OTHER", game.State.ObjectLocations["GEAR"].BattlefieldObjectId);
        var attached = moving with { CardObjects = new Dictionary<string, CardObjectState>(moving.CardObjects) {
            ["GEAR"] = moving.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };
        await Reject(attached, "P1", new PlayCardCommand("SPELL", "RAD·033/167", ["GEAR", "OTHER"], Mode: "MOVE_DEPLOYED_EQUIPMENT", Destination: "BATTLEFIELD:OTHER"));

        var look = SpellPosition("RAD·033/167");
        var lookCards = look.CardObjects.ToDictionary(); var deck = look.PlayerZones["P1"].MainDeck.ToArray();
        lookCards[deck[0]] = OfficialGarenConquestBattlefieldCountTests.Printed(deck[0], "SFD·022/221", "P1");
        lookCards[deck[0]] = lookCards[deck[0]] with { Tags = [CardObjectTags.EquipmentCard] };
        look = look with { CardObjects = lookCards };
        var lookGame = new Driver(look);
        await lookGame.Act("P1", new PlayCardCommand("SPELL", "RAD·033/167", [], Mode: "LOOK_EQUIPMENT"));
        await lookGame.Top();
        var deckChoice = Assert.IsType<PendingCardChoiceState>(lookGame.State.PendingCardChoice);
        Assert.Contains(deck[0], deckChoice.LegalObjectIds);
        await lookGame.Act("P1", new ChooseCardsCommand(deckChoice.ChoiceId, deckChoice.ChoiceWindow, [deck[0]]));
        Assert.Contains(deck[0], lookGame.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task PrimalRoarUsesTheShownUnitPowerAndRejectsAnEquipmentShowcase()
    {
        var game = new Driver(SpellPosition("RAD·082/167"));
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·082/167", ["ENEMY"], OptionalCosts: ["SHOWCASE:UNIT"]));
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        await Reject(SpellPosition("RAD·082/167"), "P1", new PlayCardCommand("SPELL", "RAD·082/167", ["ENEMY"], OptionalCosts: ["SHOWCASE:GEAR"]));
    }

    [Fact]
    public async Task UndyingRemembranceReplaysUpToThreeCheapUnitsOrEquipmentWithoutTheirCosts()
    {
        var initial = SpellPosition("RAD·135/167");
        var cards = initial.CardObjects.ToDictionary(); var locations = initial.ObjectLocations.ToDictionary(); var zones = initial.PlayerZones.ToDictionary();
        foreach (var (id, cardNo) in new[] { ("U1", "VEN·004"), ("E1", "SFD·033/221"), ("U2", "OGN·041/298") })
        {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, cardNo, "P1");
            locations[id] = new("P1", "GRAVEYARD");
        }
        zones["P1"] = zones["P1"] with { Graveyard = ["GRAVE", "U1", "E1", "U2"] };
        initial = initial with { CardObjects = cards, ObjectLocations = locations, PlayerZones = zones, RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(8, 2) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("SPELL", "RAD·135/167", []));
        await game.Top();
        var pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        Assert.Equal(new[] { "E1", "U1" }, pending.Sources.Keys.Order().ToArray());
        await Reject(game.State, "P1", new PlayCardCommand("U2", "OGN·041/298", [], Destination: "BASE"));
        await game.Act("P1", new PlayCardCommand("U1", "VEN·004", [], Destination: "BASE"));
        pending = Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay);
        await game.Act("P1", new PlayCardCommand("E1", "SFD·033/221", [], Destination: "BASE"));
        if (game.State.PendingEffectPlay is { } optional)
            await game.Act("P1", new ChooseCardsCommand(optional.ChoiceId, "EFFECT_PLAY", []));
        Assert.Contains("U1", game.State.PlayerZones["P1"].Base);
        Assert.Contains("E1", game.State.PlayerZones["P1"].Base);
        Assert.Contains("U2", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
    }
}
