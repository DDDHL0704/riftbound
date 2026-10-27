using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.ImposedMovementContinuationTests;

namespace Riftbound.ConformanceTests;

public sealed class RunePaymentCapacityTests
{
    private static readonly Type Reachability = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.RunePaymentReachability")!;
    private static readonly Type PromptBuilder = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.ActionPromptBuilder")!;
    private static readonly MethodInfo CanPay = Reachability.GetMethod("CanPay", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)]
    public void RestrictedBalancesOccupyTheSamePhysicalPowerCapacity(bool spell, int capacity)
    {
        var pool = new RunePool(0, 0, spellOnlyPower: spell ? int.MaxValue - capacity : 0,
            equipmentOnlyPower: spell ? 0 : int.MaxValue - capacity);
        var resources = new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Green] = 1 };
        var summary = Activator.CreateInstance(Reachability, BindingFlags.Instance | BindingFlags.NonPublic, null, [pool, resources], null)!;
        Assert.Equal(capacity, Reachability.GetProperty("Capacity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(summary));
        Assert.Equal(capacity, Reachability.GetProperty("MaximumTotal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(summary));
        Assert.Equal(capacity >= 1, (bool)CanPay.Invoke(summary, [0, new Dictionary<string, int> { [RuneTrait.Red] = 1 }])!);
        Assert.Equal(capacity >= 2, (bool)CanPay.Invoke(summary, [2, new Dictionary<string, int>()])!);
        Assert.Equal(capacity >= 2, (bool)CanPay.Invoke(summary, [0, resources])!);
        var eligible = PaymentCostRules.PowerPoolForPurpose(pool,
            spell ? PowerPaymentPurpose.PlaySpell : PowerPaymentPurpose.Equipment);
        Assert.True(PaymentCostRules.CanPayPowerCost(eligible, 2, new Dictionary<string, int>()));
    }

    [Fact]
    public void JointAffordabilityMatchesAnIndependentEnumerationOfEveryRepresentableRuneSubset()
    {
        var checks = 0;
        foreach (var capacity in new[] { 0, 1, 2, 3, 8 })
        for (var wild = 0; wild <= 1; wild++)
        for (var red = 0; red <= 1; red++)
        for (var green = 0; green <= 1; green++)
        for (var redRunes = 0; redRunes <= 2; redRunes++)
        for (var greenRunes = 0; greenRunes <= 1; greenRunes++)
        for (var wildRunes = 0; wildRunes <= 1; wildRunes++)
        {
            var pool = new RunePool(0, wild, new Dictionary<string, int> {
                [RuneTrait.Red] = red, [RuneTrait.Green] = green, [RuneTrait.Blue] = int.MaxValue - capacity - wild - red - green });
            var resources = new Dictionary<string, int> { [RuneTrait.Red] = redRunes, [RuneTrait.Green] = greenRunes, [""] = wildRunes };
            var summary = Activator.CreateInstance(Reachability, BindingFlags.Instance | BindingFlags.NonPublic, null, [pool, resources], null)!;
            var runes = Enumerable.Repeat(RuneTrait.Red, redRunes).Concat(Enumerable.Repeat(RuneTrait.Green, greenRunes)).Concat(Enumerable.Repeat("", wildRunes)).ToArray();
            var wallets = new List<RunePool>();
            for (var subset = 0; subset < 1 << runes.Length; subset++)
            {
                var selected = runes.Where((_, i) => (subset & 1 << i) != 0).ToArray();
                if (selected.Length > capacity) continue;
                var typed = pool.PowerByTrait.ToDictionary(p => p.Key, p => p.Value);
                foreach (var trait in selected.Where(t => t.Length > 0)) typed[trait] = typed.GetValueOrDefault(trait) + 1;
                wallets.Add(new(0, wild + selected.Count(t => t.Length == 0), typed));
            }
            for (var requiredRed = 0; requiredRed <= 2; requiredRed++)
            for (var requiredGreen = 0; requiredGreen <= 2; requiredGreen++)
            for (var generic = 0; generic <= 2; generic++)
            {
                var cost = new Dictionary<string, int> { [RuneTrait.Red] = requiredRed, [RuneTrait.Green] = requiredGreen };
                // This existing actual-wallet authorization sees each physical
                // subset separately; it never sees prospective trait bounds.
                var expected = wallets.Any(wallet => PaymentCostRules.CanPayPowerCost(wallet, generic, cost));
                Assert.Equal(expected, (bool)CanPay.Invoke(summary, [generic, cost])!); checks++;
            }
        }
        Assert.Equal(12960, checks);
    }

    [Theory]
    [InlineData(false, int.MaxValue, 0, 0, true)]
    [InlineData(false, int.MaxValue - 2, 1, 1, false)]
    [InlineData(false, int.MaxValue - 1, 1, 1, false)]
    [InlineData(true, int.MaxValue - 2, 1, 1, true)]
    public void MaximumGenericCostsStillRespectTheJointTypedDeficitAndCurrentWildcard(bool wildcard, int generic, int red, int green, bool expected)
    {
        var pool = new RunePool(0, wildcard ? 1 : 0, new Dictionary<string, int> { [RuneTrait.Blue] = int.MaxValue - (wildcard ? 2 : 1) });
        var resources = new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Green] = 1 };
        var summary = Activator.CreateInstance(Reachability, BindingFlags.Instance | BindingFlags.NonPublic, null, [pool, resources], null)!;
        Assert.Equal(expected, (bool)CanPay.Invoke(summary, [generic, new Dictionary<string, int> { [RuneTrait.Red] = red, [RuneTrait.Green] = green }])!);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(8)]
    public void PublicCapacityMetadataKeepsTheActualPoolAndAllIndividuallyLegalRuneChoices(int capacity)
    {
        var state = Position(capacity); var requirement = Requirement(ResolutionResult.BuildPrompts(state)["P1"]);
        Assert.Equal(int.MaxValue - capacity, requirement["availablePower"]!.GetValue<int>());
        Assert.Equal(int.MaxValue - capacity, requirement["availablePowerByTrait"]![RuneTrait.Blue]!.GetValue<int>());
        Assert.Equal(int.MaxValue - capacity + Math.Min(capacity, 2), requirement["availablePowerWithPaymentResources"]!.GetValue<int>());
        var ids = requirement["paymentResourceChoices"]!.AsArray().Select(n => n!["id"]!.GetValue<string>()).ToArray();
        if (capacity == 0) Assert.DoesNotContain(ids, id => id.StartsWith("RECYCLE_RUNE:", StringComparison.Ordinal));
        else Assert.Equal(new[] { "RECYCLE_RUNE:GREEN", "RECYCLE_RUNE:RUNE" }, ids.Order());
        if (capacity == 1)
        {
            Assert.Equal(1, requirement["paymentResourcePowerCapacity"]!.GetValue<int>());
            Assert.False(requirement.ContainsKey("availablePowerByTraitWithPaymentResources"));
            Assert.Equal("red", requirement["paymentResourcePowerByChoice"]!["RECYCLE_RUNE:RUNE"]!["trait"]!.GetValue<string>());
            Assert.Equal("green", requirement["paymentResourcePowerByChoice"]!["RECYCLE_RUNE:GREEN"]!["trait"]!.GetValue<string>());
        }
        else
        {
            Assert.False(requirement.ContainsKey("paymentResourcePowerCapacity"));
            Assert.True(requirement.ContainsKey("availablePowerByTraitWithPaymentResources"));
        }
        Assert.Empty(Errors(state));
    }

    [Theory]
    [InlineData(false, "red")] [InlineData(false, "green")]
    [InlineData(true, "red")]
    public async Task OneCapacitySlotKeepsBothSameOrDifferentColorSourcesAndRejectsTakingBothAtomically(bool sameColor, string trait)
    {
        var no = trait == "red" ? "SFD·006/221" : "SFD·047/221";
        var initial = Position(1, sameColor, no); var engine = new CoreRuleEngine(); var before = MatchStateHasher.Hash(initial);
        var two = new PlayCardCommand("CARD", no, [], OptionalCosts: ["RECYCLE_RUNE:RUNE", "RECYCLE_RUNE:GREEN"]);
        var quote = engine.PreviewPlayCard(initial, "P1", PlayCostPreviewTests.Request(initial, two));
        Assert.False(quote.CanPay);
        var rejected = await engine.ResolveAsync(initial, new("two-runes-over-capacity", "P1", two.CmdType), two, default);
        Assert.False(rejected.Accepted); Assert.Empty(rejected.Events); Assert.Equal(before, MatchStateHasher.Hash(rejected.State));
        var driver = new StandbyDefendObserverTests.Driver(initial);
        var source = trait == "red" ? "RUNE" : "GREEN";
        await driver.Act("P1", new PlayCardCommand("CARD", no, [], OptionalCosts: ["RECYCLE_RUNE:" + source]));
        Assert.Contains(source, driver.State.PlayerZones["P1"].RuneDeck);
        Assert.Contains(source == "RUNE" ? "GREEN" : "RUNE", driver.State.PlayerZones["P1"].Base);
        Assert.Equal(int.MaxValue - 1, driver.State.RunePools["P1"].PowerByTrait[RuneTrait.Blue]);
        Assert.Equal(0, driver.State.RunePools["P1"].PowerByTrait.GetValueOrDefault(trait));
        await RideTheWindCurrentMovementTests.Replay(initial, driver);
    }

    [Theory]
    [InlineData("wrong-capacity")] [InlineData("missing-capacity")]
    [InlineData("wrong-total")] [InlineData("missing-choice")] [InlineData("extra-choice")]
    [InlineData("fake-joint-vector")] [InlineData("wrong-per-choice")]
    [InlineData("missing-old-field-nonbinding")] [InlineData("extra-capacity-nonbinding")]
    public void RecoveryRejectsForgedCapacityShapesWithoutMakingTheOldTypedFieldGloballyOptional(string attack)
    {
        var state = Position(attack.EndsWith("nonbinding", StringComparison.Ordinal) ? 2 : 1);
        Assert.Empty(Errors(state));
        var prompt = JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state)["P1"], Web)!;
        var requirement = Requirement(prompt);
        switch (attack)
        {
            case "wrong-capacity": requirement["paymentResourcePowerCapacity"] = 2; break;
            case "missing-capacity": requirement.Remove("paymentResourcePowerCapacity"); break;
            case "wrong-total": requirement["availablePowerWithPaymentResources"] = int.MaxValue - 1; break;
            case "missing-choice": requirement["paymentResourceChoices"]!.AsArray().RemoveAt(0); break;
            case "extra-choice": requirement["paymentResourceChoices"]!.AsArray().Add(JsonSerializer.SerializeToNode(new ActionPromptChoiceDto("RECYCLE_RUNE:FORGED", "forged", "forged"), Web)); break;
            case "fake-joint-vector": requirement["availablePowerByTraitWithPaymentResources"] = JsonSerializer.SerializeToNode(new Dictionary<string, int> { ["red"] = 1, ["green"] = 1, ["blue"] = int.MaxValue - 1 }); break;
            case "wrong-per-choice": requirement["paymentResourcePowerByChoice"]!["RECYCLE_RUNE:RUNE"]!["trait"] = "green"; break;
            case "missing-old-field-nonbinding": requirement.Remove("availablePowerByTraitWithPaymentResources"); break;
            case "extra-capacity-nonbinding": requirement["paymentResourcePowerCapacity"] = 1; break;
        }
        Assert.NotEmpty(Errors(state, prompt.Deserialize<ActionPromptDto>(Web)!));
    }

    [Fact]
    public void GenuineNativeWebJsonAndReorderedObjectPropertiesKeepTheirResourceProjection()
    {
        var state = Position(1);
        var node = Reverse(JsonSerializer.SerializeToNode(ResolutionResult.BuildPrompts(state)["P1"], Web));
        var prompt = node!.Deserialize<ActionPromptDto>(Web)!;
        Assert.Empty(Errors(state, prompt));
        static JsonNode? Reverse(JsonNode? value) => value switch {
            JsonObject obj => new JsonObject(obj.Reverse().Select(p => KeyValuePair.Create(p.Key, Reverse(p.Value)))),
            JsonArray array => new JsonArray(array.Select(Reverse).ToArray()), _ => value?.DeepClone() };
    }

    [Theory]
    [InlineData("ready", 1, false)] [InlineData("ready", 2, true)]
    [InlineData("steal", 1, false)] [InlineData("steal", 2, true)]
    public void OptionalReadyAndStealPredicatesCheckPrintedAndAdditionalCostsTogether(string branch, int capacity, bool expected)
    {
        var state = Position(capacity, no: "SFD·006/221");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P2"] = state.PlayerZones["P2"] with { Base = ["GEAR"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·059/221", "P2") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["GEAR"] = new("P2", "BASE") } };
        Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·006/221", out var behavior));
        behavior = behavior with { SourceReadyAdditionalPowerCost = 1, SourceReadyAdditionalPowerTrait = RuneTrait.Green,
            PlaysSourceToBaseAsUnit = true, SourceStealEnemyEquipmentAdditionalPowerCost = 1,
            SourceStealEnemyEquipmentAdditionalPowerTrait = RuneTrait.Green, SourceStealEnemyEquipmentOptionalCostPrefix = "AKSHAN_STEAL_EQUIPMENT:" };
        var resources = new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Green] = 1 };
        var method = PromptBuilder.GetMethod(branch == "ready" ? "CanPaySourceReadyOptionalCost" : "CanPromptSourceStealEnemyEquipmentOptionalCost", BindingFlags.Static | BindingFlags.NonPublic)!;
        object?[] arguments = branch == "ready" ? [state, "P1", state.RunePools["P1"], resources, behavior] : [state, "P1", behavior, state.RunePools["P1"], resources, "CARD"];
        Assert.Equal(expected, (bool)method.Invoke(null, arguments)!);
    }

    [Theory]
    [InlineData("activate", 1, false)] [InlineData("activate", 2, true)]
    [InlineData("assemble", 1, false)] [InlineData("assemble", 2, true)]
    [InlineData("optional-reduction", 1, false)] [InlineData("optional-reduction", 2, true)]
    public void ExistingCostPredicatesUseOnlyJointlyReachablePower(string consumer, int capacity, bool expected)
    {
        // Explicit cost-predicate boundaries, not new registered card effects.
        var state = Position(capacity); var pool = state.RunePools["P1"];
        var resources = new Dictionary<string, int> { [RuneTrait.Red] = 1, [RuneTrait.Green] = 1 };
        var byChoice = new Dictionary<string, IReadOnlyDictionary<string, object?>> {
            ["RECYCLE_RUNE:RUNE"] = new Dictionary<string, object?> { ["trait"] = "red", ["power"] = 1 },
            ["RECYCLE_RUNE:GREEN"] = new Dictionary<string, object?> { ["trait"] = "green", ["power"] = 1 } };
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        bool result;
        if (consumer == "activate")
        {
            var requirement = PrivateRecord("ActionPromptBuilder+ActivateAbilityPromptRequirement", new() {
                ["PowerCostByTrait"] = resources, ["PaymentResourcePowerByChoice"] = byChoice });
            result = (bool)PromptBuilder.GetMethod("CanPayActivateAbilityRequirement", flags)!.Invoke(null, [state, "P1", requirement])!;
        }
        else if (consumer == "assemble")
        {
            var profile = PrivateRecord("AssembleEquipmentProfile", new() { ["PowerTrait"] = RuneTrait.Red, ["PowerCost"] = 2 });
            result = (bool)PromptBuilder.GetMethod("CanPayAssembleEquipmentCost", flags)!.Invoke(null,
                [state, "P1", pool, profile, new Dictionary<string, int> { [RuneTrait.Red] = 2 }, 0])!;
        }
        else
        {
            state = AddBase(state, "EZREAL", "SFD·149/221");
            Assert.True(CardBehaviorRegistry.TryGetByCardNo("SFD·006/221", out var behavior));
            behavior = behavior with { HasteReadyPowerCost = 2, HasteReadyPowerTrait = RuneTrait.Green,
                SourceUnitTags = behavior.SourceUnitTags + "|" + CardPermissionKeywordNames.Haste };
            var summary = Activator.CreateInstance(Reachability, BindingFlags.Instance | BindingFlags.NonPublic, null, [pool, resources], null)!;
            object?[] args = [state, "P1", behavior, "CARD", new[] { HasteOptionalCostNames.HasteReady }, pool, null, null, summary];
            result = (bool)typeof(CoreRuleEngine).GetMethod("CanOfferOptionalCostWithReductions", flags)!.Invoke(null, args)!;
        }
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RealAssembleAndActivatedSourcesExposeTheSameCapacityShapeAndSurviveNativeJson()
    {
        var state = AddBase(Position(1), "SONG", "SFD·059/221");
        state = state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Battlefields = ["OWN-FIELD", "RENATA"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["OWN-FIELD"] = OfficialGarenConquestBattlefieldCountTests.Printed("OWN-FIELD", "OGN·278/298", "P1") with { ControllerId = "P1" },
                ["RENATA"] = OfficialGarenConquestBattlefieldCountTests.Printed("RENATA", P4ActivatedAbilityCatalog.RenataGlascCardNo, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["OWN-FIELD"] = new("P1", "BATTLEFIELD", "OWN-FIELD"), ["RENATA"] = new("P1", "BATTLEFIELD", "OWN-FIELD") } };
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        foreach (var (action, source) in new[] { (CommandTypes.AssembleEquipment, "SONG"), (CommandTypes.ActivateAbility, "RENATA") })
        {
            var candidate = prompt.Candidates!.Single(c => c.Action == action);
            var requirements = JsonSerializer.SerializeToNode(candidate.Metadata, Web)!["sourceRequirements"]!.AsArray().Where(r => r!["sourceObjectId"]!.GetValue<string>() == source).ToArray();
            Assert.NotEmpty(requirements);
            foreach (var requirement in requirements)
            {
                Assert.Equal(1, requirement!["paymentResourcePowerCapacity"]!.GetValue<int>());
                Assert.False(requirement.AsObject().ContainsKey("availablePowerByTraitWithPaymentResources"));
                Assert.Equal(2, requirement["paymentResourceChoices"]!.AsArray().Count);
            }
        }
        Assert.Empty(Errors(state, JsonSerializer.Deserialize<ActionPromptDto>(JsonSerializer.Serialize(prompt, Web), Web)!));
    }

    internal static MatchState Position(int capacity, bool sameColor = false, string no = "OGN·268/298")
    {
        var state = AddBase(WithPool(ImposedMovementPaymentTests.Position(), new(20, 0,
            new Dictionary<string, int> { [RuneTrait.Blue] = int.MaxValue - capacity })), "GREEN", sameColor ? "OGN·007/298" : "OGN·042/298");
        return state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Append("CARD").ToArray() } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["CARD"] = OfficialGarenConquestBattlefieldCountTests.Printed("CARD", no, "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["CARD"] = new("P1", "HAND") } };
    }
    private static JsonObject Requirement(ActionPromptDto prompt) => Requirement(JsonSerializer.SerializeToNode(prompt, Web)!);
    private static JsonObject Requirement(JsonNode prompt) => prompt["candidates"]!.AsArray().Single(c => c!["action"]!.GetValue<string>() == CommandTypes.PlayCard)!
        ["metadata"]!["sourceRequirements"]!.AsArray().Single(r => r!["sourceObjectId"]!.GetValue<string>() == "CARD")!.AsObject();
    private static IReadOnlyList<string> Errors(MatchState state, ActionPromptDto? actorPrompt = null)
    {
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(id => id, id => new RecoveredPlayerView(id, state.Tick, 0, snapshots[id], state.Tick, 0, id == "P1" && actorPrompt is not null ? actorPrompt : prompts[id]));
        return MatchRecoveryValidator.Validate(state.RoomId, 0, [], [], views, state, state.Tick,
            MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, 0, [], state));
    }
    private static object PrivateRecord(string name, Dictionary<string, object?> values)
    {
        var type = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine." + name)!;
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).MaxBy(c => c.GetParameters().Length)!;
        return constructor.Invoke(constructor.GetParameters().Select(parameter => {
            if (values.TryGetValue(parameter.Name!, out var value)) return value;
            if (parameter.HasDefaultValue) return parameter.DefaultValue;
            var t = parameter.ParameterType;
            if (t == typeof(string)) return "";
            if (t.IsValueType) return Activator.CreateInstance(t);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) return Array.CreateInstance(t.GetGenericArguments()[0], 0);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)) return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(t.GetGenericArguments()));
            throw new InvalidOperationException("Unexpected cost boundary parameter " + parameter.Name);
        }).ToArray());
    }
}
