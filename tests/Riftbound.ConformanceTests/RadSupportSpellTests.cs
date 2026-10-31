using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadSupportSpellTests
{
    [Theory]
    [InlineData("RAD·025/167", 39518L, 1, 0)]
    [InlineData("RAD·134/167", 39640L, 7, 1)]
    public async Task RuntimeAdmitsTheExactOfficialSpellAndPrintedCosts(string no, long id, int mana, int power)
    {
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == no);
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy); Assert.Null(card.Power);
        Assert.Equal(power, card.ReturnEnergy ?? 0);
        Assert.Equal(no == "RAD·025/167"
            ? "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n给予一名单位在本回合内{{坚守2}}。（只要它是防守方，便{{S}}+2。）\n{{洞察}}。（查看你主牌堆顶部的一张牌。你可以选择将其回收。）"
            : "让你的所有单位变为活跃状态，并给予他们在本回合内{{S}}+3。", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var definition));
        Assert.Equal(mana, definition.ManaCost); Assert.Equal(power, PrintedPowerCostRules.ForCard(no).Amount);
    }

    [Theory]
    [InlineData("ALLY", false)] [InlineData("ALLY", true)]
    [InlineData("ENEMY", false)] [InlineData("ENEMY", true)]
    public async Task ListenGrantsEitherSidesUnitSteadfastBeforePrivateInsightAndExpiresAtTurnEnd(string target, bool recycle)
    {
        var game = new Driver(Position("RAD·025/167"));
        var deck = game.State.PlayerZones["P1"].MainDeck.ToArray();
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·025/167", [target]));
        Assert.Equal(19, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, Steadfast(game.State, target)); Assert.Null(game.State.PendingCardChoice);
        await game.Top();
        Assert.Equal(2, Steadfast(game.State, target)); Assert.Equal(4, game.State.CardObjects[target].Power);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("INSIGHT", choice.ChoiceWindow); Assert.Equal("P1", choice.PlayerId);
        Assert.Equal(new[] { deck[0] }, choice.ContextObjectIds);
        await Reject(game.State, "P2", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [deck[1]]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, recycle ? [deck[0]] : []));
        Assert.Equal(recycle ? new[] { deck[1], deck[2], deck[0] } : deck, game.State.PlayerZones["P1"].MainDeck);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(0, Steadfast(game.State, target)); await game.Replay();
    }

    [Theory]
    [InlineData("GEAR")] [InlineData("GRAVE")] [InlineData("BF")] [InlineData("UNIT")]
    public async Task ListenRejectsEquipmentOffFieldUnitsBattlefieldsAndTheSpellItself(string target)
        => await Reject(Position("RAD·025/167"), "P1", new PlayCardCommand("UNIT", "RAD·025/167", [target]));

    [Theory]
    [InlineData("RAD·025/167", "no-mana")] [InlineData("RAD·134/167", "no-mana")]
    [InlineData("RAD·025/167", "enemy-source")] [InlineData("RAD·134/167", "enemy-source")]
    [InlineData("RAD·025/167", "graveyard")] [InlineData("RAD·134/167", "graveyard")]
    [InlineData("RAD·134/167", "no-power")]
    public async Task IllegalSourceZoneOwnershipAndPaymentLeaveAllStateAndViewsUnchanged(string no, string branch)
    {
        var state = Position(no);
        if (branch is "no-mana" or "no-power") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = branch == "no-mana" ? RunePool.Empty : new(7, 0) } };
        if (branch == "enemy-source") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P2", "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value with { Hand = ["UNIT"] }) };
        if (branch == "graveyard") state = state with {
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value) };
        await Reject(state, "P1", new PlayCardCommand("UNIT", no, Targets(no)));
    }

    [Fact]
    public async Task ListenCanReactOnTheOpponentsTurnWhileBangerCannot()
    {
        var initial = Position("RAD·025/167") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = AttackDamageDistributionTests.Spell(initial, "INCOMING", "VEN·049", "P2");
        initial = AttackDamageDistributionTests.Spell(initial, "SLOW", "RAD·134/167", "P1");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("INCOMING", "VEN·049", []));
        await game.Act("P2", new PassPriorityCommand());
        await Reject(game.State, "P1", new PlayCardCommand("SLOW", "RAD·134/167", []));
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·025/167", ["ALLY"])); await game.Top();
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        Assert.Equal(2, Steadfast(game.State, "ALLY")); Assert.Single(game.State.StackItems);
        await game.Top(); Assert.Single(game.State.PlayerZones["P2"].Hand); await game.Replay();
    }

    [Theory]
    [InlineData("RAD·025/167")] [InlineData("RAD·134/167")]
    public async Task CounteringTheSpellPreventsEveryInstruction(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", no, Targets(no)));
        var parent = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [parent.StackItemId])); await game.Top();
        Assert.Equal(0, Steadfast(game.State, "ALLY")); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(game.State.StackItems); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BangerReadiesAllFriendlyUnitsBeforeBuffingThemAndLeavesOtherObjectsUnchanged(bool fieldAlreadyReady)
    {
        var initial = Position("RAD·134/167");
        if (fieldAlreadyReady) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["FIELD"] = initial.CardObjects["FIELD"] with { IsExhausted = false } } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·134/167", []));
        Assert.Equal(13, game.State.RunePools["P1"].Mana); Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Top();
        Assert.Equal(new[] { "UNIT_READIED", "UNIT_READIED", "POWER_MODIFIED_UNTIL_END_OF_TURN", "POWER_MODIFIED_UNTIL_END_OF_TURN" }, UnitInstructions(game));
        foreach (var id in new[] { "ALLY", "FIELD" }) {
            Assert.False(game.State.CardObjects[id].IsExhausted); Assert.Equal(7, game.State.CardObjects[id].Power);
        }
        foreach (var id in new[] { "ENEMY", "GEAR", "RUNE" }) {
            Assert.True(game.State.CardObjects[id].IsExhausted); Assert.Equal(initial.CardObjects[id].Power, game.State.CardObjects[id].Power);
        }
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(4, game.State.CardObjects["ALLY"].Power); Assert.Equal(4, game.State.CardObjects["FIELD"].Power); await game.Replay();
    }

    [Theory]
    [InlineData("ALLY", 4, 0)] [InlineData("ENEMY", 2, 2)]
    public async Task EchoRepeatsTheKeywordInstructionBeforeEachIndependentInsight(string later, int allyAmount, int enemyAmount)
    {
        var initial = Position("RAD·025/167");
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·025/167", ["ALLY"], OptionalCosts: ["ECHO"], RepeatChoices: [new("", [later])]));
        await game.Top(); Assert.Equal(2, Steadfast(game.State, "ALLY")); Assert.Equal(0, Steadfast(game.State, "ENEMY"));
        var first = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        var firstTop = Assert.Single(first.ContextObjectIds);
        await game.Act("P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, [firstTop]));
        var second = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(1, second.DeckContext!.ExecutionIndex); Assert.NotEqual(first.ChoiceId, second.ChoiceId);
        Assert.DoesNotContain(firstTop, second.ContextObjectIds);
        Assert.Equal(allyAmount, Steadfast(game.State, "ALLY")); Assert.Equal(enemyAmount, Steadfast(game.State, "ENEMY"));
        await Reject(game.State, "P1", new ChooseCardsCommand(first.ChoiceId, first.ChoiceWindow, []));
        await game.Act("P1", new ChooseCardsCommand(second.ChoiceId, second.ChoiceWindow, []));
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Single(game.State.PlayerZones["P1"].Graveyard, id => id == "UNIT");
        Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]); await game.Replay();
    }

    [Fact]
    public async Task EchoRepeatsTheWholeReadyThenBuffSequenceAndOnlyPlaysTheSourceOnce()
    {
        var initial = Position("RAD·134/167");
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·134/167", [], OptionalCosts: ["ECHO"])); await game.Top();
        var once = new[] { "UNIT_READIED", "UNIT_READIED", "POWER_MODIFIED_UNTIL_END_OF_TURN", "POWER_MODIFIED_UNTIL_END_OF_TURN" };
        Assert.Equal(once.Concat(once), UnitInstructions(game));
        Assert.Equal(10, game.State.CardObjects["ALLY"].Power); Assert.Equal(10, game.State.CardObjects["FIELD"].Power);
        Assert.Single(game.State.PlayerZones["P1"].Graveyard, id => id == "UNIT"); Assert.Equal(1, game.State.PlayerCardsPlayedThisTurn["P1"]);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["ALLY"].Power); await game.Replay();
    }

    private static string[] Targets(string no) => no == "RAD·025/167" ? ["ALLY"] : [];
    private static int Steadfast(MatchState state, string id) => 2 * state.CardObjects[id].UntilEndOfTurnEffects
        .Count(effect => effect.StartsWith("COMBAT_KEYWORD:坚守2:", StringComparison.Ordinal));
    private static string[] UnitInstructions(Driver game) => game.Events()
        .Where(e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "UNIT")
            && e.Event.Kind is "UNIT_READIED" or "POWER_MODIFIED_UNTIL_END_OF_TURN")
        .Select(e => e.Event.Kind).ToArray();

    private static MatchState Position(string no)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        var cards = initial.CardObjects.ToDictionary();
        foreach (var id in new[] { "ALLY", "ENEMY", "GEAR" }) cards[id] = cards[id] with { IsExhausted = true };
        cards["FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("FIELD", "OGN·219/298", "P1") with { IsExhausted = true };
        cards["RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE", "UNL-R01", "P1") with { IsExhausted = true };
        return initial with { CardObjects = cards,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FIELD"] = new("P1", "BATTLEFIELD", "BF"), ["RUNE"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) { ["P1"] = initial.PlayerZones["P1"] with {
                Base = [..initial.PlayerZones["P1"].Base, "RUNE"], Battlefields = [..initial.PlayerZones["P1"].Battlefields, "FIELD"] } } };
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("illegal-rad-support", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
