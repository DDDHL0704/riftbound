using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenMovementUnitTests
{
    public static TheoryData<string, long, int, int, int> Cards => new() {
        { "VEN·002", 37687, 4, 0, 4 }, { "VEN·016", 37701, 8, 0, 8 },
        { "VEN·095", 37792, 2, 0, 2 }, { "VEN·111", 37809, 6, 1, 6 }
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task OfficialPrintingPaysItsPrintedCostAndEntersAsAnExhaustedUnit(string no, long id, int mana, int power, int might)
    {
        var printed = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, printed.Id); Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base); Assert.Equal(might, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(game.State.StackItems);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana); await game.Replay();
        var unpayable = VenUnitEntryBasicTests.Position(no) with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty } };
        var rejected = await new CoreRuleEngine().ResolveAsync(unpayable, new("cannot-pay", "P1", CommandTypes.PlayCard), new PlayCardCommand("UNIT", no, []), default);
        Assert.False(rejected.Accepted); Assert.Equal(MatchStateHasher.Hash(unpayable), MatchStateHasher.Hash(rejected.State)); Assert.Empty(rejected.Events);
    }

    [Theory]
    [InlineData("P1")] [InlineData("P2")]
    public async Task SwiftbladeChoosesAPlayerBeforeResponsesAndBurnsOnlyAfterTheSkillResolves(string player)
    {
        var game = new Driver(AddMoveSpell(Position("VEN·002")));
        await Move(game); Assert.Equal("MOVEMENT_TRIGGER_CONFIRMATION", game.State.PendingRuleChoice!.Request.Kind);
        await Choose(game, player);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal(player, skill.MovementObserver!.SelectedPlayerId); Assert.Equal(1, skill.MovementObserver.MovementOrdinal);
        Assert.Equal(3, game.State.PlayerZones[player].MainDeck.Count);
        await game.Top(); Assert.Contains(player + "-DRAW-1", game.State.PlayerZones[player].Graveyard);
        Assert.Equal(2, game.State.PlayerZones[player].MainDeck.Count);
        await game.Act("P1", new PlayCardCommand("MOVE", "SFD·043/221", ["UNIT"])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice);
        Assert.Equal(2, game.State.CardObjects["UNIT"].MovementCountThisTurn);
        Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_MILLED"); await game.Replay();
    }

    [Fact]
    public async Task FirstMovementOfANewTurnCanTriggerAgain()
    {
        var state = Position("VEN·002");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { MovementCountThisTurn = 1 } } };
        var game = new Driver(state);
        await Move(game); Assert.Empty(game.State.StackItems);
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD", "BASE"));
        await Choose(game, "P2"); await game.Top(); Assert.Single(game.Events(), e => e.Event.Kind == "CARDS_MILLED"); await game.Replay();
    }

    [Theory]
    [InlineData(0)] [InlineData(4)] [InlineData(5)]
    public async Task EclipseDrakeDrawsOnlyWhenItsMovementRuneConditionIsMet(int runes)
    {
        var game = new Driver(AddRunes(Position("VEN·016"), runes));
        await Move(game);
        if (runes <= 4) {
            Assert.Equal(runes, Assert.Single(game.State.StackItems).MovementObserver!.ControlledRunesAtTrigger);
            Assert.Empty(game.State.PlayerZones["P1"].Hand); await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        } else { Assert.Empty(game.State.StackItems); Assert.Empty(game.State.PlayerZones["P1"].Hand); }
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EclipseDrakeHasteCostsOneExtraManaAndOneRedPower(bool paid)
    {
        var initial = VenUnitEntryBasicTests.Position("VEN·016") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·016", [], OptionalCosts: paid ? [HasteOptionalCostNames.HasteReady] : []));
        Assert.Equal(!paid, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(paid ? 11 : 12, game.State.RunePools["P1"].Mana); Assert.Equal(paid ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        await game.Replay();
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ShadowDiscipleBurnIsOptionalAndItsBuffExpires(bool burn)
    {
        var game = new Driver(Position("VEN·095")); await Move(game); await game.Top();
        Assert.Equal("MOVEMENT_OPTIONAL_BURN", game.State.PendingRuleChoice!.Request.Kind);
        await Choose(game, burn ? "BURN" : "DECLINE");
        Assert.Equal(burn ? 3 : 2, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(burn ? 2 : 3, game.State.PlayerZones["P1"].MainDeck.Count);
        Assert.Equal(burn, game.State.PlayerZones["P1"].Graveyard.Contains("P1-DRAW-1"));
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(2, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Fact]
    public async Task ShadowDiscipleCannotPayOptionalBurnFromAnEmptyDeck()
    {
        var initial = Position("VEN·095"); var old = initial.PlayerZones["P1"].MainDeck;
        initial = initial with { PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with {
                MainDeck = [], Banished = old } : p.Value),
            ObjectLocations = initial.ObjectLocations.ToDictionary(p => p.Key, p => old.Contains(p.Key) ? new ObjectLocationState("P1", "BANISHED") : p.Value) };
        var game = new Driver(initial); await Move(game); await game.Top();
        Assert.Null(game.State.PendingRuleChoice); Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "CARDS_MILLED"); await game.Replay();
    }

    [Fact]
    public async Task TheOptionalBurnCanHappenAfterTheSourceLeavesButItsBuffCannotAffectTheNewObject()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·095"), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial); await Move(game);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["UNIT"].Zone);
        await game.Top(); await Choose(game, "BURN");
        Assert.Equal(2, game.State.CardObjects["UNIT"].Power); Assert.Equal(1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData("DRAW", "P1")] [InlineData("DISCARD", "P1")]
    [InlineData("DRAW", "P2")] [InlineData("DISCARD", "P2")]
    public async Task MinaConfirmsOneModeBeforeResponsesAndEachPlayerMakesTheirOwnPrivateHandChoice(string mode, string controller)
    {
        var initial = AddHandCards(Position("VEN·111"));
        if (controller == "P2") initial = initial with {
            TurnPlayerId = "P2", ActivePlayerId = "P2",
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["UNIT"] = initial.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } },
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Value with { Base = p.Key == "P1" ? p.Value.Base.Where(id => id != "UNIT").ToArray() : [..p.Value.Base, "UNIT"] }),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P2", "BASE") }
        };
        var game = new Driver(initial);
        await game.Act(controller, new MoveUnitCommand("UNIT", "BASE", controller == "P1" ? "BATTLEFIELD:BF" : "BATTLEFIELD:OTHER"));
        await Choose(game, mode);
        Assert.Equal(mode, Assert.Single(game.State.StackItems).MovementObserver!.Mode);
        Assert.Single(game.State.PlayerZones["P1"].Hand); Assert.Single(game.State.PlayerZones["P2"].Hand);
        await game.Top();
        if (mode == "DISCARD") {
            var other = controller == "P1" ? "P2" : "P1";
            Assert.Equal(controller, game.State.PendingRuleChoice!.Request.PlayerId);
            Assert.DoesNotContain(controller + "-HAND", JsonSerializer.Serialize(game.PromptFor(other)));
            await Choose(game, controller + "-HAND");
            Assert.Equal(other, game.State.PendingRuleChoice!.Request.PlayerId);
            Assert.DoesNotContain(other + "-HAND", JsonSerializer.Serialize(game.PromptFor(controller)));
            // Both decisions precede the simultaneous discard publication.
            Assert.Contains(controller + "-HAND", game.State.PlayerZones[controller].Hand);
            await Choose(game, other + "-HAND");
        }
        foreach (var p in new[] { "P1", "P2" }) {
            if (mode == "DRAW") { Assert.Contains(p + "-DRAW-1", game.State.PlayerZones[p].Hand); Assert.Equal(2, game.State.PlayerZones[p].Hand.Count); }
            else { Assert.Empty(game.State.PlayerZones[p].Hand); Assert.Contains(p + "-HAND", game.State.PlayerZones[p].Graveyard); }
        }
        await game.Replay();
    }

    [Fact]
    public async Task MinaDoesNotTriggerWhenMovingBackToBase()
    {
        var initial = LocalDestructionRecallTests.At(Position("VEN·111"), "UNIT", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD", "BASE"));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingRuleChoice); await game.Replay();
    }

    [Fact]
    public async Task InvalidConfirmationPlayerAndChoiceAreRejectedWithoutMutation()
    {
        var game = new Driver(Position("VEN·002")); await Move(game);
        var pending = game.State.PendingRuleChoice!; var state = game.State;
        foreach (var (player, id) in new[] { ("P2", "P1"), ("P1", "FORGED") }) {
            var bad = await new CoreRuleEngine().ResolveAsync(state, new("bad-choice", player, CommandTypes.PayCost),
                new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [id]), default);
            Assert.False(bad.Accepted); Assert.Empty(bad.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(bad.State));
        }
        await Choose(game, "P1"); await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData("VEN·002")]
    [InlineData("VEN·016")]
    [InlineData("VEN·095")]
    [InlineData("VEN·111")]
    public async Task RecoveryRejectsForeignOrMissingMovementInstructionFields(string card)
    {
        var game = new Driver(Position(card)); await Move(game);
        if (card == "VEN·002") await Choose(game, "P2");
        if (card == "VEN·111") await Choose(game, "DRAW");
        var item = Assert.Single(game.State.StackItems); var context = item.MovementObserver!;
        var invalid = card switch {
            "VEN·002" => new[] { context with { SelectedPlayerId = "FORGED" }, context with { SelectedPlayerId = null }, context with { MovementOrdinal = 2 } },
            "VEN·016" => new[] { context with { ControlledRunesAtTrigger = null }, context with { ControlledRunesAtTrigger = 5 } },
            "VEN·111" => new[] { context with { Mode = "FORGED" }, context with { Mode = null } },
            _ => new[] { context with { SelectedPlayerId = "P1" }, context with { ControlledRunesAtTrigger = 1 }, context with { Mode = "DRAW" } }
        };
        foreach (var forged in invalid) {
            var state = game.State with { StackItems = [item with { MovementObserver = forged }] };
            Assert.Contains(OfficialInsightAndSpellLockTests.Errors(state), e => e.Contains("movement observer"));
        }
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with { PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Hand = [], Base = [..p.Value.Base, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
    }
    private static MatchState AddRunes(MatchState state, int count)
    {
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        for (var i = 0; i < count; i++) {
            var id = "RUNE-" + i; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R01", "P1");
            zones["P1"] = zones["P1"] with { Base = [..zones["P1"].Base, id] }; locations[id] = new("P1", "BASE");
        }
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }
    private static MatchState AddMoveSpell(MatchState state) => AttackDamageDistributionTests.Spell(state, "MOVE", "SFD·043/221", "P1");
    private static MatchState AddHandCards(MatchState state)
    {
        var cards = state.CardObjects.ToDictionary(); var zones = state.PlayerZones.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        foreach (var p in new[] { "P1", "P2" }) {
            var id = p + "-HAND"; cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, p == "P1" ? "OGN·001/298" : "OGN·002/298", p);
            zones[p] = zones[p] with { Hand = [id] }; locations[id] = new(p, "HAND");
        }
        return state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations };
    }
    private static Task Move(Driver game) => game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF"));
    private static Task Choose(Driver game, string selection)
    {
        var p = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        var option = p.Request.Options.FirstOrDefault(o => o.ObjectIds?.Contains(selection) == true)?.Id ?? selection;
        return game.Act(p.Request.PlayerId, new PayCostCommand(p.Request.Id, "RULE_REPLACEMENT", [option]));
    }
}
