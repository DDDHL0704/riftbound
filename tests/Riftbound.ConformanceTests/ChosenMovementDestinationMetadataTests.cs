using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ChosenMovementDestinationMetadataTests
{
    [Fact]
    public void BaitPromptReportsDestinationsForEachMovementTarget()
    {
        var state = RadGearTestPosition.Put(
            RadGearTestPosition.Create(),
            "SPELL",
            "SFD·129/221",
            "P1",
            "HAND");
        state = RadGearTestPosition.Put(state, "ENEMY", "OGN·219/298", "P2", "BATTLEFIELD", "BF");
        state = RadGearTestPosition.Put(state, "ANCHOR", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "ENEMY-OTHER", "OGN·219/298", "P2", "BATTLEFIELD", "OTHER");
        state = RadGearTestPosition.Put(state, "ANCHOR-BASE", "OGN·219/298", "P2", "BASE");

        var prompt = ResolutionResult.BuildPrompts(state)["P1"];
        var play = Assert.Single(prompt.Candidates ?? [], candidate => candidate.Action == CommandTypes.PlayCard);
        Assert.Contains(play.Targets ?? [], target => target.Id == "ENEMY");
        Assert.Contains(play.Targets ?? [], target => target.Id == "ENEMY-OTHER");

        var destinations = Destinations(SourceRequirement(state));

        var firstTargetDestinations = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                destinations["ENEMY"])
            .Select(choice => choice.Id)
            .ToArray();
        var battlefieldTargetDestinations = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
                destinations["ENEMY-OTHER"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Contains("BATTLEFIELD:OTHER", firstTargetDestinations);
        Assert.Contains("BASE:P2", firstTargetDestinations);
        Assert.Equal(["BASE:P2", "BATTLEFIELD:BF"], battlefieldTargetDestinations);
        Assert.DoesNotContain("BATTLEFIELD:OTHER", battlefieldTargetDestinations);
    }

    [Fact]
    public void BaitDisablesPlayWhenNoOtherEnemyUnitCanSupplyADestination()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "SFD·129/221", "P1", "HAND");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");

        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
            candidate => candidate.Action == CommandTypes.PlayCard);
        Assert.False(play.Enabled);
        Assert.Empty(play.Sources ?? []);
        var metadata = Assert.IsType<Dictionary<string, object?>>(play.Metadata);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public void StunningDisplayRetainsBattlefieldDestinationsWithIndependentlyControlledGear(string gearController)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "OGN·270/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", gearController, "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with { AttachedToObjectId = "ALLY" } } };

        var destinations = Destinations(SourceRequirement(state));
        Assert.Equal(["BATTLEFIELD:BF", "BATTLEFIELD:OTHER"], destinations["ALLY"].Select(choice => choice.Id));
        foreach (var destination in destinations["ALLY"])
        {
            var quote = new CoreRuleEngine().PreviewPlayCard(state, "P1", new("display-quote",
                ResolutionResult.BuildPrompts(state)["P1"].PromptId!, state.Tick,
                new PlayCardCommand("SPELL", "OGN·270/298", ["ALLY"], Destination: destination.Id)));
            Assert.True(quote.IsValid, quote.Message);
            Assert.True(quote.CanPay, quote.Message);
        }
    }

    [Fact]
    public void DemonShadeReportsOnlyOpenUnoccupiedBattlefields()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "RAD·102/167", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "OPEN", "OGN·296/298", "P1", "BATTLEFIELD", "OPEN");
        state = RadUnitEffectTestFixture.Place(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OPEN"] = state.CardObjects["OPEN"] with { ControllerId = null },
            ["BF"] = state.CardObjects["BF"] with { ControllerId = null } } };

        var destinations = Destinations(SourceRequirement(state, "MOVE_TO_OPEN_BATTLEFIELD"));

        Assert.Equal(["BATTLEFIELD:OPEN"], destinations["ALLY"].Select(choice => choice.Id));
    }

    [Fact]
    public void PrecisionBombDescribesOnlyTheMovingUnitAndExcludesTheBattlefieldTarget()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "RAD·096/167", "P1", "HAND");

        var destinations = Destinations(SourceRequirement(state));

        Assert.Equal(["ALLY"], destinations.Keys);
        Assert.Equal(["BATTLEFIELD:BF", "BATTLEFIELD:OTHER"], destinations["ALLY"].Select(choice => choice.Id));
    }

    [Fact]
    public void MalformedAttachedEquipmentRemovesOnlyItsHostFromMovementChoices()
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "SPELL", "OGN·270/298", "P1", "HAND");
        state = RadGearTestPosition.Put(state, "GOOD", "OGN·219/298", "P1", "BASE");
        state = RadGearTestPosition.Put(state, "GEAR", "SFD·022/221", "P2", "BASE");
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["GEAR"] = state.CardObjects["GEAR"] with {
                AttachedToObjectId = "ALLY", ControllerId = null } } };

        var requirement = SourceRequirement(state);
        var destinations = Destinations(requirement);
        var targetChoices = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(requirement["targetChoicesByIndex"]);
        var targets = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(targetChoices["0"]);

        Assert.Equal(["GOOD"], targets.Select(choice => choice.Id));
        Assert.Equal(["GOOD"], destinations.Keys);
    }

    private static IReadOnlyDictionary<string, object?> SourceRequirement(MatchState state, string? mode = null)
    {
        var play = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates ?? [],
            candidate => candidate.Action == CommandTypes.PlayCard);
        var metadata = Assert.IsType<Dictionary<string, object?>>(play.Metadata);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(metadata["sourceRequirements"]);
        return Assert.Single(requirements, requirement => Equals(requirement["sourceObjectId"], "SPELL")
            && Equals(requirement["mode"], mode));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>> Destinations(
        IReadOnlyDictionary<string, object?> requirement)
        => Assert.IsAssignableFrom<IReadOnlyDictionary<string, IReadOnlyList<ActionPromptChoiceDto>>>(
            requirement["destinationChoicesByTargetObjectId"]);
}
