using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class MeditationOptionalTargetTests
{
    [Theory]
    [InlineData("ally", true)]
    [InlineData("dual-unit-equipment", true)]
    [InlineData("enemy", false)]
    [InlineData("equipment", false)]
    [InlineData("rune", false)]
    [InlineData("battlefield", false)]
    [InlineData("hidden-unit", false)]
    [InlineData("exhausted-unit", false)]
    [InlineData("foreign-controller", false)]
    [InlineData("missing", false)]
    public async Task AdditionalCostRequiresAnActiveVisibleFriendlyUnit(string kind, bool allowed)
    {
        var state = Position();
        var target = kind switch {
            "enemy" => "ENEMY", "equipment" => "GEAR", "rune" => "RUNE", "battlefield" => "BF",
            "missing" => "MISSING", _ => "ALLY" };
        if (kind is "hidden-unit" or "exhausted-unit" or "foreign-controller" or "dual-unit-equipment")
        {
            var ally = state.CardObjects["ALLY"];
            ally = kind switch {
                "hidden-unit" => ally with { IsFaceDown = true },
                "exhausted-unit" => ally with { IsExhausted = true },
                "foreign-controller" => ally with { ControllerId = "P2" },
                _ => ally with { Tags = ally.Tags.Append(CardObjectTags.EquipmentCard).ToArray() } };
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) { ["ALLY"] = ally } };
        }
        var before = MatchStateHasher.Hash(state);
        var command = new PlayCardCommand("UNIT", "OGN·048/298", [], OptionalCosts: ["EXHAUST_FRIENDLY_UNIT:" + target]);
        var engine = new CoreRuleEngine();
        var quote = engine.PreviewPlayCard(state, "P1", PlayCostPreviewTests.Request(state, command));
        var result = await engine.ResolveAsync(state, new("meditation-target", "P1", CommandTypes.PlayCard), command, default);
        Assert.Equal(allowed, quote.CanPay);
        Assert.True(allowed == result.Accepted, $"kind={kind}, accepted={result.Accepted}, error={result.ErrorCode}: {result.ErrorMessage}");
        if (!allowed)
        {
            Assert.Empty(result.Events);
            Assert.Equal(before, MatchStateHasher.Hash(result.State));
        }
        else
        {
            Assert.True(result.State.CardObjects[target].IsExhausted);
            Assert.Equal(18, result.State.RunePools["P1"].Mana);
        }
    }

    [Fact]
    public async Task PromptOffersBothTheRealGrantAndOnlyLegalUnitCosts()
    {
        var initial = Position();
        var game = new StandbyDefendObserverTests.Driver(initial);
        await game.Act("P1", new ActivateAbilityCommand("GEAR", P4ActivatedAbilityCatalog.NextSpellEchoAbilityId, []));
        await game.Top();
        var state = game.State;
        var candidate = Assert.Single(ResolutionResult.BuildPrompts(state)["P1"].Candidates!, c => c.Action == CommandTypes.PlayCard);
        var requirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(candidate.Metadata!["sourceRequirements"]);
        var requirement = Assert.Single(requirements, r => Equals(r["sourceObjectId"], "UNIT"));
        var choices = Assert.IsAssignableFrom<IReadOnlyList<ActionPromptChoiceDto>>(requirement["optionalCostChoices"]);
        Assert.Contains(choices, c => c.Id == "ECHO");
        Assert.Contains(choices, c => c.Id == "EXHAUST_FRIENDLY_UNIT:ALLY");
        Assert.DoesNotContain(choices, c => c.Id is "EXHAUST_FRIENDLY_UNIT:GEAR" or "EXHAUST_FRIENDLY_UNIT:RUNE"
            or "EXHAUST_FRIENDLY_UNIT:BF" or "EXHAUST_FRIENDLY_UNIT:ENEMY");
    }

    private static MatchState Position()
    {
        var state = VenUnitEntryBasicTests.Position("OGN·048/298");
        return state with {
            RoomId = "MEDITATION-ADDITIONAL-TARGET",
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["GEAR"] = OfficialGarenConquestBattlefieldCountTests.Printed("GEAR", "SFD·078/221", "P1"),
                ["RUNE"] = OfficialGarenConquestBattlefieldCountTests.Printed("RUNE", "UNL-R06", "P1") },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) { ["RUNE"] = new("P1", "BASE") },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
                ["P1"] = state.PlayerZones["P1"] with { Base = [..state.PlayerZones["P1"].Base, "RUNE"] } }
        };
    }
}
