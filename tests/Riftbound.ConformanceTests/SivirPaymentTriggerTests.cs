using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class SivirPaymentTriggerTests
{
    [Theory]
    [InlineData("SFD·203/221", false)]
    [InlineData("SFD·203/221", true)]
    [InlineData("SFD·250/221", false)]
    [InlineData("SFD·250/221", true)]
    public async Task QuotedInlineWardRunePaymentCapturesSivirBeforeTheSpellResponse(string card, bool pay)
    {
        var initial = InlinePosition(card);
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["T"], OptionalCosts: ["RECYCLE_RUNE:RUNE"]);
        var engine = new CoreRuleEngine(); var before = MatchStateHasher.Hash(initial);
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, command));
        Assert.True(quote.CanPay, quote.Message); Assert.Equal(2, quote.Cost!.Mana); Assert.Equal(1, quote.Cost.GenericPower);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        var played = await Act(initial, "P1", command); Restore(played.State);
        Assert.Equal(0, played.State.RunePools["P1"].Mana); Assert.Equal(0, played.State.RunePools["P1"].TotalPower);
        Assert.Contains("RUNE", played.State.PlayerZones["P1"].RuneDeck);
        Assert.False(played.State.CardObjects["SIVIR"].IsExhausted); Assert.Equal(0, played.State.CardObjects["T"].Damage);
        Assert.Equal(2, played.State.StackItems.Count);
        var done = await ConfirmSivirAndResolve(played.State, pay);
        Assert.Equal(2, done.CardObjects["T"].Damage); Assert.Equal(pay, done.CardObjects["SIVIR"].IsExhausted);
        Assert.Equal(pay ? 1 : 0, done.PlayerZones["P1"].Base.Count); Assert.Empty(done.StackItems); Restore(done);
    }

    [Theory]
    [InlineData("SFD·203/221", false, false)]
    [InlineData("SFD·203/221", false, true)]
    [InlineData("SFD·203/221", true, false)]
    [InlineData("SFD·203/221", true, true)]
    [InlineData("SFD·250/221", false, false)]
    [InlineData("SFD·250/221", false, true)]
    [InlineData("SFD·250/221", true, false)]
    [InlineData("SFD·250/221", true, true)]
    public async Task RuneResourceDuringActualTriggerWardWaitsForPaymentCompletion(string card, bool wardPay, bool sivirPay)
    {
        var prepared = await PendingWardPosition(card); var pending = prepared.PendingPayment!;
        var recycled = await Act(prepared, "P1", new RecycleRuneCommand("RUNE")); Restore(recycled.State);
        Assert.Equal(pending.PaymentId, recycled.State.PendingPayment?.PaymentId);
        Assert.Null(recycled.State.PendingCardChoice); Assert.Null(recycled.State.PriorityPlayerId);
        Assert.Single(recycled.State.TriggerQueue); Assert.Single(recycled.State.StackItems);
        Assert.False(recycled.State.CardObjects["SIVIR"].IsExhausted); Assert.Equal(0, recycled.State.CardObjects["ENEMY"].Damage);
        var before = MatchStateHasher.Hash(recycled.State);
        var forbidden = await new CoreRuleEngine().ResolveAsync(recycled.State, new("premature", "P2", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.False(forbidden.Accepted); Assert.Equal(before, MatchStateHasher.Hash(forbidden.State));
        var paid = await Act(recycled.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, [wardPay ? "PAY" : "DECLINE"])); Restore(paid.State);
        Assert.Null(paid.State.PendingPayment); Assert.Empty(paid.State.TriggerQueue);
        Assert.Equal(wardPay ? 0 : 1, paid.State.RunePools["P1"].TotalPower);
        Assert.Equal("TRIGGER_COST_CONFIRMATION", paid.State.PendingCardChoice?.ChoiceWindow);
        var done = await ConfirmSivirAndResolve(paid.State, sivirPay);
        Assert.Equal(wardPay ? 5 : 0, done.CardObjects["ENEMY"].Damage);
        Assert.Equal(sivirPay, done.CardObjects["SIVIR"].IsExhausted);
        Assert.Equal(sivirPay ? 1 : 0, done.PlayerZones["P1"].Base.Except(prepared.PlayerZones["P1"].Base).Count());
        Assert.Contains("RUNE2", done.PlayerZones["P1"].Base); Restore(done);
    }

    [Fact]
    public async Task InvalidInlineRunePaymentRejectsWithoutMovementOrCapturedTrigger()
    {
        var initial = InlinePosition("SFD·203/221");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["RUNE"] = initial.CardObjects["RUNE"] with { IsFaceDown = true } } };
        var command = new PlayCardCommand("CARD", "OGS·003/024", ["T"], OptionalCosts: ["RECYCLE_RUNE:RUNE"]);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("invalid", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    internal static MatchState InlinePosition(string card) => AddSivirAndRune(OfficialWardCostTests.State(new(2, 0)), card);
    internal static async Task<MatchState> PendingWardPosition(string card)
    {
        var initial = AddSivirAndRune(UncontrolledConquestTests.Position(), card);
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = RunePool.Empty },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["ENEMY"] = initial.CardObjects["ENEMY"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var conquered = await ConquestLifecycleRegressionTests.Conquer(initial);
        var chosen = await UncontrolledConquestTests.Choose(conquered.State); Restore(chosen.State);
        var opened = Assert.Single(chosen.Events, e => e.Kind == "PAYMENT_WINDOW_OPENED");
        Assert.Equal(chosen.State.PendingPayment!.PaymentId, opened.Payload["paymentId"]);
        Assert.Equal("TRIGGER_TARGET_COST", opened.Payload["paymentWindow"]); Assert.Equal(1, opened.Payload["powerCost"]);
        Assert.Equal(["ENEMY"], Assert.IsAssignableFrom<IReadOnlyList<string>>(opened.Payload["targetObjectIds"]));
        return chosen.State;
    }
    private static MatchState AddSivirAndRune(MatchState initial, string card)
    {
        var old = initial.PlayerZones["P1"].LegendZone.ToHashSet();
        var cards = initial.CardObjects.Where(p => !old.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        var locations = initial.ObjectLocations.Where(p => !old.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        cards["SIVIR"] = new("SIVIR", cardNo: card, ownerId: "P1", controllerId: "P1");
        cards["RUNE"] = new("RUNE", cardNo: "UNL-R03", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:blue"]);
        locations["SIVIR"] = new("P1", "LEGEND_ZONE"); locations["RUNE"] = new("P1", "BASE");
        return initial with { CardObjects = cards, ObjectLocations = locations,
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { LegendZone = ["SIVIR"], Base = initial.PlayerZones["P1"].Base.Where(id => id != "RUNE").Append("RUNE").ToArray() } } };
    }

    private static async Task<MatchState> ConfirmSivirAndResolve(MatchState current, bool pay)
    {
        var choice = current.PendingCardChoice!;
        Assert.Equal("TRIGGER_COST_CONFIRMATION", choice.ChoiceWindow); Assert.Equal(["SIVIR"], choice.LegalObjectIds);
        current = (await Act(current, "P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, pay ? ["SIVIR"] : []))).State;
        Assert.Equal(pay, current.CardObjects["SIVIR"].IsExhausted); Restore(current);
        if (pay) current = (await Top(current)).State;
        if (current.StackItems.Count > 0) current = (await Top(current)).State;
        return current;
    }
}
