using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Keep the original manual P7 inputs and early commands as rejection controls.
// CN204.3.a / 740.4.a.1 require a distinct, actual resolving-instruction choice.
// The current positive copy supplies the one legacy target's missing UNIT type
// and known zone controller, without inventing a printing/owner or changing stats.
// These legacy boundary fixtures are not certified ordinary-room birth logs.
internal static class InstructionPaymentOriginalP7Assertions
{
    internal sealed record Opened(ResolutionResult EarlyRejection, ResolutionResult Declaration,
        MatchState Window, string Choice);
    internal sealed record Observed(Opened Opened, ResolutionResult Final)
    {
        internal ResolutionResult Declaration => Opened.Declaration;
    }

    internal static async Task<Opened> Open(MatchState initial, PlayerIntent intent, PlayCardCommand original)
    {
        Assert.Equal("OGN·268/298", original.CardNo);
        var costs = original.OptionalCosts ?? [];
        var choice = Assert.Single(costs, id => id.StartsWith("SPEND_POWER:", StringComparison.Ordinal));
        var before = MatchStateHasher.Hash(initial); var engine = new CoreRuleEngine();
        var early = await engine.ResolveAsync(initial, intent, original, default);
        Assert.False(early.Accepted); Assert.Equal(ErrorCodes.InvalidTarget, early.ErrorCode);
        Assert.Empty(early.Events); Assert.Equal(before, MatchStateHasher.Hash(early.State));
        var resources = costs.Where(id => !id.StartsWith("SPEND_POWER:", StringComparison.Ordinal)).ToArray();
        var currentFixture = CurrentTargetBoundary(initial);
        var declaration = await engine.ResolveAsync(currentFixture, intent with { IntentId = intent.IntentId + "-current-declaration" },
            original with { OptionalCosts = resources }, default);
        Assert.True(declaration.Accepted, declaration.ErrorMessage);
        Assert.DoesNotContain(declaration.Events, e => e.Kind == "DAMAGE_APPLIED");
        Assert.Equal(initial.RunePools["P1"].TotalPower + resources.Count(id => id.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal)),
            declaration.State.RunePools["P1"].TotalPower);
        var baseCost = Assert.Single(declaration.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload.GetValueOrDefault("paymentWindow"), "PLAY_CARD"));
        Assert.Equal(0, baseCost.Payload["totalPowerCost"]);
        var window = await InstructionPowerPaymentTimingTests.ToPayment(RoundTrip(declaration.State));
        Assert.Equal("INSTRUCTION_POWER_PAYMENT", window.PendingRuleChoice!.Request.Kind);
        Assert.Equal(intent.PlayerId, window.PendingRuleChoice.Request.PlayerId);
        Assert.Equal(0, window.CardObjects["P2-BULLET-TIME-UNIT-001"].Damage);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        Export(intent.IntentId + "-open", new { originalInitial = initial, currentFixture, original, early, declaration, window });
        return new(early, declaration, RoundTrip(window), choice);
    }

    internal static async Task<Observed> Execute(MatchState initial, PlayerIntent intent, PlayCardCommand original)
    {
        var opened = await Open(initial, intent, original);
        Assert.Contains(opened.Window.PendingRuleChoice!.Request.Options, o => o.Id == opened.Choice);
        var final = await InstructionPowerPaymentTimingTests.Pay(opened.Window, opened.Choice);
        Export(intent.IntentId + "-final", new { originalInitial = initial, opened, final });
        Assert.True(final.Accepted, final.ErrorMessage); Assert.Null(final.State.PendingRuleChoice);
        Assert.Empty(final.State.StackItems); RoundTrip(final.State);
        var paid = Assert.Single(final.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload.GetValueOrDefault("paymentWindow"), "INSTRUCTION_POWER_PAYMENT"));
        var events = final.Events.ToList();
        Assert.True(events.IndexOf(paid) < events.FindIndex(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Equal([opened.Choice], Assert.IsType<string[]>(paid.Payload["paymentChoiceIds"]));
        return new(opened, final);
    }

    internal static GameEvent DeclarationCost(Observed observed)
        => Assert.Single(observed.Declaration.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload.GetValueOrDefault("paymentWindow"), "PLAY_CARD"));

    private static MatchState CurrentTargetBoundary(MatchState original)
    {
        const string id = "P2-BULLET-TIME-UNIT-001";
        var target = original.CardObjects[id]; var location = original.ObjectLocations[id];
        Assert.Equal(id, target.ObjectId); Assert.Equal(5, target.Power); Assert.Equal(0, target.Damage);
        Assert.Empty(target.Tags); Assert.Null(target.CardNo); Assert.Null(target.ControllerId); Assert.Null(target.OwnerId);
        Assert.False(target.IsFaceDown); Assert.False(target.IsExhausted); Assert.Equal(0, target.ObjectGeneration);
        Assert.Equal("P2", location.PlayerId); Assert.Equal("BATTLEFIELD", location.Zone);
        Assert.Equal(SelectedBattlefieldFixture.Field, location.BattlefieldObjectId);
        Assert.Contains(id, original.PlayerZones["P2"].Battlefields);
        return original with { CardObjects = new Dictionary<string, CardObjectState>(original.CardObjects) {
            [id] = target with { Tags = [CardObjectTags.UnitCard], ControllerId = location.PlayerId } } };
    }

    private static void Export(string name, object value)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_P7_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(value, value.GetType(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private static MatchState RoundTrip(MatchState state)
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var restored = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(state, web), web)!;
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
        return restored;
    }
}
