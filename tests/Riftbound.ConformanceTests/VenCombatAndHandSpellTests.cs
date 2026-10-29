using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class VenCombatAndHandSpellTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task RampagePaysItsChosenCostAndUsesCurrentMightForBothDamageSources(bool extra, bool empowered)
    {
        var initial = Position("VEN·083");
        if (empowered) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", "VEN·124", "P1") with { IsEmpowered = true } } };
        var command = new PlayCardCommand("UNIT", "VEN·083", ["ALLY", "ENEMY"], OptionalCosts: extra ? ["SPEND_POWER:orange:1"] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(extra ? 1 : 0, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Orange));
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Top();
        var damage = game.Events().Where(e => e.Event.Kind == "DAMAGE_APPLIED").Select(e => e.Event).ToArray();
        Assert.Equal(2, damage.Length);
        Assert.Contains(damage, e => Equals(e.Payload.GetValueOrDefault("damageSourceObjectId"), "ALLY")
            && Equals(e.Payload.GetValueOrDefault("damage"), (empowered ? 5 : 4) + (extra ? 2 : 0)));
        Assert.Contains(damage, e => Equals(e.Payload.GetValueOrDefault("damageSourceObjectId"), "ENEMY")
            && Equals(e.Payload.GetValueOrDefault("damage"), 4));
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Graveyard);
        if (extra || empowered) {
            Assert.Contains("ALLY", game.State.PlayerZones["P1"].Base);
            await game.Act("P1", new EndTurnCommand());
            Assert.Equal(empowered ? 3 : 4, game.State.CardObjects["ALLY"].Power);
        } else Assert.Contains("ALLY", game.State.PlayerZones["P1"].Graveyard);
        await game.Replay();
    }

    [Fact]
    public async Task RampageStillGrantsItsPaidBonusWhenTheEnemyLeavesBeforeResolution()
    {
        var initial = AttackDamageDistributionTests.Spell(Position("VEN·083"), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·083", ["ALLY", "ENEMY"], OptionalCosts: ["SPEND_POWER:orange:1"]));
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"]));
        await game.Top(); await game.Top();
        Assert.Equal(6, game.State.CardObjects["ALLY"].Power);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "DAMAGE_APPLIED");
        await game.Act("P1", new EndTurnCommand()); Assert.Equal(4, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("SPEND_POWER:green:1")] [InlineData("SPEND_POWER:orange:2")]
    [InlineData("SPEND_MANA:1")] [InlineData("SPEND_POWER:orange:1,SPEND_POWER:orange:1")]
    public async Task RampageRejectsInventedOrDuplicateAdditionalCosts(string costs)
        => await Reject(Position("VEN·083"), "P1", new PlayCardCommand("UNIT", "VEN·083", ["ALLY", "ENEMY"], OptionalCosts: costs.Split(',')));

    [Theory]
    [InlineData("ALLY", "GEAR")] [InlineData("ENEMY", "ALLY")]
    [InlineData("ALLY", "GRAVE")] [InlineData("ALLY", "ALLY")]
    public async Task RampageRequiresOneFriendlyAndOneEnemyUnit(string first, string second)
        => await Reject(Position("VEN·083"), "P1", new PlayCardCommand("UNIT", "VEN·083", [first, second]));

    [Fact]
    public async Task RampageCannotSpendAnotherTraitInPlaceOfItsOptionalOrangePower()
    {
        var initial = Position("VEN·083");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(3, 0, new Dictionary<string, int> { [RuneTrait.Green] = 1 }) } };
        await Reject(initial, "P1", new PlayCardCommand("UNIT", "VEN·083", ["ALLY", "ENEMY"], OptionalCosts: ["SPEND_POWER:orange:1"]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvocationRevealsTheWholeHandButCanRecycleOnlyABlueCardIncludingAUnit(bool hasBlue)
    {
        var initial = Position("VEN·085");
        initial = WithHand(initial, hasBlue ? "OGN·097/298" : "OGN·219/298");
        var game = new Driver(initial);
        await Reject(initial, "P1", new PlayCardCommand("UNIT", "VEN·085", ["BLUE"]));
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·085", []));
        Assert.DoesNotContain("\"BLUE\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Top();
        var choice = game.State.PendingCardChoice!;
        Assert.Equal(hasBlue ? new[] { "BLUE" } : [], choice.LegalObjectIds);
        Assert.Equal(hasBlue ? 1 : 0, choice.RequiredCount);
        Assert.Contains("\"BLUE\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["NONBLUE"]));
        if (hasBlue) await Reject(game.State, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, []));
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, hasBlue ? ["BLUE"] : []));
        Assert.Contains("NONBLUE", game.State.PlayerZones["P2"].Hand);
        Assert.Equal(hasBlue, game.State.PlayerZones["P2"].MainDeck.Contains("BLUE"));
        Assert.DoesNotContain("\"BLUE\"", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P1"]));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SandsCanBePlayedButOnlyCountersIfAnOpponentConfirmedAnotherSpell(bool otherOnTheSameChain)
    {
        var initial = CounterPosition();
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("FIRST", "OGN·048/298", []));
        var first = game.State.StackItems.Single().StackItemId;
        if (otherOnTheSameChain) await game.Act("P2", new PlayCardCommand("OTHER-SPELL", "OGN·045/298", [first]));
        await game.Act("P2", new PassPriorityCommand());
        var command = new PlayCardCommand("UNIT", "VEN·039", [first]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Green]);
        await game.Act("P1", command); await game.Top();
        Assert.Equal(!otherOnTheSameChain, game.State.StackItems.Any(i => i.StackItemId == first));
        Assert.Equal(otherOnTheSameChain, game.Events().Any(e => e.Event.Kind == "STACK_ITEM_COUNTERED"));
        await game.Replay();
    }

    [Fact]
    public async Task SandsCountsAnEarlierCounteredSpellAndDoesNotRequireItToResolve()
    {
        var initial = AttackDamageDistributionTests.Spell(CounterPosition(), "DENY", "OGN·045/298", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "SECOND", "OGN·048/298", "P2");
        var game = new Driver(initial);
        await game.Act("P2", new PlayCardCommand("FIRST", "OGN·048/298", []));
        var first = game.State.StackItems.Single().StackItemId;
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("DENY", "OGN·045/298", [first])); await game.Top();
        Assert.Empty(game.State.StackItems);
        await game.Act("P2", new PlayCardCommand("SECOND", "OGN·048/298", []));
        var second = game.State.StackItems.Single().StackItemId;
        await game.Act("P2", new PassPriorityCommand());
        await game.Act("P1", new PlayCardCommand("UNIT", "VEN·039", [second])); await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "STACK_ITEM_COUNTERED"));
        Assert.DoesNotContain(game.State.CardPlays.Resolved, p => p.PlayerId == "P2");
        await game.Replay();
    }

    [Theory]
    [InlineData("VEN·039")] [InlineData("VEN·083")] [InlineData("VEN·085")]
    public async Task InsufficientManaNeverChangesTheStateOrDisclosesTheHand(string no)
    {
        var initial = WithHand(Position(no), "OGN·097/298");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 0) } };
        await Reject(initial, "P1", new PlayCardCommand("UNIT", no, no == "VEN·083" ? ["ALLY", "ENEMY"] : []));
    }

    private static MatchState CounterPosition()
    {
        var initial = Position("VEN·039") with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        initial = AttackDamageDistributionTests.Spell(initial, "FIRST", "OGN·048/298", "P2");
        return AttackDamageDistributionTests.Spell(initial, "OTHER-SPELL", "OGN·045/298", "P2");
    }

    private static MatchState WithHand(MatchState state, string no)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BLUE"] = OfficialGarenConquestBattlefieldCountTests.Printed("BLUE", no, "P2"),
            ["NONBLUE"] = OfficialGarenConquestBattlefieldCountTests.Printed("NONBLUE", "OGN·156/298", "P2") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Hand = ["BLUE", "NONBLUE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["BLUE"] = new("P2", "HAND"), ["NONBLUE"] = new("P2", "HAND") } };

    private static MatchState Position(string no)
    {
        var initial = VenUnitEntryBasicTests.Position(no);
        return initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Orange] = 3, [RuneTrait.Green] = 3 }),
            ["P2"] = new(20, 0, new Dictionary<string, int> { [RuneTrait.Green] = 3 }) } };
    }

    private static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-ven-combat", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }
}
