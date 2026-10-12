using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class OrbOfRegretTests
{
    internal const string Card = "OGN·090/298";
    internal const string Ability = "OGN_090_298_UNIT_POWER_MODIFIER_UNTIL_END_OF_TURN";
    internal static MatchState Board(int might = 1, bool aura = true)
    {
        var s = CurrentPowerTargetTests.Position("OGN·095/298", might, aura);
        return s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["ORB"] = new("ORB", cardNo: Card, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.EquipmentCard]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = [..s.PlayerZones["P1"].Base, "ORB"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ORB"] = new("P1", "BASE") } };
    }
    internal static ActivateAbilityCommand Command(string target = "D") => new("ORB", Ability, [target]);

    [Fact]
    public void CurrentOfficialEquipmentOffersItsTargetedExhaustAbility()
    {
        var prompt = ResolutionResult.BuildPrompts(Board())["P1"];
        var candidate = Assert.Single(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "ORB"));
        Assert.Equal(Ability, requirement["abilityId"]);
        Assert.Equal(1, requirement["minTargetCount"]);
        Assert.True(Assert.IsType<bool>(requirement["exhaustsSource"]));
    }

    [Fact]
    public async Task ActivationPaysExhaustBeforeResponseAndAppliesCurrentMightOnlyOnResolution()
    {
        var s = Board();
        var activated = await Act(s, "P1", Command());
        Assert.True(activated.State.CardObjects["ORB"].IsExhausted);
        Assert.Equal(1, activated.State.CardObjects["D"].Power);
        Assert.Single(activated.State.StackItems);
        Restore(activated.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(activated.State));
        var done = await Top(activated.State);
        Assert.Equal(0, done.State.CardObjects["D"].Power);
        Assert.Equal(1, CurrentPowerTargetTests.DisplayPower(done.State));
        Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
    }

    [Theory]
    [InlineData(0)] [InlineData(-2)]
    public async Task MinimumDoesNotRaiseAnAlreadyWeakerUnit(int might)
    {
        var done = await Top((await Act(Board(might, false), "P1", Command())).State);
        Assert.Equal(might, done.State.CardObjects["D"].Power);
        Assert.Empty(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Restore(done.State);
    }

    [Theory]
    [InlineData("exhausted")] [InlineData("opponent")] [InlineData("hidden")]
    [InlineData("wrong-card")] [InlineData("wrong-type")] [InlineData("off-field")]
    public async Task InvalidSourcesAreNotOfferedAndRejectWithoutMutation(string change)
    {
        var s = Board(); var orb = s.CardObjects["ORB"];
        orb = change switch {
            "exhausted" => orb with { IsExhausted = true },
            "opponent" => orb with { ControllerId = "P2" },
            "hidden" => orb with { IsFaceDown = true },
            "wrong-card" => orb with { CardNo = "OGN·091/298" },
            "wrong-type" => orb with { Tags = [CardObjectTags.UnitCard] },
            _ => orb
        };
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["ORB"] = orb } };
        if (change == "off-field") s = s with {
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) {
                ["P1"] = s.PlayerZones["P1"] with { Base = s.PlayerZones["P1"].Base.Where(id => id != "ORB").ToArray(), Hand = ["ORB"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ORB"] = new("P1", "HAND") } };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(s)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.ActivateAbility && c.Enabled && (c.Sources ?? []).Any(choice => choice.Id == "ORB"));
        await Reject(s, Command());
    }

    [Theory]
    [InlineData("opponent-turn")] [InlineData("closed")]
    [InlineData("no-target")] [InlineData("duplicate")] [InlineData("gear")]
    [InlineData("hidden-target")] [InlineData("unnecessary-recycle")]
    public async Task InvalidTimingAndTargetsRejectWithoutPayment(string change)
    {
        var s = Board(); var command = Command();
        if (change == "opponent-turn") s = s with { ActivePlayerId = "P2", TurnPlayerId = "P2" };
        if (change == "closed") s = s with { TimingState = TimingStates.NeutralClosed };
        if (change == "hidden-target") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { IsFaceDown = true } } };
        command = change switch {
            "no-target" => command with { TargetObjectIds = [] },
            "duplicate" => command with { TargetObjectIds = ["D", "D"] },
            "gear" => Command("ORB"),
            "unnecessary-recycle" => command with { OptionalCosts = ["RECYCLE_RUNE:missing"] },
            _ => command
        };
        await Reject(s, command);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OpponentDeflectIsPaidBeforeSourceExhausts(bool recycle)
    {
        var s = Board(4, false) with { RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(20, 20) } };
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { CardNo = "SFD·125/221", Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(s)["P1"].Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "ORB"));
        var choices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.DoesNotContain(choices["0"], choice => choice.Id == "D");
        await Reject(s, Command());
        var command = Command();
        if (recycle) {
            s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
                ["RUNE"] = new("RUNE", cardNo: "UNL-R01", ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.RuneCard, "COLOR:RED"]) },
                PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { Base = [..s.PlayerZones["P1"].Base, "RUNE"] } },
                ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["RUNE"] = new("P1", "BASE") } };
            command = command with { OptionalCosts = ["RECYCLE_RUNE:RUNE"] };
        } else s = s with { RunePools = new Dictionary<string, RunePool>(s.RunePools) { ["P1"] = new(0, 1) } };
        var activated = await Act(s, "P1", command);
        Assert.True(activated.State.CardObjects["ORB"].IsExhausted);
        Assert.Equal(0, activated.State.RunePools["P1"].TotalPower);
        var cost = Assert.Single(activated.Events, e => e.Kind == "COST_PAID");
        Assert.Equal(1, cost.Payload["spellshieldTaxPower"]);
        if (recycle) Assert.Contains("RUNE", activated.State.PlayerZones["P1"].RuneDeck);
        Restore(activated.State);
        var done = await Top(activated.State); Assert.Equal(3, done.State.CardObjects["D"].Power); Restore(done.State);
    }

    [Fact]
    public async Task FriendlyDeflectDoesNotChargePower()
    {
        var s = LocalDestructionRecallTests.At(Board(4, false), "D", "P1", "BASE") with {
            RunePools = new Dictionary<string, RunePool> { ["P1"] = RunePool.Empty, ["P2"] = new(20, 20) } };
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { Tags = [CardObjectTags.UnitCard, CardObjectTags.Spellshield] } } };
        var activated = await Act(s, "P1", Command());
        Assert.Equal(0, Assert.Single(activated.Events, e => e.Kind == "COST_PAID").Payload["spellshieldTaxPower"]);
        Assert.Equal(3, (await Top(activated.State)).State.CardObjects["D"].Power);
    }

    [Fact]
    public async Task SuccessfulEnemyTargetingUsesTheSharedLegendObserver()
    {
        var s = Board();
        s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["EZREAL"] = new("EZREAL", cardNo: "SFD·199/221", ownerId: "P1", controllerId: "P1", tags: ["LEGEND_CARD"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with { LegendZone = ["EZREAL"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["EZREAL"] = new("P1", "LEGEND") } };
        var activated = await Act(s, "P1", Command());
        Assert.Contains("EZREAL_ENEMY_TARGETS_THIS_TURN:P1:1", activated.State.UntilEndOfTurnEffects);
    }

    [Fact]
    public async Task CounterLeavesTheExhaustCostPaidAndTheUnitUnmodified()
    {
        var s = EquipChainTests.Response(Board(4, false), "SFD·045/221", "P2");
        var activated = await Act(s, "P1", Command());
        var pending = (await Act(activated.State, "P1", new PassPriorityCommand())).State;
        var countered = await Act(pending, "P2", new PlayCardCommand("RESPONSE", "SFD·045/221", [pending.StackItems.Single().StackItemId]));
        var done = await Top(countered.State);
        Assert.Empty(done.State.StackItems); Assert.True(done.State.CardObjects["ORB"].IsExhausted);
        Assert.Equal(4, done.State.CardObjects["D"].Power); Assert.Empty(done.State.CardObjects["D"].UntilEndOfTurnPowerModifiers);
        Restore(done.State); Assert.Empty(MinimumMightAuditTests.PublicErrors(done.State));
    }

    [Theory]
    [InlineData("source-left", 3)] [InlineData("target-new-object", 4)]
    [InlineData("aura-left", 1)] [InlineData("target-hidden", 4)]
    public async Task ResolutionUsesTheCurrentTargetAndIsIndependentOfTheSource(string change, int expected)
    {
        var activated = await Act(Board(change == "aura-left" ? 1 : 4, change == "aura-left"), "P1", Command());
        var s = activated.State;
        if (change == "source-left") s = s with {
            CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) { ["ORB"] = s.CardObjects["ORB"] with { ObjectGeneration = 1 } },
            PlayerZones = new Dictionary<string, PlayerZones>(s.PlayerZones) { ["P1"] = s.PlayerZones["P1"] with {
                Base = s.PlayerZones["P1"].Base.Where(id => id != "ORB").ToArray(), Graveyard = ["ORB"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(s.ObjectLocations) { ["ORB"] = new("P1", "GRAVEYARD") } };
        if (change == "target-new-object") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { ObjectGeneration = 1 } } };
        if (change == "target-hidden") s = s with { CardObjects = new Dictionary<string, CardObjectState>(s.CardObjects) {
            ["D"] = s.CardObjects["D"] with { IsFaceDown = true } } };
        if (change == "aura-left") s = LocalDestructionRecallTests.At(s, "GAREN", "P2", "BASE");
        var done = await Top(s); Assert.Equal(expected, done.State.CardObjects["D"].Power); Restore(done.State);
    }

    [Theory]
    [InlineData("missing")] [InlineData("unknown-ability")] [InlineData("null-source")]
    [InlineData("future-generation")] [InlineData("forged-card")]
    [InlineData("repeat")] [InlineData("destination")] [InlineData("movement-plan")]
    public async Task RecoveryRejectsForgedAbilityIdentityAndEffectShape(string change)
    {
        var s = (await Act(Board(), "P1", Command())).State;
        var item = Assert.Single(s.StackItems); var context = item.TargetedPowerAbility!;
        item = change switch {
            "missing" => item with { TargetedPowerAbility = null },
            "unknown-ability" => item with { TargetedPowerAbility = context with { AbilityId = "FORGED" } },
            "null-source" => item with { TargetedPowerAbility = context with { SourceObjectId = null! }, SourceObjectId = null! },
            "future-generation" => item with { TargetedPowerAbility = context with { SourceGeneration = 100 } },
            "forged-card" => item with { CardNo = "OGN·091/298" },
            "repeat" => item with { RepeatExecutions = [new(Ability, ["D"], new Dictionary<string, long> { ["D"] = 0 })] },
            "destination" => item with { Destination = "BASE" },
            "movement-plan" => item with { MovementDestinations = ["BASE"] },
            _ => item
        };
        var errors = OfficialInsightAndSpellLockTests.Errors(s with { StackItems = [item] });
        Assert.Contains(errors, e => e.Contains("targeted power ability", StringComparison.Ordinal));
    }

    private static async Task Reject(MatchState s, ActivateAbilityCommand command)
    {
        var result = await new CoreRuleEngine().ResolveAsync(s, new("invalid-orb", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(s), MatchStateHasher.Hash(result.State));
        Assert.Empty(result.Events);
    }
}
