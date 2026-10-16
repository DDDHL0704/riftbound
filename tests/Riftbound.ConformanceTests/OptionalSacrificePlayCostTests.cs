using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OptionalSacrificePlayCostTests
{
    private const string Atakhan = "UNL-170/219";
    private const string Victim = "SACRIFICE";
    private const string SacrificeCost = "DESTROY_FRIENDLY_UNIT:" + Victim;

    [Theory]
    [InlineData(null, 10, 3)]
    [InlineData("SFD·029/221", 7, 3)]
    [InlineData("OGN·231/298", 4, 0)]
    [InlineData("UNL-118/219", 0, 0)]
    [InlineData("UNL-170/219", 0, 0)]
    public async Task OptionalSacrificeQuoteAndPaymentUseTheVictimsPrintedCosts(string? victimCard, int mana, int power)
    {
        var state = Position(victimCard);
        var command = new PlayCardCommand("CARD", Atakhan, [],
            OptionalCosts: victimCard is null ? [] : [SacrificeCost]);
        var before = MatchStateHasher.Hash(state);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(mana, quote.Cost!.Mana);
        Assert.Equal(power, quote.Cost.PowerByTrait.GetValueOrDefault(RuneTrait.Yellow));
        Assert.Equal(before, MatchStateHasher.Hash(state));
        var result = await engine.ResolveAsync(state, new("optional-sacrifice", "P1", CommandTypes.PlayCard), command, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(30 - mana, result.State.RunePools["P1"].Mana);
        Assert.Equal(10 - power, result.State.RunePools["P1"].TotalPower);
        if (victimCard is not null) Assert.Contains(Victim, result.State.PlayerZones["P1"].Graveyard);
        Assert.Contains("CARD", result.State.PlayerZones["P1"].Base);
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(mana, paid.Payload["mana"]);
        Assert.Equal(power, paid.Payload["power"]);
        Assert.Equal(10, CardBehaviorRegistry.GetAll().First(b => b.CardNo == Atakhan).ManaCost);
        Assert.Equal(3, PrintedPowerCostRules.ForCard(Atakhan).Amount);
    }

    [Fact]
    public void ACurrentlyAffordableSacrificeMakesTheHandPlayAndVictimChoiceAvailable()
    {
        var state = Position("SFD·029/221", 7, 3);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var play = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(play.OptionalCosts ?? [], c => c.Id == SacrificeCost);
    }

    [Theory]
    [InlineData("enemy")]
    [InlineData("no-controller")]
    [InlineData("face-down")]
    [InlineData("equipment")]
    [InlineData("graveyard")]
    [InlineData("stale-location")]
    [InlineData("unknown")]
    [InlineData("source-in-hand")]
    [InlineData("duplicate")]
    [InlineData("two-units")]
    public async Task InvalidSacrificesCannotQuotePayOrMutateTheMatch(string branch)
    {
        var state = Position("SFD·029/221");
        var card = state.CardObjects[Victim];
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [Victim] = branch switch {
                "enemy" => card with { ControllerId = "P2" },
                "no-controller" => card with { ControllerId = null },
                "face-down" => card with { IsFaceDown = true },
                "equipment" => card with { Tags = [CardObjectTags.EquipmentCard] },
                _ => card
            } } };
        if (branch is "graveyard" or "stale-location")
            state = LocalDestructionRecallTests.At(state, Victim, "P1", "GRAVEYARD");
        if (branch == "graveyard") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [], Graveyard = [Victim] } } };
        IReadOnlyList<string> costs = branch switch {
            "unknown" => ["DESTROY_FRIENDLY_UNIT:MISSING"],
            "source-in-hand" => ["DESTROY_FRIENDLY_UNIT:CARD"],
            "duplicate" => [SacrificeCost, SacrificeCost],
            "two-units" => [SacrificeCost, "DESTROY_FRIENDLY_UNIT:OTHER"],
            _ => [SacrificeCost]
        };
        if (branch == "two-units") state = AddVictim(state, "OTHER", "OGN·231/298");
        var command = new PlayCardCommand("CARD", Atakhan, [], OptionalCosts: costs);
        var before = MatchStateHasher.Hash(state);
        var engine = new CoreRuleEngine();
        Assert.False(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        var rejected = await engine.ResolveAsync(state, new("bad-sacrifice-" + branch, "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(state));
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Fact]
    public void ThePromptCannotCombineDifferentVictimsManaAndPowerReductions()
    {
        // Mountain Drake gives -9 mana/-0 power; Ledros gives -6 mana/-4 power.
        // At 1 mana/0 power neither can pay the same complete play.
        var state = AddVictim(Position("OGN·142/298", 1, 0), "OTHER", "OGN·231/298");
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        Assert.DoesNotContain(prompt.Candidates!.Where(c => c.Action == CommandTypes.PlayCard)
            .SelectMany(c => c.Sources ?? []), choice => choice.Id == "CARD");
        var engine = new CoreRuleEngine();
        foreach (var cost in new[] { SacrificeCost, "DESTROY_FRIENDLY_UNIT:OTHER" })
        {
            var command = new PlayCardCommand("CARD", Atakhan, [], OptionalCosts: [cost]);
            Assert.False(engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command)).CanPay);
        }
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task SacrificingTheOutsideHandHasteGrantRetainsThePaidEntry(string grantCard)
    {
        var initial = GrantedOriginHasteTests.Position(grantCard, Atakhan);
        initial = WithPool(initial, 8, 4);
        var driver = await GrantedOriginHasteTests.Reveal(initial, "optional-sacrifice-" + grantCard.Replace('/', '-'));
        var choices = ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!
            .Single(c => c.Action == CommandTypes.PlayCard).OptionalCosts ?? [];
        Assert.Contains(choices, c => c.Id == HasteOptionalCostNames.HasteReady);
        var command = new PlayCardCommand("ATK_TOP", Atakhan, [],
            OptionalCosts: ["DESTROY_FRIENDLY_UNIT:HASTE-GRANT", HasteOptionalCostNames.HasteReady]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(8, quote.Cost!.Mana); Assert.Equal(4, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        var result = await driver.Act("P1", command);
        Assert.Equal(0, result.State.RunePools["P1"].Mana); Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        Assert.Contains("HASTE-GRANT", result.State.PlayerZones["P1"].Graveyard);
        Assert.False(result.State.CardObjects["ATK_TOP"].IsExhausted);
        Assert.Equal(true, Assert.Single(result.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE").Payload["hasteReadyOptionalCostPaid"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task YellowReductionCanPayAllHastePowerAndTheSelectedHasteStillReadiesTheUnit()
    {
        var initial = WithPool(GrantedOriginHasteTests.Position("SFD·029/221", Atakhan), 5, 0);
        initial = AddVictim(initial, Victim, "OGN·231/298");
        var driver = await GrantedOriginHasteTests.Reveal(initial, "haste-reduced-to-zero");
        var choices = ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!
            .Single(c => c.Action == CommandTypes.PlayCard).OptionalCosts ?? [];
        Assert.Contains(choices, c => c.Id == HasteOptionalCostNames.HasteReady);
        var command = new PlayCardCommand("ATK_TOP", Atakhan, [], OptionalCosts: [SacrificeCost, HasteOptionalCostNames.HasteReady]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(5, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.PowerByTrait.Values.Sum());
        Assert.Contains(quote.Cost.Adjustments, a => a.Power == -4);
        var result = await driver.Act("P1", command);
        Assert.False(result.State.CardObjects["ATK_TOP"].IsExhausted);
        Assert.Equal(true, Assert.Single(result.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE").Payload["hasteReadyOptionalCostPaid"]);
        Assert.Equal(0, result.State.RunePools["P1"].Mana); Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task ARealCopiedImageUsesItsCopiedPrintedCostsAndCeasesAfterSacrifice()
    {
        var copied = await OfficialCopyIdentityTests.Copy(OfficialCopyIdentityTests.Position("OGN·231/298"));
        var state = OfficialCopyIdentityTests.AddSpell(copied.State, "CARD", Atakhan);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["CARD"] = state.CardObjects["CARD"] with { Tags = [CardObjectTags.UnitCard] } } };
        state = WithPool(state, 4, 0);
        var command = new PlayCardCommand("CARD", Atakhan, [], OptionalCosts: ["DESTROY_FRIENDLY_UNIT:C-TOKEN-001"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(4, quote.Cost!.Mana); Assert.Empty(quote.Cost.PowerByTrait);
        var played = await OfficialGraveyardRecastTests.Act(state, "P1", command);
        Assert.False(played.State.CardObjects.ContainsKey("C-TOKEN-001"));
        Assert.DoesNotContain("C-TOKEN-001", played.State.PlayerZones["P1"].Graveyard);
        Assert.Single(played.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
        OfficialGraveyardRecastTests.Restore(played.State);
    }

    private static MatchState WithPool(MatchState state, int mana, int power)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(mana, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = power }) } };

    private static MatchState AddVictim(MatchState state, string id, string card)
        => state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = new(id, cardNo: card, ownerId: "P1", controllerId: "P1", power: 30, tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, id] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "BASE") }
        };

    internal static MatchState Position(string? victimCard = null, int mana = 30, int power = 10)
    {
        var state = PlayCostPreviewTests.Position(new(mana, 0,
            new Dictionary<string, int> { [RuneTrait.Yellow] = power }), Atakhan);
        if (victimCard is null) return state;
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [Victim] = new(Victim, cardNo: victimCard, ownerId: "P1", controllerId: "P1", power: 30,
                    tags: [CardObjectTags.UnitCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [Victim] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                [Victim] = new("P1", "BASE") }
        };
    }
}
