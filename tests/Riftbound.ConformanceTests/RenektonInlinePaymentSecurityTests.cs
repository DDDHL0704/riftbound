using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RenektonInlinePaymentSecurityTests
{
    private const string Ability = "LEGEND_REACTION_PAY_2_POWER_EXHAUST_GAIN_2_UNIT_MANA";
    private const string Resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:2";
    private const string Mana = "MANA_SOURCE:UNIT_PLAY_OR_UNIT_ABILITY_ONLY:2,UNRESTRICTED:2";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PromptExposesTheActionAndDoesNotOfferAnUnfundedUnit()
    {
        var funded = UnitPosition(new(2, 3));
        var prompt = ResolutionResult.BuildPrompts(funded)["P1"];
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var metadata = JsonSerializer.SerializeToElement(play.Metadata, Web);
        var unit = Assert.Single(metadata.GetProperty("sourceRequirements").EnumerateArray(),
            r => r.GetProperty("sourceObjectId").GetString() == "CARD");
        Assert.Equal(4, unit.GetProperty("availableManaWithPaymentResources").GetInt32());
        Assert.Contains(unit.GetProperty("paymentResourceChoices").EnumerateArray(),
            c => c.GetProperty("id").GetString() == Resource);
        var resource = unit.GetProperty("paymentResourcePowerByChoice").GetProperty(Resource);
        Assert.Equal(2, resource.GetProperty("powerCost").GetInt32());
        Assert.True(resource.GetProperty("unitPlayOrUnitAbilityOnly").GetBoolean());

        var unfunded = ResolutionResult.BuildPrompts(UnitPosition(new(2, 2)))["P1"];
        Assert.DoesNotContain(unfunded.Candidates ?? [], c => c.Action == CommandTypes.PlayCard
            && (c.Sources ?? []).Any(s => s.Id == "CARD"));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("short")]
    [InlineData("missing")]
    [InlineData("reused")]
    public async Task ForgedOrReusedLegendResourcesAreRejectedAtomically(string kind)
    {
        var initial = UnitPosition(new RunePool(2, 1, new Dictionary<string, int> { ["red"] = 1, ["purple"] = 1 }));
        var valid = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1,red:1";
        var actions = kind switch
        {
            "duplicate" => new[] { valid, valid, Mana },
            "short" => new[] { "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1", Mana },
            "missing" => new[] { "LEGEND_RESOURCE:MISSING:" + Ability + ":POWER_SPEND:A:1,red:1", Mana },
            _ => new[] { valid, Mana, "POWER_SPEND:red:1" }
        };
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: actions, Destination: "BASE");
        var before = MatchStateHasher.Hash(initial);
        Assert.False(new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)).CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("bad-inline-" + kind, "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.False(result.State.CardObjects["LEGEND"].IsExhausted);
    }

    [Fact]
    public async Task UnspentGeneratedManaRemainsRestrictedAndTamperedHistoryDoesNotReplay()
    {
        var initial = UnitPosition(new RunePool(4, 2, new Dictionary<string, int> { ["red"] = 1, ["purple"] = 1 }));
        var selected = "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:1,red:1";
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts:
            [selected, "MANA_SOURCE:UNRESTRICTED:4", "POWER_SPEND:purple:1"], Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(2, quote.Cost!.RemainingUnitPlayOrUnitAbilityOnlyMana);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        var commands = game.Commands();
        var original = Assert.Single(commands);
        var changed = JsonNode.Parse(original.RawCommand!.Value.GetRawText())!.AsObject();
        changed["optionalCosts"] = JsonSerializer.SerializeToNode(new[] {
            "LEGEND_RESOURCE:LEGEND:" + Ability + ":POWER_SPEND:A:2",
            "MANA_SOURCE:UNRESTRICTED:4", "POWER_SPEND:purple:1" }, Web);
        commands[0] = original with { RawCommand = JsonSerializer.SerializeToElement(changed, Web) };
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, commands, game.State,
            new CoreRuleEngine(), default, game.Events());
        Assert.False(replay.IsMatch); Assert.NotEmpty(replay.Errors);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task LegendResourceAcquiredDuringSpellPaymentCannotPayTheSpell(int ordinaryMana, bool succeeds)
    {
        var initial = WithLegend(RestrictedManaLifecycleTests.Position());
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(ordinaryMana, 2) } };
        var command = new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"], OptionalCosts: [Resource]);
        var before = MatchStateHasher.Hash(initial);
        var engine = new CoreRuleEngine();
        Assert.Equal(succeeds, engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)).CanPay);
        if (!succeeds)
        {
            var rejected = await engine.ResolveAsync(initial, new("no-spell-mana", "P1", command.CmdType), command, default);
            Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
            Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
            return;
        }
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecycledRunesCanFundTheLegendAndTheUnitInOnePayment(bool fundLegendFromRunes)
    {
        var initial = UnitPosition(new(2, fundLegendFromRunes ? 0 : 2));
        initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Purple, "PURPLE");
        var costs = new List<string> { "RECYCLE_RUNE:PURPLE", Mana, "POWER_SPEND:purple:1" };
        var legendSpend = "POWER_SPEND:A:2";
        if (fundLegendFromRunes)
        {
            initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Red, "RED-1");
            initial = VoluntaryPaymentResourceChoicesTests.WithRune(initial, RuneTrait.Red, "RED-2");
            costs.Add("RECYCLE_RUNE:RED-1"); costs.Add("RECYCLE_RUNE:RED-2");
            legendSpend = "POWER_SPEND:red:2";
        }
        var resource = "LEGEND_RESOURCE:LEGEND:" + Ability + ":" + legendSpend;
        costs.Add(resource);
        var command = new PlayCardCommand("CARD", "SFD·143/221", [], OptionalCosts: costs, Destination: "BASE");
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message);
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var metadata = JsonSerializer.SerializeToElement(play.Metadata, Web);
        var unit = Assert.Single(metadata.GetProperty("sourceRequirements").EnumerateArray(),
            r => r.GetProperty("sourceObjectId").GetString() == "CARD");
        Assert.Contains(unit.GetProperty("paymentResourceChoices").EnumerateArray(),
            c => c.GetProperty("id").GetString() == resource);
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["LEGEND"].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredMana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalStoredPower);
        await game.Replay();
    }

    private static MatchState UnitPosition(RunePool pool)
    {
        var state = WithLegend(RestrictedPowerPlayTests.Position("SFD·143/221", false));
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = pool } };
    }

    private static MatchState WithLegend(MatchState state)
    {
        state = OfficialHoldSequenceTests.AddLegend(state, "VEN·141");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["LEGEND"] = OfficialGarenConquestBattlefieldCountTests.Printed("LEGEND", "VEN·141", "P1") } };
    }
}
