using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class Ven055EmpoweredSpellDiscountConsumerTests
{
    private const string No = "VEN·055";
    private const string Text = "{{强化3}}（支付{{3}}：强化我。仅在未强化时可用。）\n{{已强化>}} 你的法术费用减少{{1}}和{{A}}，不得低于{{1}}。";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public async Task ExactCnPrintingSeparatesFourManaFourMightFromTheThreeManaEmpowerAndConditionalSpellDiscount()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadDefaultAsync()).Cards, c => c.CardNo == No);
        Assert.Equal(37746, card.Id); Assert.Equal("实干研究员", card.CardName); Assert.Equal(Text, card.CardEffect);
        Assert.Equal(4, card.Energy); Assert.Equal(4, card.Power); Assert.Null(card.ReturnEnergy);
        Assert.Equal([RuneTrait.Blue], card.CardColorList); Assert.Equal("恕瑞玛", card.Region);
        Assert.True(CardBehaviorRegistry.TryGetByCardNo(No, out var behavior));
        Assert.True(behavior.PlaysSourceToBaseAsUnit); Assert.Equal(4, behavior.ManaCost); Assert.Equal(4, behavior.SourceUnitPower);
        Assert.Equal(0, PrintedPowerCostRules.ForCard(No).Amount);
        var cost = Assert.Single(Assert.Single(EmpowerParser.Parse(card.CardEffect)).Alternatives);
        Assert.Equal(3, cost.Mana); Assert.Equal(0, cost.Power); Assert.Empty(cost.TypedPower); Assert.True(cost.SupportedResources);
    }

    [Theory]
    [InlineData(false, "BASE")] [InlineData(true, "BASE")]
    [InlineData(false, "BATTLEFIELD:BF")] [InlineData(true, "BATTLEFIELD:BF")]
    public async Task ActualEmpowerEnablesBothSpellDiscountComponentsInBaseOrBattlefield(bool empower, string destination)
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [], Destination: destination));
            Assert.Equal(36, game.State.RunePools["P1"].Mana); Assert.Equal(4, Might(game.State, "UNIT"));
            if (empower) { await game.Act("P1", Activate()); await game.Top(); }
            Assert.Equal(empower, game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(4, Might(game.State, "UNIT"));
            var before = game.State.RunePools["P1"];
            await game.Act("P1", Smoke());
            Assert.Equal(before.Mana - (empower ? 1 : 2), game.State.RunePools["P1"].Mana);
            Assert.Equal(before.PowerByTrait[RuneTrait.Blue] - (empower ? 0 : 1), game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(4, Might(game.State, "ENEMY", "P2")); Restore(initial, game);
            await game.Top(); Assert.Equal(1, Might(game.State, "ENEMY", "P2"));
            Assert.Equal(4, Might(game.State, "UNIT")); Assert.Contains("SPELL", game.State.PlayerZones["P1"].Graveyard);
            await game.Replay(); Restore(initial, game);
        }
        finally { await Save("mana-power-" + empower + "-" + destination.Replace(':', '-'), initial, game); }
    }

    [Fact]
    public async Task AReactionCastBeforeEmpowerResolvesStillPaysItsFullPrintedCost()
    {
        var initial = Position(); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate());
            Assert.False(game.State.CardObjects["UNIT"].IsEmpowered); Assert.Equal(33, game.State.RunePools["P1"].Mana);
            await game.Act("P1", Smoke()); Assert.Equal(31, game.State.RunePools["P1"].Mana);
            Assert.Equal(3, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            await game.Top(); Assert.False(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(1, Might(game.State, "ENEMY", "P2")); Restore(initial, game);
            await game.Top(); Assert.True(game.State.CardObjects["UNIT"].IsEmpowered);
            Assert.Equal(31, game.State.RunePools["P1"].Mana); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("pending-empower-no-discount", initial, game); }
    }

    [Fact]
    public async Task TheOneManaFloorDoesNotPreventTheBluePowerCostFromBecomingZero()
    {
        var initial = Position("SFD·080/221"); var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            await game.Act("P1", new PlayCardCommand("SPELL", "SFD·080/221", ["ENEMY"]));
            Assert.Equal(32, game.State.RunePools["P1"].Mana); Assert.Equal(4, game.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
            Assert.Equal(0, game.State.CardObjects["ENEMY"].Damage); await game.Top();
            Assert.Equal(1, game.State.CardObjects["ENEMY"].Damage); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("floor-one-power-zero", initial, game); }
    }

    [Theory]
    [InlineData(8)] [InlineData(40)]
    public async Task ActualEmpowerAllowsARealBlueSpellWithNoPowerInThePool(int initialMana)
    {
        var initial = Position() with { RunePools = new Dictionary<string, RunePool> { ["P1"] = new(initialMana, 0), ["P2"] = new(20, 20) } };
        var game = new Driver(initial);
        try
        {
            await game.Act("P1", new PlayCardCommand("UNIT", No, [])); await game.Act("P1", Activate()); await game.Top();
            Assert.Equal(initialMana - 7, game.State.RunePools["P1"].Mana); Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
            await game.Act("P1", Smoke()); Assert.Equal(initialMana - 8, game.State.RunePools["P1"].Mana);
            Assert.Equal(0, game.State.RunePools["P1"].TotalPower); await game.Top();
            Assert.Equal(1, Might(game.State, "ENEMY", "P2")); await game.Replay(); Restore(initial, game);
        }
        finally { await Save("no-power-pool-" + initialMana, initial, game); }
    }

    private static ActivateAbilityCommand Activate(string id = "UNIT") => new(id, "EMPOWER:0:0", []);
    private static PlayCardCommand Smoke() => new("SPELL", "OGN·093/298", ["ENEMY"]);
    private static MatchState Position(string spell = "OGN·093/298") => Add(VenUnitEntryBasicTests.Position(No), "SPELL", spell) with {
        RunePools = new Dictionary<string, RunePool> { ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Blue] = 4 }), ["P2"] = new(20, 20) } };
    private static MatchState Add(MatchState state, string id, string no, string owner = "P1") => state with {
        CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
        ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, "HAND") },
        PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static int Might(MatchState state, string id, string owner = "P1") => JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(state)[owner], Json)
        .GetProperty("players").GetProperty(owner).GetProperty("objects").GetProperty(id).GetProperty("effectivePower").GetInt32();
    private static void Restore(MatchState initial, Driver game)
    {
        var state = game.State; var sequence = game.Commands().Last().CompletedEventSequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(p => p, p => new RecoveredPlayerView(p, state.Tick, sequence, snapshots[p], state.Tick, sequence, prompts[p]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        var errors = MatchRecoveryValidator.Validate(state.RoomId, sequence, game.Commands(), game.Events(), views, state, state.Tick, spectator); Assert.Empty(errors);
        var frame = new MatchRecoveryFrame(state.RoomId, state.Tick, sequence, game.Commands(), game.Events(), views, errors, state, initial, spectator) { OpeningSeed = initial.Seed };
        var decoded = JsonSerializer.Deserialize<MatchRecoveryFrame>(JsonSerializer.Serialize(frame, Json), Json)!;
        var restored = MatchSession.Restore(decoded, new CoreRuleEngine(), NoopMatchJournal.Instance);
        foreach (var player in state.Seats.Keys)
        {
            Assert.Equal(MatchStateHasher.HashValue(snapshots[player]), MatchStateHasher.HashValue(restored.SnapshotFor(player)));
            Assert.Equal(MatchStateHasher.HashValue(prompts[player]), MatchStateHasher.HashValue(restored.PromptFor(player)));
        }
    }
    private static async Task Save(string name, MatchState initial, Driver game)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_VEN055_EVIDENCE") is not { Length: > 0 } output) return;
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(new {
            scope = "Explicit midgame actual play and Empower commands; not canonical opening or whole-card certification",
            initial, final = game.State, commands = game.Commands(), events = game.Events() }, Json));
    }
}
