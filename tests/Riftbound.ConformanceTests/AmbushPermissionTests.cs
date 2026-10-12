using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class AmbushPermissionTests
{
    internal static MatchState LegacyOccupiedField(MatchState state)
    {
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["P1-MAIN"] = new("P1-MAIN", cardNo: "OGN·278/298", ownerId: "P1", controllerId: "P1", tags: [P6TokenFactoryCatalog.BattlefieldCardTag]),
            ["P1-BATTLEFIELD-FRIENDLY-001"] = state.CardObjects["P1-BATTLEFIELD-FRIENDLY-001"] with {
                CardNo = "SFD·125/221", OwnerId = "P1", ControllerId = "P1" } };
        if (cards.TryGetValue("P1-HAND-UNL-GLOOMY-APOTHECARY", out var source))
            cards[source.ObjectId] = source with { CardNo = "UNL-021/219", ManaCost = 3, OwnerId = "P1", ControllerId = "P1" };
        return state with { CardObjects = cards,
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Battlefields = state.PlayerZones["P1"].Battlefields.Append("P1-MAIN").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["P1-MAIN"] = new("P1", "BATTLEFIELD", "P1-MAIN"),
                ["P1-BATTLEFIELD-FRIENDLY-001"] = new("P1", "BATTLEFIELD", "P1-MAIN") } };
    }

    internal static MatchState Position(string card = "UNL-178/219", bool friendly = true)
    {
        var state = MightyExperienceFrameTests.Hunter(experience: 6);
        state = MightyExperienceFrameTests.HandUnit(state, "AMBUSH", card);
        if (friendly) state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        else {
            state = OfficialHoldSequenceTests.AddUnit(state, "FOE", "SFD·125/221", "F", "P2");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["F"] = state.CardObjects["F"] with { ControllerId = "P2" } } };
        }
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(20, 10, powerByTrait: new Dictionary<string, int> { [RuneTrait.Yellow] = 5, [RuneTrait.Orange] = 5 }) } };
    }

    internal static async Task<MatchState> Response(MatchState state)
    {
        state = OfficialCopyIdentityTests.AddSpell(state, "SPELL", "OGN·048/298");
        return (await Act(state, "P1", new PlayCardCommand("SPELL", "OGN·048/298", []))).State;
    }

    internal static PlayCardCommand Poppy(string card = "UNL-178/219", string mode = "")
        => new("AMBUSH", card, [], Mode: mode, OptionalCosts: ["SPEND_EXPERIENCE:3"], Destination: "BATTLEFIELD:F");

    [Theory]
    [InlineData("UNL-178/219", false, "")] [InlineData("UNL-178a/219", false, "")]
    [InlineData("UNL-178/219", true, "")] [InlineData("UNL-178a/219", true, "")]
    [InlineData("UNL-178/219", true, "AMBUSH")] [InlineData("UNL-178a/219", true, "AMBUSH")]
    public async Task OccupiedRealBattlefieldUsesCommonPrintedAndExperienceCosts(string card, bool response, string mode)
    {
        var state = Position(card); if (response) state = await Response(state);
        var command = Poppy(card, mode);
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", new("quote", "prompt", state.Tick, command));
        Assert.True(quote.IsValid, quote.Message); Assert.True(quote.CanPay, quote.Message);
        Assert.Equal(3, quote.Cost!.Mana); Assert.Equal(3, quote.Cost.Experience);
        Assert.Equal(1, quote.Cost.PowerByTrait[RuneTrait.Yellow]);
        var done = await Act(state, "P1", command);
        Assert.Equal(3, done.State.PlayerExperience["P1"]);
        Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", done.State.ObjectLocations["AMBUSH"].BattlefieldObjectId);
        Assert.DoesNotContain(done.State.StackItems, item => item.SourceObjectId == "AMBUSH");
        Assert.Contains(done.Events, e => e.Kind == "COST_PAID" && Equals(e.Payload["experienceCost"], 3));
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("UNL-120/219")] [InlineData("UNL-120a/219")]
    public async Task EnemyOccupiedBattlefieldExtendsTheCommonAmbushPermission(string card)
    {
        var state = await Response(Position(card, friendly: false));
        var done = await Act(state, "P1", new PlayCardCommand("AMBUSH", card, [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", done.State.ObjectLocations["AMBUSH"].BattlefieldObjectId);
        Assert.Contains("FOE", done.State.PlayerZones["P2"].Battlefields);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    public static IEnumerable<object[]> RegisteredPrintedAmbushUnits => new[] {
        "UNL-002/219", "UNL-012/219", "UNL-021/219", "UNL-060/219", "UNL-060a/219", "UNL-071/219",
        "UNL-114/219", "UNL-120/219", "UNL-120a/219", "UNL-143/219", "UNL-143a/219", "UNL-149/219",
        "UNL-149a/219", "UNL-176/219", "UNL-176a/219", "UNL-178/219", "UNL-178a/219"
    }.Select(card => new object[] { card });

    [Theory]
    [MemberData(nameof(RegisteredPrintedAmbushUnits))]
    public async Task EachRegisteredPrintedUnitCanEnterTheSelectedOccupiedFieldDuringResponses(string card)
    {
        var state = await Response(Position(card));
        var done = await Act(state, "P1", new PlayCardCommand("AMBUSH", card, [], Destination: "BATTLEFIELD:F"));
        Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields);
        Assert.Equal("F", done.State.ObjectLocations["AMBUSH"].BattlefieldObjectId);
        Assert.Equal(1, done.State.CardObjects["AMBUSH"].ObjectGeneration);
        Assert.DoesNotContain(done.State.StackItems, item => item.SourceObjectId == "AMBUSH" && !item.SourceConfirmed);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("empty")] [InlineData("hidden")] [InlineData("base")] [InlineData("wrong-priority")]
    [InlineData("unknown-field")] [InlineData("non-unit")]
    public async Task InvalidReactionDestinationsOrActorsAreRejectedWithoutPayment(string invalid)
    {
        var state = await Response(Position()); var command = Poppy();
        if (invalid == "empty") state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BASE");
        if (invalid == "hidden") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { IsFaceDown = true, Tags = [CardObjectTags.UnitCard, CardObjectTags.Standby] } } };
        if (invalid == "base") command = command with { Destination = "BASE" };
        if (invalid == "wrong-priority") state = state with { PriorityPlayerId = "P2", ActivePlayerId = "P2" };
        if (invalid == "unknown-field") command = command with { Destination = "BATTLEFIELD:UNKNOWN" };
        if (invalid == "non-unit") state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["HOST"] = state.CardObjects["HOST"] with { Tags = [CardObjectTags.EquipmentCard] } } };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("reject", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    [Fact]
    public async Task ThePromptOffersOnlyLocationsWithCurrentReactionPermission()
    {
        var state = await Response(Position());
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var candidate = Assert.Single(prompt.Candidates!, c => c.Action == CommandTypes.PlayCard);
        var data = System.Text.Json.JsonSerializer.SerializeToElement(candidate, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var source = data.GetProperty("metadata").GetProperty("sourceRequirements").EnumerateArray()
            .Single(r => r.GetProperty("sourceObjectId").GetString() == "AMBUSH");
        var choices = source.GetProperty("destinationChoices").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToArray();
        Assert.Equal(new[] { "BATTLEFIELD:F" }, choices); Assert.True(string.IsNullOrEmpty(source.GetProperty("mode").GetString()));
    }

    [Theory]
    [InlineData("mana")] [InlineData("yellow")] [InlineData("experience")]
    public async Task MissingSharedResourcesNeverCommitOrEnterTheField(string missing)
    {
        var state = await Response(Position());
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(missing == "mana" ? 2 : 10, missing == "yellow" ? 0 : 1,
                powerByTrait: missing == "yellow" ? new Dictionary<string, int>() : new Dictionary<string, int> { [RuneTrait.Yellow] = 1 }) },
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = missing == "experience" ? 2 : 6 } };
        var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", new("quote", "prompt", state.Tick, Poppy()));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(state, new("reject-cost", "P1", CommandTypes.PlayCard), Poppy(), default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }

    [Fact]
    public async Task ANewAttackerAppliesPrintedAssaultAndRetainsItsEnteredGeneration()
    {
        var state = MightyExperienceFrameTests.HandUnit(AttackDamageDistributionTests.Position("OGN·001/298"), "AMBUSH", "UNL-002/219");
        state = OfficialHoldSequenceTests.AddUnit(state, "FIORA", "SFD·180/221", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "FIORA", "P1", "BASE");
        state = state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var battle = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var entered = await Act(battle.State, "P1", new PlayCardCommand("AMBUSH", "UNL-002/219", [], Destination: "BATTLEFIELD:F"));
        Assert.True(entered.State.CardObjects["AMBUSH"].IsAttacking); Assert.False(entered.State.CardObjects["AMBUSH"].IsDefending);
        var publicUnit = CurrentPowerTargetTests.Snapshot(entered.State).GetProperty("players").GetProperty("P1").GetProperty("objects").GetProperty("AMBUSH");
        Assert.Equal(1, entered.State.CardObjects["AMBUSH"].Power);
        Assert.Equal(3, publicUnit.GetProperty("effectivePower").GetInt32());
        Assert.Equal(1, entered.State.CardObjects["AMBUSH"].ObjectGeneration);
        Assert.Empty(entered.State.StackItems);
        Restore(entered.State); ArmamentMightTests.PublicRecovery(entered.State);
    }

    [Fact]
    public async Task APrintedFourUnitEnteringTheBarracksUsesItsCurrentMightAndEnteredGeneration()
    {
        var state = MightyExperienceFrameTests.HandUnit(AttackDamageDistributionTests.Position("OGN·001/298"), "AMBUSH", "UNL-143/219");
        state = OfficialHoldSequenceTests.AddUnit(state, "FIORA", "SFD·180/221", "F", "P1");
        state = LocalDestructionRecallTests.At(state, "FIORA", "P1", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["F"] = state.CardObjects["F"] with { CardNo = "OGN·294/298" } },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = new(20, 20) } };
        var battle = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var entered = await Act(battle.State, "P1", new PlayCardCommand("AMBUSH", "UNL-143/219", [], Destination: "BATTLEFIELD:F"));
        Assert.True(entered.State.CardObjects["AMBUSH"].IsAttacking);
        var context = Assert.Single(entered.State.StackItems).MightySkill!;
        Assert.NotNull(context); Assert.Equal("AMBUSH", context.Unit.ObjectId);
        Assert.Equal(entered.State.CardObjects["AMBUSH"].ObjectGeneration, context.Unit.Generation);
        Assert.Equal(4, context.BeforeMight); Assert.Equal(5, context.AfterMight);
        Restore(entered.State); ArmamentMightTests.PublicRecovery(entered.State);
    }

    [Fact]
    public async Task ANewDefenderGetsItsRoleFromTheExistingBattle()
    {
        var state = MightyExperienceFrameTests.HandUnit(AttackDamageDistributionTests.Position("OGN·001/298"), "AMBUSH", "UNL-178/219");
        state = state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["AMBUSH"] = state.CardObjects["AMBUSH"] with { OwnerId = "P2", ControllerId = "P2" } },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "AMBUSH").ToArray() },
                ["P2"] = state.PlayerZones["P2"] with { Hand = state.PlayerZones["P2"].Hand.Append("AMBUSH").ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["AMBUSH"] = new("P2", "HAND") },
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P2"] = new(20, 20) } };
        var battle = await Act(state, "P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:F", []));
        var focus = await Act(battle.State, "P1", new PassFocusCommand());
        var entered = await Act(focus.State, "P2", new PlayCardCommand("AMBUSH", "UNL-178/219", [], Destination: "BATTLEFIELD:F"));
        Assert.True(entered.State.CardObjects["AMBUSH"].IsDefending); Assert.False(entered.State.CardObjects["AMBUSH"].IsAttacking);
        Assert.Equal(1, entered.State.CardObjects["AMBUSH"].ObjectGeneration);
        Restore(entered.State); ArmamentMightTests.PublicRecovery(entered.State);
    }

    [Fact]
    public async Task ARequiredTraitSacrificeUsesTheSharedCostFlowWhileAnotherUnitPreservesAmbush()
    {
        var state = MightyExperienceFrameTests.HandUnit(Position("UNL-166/219"), "DOG", "UNL-075/219");
        state = LocalDestructionRecallTests.At(state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "DOG").ToArray() } } }, "DOG", "P1", "BASE");
        state = await Response(state);
        var done = await Act(state, "P1", new PlayCardCommand("AMBUSH", "UNL-166/219", [],
            OptionalCosts: ["DESTROY_FRIENDLY_TRAIT_UNIT:DOG"], Destination: "BATTLEFIELD:F"));
        Assert.Contains("DOG", done.State.PlayerZones["P1"].Graveyard); Assert.Contains("AMBUSH", done.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(done.Events, e => e.Kind == "COST_PAID");
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task PayingTheLastFriendlyBattlefieldUnitCannotRetainAmbushReactionPermission()
    {
        var state = MightyExperienceFrameTests.HandUnit(Position("UNL-166/219", friendly: false), "DOG", "UNL-075/219");
        state = LocalDestructionRecallTests.At(state with { PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = state.PlayerZones["P1"].Hand.Where(id => id != "DOG").ToArray() } } }, "DOG", "P1", "BASE");
        var battle = await Act(state, "P1", new MoveUnitCommand("DOG", "BASE", "BATTLEFIELD:F", []));
        var command = new PlayCardCommand("AMBUSH", "UNL-166/219", [], OptionalCosts: ["DESTROY_FRIENDLY_TRAIT_UNIT:DOG"], Destination: "BATTLEFIELD:F");
        var quote = new CoreRuleEngine().PreviewPlayCard(battle.State, "P1", new("quote", "prompt", battle.State.Tick, command));
        Assert.False(quote.CanPay);
        var result = await new CoreRuleEngine().ResolveAsync(battle.State, new("last-unit", "P1", CommandTypes.PlayCard), command, default);
        Assert.False(result.Accepted); Assert.Equal(MatchStateHasher.Hash(battle.State), MatchStateHasher.Hash(result.State)); Assert.Empty(result.Events);
    }
}
