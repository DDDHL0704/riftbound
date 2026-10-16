using Riftbound.Contracts;
using Riftbound.Engine;
using System.Text.Json;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GrantedTokenHasteTests
{
    private const string Spell = "SFD·031/221";
    private const string Source = "TOKEN-SPELL";
    private const string Grant = "TOKEN-HASTE-GRANT";

    [Theory]
    [InlineData("SFD·029/221", RuneTrait.Blue)]
    [InlineData("SFD·029a/221", RuneTrait.Blue)]
    [InlineData("SFD·029/221", RuneTrait.Red)]
    [InlineData("SFD·029a/221", RuneTrait.Red)]
    [InlineData("SFD·029/221", "")]
    [InlineData("SFD·029a/221", "")]
    public async Task ARealSandcraftOffersHasteOnTheColorlessTokenAndSpendsAnyTraitOnlyAtTokenPlay(string grantCard, string trait)
    {
        var initial = Position(grantCard, trait);
        var driver = Driver(initial, "pay-" + grantCard.Replace('/', '-') + "-" + (trait == "" ? "ANY" : trait));
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var spell = prompt.Candidates!.Single(c => c.Action == CommandTypes.PlayCard && (c.Sources ?? []).Any(s => s.Id == Source));
        Assert.DoesNotContain(spell.OptionalCosts ?? [], c => c.Id.StartsWith(HasteOptionalCostNames.HasteReady, StringComparison.Ordinal)
            && (c.ObjectIds ?? []).Contains(Source, StringComparer.Ordinal));
        await driver.Act("P1", new PlayCardCommand(Source, Spell, []));
        Assert.Equal(8, driver.State.RunePools["P1"].Mana);
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        var resolving = await driver.Top();
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        Assert.Equal("TOKEN_HASTE_PAYMENT", pending.Request.Kind);
        Assert.Equal("P1", pending.Request.PlayerId);
        var offered = resolving.Prompts["P1"].Candidates!.Single(c => c.Action == CommandTypes.PayCost);
        var metadata = JsonSerializer.SerializeToElement(offered.Metadata);
        Assert.Equal(1, metadata.GetProperty("cost").GetProperty("mana").GetInt32());
        Assert.Equal(1, metadata.GetProperty("cost").GetProperty("power").GetInt32());
        Assert.Equal("指示物急速支付", offered.Label);
        Assert.DoesNotContain(resolving.Events, e => e.Kind == "UNIT_TOKEN_CREATED");
        Assert.DoesNotContain(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        var payment = trait == "" ? "PAY:ANY" : "PAY:TRAIT:" + trait;
        Assert.Contains(pending.Request.Options, o => o.Id == payment);
        await driver.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [payment]));
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.Equal("SFD·T02", token.CardNo);
        Assert.False(token.IsExhausted);
        Assert.Equal(7, driver.State.RunePools["P1"].Mana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task ADepartedGrantDoesNotOpenTokenHaste(string grantCard)
    {
        var initial = Position(grantCard, RuneTrait.Blue);
        initial = LocalDestructionRecallTests.At(initial, Grant, "P1", "GRAVEYARD");
        initial = initial with { PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
            ["P1"] = initial.PlayerZones["P1"] with {
                Base = initial.PlayerZones["P1"].Base.Where(id => id != Grant).ToArray(),
                Battlefields = initial.PlayerZones["P1"].Battlefields.Where(id => id != Grant).ToArray(),
                Graveyard = [..initial.PlayerZones["P1"].Graveyard, Grant] } } };
        var driver = Driver(initial, "departed-" + grantCard.Replace('/', '-'));
        await driver.Act("P1", new PlayCardCommand(Source, Spell, []));
        await driver.Top();
        Assert.Null(driver.State.PendingRuleChoice);
        var token = Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.True(token.IsExhausted);
        Assert.Equal(8, driver.State.RunePools["P1"].Mana);
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task EchoTokensHaveIndependentPreEntryHasteDecisions(bool firstReady, bool secondReady)
    {
        var initial = Position("SFD·029/221");
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(10, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 2 }) } };
        var driver = Driver(initial, $"echo-{firstReady}-{secondReady}");
        await driver.Act("P1", new PlayCardCommand(Source, Spell, [], OptionalCosts: ["ECHO"]));
        Assert.Equal(6, driver.State.RunePools["P1"].Mana);
        await driver.Top();
        await Choose(driver, firstReady ? "PAY:TRAIT:" + RuneTrait.Blue : "DECLINE");
        var preview = driver.State.PendingRuleChoice!.Request.Preview!;
        var first = Assert.Single(preview.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier));
        Assert.Contains(first.ObjectId, preview.PlayerZones["P1"].Base);
        Assert.Equal(!firstReady, first.IsExhausted);
        await Choose(driver, secondReady ? "PAY:TRAIT:" + RuneTrait.Blue : "DECLINE");
        var tokens = driver.State.CardObjects.Values.Where(c => c.Tags.Contains(CardObjectTags.SandSoldier)).OrderBy(c => c.ObjectId).ToArray();
        Assert.Equal(2, tokens.Length);
        Assert.Equal(!firstReady, tokens[0].IsExhausted);
        Assert.Equal(!secondReady, tokens[1].IsExhausted);
        var payments = (firstReady ? 1 : 0) + (secondReady ? 1 : 0);
        Assert.Equal(6 - payments, driver.State.RunePools["P1"].Mana);
        Assert.Equal(2 - payments, driver.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, driver.Events().Count(e => e.Event.Kind == "UNIT_TOKEN_CREATED"));
        Assert.Equal(payments, driver.Events().Count(e => e.Event.Kind == "COST_PAID" && Equals(e.Event.Payload.GetValueOrDefault("reason"), "TOKEN_HASTE_PAYMENT")));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualRuneResourcesCanBeUsedWhilePayingWithoutResurrectingTheRecycledRune(bool exhausted)
    {
        var initial = Position("SFD·029/221");
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(exhausted ? 3 : 2, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["TOKEN-RUNE"] = new("TOKEN-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                    isExhausted: exhausted, tags: [CardObjectTags.RuneCard, "COLOR:red"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "TOKEN-RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["TOKEN-RUNE"] = new("P1", "BASE") }
        };
        var driver = Driver(initial, "rune-" + exhausted);
        await driver.Act("P1", new PlayCardCommand(Source, Spell, []));
        await driver.Top();
        Assert.DoesNotContain(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("PAY:"));
        if (!exhausted)
        {
            await Choose(driver, "TAP_RUNE:TOKEN-RUNE");
            Assert.Equal(1, driver.State.PendingRuleChoice!.Request.Preview!.RunePools["P1"].Mana);
        }
        else Assert.DoesNotContain(driver.State.PendingRuleChoice.Request.Options, o => o.Id == "TAP_RUNE:TOKEN-RUNE");
        await Choose(driver, "RECYCLE_RUNE:TOKEN-RUNE");
        Assert.Contains("TOKEN-RUNE", driver.State.PendingRuleChoice!.Request.Preview!.PlayerZones["P1"].RuneDeck);
        await Choose(driver, "PAY:TRAIT:" + RuneTrait.Red);
        Assert.Equal(0, driver.State.RunePools["P1"].Mana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        Assert.Contains("TOKEN-RUNE", driver.State.PlayerZones["P1"].RuneDeck);
        Assert.DoesNotContain("TOKEN-RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.Equal("RUNE_DECK", driver.State.ObjectLocations["TOKEN-RUNE"].Zone);
        Assert.False(Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.Equal(exhausted ? 0 : 1, driver.Events().Count(e => e.Event.Kind == "RUNE_TAPPED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("player")]
    [InlineData("id")]
    [InlineData("window")]
    [InlineData("option")]
    public async Task ATokenHasteDecisionCannotBeStolenOrForged(string mutation)
    {
        var initial = Position("SFD·029/221");
        var driver = Driver(initial, "forged-" + mutation);
        await driver.Act("P1", new PlayCardCommand(Source, Spell, [])); await driver.Top();
        var pending = driver.State.PendingRuleChoice!;
        var before = MatchStateHasher.Hash(driver.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(driver.State,
            new("forged-token-haste", mutation == "player" ? "P2" : "P1", CommandTypes.PayCost),
            new PayCostCommand(mutation == "id" ? "OTHER" : pending.Request.Id,
                mutation == "window" ? "PLAY_CARD" : "RULE_REPLACEMENT",
                [mutation == "option" ? "PAY:FORGED_FREE" : "PAY:TRAIT:" + RuneTrait.Blue]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await Choose(driver, "DECLINE");
        Assert.True(Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        Assert.Equal(8, driver.State.RunePools["P1"].Mana);
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task ARealResponseReturningTheGrantPreventsHasteAtTheLaterTokenPlay(string grantCard)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(grantCard), "RETURN-GRANT", "OGN·104/298", "P1");
        var driver = Driver(initial, "return-response-" + grantCard.Replace('/', '-'));
        await driver.Act("P1", new PlayCardCommand(Source, Spell, []));
        await driver.Act("P1", new PlayCardCommand("RETURN-GRANT", "OGN·104/298", [Grant]));
        await driver.Top();
        Assert.Contains(Grant, driver.State.PlayerZones["P1"].Hand);
        await driver.Top();
        Assert.Null(driver.State.PendingRuleChoice);
        Assert.True(Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        Assert.Equal(7, driver.State.RunePools["P1"].Mana); // Sandcraft2 + actual return spell1.
        Assert.Equal(1, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task AParentSpellCannotBuyItsFutureTokensHaste(string grantCard)
    {
        var initial = Position(grantCard);
        var command = new PlayCardCommand(Source, Spell, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]);
        var engine = new CoreRuleEngine();
        var before = MatchStateHasher.Hash(initial);
        Assert.False(engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command)).CanPay);
        var rejected = await engine.ResolveAsync(initial, new("forged-parent-haste", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task ALegendTokenUsesTheSamePreEntryWindowAndSeparateCost(string grantCard)
    {
        var initial = OfficialLegendTokenTests.Position("SFD·197/221", 0) with { Tick = 0 };
        initial = OfficialHoldSequenceTests.AddUnit(initial, Grant, grantCard, "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, Grant, "P1", "BASE");
        var driver = Driver(initial, "legend-" + grantCard.Replace('/', '-'));
        await driver.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.AzirLegendAbilityId, [], ["SPEND_MANA:1"]));
        Assert.Equal(29, driver.State.RunePools["P1"].Mana);
        await driver.Top();
        Assert.Equal("TOKEN_HASTE_PAYMENT", driver.State.PendingRuleChoice!.Request.Kind);
        await Choose(driver, "PAY:ANY");
        Assert.False(Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        Assert.Equal(28, driver.State.RunePools["P1"].Mana);
        Assert.Equal(29, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("hidden")]
    public async Task GrantEligibilityUsesActualVisibleControlRatherThanOwnership(string branch)
    {
        var initial = Position("SFD·029/221");
        var grant = initial.CardObjects[Grant];
        if (branch == "owner") initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [Grant] = grant with { OwnerId = "P2" } } };
        else if (branch == "controller") initial = LocalDestructionRecallTests.At(initial, Grant, "P2", "BASE");
        else initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            [Grant] = grant with { IsFaceDown = true } } };
        var driver = Driver(initial, "grant-control-" + branch);
        await driver.Act("P1", new PlayCardCommand(Source, Spell, [])); await driver.Top();
        if (branch == "owner")
        {
            Assert.NotNull(driver.State.PendingRuleChoice);
            await Choose(driver, "PAY:TRAIT:" + RuneTrait.Blue);
        }
        else Assert.Null(driver.State.PendingRuleChoice);
        Assert.Equal(branch != "owner", Assert.Single(driver.State.CardObjects.Values, c => c.Tags.Contains(CardObjectTags.SandSoldier)).IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("mana")]
    [InlineData("option")]
    [InlineData("preview")]
    public async Task RecoveryRejectsForgedTokenPaymentRequestsRatherThanTrustingThePresentedChoices(string mutation)
    {
        var initial = Position("SFD·029/221");
        var driver = Driver(initial, "recovery-" + mutation);
        await driver.Act("P1", new PlayCardCommand(Source, Spell, [])); await driver.Top();
        var pending = driver.State.PendingRuleChoice!;
        var request = pending.Request;
        request = mutation switch {
            "amount" => request with { PowerCost = 0 },
            "mana" => request with { ManaCost = 0 },
            "option" => request with { Options = request.Options.Append(new("PAY:FORGED_FREE", "free")).ToArray() },
            _ => request with { Preview = request.Preview! with { RunePools = new Dictionary<string, RunePool>(request.Preview.RunePools) { ["P1"] = new(99, 99) } } }
        };
        var forged = driver.State with { PendingRuleChoice = pending with { Request = request } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
        await Choose(driver, "PAY:TRAIT:" + RuneTrait.Blue);
        Assert.Equal(7, driver.State.RunePools["P1"].Mana);
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static Task<ResolutionResult> Choose(StandbyDefendObserverTests.Driver driver, string option)
    {
        var pending = Assert.IsType<RuleChoiceContinuation>(driver.State.PendingRuleChoice);
        return driver.Act(pending.Request.PlayerId, new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option]));
    }

    internal static MatchState Position(string grantCard, string trait = RuneTrait.Blue)
    {
        var initial = AttackDamageDistributionTests.Spell(TurnUnitPlayGrantsTests.Position(TurnUnitPlayGrantsTests.Rally), Source, Spell, "P1");
        initial = OfficialHoldSequenceTests.AddUnit(initial, Grant, grantCard, "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, Grant, "P1", "BASE");
        return initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = trait == "" ? new(10, 1) : new(10, 0, new Dictionary<string, int> { [trait] = 1 }) } };
    }

    private static StandbyDefendObserverTests.Driver Driver(MatchState initial, string branch)
    {
        var root = Environment.GetEnvironmentVariable("RIFTBOUND_TOKEN_HASTE_EVIDENCE");
        return new(initial, string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, branch));
    }
}
