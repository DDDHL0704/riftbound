using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ColoredHeroActivationFeeTests
{
    public static IEnumerable<object[]> Printings() => new[] { "UNL-026/219", "UNL-030/219", "UNL-030a/219" }
        .Select(no => new object[] { no });
    internal static RunePool RedPool(int mana, int red, int generic = 0)
        => new(mana, generic, new Dictionary<string, int> { [RuneTrait.Red] = red });
    private static bool Xerath(string no) => no == "UNL-026/219";
    private static string Ability(string no) => Xerath(no)
        ? P4ActivatedAbilityCatalog.XerathDamageAbilityId : P4ActivatedAbilityCatalog.ViDoublePowerAbilityId;
    private static ActivateAbilityCommand Command(string no, IReadOnlyList<string>? resources = null)
        => new("HOST", Ability(no), Xerath(no) ? ["ENEMY"] : [], resources);
    private static MatchState Position(string no, RunePool pool, bool ward = false)
    {
        var initial = ArmamentMightTests.Position(unit: no, power: 3);
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["ENEMY"] = new("ENEMY", cardNo: "OGN·219/298", power: 10, ownerId: "P2", controllerId: "P2",
                tags: ward ? [CardObjectTags.UnitCard, CardObjectTags.Spellshield] : [CardObjectTags.UnitCard]) },
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = pool } };
        return LocalDestructionRecallTests.At(LocalDestructionRecallTests.At(initial, "HOST", "P1", "BATTLEFIELD", "F"),
            "ENEMY", "P2", "BASE");
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task EveryPrintingRequiresRedAndPaysItBeforeBothPlayersCanRespond(string no)
    {
        var initial = Position(no, RedPool(Xerath(no) ? 0 : 2, 1, 2));
        var requirement = ResolutionResult.BuildPrompts(initial)["P1"].Candidates!
            .Where(c => c.Action == CommandTypes.ActivateAbility)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Single(r => Equals(r["sourceObjectId"], "HOST") && Equals(r["abilityId"], Ability(no)));
        Assert.Equal(0, requirement["powerCost"]);
        Assert.Equal(1, Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(requirement["powerCostByTrait"])[RuneTrait.Red]);
        var game = new Driver(initial); await game.Act("P1", Command(no));
        Assert.Equal(0, game.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(RuneTrait.Red));
        Assert.Equal(2, game.State.RunePools["P1"].Power);
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(Xerath(no), game.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(3, game.State.CardObjects["HOST"].Power); Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload;
        Assert.Equal(1, paid["power"]); Assert.Equal(1, paid["totalPowerCost"]);
        OfficialInsightAndSpellLockTests.Restore(game.State);
        await game.Top();
        Assert.Equal(Xerath(no) ? 3 : 6, game.State.CardObjects["HOST"].Power);
        Assert.Equal(Xerath(no) ? 3 : 0, game.State.CardObjects["ENEMY"].Damage);
        await game.Replay();
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task OtherColoredPowerCannotReplaceThePrintedRedCost(string no)
    {
        var initial = Position(no, new(Xerath(no) ? 0 : 2, 0,
            new Dictionary<string, int> { [RuneTrait.Blue] = 20 }));
        await RadGearTestPosition.Reject(initial, "P1", Command(no));
        Assert.False(initial.CardObjects["HOST"].IsExhausted);
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task WildcardPowerStillPaysTheColoredFeeUnderTheSharedRule(string no)
    {
        // CN 163.2.b: wildcard power can pay any domain's power cost.
        var game = new Driver(Position(no, new(Xerath(no) ? 0 : 2, 1)));
        await game.Act("P1", Command(no));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Single(game.State.StackItems); await game.Top(); await game.Replay();
    }

    [Theory, MemberData(nameof(Printings))]
    public async Task RecyclingARealRedRunePaysTheSameColoredCostInline(string no)
    {
        var initial = Position(no, RedPool(Xerath(no) ? 0 : 2, 0));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["RED"] = OfficialGarenConquestBattlefieldCountTests.Printed("RED", "OGN·007/298", "P1") } };
        initial = LocalDestructionRecallTests.At(initial, "RED", "P1", "BASE");
        var game = new Driver(initial); await game.Act("P1", Command(no, ["RECYCLE_RUNE:RED"]));
        Assert.Contains("RED", game.State.PlayerZones["P1"].RuneDeck);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains(game.Events(), e => e.Event.Kind == "RUNE_RECYCLED");
        Assert.Single(game.State.StackItems); await game.Top(); await game.Replay();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task XerathsRedFeeAndEnemyWardAreSeparateRequirements(bool extraPower)
    {
        const string no = "UNL-026/219";
        var initial = Position(no, RedPool(0, 1, extraPower ? 1 : 0), ward: true);
        if (!extraPower) { await RadGearTestPosition.Reject(initial, "P1", Command(no)); return; }
        var game = new Driver(initial); await game.Act("P1", Command(no));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        var paid = Assert.Single(game.Events(), e => e.Event.Kind == "COST_PAID").Event.Payload;
        Assert.Equal(2, paid["power"]); Assert.Equal(2, paid["totalPowerCost"]); Assert.Equal(1, paid["spellshieldTaxPower"]);
        Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage);
        await game.Top(); Assert.Equal(3, game.State.CardObjects["ENEMY"].Damage); await game.Replay();
    }
}
