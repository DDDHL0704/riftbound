using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class PrintedUnitEquipmentResourceCostTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IEnumerable<object[]> Boundaries()
        => from zone in new[] { "BASE", "BATTLEFIELD" }
           from replacement in new[] { "none", "hourglass", "angel" }
           from typed in new[] { false, true }
           from room in new[] { 1, 2 }
           select new object[] { zone, replacement, typed, room };

    [Theory]
    [MemberData(nameof(Boundaries))]
    public async Task AnActuallyPrintedUnitEquipmentCanPayItsDestructionCostOnlyWhenTheGainFits(
        string zone, string replacement, bool typed, int room)
    {
        // CN 178: VEN058 is printed as both types. MAX is only the server's
        // representation boundary; this explicit midgame is not a legal opening.
        var initial = Position(zone, replacement, typed, room); var game = new Driver(initial);
        MatchState? before = null; ResolutionResult? probe = null; ActionPromptDto? prompt = null;
        try
        {
            if (replacement == "angel")
            {
                await game.Act("P1", new AssembleEquipmentCommand("REPLACEMENT", "DUAL", ["ASSEMBLE_GREEN"]));
                await game.Top(); await game.Act("P1", new RecycleRuneCommand("SETUP-RUNE"));
                Assert.Equal("DUAL", game.State.CardObjects["REPLACEMENT"].AttachedToObjectId);
                Assert.Equal(game.State.ObjectLocations["DUAL"], game.State.ObjectLocations["REPLACEMENT"]);
            }
            before = game.State; var hash = MatchStateHasher.Hash(before); var generation = before.CardObjects["DUAL"].ObjectGeneration;
            Assert.Equal("VEN·058", before.CardObjects["DUAL"].CardNo);
            Assert.Contains(CardObjectTags.UnitCard, before.CardObjects["DUAL"].Tags);
            Assert.Contains(CardObjectTags.EquipmentCard, before.CardObjects["DUAL"].Tags);
            Assert.Equal(int.MaxValue - room, before.RunePools["P1"].TotalPower);
            Assert.Equal(int.MaxValue, before.RunePools["P1"].Mana);
            if (replacement != "none") Assert.DoesNotContain(CardObjectTags.UnitCard, before.CardObjects["REPLACEMENT"].Tags);
            prompt = ResolutionResult.BuildPrompts(before)["P1"];
            var command = new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["DUAL"]);
            probe = await new CoreRuleEngine().ResolveAsync(before, new("printed-dual-cost", "P1", CommandTypes.ActivateAbility), command, default);
            Assert.Equal(hash, MatchStateHasher.Hash(before));
            if (room < 2)
            {
                Assert.False(probe.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, probe.ErrorCode);
                Assert.Empty(probe.Events); Assert.Equal(hash, MatchStateHasher.Hash(probe.State));
                Assert.DoesNotContain(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility
                    && (c.Sources ?? []).Any(s => s.Id == "MALZ"));
                var wire = JsonSerializer.SerializeToNode(command, command.GetType(), Json)!.AsObject();
                wire["promptId"] = prompt.PromptId; wire["snapshotTick"] = before.Tick;
                await game.ActRetained(new("P1", "printed-dual-rejected", JsonSerializer.SerializeToElement(wire, Json)), false);
                Assert.Equal(ErrorCodes.UnsupportedCardBehavior, game.Commands()[^1].ErrorCode);
            }
            else
            {
                Assert.True(probe.Accepted, probe.ErrorMessage);
                await game.Act("P1", command); var state = game.State;
                Assert.Null(state.PendingRuleChoice); Assert.Equal(int.MaxValue, state.RunePools["P1"].TotalPower);
                Assert.Equal(int.MaxValue, state.RunePools["P1"].Mana); Assert.True(state.CardObjects["MALZ"].IsExhausted);
                Assert.Contains(CardObjectTags.UnitCard, state.CardObjects["DUAL"].Tags);
                Assert.Contains(CardObjectTags.EquipmentCard, state.CardObjects["DUAL"].Tags);
                Assert.Equal(replacement == "none", state.PlayerZones["P1"].Graveyard.Contains("DUAL"));
                Assert.Equal(replacement == "none", state.DestroyedUnitOwnerIdsThisTurn.Contains("P1"));
                if (replacement != "none")
                {
                    Assert.Contains("DUAL", state.PlayerZones["P1"].Base); Assert.True(state.CardObjects["DUAL"].IsExhausted);
                    Assert.Equal(generation, state.CardObjects["DUAL"].ObjectGeneration);
                    Assert.Contains("REPLACEMENT", state.PlayerZones["P1"].Graveyard);
                }
                Assert.Single(probe.Events, e => e.Kind == "POWER_GAINED" && Equals(e.Payload.GetValueOrDefault("sourceObjectId"), "MALZ"));
            }
        }
        finally
        {
            if (Environment.GetEnvironmentVariable("RIFTBOUND_UNIT_GEAR_COST_EVIDENCE") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                await File.WriteAllTextAsync(Path.Combine(output, $"{zone}-{replacement}-{typed}-{room}.json"), JsonSerializer.Serialize(new {
                    scope = "Printed dual-type VEN058 cost target, actual attachment/recycle setup, explicit MAX midgame representation fixture. Current raw/erratum Zhonya source difference remains separate. No fabricated unit type on replacement sources, ordinary opening, network, SQL or whole-card certification.",
                    initial, before, prompt, coreProbe = probe, driverState = game.State, commands = game.Commands(), events = game.Events() }, Json));
            }
        }
    }

    private static MatchState Position(string zone, string replacement, bool typed, int room)
    {
        var entries = new List<(string Id, string No, string Player, string Zone, string? Field)> {
            ("MALZ", P4ActivatedAbilityCatalog.MalzaharCardNo, "P1", "BASE", null),
            ("DUAL", "VEN·058", "P1", zone, zone == "BATTLEFIELD" ? "FIELD1" : null),
            ("FIELD1", "OGN·278/298", "P1", "BATTLEFIELD", "FIELD1"),
            ("FIELD2", "OGN·287/298", "P2", "BATTLEFIELD", "FIELD2") };
        if (replacement != "none") entries.Add(("REPLACEMENT", replacement == "angel" ? "SFD·051/221" : "OGN·077/298", "P1", "BASE", null));
        if (replacement == "angel") entries.Add(("SETUP-RUNE", "OGN·042/298", "P1", "BASE", null));
        var green = replacement == "angel" ? 1 : 0;
        return new("PRINTED-DUAL-COST", 0, 5, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, readyPlayerIds: ["P1", "P2"], turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, seed: 424242,
            cardObjects: entries.ToDictionary(e => e.Id, e => OfficialGarenConquestBattlefieldCountTests.Printed(e.Id, e.No, e.Player)),
            objectLocations: entries.ToDictionary(e => e.Id, e => new ObjectLocationState(e.Player, e.Zone, e.Field)),
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = entries.Where(e => e.Player == "P1" && e.Zone == "BASE").Select(e => e.Id).ToArray(),
                    Battlefields = entries.Where(e => e.Player == "P1" && e.Zone == "BATTLEFIELD").Select(e => e.Id).ToArray() },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD2"] } },
            runePools: new Dictionary<string, RunePool> {
                ["P1"] = new(int.MaxValue, typed ? 0 : int.MaxValue - room, typed
                    ? new Dictionary<string, int> { [RuneTrait.Red] = int.MaxValue - room - green, [RuneTrait.Green] = green } : null),
                ["P2"] = RunePool.Empty });
    }
}
