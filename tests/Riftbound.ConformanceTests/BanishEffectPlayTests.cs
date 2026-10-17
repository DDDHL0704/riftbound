using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BanishEffectPlayTests
{
    internal const string Hunt = "UNL-184/219";
    internal const string Rescue = "OGN·102/298";

    internal static MatchState Position(string spell = Hunt, string unit = "OGN·096/298", string owner = "P1")
    {
        var initial = ArmamentMightTests.Position(unit: unit);
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["HOST"] = initial.CardObjects["HOST"] with { OwnerId = owner },
                ["F"] = initial.CardObjects["F"] with { ControllerId = null },
                ["OTHER"] = initial.CardObjects["OTHER"] with { ControllerId = null } },
            RunePools = initial.Seats.Keys.ToDictionary(id => id,
                _ => new RunePool(20, 20, powerByTrait: new Dictionary<string, int> { [RuneTrait.Red] = 20 })) };
        return AttackDamageDistributionTests.Spell(initial, "HUNT", spell, "P1");
    }

    internal static async Task<(MatchState Initial, StandbyDefendObserverTests.Driver Driver)> Open(
        string spell = Hunt, string unit = "OGN·096/298", string owner = "P1")
    {
        var initial = Position(spell, unit, owner);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", spell, ["HOST"]));
        await driver.Top();
        return (initial, driver);
    }

    internal static async Task Reject(MatchState state, string player, GameCommand command)
    {
        var before = MatchStateHasher.Hash(state);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-effect-play", player, command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
    }

    internal static async Task Replay(MatchState initial, StandbyDefendObserverTests.Driver driver)
    {
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), driver.State,
            new CoreRuleEngine(), default, driver.Events());
        Assert.True(replay.IsMatch, string.Join("; ", replay.Errors));
    }

    [Theory]
    [InlineData(Hunt, "P1", "BATTLEFIELD:F")]
    [InlineData(Hunt, "P1", "BATTLEFIELD:OTHER")]
    [InlineData(Hunt, "P2", "BATTLEFIELD:F")]
    [InlineData(Hunt, "P2", "BATTLEFIELD:OTHER")]
    [InlineData(Rescue, "P1", "BASE")]
    [InlineData(Rescue, "P2", "BASE")]
    public async Task OwnerActuallyChoosesThePrintedDestinationAndBothBaseCostsAreWaived(string spell, string owner, string destination)
    {
        var (initial, driver) = await Open(spell, owner: owner);
        var pending = Assert.IsType<PendingEffectPlayState>(driver.State.PendingEffectPlay);
        Assert.Equal(owner, pending.PlayerId);
        Assert.Equal(owner, driver.State.ActivePlayerId);
        Assert.False(pending.Optional);
        Assert.True(pending.IgnoreBaseMana); Assert.True(pending.IgnoreBasePower); Assert.False(pending.IgnoreAllCosts);
        Assert.Equal(new[] { "HOST" }, driver.State.PlayerZones[owner].Banished);
        Assert.Equal(initial.CardObjects["HOST"].ObjectGeneration + 1, pending.Sources["HOST"]);
        var prompt = ResolutionResult.BuildPrompts(driver.State)[owner];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        Assert.Equal(spell == Hunt ? new[] { "BATTLEFIELD:F", "BATTLEFIELD:OTHER" } : new[] { "BASE" },
            candidate.Destinations!.Select(d => d.Id).Order().ToArray());
        Assert.DoesNotContain(prompt.Candidates!, c => c.Action == CommandTypes.ChooseCards);
        var play = new PlayCardCommand("HOST", "OGN·096/298", [], Destination: destination);
        await Reject(driver.State, owner == "P1" ? "P2" : "P1", play);
        await Reject(driver.State, owner, new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        await Reject(driver.State, owner, play with { Destination = spell == Hunt ? "BASE" : "BATTLEFIELD:F" });
        await Reject(driver.State, owner, play with { SourceObjectId = "HUNT", CardNo = spell });
        var pool = driver.State.RunePools[owner];
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, owner, PlayCostPreviewTests.Request(driver.State, play));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(0, quote.Cost!.Mana); Assert.Equal(0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await driver.Act(owner, play);
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains("HUNT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(owner, driver.State.CardObjects["HOST"].OwnerId);
        Assert.Equal(owner, driver.State.CardObjects["HOST"].ControllerId);
        Assert.Equal(initial.CardObjects["HOST"].ObjectGeneration + 2, driver.State.CardObjects["HOST"].ObjectGeneration);
        Assert.Equal(destination == "BASE" ? null : destination["BATTLEFIELD:".Length..], driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        Assert.Equal(pool, driver.State.RunePools[owner]);
        Assert.Equal(owner, Assert.Single(driver.State.CardPlays.Resolved, p => p.Source.ObjectId == "HOST").PlayerId);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalHasteRemainsARealPaidCost(bool paid)
    {
        var (initial, driver) = await Open(unit: "UNL-006/219");
        var pool = driver.State.RunePools["P1"];
        var play = new PlayCardCommand("HOST", "UNL-006/219", [], Destination: "BATTLEFIELD:F", OptionalCosts: paid ? [HasteOptionalCostNames.HasteReady] : []);
        var quote = new CoreRuleEngine().PreviewPlayCard(driver.State, "P1", PlayCostPreviewTests.Request(driver.State, play));
        Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(paid ? 1 : 0, quote.Cost!.Mana);
        Assert.Equal(paid ? 1 : 0, quote.Cost.GenericPower + quote.Cost.PowerByTrait.Values.Sum());
        await driver.Act("P1", play);
        Assert.Equal(!paid, driver.State.CardObjects["HOST"].IsExhausted);
        Assert.Equal(pool.Mana - (paid ? 1 : 0), driver.State.RunePools["P1"].Mana);
        Assert.Equal(pool.TotalPower - (paid ? 1 : 0), driver.State.RunePools["P1"].TotalPower);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task MandatoryAdditionalSacrificeCannotBeWaived()
    {
        var initial = Position(unit: "OGN·208/298");
        initial = OfficialHoldSequenceTests.AddUnit(initial, "ALLY", "OGN·096/298", "F", "P1");
        initial = LocalDestructionRecallTests.At(initial, "ALLY", "P1", "BASE");
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", Hunt, ["HOST"])); await driver.Top();
        var play = new PlayCardCommand("HOST", "OGN·208/298", [], Destination: "BATTLEFIELD:F");
        await Reject(driver.State, "P1", play);
        await driver.Act("P1", play with { OptionalCosts = ["DESTROY_FRIENDLY_UNIT:ALLY"] });
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Battlefields);
        Assert.Contains("ALLY", driver.State.PlayerZones["P1"].Graveyard);
        Assert.Single(driver.Events(), e => e.Event.Kind == "UNIT_DESTROYED" && Equals(e.Event.Payload.GetValueOrDefault("targetObjectId"), "ALLY"));
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(Hunt, false)]
    [InlineData(Hunt, true)]
    [InlineData(Rescue, false)]
    [InlineData(Rescue, true)]
    public async Task ATokenIncludingACopiedPrintedFaceCeasesAfterBanishmentAndCannotBePlayed(string spell, bool copied)
    {
        var initial = Position(spell);
        var tokenNo = P6TokenFactoryCatalog.ImageTokenCardNo;
        var token = P6TokenFactoryCatalog.GetAll().Single(t => t.CardNo == tokenNo).CreateObject("HOST", "P1", "P1");
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["HOST"] = copied ? initial.CardObjects["HOST"] with { TokenFactoryCardNo = tokenNo,
                CopyFaceTransitions = [new(initial.Tick, initial.CardObjects["HOST"].ObjectGeneration, tokenNo, "OGN·096/298")] } : token } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", spell, ["HOST"])); await driver.Top();
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.False(driver.State.CardObjects.ContainsKey("HOST"));
        Assert.False(driver.State.ObjectLocations.ContainsKey("HOST"));
        Assert.DoesNotContain("HOST", driver.State.PlayerZones.Values.SelectMany(z => z.Base.Concat(z.Battlefields).Concat(z.Banished)));
        Assert.DoesNotContain(driver.State.CardPlays.Resolved, p => p.Source.ObjectId == "HOST");
        Assert.Contains("HUNT", driver.State.PlayerZones["P1"].Graveyard);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(Hunt, "owner")]
    [InlineData(Hunt, "source")]
    [InlineData(Hunt, "generation")]
    [InlineData(Hunt, "coordinated-generation")]
    [InlineData(Hunt, "destination")]
    [InlineData(Hunt, "optional")]
    [InlineData(Hunt, "all-costs")]
    [InlineData(Hunt, "base-mana")]
    [InlineData(Hunt, "base-power")]
    [InlineData(Hunt, "reduction")]
    [InlineData(Hunt, "zone")]
    [InlineData(Hunt, "parent-target")]
    [InlineData(Rescue, "owner")]
    [InlineData(Rescue, "generation")]
    [InlineData(Rescue, "destination")]
    [InlineData(Rescue, "optional")]
    [InlineData(Rescue, "all-costs")]
    [InlineData(Rescue, "base-mana")]
    public async Task IndependentRecoveryRejectsForgedOwnerSourceGenerationAndCostPermission(string spell, string mutation)
    {
        var (_, driver) = await Open(spell);
        var state = driver.State;
        var p = state.PendingEffectPlay!;
        var generation = p.Sources["HOST"];
        var bad = mutation switch {
            "owner" => p with { PlayerId = "P2" },
            "source" => p with { Sources = new Dictionary<string, long> { ["HUNT"] = generation } },
            "generation" or "coordinated-generation" => p with { Sources = new Dictionary<string, long> { ["HOST"] = generation + 1 } },
            "destination" => p with { DestinationPolicy = spell == Hunt ? "BASE" : "ANY_BATTLEFIELD" },
            "optional" => p with { Optional = true },
            "all-costs" => p with { IgnoreAllCosts = true },
            "base-mana" => p with { IgnoreBaseMana = false },
            "base-power" => p with { IgnoreBasePower = false },
            "reduction" => p with { ManaReduction = 1 },
            "zone" => p with { SourceZone = "HAND" },
            _ => p with { Parent = p.Parent with { TargetObjectIds = ["GEAR"] } }
        };
        var forged = state with { PendingEffectPlay = bad };
        if (mutation == "coordinated-generation") forged = forged with {
            CardObjects = new Dictionary<string, CardObjectState>(forged.CardObjects) {
                ["HOST"] = forged.CardObjects["HOST"] with { ObjectGeneration = generation + 1 } } };
        Assert.Contains("invalid banished unit effect-play continuation", OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Fact]
    public async Task RebuildingAllGenerationFieldsCannotRewriteTheOriginalAcceptedCommands()
    {
        var (initial, driver) = await Open();
        var p = driver.State.PendingEffectPlay!;
        var generation = p.Sources["HOST"] + 1;
        var forged = driver.State with {
            PendingEffectPlay = p with { Sources = new Dictionary<string, long> { ["HOST"] = generation },
                Parent = p.Parent with { TargetGenerations = new Dictionary<string, long> { ["HOST"] = generation - 1 } } },
            CardObjects = new Dictionary<string, CardObjectState>(driver.State.CardObjects) {
                ["HOST"] = driver.State.CardObjects["HOST"] with { ObjectGeneration = generation } } };
        // Structural consistency alone cannot establish the cause of a move.
        Assert.DoesNotContain("invalid banished unit effect-play continuation", OfficialInsightAndSpellLockTests.Errors(forged));
        var replay = await MatchActionLogReplayer.VerifyFinalStateAsync(initial, driver.Commands(), forged,
            new CoreRuleEngine(), default, driver.Events());
        Assert.False(replay.IsMatch);
        Assert.NotEqual(replay.ExpectedStateHash, replay.ReplayedStateHash);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task AnOlderUnitAbilityCannotBlockTheNewBanishedIncarnationsActualPlay()
    {
        var initial = AttachedRuleTextTests.WithSpell(EmpowerTests.Position(), "P1", Hunt);
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HOST", "VEN·074", []));
        await driver.Act("P1", new ActivateAbilityCommand("HOST", "EMPOWER:0:0", []));
        await driver.Act("P1", new PlayCardCommand("SPELL", Hunt, ["HOST"])); await driver.Top();
        Assert.Equal("BANISHED", driver.State.ObjectLocations["HOST"].Zone);
        var old = Assert.Single(driver.State.StackItems).EmpowerActivation!;
        var pending = driver.State.PendingEffectPlay!;
        await Reject(driver.State, "P1", new PlayCardCommand("HOST", "VEN·074", [], Destination: "BASE"));
        await driver.Act("P1", new PlayCardCommand("HOST", "VEN·074", [], Destination: "BATTLEFIELD:F"));
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Equal(old, Assert.Single(driver.State.StackItems).EmpowerActivation);
        Assert.Equal(pending.Sources["HOST"] + 1, driver.State.CardObjects["HOST"].ObjectGeneration);
        Assert.False(driver.State.CardObjects["HOST"].IsEmpowered);
        await driver.Top();
        Assert.False(driver.State.CardObjects["HOST"].IsEmpowered);
        await Replay(initial, driver);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnyBattlefieldPermissionStillRespectsPrintedProhibitionsAndCannotDeadlock(bool allBlocked)
    {
        var initial = Position();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["F"] = initial.CardObjects["F"] with { CardNo = "SFD·216/221" },
            ["OTHER"] = initial.CardObjects["OTHER"] with { CardNo = allBlocked ? "SFD·216/221" : "OGN·294/298" } } };
        var driver = new StandbyDefendObserverTests.Driver(initial);
        await driver.Act("P1", new PlayCardCommand("HUNT", Hunt, ["HOST"])); await driver.Top();
        var pending = driver.State.PendingEffectPlay!;
        await Reject(driver.State, "P1", new PlayCardCommand("HOST", "OGN·096/298", [], Destination: "BATTLEFIELD:F"));
        if (allBlocked)
        {
            await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
            Assert.Contains("HOST", driver.State.PlayerZones["P1"].Banished);
            Assert.DoesNotContain(driver.State.CardPlays.Resolved, p => p.Source.ObjectId == "HOST");
        }
        else
        {
            await driver.Act("P1", new PlayCardCommand("HOST", "OGN·096/298", [], Destination: "BATTLEFIELD:OTHER"));
            Assert.Equal("OTHER", driver.State.ObjectLocations["HOST"].BattlefieldObjectId);
        }
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains("HUNT", driver.State.PlayerZones["P1"].Graveyard);
        await Replay(initial, driver);
    }

    [Fact]
    public async Task AnImpossibleMandatoryAdditionalCostLeavesTheCardBanishedAndCompletesTheParent()
    {
        var (initial, driver) = await Open(unit: "OGN·208/298");
        var pending = driver.State.PendingEffectPlay!;
        await Reject(driver.State, "P1", new PlayCardCommand("HOST", "OGN·208/298", [], Destination: "BATTLEFIELD:F"));
        await driver.Act("P1", new ChooseCardsCommand(pending.ChoiceId, "EFFECT_PLAY", []));
        Assert.Null(driver.State.PendingEffectPlay);
        Assert.Contains("HOST", driver.State.PlayerZones["P1"].Banished);
        Assert.Contains("HUNT", driver.State.PlayerZones["P1"].Graveyard);
        Assert.DoesNotContain(driver.State.CardPlays.Resolved, p => p.Source.ObjectId == "HOST");
        await Replay(initial, driver);
    }
}
