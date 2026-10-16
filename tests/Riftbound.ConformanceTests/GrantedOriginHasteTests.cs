using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class GrantedOriginHasteTests
{
    private const string Source = "ATK_TOP";
    private const string Grant = "HASTE-GRANT";
    private const string BlueUnit = "OGN·096/298";

    [Theory]
    [InlineData("SFD·029/221", true)]
    [InlineData("SFD·029a/221", true)]
    [InlineData("SFD·029/221", false)]
    [InlineData("SFD·029a/221", false)]
    public async Task ARealDeckPlayOffersOptionalHasteAndPaysOnlyWhenSelected(string grantCard, bool ready)
    {
        var initial = Position(grantCard, BlueUnit);
        var driver = await Reveal(initial, "grant-" + ready + grantCard.Replace('/', '-'));
        var prompt = ResolutionResult.BuildPrompts(driver.State)["P1"];
        var play = prompt.Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
        Assert.Contains(play.OptionalCosts ?? [], c => c.Id == HasteOptionalCostNames.HasteReady);
        var command = new PlayCardCommand(Source, BlueUnit, [],
            OptionalCosts: ready ? [HasteOptionalCostNames.HasteReady] : []);
        var before = driver.State.RunePools["P1"];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(ready ? 3 : 2, quote.Cost!.Mana);
        var played = await driver.Act("P1", command);
        Assert.Equal(before.Mana - (ready ? 3 : 2), played.State.RunePools["P1"].Mana);
        Assert.Equal(before.PowerByTrait!.GetValueOrDefault(RuneTrait.Blue) - (ready ? 1 : 0),
            played.State.RunePools["P1"].PowerByTrait!.GetValueOrDefault(RuneTrait.Blue));
        Assert.Equal(!ready, played.State.CardObjects[Source].IsExhausted);
        var entered = Assert.Single(played.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Equal(ready, entered.Payload.TryGetValue("hasteReadyOptionalCostPaid", out var paid) && Equals(paid, true));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task HandPlayCannotBuyAnOutsideHandGrant(string grantCard)
    {
        var initial = Position(grantCard, BlueUnit);
        var own = initial.PlayerZones["P1"];
        initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = own with { MainDeck = own.MainDeck.Where(id => id != Source).ToArray(), Hand = [..own.Hand, Source] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { [Source] = new("P1", "HAND") }
        };
        await RejectHaste(initial);
    }

    [Theory]
    [InlineData("SFD·029/221", "enemy")]
    [InlineData("SFD·029a/221", "enemy")]
    [InlineData("SFD·029/221", "graveyard")]
    [InlineData("SFD·029a/221", "graveyard")]
    public async Task AnEnemyOrDepartedGrantCannotAuthorizeHaste(string grantCard, string branch)
    {
        var initial = Position(grantCard, BlueUnit);
        initial = LocalDestructionRecallTests.At(initial, Grant, branch == "enemy" ? "P2" : "P1",
            branch == "enemy" ? "BASE" : "GRAVEYARD");
        if (branch == "enemy") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [Grant] = initial.CardObjects[Grant] with { OwnerId = "P2", ControllerId = "P2" } } };
        else initial = initial with {
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with {
                    Base = initial.PlayerZones["P1"].Base.Where(id => id != Grant).ToArray(),
                    Battlefields = initial.PlayerZones["P1"].Battlefields.Where(id => id != Grant).ToArray(),
                    Graveyard = [..initial.PlayerZones["P1"].Graveyard, Grant] } } };
        var driver = await Reveal(initial, branch + grantCard.Replace('/', '-'));
        await RejectHaste(driver.State);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221", RuneTrait.Red)]
    [InlineData("SFD·029a/221", RuneTrait.Red)]
    [InlineData("SFD·029/221", RuneTrait.Purple)]
    [InlineData("SFD·029a/221", RuneTrait.Purple)]
    public async Task AGrantedMultitraitUnitSelectsItsHasteTraitAndPaysPrintedPowerAsWell(string grantCard, string trait)
    {
        const string tibbers = "OGS·018/024";
        var initial = Position(grantCard, tibbers);
        initial = WithPower(initial, trait, 3);
        var driver = await Reveal(initial, "two-traits-" + trait + grantCard.Replace('/', '-'));
        var choices = ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!
            .Single(c => c.Action == CommandTypes.PlayCard).OptionalCosts ?? [];
        var selected = HasteOptionalCostNames.HasteReady + ":" + trait;
        Assert.Contains(choices, c => c.Id == selected);
        Assert.DoesNotContain(choices, c => c.Id == HasteOptionalCostNames.HasteReady);
        var command = new PlayCardCommand(Source, tibbers, [], OptionalCosts: [selected]);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(9, quote.Cost!.Mana);
        var played = await driver.Act("P1", command);
        Assert.Equal(21, played.State.RunePools["P1"].Mana);
        Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.False(played.State.CardObjects[Source].IsExhausted);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task AGrantedMultitraitUnitDoesNotOfferHasteWhenOnlyItsPrintedPowerIsAffordable(string grantCard)
    {
        var initial = WithPower(Position(grantCard, "OGS·018/024"), RuneTrait.Red, 2);
        var driver = await Reveal(initial, "combined-power-" + grantCard.Replace('/', '-'));
        var choices = ResolutionResult.BuildPrompts(driver.State)["P1"].Candidates!
            .Single(c => c.Action == CommandTypes.PlayCard).OptionalCosts ?? [];
        Assert.DoesNotContain(choices, c => c.Id.StartsWith(HasteOptionalCostNames.HasteReady, StringComparison.Ordinal));
        var command = new PlayCardCommand(Source, "OGS·018/024", [], OptionalCosts: [HasteOptionalCostNames.HasteReady + ":" + RuneTrait.Red]);
        var before = MatchStateHasher.Hash(driver.State);
        var engine = new CoreRuleEngine();
        Assert.False(engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command)).CanPay);
        var rejected = await engine.ResolveAsync(driver.State, new("insufficient-combined", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task DestroyingTheGrantAsAMandatoryCostKeepsThePaidEntryReplacement(string grantCard)
    {
        const string aristocrat = "OGN·208/298";
        var initial = WithPower(Position(grantCard, aristocrat), RuneTrait.Yellow, 1);
        var driver = await Reveal(initial, "grant-paid-away-" + grantCard.Replace('/', '-'));
        var played = await driver.Act("P1", new PlayCardCommand(Source, aristocrat, [],
            OptionalCosts: ["DESTROY_FRIENDLY_UNIT:" + Grant, HasteOptionalCostNames.HasteReady]));
        Assert.Contains(Grant, played.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(Grant, played.State.PlayerZones["P1"].Base);
        Assert.Contains(Source, played.State.PlayerZones["P1"].Base);
        Assert.Equal(25, played.State.RunePools["P1"].Mana); Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.False(played.State.CardObjects[Source].IsExhausted);
        var entry = Assert.Single(played.Events, e => e.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Equal(true, entry.Payload["hasteReadyOptionalCostPaid"]);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static MatchState WithPower(MatchState state, string trait, int power)
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> { [trait] = power }) } };

    [Theory]
    [InlineData("SFD·029/221", "SFD·029a/221")]
    [InlineData("SFD·029a/221", "SFD·029/221")]
    public async Task TwoRealGrantSourcesStillChargeHasteOnlyOnce(string first, string second)
    {
        var initial = WithPower(Position(first, BlueUnit), RuneTrait.Blue, 1);
        initial = OfficialHoldSequenceTests.AddUnit(initial, "OTHER-GRANT", second, "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, "OTHER-GRANT", "P1", "BASE");
        var driver = await Reveal(initial, "multiple-grants-" + first.Replace('/', '-'));
        var played = await driver.Act("P1", new PlayCardCommand(Source, BlueUnit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        Assert.Equal(27, played.State.RunePools["P1"].Mana); Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.False(played.State.CardObjects[Source].IsExhausted);
        Assert.Single(played.Events, e => e.Kind == "COST_PAID");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("SFD·029/221")]
    [InlineData("SFD·029a/221")]
    public async Task AClientCannotInventAnOffTraitHastePaymentForAMultitraitUnit(string grantCard)
    {
        var initial = WithPower(Position(grantCard, "OGS·018/024"), RuneTrait.Blue, 20);
        var driver = await Reveal(initial, "invalid-trait-" + grantCard.Replace('/', '-'));
        var command = new PlayCardCommand(Source, "OGS·018/024", [], OptionalCosts: [HasteOptionalCostNames.HasteReady + ":" + RuneTrait.Blue]);
        var before = MatchStateHasher.Hash(driver.State); var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, command));
        Assert.False(quote.CanPay);
        var rejected = await engine.ResolveAsync(driver.State, new("invented-trait", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    private static async Task RejectHaste(MatchState state)
    {
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var play = prompt.Candidates!.Single(c => c.Action == CommandTypes.PlayCard);
        Assert.DoesNotContain(play.OptionalCosts ?? [], c => c.Id.StartsWith(HasteOptionalCostNames.HasteReady, StringComparison.Ordinal));
        var command = new PlayCardCommand(Source, BlueUnit, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]);
        var before = MatchStateHasher.Hash(state);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var rejected = await engine.ResolveAsync(state, new("no-grant-haste", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    internal static MatchState Position(string grantCard, string playedCard)
    {
        var state = DeckPlaySequenceTests.TopCard(AttackDeckPlayTests.Position(), Source, playedCard);
        state = OfficialHoldSequenceTests.AddUnit(state, Grant, grantCard, "F", "P1");
        state = LocalDestructionRecallTests.At(state, Grant, "P1", "BASE");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(30, 0, new Dictionary<string, int> {
                [RuneTrait.Red] = 20, [RuneTrait.Blue] = 20, [RuneTrait.Purple] = 20, [RuneTrait.Yellow] = 20 }) } };
    }

    internal static async Task<StandbyDefendObserverTests.Driver> Reveal(MatchState initial, string branch)
    {
        var driver = AttackDeckPlayTests.Driver(initial, "origin-haste-" + branch);
        await AttackDeckPlayTests.Attack(driver); await driver.Top(); await AttackDeckPlayTests.Reveal(driver);
        await DeckPlaySequenceTests.Select(driver, Source); Assert.NotNull(driver.State.PendingEffectPlay);
        return driver;
    }
}
