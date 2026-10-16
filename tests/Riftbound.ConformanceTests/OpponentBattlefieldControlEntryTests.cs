using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class OpponentBattlefieldControlEntryTests
{
    private const string Source = "CASTER-VAYNE";
    private const string CasterField = "CASTER-OWNED-FIELD";
    private const string OtherField = "OPPONENT-OWNED-FIELD";

    [Theory]
    [InlineData("OGN·035/298", "P2", null, true)]
    [InlineData("SFD·223/221", "P2", null, true)]
    [InlineData("SFD·223*/221", "P2", null, true)]
    [InlineData("OGN·035/298", "P2", "P1", true)]
    [InlineData("SFD·223/221", "P2", "P1", true)]
    [InlineData("SFD·223*/221", "P2", "P1", true)]
    [InlineData("OGN·035/298", null, "P2", true)]
    [InlineData("SFD·223/221", null, "P2", true)]
    [InlineData("SFD·223*/221", null, "P2", true)]
    [InlineData("OGN·035/298", null, null, false)]
    [InlineData("SFD·223/221", null, null, false)]
    [InlineData("SFD·223*/221", null, null, false)]
    [InlineData("OGN·035/298", "P1", "P1", false)]
    [InlineData("SFD·223/221", "P1", "P1", false)]
    [InlineData("SFD·223*/221", "P1", "P1", false)]
    [InlineData("OGN·035/298", "P1", null, false)]
    [InlineData("SFD·223/221", "P1", null, false)]
    [InlineData("SFD·223*/221", "P1", null, false)]
    public async Task EntryUsesCurrentBattlefieldControllerAcrossBothOwnersAndUncontrolledFields(
        string cardNo, string? casterFieldController, string? otherFieldController, bool expectedReady)
    {
        var state = BuildState(cardNo, casterFieldController, otherFieldController);
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("actual-battlefield-controller", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(Source, cardNo, []), default);

        AssertEntry(result, expectedReady);
        Assert.Equal("P1", result.State.CardObjects[CasterField].OwnerId);
        Assert.Equal("P2", result.State.CardObjects[OtherField].OwnerId);
        Assert.Contains(CasterField, result.State.PlayerZones["P1"].Battlefields);
        Assert.Contains(OtherField, result.State.PlayerZones["P2"].Battlefields);
    }

    [Theory]
    [InlineData("OGN·035/298", "unknown-controller")]
    [InlineData("SFD·223/221", "unknown-controller")]
    [InlineData("SFD·223*/221", "unknown-controller")]
    [InlineData("OGN·035/298", "face-down")]
    [InlineData("SFD·223/221", "face-down")]
    [InlineData("SFD·223*/221", "face-down")]
    [InlineData("OGN·035/298", "unit-at-battlefield")]
    [InlineData("SFD·223/221", "unit-at-battlefield")]
    [InlineData("SFD·223*/221", "unit-at-battlefield")]
    public async Task OnlyAnActualOpponentControlledPublicBattlefieldEnablesReadyEntry(string cardNo, string excluded)
    {
        var state = BuildState(cardNo, null, excluded == "unknown-controller" ? "GHOST" : "P2");
        var fields = state.CardObjects.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (excluded == "face-down") fields[OtherField] = fields[OtherField] with { IsFaceDown = true };
        if (excluded == "unit-at-battlefield") fields[OtherField] = new(OtherField, isExhausted: false,
            tags: [CardObjectTags.UnitCard], cardNo: "OGN·096/298", ownerId: "P2", controllerId: "P2");
        state = state with { CardObjects = fields };
        var result = await new CoreRuleEngine().ResolveAsync(state,
            new PlayerIntent("exclude-non-battlefield-control", "P1", CommandTypes.PlayCard),
            new PlayCardCommand(Source, cardNo, []), default);
        AssertEntry(result, false);
    }

    private static void AssertEntry(ResolutionResult result, bool ready)
    {
        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Contains(Source, result.State.PlayerZones["P1"].Base);
        Assert.Equal(!ready, result.State.CardObjects[Source].IsExhausted);
        var entered = Assert.Single(result.Events, ev => ev.Kind == "UNIT_PLAYED_TO_BASE");
        Assert.Equal(!ready, entered.Payload["isExhausted"]);
        Assert.Equal(ready, entered.Payload.ContainsKey("entryStaticAbilityKind"));
        if (ready)
        {
            Assert.Equal(StaticAbilityKinds.SourceUnitEnterReady, entered.Payload["entryStaticAbilityKind"]);
            Assert.Equal(Source, entered.Payload["entryStaticAbilitySourceObjectId"]);
        }
    }

    private static MatchState BuildState(string cardNo, string? casterController, string? otherController)
    {
        return new MatchState("opponent-battlefield-control-entry", 0, 1, "P1",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["P1"] = "s1", ["P2"] = "s2" }) with
        {
            TurnPlayerId = "P1", Phase = MatchPhases.Main, TimingState = TimingStates.NeutralOpen,
            RunePools = new Dictionary<string, RunePool>(StringComparer.Ordinal) { ["P1"] = new(4, 1), ["P2"] = RunePool.Empty },
            PlayerZones = new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with { Hand = [Source], Battlefields = [CasterField] },
                ["P2"] = PlayerZones.Empty with { Battlefields = [OtherField] }
            },
            CardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [Source] = new(Source, isExhausted: true, cardNo: cardNo, ownerId: "P1", controllerId: "P1"),
                [CasterField] = new(CasterField, isExhausted: false, tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo, ownerId: "P1", controllerId: casterController),
                [OtherField] = new(OtherField, isExhausted: false, tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                    cardNo: P6TokenFactoryCatalog.BrushBattlefieldTokenCardNo, ownerId: "P2", controllerId: otherController)
            },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [Source] = new("P1", "HAND"), [CasterField] = new("P1", "BATTLEFIELD"), [OtherField] = new("P2", "BATTLEFIELD")
            }
        };
    }
}
