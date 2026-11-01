using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AttachedSelfDestructionReplacementTests
{
    public AttachedSelfDestructionReplacementTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualGuardianAngelOnlyReplacesItsActualAttachedHostsDestruction(bool attached)
    {
        var initial = Position();
        var run = new Driver(initial) { RecoverAfterEveryCommand = false };
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE"));
        if (attached) {
            await run.Act("P1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
            Assert.Equal("HOST", run.State.CardObjects["ANGEL"].AttachedToObjectId);
        }
        Assert.Equal(2, run.State.CardObjects["HOST"].Power);
        var snapshot = JsonSerializer.SerializeToElement(ResolutionResult.BuildSnapshots(run.State)["P1"], Json);
        Assert.Equal(attached ? 3 : 2, snapshot.GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("HOST").GetProperty("effectivePower").GetInt32());
        await run.Act("P1", new EndTurnCommand());
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(runes[0], runes));
        await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(2).ToArray()));
        await run.ColdCheckpoint();
        run.RecoverAfterEveryCommand = true;
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", ["HOST"])); await run.Top();
        Export(initial, run, attached);
        Assert.Null(run.State.PendingRuleChoice);
        Assert.Equal(attached, run.State.PlayerZones["P1"].Base.Contains("HOST"));
        Assert.Equal(!attached, run.State.PlayerZones["P1"].Graveyard.Contains("HOST"));
        Assert.Equal(attached, run.State.PlayerZones["P1"].Graveyard.Contains("ANGEL"));
        Assert.Equal(!attached, run.State.PlayerZones["P1"].Base.Contains("ANGEL"));
        if (attached) {
            Assert.True(run.State.CardObjects["HOST"].IsExhausted); Assert.Equal(0, run.State.CardObjects["HOST"].Damage);
            Assert.Equal(1, run.State.CardObjects["HOST"].ObjectGeneration);
        }
        await run.Replay();
    }

    [Theory]
    [InlineData("detached")] [InlineData("equipment-return")] [InlineData("unit-generation")] [InlineData("other-unit")]
    public async Task ActualAttachmentLossOrAnotherUnitCannotReceiveGuardianReplacement(string mode)
    {
        var initial = Position();
        foreach (var (id, no) in new[] { ("DETACH", "SFD·011/221"), ("RETURN-GEAR", "SFD·135/221"),
            ("RETURN-UNIT", "OGN·104/298"), ("OTHER", "OGN·210/298") }) initial = AddInitial(initial, id, no, "P1");
        var run = new Driver(initial) { RecoverAfterEveryCommand = false };
        await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BASE"));
        await run.Act("P1", new PlayCardCommand("ANGEL", "SFD·051/221", [], Destination: "BASE"));
        await run.Act("P1", new AssembleEquipmentCommand("ANGEL", "HOST", ["ASSEMBLE_GREEN"])); await run.Top();
        var generation = run.State.CardObjects["HOST"].ObjectGeneration;
        if (mode == "detached") { await run.Act("P1", new PlayCardCommand("DETACH", "SFD·011/221", ["HOST", "ANGEL"])); await run.Top(); }
        if (mode == "equipment-return") { await run.Act("P1", new PlayCardCommand("RETURN-GEAR", "SFD·135/221", ["ANGEL"])); await run.Top(); }
        if (mode == "unit-generation") {
            await run.Act("P1", new PlayCardCommand("RETURN-UNIT", "OGN·104/298", ["HOST"])); await run.Top();
            await run.Act("P1", new PlayCardCommand("HOST", "OGN·210/298", [], Destination: "BASE"));
            Assert.True(run.State.CardObjects["HOST"].ObjectGeneration > generation);
        }
        if (mode == "other-unit") await run.Act("P1", new PlayCardCommand("OTHER", "OGN·210/298", [], Destination: "BASE"));
        else Assert.Null(run.State.CardObjects["ANGEL"].AttachedToObjectId);
        await run.Act("P1", new EndTurnCommand());
        var runes = Enumerable.Range(1, 9).Select(i => "P2-FUND-" + i).ToArray();
        await run.Act("P2", new TapRuneCommand(runes[0], runes)); await run.Act("P2", new RecycleRuneCommand(runes[0], runes.Take(2).ToArray()));
        await run.ColdCheckpoint();
        run.RecoverAfterEveryCommand = true;
        var target = mode == "other-unit" ? "OTHER" : "HOST";
        await run.Act("P2", new PlayCardCommand("KILL", "OGN·229/298", [target])); await run.Top();
        Assert.Null(run.State.PendingRuleChoice); Assert.Contains(target, run.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain("ANGEL", run.State.PlayerZones["P1"].Graveyard);
        if (mode == "equipment-return") Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Hand);
        else Assert.Contains("ANGEL", run.State.PlayerZones["P1"].Base);
        if (mode == "other-unit") {
            Assert.Contains("HOST", run.State.PlayerZones["P1"].Base); Assert.Equal("HOST", run.State.CardObjects["ANGEL"].AttachedToObjectId);
        }
        await run.Replay(); Export(initial, run, false, mode);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static MatchState Position(string hostNo = "OGN·210/298", bool battlefield = false, bool richOpponent = false)
    {
        var cards = new Dictionary<string, CardObjectState>(); var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string owner, string zone, string? field = null) {
            cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner); locations[id] = new(owner, zone, field);
        }
        Put("HOST", hostNo, "P1", "HAND"); Put("ANGEL", "SFD·051/221", "P1", "HAND"); Put("KILL", "OGN·229/298", "P2", "HAND");
        if (battlefield) {
            Put("HOME", "OGN·278/298", "P1", "BATTLEFIELD", "HOME");
            cards["HOME"] = cards["HOME"] with { ControllerId = "P1" };
        }
        var funding = Enumerable.Range(1, richOpponent ? 18 : 9).Select(i => "P2-FUND-" + i).ToArray(); foreach (var id in funding) Put(id, "UNL-R06", "P2", "BASE");
        var purple = richOpponent ? Enumerable.Range(1, 3).Select(i => "P2-PURPLE-" + i).ToArray() : [];
        foreach (var id in purple) Put(id, "UNL-R05", "P2", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" }) {
            var deck = Enumerable.Range(1, 4).Select(i => player + "-DRAW-" + i).Append("PRIVATE-" + player).ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck, Hand = player == "P1" ? ["HOST", "ANGEL"] : ["KILL"],
                Base = player == "P1" ? [] : [..funding, ..purple], Battlefields = player == "P1" && battlefield ? ["HOME"] : [] };
        }
        return new MatchState("C9-ATTACHED-SELF-DESTRUCTION", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = new(40, 0, new Dictionary<string, int> { [RuneTrait.Green] = 10, [RuneTrait.Yellow] = 10 }), ["P2"] = RunePool.Empty },
            seed: 424242) with { CardPlays = new(5, [], []) };
    }
    private static MatchState AddInitial(MatchState state, string id, string no, string owner, bool rune = false)
        => state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            [id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new(owner, rune ? "BASE" : "HAND") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { [owner] = rune
                ? state.PlayerZones[owner] with { Base = [..state.PlayerZones[owner].Base, id] }
                : state.PlayerZones[owner] with { Hand = [..state.PlayerZones[owner].Hand, id] } } };
    private static void Export(MatchState initial, Driver run, bool attached, string? mode = null)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_C9_ATTACHED_REPLACEMENT_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "guardian-angel-" + (mode ?? attached.ToString()) + ".json"), JsonSerializer.Serialize(new {
            initial, final = run.State, commands = run.Commands(), events = run.Events(), expectedHostRecalled = attached,
            runtime = new { baseDirectory = AppContext.BaseDirectory, enginePath = typeof(CoreRuleEngine).Assembly.Location,
                engineSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(CoreRuleEngine).Assembly.Location))).ToLowerInvariant(),
                engineMvid = typeof(CoreRuleEngine).Assembly.ManifestModule.ModuleVersionId },
            scope = "Explicit canonical-card QA initial; actual play and optional actual assemble, real opposing Vengeance, unchanged command journal/JSON-cold/replay checks."
        }, Json));
    }
}
