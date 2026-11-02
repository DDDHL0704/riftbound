using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadRivenBrokenBladeTests
{
    private static MatchState Position(string no = "RAD·043/167", bool boon = false)
    {
        var s = VenUnitEntryBasicTests.Position(no);
        var unit = s.CardObjects["UNIT"];
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["UNIT"] = unit with { IsExhausted = false, Power = unit.Power + (boon ? 1 : 0),
                    Tags = boon ? [..unit.Tags, CardObjectTags.Boon] : unit.Tags } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Hand = [], Base = [..s.PlayerZones["P1"].Base, "UNIT"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["UNIT"] = new("P1", "BASE") } };
        return s;
    }
    private static Task Move(Driver g, bool toField = true)
        => g.Act("P1", new MoveUnitCommand("UNIT", toField ? "BASE" : "BATTLEFIELD", toField ? "BATTLEFIELD:BF" : "BASE"));
    private static Task Choose(Driver g, string id)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(g.State.PendingCardChoice);
        return g.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    [Theory]
    [InlineData("RAD·043/167", 39537L)] [InlineData("RAD·043a/167", 39538L)]
    public async Task OfficialPrintingsPayFourManaAndEnteringExhaustedDoesNotTrigger(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Null(card.ReturnEnergy);
        var g = new Driver(VenUnitEntryBasicTests.Position(no));
        await g.Act("P1", new PlayCardCommand("UNIT", no, []));
        Assert.Equal(16, g.State.RunePools["P1"].Mana); Assert.Equal(4, g.State.CardObjects["UNIT"].Power);
        Assert.True(g.State.CardObjects["UNIT"].IsExhausted); Assert.Null(g.State.PendingCardChoice);
        Assert.Empty(g.State.StackItems); await g.Replay();
    }

    [Theory]
    [InlineData("RAD·043/167")] [InlineData("RAD·043a/167")]
    public async Task EachModeIsChosenOncePerTextInstanceAndTheLedgerResetsWithTheTurn(string no)
    {
        var s = OfficialCopyIdentityTests.AddSpell(Position(no), "READY-1", "UNL-009/219");
        s = OfficialCopyIdentityTests.AddSpell(s, "READY-2", "UNL-009/219");
        var g = new Driver(s); await Move(g);
        Assert.Equal(new[] { "GAIN_BOON", "CONSUME_BOON_STUN", "CONSUME_BOON_READY" }, g.State.PendingCardChoice!.LegalObjectIds);
        await Choose(g, "GAIN_BOON"); Assert.DoesNotContain(CardObjectTags.Boon, g.State.CardObjects["UNIT"].Tags);
        await g.Top(); Assert.Contains(CardObjectTags.Boon, g.State.CardObjects["UNIT"].Tags);
        await g.Act("P1", new PlayCardCommand("READY-1", "UNL-009/219", ["UNIT"])); await g.Top();
        await Move(g, false); Assert.DoesNotContain("GAIN_BOON", g.State.PendingCardChoice!.LegalObjectIds);
        await Choose(g, "CONSUME_BOON_READY");
        Assert.True(g.State.CardObjects["UNIT"].IsExhausted); Assert.Contains(CardObjectTags.Boon, g.State.CardObjects["UNIT"].Tags);
        Assert.Null(g.State.PendingPayment); await g.Top();
        Assert.False(g.State.CardObjects["UNIT"].IsExhausted); Assert.DoesNotContain(CardObjectTags.Boon, g.State.CardObjects["UNIT"].Tags);
        await Move(g); Assert.Equal(new[] { "CONSUME_BOON_STUN" }, g.State.PendingCardChoice!.LegalObjectIds);
        await Choose(g, "CONSUME_BOON_STUN"); await Choose(g, "ENEMY"); await g.Top();
        Assert.DoesNotContain("STUNNED", g.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Equal(3, g.State.RuleTextModeUses!.Uses.Count);
        await g.Act("P1", new PlayCardCommand("READY-2", "UNL-009/219", ["UNIT"])); await g.Top();
        await Move(g, false); Assert.Null(g.State.PendingCardChoice); Assert.Empty(g.State.StackItems);
        await g.Act("P1", new EndTurnCommand()); Assert.Null(g.State.RuleTextModeUses); await g.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StunConsumesTheBoonAtResolutionAndDoesNothingWithoutOne(bool boon)
    {
        var g = new Driver(Position(boon: boon)); await Move(g); await Choose(g, "CONSUME_BOON_STUN");
        Assert.DoesNotContain("UNIT", g.State.PendingCardChoice!.LegalObjectIds);
        Assert.DoesNotContain("ALLY", g.State.PendingCardChoice.LegalObjectIds);
        await Choose(g, "ENEMY"); Assert.Null(g.State.PendingPayment);
        Assert.Equal(boon, g.State.CardObjects["UNIT"].Tags.Contains(CardObjectTags.Boon));
        Assert.DoesNotContain("STUNNED", g.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        await g.Top(); Assert.Equal(boon, g.State.CardObjects["ENEMY"].UntilEndOfTurnEffects.Contains("STUNNED"));
        Assert.DoesNotContain(CardObjectTags.Boon, g.State.CardObjects["UNIT"].Tags);
        Assert.Equal(4, g.State.CardObjects["UNIT"].Power); await g.Replay();
    }

    [Theory]
    [InlineData("GAIN_BOON")] [InlineData("CONSUME_BOON_READY")] [InlineData("CONSUME_BOON_STUN")]
    public async Task ReturningTheSourceDuringResponseKeepsTheModeUseButCannotConsumeOrBoonItsNewObject(string mode)
    {
        var g = new Driver(OfficialCopyIdentityTests.AddSpell(Position(boon: true), "RETURN", "OGN·104/298"));
        await Move(g); await Choose(g, mode); if (mode == "CONSUME_BOON_STUN") await Choose(g, "ENEMY");
        await g.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await g.Top(); await g.Top();
        Assert.Contains("UNIT", g.State.PlayerZones["P1"].Hand); Assert.Single(g.State.RuleTextModeUses!.Uses);
        Assert.DoesNotContain("STUNNED", g.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await g.Replay();
    }

    [Fact]
    public async Task InvalidModeAndTargetCommandsAreAtomicAndMissingModeReceiptsAreRejectedOnRecovery()
    {
        var g = new Driver(Position(boon: true)); await Move(g);
        var choice = g.State.PendingCardChoice!;
        await Reject(g.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["missing"]));
        await Choose(g, "CONSUME_BOON_STUN"); choice = g.State.PendingCardChoice!;
        await Reject(g.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
        await Choose(g, "ENEMY");
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with { RuleTextModeUses = null }));
        var uses = g.State.RuleTextModeUses!;
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(g.State with { RuleTextModeUses = uses with { TurnNumber = uses.TurnNumber + 1 } }));
        await g.Top(); await g.Replay();
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-riven-mode", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }
}
