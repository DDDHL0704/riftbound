using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadBasicEffectTests
{
    public static IEnumerable<object[]> Cards() => new[] {
        new object[] { "RAD·003/167", 39493L, 3, 3, 0 },
        new object[] { "RAD·008/167", 39498L, 3, 2, 0 },
        new object[] { "RAD·059/167", 39555L, 9, -1, 1 },
        new object[] { "RAD·077/167", 39576L, 9, 7, 0 },
        new object[] { "RAD·088/167", 39588L, 2, 2, 1 } };

    [Theory]
    [MemberData(nameof(Cards))]
    public async Task RuntimeUsesTheExactOfficialPrintingAndItsPrintedCosts(string no, long id, int mana, int power, int powerCost)
    {
        var original = await OfficialCardCatalog.LoadDefaultAsync();
        var runtime = await OfficialCardCatalog.LoadRuntimeAsync();
        Assert.DoesNotContain(original.Cards, c => c.CardNo == no);
        var card = Assert.Single(runtime.Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(mana, card.Energy);
        Assert.Equal(power < 0 ? null : (int?)power, card.Power);
        Assert.Equal(powerCost, card.ReturnEnergy ?? 0);
        Assert.Equal(no switch {
            "RAD·003/167" => "当你打出我时，对最多两名单位各造成1点伤害。",
            "RAD·008/167" => "{{急速}}（你可以选择额外支付{{1}}和{{红色}}，让我以活跃状态进场。）\n当你打出我时，给予一名单位在本回合内{{S}}+2。",
            "RAD·059/167" => "对一名单位造成10点伤害。抽两张牌。",
            "RAD·077/167" => "我以活跃状态进场。\n{{坚守3}}（只要我是防守方，便{{S}}+3。）",
            _ => "{{强攻2}}，{{坚守2}}（只要我是进攻方或防守方，便{{S}}+2。）" }, card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.Equal(mana, behavior.ManaCost);
        Assert.Equal(powerCost, PrintedPowerCostRules.ForCard(no).Amount);
    }

    public static IEnumerable<object[]> Units() => Cards().Where(c => (int)c[3] >= 0)
        .SelectMany(c => new[] { "BASE", "BATTLEFIELD:BF" }.Select(destination => new object[] { c[0], c[2], c[3], destination }));

    [Theory]
    [MemberData(nameof(Units))]
    public async Task UnitsPayAndEnterBeforeTheirPrintedSkillsResolve(string no, int mana, int power, string destination)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, [], Destination: destination));
        Assert.Equal(20 - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(no == "RAD·088/167" ? 19 : 20, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(power, game.State.CardObjects["UNIT"].Power);
        Assert.Equal(no != "RAD·077/167", game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        if (no is "RAD·003/167" or "RAD·008/167") {
            Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
            Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ALLY"]));
            Assert.Equal(0, game.State.CardObjects["ALLY"].Damage);
            Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
            await game.Top();
            Assert.Equal(no == "RAD·003/167" ? 1 : 0, game.State.CardObjects["ALLY"].Damage);
            Assert.Equal(no == "RAD·008/167" ? 6 : 4, game.State.CardObjects["ALLY"].Power);
        } else {
            var profile = CardCombatKeywordRules.BuildProfile(CardBehaviorRegistry.GetAll().Single(b => b.CardNo == no));
            Assert.Equal(no == "RAD·088/167" ? 2 : 0, profile.AssaultAmount);
            Assert.Equal(no == "RAD·088/167" ? 2 : 3, profile.SteadfastAmount);
            Assert.Contains("坚守" + profile.SteadfastAmount, game.State.CardObjects["UNIT"].Tags);
            if (no == "RAD·088/167") Assert.Contains("强攻2", game.State.CardObjects["UNIT"].Tags);
            Assert.Empty(game.State.StackItems);
        }
        await game.Replay();
    }

    public static IEnumerable<object[]> InvalidPlays() => Cards().SelectMany(c =>
        new[] { "no-mana", "wrong-turn", "enemy-source", "graveyard" }.Select(branch => new object[] { c[0], branch }));

    [Theory]
    [MemberData(nameof(InvalidPlays))]
    public async Task InvalidPaymentTimingOwnershipOrZoneCannotChangeState(string no, string branch)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        if (branch == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        if (branch == "wrong-turn") state = state with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "enemy-source") state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["UNIT"] = state.CardObjects["UNIT"] with { OwnerId = "P2", ControllerId = "P2" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P2", "HAND") },
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [] } : p.Value with { Hand = ["UNIT"] }) };
        if (branch == "graveyard") state = state with {
            PlayerZones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1" ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        await Reject(state, new PlayCardCommand("UNIT", no, no == "RAD·059/167" ? ["ENEMY"] : []));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task PursuerMayDamageZeroOneOrTwoDistinctUnitsFromEitherSide(int count)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("RAD·003/167"));
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·003/167", []));
        var choice = game.State.PendingCardChoice!;
        var targets = new[] { "ALLY", "ENEMY" }.Take(count).ToArray();
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        if (game.State.StackItems.Count > 0) await game.Top();
        Assert.Equal(count > 0 ? 1 : 0, game.State.CardObjects["ALLY"].Damage);
        Assert.Equal(count > 1 ? 1 : 0, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(0, game.State.CardObjects["UNIT"].Damage);
        await game.Replay();
    }

    [Theory]
    [InlineData("RAD·003/167", "GEAR")] [InlineData("RAD·003/167", "GRAVE")]
    [InlineData("RAD·008/167", "GEAR")] [InlineData("RAD·008/167", "GRAVE")]
    public async Task EntryChoicesRejectEquipmentAndUnitsOutsidePlayWithoutLosingTheSkill(string no, string target)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position(no));
        await game.Act("P1", new PlayCardCommand("UNIT", no, []));
        var choice = game.State.PendingCardChoice!;
        await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [target]));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task PursuerRejectsDuplicateTargetsAndMoreThanTwoTargets()
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("RAD·003/167"));
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·003/167", []));
        var choice = game.State.PendingCardChoice!;
        foreach (var targets in new[] { new[] { "ALLY", "ALLY" }, new[] { "ALLY", "ENEMY", "UNIT" } })
            await Reject(game.State, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, targets));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await game.Top(); Assert.Equal(1, game.State.CardObjects["UNIT"].Damage); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ScoutsOptionalHastePaysExactlyOneExtraManaAndRedPowerAndItsBuffExpires(bool haste)
    {
        var initial = VenUnitEntryBasicTests.Position("RAD·008/167") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·008/167", [], OptionalCosts: haste ? ["HASTE_READY"] : []));
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].Mana);
        Assert.Equal(haste ? 0 : 1, game.State.RunePools["P1"].TotalPower);
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]));
        await game.Top(); Assert.Equal(4, game.State.CardObjects["UNIT"].Power);
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(2, game.State.CardObjects["UNIT"].Power); await game.Replay();
    }

    [Fact]
    public async Task ScoutsRedHasteCostCannotUseBluePower()
    {
        var state = VenUnitEntryBasicTests.Position("RAD·008/167") with { RunePools = new Dictionary<string, RunePool> {
            ["P1"] = new(4, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }), ["P2"] = new(20, 20) } };
        await Reject(state, new PlayCardCommand("UNIT", "RAD·008/167", [], OptionalCosts: ["HASTE_READY"]));
    }

    [Theory]
    [InlineData("ALLY")] [InlineData("ENEMY")]
    public async Task SupernovaDamagesEitherPlayersUnitThenDrawsTwoAfterResponses(string target)
    {
        var game = new Driver(VenUnitEntryBasicTests.Position("RAD·059/167"));
        await game.Act("P1", new PlayCardCommand("UNIT", "RAD·059/167", [target]));
        Assert.Equal(11, game.State.RunePools["P1"].Mana);
        Assert.Equal(19, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.CardObjects[target].Damage); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        await game.Top();
        Assert.Contains(target, game.State.PlayerZones[target == "ALLY" ? "P1" : "P2"].Graveyard);
        Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, game.State.PlayerZones["P1"].Hand);
        Assert.Equal(10, Convert.ToInt32(Assert.Single(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED").Event.Payload["damage"]));
        Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    [Theory]
    [InlineData("GEAR")] [InlineData("GRAVE")] [InlineData("BF")]
    public async Task SupernovaRejectsNonunitAndOffFieldTargets(string target)
        => await Reject(VenUnitEntryBasicTests.Position("RAD·059/167"), new PlayCardCommand("UNIT", "RAD·059/167", [target]));

    [Fact]
    public async Task CounteringSupernovaPreventsBothDamageAndDraw()
    {
        var initial = AttackDamageDistributionTests.Spell(VenUnitEntryBasicTests.Position("RAD·059/167"), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial); await game.Act("P1", new PlayCardCommand("UNIT", "RAD·059/167", ["ENEMY"]));
        var item = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [item.StackItemId])); await game.Top();
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); Assert.Empty(game.State.PlayerZones["P1"].Hand);
        Assert.Equal(3, game.State.PlayerZones["P1"].MainDeck.Count); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Graveyard); await game.Replay();
    }

    private static async Task Reject(MatchState state, GameCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("rad-illegal", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
