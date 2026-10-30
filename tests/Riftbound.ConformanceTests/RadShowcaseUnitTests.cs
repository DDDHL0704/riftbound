using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadShowcaseUnitTests
{
    private const string Construct = "RAD·011/167", Serpent = "RAD·060/167", Guardian = "RAD·103/167";
    private static MatchState Position(string no, string shown, bool hand)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SHOWN"] = OfficialGarenConquestBattlefieldCountTests.Printed("SHOWN", shown, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["SHOWN"] = new("P1", hand ? "HAND" : "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Hand = hand ? ["UNIT", "SHOWN"] : ["UNIT"], Base = hand ? ["ALLY", "GEAR"] : ["ALLY", "GEAR", "SHOWN"] } } };
    }
    private static PlayCardCommand Play(string no, params string[] choices) => new("UNIT", no, [], OptionalCosts: choices);
    private static async Task Insight(Driver game, bool recycle = false)
    {
        var choice = game.State.PendingCardChoice!;
        Assert.Equal("INSIGHT", choice.ChoiceWindow);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycle ? choice.LegalObjectIds : []));
        if (game.State.PendingCardChoice is { ChoiceWindow: "INSIGHT_ORDER" } order)
            await game.Act("P1", new ChooseCardsCommand(order.ChoiceId, order.ChoiceWindow, order.LegalObjectIds.Reverse().ToArray()));
    }
    private static async Task Reject(MatchState state, GameCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-showcase", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(Construct, "RAD·055/167", true)]
    [InlineData(Construct, "RAD·055/167", false)]
    [InlineData(Serpent, "SFD·022/221", true)]
    [InlineData(Serpent, "SFD·022/221", false)]
    [InlineData(Guardian, Guardian, true)]
    [InlineData(Guardian, Guardian, false)]
    public async Task HandAndFriendlyFieldShowcaseShareConfirmationAndIndependentResolution(string no, string shown, bool hand)
    {
        var game = new Driver(Position(no, shown, hand));
        await game.Act("P1", Play(no, "SHOWCASE:SHOWN"));
        Assert.Equal(no != Construct, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(hand ? 1 : 0, game.Events().Count(e => e.Event.Kind == "CARDS_REVEALED"));
        Assert.DoesNotContain("SHOWCASE:SHOWN", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"]));
        Assert.Equal("SHOWN", game.State.CardPlays.Finalized.Single().Showcase!.Object.ObjectId);
        if (no != Construct) {
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.OptionalCosts);
            var power = game.State.RunePools["P1"].Power;
            await game.Top();
            if (no == Serpent) { Assert.Single(game.State.PendingCardChoice!.LegalObjectIds); await Insight(game); }
            else Assert.Equal(power + 1, game.State.RunePools["P1"].Power);
        }
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Equal((hand ? 1 : 0) + (no == Serpent ? 2 : 0), game.State.PlayerZones["P1"].Hand.Count);
        await game.Replay();
    }

    [Theory]
    [InlineData(Construct)] [InlineData(Serpent)] [InlineData(Guardian)]
    public async Task DecliningShowcaseCreatesNoAbilityOrExtraChoice(string no)
    {
        var game = new Driver(Position(no, no, true));
        await game.Act("P1", Play(no));
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted); Assert.Empty(game.State.StackItems);
        Assert.Null(game.State.PendingCardChoice); Assert.Null(game.State.CardPlays.Finalized.Single().Showcase);
        Assert.Single(game.State.PlayerZones["P1"].Hand); Assert.Equal(20, game.State.RunePools["P1"].Power);
    }

    [Theory]
    [InlineData("OGN·219/298", 0)] [InlineData("OGN·223/298", 1)] [InlineData("RAD·018/167", 2)]
    public async Task InsightUsesLockedPrintedPowerCostIncludingZero(string shown, int count)
    {
        var game = new Driver(Position(Serpent, shown, false));
        await game.Act("P1", Play(Serpent, "SHOWCASE:SHOWN")); await game.Top();
        if (count > 0) { Assert.Equal(count, game.State.PendingCardChoice!.LegalObjectIds.Count); await Insight(game, recycle: true); }
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData(Construct, "OGN·219/298")] [InlineData(Guardian, "OGN·219/298")]
    public async Task ACardWithoutTheRequiredPropertyCannotBeShown(string no, string shown)
        => await Reject(Position(no, shown, true), Play(no, "SHOWCASE:SHOWN"));

    [Theory]
    [InlineData(Construct)] [InlineData(Serpent)] [InlineData(Guardian)]
    public async Task DuplicateSelfForeignHiddenAndWrongZoneSelectionsAreAtomic(string no)
    {
        var state = Position(no, no == Construct ? "RAD·055/167" : Guardian, true);
        foreach (var id in new[] { "UNIT", "ENEMY", "P1-DRAW-1", "P2-DRAW-1", "GRAVE", "missing" })
            await Reject(state, Play(no, "SHOWCASE:" + id));
        await Reject(state, Play(no, "SHOWCASE:SHOWN", "SHOWCASE:SHOWN"));
        await Reject(state, new PlayCardCommand("UNIT", no, ["SHOWN"], OptionalCosts: ["SHOWCASE:SHOWN"]));
        var facedown = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHOWN"] = state.CardObjects["SHOWN"] with { IsFaceDown = true } } };
        await Reject(facedown, Play(no, "SHOWCASE:SHOWN"));
    }

    [Theory]
    [InlineData(Serpent)] [InlineData(Guardian)]
    public async Task RecoveryRejectsInventedOrRemovedReceipts(string no)
    {
        var game = new Driver(Position(no, Guardian, true)); await game.Act("P1", Play(no, "SHOWCASE:SHOWN"));
        var item = Assert.Single(game.State.StackItems); var receipt = item.Showcase!;
        foreach (var forged in new ShowcaseReceipt?[] { null,
            receipt with { PowerCost = receipt.PowerCost + 1 }, receipt with { PlayerId = "P2" },
            receipt with { Object = receipt.Object with { Generation = receipt.Object.Generation + 1 } },
            receipt with { CardNo = Construct }, receipt with { Tick = game.State.Tick + 1 } })
            Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with { StackItems = [item with { Showcase = forged }] }));
    }

    [Fact]
    public async Task AResponseReturningTheShownCardDoesNotInvalidateItsLockedInformation()
    {
        var initial = OfficialCopyIdentityTests.AddSpell(Position(Serpent, "RAD·018/167", false), "RETURN", "OGN·104/298");
        var game = new Driver(initial); await game.Act("P1", Play(Serpent, "SHOWCASE:SHOWN"));
        var receipt = Assert.Single(game.State.StackItems).Showcase;
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["SHOWN"])); await game.Top();
        Assert.Contains("SHOWN", game.State.PlayerZones["P1"].Hand);
        Assert.Equal(receipt, Assert.Single(game.State.StackItems).Showcase);
        await game.Top(); Assert.Equal(2, game.State.PendingCardChoice!.LegalObjectIds.Count); await Insight(game);
        Assert.Equal(3, game.State.PlayerZones["P1"].Hand.Count); await game.Replay();
    }

    [Theory]
    [InlineData(Construct)] [InlineData(Serpent)] [InlineData(Guardian)]
    public async Task AnUnaffordablePlayDoesNotRevealTheChosenHandCard(string no)
    {
        var state = Position(no, no == Construct ? "RAD·055/167" : Guardian, true);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(state, Play(no, "SHOWCASE:SHOWN"));
    }

    [Theory]
    [InlineData(Guardian)] [InlineData("RAD·107/167")]
    public async Task StandbyPlayUsesTheSameShowcaseAndPostEntryTargetFlows(string no)
    {
        var state = StandbySourceRulesTests.Position(no);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SHADOW"] = OfficialGarenConquestBattlefieldCountTests.Printed("SHADOW", no, "P1") } };
        state = OfficialCopyIdentityTests.AddSpell(state, "REVEAL", Guardian);
        state = AttackDamageDistributionTests.Spell(state, "REPLY", "OGN·048/298", "P2");
        var game = new Driver(state);
        await game.Act("P1", new HideCardCommand("SHADOW", no, "BATTLEFIELD:F", ["STANDBY_A"]));
        await game.Act("P1", new EndTurnCommand());
        await game.TapRunes("P2", ["SHADOW-P2-RUNE-1", "SHADOW-P2-RUNE-2"]);
        await game.Act("P2", new PlayCardCommand("REPLY", "OGN·048/298", [])); await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("SHADOW", no, [], OptionalCosts: no == Guardian ? ["SHOWCASE:REVEAL"] : []));
        Assert.Equal("F", game.State.ObjectLocations["SHADOW"].BattlefieldObjectId);
        if (no != Guardian) {
            var choice = game.State.PendingCardChoice!;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["F"]));
        }
        await game.Top();
        if (no == Guardian) Assert.Equal(1, game.State.RunePools["P1"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(Construct, 39501, 4, 4)] [InlineData(Serpent, 39556, 8, 8)] [InlineData(Guardian, 39605, 3, 3)]
    public async Task OfficialPrintIdentityCostAndPowerArePreserved(string no, long id, int mana, int power)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Equal(power, card.Power);
        Assert.Contains("{{亮出}}", card.CardEffect);
    }
}
