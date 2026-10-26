using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven007RequiredDiscardConsumerTests
{
    private const string No = "VEN·007";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCnPrintingKeepsItsRequiredDiscardDistinctFromAFreeResourceCost()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37692, card.Id); Assert.Equal("拳拳魄罗", card.CardName);
        Assert.Equal("{{强化}} — 弃置一张手牌（支付此费用：强化我。仅在未强化时可用。）\n{{已强化>}} 我获得{{S}}+1。", card.CardEffect);
        Assert.Equal(2, card.Energy); Assert.Null(card.ReturnEnergy); Assert.Equal(2, card.Power);
        Assert.Equal([RuneTrait.Red], card.CardColorList); Assert.Equal("魄罗", card.Tag);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal("弃置一张手牌", cost.AdditionalCost); Assert.False(cost.SupportedResources);
        Assert.Equal(0, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower);
        Assert.Equal(1, EmpowerParser.Self(card.CardEffect).Might);
    }

    [Theory]
    [InlineData("OGN·096/298")] [InlineData("VEN·049")] [InlineData("SFD·022/221")]
    public async Task ActualRequiredPaymentDiscardsOneChosenHandCardBeforeEmpowerResolves(string paymentCard)
    {
        var initial = Add(VenUnitEntryBasicTests.Position(No), "FODDER", paymentCard);
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, []));
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            await game.Act("P1", new ActivateAbilityCommand("UNIT", "EMPOWER:0:0", [], OptionalCosts: ["DISCARD_HAND_CARD:FODDER"]));
            Assert.DoesNotContain("FODDER", game.State.PlayerZones["P1"].Hand); Assert.Contains("FODDER", game.State.PlayerZones["P1"].Graveyard);
            Assert.Equal("GRAVEYARD", game.State.ObjectLocations["FODDER"].Zone);
            Assert.Equal(18, game.State.RunePools["P1"].Mana); Assert.Equal(20, game.State.RunePools["P1"].TotalPower);
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Empty(Assert.Single(game.State.StackItems).TargetObjectIds);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(2, game.State.CardObjects["UNIT"].Power);
            Assert.Equal(3, Might(game.State)); Assert.Empty(game.State.CardObjects["UNIT"].UntilEndOfTurnPowerModifiers);
            await game.Replay();
        }
        finally { await Save("required-discard-" + paymentCard.Replace('/', '-'), initial, game); }
    }

    private static MatchState Add(MatchState state, string id, string no) => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, "P1") },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = [..state.PlayerZones["P1"].Hand, id] } } };
    private static int Might(MatchState state) => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)["P1"], Json)
        .GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("UNIT").GetProperty("effectivePower").GetInt32();
    private static async Task Save(string label, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN007_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        var runtime = new[] { typeof(CoreRuleEngine).Assembly, typeof(OfficialCardCatalog).Assembly, typeof(GameCommand).Assembly, Assembly.GetExecutingAssembly() }
            .Select(a => new { name = a.GetName().Name, path = a.Location, mvid = a.ManifestModule.ModuleVersionId,
                infoVersion = a.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant() });
        await File.WriteAllTextAsync(Path.Combine(output, label + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame consumer reproduction, including an original rejected command when unsupported; not canonical-opening or certification evidence",
            initial, final = game.State, commands = game.Commands(), events = game.Events(), runtime, ruleInputs = RuleInputIdentity.Current }, Json));
    }
}
