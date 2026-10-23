using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class BoundedResourceQuantityTests
{
    [Theory]
    [InlineData("stele", 0)] [InlineData("stele", 1)] [InlineData("stele", 63)]
    [InlineData("stele", 64)] [InlineData("stele", 65)] [InlineData("stele", int.MaxValue)]
    [InlineData("anomaly", 0)] [InlineData("anomaly", 1)] [InlineData("anomaly", 63)]
    [InlineData("anomaly", 64)] [InlineData("anomaly", 65)] [InlineData("anomaly", int.MaxValue)]
    public void NativeConversionMenuIsFiniteAndRetainsAllSmallAmountsIncludingZero(string kind, int maximum)
    {
        var state = ConversionPosition(kind, maximum); var prefix = Prefix(kind);
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.ActivateAbility);
        var ids = ChoiceIds(JsonSerializer.SerializeToElement(candidate, Web)).Where(id => id.StartsWith(prefix, StringComparison.Ordinal)).Distinct().ToArray();
        if (maximum <= 63) Assert.Equal(Enumerable.Range(0, maximum + 1).Select(n => prefix + n).Order(), ids.Order());
        else Assert.Equal([prefix + "CHOOSE_AMOUNT"], ids);
    }

    [Theory]
    [InlineData("stele", 0)] [InlineData("stele", 1)] [InlineData("stele", 63)]
    [InlineData("stele", 64)] [InlineData("stele", 65)] [InlineData("stele", int.MaxValue)]
    [InlineData("anomaly", 0)] [InlineData("anomaly", 1)] [InlineData("anomaly", 63)]
    [InlineData("anomaly", 64)] [InlineData("anomaly", 65)] [InlineData("anomaly", int.MaxValue)]
    public async Task OrdinaryLargeConversionUsesAtMost31UncommittedDecisionsAndCommitsOnce(string kind, int amount)
    {
        var initial = ConversionPosition(kind, int.MaxValue); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", Native(new ActivateAbilityCommand("SOURCE", Ability(kind), [], [Prefix(kind) + "CHOOSE_AMOUNT"])));
        var decisions = await SelectQuantity(driver, amount, "RESOURCE_CONVERSION_AMOUNT", () => {
            Assert.False(driver.State.CardObjects["SOURCE"].IsExhausted);
            Assert.False(driver.State.PendingRuleChoice!.Request.Preview!.CardObjects["SOURCE"].IsExhausted);
            Assert.Equal(initial.RunePools["P1"], driver.State.PendingRuleChoice.Request.Preview.RunePools["P1"]);
            Assert.DoesNotContain(driver.Events(), e => e.Event.Kind is "RESOURCE_SKILL_RESOLVED" or "EQUIPMENT_EXHAUSTED" or "MANA_GAINED" or "POWER_GAINED");
        });
        Assert.InRange(decisions, 1, 31); Assert.Null(driver.State.PendingRuleChoice);
        Assert.True(driver.State.CardObjects["SOURCE"].IsExhausted);
        Assert.Equal(kind == "stele" ? int.MaxValue - amount : amount, driver.State.RunePools["P1"].Mana);
        Assert.Equal(kind == "stele" ? amount : int.MaxValue - amount, driver.State.RunePools["P1"].TotalPower);
        Assert.Single(driver.Events(), e => e.Event.Kind == "RESOURCE_SKILL_RESOLVED");
        Assert.Single(driver.Events(), e => e.Event.Kind == "EQUIPMENT_EXHAUSTED");
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("stele", 0)] [InlineData("stele", 64)] [InlineData("stele", int.MaxValue)]
    [InlineData("anomaly", 0)] [InlineData("anomaly", 64)] [InlineData("anomaly", int.MaxValue)]
    public async Task ExistingExactNumericCommandsRemainLegalAtLargeBalances(string kind, int amount)
    {
        var initial = ConversionPosition(kind, int.MaxValue); var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", Native(new ActivateAbilityCommand("SOURCE", Ability(kind), [], [Prefix(kind) + amount])));
        Assert.Null(driver.State.PendingRuleChoice); Assert.True(driver.State.CardObjects["SOURCE"].IsExhausted);
        Assert.Equal(kind == "stele" ? amount : int.MaxValue - amount, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task AConversionInsideImposedPaymentKeepsItsQuantityAndTheFinalMovementDecisionSeparate(bool pay)
    {
        var initial = AddBase(WithPool(Position("standard"), new(int.MaxValue, 0)), "STELE", P4ActivatedAbilityCatalog.AncientSteleCardNo);
        var driver = await Open(initial, "standard", "range-payment-" + pay);
        var option = Assert.Single(driver.State.PendingRuleChoice!.Request.Options, o => o.Id.StartsWith("RESOURCE:STELE:", StringComparison.Ordinal));
        Assert.EndsWith("CHOOSE_AMOUNT", option.Id); await Decide(driver, option.Id);
        await SelectQuantity(driver, 1, "RESOURCE_CONVERSION_AMOUNT");
        Assert.Equal("IMPOSED_MOVEMENT_POWER_PAYMENT", driver.State.PendingRuleChoice!.Request.Kind);
        Assert.Equal(int.MaxValue - 1, driver.State.PendingRuleChoice.Request.Preview!.RunePools["P1"].Mana);
        Assert.Equal(1, driver.State.PendingRuleChoice.Request.Preview.RunePools["P1"].TotalPower);
        await Decide(driver, pay ? "PAY" : "DECLINE");
        Assert.Equal(int.MaxValue - 1, driver.State.RunePools["P1"].Mana);
        Assert.Equal(pay ? 0 : 1, driver.State.RunePools["P1"].TotalPower);
        Assert.True(driver.State.CardObjects["STELE"].IsExhausted);
        Assert.Equal(pay ? "FIELD2" : null, driver.State.ObjectLocations["A"].BattlefieldObjectId);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("stele")]
    [InlineData("anomaly")]
    public async Task InputAndOutputCapacityExcludeRestrictedManaAndNeverOverflow(string kind)
    {
        var state = ConversionPosition(kind, 5);
        var pool = kind == "stele" ? new RunePool(5, int.MaxValue - 1, spellOnlyMana: 7)
            : new RunePool(0, 5, spellOnlyMana: int.MaxValue - 1);
        state = WithPool(state, pool);
        var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.ActivateAbility);
        var choices = ChoiceIds(JsonSerializer.SerializeToElement(candidate, Web)).Where(id => id.StartsWith(Prefix(kind), StringComparison.Ordinal)).Distinct();
        Assert.Equal(new[] { Prefix(kind) + "0", Prefix(kind) + "1" }.Order(), choices.Order());
        var rejected = await new CoreRuleEngine().ResolveAsync(state, new("overflow-conversion", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("SOURCE", Ability(kind), [], [Prefix(kind) + "2"]), default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(rejected.State));
        if (kind == "stele")
        {
            var onlyRestricted = WithPool(state, new RunePool(0, 0, spellOnlyMana: int.MaxValue));
            var zero = await new CoreRuleEngine().ResolveAsync(onlyRestricted, new("zero-conversion", "P1", CommandTypes.ActivateAbility),
                new ActivateAbilityCommand("SOURCE", Ability(kind), [], [Prefix(kind) + "0"]), default);
            Assert.True(zero.Accepted, zero.ErrorMessage); Assert.Equal(int.MaxValue, zero.State.RunePools["P1"].SpellOnlyMana);
            Assert.Equal(0, zero.State.RunePools["P1"].TotalPower); Assert.True(zero.State.CardObjects["SOURCE"].IsExhausted);
        }
    }

    [Theory]
    [InlineData("payer")] [InlineData("range")] [InlineData("source")] [InlineData("ability")]
    public async Task ARangeAnswerCannotChangeItsPayerSourceAbilityOrInterval(string branch)
    {
        var driver = new StandbyDefendObserverTests.Driver(ConversionPosition("stele", 65));
        await driver.Act("P1", Native(new ActivateAbilityCommand("SOURCE", Ability("stele"), [], [Prefix("stele") + "CHOOSE_AMOUNT"])));
        var state = driver.State; var request = state.PendingRuleChoice!.Request; var id = request.Options[0].Id;
        id = branch switch { "source" => id.Replace("SOURCE", "OTHER"), "ability" => id.Replace(Ability("stele"), Ability("anomaly")),
            "range" => id + ":FORGED", _ => id };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("forged-range", branch == "payer" ? "P2" : "P1", CommandTypes.PayCost),
            new PayCostCommand(request.Id, "RULE_REPLACEMENT", [id]), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        var options = request.Options.Select((o, i) => i == 0 ? o with { Id = o.Id + ":FORGED" } : o).ToArray();
        var forged = state with { PendingRuleChoice = state.PendingRuleChoice with { Request = request with { Options = options } } };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged)); Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state));
    }

    [Theory]
    [InlineData("energy")] [InlineData("wrong-prefix")] [InlineData("multiple")] [InlineData("small-range")]
    public async Task ASelectionMarkerIsNotAnArbitraryResourceAmountOrAnotherAbility(string branch)
    {
        var state = ConversionPosition("stele", branch == "small-range" ? 63 : 65);
        if (branch == "energy") state = AddBase(state, "CHANNEL", P4ActivatedAbilityCatalog.EnergyChannelCardNo);
        var source = branch == "energy" ? "CHANNEL" : "SOURCE";
        var ability = branch == "energy" ? P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId : Ability("stele");
        var costs = branch == "multiple" ? new[] { Prefix("stele") + "CHOOSE_AMOUNT", Prefix("stele") + "0" }
            : [Prefix(branch == "wrong-prefix" ? "anomaly" : "stele") + "CHOOSE_AMOUNT"];
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-amount-marker", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(source, ability, [], costs), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    internal static async Task<int> SelectQuantity(StandbyDefendObserverTests.Driver driver, int amount, string kind, Action? before = null)
    {
        var count = 0;
        while (driver.State.PendingRuleChoice is { Request: var request } && request.Kind == kind && request.Options.All(o => o.Id.StartsWith("QUANTITY:", StringComparison.Ordinal)))
        {
            before?.Invoke(); Assert.Equal(2, request.Options.Count); Assert.True(++count <= 31);
            var selected = request.Options.Single(option => {
                var pieces = option.Id.Split(':'); var low = int.Parse(pieces[^2]); var high = int.Parse(pieces[^1]); return low <= amount && amount <= high; });
            await Decide(driver, selected.Id);
        }
        return count;
    }
    private static MatchState ConversionPosition(string kind, int amount)
        => AddBase(WithPool(Position("standard"), kind == "stele" ? new(amount, 0) : new(0, amount)), "SOURCE",
            kind == "stele" ? P4ActivatedAbilityCatalog.AncientSteleCardNo : P4ActivatedAbilityCatalog.HextechAnomalyCardNo);
    internal static GameCommand Native(GameCommand command) => GameCommandJsonMapper.Map(JsonSerializer.SerializeToElement(command, command.GetType(), Web));
    private static string Ability(string kind) => kind == "stele" ? P4ActivatedAbilityCatalog.AncientSteleResourceAbilityId : P4ActivatedAbilityCatalog.HextechAnomalyResourceAbilityId;
    private static string Prefix(string kind) => kind == "stele" ? P4ActivatedAbilityCatalog.AncientSteleConversionOptionalCostPrefix : P4ActivatedAbilityCatalog.HextechAnomalyConversionOptionalCostPrefix;
    private static IEnumerable<string> ChoiceIds(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
            foreach (var property in node.EnumerateObject())
            {
                if (property.NameEquals("id") && property.Value.ValueKind == JsonValueKind.String) yield return property.Value.GetString()!;
                foreach (var value in ChoiceIds(property.Value)) yield return value;
            }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var item in node.EnumerateArray()) foreach (var value in ChoiceIds(item)) yield return value;
    }
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}
