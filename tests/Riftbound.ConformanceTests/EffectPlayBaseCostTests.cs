using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class EffectPlayBaseCostTests
{
    private const string HasteUnit = "UNL-006/219";
    private static readonly string[] Sources = ["OGN·025/298", "RAD·018/167", "RAD·115/167", "RAD·135/167", "RAD·136/167", "RAD·140/167"];
    public static IEnumerable<object[]> Cards() => Sources.Select(card => new object[] { card });
    public static IEnumerable<object[]> HasteCases() => Sources.SelectMany(card => new[] { false, true }
        .Select(paid => new object[] { card, paid }));

    private static async Task Pick(Driver game, string id)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        await game.Act(choice.PlayerId, new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [id]));
    }

    private static async Task<(Driver Game, string Id)> Open(string source, string unit = HasteUnit)
    {
        var initial = source switch {
            "RAD·018/167" or "OGN·025/298" => RadOpponentDeckPlayUnitTests.TopCard(RadOpponentDeckPlayUnitTests.Position(source), "P2", unit),
            "RAD·115/167" => VenConditionalPlayCostTests.Place(TranspositionMageActivatedAbilityTests.Position(targetPower: 9),
                "PLAY", unit, "P1", "HAND"),
            "RAD·135/167" => VenConditionalPlayCostTests.Place(VenUnitEntryBasicTests.Position(source), "PLAY", unit, "P1", "GRAVEYARD"),
            "RAD·136/167" => RadDocksideCageTests.Position(),
            _ => VenConditionalPlayCostTests.Place(VenUnitEntryBasicTests.Position(source), "ALLY", unit, "P1", "BASE") };
        if (source == "RAD·136/167") initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["PRISONER"] = OfficialGarenConquestBattlefieldCountTests.Printed("PRISONER", unit, "P1") } };
        initial = VenConditionalPlayCostTests.Place(initial, "SACRIFICE", "OGN·096/298", "P1", "BASE");
        var pool = source switch {
            "OGN·025/298" => new RunePool(4, 2), "RAD·018/167" => new RunePool(9, 2), "RAD·135/167" => new RunePool(8, 2),
            "RAD·136/167" => new RunePool(6, 2), "RAD·140/167" => new RunePool(2, 1),
            _ => new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Purple] = 1 }) };
        // Public opening rune; its real tap and recycle will pay the child fee.
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["FEE-RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("FEE-RUNE", "OGN·007/298", "P1") },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = [..initial.PlayerZones["P1"].Base, "FEE-RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) { ["FEE-RUNE"] = new("P1", "BASE") } };
        var game = new Driver(initial);
        string id;
        switch (source)
        {
            case "OGN·025/298":
            case "RAD·018/167":
                await game.Act("P1", new PlayCardCommand("UNIT", source, [])); await game.Top();
                await Pick(game, "P2-TOP"); id = "P2-TOP"; break;
            case "RAD·115/167":
                await game.Act("P1", new ActivateAbilityCommand("MAGE", P4ActivatedAbilityCatalog.TranspositionMageAbilityId, ["TARGET"]));
                await game.Top(); id = "PLAY"; break;
            case "RAD·135/167":
                await game.Act("P1", new PlayCardCommand("UNIT", source, ["PLAY"])); await game.Top(); id = "PLAY"; break;
            case "RAD·136/167":
                await game.Act("P1", new PlayCardCommand("CARD", source, [], Destination: "BATTLEFIELD:BF"));
                await Pick(game, "PRISONER"); await game.Top();
                await RadDocksideCageTests.Hold(game); await Pick(game, "CARD"); await game.Top(); id = "PRISONER"; break;
            default:
                await game.Act("P1", new PlayCardCommand("UNIT", source, [], OptionalCosts: ["ADDITIONAL_POWER:1"])); await game.Top();
                if (game.State.PendingRuleChoice is { } rule)
                    await game.Act(rule.Request.PlayerId, new PayCostCommand(rule.Request.Id, "RULE_REPLACEMENT", ["ALLY"]));
                id = "ALLY"; break;
        }
        Assert.Contains(id, Assert.IsType<PendingEffectPlayState>(game.State.PendingEffectPlay).Sources.Keys);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        return (game, id);
    }

    [Theory, MemberData(nameof(HasteCases))]
    public async Task BaseWaiverStillQuotesAndPaysTheRealOptionalHasteCost(string source, bool paid)
    {
        var (game, id) = await Open(source);
        var play = new PlayCardCommand(id, HasteUnit, [], Destination: "BASE",
            OptionalCosts: paid ? [HasteOptionalCostNames.HasteReady] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, play));
        Assert.Equal(paid ? 1 : 0, quote.Cost!.Mana);
        Assert.Equal(paid ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        if (paid)
        {
            Assert.False(quote.CanPay);
            await RadGearTestPosition.Reject(game.State, "P1", play);
            await game.Act("P1", new TapRuneCommand("FEE-RUNE"));
            await game.Act("P1", new RecycleRuneCommand("FEE-RUNE"));
        }
        else Assert.True(quote.CanPay, quote.Message);
        await game.Act("P1", play);
        Assert.Equal(!paid, game.State.CardObjects[id].IsExhausted);
        Assert.Equal(0, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        if (game.State.PendingEffectPlay is { Optional: true } pending)
            await game.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await game.Replay();
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task AnExistingHeldUnitTaxStillIncreasesTheOtherwiseFreePlay(string source)
    {
        var (game, id) = await Open(source);
        // Controlled payment boundary: the holder's existing turn-wide increase
        // applies independently of which effect granted this play permission.
        var state = game.State with { UntilEndOfTurnEffects = [..game.State.UntilEndOfTurnEffects,
            "BATTLEFIELD_HELD_NON_TOKEN_UNIT_COST_INCREASE:P1"] };
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
        var play = new PlayCardCommand(id, HasteUnit, [], Destination: "BASE");
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, play));
        Assert.False(quote.CanPay); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await RadGearTestPosition.Reject(state, "P1", play);
        var tap = await engine.ResolveAsync(state, new("tax-resource", "P1", CommandTypes.TapRune),
            new TapRuneCommand("FEE-RUNE"), default);
        Assert.True(tap.Accepted, tap.ErrorMessage);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(tap.State));
        var result = await engine.ResolveAsync(tap.State, new("tax-payment", "P1", CommandTypes.PlayCard), play, default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(id, result.State.PlayerZones["P1"].Base);
        Assert.Equal(0, result.State.RunePools["P1"].Mana);
        Assert.True(result.State.CardObjects[id].IsExhausted);
        FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
    }

    [Theory, MemberData(nameof(Cards))]
    public async Task RecoveryRejectsReplacingTheBaseWaiverWithAnAllCostsWaiver(string source)
    {
        var (game, _) = await Open(source);
        var pending = game.State.PendingEffectPlay!;
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(game.State with {
            PendingEffectPlay = pending with { IgnoreAllCosts = true } }));
    }

    [Theory]
    [InlineData("OGN·025/298")]
    [InlineData("RAD·018/167")]
    [InlineData("RAD·115/167")]
    [InlineData("RAD·136/167")]
    [InlineData("RAD·140/167")]
    public async Task ARealMandatoryAdditionalSacrificeIsNeverWaived(string source)
    {
        const string no = "OGN·208/298";
        var (game, id) = await Open(source, no);
        var play = new PlayCardCommand(id, no, [], Destination: "BASE");
        await RadGearTestPosition.Reject(game.State, "P1", play);
        await game.Act("P1", play with { OptionalCosts = ["DESTROY_FRIENDLY_UNIT:SACRIFICE"] });
        Assert.Contains("SACRIFICE", game.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(id, game.State.PlayerZones["P1"].Base);
        await game.Replay();
    }
}
