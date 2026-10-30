using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadExhaustCostSpellTests
{
    private const string Roar = "RAD·028/167";
    private const string Renewal = "RAD·029/167";
    private const string Exhaust = "EXHAUST_FRIENDLY_UNIT:";

    [Theory]
    [InlineData(Roar, 39521L)] [InlineData(Renewal, 39522L)]
    public async Task ExactOfficialPrintingsKeepTheirCostTimingAndMandatoryUnitPayment(string no, long id)
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == no);
        Assert.Equal(id, card.Id); Assert.Equal(1, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Null(card.Power);
        Assert.Equal("法术", card.CardCategoryName);
        Assert.Equal(no == Roar
            ? "{{迅捷}}（可在你的回合或法术对决中打出。）\n让一名友方单位变为休眠状态，作为打出此牌的额外费用。\n{{眩晕}}一名单位。（使其在本回合内无法造成战斗伤害。）\n"
            : "{{反应}}（可在任意时机打出，甚至先于其他法术和技能的结算。）\n让一名友方单位变为休眠状态，作为打出此牌的额外费用。\n抽一张牌并召出一枚休眠的符文。", card.CardEffect);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(no, out var behavior));
        Assert.True(behavior.RequiresExhaustFriendlyUnitAdditionalCost);
        Assert.Equal(no == Renewal, behavior.CanPlayDuringPriority);
        Assert.Equal(no == Roar, behavior.CanPlayDuringSpellDuel);
    }

    [Theory]
    [InlineData(Roar, "BASE")] [InlineData(Roar, "BATTLEFIELD")]
    [InlineData(Renewal, "BASE")] [InlineData(Renewal, "BATTLEFIELD")]
    public async Task PaymentExhaustsOneFriendlyUnitBeforeTheResponseWindowAndNeverTaxesItAsATarget(string no, string zone)
    {
        var initial = RadUnitEffectTestFixture.Place(Position(no), "ALLY", "P1", zone, zone == "BATTLEFIELD" ? "BF" : null);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { Tags = [..initial.CardObjects["ALLY"].Tags, "法盾3"] } } };
        var command = Play(no);
        var quote = new CoreRuleEngine().PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(1, quote.Cost!.Mana);
        Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        var requirement = Requirement(initial);
        Assert.Equal(1, requirement["requiredAdditionalCostChoiceCount"]);
        var choices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["additionalCostChoices"]);
        Assert.Equal(new[] { Exhaust + "ALLY" }, choices.Select(c => c.Id));
        var game = new Driver(initial);
        await game.Act("P1", command);
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Equal(19, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count);
        await game.Top();
        if (no == Roar) Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        else {
            Assert.Equal(new[] { "P1-DRAW-1" }, game.State.PlayerZones["P1"].Hand);
            Assert.Contains("COST-RUNE-0", game.State.PlayerZones["P1"].Base);
            Assert.True(game.State.CardObjects["COST-RUNE-0"].IsExhausted);
            Assert.Equal(new[] { "CARD_DRAWN", "RUNES_CALLED" }, game.Events().Where(e => e.Event.Kind is "CARD_DRAWN" or "RUNES_CALLED")
                .Select(e => e.Event.Kind));
        }
        await game.Replay();
    }

    public static IEnumerable<object[]> InvalidPayments() => new[] { Roar, Renewal }.SelectMany(no =>
        new[] { "missing", "sleeping", "hidden", "equipment", "rune", "enemy", "graveyard", "hand", "multiple", "unknown", "no-mana", "wrong-turn" }
            .Select(reason => new object[] { no, reason }));

    [Theory]
    [MemberData(nameof(InvalidPayments))]
    public async Task InvalidPaymentCannotExhaustAnythingSpendResourcesOrAlterViews(string no, string reason)
    {
        var state = Position(no);
        if (reason is "sleeping" or "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["ALLY"] = state.CardObjects["ALLY"] with { IsExhausted = reason == "sleeping", IsFaceDown = reason == "hidden" } } };
        if (reason is "graveyard" or "hand") state = RadUnitEffectTestFixture.Place(state, "ALLY", "P1", reason == "hand" ? "HAND" : "GRAVEYARD");
        if (reason == "no-mana") state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 20) } };
        if (reason == "wrong-turn") state = state with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        var payment = reason switch { "equipment" => "GEAR", "rune" => "COST-RUNE-0", "enemy" => "ENEMY", "unknown" => "UNKNOWN", _ => "ALLY" };
        await Reject(state, Play(no) with { OptionalCosts = reason == "missing" ? [] : reason == "multiple"
            ? [Exhaust + "ALLY", Exhaust + "ENEMY"] : [Exhaust + payment] });
        if (reason is "sleeping" or "hidden" or "graveyard" or "hand")
            Assert.DoesNotContain(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
                c => c.Action == CommandTypes.PlayCard && c.Sources?.Any(s => s.Id == "UNIT") == true);
    }

    [Fact]
    public async Task TheStunCanChooseItsOwnPaymentUnitAndAnAlreadyStunnedUnitStillRequiresPayment()
    {
        var initial = Position(Roar);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ALLY"] = initial.CardObjects["ALLY"] with { UntilEndOfTurnEffects = ["STUNNED"] } } };
        var game = new Driver(initial);
        await game.Act("P1", Play(Roar) with { TargetObjectIds = ["ALLY"] }); await game.Top();
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.Contains("STUNNED", game.State.CardObjects["ALLY"].UntilEndOfTurnEffects);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED"); await game.Replay();
    }

    [Fact]
    public async Task WardShortfallRejectsTheWholePaymentBeforeExhaustingTheUnit()
    {
        var state = Position(Roar);
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 0) },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = state.CardObjects["ENEMY"] with { Tags = [..state.CardObjects["ENEMY"].Tags, "法盾"] } } };
        await Reject(state, Play(Roar));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(1, 1) } };
        var game = new Driver(state);
        await game.Act("P1", Play(Roar));
        Assert.True(game.State.CardObjects["ALLY"].IsExhausted); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        await game.Top(); Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects); await game.Replay();
    }

    [Theory]
    [InlineData(Roar)] [InlineData(Renewal)]
    public async Task ReturningThePaymentUnitAfterItWasPaidDoesNotRemoveTheSpellEffect(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "RETURN", "OGN·104/298", "P1");
        var game = new Driver(initial);
        await game.Act("P1", Play(no));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["ALLY"])); await game.Top();
        Assert.Contains("ALLY", game.State.PlayerZones["P1"].Hand); await game.Top();
        if (no == Roar) Assert.Contains("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        else { Assert.Contains("P1-DRAW-1", game.State.PlayerZones["P1"].Hand); Assert.Equal(2, game.State.PlayerZones["P1"].RuneDeck.Count); }
        await game.Replay();
    }

    [Fact]
    public async Task AReturnedStunTargetDoesNotRefundThePaidUnitOrAllowRetargeting()
    {
        var initial = AttackDamageDistributionTests.Spell(Position(Roar), "RETURN", "OGN·104/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", Play(Roar)); await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("RETURN", "OGN·104/298", ["ENEMY"])); await game.Top(); await game.Top();
        Assert.Contains("ENEMY", game.State.PlayerZones["P2"].Hand); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain(game.Events(), e => e.Event.Kind == "STATUS_EFFECT_APPLIED"); await game.Replay();
    }

    [Theory]
    [InlineData(Roar)] [InlineData(Renewal)]
    public async Task CounteringPreventsTheEffectAndNeverRefundsTheExhaustCost(string no)
    {
        var initial = AttackDamageDistributionTests.Spell(Position(no), "COUNTER", "OGN·064/298", "P2");
        var game = new Driver(initial);
        await game.Act("P1", Play(no)); var spell = Assert.Single(game.State.StackItems);
        await game.Act("P1", new PassPriorityCommand());
        await game.Act("P2", new PlayCardCommand("COUNTER", "OGN·064/298", [spell.StackItemId])); await game.Top();
        Assert.Empty(game.State.StackItems); Assert.True(game.State.CardObjects["ALLY"].IsExhausted);
        Assert.DoesNotContain("STUNNED", game.State.CardObjects["ENEMY"].UntilEndOfTurnEffects);
        Assert.Empty(game.State.PlayerZones["P1"].Hand); Assert.Equal(3, game.State.PlayerZones["P1"].RuneDeck.Count); await game.Replay();
    }

    [Theory]
    [InlineData(Roar)] [InlineData(Renewal)]
    public async Task EchoPaysTheUnitCostOnceAndRepeatsTheWholeEffect(string no)
    {
        var initial = Position(no);
        initial = initial with { UntilEndOfTurnEffects = EchoCostRules.AddBaseCostGrant(initial.UntilEndOfTurnEffects, "P1") };
        var game = new Driver(initial);
        await game.Act("P1", Play(no) with { OptionalCosts = [Exhaust + "ALLY", "ECHO"],
            RepeatChoices = no == Roar ? [new(null, ["ALLY"])] : null }); await game.Top();
        Assert.Equal(18, game.State.RunePools["P1"].Mana);
        Assert.Single(game.Events(), e => e.Event.Kind == "UNIT_EXHAUSTED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        if (no == Roar) foreach (var id in new[] { "ALLY", "ENEMY" }) Assert.Contains("STUNNED", game.State.CardObjects[id].UntilEndOfTurnEffects);
        else {
            Assert.Equal(new[] { "P1-DRAW-1", "P1-DRAW-2" }, game.State.PlayerZones["P1"].Hand);
            Assert.Equal(new[] { "CARD_DRAWN", "RUNES_CALLED", "CARD_DRAWN", "RUNES_CALLED" },
                game.Events().Where(e => e.Event.Kind is "CARD_DRAWN" or "RUNES_CALLED").Select(e => e.Event.Kind));
        }
        Assert.Single(game.State.PlayerZones["P1"].Graveyard, id => id == "UNIT"); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RenewalKeepsTheDrawWhenNoRuneIsAvailableAndUsesTheSharedReplacementWhenPresent(bool wyvern)
    {
        var initial = Position(Renewal);
        if (wyvern) {
            initial = HandDiscardObserverTests.Put(initial, "WYVERN", "RAD·092/167", "P1", "HAND");
            initial = RadUnitEffectTestFixture.Place(initial, "WYVERN", "P1", "BASE");
        } else foreach (var id in initial.PlayerZones["P1"].RuneDeck.ToArray())
            initial = RadUnitEffectTestFixture.Place(initial, id, "P1", "BANISHED");
        var game = new Driver(initial);
        await game.Act("P1", Play(Renewal)); await game.Top();
        Assert.Equal(new[] { "P1-DRAW-1" }, game.State.PlayerZones["P1"].Hand);
        var called = Assert.Single(game.Events(), e => e.Event.Kind == "RUNES_CALLED");
        Assert.Equal(wyvern ? 2 : 0, Convert.ToInt32(called.Event.Payload["count"]));
        foreach (var id in game.State.PlayerZones["P1"].Base.Where(id => id.StartsWith("COST-RUNE-")))
            Assert.True(game.State.CardObjects[id].IsExhausted);
        await game.Replay();
    }

    private static PlayCardCommand Play(string no) => new("UNIT", no, no == Roar ? ["ENEMY"] : [], OptionalCosts: [Exhaust + "ALLY"]);

    private static IReadOnlyDictionary<string, object?> Requirement(MatchState state)
    {
        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [], c => c.Action == CommandTypes.PlayCard);
        var rows = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]);
        return Assert.Single(rows, r => Equals(r["sourceObjectId"], "UNIT"));
    }

    private static MatchState Position(string no)
    {
        var state = VenUnitEntryBasicTests.Position(no);
        var cards = state.CardObjects.ToDictionary(); var locations = state.ObjectLocations.ToDictionary();
        var runes = Enumerable.Range(0, 3).Select(i => "COST-RUNE-" + i).ToArray();
        foreach (var id in runes) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, "UNL-R02", "P1"); locations[id] = new("P1", "RUNE_DECK"); }
        return state with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { RuneDeck = runes } } };
    }

    private static async Task Reject(MatchState state, PlayCardCommand command)
    {
        var hash = MatchStateHasher.Hash(state);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));
        var prompts = JsonSerializer.Serialize(ResolutionResult.BuildPrompts(state));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay); Assert.Equal(hash, MatchStateHasher.Hash(state));
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-exhaust-cost", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(hash, MatchStateHasher.Hash(result.State));
        Assert.Equal(snapshots, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
        Assert.Equal(prompts, JsonSerializer.Serialize(ResolutionResult.BuildPrompts(result.State)));
    }
}
