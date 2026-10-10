using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
namespace Riftbound.ConformanceTests;

// CN 357.2 / 429; Malzahar's official cost says friendly, not another friendly.
public sealed class OfficialDestructionCostRulesTests
{
    [Theory]
    [InlineData("UNIT")]
    [InlineData("EQUIPMENT")]
    [InlineData("SELF")]
    [InlineData("GOLD")]
    public async Task DestructionCostProducesOrdinaryPowerAndDoesNotResurrectSource(string target)
    {
        var state = State();
        var result = await Resolve(state, new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId,
            [target == "SELF" ? "MALZ" : target]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(2, result.State.RunePools["P1"].Power);
        RetiredPaymentLedgerTests.AssertNoLedger(result.State);
        var id = target == "SELF" ? "MALZ" : target;
        Assert.DoesNotContain(id, result.State.PlayerZones["P1"].Base);
        if (target == "GOLD")
        {
            Assert.DoesNotContain(id, result.State.CardObjects.Keys);
            Assert.DoesNotContain(id, result.State.ObjectLocations.Keys);
            Assert.DoesNotContain(id, result.State.PlayerZones["P1"].Graveyard);
            Assert.Single(result.Events, e => e.Kind == "TOKEN_CEASED_TO_EXIST");
        }
        else Assert.Contains(id, result.State.PlayerZones["P1"].Graveyard);
        Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State))!));
    }

    [Fact]
    public async Task DestroyingSentinelAsCostEnqueuesLastBreathAfterGainingResources()
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["UNIT"] = cards["UNIT"] with { CardNo = "OGN·096/298" };
        var result = await Resolve(state with { CardObjects = cards }, new ActivateAbilityCommand("MALZ",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(2, result.State.RunePools["P1"].Power);
        Assert.Single(result.State.StackItems);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
        for (var i = 0; i < 2; i++)
        {
            result = await Resolve(result.State, new PassPriorityCommand(), result.State.PriorityPlayerId!);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Single(result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
    }

    [Fact]
    public void SelfIsAnAvailableCostChoice()
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(State())["P1"].Candidates!, c => c.Action == CommandTypes.ActivateAbility);
        Assert.Contains(candidate.Targets!, t => t.Id == "MALZ");
    }

    [Fact]
    public async Task DuplicateIntentAndStalePromptCannotPayAgain()
    {
        var session = new MatchSession(State(), new CoreRuleEngine(), NoopMatchJournal.Instance);
        var prompt = session.PromptFor("P1");
        var command = new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]);
        var raw = JsonSerializer.SerializeToElement(new { cmdType = command.CmdType, sourceObjectId = "MALZ",
            abilityId = command.AbilityId, targetObjectIds = new[] { "UNIT" }, promptId = prompt.PromptId, snapshotTick = prompt.SnapshotTick });
        var first = await session.SubmitAsync("P1", "cost-once", command, raw, default);
        Assert.True(first.Accepted, first.ErrorMessage);
        var duplicate = await session.SubmitAsync("P1", "cost-once", command, raw, default);
        var stale = await session.SubmitAsync("P1", "new-stale-intent", command, raw, default);
        Assert.True(duplicate.Accepted);
        Assert.False(stale.Accepted);
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(duplicate.State));
        Assert.Equal(MatchStateHasher.Hash(first.State), MatchStateHasher.Hash(stale.State));
        Assert.Equal(2, stale.State.RunePools["P1"].Power);
    }

    [Theory]
    [InlineData("SFD·135/221", "GOLD", true)]
    [InlineData("SFD·135/221", "EQUIPMENT", false)]
    [InlineData("OGN·022/298", "GOLD", true)]
    public async Task SpellDrivenNonfieldTransitionRemovesTokensButKeepsPhysicalCards(string spell, string target, bool token)
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["SPELL"] = new("SPELL", cardNo: spell, tags: [CardObjectTags.SpellCard], ownerId: "P1", controllerId: "P1");
        var zones = state.PlayerZones.ToDictionary(e => e.Key, e => e.Value);
        zones["P1"] = zones["P1"] with { Hand = ["SPELL"] };
        state = state with { CardObjects = cards, PlayerZones = zones,
            RunePools = new Dictionary<string, RunePool> { ["P1"] = new(10, 10), ["P2"] = RunePool.Empty } };
        var result = await Resolve(state, new PlayCardCommand("SPELL", spell, spell == "OGN·022/298" ? [] : [target]));
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; i < 2; i++)
        {
            result = await Resolve(result.State, new PassPriorityCommand(), result.State.PriorityPlayerId!);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        if (token)
        {
            Assert.DoesNotContain(target, result.State.CardObjects.Keys);
            Assert.DoesNotContain(target, result.State.ObjectLocations.Keys);
            Assert.All(result.State.PlayerZones.Values, z => Assert.DoesNotContain(target,
                z.Hand.Concat(z.Graveyard).Concat(z.MainDeck).Concat(z.Banished).Concat(z.Base).Concat(z.Battlefields)));
        }
        else
        {
            Assert.Contains(target, result.State.PlayerZones["P1"].Hand);
            Assert.Equal("OGN·098/298", result.State.CardObjects[target].CardNo);
            Assert.True(result.State.CardObjects[target].ObjectGeneration > state.CardObjects[target].ObjectGeneration);
        }
    }

    [Theory]
    [InlineData("opponent")]
    [InlineData("exhausted")]
    [InlineData("missing-position")]
    [InlineData("reaction-window")]
    public async Task IllegalCostsAndNonSwiftWindowsDoNotMutateState(string fault)
    {
        var state = State();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        if (fault == "opponent") cards["UNIT"] = cards["UNIT"] with { ControllerId = "P2" };
        if (fault == "exhausted") cards["MALZ"] = cards["MALZ"] with { IsExhausted = true };
        state = state with { CardObjects = cards };
        if (fault == "missing-position") state = state with { ObjectLocations = new Dictionary<string, ObjectLocationState>() };
        if (fault == "reaction-window") state = state with { TimingState = TimingStates.NeutralClosed, PriorityPlayerId = "P1",
            StackItems = [new("PENDING", "P2", "SPELL", "PENDING")] };
        var result = await Resolve(state, new ActivateAbilityCommand("MALZ", P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["UNIT"]));
        Assert.False(result.Accepted);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData("UNIT")]
    [InlineData("MALZ")]
    public async Task ReplacedDestructionStillPaysCostWithoutLastBreath(string target)
    {
        // CN 203.2 / 357.2.a explicitly use Zhonya's as the replacement-cost example.
        var state = State();
        var cards = state.CardObjects.ToDictionary(e => e.Key, e => e.Value);
        cards["EQUIPMENT"] = cards["EQUIPMENT"] with { CardNo = "OGN·077/298" };
        cards["UNIT"] = cards["UNIT"] with { CardNo = "OGN·096/298" };
        var result = await Resolve(state with { CardObjects = cards }, new ActivateAbilityCommand("MALZ",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, [target]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(2, result.State.RunePools["P1"].Power);
        Assert.Contains(target, result.State.PlayerZones["P1"].Base);
        Assert.True(result.State.CardObjects[target].IsExhausted);
        Assert.Contains("EQUIPMENT", result.State.PlayerZones["P1"].Graveyard);
        Assert.Empty(result.State.StackItems);
        Assert.Empty(result.State.PlayerZones["P1"].Hand);
    }

    [Fact]
    public async Task NativeAcceptanceScenarioUsesAuthoritativePositionsAndPlayableSources()
    {
        var session = new MatchSession("resource-native", new CoreRuleEngine());
        session.EnsurePlayer("P1"); session.EnsurePlayer("P2");
        var seeded = await session.SeedScenarioAsync("P1", "seed", "official-resource-costs", null, default);
        var result = await Resolve(seeded.State, new ActivateAbilityCommand("QA-GOLD",
            P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId, []));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(1, result.State.RunePools["P1"].Power);
        result = await Resolve(result.State, new ActivateAbilityCommand("QA-MALZAHAR",
            P4ActivatedAbilityCatalog.MalzaharResourceAbilityId, ["QA-SENTINEL"]));
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(3, result.State.RunePools["P1"].Power);
        Assert.Single(result.State.StackItems);
    }

    private static ValueTask<ResolutionResult> Resolve(MatchState state, GameCommand command, string player = "P1") =>
        new CoreRuleEngine().ResolveAsync(state, new("cost", player, command.CmdType), command, default);
    private static MatchState State()
    {
        var cards = new Dictionary<string, CardObjectState>
        {
            ["MALZ"] = new("MALZ", cardNo: "OGN·113/298", power: 3, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["UNIT"] = new("UNIT", cardNo: "SFD·125/221", power: 2, tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1"),
            ["EQUIPMENT"] = new("EQUIPMENT", cardNo: "OGN·098/298", tags: [CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1"),
            ["GOLD"] = new("GOLD", cardNo: "SFD·T03", tags: [CardObjectTags.EquipmentCard], ownerId: "P1", controllerId: "P1"),
            ["DRAW"] = new("DRAW", cardNo: "SFD·125/221", tags: [CardObjectTags.UnitCard], ownerId: "P1", controllerId: "P1")
        };
        return new("COST-RULES", 0, 3, "P1", new Dictionary<string,string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, phase: MatchPhases.Main, timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string,RunePool> { ["P1"] = RunePool.Empty, ["P2"] = RunePool.Empty }, cardObjects: cards,
            playerZones: new Dictionary<string,PlayerZones> { ["P1"] = PlayerZones.Empty with { Base = ["MALZ", "UNIT", "EQUIPMENT", "GOLD"], MainDeck = ["DRAW"] }, ["P2"] = PlayerZones.Empty },
            objectLocations: cards.Keys.ToDictionary(id => id, id => new ObjectLocationState("P1", id == "DRAW" ? "MAIN_DECK" : "BASE")));
    }
}
