using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class BoonAdditionalPlayCostTests
{
    private const string Beatdown = "OGN·146/298", Kraken = "OGN·150/298", Glory = "OGN·207/298";

    private static CardBehaviorDefinition Behavior(string no)
    { Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var value)); return value; }

    private static MatchState Position(string no, int boons = 1, int mana = 0, int orange = 0)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Orange] = orange }) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = true } } };
        for (var i = 1; i <= boons; i++) {
            var id = "PAYER" + i;
            state = RadGearTestPosition.Put(state, id, "UNL-004/219", "P1", "BASE");
            Assert.True(BoonRules.TryGrant(state.CardObjects[id], state.PlayerZones, state.CardObjects, out var granted));
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = granted } };
        }
        return state;
    }

    private static string[] BoonChoices(MatchState state, string no, string player = "P1")
        => CoreRuleEngine.BoonPlayCostChoices(state, player, Behavior(no)).Select(choice => choice.Id).ToArray();

    private static PlayCardCommand Spell(string no, IReadOnlyList<string> costs, string target = "ALLY")
        => new("UNIT", no, [target], OptionalCosts: costs);

    private static void QuoteMatches(MatchState state, PlayCardCommand command, int mana, int power)
    {
        var before = MatchStateHasher.Hash(state);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData(Beatdown)] [InlineData(Glory)]
    public async Task EachSpellCanConsumeOneControlledBoonToIgnoreItsBaseCostBeforeResponses(string no)
    {
        var initial = Position(no); var costs = BoonChoices(initial, no);
        Assert.Single(costs);
        var command = Spell(no, costs); QuoteMatches(initial, command, 0, 0);
        Assert.Contains(CommandTypes.PlayCard, ResolutionResult.BuildPrompts(initial)["P1"].Actions);
        var game = new Driver(initial); var power = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", command);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        Assert.Single(game.Events(), entry => entry.Event.Kind == "BOON_CONSUMED");
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(power, game.State.CardObjects["ALLY"].Power);
        await game.Top();
        Assert.Equal(no != Beatdown, game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(power + (no == Glory ? 3 : 0), game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData(Beatdown, 2)] [InlineData(Glory, 3)]
    public async Task DecliningTheOptionalBoonCostPaysThePrintedManaAndKeepsTheBoon(string no, int mana)
    {
        var initial = Position(no, mana: mana); var command = Spell(no, []);
        QuoteMatches(initial, command, mana, 0);
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(1, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "BOON_CONSUMED");
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(0, false, 2, 3)] [InlineData(1, false, 1, 3)] [InlineData(2, false, 0, 3)]
    [InlineData(1, true, 2, 4)] [InlineData(2, true, 1, 4)]
    [InlineData(3, true, 0, 4)] [InlineData(4, true, 0, 4)]
    public async Task KrakenReducesOrangeInItsTotalCostIncludingHasteAndMayConsumeBeyondZero(int count, bool haste, int power, int mana)
    {
        var initial = Position(Kraken, count, mana, power);
        IReadOnlyList<string> costs = BoonChoices(initial, Kraken).Concat(haste ? new[] { HasteOptionalCostNames.HasteReady } : []).ToArray();
        var command = new PlayCardCommand("UNIT", Kraken, [], OptionalCosts: costs);
        QuoteMatches(initial, command, mana, power);
        Assert.Contains(CommandTypes.PlayCard, ResolutionResult.BuildPrompts(initial)["P1"].Actions);
        var game = new Driver(initial); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(!haste, game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(count, game.Events().Count(entry => entry.Event.Kind == "BOON_CONSUMED"));
        for (var i = 1; i <= count; i++) Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER" + i]));
        await game.Replay();
    }

    [Theory]
    [InlineData(Beatdown)] [InlineData(Glory)]
    public async Task IgnoringTheBaseCostStillRequiresTheEnemyTargetsWardPower(string no)
    {
        var initial = Position(no);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [..initial.CardObjects["ENEMY"].Tags, CardObjectTags.Spellshield] } } };
        var costs = BoonChoices(initial, no); var command = Spell(no, costs, "ENEMY");
        await Reject(initial, command);
        var funded = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(0, 1) } };
        QuoteMatches(funded, command, 0, 1);
        var game = new Driver(funded); await game.Act("P1", command);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        await game.Top(); await game.Replay();
    }

    [Theory]
    [InlineData(Beatdown)] [InlineData(Glory)]
    public async Task CounteringTheSpellCannotRefundItsAlreadyConsumedAdditionalBoon(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "COUNTER", "OGN·064/298", "P2", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", Spell(no, BoonChoices(initial, no)));
        var source = Assert.Single(game.State.StackItems).StackItemId;
        if (game.State.PriorityPlayerId != "P2") await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [source]));
        await game.Top();
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(initial.CardObjects["ALLY"].Power, game.State.CardObjects["ALLY"].Power);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        await game.Replay();
    }

    [Fact]
    public async Task ARealReturnResponseCannotLinkThePrivateBoonPayerOrRevokeThePaidSpell()
    {
        var initial = RadGearTestPosition.Put(Position(Glory, mana: 1), "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var power = game.State.CardObjects["ALLY"].Power;
        await game.Act("P1", Spell(Glory, BoonChoices(initial, Glory)));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["PAYER1"]));
        await game.Top();
        Assert.Contains("PAYER1", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain("PAYER1", JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        await game.Top(); Assert.Equal(power + 3, game.State.CardObjects["ALLY"].Power);
        await game.Replay();
    }

    [Theory]
    [InlineData("0")] [InlineData("01")] [InlineData("-1")] [InlineData("2147483648")]
    public async Task InvalidBoonQuantitiesCannotSpendCountersOrResources(string count)
    {
        var initial = Position(Glory, mana: 3); var choice = Assert.Single(BoonChoices(initial, Glory));
        await Reject(initial, Spell(Glory, [choice[..(choice.LastIndexOf(':') + 1)] + count]));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TheSingleBoonWaiverRejectsDuplicateOrMultiplePayers(bool duplicate)
    {
        var initial = Position(Glory, boons: 2, mana: 3); var choices = BoonChoices(initial, Glory);
        await Reject(initial, Spell(Glory, duplicate ? [choices[0], choices[0]] : choices));
    }

    [Fact]
    public async Task TheOtherPlayersBoonAndAnEarlierGenerationCannotPay()
    {
        var initial = Position(Glory, mana: 3);
        Assert.True(BoonRules.TryGrant(initial.CardObjects["ENEMY"], initial.PlayerZones, initial.CardObjects, out var enemy));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["ENEMY"] = enemy } };
        var foreign = Assert.Single(BoonChoices(initial, Glory, "P2"));
        await Reject(initial, Spell(Glory, [foreign]));
        var old = Assert.Single(BoonChoices(initial, Glory));
        var fresh = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["PAYER1"] = initial.CardObjects["PAYER1"] with { ObjectGeneration = 1 } } };
        await Reject(fresh, Spell(Glory, [old]));
    }

    [Fact]
    public async Task KrakenCanConsumeSeveralRealBoonsFromOnePermittedUnit()
    {
        var initial = Position(Kraken, boons: 0, mana: 4);
        initial = RadGearTestPosition.Put(initial, "PAYER1", "OGN·078/298", "P1", "BASE");
        for (var i = 0; i < 3; i++) {
            Assert.True(BoonRules.TryGrant(initial.CardObjects["PAYER1"], initial.PlayerZones, initial.CardObjects, out var granted));
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) { ["PAYER1"] = granted } };
        }
        var one = Assert.Single(BoonChoices(initial, Kraken));
        var all = one[..(one.LastIndexOf(':') + 1)] + "3";
        var command = new PlayCardCommand("UNIT", Kraken, [], OptionalCosts: [all, HasteOptionalCostNames.HasteReady]);
        QuoteMatches(initial, command, 4, 0);
        var game = new Driver(initial); var power = game.State.CardObjects["PAYER1"].Power;
        await game.Act("P1", command);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        Assert.Equal(power - 3, game.State.CardObjects["PAYER1"].Power);
        Assert.Equal(3, game.Events().Count(entry => entry.Event.Kind == "BOON_CONSUMED"));
        Assert.False(game.State.CardObjects["UNIT"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task AControlledBoonMayBePaidEvenWhenItsUnitBelongsToTheOpponent()
    {
        var initial = Position(Glory);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["PAYER1"] = initial.CardObjects["PAYER1"] with { OwnerId = "P2" } } };
        var game = new Driver(initial);
        await game.Act("P1", Spell(Glory, BoonChoices(initial, Glory)));
        Assert.Equal("P2", game.State.CardObjects["PAYER1"].OwnerId);
        Assert.Equal(0, BoonRules.Count(game.State.CardObjects["PAYER1"]));
        await game.Top(); await game.Replay();
    }

    [Fact]
    public async Task AnAffordableOptionalBranchDoesNotAutomaticallyConsumeAnUndeclaredBoon()
    {
        var initial = Position(Glory);
        Assert.Contains(CommandTypes.PlayCard, ResolutionResult.BuildPrompts(initial)["P1"].Actions);
        await Reject(initial, Spell(Glory, []));
        Assert.Equal(1, BoonRules.Count(initial.CardObjects["PAYER1"]));
    }

    [Fact]
    public async Task ACardWithoutThePrintedBoonPermissionCannotUseTheSamePayersChoice()
    {
        var initial = Position("SFD·012/221", mana: 5);
        var borrowedChoice = Assert.Single(BoonChoices(initial, Glory));
        var normal = new PlayCardCommand("UNIT", "SFD·012/221", []);
        QuoteMatches(initial, normal, 5, 0);
        await Reject(initial, normal with { OptionalCosts = [borrowedChoice] });
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("reject-boon-cost", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Same(state, result.State); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(result.Snapshots));
        Assert.Equal(prompts, JsonSerializer.Serialize(result.Prompts));
    }
}
