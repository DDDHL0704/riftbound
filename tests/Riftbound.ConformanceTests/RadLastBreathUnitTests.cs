using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadLastBreathUnitTests
{
    private const string Hound = "RAD·119/167";
    private const string Dredger = "UNL-153/219";
    private const string Kill = "OGN·229/298";

    public static IEnumerable<object[]> Entries() => new[] { Hound, Dredger }
        .SelectMany(no => new[] { "BASE", "BATTLEFIELD:BF" }.Select(destination => new object[] { no, destination }));

    public static IEnumerable<object[]> InvalidPlays() => new[] { Hound, Dredger }
        .SelectMany(no => new[] { "mana", "turn", "owner", "graveyard", "targets" }
            .Select(reason => new object[] { no, reason }));

    [Theory]
    [InlineData(Hound)]
    [InlineData(Dredger)]
    public async Task BothOfficialWordingsProduceOneMandatoryWarhawkSkill(string no)
    {
        var catalog = await OfficialCardCatalog.LoadRuntimeAsync();
        var card = Assert.Single(catalog.Cards, c => c.CardNo == no);
        if (no == Hound)
        {
            Assert.Equal(39624, card.Id);
            Assert.Equal("狩猎犬", card.CardName);
            Assert.Equal(3, card.Energy);
            Assert.Equal(3, card.Power);
            Assert.Null(card.ReturnEnergy);
            Assert.Equal("{{绝念>}} 打出一名1{{S}}的“战鹰”单位指示物到你的基地，它拥有{{法盾}}。（当我被摧毁后，发动此效果。对手必须支付{{A}}才能将拥有{{法盾}}的目标选作法术或技能的目标。）", card.CardEffect);
        }
        var trigger = Assert.Single(TriggerParser.Parse(card.CardEffect), t => t.Timing == TriggerTimings.UnitDestroyed);
        Assert.Equal(TriggerKinds.UnitLastBreathCreateWarhawk, trigger.Kind);
        Assert.Equal(1, trigger.CreatedTokenCount);
        Assert.Equal(1, trigger.CreatedTokenPower);
        Assert.Equal("战鹰", trigger.CreatedTokenName);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, trigger.CreatedTokenDestination);
        Assert.Equal(new[] { "法盾" }, trigger.CreatedTokenKeywords);
        Assert.False(trigger.Optional ?? false);
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public async Task PaidEntryAndDestructionWaitForSeparateResponsesBeforeCreatingTheWarhawk(string no, string destination)
    {
        var initial = WithHandCard(VenUnitEntryBasicTests.Position(no), "KILL", Kill);
        var game = new Driver(initial);
        var cost = CardBehaviorRegistry.GetAll().Single(b => b.CardNo == no).ManaCost;
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(20 - cost, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(Warhawks(game.State));

        var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"]));
        Assert.DoesNotContain("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(Warhawks(game.State));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        var item = Assert.Single(game.State.StackItems);
        var context = Assert.IsType<LastBreathUnitCreationContext>(item.LastBreathUnitCreation);
        Assert.Equal(no, context.CardNo);
        Assert.Equal(generation, context.Source.Generation);
        Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration);
        Assert.Empty(Warhawks(game.State));

        var mana = game.State.RunePools["P1"].Mana;
        await game.Top();
        AssertWarhawk(game.State, "P1");
        Assert.Equal(mana, game.State.RunePools["P1"].Mana);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(Hound)]
    [InlineData(Dredger)]
    public async Task TheDeathControllerReceivesTheTokenWhileThePhysicalSourceReturnsToItsOwner(string no)
    {
        var initial = WithHandCard(VenUnitEntryBasicTests.Position(no), "KILL", Kill);
        initial = RadUnitEffectTestFixture.Place(initial, "UNIT", "P2", "BASE");
        var game = new Driver(initial);
        Assert.Equal("P1", game.State.CardObjects["UNIT"].OwnerId);
        await game.Act("P1", new PlayCardCommand("KILL", Kill, ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        Assert.Equal("P2", Assert.Single(game.State.StackItems).ControllerId);
        Assert.Empty(Warhawks(game.State));
        await game.Top();
        AssertWarhawk(game.State, "P2");
        await game.Replay();
    }

    [Theory]
    [InlineData(Hound)]
    [InlineData(Dredger)]
    public async Task ReturningTheLivingUnitToHandDoesNotCreateADeathSkillOrToken(string no)
    {
        const string recall = "OGN·104/298";
        var game = new Driver(WithHandCard(VenUnitEntryBasicTests.Position(no), "RETURN", recall));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        await game.Act("P1", new PlayCardCommand("RETURN", recall, ["UNIT"]));
        await game.Top();
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
        Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.TriggerQueue);
        Assert.Empty(Warhawks(game.State));
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "UNIT_DESTROYED");
        await game.Replay();
    }

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task IllegalPlaysCannotSpendResourcesMoveTheCardOrCreateTokens(string no, string reason)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (reason == "mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        if (reason == "turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (reason == "owner") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P2", "HAND");
        if (reason == "graveyard") state = RadUnitEffectTestFixture.Place(state, "UNIT", "P1", "GRAVEYARD");
        var before = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var command = new PlayCardCommand("UNIT", no, reason == "targets" ? ["ALLY"] : []);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-last-breath-unit", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
        Assert.Empty(Warhawks(result.State));
    }

    private static MatchState WithHandCard(MatchState state, string id, string no)
        => HandDiscardObserverTests.Put(state, id, no, "P1", "HAND");

    private static CardObjectState[] Warhawks(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.WarhawkTokenCardNo).ToArray();

    private static void AssertWarhawk(MatchState state, string player)
    {
        var token = Assert.Single(Warhawks(state));
        Assert.Equal(player, token.OwnerId);
        Assert.Equal(player, token.ControllerId);
        Assert.Equal(1, token.Power);
        Assert.True(token.IsExhausted);
        Assert.Contains(CardObjectTags.Spellshield, token.Tags);
        Assert.Contains(token.ObjectId, state.PlayerZones[player].Base);
        Assert.Equal("BASE", state.ObjectLocations[token.ObjectId].Zone);
    }
}
