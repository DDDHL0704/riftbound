using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class OtherSelfAbilitySourceContinuityTests
{
    private static MatchState Position(string kind, bool token)
    {
        var no = kind switch {
            "empower" => "VEN·074", "self-cost" => "VEN·124",
            "move" => P4ActivatedAbilityCatalog.EzrealBlueSwiftCardNo, _ => "OGN·001/298" };
        var state = ArmamentMightTests.Position(unit: no, power: 4);
        if (kind == "move") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        var source = state.CardObjects["HOST"] with { IsExhausted = kind == "granted" };
        if (token) source = source with {
            TokenFactoryCardNo = P6TokenFactoryCatalog.ImageTokenCardNo,
            CopyFaceTransitions = [new(0, source.ObjectGeneration, P6TokenFactoryCatalog.ImageTokenCardNo, no)] };
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["HOST"] = source },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) {
                ["P1"] = new(20, 20, new Dictionary<string, int> { [RuneTrait.Blue] = 10 }) } };
        state = AttackDamageDistributionTests.Spell(state, "RETURN", "OGN·104/298", "P1");
        return kind == "granted" ? AttackDamageDistributionTests.Spell(state, "GRANT", "VEN·142", "P1") : state;
    }

    private static async Task<Driver> Activate(string kind, bool token)
    {
        var game = new Driver(Position(kind, token));
        string id;
        if (kind == "granted")
        {
            await game.Act("P1", new PlayCardCommand("GRANT", "VEN·142", ["HOST"]));
            await game.Top();
            id = CurrentPowerTargetTests.Snapshot(game.State).GetProperty("players").GetProperty("P1")
                .GetProperty("objects").GetProperty("HOST").GetProperty("untilEndOfTurnEffects")
                .EnumerateArray().Select(entry => entry.GetString()!).First(entry => entry.StartsWith("GRANTED:", StringComparison.Ordinal));
        }
        else id = kind == "move" ? P4ActivatedAbilityCatalog.EzrealBlueSwiftMoveAbilityId : "EMPOWER:0:0";
        var before = game.State.RunePools["P1"];
        await game.Act("P1", new ActivateAbilityCommand("HOST", id, []));
        Assert.Equal(before.Mana - (kind == "empower" ? 1 : 0), game.State.RunePools["P1"].Mana);
        Assert.Equal(before.TotalPower - (kind == "granted" ? 2 : kind == "move" ? 1 : 0), game.State.RunePools["P1"].TotalPower);
        return game;
    }

    private static CeasedSelfAbilitySource? Receipt(StackItemState item)
        => item.EmpowerActivation?.CeasedSource ?? item.SelfMovement?.CeasedSource ?? item.TemporaryActivation?.CeasedSource;

    private static StackItemState WithReceipt(StackItemState item, CeasedSelfAbilitySource? receipt)
        => item.EmpowerActivation is { } empower ? item with { EmpowerActivation = empower with { CeasedSource = receipt } }
            : item.SelfMovement is { } movement ? item with { SelfMovement = movement with { CeasedSource = receipt } }
            : item with { TemporaryActivation = item.TemporaryActivation! with { CeasedSource = receipt } };

    private static void AssertInvalid(MatchState state, StackItemState item)
    {
        var error = item.EmpowerActivation is not null ? "invalid captured empower activation"
            : item.SelfMovement is not null ? "invalid captured self movement" : "invalid captured temporary activation";
        Assert.Contains(error, OfficialInsightAndSpellLockTests.Errors(state with { StackItems = [item] }));
    }

    [Theory]
    [InlineData("empower", false)] [InlineData("empower", true)]
    [InlineData("move", false)] [InlineData("move", true)]
    [InlineData("granted", false)] [InlineData("granted", true)]
    public async Task ARealReturnResponsePreservesThePaidSkillWithoutApplyingItToTheDepartedSource(string kind, bool token)
    {
        var game = await Activate(kind, token);
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"]));
        await game.Top();
        var item = Assert.Single(game.State.StackItems);
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("HOST"));
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(Receipt(item));
            foreach (var forged in new[] {
                receipt with { Source = receipt.Source with { Generation = receipt.Source.Generation + 1 } },
                receipt with { CardNo = "OGN·002/298" },
                receipt with { FaceRevision = receipt.FaceRevision + 1 },
                receipt with { AcquiredTick = receipt.AcquiredTick + 1 },
                receipt with { TokenFactoryCardNo = P6TokenFactoryCatalog.BombTokenCardNo },
                receipt with { DepartedTick = receipt.AcquiredTick },
                receipt with { DepartedTick = game.State.Tick + 1 } })
                AssertInvalid(game.State, WithReceipt(item, forged));
            AssertInvalid(game.State, WithReceipt(item, null));
        }
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Hand);
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(game.State.RoomId, game.State.Tick, 0, [], game.State);
        foreach (var snapshot in new[] { ResolutionResult.BuildSnapshots(game.State)["P2"], spectator.SpectatorSnapshot })
        {
            var shown = Assert.IsType<Dictionary<string, object?>>(Assert.Single(snapshot.Stack));
            Assert.Equal(token ? "HOST" : "HIDDEN", shown["sourceObjectId"]);
        }
        var resources = game.State.RunePools["P1"];
        var sourceHash = token ? null : MatchStateHasher.HashValue(game.State.CardObjects["HOST"]);
        var firstEvent = game.Events().Length;
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.Equal(MatchStateHasher.HashValue(resources), MatchStateHasher.HashValue(game.State.RunePools["P1"]));
        Assert.DoesNotContain(game.Events().Skip(firstEvent), entry =>
            entry.Event.Kind is "OBJECT_EMPOWERED" or "UNIT_MOVED_TO_BASE" or "UNIT_READIED");
        if (!token) Assert.Equal(sourceHash, MatchStateHasher.HashValue(game.State.CardObjects["HOST"]));
        await game.Replay();
    }

    [Theory]
    [InlineData("empower")] [InlineData("move")] [InlineData("granted")]
    public async Task RecoveryRejectsAMissingSourceWithoutACapturedTokenDeparture(string kind)
    {
        var game = await Activate(kind, token: true);
        var forged = game.State with {
            CardObjects = game.State.CardObjects.Where(entry => entry.Key != "HOST").ToDictionary(),
            ObjectLocations = game.State.ObjectLocations.Where(entry => entry.Key != "HOST").ToDictionary(),
            PlayerZones = new Dictionary<string, PlayerZones>(game.State.PlayerZones) {
                ["P1"] = game.State.PlayerZones["P1"] with {
                    Base = game.State.PlayerZones["P1"].Base.Where(id => id != "HOST").ToArray(),
                    Battlefields = game.State.PlayerZones["P1"].Battlefields.Where(id => id != "HOST").ToArray() } } };
        AssertInvalid(forged, Assert.Single(game.State.StackItems));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmpowerMaySacrificeItsOwnSourceAndTheCapturedSkillStillCompletes(bool token)
    {
        var game = new Driver(Position("self-cost", token));
        await game.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", [],
            ["DESTROY_FRIENDLY_UNIT:HOST"]));
        var item = Assert.Single(game.State.StackItems);
        if (token)
        {
            Assert.False(game.State.CardObjects.ContainsKey("HOST"));
            var receipt = Assert.IsType<CeasedSelfAbilitySource>(Receipt(item));
            Assert.Equal(item.EmpowerActivation!.AcquiredTick, receipt.DepartedTick);
            AssertInvalid(game.State, WithReceipt(item, null));
            AssertInvalid(game.State, WithReceipt(item, receipt with { DepartedTick = receipt.AcquiredTick - 1 }));
            var context = item.EmpowerActivation!;
            AssertInvalid(game.State, item with { EmpowerActivation = context with {
                DestructionCost = context.DestructionCost! with { TokenCeasedAtTick = context.AcquiredTick + 1 } } });
            AssertInvalid(game.State, item with { EmpowerActivation = context with {
                DestructionCost = context.DestructionCost! with { Unit = new("FORGED-COST", 0) } } });
        }
        else Assert.Contains("HOST", game.State.PlayerZones["P1"].Graveyard);
        await game.Top();
        Assert.Empty(game.State.StackItems);
        Assert.DoesNotContain(game.Events(), entry => entry.Event.Kind == "OBJECT_EMPOWERED");
        if (!token) Assert.False(game.State.CardObjects["HOST"].IsEmpowered);
        await game.Replay();
    }
}
