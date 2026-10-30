using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustedUnitCountTests
{
    private const string Guest = "RAD·122/167";
    private const string Chorus = "RAD·048/167";

    public static IEnumerable<object[]> Counts() => new[] { Guest, Chorus }
        .SelectMany(no => (no == Guest ? new[] { 0, 1, 3, 12 } : new[] { 0, 1, 3 })
            .Select(count => new object[] { no, count }));

    public static IEnumerable<object[]> InvalidPlays() => new[] { Guest, Chorus }
        .SelectMany(no => new[] { "mana", "source", "graveyard", "targets" }.Select(reason => new object[] { no, reason }));

    [Theory]
    [InlineData(Guest, 39627, 9, 6)]
    [InlineData(Chorus, 39544, 3, -1)]
    public async Task EveryPrintingUsesItsExactOfficialTextCostsAndType(string no, long id, int mana, int might)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id);
        Assert.Equal(mana, card.Energy);
        Assert.Equal(might < 0 ? null : (int?)might, card.Power);
        Assert.Equal(no == Guest ? 1 : 0, card.ReturnEnergy ?? 0);
        Assert.Equal(no == Guest
            ? "你每控制一名休眠的单位，我的费用便减少{{1}}。\n{{壁垒}}（必须先为我分配战斗伤害。）"
            : "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n给予一名单位在本回合内-{{S}}，其数值等同于你所控制的休眠单位数量。", card.CardEffect);
    }

    [Theory]
    [MemberData(nameof(Counts))]
    public async Task CountsOnlyControlledExhaustedFieldUnitsAndKeepsCostsAndEffectsSeparate(string no, int count)
    {
        var initial = Position(no, count);
        var command = Play(no);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var mana = no == Guest ? Math.Max(0, 9 - count) : 3;
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(no == Guest ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());

        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(no == Guest ? 19 : 20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        if (no == Guest)
        {
            Assert.Contains("UNIT", game.State.PlayerZones["P1"].Base);
            Assert.Equal(6, game.State.CardObjects["UNIT"].Power);
            Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
            Assert.Contains(CardObjectTags.Bulwark, game.State.CardObjects["UNIT"].Tags);
            Assert.Empty(game.State.StackItems);
        }
        else
        {
            Assert.Single(game.State.StackItems);
            await game.Top();
            Assert.Equal(4 - count, game.State.CardObjects["ENEMY"].Power);
            Assert.Equal(-count, game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifier);
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifier);
        }
        await game.Replay();
    }

    [Theory]
    [InlineData("BASE")]
    [InlineData("BATTLEFIELD:BF")]
    public async Task AQuotedUnitCostIsRecomputedAfterARealMovementExhaustsAnotherUnit(string destination)
    {
        var initial = Position(Guest, 0);
        if (destination == "BASE") initial = RadUnitEffectTestFixture.Place(initial, "ALLY", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial);
        var before = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play(Guest)));
        Assert.Equal(9, before.Cost!.Mana);
        await game.Act("P1", new MoveUnitCommand("ALLY", "BATTLEFIELD", destination, []));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        var after = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play(Guest)));
        Assert.True(after.CanPay, after.Message);
        Assert.Equal(8, after.Cost!.Mana);
        await game.Act("P1", Play(Guest));
        Assert.Equal(12, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public async Task AReadyReactionChangesTheChorusCountWhenItResolves()
    {
        const string ready = "RAD·034/167";
        var initial = HandDiscardObserverTests.Put(Position(Chorus, 1), "READY", ready, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", Play(Chorus));
        await game.Act("P1", new PlayCardCommand("READY", ready, ["RESTING-0"]));
        Assert.True(game.State.CardObjects["RESTING-0"].IsExhausted);
        Assert.Equal(2, game.State.StackItems.Count);
        await game.Top();
        Assert.False(game.State.CardObjects["RESTING-0"].IsExhausted);
        Assert.Single(game.State.StackItems);
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        await game.Top();
        Assert.Equal(4, game.State.CardObjects["ENEMY"].Power);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].UntilEndOfTurnPowerModifier);
        await game.Replay();
    }

    [Fact]
    public async Task AReadyReactionInvalidatesAnEarlierUnitCostQuote()
    {
        const string ready = "RAD·034/167";
        var initial = HandDiscardObserverTests.Put(Position(Guest, 1), "READY", ready, "P1", "HAND");
        var game = new Driver(initial);
        var before = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play(Guest)));
        Assert.Equal(8, before.Cost!.Mana);
        await game.Act("P1", new PlayCardCommand("READY", ready, ["RESTING-0"]));
        await game.Top();
        var after = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, Play(Guest)));
        Assert.True(after.CanPay, after.Message);
        Assert.Equal(9, after.Cost!.Mana);
        await game.Act("P1", Play(Guest));
        Assert.Equal(10, game.State.RunePools["P1"].Mana);
        await game.Replay();
    }

    [Fact]
    public void AnUnidentifiedStandbyCardNeverReducesTheUnitQuote()
    {
        var state = Position(Guest, 0);
        state = HandDiscardObserverTests.Put(state, "SECRET", "OGN·219/298", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "SECRET", "P1", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SECRET"] = state.CardObjects["SECRET"] with { IsExhausted = true, IsFaceDown = true, Tags = [CardObjectTags.Standby] } } };
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, Play(Guest)));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(9, quote.Cost!.Mana);
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task InvalidCommandsDoNotChangeTheCountResourcesOrPendingState(string no, string reason)
    {
        var state = Position(no, 2);
        if (reason == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        if (reason == "source") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "HAND");
        if (reason == "graveyard") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        var command = reason == "targets"
            ? new PlayCardCommand("UNIT", no, no == Guest ? ["ALLY"] : ["GEAR"])
            : Play(no);
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-exhausted-count", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }

    private static PlayCardCommand Play(string no) => new("UNIT", no, no == Guest ? [] : ["ENEMY"]);

    private static MatchState Position(string no, int count)
    {
        var state = HandDiscardObserverTests.Put(VenUnitEntryBasicTests.Position(no), "HAND-UNIT", "OGN·219/298", "P1", "HAND");
        state = state with { CardObjects = state.CardObjects.ToDictionary(p => p.Key, p => p.Value with {
            IsExhausted = p.Key is "GEAR" or "GRAVE" or "HAND-UNIT" or "ENEMY" }) };
        for (var index = 0; index < count; index++)
        {
            var id = "RESTING-" + index;
            state = HandDiscardObserverTests.Put(state, id, "OGN·219/298", "P1", "HAND");
            state = RadUnitEffectTestFixture.Place(state, id, "P1", index % 2 == 0 ? "BASE" : "BATTLEFIELD", index % 2 == 0 ? null : "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { IsExhausted = true } } };
        }
        return state;
    }
}
