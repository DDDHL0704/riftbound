using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed partial class VenBorrowedDependentAbilityFaqTests
{
    private const string ConversionPrefix = "BORROWED_CONVERSION:";

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 5)]
    public async Task BorrowedConversionLimitsOutputByAllStoredRestrictedAndUnrestrictedResources(bool manaToPower, int maximum)
    {
        var game = await WithTemplate(manaToPower ? "SFD·117/221" : "SFD·083/221", "EQUIPMENT", pool: new(int.MaxValue, int.MaxValue));
        var row = Assert.Single(ResourceRows(game.State));
        var id = (string)row["abilityId"]!;
        Assert.Equal(Enumerable.Range(0, maximum + 1).Select(n => ConversionPrefix + n),
            ((IReadOnlyList<ActionPromptChoiceDto>)row["optionalCostChoices"]!).Select(c => c.Id));
        var old = game.State;
        var bad = await new CoreRuleEngine().ResolveAsync(old, new("borrowed-convert-overflow", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + (maximum + 1)]), default);
        Assert.False(bad.Accepted);
        Assert.Empty(bad.Events);
        Assert.Equal(MatchStateHasher.Hash(old), MatchStateHasher.Hash(bad.State));
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + maximum]));
        Assert.True(game.State.RunePools["P1"].HasSupportedAmounts);
        Assert.Equal(int.MaxValue, manaToPower ? game.State.RunePools["P1"].TotalStoredPower : game.State.RunePools["P1"].TotalStoredMana);
        await game.Replay();
    }

    [Fact]
    public async Task BorrowedConversionCopiedThroughEquipmentStillUsesTheUnitsPaymentPurpose()
    {
        var game = await WithTemplate("SFD·083/221", "EQUIPMENT", pool: new(20, 1, equipmentOnlyPower: 3, spellOnlyPower: 2));
        await game.Act("P1", new AssembleEquipmentCommand("GEAR", "HOST", ["ASSEMBLE_GREEN"]));
        await game.Top();
        Assert.NotNull(game.State.CardObjects["GEAR"].AttachedRuleText);
        var rows = ResourceRows(game.State);
        Assert.Equal(2, rows.Length);
        var row = rows[^1];
        var before = game.State.RunePools["P1"];
        Assert.Equal(0, before.Power);
        Assert.Equal(ConversionPrefix + "0", Assert.Single((IReadOnlyList<ActionPromptChoiceDto>)row["optionalCostChoices"]!).Id);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
            new("copied-conversion-restricted", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", (string)row["abilityId"]!, [], [ConversionPrefix + "2"]), default);
        Assert.False(rejected.Accepted);
        Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(rejected.State));
        await game.Act("P1", new ActivateAbilityCommand("HOST", (string)row["abilityId"]!, [], [ConversionPrefix + "0"]));
        Assert.Equal(before, game.State.RunePools["P1"]);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 3)]
    [InlineData(false, 0)]
    [InlineData(false, 3)]
    public async Task BorrowedConversionRetainsZeroAndUsesTheBorrowerForPayment(bool manaToPower, int amount)
    {
        var game = await WithTemplate(manaToPower ? "SFD·117/221" : "SFD·083/221", "EQUIPMENT", pool: new(8, 4));
        var row = Assert.Single(ResourceRows(game.State));
        Assert.True((bool)row["resolvesImmediately"]!);
        var id = (string)row["abilityId"]!;
        var choices = (IReadOnlyList<ActionPromptChoiceDto>)row["optionalCostChoices"]!;
        Assert.Equal(Enumerable.Range(0, 4).Select(n => ConversionPrefix + n), choices.Select(c => c.Id));
        var before = game.State;
        Assert.Equal(3, before.RunePools["P1"].Mana);
        Assert.Equal(3, before.RunePools["P1"].Power);
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + amount]));
        var old = before.RunePools["P1"];
        Assert.Equal(old with { Mana = old.Mana + (manaToPower ? -amount : amount),
            Power = old.Power + (manaToPower ? amount : -amount) }, game.State.RunePools["P1"]);
        Assert.Empty(game.State.StackItems);
        Assert.Equal(before.TimingState, game.State.TimingState);
        Assert.Equal(before.PriorityPlayerId, game.State.PriorityPlayerId);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.True(game.State.CardObjects["DONOR"].IsExhausted);
        await RejectBorrowedResource(game.State, "HOST", id, []);
        await game.Replay();
    }

    [Theory]
    [InlineData(true, "missing")]
    [InlineData(true, "negative")]
    [InlineData(true, "unpayable")]
    [InlineData(true, "multiple")]
    [InlineData(true, "marker")]
    [InlineData(true, "wrong-prefix")]
    [InlineData(false, "missing")]
    [InlineData(false, "negative")]
    [InlineData(false, "unpayable")]
    [InlineData(false, "multiple")]
    [InlineData(false, "marker")]
    [InlineData(false, "wrong-prefix")]
    public async Task BorrowedConversionRejectsAmountsOutsideItsAuthorizedMenu(bool manaToPower, string boundary)
    {
        var game = await WithTemplate(manaToPower ? "SFD·117/221" : "SFD·083/221", "EQUIPMENT", pool: new(8, 4));
        var id = (string)Assert.Single(ResourceRows(game.State))["abilityId"]!;
        IReadOnlyList<string> costs = boundary switch {
            "missing" => [], "negative" => [ConversionPrefix + "-1"], "unpayable" => [ConversionPrefix + "4"],
            "multiple" => [ConversionPrefix + "0", ConversionPrefix + "1"],
            "marker" => [ConversionPrefix + "CHOOSE_AMOUNT"], _ => ["HEXTECH_ANOMALY_CONVERT:1"] };
        var before = MatchStateHasher.Hash(game.State);
        var rejected = await new CoreRuleEngine().ResolveAsync(game.State, new("borrowed-convert-invalid", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [], costs), default);
        Assert.False(rejected.Accepted);
        Assert.Empty(rejected.Events);
        Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BorrowedConversionUsesBoundedQuantityWithoutPayingDuringSelection(bool manaToPower)
    {
        var game = await WithTemplate(manaToPower ? "SFD·117/221" : "SFD·083/221", "EQUIPMENT", pool: new(70, 66));
        var row = Assert.Single(ResourceRows(game.State));
        var id = (string)row["abilityId"]!;
        Assert.Equal(ConversionPrefix + "CHOOSE_AMOUNT", Assert.Single((IReadOnlyList<ActionPromptChoiceDto>)row["optionalCostChoices"]!).Id);
        var old = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + "CHOOSE_AMOUNT"]));
        var count = 0;
        while (game.State.PendingRuleChoice is { } pending)
        {
            Assert.Equal("RESOURCE_CONVERSION_AMOUNT", pending.Request.Kind);
            Assert.Equal(old, game.State.RunePools["P1"]);
            Assert.False(game.State.CardObjects["HOST"].IsExhausted);
            Assert.True(++count <= 7);
            var option = pending.Request.Options.Single(o => {
                var pieces = o.Id.Split(':'); return int.Parse(pieces[^2]) <= 64 && int.Parse(pieces[^1]) >= 64; });
            var bad = await new CoreRuleEngine().ResolveAsync(game.State, new("borrowed-quantity-forged", "P2", CommandTypes.PayCost),
                new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]), default);
            Assert.False(bad.Accepted);
            Assert.Equal(MatchStateHasher.Hash(game.State), MatchStateHasher.Hash(bad.State));
            await game.Act("P1", new PayCostCommand(pending.Request.Id, "RULE_REPLACEMENT", [option.Id]));
        }
        Assert.Equal(old with { Mana = old.Mana + (manaToPower ? -64 : 64),
            Power = old.Power + (manaToPower ? 64 : -64) }, game.State.RunePools["P1"]);
        Assert.True(game.State.CardObjects["HOST"].IsExhausted);
        Assert.Empty(game.State.StackItems);
        await game.Replay();
    }

    [Theory]
    [InlineData("spell-mana")]
    [InlineData("unit-mana")]
    [InlineData("restricted-power")]
    public async Task BorrowedConversionPreservesTheActualBorrowersResourceRestrictions(string kind)
    {
        var pool = kind switch {
            "spell-mana" => new RunePool(3, 1, spellOnlyMana: 8),
            "unit-mana" => new RunePool(2, 1, unitPlayOrUnitAbilityOnlyMana: 6),
            _ => new RunePool(8, 1, equipmentOnlyPower: 5, spellOnlyPower: 7) };
        var game = await WithTemplate(kind == "restricted-power" ? "SFD·083/221" : "SFD·117/221", "EQUIPMENT", pool: pool);
        var row = Assert.Single(ResourceRows(game.State));
        var id = (string)row["abilityId"]!;
        var before = game.State;
        var amount = kind == "unit-mana" ? 3 : 1;
        var result = await new CoreRuleEngine().ResolveAsync(before, new("borrowed-restricted-conversion", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + amount]), default);
        if (kind != "unit-mana")
        {
            Assert.False(result.Accepted);
            Assert.Equal(MatchStateHasher.Hash(before), MatchStateHasher.Hash(result.State));
            Assert.Equal(ConversionPrefix + "0", Assert.Single((IReadOnlyList<ActionPromptChoiceDto>)row["optionalCostChoices"]!).Id);
        }
        else
        {
            await game.Act("P1", new ActivateAbilityCommand("HOST", id, [], [ConversionPrefix + amount]));
            Assert.Equal(before.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana - 3, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
            Assert.Equal(before.RunePools["P1"].Power + 3, game.State.RunePools["P1"].Power);
            await game.Replay();
        }
    }
}
