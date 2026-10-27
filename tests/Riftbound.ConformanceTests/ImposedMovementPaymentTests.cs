using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// CN204.4.b/b.1/c and 430.3: imposed costs are paid at execution, with
// reaction-resource access even when the moving actor has no priority.
public sealed class ImposedMovementPaymentTests
{
    [Fact]
    public async Task TwoSequentialInstructionsToTheSameBattlefieldAreNotOneTaxedGroup()
    {
        var state = Position() with { RunePools = new Dictionary<string, RunePool>(Position().RunePools) { ["P1"] = new(2, 1) } };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["SPELL"] = OfficialGarenConquestBattlefieldCountTests.Printed("SPELL", "UNL-202/219", "P1") } };
        var engine = new CoreRuleEngine();
        var cast = await engine.ResolveAsync(state, new("imposed-sequential", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "UNL-202/219", ["A", "D"], MovementDestinations: ["BATTLEFIELD:FIELD2", "BATTLEFIELD:FIELD2"]), default);
        Assert.True(cast.Accepted, cast.ErrorCode + ": " + cast.ErrorMessage);
        Assert.Equal(0, cast.State.RunePools["P1"].TotalPower);
        var done = await OfficialGraveyardRecastTests.Top(cast.State);
        Assert.All(new[] { "A", "D" }, id => Assert.Equal("FIELD2", done.State.ObjectLocations[id].BattlefieldObjectId));
        Assert.DoesNotContain(done.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(0, done.State.RunePools["P1"].TotalPower);
    }

    [Fact]
    public async Task TwoPhysicalPatrolsChargeTheFundedStandardIntentTwice()
    {
        var state = Position(); state = state with {
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(0, 2) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Battlefields = state.PlayerZones["P2"].Battlefields.Append("PATROL2").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PATROL2"] = OfficialGarenConquestBattlefieldCountTests.Printed("PATROL2", "UNL-163/219", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["PATROL2"] = new("P2", "BATTLEFIELD", "FIELD2") } };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("imposed-funded", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        var paid = Assert.Single(result.Events, e => e.Kind == "COST_PAID"); Assert.Equal(2, paid.Payload["powerCost"]);
        Assert.All(new[] { "A", "B" }, id => Assert.Equal("FIELD2", result.State.ObjectLocations[id].BattlefieldObjectId));
    }

    [Theory]
    [InlineData("friendly")]
    [InlineData("base")]
    [InlineData("face-down")]
    public async Task AnInactiveOrFriendlyPatrolDoesNotImposeTheCost(string boundary)
    {
        // Explicit source-legality fixture boundaries, not claimed reachable
        // native HIDE or TAKE_CONTROL commands for this exact printing.
        var state = Position();
        if (boundary == "friendly") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["PATROL"] },
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => id != "PATROL").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["PATROL"] = state.CardObjects["PATROL"] with { ControllerId = "P1" },
                ["FIELD2"] = state.CardObjects["FIELD2"] with { ControllerId = "P1" } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["PATROL"] = new("P1", "BATTLEFIELD", "FIELD2") } };
        if (boundary == "base") state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with {
                Base = ["PATROL"], Battlefields = state.PlayerZones["P2"].Battlefields.Where(id => id != "PATROL").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["PATROL"] = new("P2", "BASE") } };
        if (boundary == "face-down") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["PATROL"] = state.CardObjects["PATROL"] with { IsFaceDown = true } } };
        if (boundary == "face-down")
        {
            var candidate = ResolutionResult.BuildPrompts(state)["P1"].Candidates!.Single(c => c.Action == CommandTypes.MoveUnit);
            var fees = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(candidate.Metadata!["powerPerExtraUnitByDestination"]);
            Assert.Equal(0, fees["BATTLEFIELD:FIELD2"]);
        }
        var result = await new CoreRuleEngine().ResolveAsync(state, new("imposed-no-tax-" + boundary, "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]), default);
        Assert.True(result.Accepted, result.ErrorCode + ": " + result.ErrorMessage);
        Assert.DoesNotContain(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(0, result.State.RunePools["P1"].TotalPower);
        Assert.All(new[] { "A", "B" }, id => Assert.Equal("FIELD2", result.State.ObjectLocations[id].BattlefieldObjectId));
    }

    [Fact]
    public async Task AStandardSingleMoveNeedsNoPatrolPayment()
    {
        var state = Position();
        var result = await new CoreRuleEngine().ResolveAsync(state, new("imposed-single", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A"]), default);
        Assert.True(result.Accepted, result.ErrorMessage); Assert.DoesNotContain(result.Events, e => e.Kind == "COST_PAID");
        Assert.Equal("FIELD2", result.State.ObjectLocations["A"].BattlefieldObjectId); Assert.Equal("BASE", result.State.ObjectLocations["B"].Zone);
    }

    [Fact]
    public async Task AnUnfundedStandardGroupWithNoResourceSourceStillRejectsWithoutMutation()
    {
        var state = Position(); state = state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Base = ["A", "B"], RuneDeck = ["RUNE"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "RUNE_DECK") } };
        var hash = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("imposed-unfunded", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]), default);
        Assert.False(result.Accepted); Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode); Assert.Empty(result.Events);
        Assert.Equal(hash, MatchStateHasher.Hash(result.State));
    }

    [Fact]
    public async Task ARealAttachedCopyOfPatrolTextAddsItsIndependentImposedCost()
    {
        var initial = Position() with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        initial = initial with {
            RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P2"] = new(1, 1) },
            PlayerZones = new Dictionary<string, PlayerZones>(initial.PlayerZones) {
                ["P1"] = initial.PlayerZones["P1"] with { Base = initial.PlayerZones["P1"].Base.Append("RUNE2").ToArray(), MainDeck = ["DRAW"] },
                ["P2"] = initial.PlayerZones["P2"] with { Base = ["SONG"] } },
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["SONG"] = OfficialGarenConquestBattlefieldCountTests.Printed("SONG", "SFD·059/221", "P2"),
                ["RUNE2"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE2", "OGN·007/298", "P1"),
                ["DRAW"] = OfficialGarenConquestBattlefieldCountTests.Printed("DRAW", "SFD·125/221", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(initial.ObjectLocations) {
                ["SONG"] = new("P2", "BASE"), ["RUNE2"] = new("P1", "BASE"), ["DRAW"] = new("P1", "MAIN_DECK") } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P2", new AssembleEquipmentCommand("SONG", "PATROL", ["ASSEMBLE_GREEN"]));
        while (driver.State.StackItems.Any(i => i.EffectKind == "ASSEMBLE_EQUIPMENT"))
            await driver.Act(driver.State.PriorityPlayerId!, new PassPriorityCommand());
        Assert.Equal("UNL-163/219", driver.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await driver.Act("P2", new EndTurnCommand());
        await driver.Act("P1", new RecycleRuneCommand("RUNE"));
        var acquired = await driver.Act("P1", new RecycleRuneCommand("RUNE2"));
        Assert.Equal(2, driver.State.RunePools["P1"].TotalPower);
        var move = acquired.Prompts["P1"].Candidates!.Single(c => c.Action == CommandTypes.MoveUnit);
        var costs = Assert.IsAssignableFrom<IReadOnlyDictionary<string, int>>(move.Metadata!["powerPerExtraUnitByDestination"]);
        Export("copied-source-red", driver.State, acquired);
        Assert.Equal(2, costs["BATTLEFIELD:FIELD2"]);
        await driver.Act("P1", new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]));
        Assert.Equal(0, driver.State.RunePools["P1"].TotalPower);
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Fact]
    public async Task StandardGroupMovementCanAcquirePowerWhilePayingItsImposedCost()
    {
        var initial = Position(); var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(initial, new("imposed-standard", "P1", CommandTypes.MoveUnit),
            new MoveUnitCommand("A", "BASE", "BATTLEFIELD:FIELD2", SourceObjectIds: ["A", "B"]), default);
        Export("standard-red", initial, result);
        Assert.True(result.Accepted, result.ErrorCode + ": " + result.ErrorMessage);
        var pending = Assert.IsType<RuleChoiceContinuation>(result.State.PendingRuleChoice);
        Assert.Equal("P1", pending.Request.PlayerId);
        Assert.Contains(pending.Request.Options, o => o.Id == "RECYCLE_RUNE:RUNE");
        Assert.All(new[] { "A", "B" }, id => Assert.Equal("BASE", result.State.ObjectLocations[id].Zone));
        Assert.All(new[] { "A", "B" }, id => Assert.False(result.State.CardObjects[id].IsExhausted));
    }

    [Fact]
    public async Task AResolvingSpellCannotMoveTheGroupWithoutPayingTheImposedCost()
    {
        var initial = Position();
        // One wildcard pays this spell's printed colored cost; there is no power
        // left for Patrol. The ready rune is the legitimate payment source.
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) { ["P1"] = new(4, 1) } };
        var engine = new CoreRuleEngine();
        var cast = await engine.ResolveAsync(initial, new("imposed-tentacles", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "UNL-054/219", ["D", "D2"], Destination: "BATTLEFIELD:FIELD2"), default);
        Assert.True(cast.Accepted, cast.ErrorCode + ": " + cast.ErrorMessage);
        Assert.Equal(0, cast.State.RunePools["P1"].TotalPower);
        var first = await engine.ResolveAsync(cast.State, new("imposed-first-pass", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var final = await engine.ResolveAsync(first.State, new("imposed-second-pass", "P2", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(final.Accepted, final.ErrorCode + ": " + final.ErrorMessage);
        Export("spell-red", first.State, final);
        var pending = Assert.IsType<RuleChoiceContinuation>(final.State.PendingRuleChoice);
        Assert.Equal("P1", pending.Request.PlayerId);
        Assert.Equal("P2", pending.Origin.PriorityPlayerId);
        Assert.Contains(pending.Request.Options, o => o.Id == "RECYCLE_RUNE:RUNE");
        Assert.Contains(pending.Request.Options, o => o.Id == "DECLINE");
        Assert.All(new[] { "D", "D2" }, id => Assert.Equal("FIELD1", final.State.ObjectLocations[id].BattlefieldObjectId));
    }

    internal static MatchState Position()
    {
        CardObjectState Printed(string id, string no, string player)
            => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, player);
        return new("IMPOSED-MOVEMENT-PAYMENT", 0, 3, "P1", new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty },
            playerZones: new Dictionary<string, PlayerZones> {
                ["P1"] = PlayerZones.Empty with { Base = ["A", "B", "RUNE"], Hand = ["SPELL"] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["FIELD1", "D", "D2", "FIELD2", "PATROL"] } },
            cardObjects: new Dictionary<string, CardObjectState> {
                ["A"] = Printed("A", "SFD·125/221", "P1"), ["B"] = Printed("B", "SFD·125/221", "P1"),
                ["RUNE"] = Printed("RUNE", "OGN·007/298", "P1"), ["SPELL"] = Printed("SPELL", "UNL-054/219", "P1"),
                ["D"] = Printed("D", "SFD·125/221", "P2"), ["D2"] = Printed("D2", "SFD·125/221", "P2"),
                ["PATROL"] = Printed("PATROL", "UNL-163/219", "P2"),
                ["FIELD1"] = Printed("FIELD1", "OGN·278/298", "P2") with { ControllerId = "P2" },
                ["FIELD2"] = Printed("FIELD2", "OGN·287/298", "P2") with { ControllerId = "P2" } },
            objectLocations: new Dictionary<string, ObjectLocationState> {
                ["A"] = new("P1", "BASE"), ["B"] = new("P1", "BASE"), ["RUNE"] = new("P1", "BASE"), ["SPELL"] = new("P1", "HAND"),
                ["D"] = new("P2", "BATTLEFIELD", "FIELD1"), ["D2"] = new("P2", "BATTLEFIELD", "FIELD1"),
                ["PATROL"] = new("P2", "BATTLEFIELD", "FIELD2"), ["FIELD1"] = new("P2", "BATTLEFIELD", "FIELD1"),
                ["FIELD2"] = new("P2", "BATTLEFIELD", "FIELD2") });
    }

    private static void Export(string name, MatchState before, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_IMPOSED_MOVEMENT_EVIDENCE") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); var assembly = typeof(CoreRuleEngine).Assembly;
        File.WriteAllText(Path.Combine(folder, name + ".json"), JsonSerializer.Serialize(new { before, result,
            runtime = new { assemblyPath = assembly.Location, mvid = assembly.ManifestModule.ModuleVersionId,
                informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant() } },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
