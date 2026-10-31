using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadFixedSacrificeUnitTests
{
    private const string Card = "RAD·125/167";
    private const string Sacrifice = "DESTROY_FRIENDLY_UNIT:ALLY";

    [Fact]
    public async Task RuntimeAdmitsTheExactPrintingAndItsYellowPowerCost()
    {
        Assert.DoesNotContain((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == Card);
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Card);
        Assert.Equal(39630, card.Id); Assert.Equal("沙丘吞噬者", card.CardName);
        Assert.Equal(7, card.Energy); Assert.Equal(7, card.Power); Assert.Equal(1, card.ReturnEnergy);
        Assert.Equal("你可以选择摧毁一名友方单位，作为打出我的额外费用。若如此做，则我的费用减少{{2}}。\n{{法盾}}（对手必须支付{{A}}才能将我选作法术或技能的目标。）", card.CardEffect);
        var power = PrintedPowerCostRules.ForCard(Card);
        Assert.Equal(1, power.Amount); Assert.Equal([RuneTrait.Yellow], power.Traits);
    }

    [Theory]
    [InlineData(null, "BASE")] [InlineData(null, "BATTLEFIELD:BF")]
    [InlineData("OGN·219/298", "BASE")] [InlineData("OGN·219/298", "BATTLEFIELD:BF")]
    [InlineData("OGN·142/298", "BASE")] [InlineData("OGN·142/298", "BATTLEFIELD:BF")]
    [InlineData("OGN·231/298", "BASE")] [InlineData("OGN·231/298", "BATTLEFIELD:BF")]
    [InlineData("UNL-170/219", "BASE")] [InlineData("UNL-170/219", "BATTLEFIELD:BF")]
    public async Task SacrificeAlwaysReducesOnlyTwoManaRegardlessOfTheVictimsPrintedCosts(string? victim, string destination)
    {
        var initial = Position(victim);
        var game = new Driver(initial);
        var command = new PlayCardCommand("UNIT", Card, [], Destination: destination,
            OptionalCosts: victim is null ? [] : [Sacrifice]);
        var hash = MatchStateHasher.Hash(initial);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(victim is null ? 7 : 5, quote.Cost!.Mana);
        Assert.Equal(1, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        Assert.Equal(hash, MatchStateHasher.Hash(initial));
        await game.Act("P1", command);
        Assert.Equal(victim is null ? 13 : 15, game.State.RunePools["P1"].Mana);
        Assert.Equal(2, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
        Assert.True(game.State.CardObjects["UNIT"].IsExhausted);
        Assert.Contains(CardObjectTags.Spellshield, game.State.CardObjects["UNIT"].Tags);
        Assert.Equal(destination == "BASE" ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations["UNIT"].Zone);
        Assert.Equal(victim is not null, game.State.PlayerZones["P1"].Graveyard.Contains("ALLY"));
        Assert.Equal(victim is null, game.State.PlayerZones["P1"].Base.Contains("ALLY"));
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Fact]
    public void ExactFiveManaAndOneYellowOfferTheAffordableSacrificeInTheNormalPrompt()
    {
        var initial = WithPool(Position("OGN·231/298"), new(5, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }));
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(play.OptionalCosts ?? [], c => c.Id == Sacrifice);
        var engine = new CoreRuleEngine();
        var ordinary = new PlayCardCommand("UNIT", Card, []);
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, ordinary)).CanPay);
        var reduced = ordinary with { OptionalCosts = [Sacrifice] };
        Assert.True(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, reduced)).CanPay);
    }

    [Theory]
    [InlineData("mana")] [InlineData("power")] [InlineData("wrong-trait")]
    [InlineData("no-sacrifice")] [InlineData("enemy")] [InlineData("equipment")]
    [InlineData("graveyard")] [InlineData("source-in-hand")] [InlineData("unknown")]
    [InlineData("duplicate")] [InlineData("two-victims")] [InlineData("face-down")]
    [InlineData("no-controller")] [InlineData("wrong-turn")] [InlineData("source-graveyard")]
    public async Task InvalidCostsPaymentTimingAndSourcesPreserveTheMatchAndProjections(string branch)
    {
        var initial = Position("OGN·231/298");
        if (branch is "mana" or "power" or "wrong-trait" or "no-sacrifice") initial = WithPool(initial, branch switch {
            "mana" => new(4, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }),
            "power" => new(5, 0),
            "wrong-trait" => new(5, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 1 }),
            _ => new(5, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) });
        if (branch is "face-down" or "no-controller") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ALLY"] = initial.CardObjects["ALLY"] with { IsFaceDown = branch == "face-down", ControllerId = branch == "no-controller" ? null : "P1" } } };
        if (branch == "wrong-turn") initial = initial with { TurnPlayerId = "P2", ActivePlayerId = "P2" };
        if (branch == "source-graveyard") initial = initial with {
            PlayerZones = initial.PlayerZones.ToDictionary(p => p.Key, p => p.Key == "P1"
                ? p.Value with { Hand = [], Graveyard = [..p.Value.Graveyard, "UNIT"] } : p.Value),
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["UNIT"] = new("P1", "GRAVEYARD") } };
        string[] costs = branch switch {
            "no-sacrifice" => [], "enemy" => ["DESTROY_FRIENDLY_UNIT:ENEMY"],
            "equipment" => ["DESTROY_FRIENDLY_UNIT:GEAR"], "graveyard" => ["DESTROY_FRIENDLY_UNIT:GRAVE"],
            "source-in-hand" => ["DESTROY_FRIENDLY_UNIT:UNIT"], "unknown" => ["DESTROY_FRIENDLY_UNIT:MISSING"],
            "duplicate" => [Sacrifice, Sacrifice], "two-victims" => [Sacrifice, "DESTROY_FRIENDLY_UNIT:GRAVE"],
            _ => [Sacrifice] };
        await Reject(initial, "P1", new PlayCardCommand("UNIT", Card, [], OptionalCosts: costs));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task TheNormallyPlayedBodyChargesWardSeparatelyWhenAnOpponentTargetsIt(int wardPower)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(), "OPEN", "VEN·049", "P1");
        initial = AttackDamageDistributionTests.Spell(initial, "DEBUFF", "UNL-066/219", "P2");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(7, wardPower) } };
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("UNIT", Card, [], Destination: "BATTLEFIELD:BF"));
        await game.Act("P1", new PlayCardCommand("OPEN", "VEN·049", []));
        await game.Act("P1", new PassPriorityCommand());
        var debuff = new PlayCardCommand("DEBUFF", "UNL-066/219", ["UNIT"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P2", PlayCostPreviewTests.Request(game.State, debuff));
        Assert.Equal(7, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower);
        Assert.Equal(wardPower == 1, quote.CanPay);
        if (wardPower == 0) await Reject(game.State, "P2", debuff);
        else {
            await game.Act("P2", debuff);
            Assert.Equal(0, game.State.RunePools["P2"].TotalPower);
            Assert.Equal(1, game.Events().Last(e => e.Event.Kind == "COST_PAID").Event.Payload["spellshieldTaxPower"]);
            Assert.Equal(7, game.State.CardObjects["UNIT"].Power);
            await game.Top(); Assert.Equal(-3, game.State.CardObjects["UNIT"].Power);
        }
        await game.Replay();
    }

    private static MatchState Position(string? victim = null)
    {
        var initial = VenUnitEntryBasicTests.Position(Card);
        if (victim is not null) initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ALLY", victim, "P1") } };
        return WithPool(initial, new(20, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 3 }));
    }

    private static MatchState WithPool(MatchState initial, RunePool pool)
        => initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };

    private static async Task Reject(MatchState state, string player, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state); var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var engine = new CoreRuleEngine();
        Assert.False(engine.PreviewPlayCard(state, player, PlayCostPreviewTests.Request(state, command)).CanPay);
        var result = await engine.ResolveAsync(state, new("invalid-fixed-sacrifice", player, command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
