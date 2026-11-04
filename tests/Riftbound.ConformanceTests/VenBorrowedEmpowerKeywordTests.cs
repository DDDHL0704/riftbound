using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;
using static Riftbound.ConformanceTests.RadGearTestPosition;

namespace Riftbound.ConformanceTests;

public sealed class VenBorrowedEmpowerKeywordTests
{
    private static MatchState Position(string donor = "VEN·075", string printing = "OGN·111/298", string field = "OGN·296/298")
    {
        var state = Put(Put(Create(field), "BORROWER", printing, "P1", "BASE"), "DONOR", donor, "P1", "BASE");
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        return state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["DONOR"] = state.CardObjects["DONOR"] with { IsExhausted = true, IsEmpowered = true } } };
    }

    private static string[] Skills(MatchState state)
        => (ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [])
            .Where(c => c.Action == CommandTypes.ActivateAbility && c.Metadata?.GetValueOrDefault("sourceRequirements")
                is IEnumerable<IReadOnlyDictionary<string, object?>>)
            .SelectMany(c => (IEnumerable<IReadOnlyDictionary<string, object?>>)c.Metadata!["sourceRequirements"]!)
            .Where(r => Equals(r.GetValueOrDefault("sourceObjectId"), "BORROWER")
                && r.GetValueOrDefault("abilityId") is string id && id.Contains(":BORROWED:", StringComparison.Ordinal))
            .Select(r => (string)r["abilityId"]!).ToArray();

    [Theory]
    [InlineData("VEN·054", "OGN·111/298", 0)]
    [InlineData("VEN·075", "OGN·111/298", 1)]
    [InlineData("VEN·087", "OGN·111/298", 0)]
    [InlineData("VEN·054", "ARC-003/006", 0)]
    [InlineData("VEN·075", "ARC-003/006", 1)]
    [InlineData("VEN·087", "ARC-003/006", 0)]
    public async Task KeywordBorrowsItsCompleteCostAndEmpowersOnlyTheBorrower(string donor, string printing, int mana)
    {
        var state = Position(donor, printing);
        var id = Assert.Single(Skills(state));
        await Reject(state, "P2", new ActivateAbilityCommand("BORROWER", id, []));
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, ["BORROWER"]));
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, [], OptionalCosts: ["FORGED"]));
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", id, []));
        Assert.True(game.State.CardObjects["BORROWER"].IsExhausted);
        Assert.False(game.State.CardObjects["BORROWER"].IsEmpowered);
        Assert.Equal(state.RunePools["P1"].Mana - mana, game.State.RunePools["P1"].Mana);
        Assert.Equal(state.RunePools["P1"].TotalPower, game.State.RunePools["P1"].TotalPower);
        var item = Assert.Single(game.State.StackItems);
        var context = item.EmpowerActivation!;
        Assert.Equal(donor, context.CardNo);
        Assert.Equal(mana, context.PaidMana);
        Assert.Equal("BORROWER", context.Borrowed!.Borrower.Object.ObjectId);
        Assert.Equal("DONOR", context.Borrowed.Donor.Object.ObjectId);
        Assert.Empty(item.TargetObjectIds);
        foreach (var forged in new[] { context with { Borrowed = null },
            context with { Borrowed = context.Borrowed with { Donor = context.Borrowed.Donor with { Object = new("DONOR", 999) } } },
            context with { Borrowed = context.Borrowed with { Borrower = context.Borrowed.Borrower with { CardNo = donor } } },
            context with { PaidMana = context.PaidMana + 1 } })
            Assert.Contains("invalid captured empower activation", OfficialInsightAndSpellLockTests.Errors(game.State with {
                StackItems = [item with { EmpowerActivation = forged }] }));
        await game.Top();
        Assert.True(game.State.CardObjects["BORROWER"].IsEmpowered);
        Assert.Equal(state.CardObjects["DONOR"], game.State.CardObjects["DONOR"]);
        Assert.Empty(Skills(game.State));
        Assert.Single(game.Events(), e => e.Event.Kind == "OBJECT_EMPOWERED");
        await game.Replay();
    }

    [Theory]
    [InlineData("empowered")]
    [InlineData("exhausted")]
    [InlineData("enemy")]
    [InlineData("hand")]
    [InlineData("no-exhaust")]
    public Task RestrictionsCheckTheBorrowerAndEligiblePublicProvider(string branch)
    {
        var state = Position(branch == "no-exhaust" ? "VEN·014" : "VEN·075");
        if (branch == "hand") state = RadUnitEffectTestFixture.Place(state, "DONOR", "P1", "HAND");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["BORROWER"] = state.CardObjects["BORROWER"] with {
                IsEmpowered = branch == "empowered", IsExhausted = branch == "exhausted" },
            ["DONOR"] = state.CardObjects["DONOR"] with { ControllerId = branch == "enemy" ? "P2" : "P1" } } };
        Assert.Empty(Skills(state));
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnUnpaidFullCostRejectsWithoutExhaustingEitherCard()
    {
        var state = Position();
        var id = Assert.Single(Skills(state));
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty } };
        await Reject(state, "P1", new ActivateAbilityCommand("BORROWER", id, []));
        Assert.False(state.CardObjects["BORROWER"].IsExhausted);
    }

    [Theory]
    [InlineData("BORROWER", false)]
    [InlineData("BORROWER", true)]
    public async Task ResponseDepartureKeepsTheCapturedSkillAndNeverEmpowersANewIncarnation(string returned, bool token)
    {
        var state = Position();
        if (token)
        {
            var source = state.CardObjects["BORROWER"];
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["BORROWER"] = source with { TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
                    CopyFaceTransitions = [new(0, source.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, source.CardNo!)] } } };
        }
        var game = new Driver(state);
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", Assert.Single(Skills(state)), []));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [returned]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        var context = item.EmpowerActivation!;
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("BORROWER"));
            var borrower = context.Borrowed!.Borrower;
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(borrower.CeasedSource);
            foreach (var forged in new CeasedSelfAbilitySource?[] { null, receipt with { DepartedTick = game.State.Tick + 1 },
                receipt with { Source = new("BORROWER", receipt.Source.Generation + 1) } })
                Assert.Contains("invalid captured empower activation", OfficialInsightAndSpellLockTests.Errors(game.State with {
                    StackItems = [item with { EmpowerActivation = context with { Borrowed = context.Borrowed with {
                        Borrower = borrower with { CeasedSource = forged } } } }] }));
        }
        else Assert.Contains(returned, game.State.PlayerZones["P1"].Hand);
        var paid = game.State.RunePools["P1"];
        await game.Top();
        Assert.Equal(paid, game.State.RunePools["P1"]);
        Assert.False(game.State.CardObjects.GetValueOrDefault("BORROWER")?.IsEmpowered == true);
        await game.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmpowerDiscountsUseTheBorrowersLocationAndType(bool atAltar)
    {
        var state = Position(field: atAltar ? "VEN·163" : "SFD·208/221");
        if (atAltar) state = RadUnitEffectTestFixture.Place(state, "BORROWER", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(state);
        var id = Assert.Single(Skills(state));
        Assert.Equal(atAltar, id.EndsWith(":REDUCE_MANA", StringComparison.Ordinal));
        await game.Act("P1", new ActivateAbilityCommand("BORROWER", id, []));
        var context = Assert.Single(game.State.StackItems).EmpowerActivation!;
        Assert.Equal(atAltar ? 0 : 1, context.PaidMana);
        Assert.Equal(atAltar, context.BattlefieldReduction is not null);
        Assert.Null(context.EquipmentManaReduction);
        await game.Top();
        Assert.True(game.State.CardObjects["BORROWER"].IsEmpowered);
        await game.Replay();
    }
}
