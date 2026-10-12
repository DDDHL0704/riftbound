using Riftbound.Contracts;
using Riftbound.Engine;
using System.Reflection;
using Xunit;
using static Riftbound.ConformanceTests.OfficialGraveyardRecastTests;

namespace Riftbound.ConformanceTests;

public sealed class MightyExperienceFrameTests
{
    internal static MatchState Hunter(string fiora = "SFD·180/221", int experience = 5)
    {
        var state = MightyTransitionTests.UnitSource(fiora, alreadyMighty: true);
        return state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["HOST"] = state.CardObjects["HOST"] with { CardNo = "UNL-094/219", Power = 2 } } };
    }

    internal static MatchState Yi(MatchState state, int experience = 6)
        => state with {
            PlayerExperience = new Dictionary<string, int>(state.PlayerExperience) { ["P1"] = experience },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["YI"] = new("YI", cardNo: "UNL-191/219", ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with { LegendZone = ["YI"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["YI"] = new("P1", "LEGEND") } };

    internal static MatchState Envoy(MatchState state)
    {
        state = HandUnit(state, "ENVOY", "UNL-092/219");
        return state with { RunePools = new Dictionary<string, RunePool>(state.RunePools) {
            ["P1"] = new(10, 3, powerByTrait: new Dictionary<string, int> { [RuneTrait.Yellow] = 3 }) } };
    }

    internal static MatchState HandUnit(MatchState state, string id, string cardNo)
    {
        var factory = typeof(CoreRuleEngine).Assembly.GetType("Riftbound.Engine.PrintedCardFactory")!;
        object?[] args = [id, "P1", cardNo, null];
        Assert.True((bool)factory.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args)!);
        var card = Assert.IsType<CardObjectState>(args[3]);
        Assert.Contains(CardObjectTags.UnitCard, card.Tags);
        return state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { [id] = card },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Hand = state.PlayerZones["P1"].Hand.Append(id).ToArray() } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { [id] = new("P1", "HAND") } };
    }

    internal static async Task<ResolutionResult> Gain(MatchState state)
        => await Top((await Act(Envoy(state), "P1", new PlayCardCommand("ENVOY", "UNL-092/219", []))).State);

    [Theory]
    [InlineData("SFD·180/221")]
    [InlineData("SFD·180a/221")]
    public async Task GainingTheSixthExperienceCrossesFourToFiveWithoutChangingStoredPower(string fiora)
    {
        var state = Hunter(fiora); Assert.Equal(4, ArmamentMightTests.Might(state));
        var done = await Gain(state);
        Assert.Equal(6, done.State.PlayerExperience["P1"]);
        Assert.Equal(5, ArmamentMightTests.Might(done.State));
        Assert.Equal(2, done.State.CardObjects["HOST"].Power);
        var skill = Assert.Single(done.State.StackItems).MightySkill!;
        Assert.NotNull(skill); Assert.Equal(4, skill.BeforeMight); Assert.Equal(5, skill.AfterMight);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData(4)] [InlineData(6)]
    public async Task ExperienceThatDoesNotCrossTheThresholdDoesNotQueueASkill(int experience)
    {
        var done = await Gain(Hunter(experience: experience));
        Assert.Equal(experience + 1, done.State.PlayerExperience["P1"]);
        Assert.Empty(done.State.StackItems); Assert.Empty(done.State.TriggerQueue); Assert.Null(done.State.PendingPayment);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Theory]
    [InlineData("SFD·205/221")] [InlineData("SFD·251/221")]
    public async Task TheLegendUsesTheSameExperienceThresholdHistory(string legend)
    {
        var state = Hunter();
        state = state with {
            CardObjects = state.CardObjects.Where(p => p.Key != "FIORA").ToDictionary(p => p.Key, p => p.Value),
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) { ["P1"] = state.PlayerZones["P1"] with {
                Base = state.PlayerZones["P1"].Base.Where(id => id != "FIORA").ToArray(), LegendZone = ["LEGEND"] } },
            ObjectLocations = state.ObjectLocations.Where(p => p.Key != "FIORA").ToDictionary(p => p.Key, p => p.Value) };
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["LEGEND"] = new("LEGEND", cardNo: legend, ownerId: "P1", controllerId: "P1", tags: ["CARD_TYPE:LEGEND"]) },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["LEGEND"] = new("P1", "LEGEND") } };
        var done = await Gain(state); var context = Assert.Single(done.State.StackItems).MightySkill!;
        Assert.NotNull(done.State.PendingCardChoice); Assert.Equal("LEGEND", context.Source.ObjectId);
        Assert.Equal(4, context.BeforeMight); Assert.Equal(5, context.AfterMight);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ThePendingPoppyAmbushGapDoesNotRewriteCapturedExperience()
    {
        var state = HandUnit(Hunter(), "POPPY", "UNL-178/219");
        state = LocalDestructionRecallTests.At(state, "HOST", "P1", "BATTLEFIELD", "F");
        var opened = await Gain(state); var pending = opened.State.PendingPayment!;
        var paid = await Act(opened.State, "P1", new PayCostCommand(pending.PaymentId, pending.PaymentWindow, ["SPEND_POWER:yellow:1"]));
        var response = await new CoreRuleEngine().ResolveAsync(paid.State, new("poppy-response", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("POPPY", "UNL-178/219", [], Destination: "BATTLEFIELD:F", Mode: "AMBUSH",
                OptionalCosts: ["SPEND_EXPERIENCE:3"]), default);
        // Retain this method identity as the regression for the former gap.
        Assert.True(response.Accepted, response.ErrorMessage);
        Assert.Equal(3, response.State.PlayerExperience["P1"]);
        Assert.Contains("POPPY", response.State.PlayerZones["P1"].Battlefields);
        Assert.Equal(4, ArmamentMightTests.Might(response.State));
        var remembered = response.State.StackItems.Single(i => i.MightySkill is not null).MightySkill!;
        Assert.Equal(4, remembered.BeforeMight); Assert.Equal(5, remembered.AfterMight);
        Restore(response.State); ArmamentMightTests.PublicRecovery(response.State);
        var done = await Top(response.State);
        Assert.False(done.State.CardObjects["HOST"].IsExhausted); Assert.Equal(4, ArmamentMightTests.Might(done.State));
        Assert.Empty(done.State.StackItems); Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task ARealExperiencePaymentDropsMightAndASeparateGainCrossesAgain()
    {
        var state = Envoy(HandUnit(Hunter(experience: 8), "POPPY", "UNL-178/219"));
        Assert.Equal(5, ArmamentMightTests.Might(state));
        var played = await Act(state, "P1", new PlayCardCommand("POPPY", "UNL-178/219", [], OptionalCosts: ["SPEND_EXPERIENCE:3"]));
        Assert.Equal(5, played.State.PlayerExperience["P1"]); Assert.Equal(4, ArmamentMightTests.Might(played.State));
        Assert.Empty(played.State.TriggerQueue); Restore(played.State); ArmamentMightTests.PublicRecovery(played.State);
        var poppy = played.State.StackItems.Count == 0 ? played : await Top(played.State);
        Assert.Contains("POPPY", poppy.State.PlayerZones["P1"].Base); Assert.Empty(poppy.State.StackItems);
        var gained = await Top((await Act(poppy.State, "P1", new PlayCardCommand("ENVOY", "UNL-092/219", []))).State);
        var context = Assert.Single(gained.State.StackItems).MightySkill!;
        Assert.Equal(4, context.BeforeMight); Assert.Equal(5, context.AfterMight);
        Restore(gained.State); ArmamentMightTests.PublicRecovery(gained.State);
    }

    [Theory]
    [InlineData("before")] [InlineData("after")] [InlineData("negative")] [InlineData("missing-seat")]
    [InlineData("extra-seat")] [InlineData("null")]
    public async Task RecoveryRejectsChangedOrMalformedHistoricalExperience(string mutation)
    {
        var opened = await Gain(Hunter()); var item = Assert.Single(opened.State.StackItems); var context = item.MightySkill!;
        var frame = mutation == "before" ? context.Before : context.After;
        var experience = new Dictionary<string, int>(frame.PlayerExperience);
        if (mutation == "missing-seat") experience.Remove("P2");
        else if (mutation == "extra-seat") experience["UNKNOWN"] = 0;
        else experience["P1"] = mutation == "negative" ? -1 : mutation == "before" ? 6 : 5;
        frame = frame with { PlayerExperience = mutation == "null" ? null! : experience };
        var forged = mutation == "before" ? context with { Before = frame } : context with { After = frame };
        Assert.Contains(OfficialInsightAndSpellLockTests.Errors(opened.State with { StackItems = [item with { MightySkill = forged }] }),
            error => error.Contains("Mighty transition", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConquestQualifiesWithTheExperienceDependentFriendlyAura()
    {
        var state = Yi(SunkenTempleMightTests.Position(power: 1));
        var done = await ConquestLifecycleRegressionTests.Conquer(state);
        var context = Assert.Single(done.State.StackItems).BattlefieldConquestMight;
        Assert.NotNull(context); Assert.Equal(5, Assert.Single(context!.PowerfulUnits).Might);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }

    [Fact]
    public async Task LastBreathRetainsTheExperienceDependentFriendlyAuraAtDeparture()
    {
        var state = Yi(ArmamentMightTests.Position(gear: "SFD·022/221", unit: "SFD·167/221", power: 2, attached: true));
        Assert.Equal(5, ArmamentMightTests.Might(state));
        var done = await PowerfulLastBreathTests.Destroy(state);
        var context = Assert.Single(done.State.StackItems).LastBreathMight;
        Assert.NotNull(context); Assert.Equal(5, context!.Might);
        Restore(done.State); ArmamentMightTests.PublicRecovery(done.State);
    }
}
