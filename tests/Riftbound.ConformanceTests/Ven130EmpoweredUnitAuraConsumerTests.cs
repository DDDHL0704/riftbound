using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven130EmpoweredUnitAuraConsumerTests
{
    private const string No = "VEN·130";
    private const string Text = "{{强化3黄色}}（支付{{3}}和{{黄色}}：强化我。仅在未强化时可用。）\n{{已强化>}} 你的{{已强化}}单位获得{{S}}+2（包括我在内）。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task TheExactPrintingAddsOneFunctionalGroupWithoutChangingExistingMembers()
    {
        var catalog = await OfficialCardCatalog.LoadDefaultAsync(); var oldCards = catalog.Cards.Where(c => c.CardNo != No).ToArray();
        Assert.Equal(1083, oldCards.Length); Assert.Equal(1084, catalog.Cards.Count);
        var prior = FunctionalUnitBuilder.Build(oldCards); var current = FunctionalUnitBuilder.Build(catalog.Cards);
        Assert.Equal(864, prior.Count); Assert.Equal(865, current.Count);
        foreach (var unit in prior) Assert.Equal(JsonSerializer.Serialize(unit), JsonSerializer.Serialize(Assert.Single(current, c => c.Id == unit.Id)));
        Assert.Equal(No, Assert.Single(Assert.Single(current, c => c.Cards.Any(card => card.CardNo == No)).Cards).CardNo);
    }

    [Fact]
    public async Task ExactCnSourceSeparatesTypedActivationCostFromTheConditionalFriendlyAura()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37830, card.Id); Assert.Equal("奥洛克将军", card.CardName); Assert.Empty(card.SubTitle); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(5, card.Energy); Assert.Equal(5, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Yellow], card.CardColorList); Assert.Equal(["unit"], card.CardCategoryList);
        Assert.Equal("诺克萨斯", card.Region); Assert.Equal("崔法利", card.Tag);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Equal(1, Assert.Single(cost.TypedPower).Value);
        Assert.Equal(RuneTrait.Yellow, Assert.Single(cost.TypedPower).Key); Assert.True(cost.SupportedResources); Assert.False(cost.Exhaust);
        Assert.Equal(0, EmpowerParser.Self(card.CardEffect).Might);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior)); Assert.True(behavior.PlaysSourceToBaseAsUnit);
        Assert.Equal(5, behavior.ManaCost); Assert.Equal(5, behavior.SourceUnitPower); Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task ActualPlayAndEmpowerPayTheirSeparatePrintedManaAndYellowPower(string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
            Assert.Equal(35, game.State.RunePools["P1"].Mana); Assert.Equal(2, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(5, Might(game.State, "UNIT"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", Activate()); Assert.Equal(32, game.State.RunePools["P1"].Mana);
            Assert.Equal(1, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Yellow]); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            var item = Assert.Single(game.State.StackItems); Assert.Empty(item.TargetObjectIds);
            Assert.Equal(new ObjectBinding("UNIT", generation), item.EmpowerActivation!.Source);
            Assert.Equal(3, item.EmpowerActivation.PaidMana); Assert.Equal(0, item.EmpowerActivation.Keyword); Assert.Equal(0, item.EmpowerActivation.Alternative);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("actual-cost-" + destination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("BASE")] [InlineData("BATTLEFIELD:BF")]
    public async Task TheActuallyEmpoweredSourceReceivesItsOwnCurrentAuraWithoutChangingPrintedMight(string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination)); await game.Act("P1", Activate());
            Assert.Equal(5, Might(game.State, "UNIT")); await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(7, Might(game.State, "UNIT")); Assert.Equal(5, game.State.CardObjects["UNIT"].Power);
            await game.Act("P1", new EndTurnCommand()); Assert.Equal(7, Might(game.State, "UNIT"));
            await game.Act("P2", new EndTurnCommand()); Assert.Equal(7, Might(game.State, "UNIT"));
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("self-aura-" + destination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("BASE", "BASE")] [InlineData("BASE", "BATTLEFIELD:BF")]
    [InlineData("BATTLEFIELD:BF", "BASE")] [InlineData("BATTLEFIELD:BF", "BATTLEFIELD:BF")]
    public async Task CurrentFriendlyEmpoweredUnitsAtEitherFieldLocationGainAndLoseTheAura(string sourceDestination, string allyDestination)
    {
        var initial = Add(Add(Add(Position(), "EMPOWER-ALLY", "VEN·070"), "RETURN", "OGN·104/298"), "PLAIN", "OGN·175/298", "P1", "BASE");
        initial = Add(initial, "EMPOWER-ENEMY", "VEN·070", "P2", "BASE");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["EMPOWER-ENEMY"] = initial.CardObjects["EMPOWER-ENEMY"] with { IsEmpowered = true } } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("EMPOWER-ALLY", "VEN·070", [], Destination: allyDestination));
            await game.Act("P1", new ActivateAbilityCommand("EMPOWER-ALLY", "EMPOWER:0:0", [])); await game.Top();
            Assert.True(game.State.CardObjects["EMPOWER-ALLY"].IsEmpowered); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            Assert.Equal(6, Might(game.State, "EMPOWER-ENEMY", "P2"));
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: sourceDestination));
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); await game.Act("P1", Activate());
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); await game.Top();
            Assert.Equal(8, Might(game.State, "EMPOWER-ALLY")); Assert.Equal(7, Might(game.State, "UNIT"));
            Assert.Equal(3, Might(game.State, "PLAIN")); Assert.Equal(6, Might(game.State, "EMPOWER-ENEMY", "P2"));
            var generation = game.State.CardObjects["UNIT"].ObjectGeneration;
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top();
            Assert.Equal(generation + 1, game.State.CardObjects["UNIT"].ObjectGeneration); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            await game.Act("P1", Activate()); await game.Top(); Assert.Equal(8, Might(game.State, "EMPOWER-ALLY"));
            Assert.Equal(generation + 2, game.State.CardObjects["UNIT"].ObjectGeneration);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("ally-aura-" + sourceDestination.Replace(':', '-') + "-" + allyDestination.Replace(':', '-'), initial, game); }
    }

    [Theory]
    [InlineData("unempowered")] [InlineData("pending")] [InlineData("returned")]
    public async Task InactivePendingOrReturnedSourceCannotGrantItsDependentAura(string branch)
    {
        var initial = Add(Add(Position(), "EMPOWER-ALLY", "VEN·070"), "RETURN", "OGN·104/298"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("EMPOWER-ALLY", "VEN·070", []));
            await game.Act("P1", new ActivateAbilityCommand("EMPOWER-ALLY", "EMPOWER:0:0", [])); await game.Top();
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY"));
            if (branch != "unempowered") { await game.Act("P1", Activate()); Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); }
            if (branch == "returned")
            {
                var paidMana = game.State.RunePools["P1"].Mana;
                await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["UNIT"])); await game.Top(); await game.Top();
                Assert.Equal(paidMana - 1, game.State.RunePools["P1"].Mana); Assert.Contains("UNIT", game.State.PlayerZones["P1"].Hand);
                Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(game.State.StackItems);
            }
            Assert.Equal(6, Might(game.State, "EMPOWER-ALLY")); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Replay(); Restore(game.State);
        }
        finally { await Save("inactive-source-" + branch, initial, game); }
    }

    private static ActivateAbilityCommand Activate() => new("UNIT", "EMPOWER:0:0", []);
    private static MatchState Position() => VenUnitEntryBasicTests.Position(No) with { RunePools = new Dictionary<string, RunePool> {
        ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Yellow] = 2, [RuneTrait.Orange] = 2, [RuneTrait.Blue] = 2 }), ["P2"] = new(20, 20) } };
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1", string zone = "HAND") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, zone) },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = zone == "BASE"
            ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
            : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static int Might(MatchState state, string id, string owner = "P1")
        => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json).GetProperty("players").GetProperty(owner)
            .GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static void Restore(MatchState state)
    {
        var restored = OfficialInsightAndSpellLockTests.Restore(state);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(restored));
    }
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN130_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Exact CN VEN130 explicit midgame actual play and resource-Empower consumer; source applicability, L2/L3 and whole-card certification remain OPEN",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
