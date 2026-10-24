using System.Reflection;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

// Component input for the shared power-filter interface. This does not register
// VEN159 or claim an ordinary card opening/accepted-history witness.
public sealed class CurrentKeywordAuraParticipantTests
{
    public CurrentKeywordAuraParticipantTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        var engine = typeof(CoreRuleEngine).Assembly;
        output.WriteLine($"Engine={engine.Location}; SHA256={Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(engine.Location))).ToLowerInvariant()}; MVID={engine.ManifestModule.ModuleVersionId}; RIF={RuleInputIdentity.Current.IdentitySha256}");
    }

    internal const string CurrentBulwark = "CURRENT_KEYWORD:壁垒";

    [Theory]
    [InlineData("intrinsic")]
    [InlineData("temporary")]
    [InlineData("continuous")]
    public void CurrentPossessionSelectsThePhysicalUnit(string mode)
    {
        var state = Position(mode);
        var before = MatchStateHasher.Hash(state);
        Assert.Equal(["P1-TARGET"], Members(state, Aura(CurrentBulwark)));
        Assert.Equal(before, MatchStateHasher.Hash(state));
    }

    [Theory]
    [InlineData("P1")]
    [InlineData("P2")]
    public void ContinuousGrantUsesItsControllerAndStopsWhenItsSourceLeaves(string controller)
    {
        var state = Position("continuous", controller);
        Assert.Equal([controller + "-TARGET"], Members(state, Aura(CurrentBulwark)));
        var zones = state.PlayerZones.ToDictionary(pair => pair.Key, pair => pair.Value);
        zones[controller] = zones[controller] with { Base = [], Hand = ["SOURCE"] };
        var departed = state with
        {
            PlayerZones = zones,
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
                { ["SOURCE"] = new(controller, "HAND") },
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
                { ["SOURCE"] = state.CardObjects["SOURCE"] with { ObjectGeneration = 2 } }
        };
        Assert.Empty(Members(departed, Aura(CurrentBulwark)));
    }

    [Fact]
    public void IndependentBulwarkGrantsDoNotDuplicateAnAuraParticipant()
    {
        var state = Position("continuous");
        var target = state.CardObjects["P1-TARGET"];
        state = state with
        {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects)
            {
                [target.ObjectId] = target with
                {
                    Tags = target.Tags.Append(CardCombatKeywordNames.Bulwark).ToArray(),
                    UntilEndOfTurnEffects = ["COMBAT_KEYWORD:壁垒:1", "COMBAT_KEYWORD:壁垒:2"]
                },
                ["SOURCE-2"] = Printed("SOURCE-2", "UNL-058/219", "P1")
            },
            PlayerZones = new Dictionary<string, PlayerZones>(state.PlayerZones)
                { ["P1"] = state.PlayerZones["P1"] with { Base = ["SOURCE", "SOURCE-2"] } },
            ObjectLocations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations)
                { ["SOURCE-2"] = new("P1", "BASE") }
        };
        Assert.Equal(["P1-TARGET"], Members(state, Aura(CurrentBulwark)));
    }

    [Theory]
    [InlineData("intrinsic", true)]
    [InlineData("temporary", false)]
    public void ExistingTagFilterRetainsItsLiteralMeaning(string mode, bool matches)
    {
        var members = Members(Position(mode), Aura("TAG:壁垒"));
        Assert.Equal(matches ? new[] { "P1-TARGET" } : [], members);
    }

    [Fact]
    public void CurrentKeywordSyntaxDoesNotActivateRuleTextGrantFilters()
    {
        var aura = Aura(CurrentBulwark) with { Layer = ContinuousEffectLayers.RuleText };
        Assert.Empty(Members(Position("intrinsic"), aura));
    }

    internal static StaticAuraSpec Aura(string filter) => new(
        StaticAuraKinds.BattlefieldFilteredUnitsPower, ContinuousEffectLayers.StaticAura,
        "WHILE_SOURCE_ON_PUBLIC_FIELD", StaticAuraTargetScopes.SameBattlefieldFilteredUnits,
        StaticAuraParticipantScopes.SameBattlefieldFilteredPublicUnits, 1,
        "Current-keyword participant mechanism fixture", "COMPONENT", "No catalog registration", TargetFilter: filter);

    internal static IReadOnlyList<string> Members(MatchState state, StaticAuraSpec aura)
    {
        var method = typeof(MatchState).GetMethod("BattlefieldFilteredStaticAuraParticipantObjectIds",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(method.Invoke(null, [state, "F", aura]));
    }

    internal static CardObjectState Printed(string id, string no, string owner)
        => OfficialGarenConquestBattlefieldCountTests.Printed(id, no, owner) with { ObjectGeneration = 1 };

    internal static MatchState Position(string mode, string sourceController = "P1")
    {
        Assert.Contains(mode, new[] { "intrinsic", "temporary", "continuous" });
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.FaerieTokenCardNo, out var token));
        var cards = new Dictionary<string, CardObjectState>
        {
            ["F"] = Printed("F", "OGN·278/298", "P1") with { ControllerId = "P2" },
            ["P1-TARGET"] = mode == "continuous"
                ? token.CreateObject("P1-TARGET", "P1", "P1") with { ObjectGeneration = 1 }
                : Printed("P1-TARGET", mode == "intrinsic" ? "OGN·054/298" : "OGN·210/298", "P1"),
            ["P2-TARGET"] = token.CreateObject("P2-TARGET", "P2", "P2") with { ObjectGeneration = 1 }
        };
        if (mode == "temporary") cards["P1-TARGET"] = cards["P1-TARGET"] with
            { UntilEndOfTurnEffects = ["COMBAT_KEYWORD:壁垒:1"] };
        if (mode == "continuous") cards["SOURCE"] = Printed("SOURCE", "UNL-058/219", sourceController);
        var locations = new Dictionary<string, ObjectLocationState>
        {
            ["F"] = new("P1", "BATTLEFIELD", "F"),
            ["P1-TARGET"] = new("P1", "BATTLEFIELD", "F"),
            ["P2-TARGET"] = new("P2", "BATTLEFIELD", "F")
        };
        if (mode == "continuous") locations["SOURCE"] = new(sourceController, "BASE");
        return new("N21-CURRENT-KEYWORD-COMPONENT", 0, 5, "P1",
            new Dictionary<string, string> { ["P1"] = "P1", ["P2"] = "P2" },
            status: MatchStatuses.InProgress, turnPlayerId: "P1", phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen, playerZones: new Dictionary<string, PlayerZones>
            {
                ["P1"] = PlayerZones.Empty with { Battlefields = ["F", "P1-TARGET"],
                    Base = mode == "continuous" && sourceController == "P1" ? ["SOURCE"] : [] },
                ["P2"] = PlayerZones.Empty with { Battlefields = ["P2-TARGET"],
                    Base = mode == "continuous" && sourceController == "P2" ? ["SOURCE"] : [] }
            }, cardObjects: cards, objectLocations: locations, seed: 424242);
    }
}
