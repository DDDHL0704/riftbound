using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSeraphineStarfireTests
{
    private const string Card = "RAD·138/167";
    private static MatchState InBase(string no = Card, bool exhausted = false)
    {
        var state = LocalDestructionRecallTests.At(VenUnitEntryBasicTests.Position(no), "UNIT", "P1", "BASE");
        return state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = [] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { IsExhausted = exhausted } } };
    }
    private static CardObjectState[] Minions(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "OGN·271/298").ToArray();
    private static int Might(MatchState state)
        => CurrentPowerTargetTests.Snapshot(state).GetProperty("players").GetProperty("P1")
            .GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static async Task Choose(Driver game, params string[] selected)
    {
        var c = game.State.PendingCardChoice!;
        await game.Act(c.PlayerId, new ChooseCardsCommand(c.ChoiceId, c.ChoiceWindow, selected));
    }

    [Theory]
    [InlineData(Card, 39645L)]
    [InlineData("RAD·138a/167", 39646L)]
    [InlineData("RAD·SP5/005", 39724L)]
    [InlineData("T1S·005/005", 38007L)]
    public async Task EveryPrintingKeepsItsOfficialIdentityAndCreatesOnlyOneMinionOnEntry(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal("萨勒芬妮", card.CardName); Assert.Equal("星火相随", card.SubTitle);
        Assert.Equal(5, card.Energy); Assert.Equal(1, card.ReturnEnergy); Assert.Equal(1, card.Power);
        var aura = Assert.Single(StaticAuraParser.Parse(card.CardEffect));
        Assert.Equal(StaticAuraParticipantScopes.OtherFriendlyFilteredPublicUnits, aura.ParticipantScope);
        Assert.Equal(StaticAuraTargetFilters.Exhausted, aura.TargetFilter);
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: "BASE"));
        Assert.Equal(15, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(Minions(game.State));
        Assert.Equal("PLAY_OR_EXHAUST", Assert.Single(game.State.StackItems).UnitEvent!.Timing);
        Assert.Equal(1, Might(game.State)); await game.Top();
        var minion = Assert.Single(Minions(game.State));
        Assert.Equal(1, minion.Power); Assert.True(minion.IsExhausted);
        Assert.Contains(minion.ObjectId, game.State.PlayerZones["P1"].Base);
        Assert.Equal(2, Might(game.State)); Assert.Equal(1, game.State.CardObjects["UNIT"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(Card)] [InlineData("RAD·138a/167")]
    [InlineData("RAD·SP5/005")] [InlineData("T1S·005/005")]
    public async Task ARealMovementExhaustionCapturesTheSameAbilityAndWaitsForResponses(string no)
    {
        var game = new Driver(InBase(no));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(Minions(game.State));
        Assert.Single(game.State.StackItems); Assert.Equal(1, Might(game.State));
        await game.Top(); Assert.Single(Minions(game.State)); Assert.Equal(2, Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheSourceNeverCountsItselfEvenWhenItIsExhausted(bool exhausted)
    {
        var state = InBase(exhausted: exhausted);
        Assert.Equal(1, Might(state)); ArmamentMightTests.PublicRecovery(state);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD")]
    public void CountIncludesOtherControlledPublicExhaustedUnitsAcrossBothZones(string zone)
    {
        var state = InBase(exhausted: true);
        if (zone == "BATTLEFIELD") state = LocalDestructionRecallTests.At(state, "ALLY", "P1", zone, "OTHER");
        state = LastBreathDrawBoundaryTests.Add(state, "HAND-UNIT", "OGN·219/298", "P1", "HAND");
        state = LastBreathDrawBoundaryTests.Add(state, "HIDDEN", "OGN·219/298", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true },
            ["ENEMY"] = state.CardObjects["ENEMY"] with { IsExhausted = true },
            ["GEAR"] = state.CardObjects["GEAR"] with { IsExhausted = true },
            ["HAND-UNIT"] = state.CardObjects["HAND-UNIT"] with { IsExhausted = true },
            ["HIDDEN"] = state.CardObjects["HIDDEN"] with { IsExhausted = true, IsFaceDown = true, Tags = [CardObjectTags.Standby] }
        } };
        Assert.Equal(2, Might(state));
        state = LocalDestructionRecallTests.At(state, "ALLY", "P2", "BASE");
        Assert.Equal(1, Might(state));
    }

    [Fact]
    public async Task ReadyThenExhaustAgainTriggersOnceForEachActualTransition()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(InBase(), "READY", "UNL-009/219"));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Top(); Assert.Single(Minions(game.State));
        await game.Act("P1", new PlayCardCommand("READY", "UNL-009/219", ["UNIT"])); await game.Top();
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted); Assert.Single(Minions(game.State));
        Assert.Empty(game.State.StackItems);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BATTLEFIELD", "BASE", []));
        Assert.Single(game.State.StackItems); await game.Top();
        Assert.Equal(2, Minions(game.State).Length); Assert.Equal(3, Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task ReturningTheSourceInResponseKeepsTheEarnedMinionForItsCapturedController()
    {
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(InBase(), "RETURN", "OGN·104/298"));
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"]));
        await game.Top(); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        await game.Top(); Assert.Single(Minions(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SharedTokenReplacementCanBeDeclinedOrApplyBeforeEntry(bool accept)
    {
        var state = LastBreathDrawBoundaryTests.Add(InBase(), "ZILEAN", "UNL-086/219", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "ZILEAN", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", [])); await game.Top();
        Assert.Empty(Minions(game.State)); Assert.Equal("TOKEN_ENTRY_REPLACEMENT", game.State.PendingCardChoice!.ChoiceWindow);
        await Choose(game, accept ? ["ZILEAN"] : []);
        Assert.Equal(accept ? 2 : 1, Minions(game.State).Length);
        Assert.Equal(accept ? 3 : 2, Might(game.State)); await game.Replay();
    }

    [Fact]
    public async Task TheAuraUpdatesWhenAnotherExhaustedUnitActuallyLeavesTheField()
    {
        var state = InBase(); state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        var game = new Driver(OfficialCopyIdentityTests.AddSpell(state, "RETURN", "OGN·104/298"));
        Assert.Equal(2, Might(game.State));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Equal(1, Might(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData("generation")] [InlineData("face")] [InlineData("timing")] [InlineData("card")]
    public async Task RecoveryRejectsForgedCapturedAbilityIdentity(string kind)
    {
        var game = new Driver(InBase());
        await game.Act("P1", new MoveUnitCommand("UNIT", "BASE", "BATTLEFIELD:BF", []));
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var forged = kind switch {
            "generation" => context with { SourceGeneration = 99 },
            "face" => context with { FaceRevision = 99 },
            "timing" => context with { Timing = "PLAY" },
            _ => context with { CardNo = "RAD·138a/167" }
        };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { UnitEvent = forged }] }));
    }
}
