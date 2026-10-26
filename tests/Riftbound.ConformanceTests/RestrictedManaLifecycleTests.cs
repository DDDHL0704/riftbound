using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Current OGS014/VENSP6 restrict how the gained mana can be spent. CN166-168
// put it in the rune pool and define main-start/turn-end clearing boundaries.
public sealed class RestrictedManaLifecycleTests
{
    [Theory]
    [InlineData("OGS·014/024")]
    [InlineData("VEN·SP6")]
    public async Task LuxCanAcquireRestrictedManaInAnOrdinaryReactionResourceWindow(string card)
    {
        var state = Position(card);
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("restricted-lux-activation", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []), default);
        Assert.True(result.Accepted, result.ErrorCode + ": " + result.ErrorMessage);
        Assert.True(result.State.CardObjects["LUX"].IsExhausted);
        Assert.Empty(result.State.StackItems);
        Assert.Equal(state.PriorityPlayerId, result.State.PriorityPlayerId);
        Assert.Equal(state.ActivePlayerId, result.State.ActivePlayerId);
        Assert.True(engine.PreviewPlayCard(result.State, "P1", PlayCostPreviewTests.Request(result.State,
            new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]))).CanPay);
        var play = Assert.Single(result.Prompts["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(play.Metadata!["sourceRequirements"]);
        var first = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "FIRST"));
        Assert.Equal(2, first["availableMana"]); Assert.Equal(2, first["availableSpellOnlyMana"]);
        Assert.DoesNotContain(Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(first["paymentResourceChoices"]),
            choice => choice.Id == "LUX_SPELL_ONLY_RESOURCE:LUX");
        Assert.False(engine.PreviewPlayCard(result.State, "P1", PlayCostPreviewTests.Request(result.State,
            new PlayCardCommand("UNIT", "OGN·044/298", []))).CanPay);
    }

    [Theory]
    [InlineData("OGS·014/024")]
    [InlineData("VEN·SP6")]
    public async Task InlineLuxManaSurplusRemainsRestrictedAndCanPayAnotherSpellBeforePoolClearing(string card)
    {
        var initial = Position(card);
        var engine = new CoreRuleEngine();
        var first = await engine.ResolveAsync(initial, new("restricted-lux-first-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"], OptionalCosts: ["LUX_SPELL_ONLY_RESOURCE:LUX"]), default);
        Assert.True(first.Accepted, first.ErrorCode + ": " + first.ErrorMessage);
        Assert.True(first.State.CardObjects["LUX"].IsExhausted);
        var payment = await InstructionPowerPaymentTimingTests.ToPayment(first.State);
        var completed = await InstructionPowerPaymentTimingTests.Pay(payment, "SPEND_POWER:0");
        Assert.True(completed.Accepted, completed.ErrorMessage);
        Assert.Empty(completed.State.StackItems);
        Assert.Equal(initial.TurnNumber, completed.State.TurnNumber);
        Assert.Equal(MatchPhases.Main, completed.State.Phase);
        var next = new PlayCardCommand("SECOND", "OGN·268/298", ["FIELD"]);
        var quote = engine.PreviewPlayCard(completed.State, "P1", PlayCostPreviewTests.Request(completed.State, next));
        Assert.True(quote.CanPay, JsonSerializer.Serialize(new {
            quote, pool = completed.State.RunePools["P1"],
            resourceEvents = first.Events.Where(e => e.Kind.StartsWith("TEMPORARY_PAYMENT_RESOURCE_", StringComparison.Ordinal)) }));
        Assert.DoesNotContain(first.Events, e => e.Kind == "TEMPORARY_PAYMENT_RESOURCE_CLEARED");
    }

    internal static MatchState Position(string luxCard = "OGS·014/024")
    {
        CardObjectState Card(string id, string no, string actor)
            => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, actor);
        var target = Card("TARGET", "OGN·142/298", "P2");
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(target.CardNo!, out var targetBehavior));
        target = target with { Tags = target.Tags.Concat(targetBehavior.SourceUnitTags.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray() };
        return new("RESTRICTED-MANA-LIFECYCLE", 0, 3, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(0, 10), ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["LUX"], Hand = ["FIRST", "SECOND", "UNIT"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD", "TARGET"] } },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["LUX"] = Card("LUX", luxCard, "P1"), ["FIRST"] = Card("FIRST", "OGN·268/298", "P1"),
                ["SECOND"] = Card("SECOND", "OGN·268/298", "P1"), ["UNIT"] = Card("UNIT", "OGN·044/298", "P1"),
                ["FIELD"] = Card("FIELD", "OGN·278/298", "P2") with { ControllerId = "P2" }, ["TARGET"] = target },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["LUX"] = new("P1", "BASE"), ["FIRST"] = new("P1", "HAND"), ["SECOND"] = new("P1", "HAND"),
                ["UNIT"] = new("P1", "HAND"), ["FIELD"] = new("P2", "BATTLEFIELD", "FIELD"), ["TARGET"] = new("P2", "BATTLEFIELD", "FIELD") });
    }
}
