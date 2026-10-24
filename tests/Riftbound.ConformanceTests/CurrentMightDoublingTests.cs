using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Xunit.Abstractions;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN433.1/479.4b-d: snapshot the composed current Might at resolution.
// These are explicit midgame openings, followed only by ordinary commands.
public sealed class CurrentMightDoublingTests(ITestOutputHelper output)
{
    private const string DoubleCard = "OGN·069/298";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly string[] Funding = Enumerable.Range(1, 6).Select(i => "MANA-" + i)
        .Concat(Enumerable.Range(1, 3).Select(i => "GREEN-" + i))
        .Concat(Enumerable.Range(1, 3).Select(i => "RED-" + i)).ToArray();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task RealEquipmentContributesToEachRememberedDoubleAndExpiresAtTurnEnd(
        bool ability, bool equipped, bool alternate)
    {
        var initial = Opening(ability, alternate);
        var run = await Prepare(initial, equipped);
        var before = equipped ? 4 : 3;
        AssertMight(run.State, before);
        await RejectPrivateTarget(run, ability);
        await Double(run, ability, "DOUBLE-1");
        Save($"repeat-{ability}-{equipped}-{alternate}-first", initial, run);
        AssertMight(run.State, before * 2);
        Assert.Equal(before, Assert.Single(run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers).PowerDelta);
        await Double(run, ability, "DOUBLE-2");
        Save($"repeat-{ability}-{equipped}-{alternate}-second", initial, run);
        AssertMight(run.State, before * 4);
        Assert.Equal(new[] { before, before * 2 }, run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers.Select(m => m.PowerDelta));

        await run.Act("P1", new EndTurnCommand());
        Assert.Equal(3, run.State.CardObjects["TARGET"].Power);
        AssertMight(run.State, before);
        Assert.Empty(run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers);
        await run.Replay();
        Save($"repeat-{ability}-{equipped}-{alternate}-expired", initial, run);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualReactionChangesTheValueBeforeThePendingDoubleResolves(bool ability)
    {
        var initial = Opening(ability);
        var run = await Prepare(initial, true);
        AssertMight(run.State, 4);
        await run.Act("P1", DoubleCommand(ability, "DOUBLE-1"));
        await run.Act("P1", new PlayCardCommand("BUFF", "OGN·058/298", ["TARGET"]));
        await run.Top();
        Assert.Single(run.State.StackItems);
        AssertMight(run.State, 6);
        await run.Top();
        Save($"reaction-{ability}", initial, run);
        AssertMight(run.State, 12);
        Assert.Equal(new[] { 2, 6 }, run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers.Select(m => m.PowerDelta));
        await run.Act("P1", new EndTurnCommand());
        AssertMight(run.State, 4);
        Assert.Equal(3, run.State.CardObjects["TARGET"].Power);
        await run.Replay();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualNegativeReductionMakesDoublingAddZeroAndRetainsTheOtherInstruction(bool ability)
    {
        var initial = Opening(ability, negative: true);
        var run = await Prepare(initial, false);
        await run.Act("P1", new PlayCardCommand("READY", "OGN·146/298", ["TARGET"]));
        await run.Top();
        await run.Act("P1", new MoveUnitCommand("TARGET", "BASE", "BATTLEFIELD:F", []));
        for (var i = 0; run.State.Phase != MatchPhases.Main || run.State.TimingState != TimingStates.NeutralOpen; i++)
        {
            Assert.InRange(i, 0, 8);
            if (run.State.StackItems.Count > 0) await run.Top();
            else if (run.State.FocusPlayerId is { } focus) await run.Act(focus, new PassFocusCommand());
            else if (run.State.PriorityPlayerId is { } priority) await run.Act(priority, new PassPriorityCommand());
            else Assert.Fail("The earned movement window has no legal continuation.");
        }
        Assert.Equal("P1", run.State.TurnPlayerId);
        await run.Act("P1", new PlayCardCommand("DEBUFF", "UNL-182/219", ["TARGET"], Mode: "BATTLEFIELD_UNIT_POWER_MINUS_4"));
        await run.Top();
        var negative = ability ? -1 : -2;
        Assert.Equal(negative, run.State.CardObjects["TARGET"].Power);
        AssertMight(run.State, 0);
        Assert.Equal(0, run.State.CardObjects["TARGET"].Damage);
        var ledger = run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers.ToArray();
        await Double(run, ability, "DOUBLE-1");
        Save($"negative-{ability}", initial, run);
        Assert.Equal(negative, run.State.CardObjects["TARGET"].Power);
        AssertMight(run.State, 0);
        Assert.Equal(ledger, run.State.CardObjects["TARGET"].UntilEndOfTurnPowerModifiers);
        if (!ability) Assert.Contains(CardObjectTags.Ephemeral, run.State.CardObjects["TARGET"].Tags);
        else Assert.Equal(0, run.Events().Last(e => e.Event.Kind == "POWER_MODIFIED_UNTIL_END_OF_TURN").Event.Payload["appliedPowerDelta"]);
        await run.Replay();
    }

    private static GameCommand DoubleCommand(bool ability, string source) => ability
        ? new ActivateAbilityCommand("TARGET", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, [])
        : new PlayCardCommand(source, DoubleCard, ["TARGET"]);

    private static async Task Double(Driver run, bool ability, string source)
    {
        await run.Act("P1", DoubleCommand(ability, source));
        await run.Top();
    }

    private static async Task<Driver> Prepare(MatchState initial, bool equipped)
    {
        var run = new Driver(initial);
        await run.Act("P1", new TapRuneCommand(Funding[0], Funding));
        foreach (var id in Funding.Where(id => id.StartsWith("GREEN-", StringComparison.Ordinal) || id.StartsWith("RED-", StringComparison.Ordinal)))
            await run.Act("P1", new RecycleRuneCommand(id));
        await run.Act("P1", new PlayCardCommand("TARGET", initial.CardObjects["TARGET"].CardNo!, [], Destination: "BASE"));
        if (equipped)
        {
            await run.Act("P1", new PlayCardCommand("GEAR", "SFD·051/221", [], Destination: "BASE"));
            await run.Act("P1", new AssembleEquipmentCommand("GEAR", "TARGET", ["ASSEMBLE_GREEN"]));
            await run.Top();
            Assert.Equal("TARGET", run.State.CardObjects["GEAR"].AttachedToObjectId);
        }
        return run;
    }

    private static async Task RejectPrivateTarget(Driver run, bool ability)
    {
        GameCommand bad = ability
            ? new ActivateAbilityCommand("TARGET", P4ActivatedAbilityCatalog.ViDoublePowerAbilityId, ["PRIVATE-P2-1"])
            : new PlayCardCommand("DOUBLE-1", DoubleCard, ["PRIVATE-P2-1"]);
        var raw = JsonSerializer.SerializeToNode(bad, bad.GetType(), Json)!.AsObject();
        raw["promptId"] = ResolutionResult.BuildPrompts(run.State)["P1"].PromptId;
        raw["snapshotTick"] = run.State.Tick;
        await run.ActRetained(new("P1", "doubling-private-target-reject", JsonSerializer.SerializeToElement(raw, Json)), false);
    }

    private static int Might(MatchState state) => (int)typeof(CoreRuleEngine)
        .GetMethod("ResolveCurrentFieldUnitPower", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [state, state.CardObjects["TARGET"]])!;

    private static void AssertMight(MatchState state, int expected)
    {
        Assert.Equal(expected, Might(state));
        foreach (var snapshot in ResolutionResult.BuildSnapshots(state).Values)
        {
            var card = JsonSerializer.SerializeToElement(snapshot, Json).GetProperty("players")
                .GetProperty("P1").GetProperty("objects").GetProperty("TARGET");
            Assert.Equal(expected, card.GetProperty("effectivePower").GetInt32());
        }
    }

    private void Save(string name, MatchState initial, Driver run)
    {
        var assembly = typeof(CoreRuleEngine).Assembly;
        var sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant();
        output.WriteLine($"Engine={assembly.Location}; SHA256={sha}; MVID={assembly.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
        output.WriteLine($"{name}: raw={run.State.CardObjects["TARGET"].Power}; current={Might(run.State)}; commands={run.Commands().Length}; events={run.Events().Length}");
        if (Environment.GetEnvironmentVariable("RIFTBOUND_DOUBLING_EVIDENCE") is not { } root) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(new
        { initial, commands = run.Commands(), events = run.Events(), final = run.State, snapshots = ResolutionResult.BuildSnapshots(run.State) }, Json));
    }

    private static MatchState Opening(bool ability, bool alternate = false, bool negative = false)
    {
        var cards = new Dictionary<string, CardObjectState>();
        var locations = new Dictionary<string, ObjectLocationState>();
        void Put(string id, string no, string player, string zone, string? field = null)
        { cards[id] = OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player); locations[id] = new(player, zone, field); }
        Put("F", "OGN·278/298", "P1", "BATTLEFIELD", "F");
        Put("G", "OGN·280/298", "P2", "BATTLEFIELD", "G");
        Put("TARGET", negative && !ability ? "OGN·010/298" : alternate ? P4ActivatedAbilityCatalog.ViAltACardNo : P4ActivatedAbilityCatalog.ViCardNo, "P1", "HAND");
        Put("GEAR", "SFD·051/221", "P1", "HAND");
        Put("DOUBLE-1", DoubleCard, "P1", "HAND"); Put("DOUBLE-2", DoubleCard, "P1", "HAND");
        Put("BUFF", "OGN·058/298", "P1", "HAND"); Put("READY", "OGN·146/298", "P1", "HAND"); Put("DEBUFF", "UNL-182/219", "P1", "HAND");
        foreach (var id in Funding) Put(id, id.StartsWith("GREEN-", StringComparison.Ordinal) ? "UNL-R02" : id.StartsWith("RED-", StringComparison.Ordinal) ? "UNL-R01" : "UNL-R06", "P1", "BASE");
        var zones = new Dictionary<string, PlayerZones>();
        foreach (var player in new[] { "P1", "P2" })
        {
            var deck = Enumerable.Range(1, 3).Select(i => $"PRIVATE-{player}-{i}").ToArray();
            foreach (var id in deck) Put(id, "OGN·219/298", player, "MAIN_DECK");
            var runes = player == "P1" ? [] : Enumerable.Range(1, 12).Select(i => "P2-RUNE-" + i).ToArray();
            foreach (var id in runes) Put(id, "UNL-R03", player, "RUNE_DECK");
            zones[player] = PlayerZones.Empty with { MainDeck = deck, RuneDeck = runes,
                Hand = player == "P1" ? ["TARGET", "GEAR", "DOUBLE-1", "DOUBLE-2", "BUFF", "READY", "DEBUFF"] : [],
                Base = player == "P1" ? Funding : [], Battlefields = player == "P1" ? ["F"] : ["G"] };
        }
        return new MatchState("CURRENT-MIGHT-DOUBLING", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: zones, cardObjects: cards, objectLocations: locations,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }, seed: 424242)
        { CardPlays = new(5, [], []) };
    }
}
