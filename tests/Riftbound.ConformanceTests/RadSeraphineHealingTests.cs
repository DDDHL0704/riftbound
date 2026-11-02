using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSeraphineHealingTests
{
    private const string Card = "RAD·065/167";
    private const string Exhaust = "OGN·123/298";
    private static MatchState Position(string no = Card, int active = 3)
    {
        var state = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", "BASE");
        state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [] } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(100, 100) } };
        foreach (var id in new[] { "THIRD", "FOURTH", "FIFTH" })
            state = LastBreathDrawBoundaryTests.Add(state, id, "OGN·219/298", "P1", "BASE");
        var ids = new[] { "UNIT", "ALLY", "THIRD", "FOURTH", "FIFTH" };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) };
        for (var i = 0; i < ids.Length; i++)
            ((Dictionary<string, CardObjectState>)state.CardObjects)[ids[i]] = state.CardObjects[ids[i]] with { IsExhausted = i >= active };
        return AddCard(state, "EXHAUST", Exhaust);
    }
    private static MatchState AddCard(MatchState state, string id, string no)
        => LastBreathDrawBoundaryTests.Add(state, id, no, "P1", "HAND");
    private static async Task Cast(Driver game, string id = "EXHAUST", string no = Exhaust)
    {
        await game.Act("P1", new PlayCardCommand(id, no, [])); await game.Top();
    }
    private static async Task Confirm(Driver game, bool accept)
    {
        var c = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_OPTIONAL_CONFIRMATION", c.ChoiceWindow);
        await game.Act(c.PlayerId, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, accept ? [c.SourceObjectId!] : []));
    }

    [Theory]
    [InlineData(Card, 39562L)] [InlineData("RAD·065a/167", 39563L)]
    public async Task EachPrintingKeepsItsCostsDisarmAndDoesNotTriggerOnDormantEntry(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("声愈人心", card.SubTitle);
        Assert.Equal(4, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(3, card.Power);
        Assert.Contains("每回合限一次，当你同时让不少于三名友方单位变为休眠状态时，你可以选择抽一张牌。", card.CardEffect);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(game.State.StackItems);
        Assert.Contains("缴械2", game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(16, game.State.RunePools["P1"].Mana); await game.Replay();
    }

    [Theory]
    [InlineData(Card, 0)] [InlineData(Card, 1)] [InlineData(Card, 2)]
    [InlineData(Card, 3)] [InlineData(Card, 5)] [InlineData("RAD·065a/167", 3)]
    public async Task OnlyActualSimultaneousFriendlyTransitionsReachTheThreshold(string no, int active)
    {
        var game = new Driver(Position(no, active)); var hand = game.State.PlayerZones["P1"].Hand.Count - 1;
        await Cast(game);
        Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
        if (active >= 3) {
            Assert.Equal(active, Assert.Single(game.State.StackItems).UnitEvent!.ExhaustedUnits!.Count);
            await Confirm(game, true); Assert.Equal(hand, game.State.PlayerZones["P1"].Hand.Count);
            await game.Top(); Assert.Equal(hand + 1, game.State.PlayerZones["P1"].Hand.Count);
        }
        else { Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); }
        await game.Replay();
    }

    [Fact]
    public async Task OneGroupMovePaysAllThreeExhaustionCostsTogether()
    {
        var game = new Driver(Position());
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF",
            SourceObjectIds: ["UNIT", "ALLY", "THIRD"]));
        Assert.Equal(3, Assert.Single(game.State.StackItems).UnitEvent!.ExhaustedUnits!.Count);
        await Confirm(game, true); await game.Top();
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Fact]
    public async Task AGroupOfTwoDoesNotReachTheThreshold()
    {
        var game = new Driver(Position());
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF",
            SourceObjectIds: ["UNIT", "ALLY"]));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task SeparateMoveInstructionsDoNotCombineTheirExhaustions()
    {
        var game = new Driver(Position());
        foreach (var id in new[] { "UNIT", "ALLY", "THIRD" }) {
            await game.Act("P1", new MoveUnitCommand(id, "BASE", "BATTLEFIELD:BF", []));
            Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        }
        await game.Replay();
    }

    [Fact]
    public async Task DormantUnitsEquipmentAndEnemyUnitsCannotCompleteTheThreshold()
    {
        var state = Position(active: 2);
        state = LocalDestructionRecallTests.At(state, "THIRD", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["THIRD"] = state.CardObjects["THIRD"] with { IsExhausted = false },
            ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = false } } };
        var game = new Driver(state); await Cast(game);
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task DecliningKeepsTheUseAndAcceptingPreventsAnotherUseThisTurn()
    {
        var state = Position();
        state = AddCard(state, "SECOND", Exhaust);
        state = AddCard(state, "THIRD-SPELL", Exhaust);
        state = AddCard(state, "READY", "SFD·192/221");
        state = AddCard(state, "READY-AGAIN", "SFD·192/221");
        var game = new Driver(state); await Cast(game); await Confirm(game, false);
        Assert.Empty(game.State.StackItems);
        await Cast(game, "READY", "SFD·192/221");
        await Cast(game, "SECOND"); await Confirm(game, true); await game.Top();
        Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand);
        await Cast(game, "READY-AGAIN", "SFD·192/221"); await Cast(game, "THIRD-SPELL");
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice); await game.Replay();
    }

    [Fact]
    public async Task ANewTurnRestoresTheOptionalUse()
    {
        var state = AddCard(Position(), "SECOND", Exhaust);
        var runes = Enumerable.Range(0, 9).Select(i => "PAY-" + i).ToArray();
        foreach (var id in runes)
            state = LastBreathDrawBoundaryTests.Add(state, id, "UNL-R03", "P1", "BASE");
        var game = new Driver(state);
        await Cast(game); await Confirm(game, true); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        await game.Act("P1", new TapRuneCommand(runes[0], runes));
        await game.Act("P1", new RecycleRuneCommand(runes[0], runes.Take(2).ToArray()));
        await Cast(game, "SECOND"); await Confirm(game, true); await game.Top();
        Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceAfterConfirmationDoesNotUndoTheEarnedDraw()
    {
        var game = new Driver(AddCard(Position(), "RETURN", "OGN·104/298"));
        await Cast(game); await Confirm(game, true);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("short")] [InlineData("duplicate")] [InlineData("generation")]
    public async Task RecoveryRejectsForgedBatchFacts(string kind)
    {
        var game = new Driver(Position()); await Cast(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!; var units = context.ExhaustedUnits!;
        var forged = kind == "short" ? units.Take(2).ToArray() : kind == "duplicate" ? new[] { units[0], units[0], units[1] }
            : new[] { units[0] with { Binding = units[0].Binding with { Generation = 99 } }, units[1], units[2] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            StackItems = [item with { UnitEvent = context with { ExhaustedUnits = forged } }] }));
    }
}
