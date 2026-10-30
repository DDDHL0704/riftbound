using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// CN 204.4.b.1 and 430.3 apply to skill payments as well as playing cards.
public sealed class AbilityRunePaymentTests
{
    private const string First = "ABILITY-RUNE-1";
    private const string Second = "ABILITY-RUNE-2";

    [Fact]
    public async Task EmptyManaPoolOffersALegendSkillAndInlineTapRecoversAndReplays()
    {
        var initial = Runes(LegendAttachmentTests.PositionFor("VEN·149"));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["GEAR"] = initial.CardObjects["GEAR"] with { IsExhausted = true } } };
        var requirement = Requirement(initial, CommandTypes.LegendAct, LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId);
        Assert.Equal(0, requirement["availableMana"]);
        Assert.Equal(2, requirement["availableManaWithPaymentResources"]);
        Assert.Contains(Resources(requirement), choice => choice.Id == "TAP_RUNE:" + First);

        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.JayceLegendReadyOneAbilityId,
            ["GEAR"], ["SPEND_MANA:1", "TAP_RUNE:" + First]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects[First].IsExhausted);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Replay();
    }

    [Fact]
    public async Task AResourceLegendCanRecycleRunesToPayItsOwnPowerCost()
    {
        var card = LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.RenektonLegendAbilityId)[0];
        var initial = Runes(LegendAttachmentTests.PositionFor(card));
        var requirement = Requirement(initial, CommandTypes.LegendAct, LegendActionAbilityCatalog.RenektonLegendAbilityId);
        Assert.All(new[] { First, Second }, id => Assert.Contains(Resources(requirement), c => c.Id == "RECYCLE_RUNE:" + id));
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.RenektonLegendAbilityId,
            [], ["RECYCLE_RUNE:" + First, "RECYCLE_RUNE:" + Second]));
        Assert.Equal(2, game.State.RunePools["P1"].UnitPlayOrUnitAbilityOnlyMana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.All(new[] { First, Second }, id => Assert.Contains(id, game.State.PlayerZones["P1"].RuneDeck));
        await game.Replay();
    }

    [Fact]
    public async Task EmptyManaPoolOffersTheUnitSkillAndTapsOnlyTheSelectedRune()
    {
        var initial = Runes(ArmamentMightTests.Position(unit: "VEN·092", power: 4));
        var requirement = Requirement(initial, CommandTypes.ActivateAbility, "SELF-MIGHT:0");
        Assert.Contains(Resources(requirement), c => c.Id == "TAP_RUNE:" + First);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("HOST", "SELF-MIGHT:0", [], ["TAP_RUNE:" + First]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.True(game.State.CardObjects[First].IsExhausted);
        Assert.False(game.State.CardObjects[Second].IsExhausted);
        await game.Top();
        Assert.Equal(5, ArmamentMightTests.Might(game.State));
        await game.Replay();
    }

    [Fact]
    public async Task APrintedEmpowerSkillCanTapThenRecycleTheSameRunes()
    {
        var initial = Runes(LegendAttachmentTests.PositionFor("VEN·149"));
        var requirement = Requirement(initial, CommandTypes.ActivateAbility, "EMPOWER:0:0");
        Assert.All(new[] { First, Second }, id => {
            Assert.Contains(Resources(requirement), c => c.Id == "TAP_RUNE:" + id);
            Assert.Contains(Resources(requirement), c => c.Id == "RECYCLE_RUNE:" + id);
        });
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("LEGEND", "EMPOWER:0:0", [], [
            "TAP_RUNE:" + First, "RECYCLE_RUNE:" + First, "TAP_RUNE:" + Second, "RECYCLE_RUNE:" + Second]));
        Assert.Equal(0, game.State.RunePools["P1"].Mana);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(2, Assert.Single(game.State.StackItems).EmpowerActivation!.ControlledRunes);
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "RUNE_TAPPED"));
        Assert.Equal(2, game.Events().Count(e => e.Event.Kind == "RUNE_RECYCLED"));
        await game.Top();
        Assert.True(game.State.CardObjects["LEGEND"].IsEmpowered);
        await game.Replay();
    }

    [Fact]
    public async Task AUnitTargetLegendCanRecycleTheRuneForSpellshieldBeforeResponses()
    {
        var initial = EnemySpellshield(Runes(LegendAttachmentTests.PositionFor("VEN·151")));
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["LEGEND"] = initial.CardObjects["LEGEND"] with { IsEmpowered = true } } };
        var requirement = Requirement(initial, CommandTypes.LegendAct, LegendActionAbilityCatalog.MelLegendAbilityId);
        Assert.Contains(Resources(requirement), c => c.Id == "RECYCLE_RUNE:" + First);
        var game = new Driver(initial);
        await game.Act("P1", new LegendActCommand("LEGEND", LegendActionAbilityCatalog.MelLegendAbilityId,
            ["SHIELD"], ["RECYCLE_RUNE:" + First]));
        Assert.Equal(1, Assert.Single(game.State.StackItems).LegendUnitEffect!.SpellshieldTaxPower);
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Contains(First, game.State.PlayerZones["P1"].RuneDeck);
        await game.Top();
        await game.Replay();
    }

    [Fact]
    public async Task AnExperienceSkillCanRecycleTheRuneForItsEnemySpellshieldTarget()
    {
        var initial = EnemySpellshield(Runes(LegendAttachmentTests.PositionFor("VEN·149")));
        initial = initial with {
            CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                ["GEAR"] = initial.CardObjects["GEAR"] with { CardNo = P4ActivatedAbilityCatalog.CrimsonRoseCardNo } },
            PlayerExperience = new Dictionary<string, int>(initial.PlayerExperience) { ["P1"] = 3 } };
        var requirement = Requirement(initial, CommandTypes.ActivateAbility, P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId);
        var targets = Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(requirement["targetChoicesByIndex"]);
        Assert.Contains(targets["0"], c => c.Id == "SHIELD");
        Assert.Contains(Resources(requirement), c => c.Id == "RECYCLE_RUNE:" + First);
        var game = new Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.CrimsonRoseReadyAbilityId,
            ["SHIELD"], ["RECYCLE_RUNE:" + First]));
        Assert.Equal(0, game.State.RunePools["P1"].TotalPower);
        Assert.Equal(0, game.State.PlayerExperience["P1"]);
        Assert.True(game.State.CardObjects["SHIELD"].IsExhausted);
        Assert.True(game.State.CardObjects["GEAR"].IsExhausted);
        await game.Top();
        Assert.False(game.State.CardObjects["SHIELD"].IsExhausted);
        await game.Replay();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task LegendCandidatesAndExecutionShareRestrictedPowerStorageCapacity(int capacity)
    {
        var card = LegendActionAbilityCatalog.SourceCardNosForAbility(LegendActionAbilityCatalog.RenektonLegendAbilityId)[0];
        var initial = Runes(LegendAttachmentTests.PositionFor(card));
        initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(0, 0, spellOnlyPower: int.MaxValue - capacity) } };
        var prompt = ResolutionResult.BuildPrompts(initial)["P1"];
        Assert.Equal(capacity == 2, (prompt.Candidates ?? []).Any(c => c.Action == CommandTypes.LegendAct && c.Enabled));
        var command = new LegendActCommand("LEGEND", LegendActionAbilityCatalog.RenektonLegendAbilityId,
            [], ["RECYCLE_RUNE:" + First, "RECYCLE_RUNE:" + Second]);
        var before = MatchStateHasher.Hash(initial);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("capacity-rune-payment", "P1", command.CmdType), command, default);
        Assert.Equal(capacity == 2, result.Accepted);
        Assert.Equal(int.MaxValue - capacity, result.State.RunePools["P1"].SpellOnlyPower);
        Assert.Equal(before, MatchStateHasher.Hash(initial));
        if (!result.Accepted) {
            Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("enemy")]
    [InlineData("hidden")]
    public void AnUnusableRuneCannotMakeAnEmptyPoolSkillAffordable(string kind)
    {
        var initial = Runes(ArmamentMightTests.Position(unit: "VEN·092", power: 4));
        initial = initial with { CardObjects = initial.CardObjects.ToDictionary(p => p.Key, p =>
            p.Key is First or Second ? p.Value with {
                IsExhausted = kind == "exhausted", IsFaceDown = kind == "hidden", ControllerId = kind == "enemy" ? "P2" : "P1" } : p.Value) };
        Assert.DoesNotContain(ResolutionResult.BuildPrompts(initial)["P1"].Candidates ?? [], candidate =>
            candidate.Action == CommandTypes.ActivateAbility && candidate.Sources!.Any(c => c.Id == "HOST"));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("recycle-first")]
    [InlineData("enemy")]
    [InlineData("exhausted")]
    [InlineData("hidden")]
    [InlineData("target")]
    [InlineData("timing")]
    [InlineData("capacity")]
    public async Task RejectedSkillPaymentsPreserveTheOriginalStateAndEmitNoEvents(string kind)
    {
        var initial = Runes(ArmamentMightTests.Position(unit: "VEN·092", power: 4));
        var actions = kind switch {
            "duplicate" => new[] { "TAP_RUNE:" + First, "TAP_RUNE:" + First },
            "recycle-first" => new[] { "RECYCLE_RUNE:" + First, "TAP_RUNE:" + First },
            _ => new[] { "TAP_RUNE:" + First } };
        if (kind is "enemy" or "exhausted" or "hidden")
            initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
                [First] = initial.CardObjects[First] with { ControllerId = kind == "enemy" ? "P2" : "P1",
                    IsExhausted = kind == "exhausted", IsFaceDown = kind == "hidden" } } };
        if (kind == "timing") initial = initial with { TimingState = TimingStates.NeutralClosed };
        if (kind == "capacity") initial = initial with { RunePools = new Dictionary<string, RunePool>(initial.RunePools) {
            ["P1"] = new(int.MaxValue, 0) } };
        var before = MatchStateHasher.Hash(initial);
        var command = new ActivateAbilityCommand("HOST", "SELF-MIGHT:0", kind == "target" ? ["HOST"] : [], actions);
        var result = await new CoreRuleEngine().ResolveAsync(initial, new("rejected-rune-payment", "P1", command.CmdType), command, default);
        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(before, MatchStateHasher.Hash(initial));
    }

    private static IReadOnlyDictionary<string, object?> Requirement(MatchState state, string action, string ability)
    {
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == action);
        return Assert.Single(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]),
            row => Equals(row["abilityId"], ability));
    }

    private static IEnumerable<ActionPromptChoiceDto> Resources(IReadOnlyDictionary<string, object?> requirement)
        => Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(requirement["paymentResourceChoices"]);

    private static MatchState EnemySpellshield(MatchState state)
        => state with {
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P2"] = state.PlayerZones["P2"] with { Battlefields = [..state.PlayerZones["P2"].Battlefields, "SHIELD"] } },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SHIELD"] = new("SHIELD", cardNo: "OGN·013/298", ownerId: "P2", controllerId: "P2", power: 5,
                    isExhausted: true, tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
                ["SHIELD"] = new("P2", "BATTLEFIELD", state.ObjectLocations["HOST"].BattlefieldObjectId ?? "F") } };

    private static MatchState Runes(MatchState state)
    {
        var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
        var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
        foreach (var id in new[] { First, Second }) {
            cards[id] = new(id, cardNo: "OGN·228/298", ownerId: "P1", controllerId: "P1",
                tags: [CardObjectTags.RuneCard, "COLOR:purple"]);
            locations[id] = new("P1", "BASE");
        }
        return state with { CardObjects = cards, ObjectLocations = locations,
            RunePools = new Dictionary<string, RunePool>(state.RunePools) { ["P1"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, First, Second] } } };
    }
}
