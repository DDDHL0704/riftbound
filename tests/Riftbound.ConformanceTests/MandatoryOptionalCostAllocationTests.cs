using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN356.4.c/c.1/c.2: every applicable passive reduces each declared
// component. Choosing a zero resource dimension still applies its zero floor.
public sealed class MandatoryOptionalCostAllocationTests
{
    [Theory]
    [InlineData("SFD·149/221", 33242)]
    [InlineData("SFD·149a/221", 33243)]
    public async Task CurrentExactPrintUsesTheReviewedMandatoryErrata(string cardNo, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, card => card.CardNo == cardNo);
        Assert.Equal(id, card.Id);
        Assert.Equal("当你打出我时，弃置一张手牌，然后抽两张牌。\n你支付的可选额外费用，其费用减少{{1}}或{{A}}。", card.CardEffect);
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task PromptRequiresOneAllocationOnlyWhenItsAdditionalFeeIsDeclared(string reducer)
    {
        var initial = Position(reducer);
        var play = Assert.Single(ResolutionResult.BuildPrompts(initial)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirement = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            play.Metadata!["sourceRequirements"]));
        var group = Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
            requirement["optionalCostReductionGroups"]));
        Assert.Equal(1, group["minSelections"]);
        Assert.Equal(1, group["maxSelections"]);
        Assert.Equal([HasteOptionalCostNames.HasteReady], Assert.Single(
            Assert.IsAssignableFrom<IEnumerable<IReadOnlyList<string>>>(group["requiresOneOfOptionalCostSelections"])));
        var choices = Assert.IsAssignableFrom<IEnumerable<string>>(group["choiceIds"]);
        Assert.Contains(Reduction("EZREAL", "ADDITIONAL", "MANA"), choices);
        Assert.Contains(Reduction("EZREAL", "ADDITIONAL", "POWER-red"), choices);
        Assert.DoesNotContain(Reduction("EZREAL", "ADDITIONAL", "POWER-any"), choices);
        var ordinary = new PlayCardCommand("CARD", "OGN·010/298", []);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, ordinary));
        Assert.True(quote.CanPay, quote.Message);
        var played = await new CoreRuleEngine().ResolveAsync(initial, new("no-additional-fee", "P1", ordinary.CmdType), ordinary, default);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(2, Assert.Single(played.Events, e => e.Kind == "COST_PAID").Payload["mana"]);
        Retain(reducer + "-prompt", new { initial, play, ordinary, quote, played });
    }

    [Theory]
    [InlineData("SFD·149/221", "SFD·066/221", "POWER-any", 4, 0)]
    [InlineData("SFD·149a/221", "SFD·066/221", "POWER-any", 4, 0)]
    [InlineData("SFD·149/221", "OGN·044/298", "MANA", 2, 1)]
    [InlineData("SFD·149a/221", "OGN·044/298", "MANA", 2, 1)]
    public async Task ExplicitZeroDimensionReductionKeepsTheFeeAndItsPaidEffect(
        string reducer, string card, string mode, int expectedMana, int expectedPower)
    {
        var initial = Position(reducer, card);
        var fee = card == "SFD·066/221" ? "ECHO" : "SPEND_POWER:green:1";
        var reduction = Reduction("EZREAL", card == "SFD·066/221" ? "ECHO" : "ADDITIONAL", mode);
        var command = new PlayCardCommand("CARD", card, card == "SFD·066/221" ? ["TARGET"] : [], OptionalCosts: [fee, reduction]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        var played = await engine.ResolveAsync(initial, new("zero-dimension", "P1", command.CmdType), command, default);
        Retain(reducer + "-" + card + "-" + mode, new { initial, command, quote, played });
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(expectedMana, quote.Cost!.Mana);
        Assert.Equal(expectedPower, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        Assert.True(played.Accepted, played.ErrorMessage);
        var payment = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(expectedMana, payment.Payload["mana"]);
        Assert.Equal(expectedPower, payment.Payload["power"]);
        var receipt = Assert.Single(JsonSerializer.SerializeToElement(payment.Payload["optionalCostReductions"]).EnumerateArray());
        Assert.Equal(reduction, receipt.GetProperty("choiceId").GetString());
        Assert.Equal(0, receipt.GetProperty("manaReduction").GetInt32());
        Assert.Equal(0, receipt.GetProperty("powerReduction").GetInt32());
        if (card == "SFD·066/221") Assert.Equal(2, Assert.Single(played.State.StackItems).EffectRepeatCount);
        else
        {
            var resolved = await PermanentConfirmationAssert.ResolveAfterPlayAsync(engine, played);
            Assert.Contains("REFILL", resolved.State.PlayerZones["P1"].Hand);
        }
    }

    [Theory]
    [InlineData("SFD·149/221")]
    [InlineData("SFD·149a/221")]
    public async Task AnEmptyGenericAxisCannotBypassAnExistingTypedPowerFee(string reducer)
    {
        var initial = Position(reducer);
        await AssertRejected(initial, new("CARD", "OGN·010/298", [], OptionalCosts:
            [HasteOptionalCostNames.HasteReady, Reduction("EZREAL", "ADDITIONAL", "POWER-any")]), reducer + "-fake-generic");
    }

    [Theory]
    [InlineData("first-only", false, 0, 0)]
    [InlineData("second-only", false, 0, 0)]
    [InlineData("same-axis", true, 2, 1)]
    [InlineData("both-axes", true, 2, 0)]
    public async Task EveryCurrentSourceNeedsItsOwnAllocationAndCannotSpillIntoBaseCost(
        string branch, bool accepted, int mana, int power)
    {
        var initial = Position("SFD·149/221");
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SECOND"] = Printed("SECOND", "SFD·149a/221", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["SECOND"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = ["EZREAL", "SECOND"] } } };
        var choices = new List<string> { HasteOptionalCostNames.HasteReady };
        if (branch != "second-only") choices.Add(Reduction("EZREAL", "ADDITIONAL", "MANA"));
        if (branch != "first-only") choices.Add(Reduction("SECOND", "ADDITIONAL", branch == "both-axes" ? "POWER-red" : "MANA"));
        var command = new PlayCardCommand("CARD", "OGN·010/298", [], OptionalCosts: choices);
        if (!accepted) { await AssertRejected(initial, command, branch); return; }
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        var played = await engine.ResolveAsync(initial, new(branch, "P1", command.CmdType), command, default);
        Retain(branch, new { initial, command, quote, played });
        Assert.True(quote.CanPay, quote.Message);
        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        var payment = Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(mana, payment.Payload["mana"]);
        Assert.Equal(power, payment.Payload["power"]);
        Assert.Equal(2, JsonSerializer.SerializeToElement(payment.Payload["optionalCostReductions"]).GetArrayLength());
    }

    [Theory]
    [InlineData("ECHO")]
    [InlineData("ECHO:GRANTED:1")]
    public async Task OneAllocationCannotStandInForTwoIndependentDeclaredComponents(string missing)
    {
        var initial = Position("SFD·149/221", "SFD·066/221") with {
            UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        var other = missing == "ECHO" ? "ECHO:GRANTED:1" : "ECHO";
        await AssertRejected(initial, new("CARD", "SFD·066/221", ["TARGET"], OptionalCosts:
            ["ECHO", "ECHO:GRANTED:1", Reduction("EZREAL", other, "MANA")]), "missing-" + missing);
    }

    private static async Task AssertRejected(MatchState initial, PlayCardCommand command, string name)
    {
        var before = MatchStateHasher.Hash(initial);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        var played = await engine.ResolveAsync(initial, new(name, "P1", command.CmdType), command, default);
        Retain(name, new { initial, command, quote, played });
        Assert.False(quote.IsValid);
        Assert.False(quote.CanPay);
        Assert.False(played.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, played.ErrorCode);
        Assert.Empty(played.Events);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        Assert.Equal(before, MatchStateHasher.Hash(played.State));
    }

    private static CardObjectState Printed(string id, string card, string player)
        => OfficialGarenConquestBattlefieldCountTests.Printed(id, card, player);

    private static MatchState Position(string reducer, string card = "OGN·010/298")
        => new("MANDATORY-COMPONENT-ALLOCATION", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" }, status: MatchStatuses.InProgress,
            phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(10, 0, new Dictionary<string, int> { [RuneTrait.Red] = 2, [RuneTrait.Green] = 2 }), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Hand = ["CARD"], Base = ["EZREAL"], MainDeck = ["REFILL"] },
                ["P2"] = PlayerZones.Empty with { Base = ["TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["CARD"] = Printed("CARD", card, "P1"), ["EZREAL"] = Printed("EZREAL", reducer, "P1"),
                ["REFILL"] = Printed("REFILL", "OGN·003/298", "P1"), ["TARGET"] = Printed("TARGET", "OGN·003/298", "P2") },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["CARD"] = new("P1", "HAND"), ["EZREAL"] = new("P1", "BASE"),
                ["REFILL"] = new("P1", "MAIN_DECK"), ["TARGET"] = new("P2", "BASE") });

    private static string Reduction(string source, string component, string mode)
        => "OPTIONAL_COST_REDUCTION:" + source + ":0:" + Uri.EscapeDataString(component) + ":" + mode;

    private static void Retain(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_MANDATORY_DISCOUNT_EVIDENCE") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "allocation-" + name.Replace('·', '-').Replace('/', '-').Replace(':', '-') + ".json"),
            JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
