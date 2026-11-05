using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadZiggsCopiedDamageTests
{
    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("RAD·142/167");
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var bomb));
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "RAD·023/167", "P1"),
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2"),
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P1"),
                ["BOMB"] = bomb.CreateObject("BOMB", "P1", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["BF", "BOMB"] },
                ["P2"] = state.PlayerZones["P2"] with { Base = [], Battlefields = ["OTHER", "ENEMY"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["BOMB"] = new("P1", "BATTLEFIELD", "BF"), ["ENEMY"] = new("P2", "BATTLEFIELD", "BF") }
        };
        return OfficialCopyIdentityTests.AddSpell(state, "DETACH", "SFD·011/221");
    }

    private static async Task Attach(Driver game, string gear)
    {
        await game.Act("P1", new AssembleEquipmentCommand(gear, "ALLY", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.Equal("RAD·023/167", game.State.CardObjects[gear].AttachedRuleText!.CardNo);
        await FinishEmptyDuel(game);
    }

    private static async Task FinishEmptyDuel(Driver game)
    {
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Null(game.State.PendingCardChoice);
        Assert.Null(game.State.PendingRuleChoice);
        for (var passes = 0; game.State.SpellDuelState.IsActive; passes++)
        {
            Assert.True(passes < 2);
            await game.Act(game.State.FocusPlayerId!, new PassFocusCommand());
        }
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
    }

    private static async Task CaptureBomb(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·142/167", ["BOMB"]));
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("BOMB"));
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        Assert.NotNull(Assert.Single(game.State.StackItems).FieldContext!.BombDestruction);
    }

    private static async Task Detach(Driver game)
    {
        await game.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["ALLY", "GEAR"]));
        await game.Top();
        Assert.Null(game.State.CardObjects["GEAR"].AttachedRuleText);
        Assert.Null(game.State.CardObjects["GEAR"].AttachedToObjectId);
    }

    [Theory]
    [InlineData(1, 4)] [InlineData(2, 5)]
    public async Task RealSongAttachmentsAddOneEquipmentDamageBonusPerPrintedTextInstance(int songs, int damage)
    {
        var state = songs == 2 ? AttachedRuleTextTests.AddSong(Position(), "SONG2") : Position();
        var game = new Driver(state);
        await Attach(game, "GEAR");
        if (songs == 2) await Attach(game, "SONG2");
        await CaptureBomb(game);
        var pool = MatchStateHasher.HashValue(game.State.RunePools);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Equal(damage, game.State.CardObjects["ENEMY"].Damage);
        Assert.Single(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "DAMAGE_APPLIED");
        Assert.Equal(pool, MatchStateHasher.HashValue(game.State.RunePools));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RealDetachRemovesTheCopiedBonusEvenAfterTheBombSkillWasCaptured(bool afterCapture)
    {
        var game = new Driver(Position());
        await Attach(game, "GEAR");
        if (afterCapture) await CaptureBomb(game);
        await Detach(game);
        if (!afterCapture) await CaptureBomb(game);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public async Task RealFaceReplacementKeepsOnlyTheSongBonusWhenTheHostLosesZiggsText()
    {
        var state = Position();
        state = LocalDestructionRecallTests.At(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GLASSES"] = OfficialGarenConquestBattlefieldCountTests.Printed("GLASSES", "VEN·137", "P1"),
                ["COPY"] = OfficialGarenConquestBattlefieldCountTests.Printed("COPY", "OGN·219/298", "P1") }
        }, "GLASSES", "P1", "BASE");
        state = LocalDestructionRecallTests.At(state, "COPY", "P1", "BASE");
        var game = new Driver(state);
        await Attach(game, "GEAR");
        await game.Act("P1", new AssembleEquipmentCommand("GLASSES", "ALLY", ["ASSEMBLE_YELLOW"]));
        await game.Top();
        var choice = Assert.IsType<RuleChoiceContinuation>(game.State.PendingRuleChoice);
        await game.Act("P1", new PayCostCommand(choice.Request.Id, "RULE_REPLACEMENT", ["COPY:COPY"]));
        Assert.Equal("OGN·219/298", game.State.CardObjects["ALLY"].CardNo);
        Assert.Equal("RAD·023/167", game.State.CardObjects["GEAR"].AttachedRuleText!.CardNo);
        await FinishEmptyDuel(game);
        await CaptureBomb(game);
        await game.Top();
        Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }
}
