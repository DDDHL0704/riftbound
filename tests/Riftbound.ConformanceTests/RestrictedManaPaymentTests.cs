using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class RestrictedManaPaymentTests
{
    [Fact]
    public void TheSourceOverrideIsExactlyTwoPrintsOfThisSpecificAbility()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(P4ActivatedAbilityCatalog.LuxResourceAbilityId, out var lux));
        Assert.Equal(new[] { "OGS·014/024", "VEN·SP6" }, P4ActivatedAbilityCatalog.SourceCardNosForAbility(lux));
        Assert.False(P4ActivatedAbilityCatalog.IsSourceCardNoForAbility(lux, "OGS·006/024"));
        Assert.DoesNotContain("VEN·SP6", P4ActivatedAbilityCatalog.SourceCardNosForAbility(lux with { SourceCardNo = "OGS·006/024" }));
        Assert.DoesNotContain("VEN·SP6", P4ActivatedAbilityCatalog.SourceCardNosForAbility(lux with { AbilityId = "OTHER" }));
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId, out var other));
        Assert.DoesNotContain("VEN·SP6", P4ActivatedAbilityCatalog.SourceCardNosForAbility(other));
        Assert.Contains(other.SourceCardNo, P4ActivatedAbilityCatalog.SourceCardNosForAbility(other));
    }

    [Theory]
    [InlineData(1, 2, 1, true, true, 1, 1)]
    [InlineData(1, 2, 2, true, true, 1, 0)]
    [InlineData(1, 2, 3, true, true, 0, 0)]
    [InlineData(1, 2, 4, true, false, 1, 2)]
    [InlineData(4, 2, 3, false, true, 1, 2)]
    [InlineData(0, 2, 1, false, false, 0, 2)]
    public void OnlyAnExplicitSpellPlayPurposeCanDebitRestrictedMana(int mana, int restricted, int cost,
        bool spell, bool accepted, int remainingMana, int remainingRestricted)
    {
        var pool = new RunePool(mana, 3, new Dictionary<string, int> { [RuneTrait.Red] = 1 }, restricted);
        var pools = new Dictionary<string, RunePool> { ["P1"] = pool };
        var plan = new PaymentCostRules.PaymentPlan("purpose-test", "PLAY_CARD", "P1", totalManaCost: cost,
            manaPurpose: spell ? ManaPaymentPurpose.PlaySpell : ManaPaymentPurpose.Other);
        var paid = PaymentCostRules.TryCommitPayment(plan, pools);
        Assert.Equal(accepted, paid.Accepted);
        Assert.Equal(remainingMana, paid.RunePools["P1"].Mana);
        Assert.Equal(remainingRestricted, paid.RunePools["P1"].SpellOnlyMana);
        Assert.Equal(3, paid.RunePools["P1"].Power);
        Assert.Equal(1, paid.RunePools["P1"].PowerByTrait[RuneTrait.Red]);
        Assert.Equal(pool, pools["P1"]);
    }

    [Fact]
    public void DefaultPurposeAndAuditStringsCannotGrantSpellPaymentPermission()
    {
        var pool = new RunePool(0, 0, spellOnlyMana: 2);
        var plan = new PaymentCostRules.PaymentPlan("default-purpose", "PLAY_CARD", "P1", totalManaCost: 1,
            reason: "PLAY_SPELL", auditMetadata: new Dictionary<string, object?> { ["manaPurpose"] = "PLAY_SPELL" });
        Assert.False(PaymentCostRules.AuthorizePayment(plan, pool).Accepted);
        var invalid = new PaymentCostRules.PaymentPlan("invalid-purpose", "PLAY_CARD", "P1", totalManaCost: 1,
            manaPurpose: (ManaPaymentPurpose)99);
        Assert.False(PaymentCostRules.AuthorizePayment(invalid, pool).Accepted);
        Assert.False(PaymentCostRules.CanPayRuneCosts(pool, 1, 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewTotalsExposeOnlyResourcesAuthorizedForTheActualCard(bool unit)
    {
        var state = Pool(RestrictedManaLifecycleTests.Position(), new(1, 10, spellOnlyMana: 2));
        var command = unit ? new PlayCardCommand("UNIT", "OGN·044/298", [])
            : new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]);
        if (unit) state = Pool(state, new(3, 10, spellOnlyMana: 2));
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.True(quote.CanPay, quote.Message);
        var cost = Assert.IsType<PlayCostBreakdownDto>(quote.Cost);
        Assert.Equal(3, cost.AvailableMana);
        Assert.Equal(unit ? 0 : 2, cost.AvailableSpellOnlyMana);
        Assert.Equal(unit ? 1 : 2, cost.RemainingMana);
        Assert.Equal(unit ? 0 : 1, cost.RemainingSpellOnlyMana);
        Assert.True(cost.RemainingMana - cost.RemainingSpellOnlyMana >= 0);
        var paid = await Act(state, command);
        Assert.Equal(1, paid.State.RunePools["P1"].Mana);
        Assert.Equal(unit ? 2 : 1, paid.State.RunePools["P1"].SpellOnlyMana);
        var json = JsonSerializer.Serialize(cost, Web);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<PlayCostBreakdownDto>(json, Web), Web));
        var legacy = JsonSerializer.Deserialize<LegacyCost>(json, Web)!;
        Assert.Equal(cost.AvailableMana, legacy.AvailableMana);
        Assert.Equal(cost.RemainingMana, legacy.RemainingMana);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnpayableQuotesDoNotInventRemainingBalances(bool unit)
    {
        var state = Pool(RestrictedManaLifecycleTests.Position(), new(0, 10, spellOnlyMana: unit ? 2 : 0));
        var command = unit ? new PlayCardCommand("UNIT", "OGN·044/298", [])
            : new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"]);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        Assert.False(quote.CanPay);
        var cost = Assert.IsType<PlayCostBreakdownDto>(quote.Cost);
        Assert.Equal(0, cost.AvailableMana); Assert.Equal(0, cost.AvailableSpellOnlyMana);
        Assert.Null(cost.RemainingMana); Assert.Null(cost.RemainingSpellOnlyMana);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(cost, Web));
        Assert.False(json.RootElement.TryGetProperty("availableSpellOnlyMana", out _));
        Assert.False(json.RootElement.TryGetProperty("remainingSpellOnlyMana", out _));
    }

    [Fact]
    public void MissingZeroRestrictedFieldsRoundTripAndDoNotChangeExistingWalletFields()
    {
        const string old = "{\"mana\":3,\"power\":2,\"powerByTrait\":{\"red\":1}}";
        var pool = JsonSerializer.Deserialize<RunePool>(old, Web)!;
        Assert.Equal(0, pool.SpellOnlyMana); Assert.Equal(3, pool.Mana); Assert.Equal(3, pool.TotalPower);
        Assert.DoesNotContain("spellOnlyMana", JsonSerializer.Serialize(pool, Web));
        var restricted = pool with { SpellOnlyMana = 2 };
        Assert.NotEqual(pool, restricted);
        Assert.Equal(restricted, JsonSerializer.Deserialize<RunePool>(JsonSerializer.Serialize(restricted, Web), Web));
        var state = Pool(RestrictedManaLifecycleTests.Position(), restricted);
        Assert.NotEqual(MatchStateHasher.Hash(state), MatchStateHasher.Hash(Pool(state, pool)));
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(int.MaxValue, 0, 1)]
    public void MalformedAmountsArePreservedAndCannotBeAuthorized(int mana, int power, int restricted)
    {
        var pool = new RunePool(mana, power, spellOnlyMana: restricted);
        Assert.Equal(mana, pool.Mana); Assert.Equal(power, pool.Power); Assert.Equal(restricted, pool.SpellOnlyMana);
        Assert.False(pool.HasSupportedAmounts);
        Assert.False(PaymentCostRules.AuthorizePayment(new("invalid-pool", "PLAY_CARD", "P1"), pool).Accepted);
    }

    [Theory]
    [InlineData("\"2\"")]
    [InlineData("2.5")]
    [InlineData("2147483648")]
    [InlineData("null")]
    public void RawRestrictedBalancesCannotBeCoercedIntoValidIntegers(string raw)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RunePool>(
            "{\"mana\":0,\"power\":0,\"spellOnlyMana\":" + raw + "}", Web));

    [Theory]
    [InlineData(int.MaxValue, 0)]
    [InlineData(0, int.MaxValue - 1)]
    public async Task UnrepresentableStandaloneAndInlineGainsRejectAtomically(int mana, int restricted)
    {
        var state = Pool(RestrictedManaLifecycleTests.Position(), new(mana, 10, spellOnlyMana: restricted));
        await Rejected(state, new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []), ErrorCodes.UnsupportedCardBehavior);
        await Rejected(state, new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"],
            OptionalCosts: ["LUX_SPELL_ONLY_RESOURCE:LUX"]), ErrorCodes.UnsupportedCardBehavior);
    }

    [Theory]
    [InlineData("controller")]
    [InlineData("location")]
    [InlineData("exhausted")]
    [InlineData("face-down")]
    [InlineData("forged-id")]
    [InlineData("other-lux")]
    public async Task InvalidSourceCannotGainRestrictedManaThroughEitherPath(string change)
    {
        var state = RestrictedManaLifecycleTests.Position();
        var source = state.CardObjects["LUX"];
        source = change switch {
            "controller" => source with { ControllerId = "P2" }, "exhausted" => source with { IsExhausted = true },
            "face-down" => source with { IsFaceDown = true }, "forged-id" => source with { ObjectId = "OTHER" },
            "other-lux" => OfficialGarenConquestBattlefieldCountTests.Printed("LUX", "OGS·006/024", "P1"), _ => source };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["LUX"] = source } };
        if (change == "location") state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
            { ["LUX"] = new("P2", "BASE") } };
        await Rejected(state, new ActivateAbilityCommand("LUX", P4ActivatedAbilityCatalog.LuxResourceAbilityId, []));
        await Rejected(state, new PlayCardCommand("FIRST", "OGN·268/298", ["FIELD"], OptionalCosts: ["LUX_SPELL_ONLY_RESOURCE:LUX"]));
    }

    internal static MatchState Pool(MatchState state, RunePool pool, string player = "P1")
        => state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { [player] = pool } };
    internal static async Task<ResolutionResult> Act(MatchState state, GameCommand command, string player = "P1")
    {
        var result = await new CoreRuleEngine().ResolveAsync(state, new("restricted-test-" + state.Tick, player, command.CmdType), command, default);
        Assert.True(result.Accepted, result.ErrorCode + ": " + result.ErrorMessage); return result;
    }
    internal static async Task Rejected(MatchState state, GameCommand command, string? code = null)
    {
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("restricted-reject", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        if (code is not null) Assert.Equal(code, result.ErrorCode);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State)); Assert.False(result.State.CardObjects["LUX"].IsExhausted && !state.CardObjects["LUX"].IsExhausted);
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private sealed record LegacyCost(int AvailableMana, int? RemainingMana);
}
