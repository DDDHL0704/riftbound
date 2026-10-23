using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

// Explicit supported-number fixtures, not naturally accumulated game balances.
// Official resource gains are exact; unsupported output must never consume a
// destruction/exhaust cost and leave an unrecoverable or wrapped rune pool.
public sealed class ResourceGainCapacityTests
{
    [Theory]
    [InlineData("gold-unl", "wild", 0)] [InlineData("gold-unl", "typed", 0)]
    [InlineData("gold-sfd", "wild", 0)] [InlineData("gold-sfd", "typed", 0)]
    [InlineData("gold-renata", "mana", 0)] [InlineData("gold-renata", "restricted", 0)]
    [InlineData("gold-renata", "mixed-mana", 0)]
    [InlineData("malz-self", "wild", 0)] [InlineData("malz-self", "wild", 1)] [InlineData("malz-self", "typed", 1)]
    [InlineData("malz-other", "wild", 0)] [InlineData("malz-other", "wild", 1)] [InlineData("malz-other", "typed", 1)]
    [InlineData("sage", "mana", 0)] [InlineData("sage", "restricted", 0)]
    [InlineData("channel", "mana", 0)] [InlineData("channel", "restricted", 0)]
    [InlineData("honey", "wild", 0)] [InlineData("sigil", "typed", 0)] [InlineData("sigil", "wild", 0)]
    [InlineData("tap", "mana", 0)] [InlineData("tap", "restricted", 0)]
    [InlineData("recycle", "wild", 0)] [InlineData("recycle", "typed", 0)]
    public async Task UnsupportedResourceOutputRejectsBeforeAnyDestructionOrExhaustCost(string kind, string poolKind, int capacity)
    {
        var initial = Position(kind, poolKind, capacity);
        Assert.True(initial.RunePools["P1"].HasSupportedAmounts);
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(initial));
        var command = Command(kind);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("resource-capacity-reject", "P1", command.CmdType), command, default);
        Export(kind, poolKind, capacity, initial, result);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.UnsupportedCardBehavior, result.ErrorCode);
        Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State));
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [],
            candidate => candidate.Action == command.CmdType && (candidate.Sources ?? []).Any(source => source.Id == "SOURCE"));
    }

    [Theory]
    [InlineData("gold-unl", "wild", 1)] [InlineData("gold-unl", "typed", 1)]
    [InlineData("gold-sfd", "wild", 1)]
    [InlineData("gold-renata", "mana", 1)] [InlineData("gold-renata", "restricted", 1)]
    [InlineData("malz-self", "wild", 2)] [InlineData("malz-self", "typed", 2)]
    [InlineData("malz-other", "wild", 2)] [InlineData("malz-other", "typed", 2)]
    [InlineData("sage", "restricted", 1)] [InlineData("channel", "restricted", 1)]
    [InlineData("honey", "wild", 1)] [InlineData("sigil", "typed", 1)]
    [InlineData("tap", "restricted", 1)] [InlineData("recycle", "typed", 1)]
    public async Task ExactFittingOutputStillPaysTheCostOnceAndSurvivesColdRecoveryAndReplay(string kind, string poolKind, int capacity)
    {
        var initial = Position(kind, poolKind, capacity); var driver = new StandbyDefendObserverTests.Driver(initial);
        var result = await driver.Act("P1", BoundedResourceQuantityTests.Native(Command(kind)));
        Export(kind, poolKind, capacity, initial, result);
        var pool = driver.State.RunePools["P1"]; Assert.True(pool.HasSupportedAmounts);
        var powerGain = kind.StartsWith("malz") ? 2 : kind is "sage" or "channel" or "tap" ? 0 : 1;
        var manaGain = kind is "gold-renata" or "sage" or "channel" or "tap" ? 1 : 0;
        Assert.Equal(initial.RunePools["P1"].TotalPower + powerGain, pool.TotalPower);
        Assert.Equal(initial.RunePools["P1"].Mana + manaGain, pool.Mana);
        Assert.Equal(initial.RunePools["P1"].SpellOnlyMana, pool.SpellOnlyMana);
        var expectedTraits = initial.RunePools["P1"].PowerByTrait.ToDictionary(e => e.Key, e => e.Value);
        if (kind is "sigil" or "recycle") expectedTraits[RuneTrait.Red] = expectedTraits.GetValueOrDefault(RuneTrait.Red) + 1;
        Assert.Equal(expectedTraits, pool.PowerByTrait);
        if (kind.StartsWith("gold")) Assert.DoesNotContain("SOURCE", driver.State.CardObjects.Keys);
        else if (kind.StartsWith("malz"))
        {
            Assert.Contains(kind == "malz-self" ? "SOURCE" : "A", driver.State.PlayerZones["P1"].Graveyard);
            if (kind == "malz-other") Assert.True(driver.State.CardObjects["SOURCE"].IsExhausted);
        }
        else if (kind == "recycle") Assert.Contains("SOURCE", driver.State.PlayerZones["P1"].RuneDeck);
        else Assert.True(driver.State.CardObjects["SOURCE"].IsExhausted);
        Assert.Equal(powerGain > 0 ? 1 : 0, result.Events.Count(e => e.Kind == "POWER_GAINED"));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task HoneyfruitHidesOnlyTheUpgradedBranchWhenManaCapacityIsFull(int capacity)
    {
        var initial = Position("honey", "mana", capacity);
        initial = initial with { PlayerExperience = new Dictionary<string, int> { ["P1"] = 6, ["P2"] = 0 } };
        var option = P4ActivatedAbilityCatalog.HoneyfruitLevelSixOptionalCostPrefix + "SOURCE";
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Contains(prompt.Candidates ?? [], c => c.Action == CommandTypes.ActivateAbility && (c.Sources ?? []).Any(s => s.Id == "SOURCE"));
        var upgraded = new ActivateAbilityCommand("SOURCE", P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, [], [option]);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("resource-capacity-honey", "P1", upgraded.CmdType), upgraded, default);
        Assert.Equal(capacity == 1, result.Accepted);
        if (capacity == 0) { Assert.Empty(result.Events); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(result.State)); }
        else { Assert.Equal(int.MaxValue, result.State.RunePools["P1"].Mana); Assert.Equal(1, result.State.RunePools["P1"].Power); }
        var ordinary = await new CoreRuleEngine().ResolveAsync(initial, new("resource-capacity-honey-base", "P1", CommandTypes.ActivateAbility), Command("honey"), default);
        Assert.True(ordinary.Accepted, ordinary.ErrorMessage); Assert.Equal(initial.RunePools["P1"].Mana, ordinary.State.RunePools["P1"].Mana);
        Assert.Equal(1, ordinary.State.RunePools["P1"].Power);
        Assert.Equal(capacity == 1, JsonSerializer.Serialize(prompt, new JsonSerializerOptions(JsonSerializerDefaults.Web)).Contains(option, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("held")] [InlineData("ward")]
    public async Task PayingTheRealPendingCostMakesThePreviouslyFullResourceSourceAvailableAgain(string parentKind)
    {
        var engine = new CoreRuleEngine();
        var seed = parentKind == "held" ? OfficialHoldSequenceTests.State("SFD·214/221") : OfficialHeldConfirmationTests.WardPosition();
        var opened = await engine.ResolveAsync(seed, new("resource-capacity-parent", "P1", CommandTypes.PassPriority), new PassPriorityCommand(), default);
        Assert.True(opened.Accepted, opened.ErrorMessage);
        if (parentKind == "ward")
        {
            var target = opened.State.PendingCardChoice!;
            opened = await engine.ResolveAsync(opened.State, new("resource-capacity-target", "P1", CommandTypes.ChooseCards),
                new ChooseCardsCommand(target.ChoiceId, target.ChoiceWindow, ["ENEMY"]), default);
            Assert.True(opened.Accepted, opened.ErrorMessage);
        }
        var initial = AddBase(WithPool(opened.State, new(0, int.MaxValue)), "SOURCE", P4ActivatedAbilityCatalog.GoldTokenSfdCardNo);
        var parent = initial.PendingPayment!; Assert.Null(initial.PriorityPlayerId);
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.ActivateAbility && (c.Sources ?? []).Any(s => s.Id == "SOURCE"));
        var gold = Command("gold-sfd");
        var rejected = await engine.ResolveAsync(initial, new("resource-capacity-parent-reject", "P1", gold.CmdType), gold, default);
        Assert.False(rejected.Accepted); Assert.Equal(ErrorCodes.UnsupportedCardBehavior, rejected.ErrorCode);
        Assert.Empty(rejected.Events); Assert.Equal(MatchStateHasher.Hash(initial), MatchStateHasher.Hash(rejected.State));
        // This fixture starts after genuine parent-opening commands and an
        // explicit balance boundary. It is not a new tick-zero journal birth.
        var state = initial; var session = new MatchSession(state, engine, NoopMatchJournal.Instance);
        await Act(new PayCostCommand(parent.PaymentId, parent.PaymentWindow, ["PAY"]));
        Assert.Contains(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
            c => c.Action == CommandTypes.ActivateAbility && (c.Sources ?? []).Any(s => s.Id == "SOURCE"));
        await Act(gold);
        Assert.Equal(int.MaxValue - parent.PowerCost + 1, state.RunePools["P1"].TotalPower);
        Assert.DoesNotContain("SOURCE", state.CardObjects.Keys);

        async Task Act(GameCommand command)
        {
            var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var rawNode = JsonSerializer.SerializeToNode(command, command.GetType(), web)!.AsObject();
            rawNode["promptId"] = session.PromptFor("P1").PromptId; rawNode["snapshotTick"] = state.Tick;
            var raw = JsonSerializer.SerializeToElement(rawNode); command = GameCommandJsonMapper.Map(raw);
            var result = await session.SubmitAsync("P1", "capacity-parent-" + state.Tick, command, raw, default);
            Assert.True(result.Accepted, result.ErrorMessage); FullGameEndToEndTests.AssertNoHiddenZoneLeak(result);
            state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(result.State, web), web)!;
            Assert.Equal(MatchStateHasher.Hash(result.State), MatchStateHasher.Hash(state));
            Assert.Empty(OfficialInsightAndSpellLockTests.Errors(state)); ArmamentMightTests.PublicRecovery(state);
            session = new MatchSession(state, engine, NoopMatchJournal.Instance);
        }
    }

    private static MatchState Position(string kind, string poolKind, int capacity)
    {
        var pool = poolKind switch {
            "wild" => new RunePool(0, int.MaxValue - capacity),
            "typed" => new RunePool(0, 0, new Dictionary<string, int> { [RuneTrait.Blue] = int.MaxValue - capacity }),
            "mana" => new RunePool(int.MaxValue - capacity, 0),
            "restricted" => new RunePool(0, 0, spellOnlyMana: int.MaxValue - capacity),
            "mixed-mana" => new RunePool(1, 0, spellOnlyMana: int.MaxValue - 1 - capacity),
            _ => throw new ArgumentOutOfRangeException(nameof(poolKind)) };
        var no = kind.StartsWith("malz") ? P4ActivatedAbilityCatalog.MalzaharCardNo : kind switch {
            "gold-unl" => P4ActivatedAbilityCatalog.GoldTokenUnlCardNo,
            "sage" => P4ActivatedAbilityCatalog.DragonSoulSageCardNo, "channel" => P4ActivatedAbilityCatalog.EnergyChannelCardNo,
            "honey" => P4ActivatedAbilityCatalog.HoneyfruitCardNo, "sigil" => P4ActivatedAbilityCatalog.RageSigilCardNo,
            "tap" or "recycle" => "OGN·007/298", _ => P4ActivatedAbilityCatalog.GoldTokenSfdCardNo };
        var state = AddBase(WithPool(ImposedMovementContinuationTests.Position("standard"), pool), "SOURCE", no);
        if (kind == "gold-renata")
        {
            state = OfficialHoldSequenceTests.AddLegend(state, "SFD·201/221");
            state = state with { PlayerScores = new Dictionary<string, int> { ["P1"] = 5, ["P2"] = 0 } };
        }
        return state;
    }

    private static GameCommand Command(string kind) => kind switch {
        "tap" => new TapRuneCommand("SOURCE"), "recycle" => new RecycleRuneCommand("SOURCE"),
        _ => new ActivateAbilityCommand("SOURCE", kind.StartsWith("malz") ? P4ActivatedAbilityCatalog.MalzaharResourceAbilityId : kind switch {
            "gold-unl" => P4ActivatedAbilityCatalog.GoldTokenUnlResourceAbilityId,
            "sage" => P4ActivatedAbilityCatalog.DragonSoulSageResourceAbilityId, "channel" => P4ActivatedAbilityCatalog.EnergyChannelResourceAbilityId,
            "honey" => P4ActivatedAbilityCatalog.HoneyfruitResourceAbilityId, "sigil" => P4ActivatedAbilityCatalog.RageSigilResourceAbilityId,
            _ => P4ActivatedAbilityCatalog.GoldTokenSfdResourceAbilityId },
            kind.StartsWith("malz") ? [kind == "malz-self" ? "SOURCE" : "A"] : []) };

    private static void Export(string kind, string poolKind, int capacity, MatchState before, ResolutionResult result)
    {
        if (Environment.GetEnvironmentVariable("RIFTBOUND_RESOURCE_CAPACITY_EVIDENCE") is not { Length: > 0 } root) return;
        Directory.CreateDirectory(root); var assembly = typeof(CoreRuleEngine).Assembly;
        File.WriteAllText(Path.Combine(root, $"{kind}-{poolKind}-{capacity}.json"), JsonSerializer.Serialize(new { before, result,
            runtime = new { path = assembly.Location, mvid = assembly.ManifestModule.ModuleVersionId,
                informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))).ToLowerInvariant() } },
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }
}
