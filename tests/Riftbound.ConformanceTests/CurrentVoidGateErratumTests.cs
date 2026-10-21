using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class CurrentVoidGateErratumTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentOfficialTextKeepsSpellDamageBonusForUnitAndBattlefieldSelections(bool battlefieldSelection)
    {
        var spell = battlefieldSelection ? "OGS·002/024" : "UNL-007/219";
        var state = Position(spell);
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("current-void-spell", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", spell, battlefieldSelection ? ["HILL"] : ["UNIT"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; i < 2; i++) {
            result = await engine.ResolveAsync(result.State, new("current-void-pass-" + i, result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Equal(4, result.State.CardObjects["UNIT"].Damage);
        Assert.Equal(battlefieldSelection ? 4 : 0, result.State.CardObjects["SECOND"].Damage);
        Assert.Equal(0, result.State.CardObjects["OUTSIDE"].Damage);
        Assert.Equal(battlefieldSelection ? 2 : 1, result.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(result.State));
        var field = (await OfficialCardCatalog.LoadDefaultAsync()).Cards.Single(c => c.Id == 31542);
        Assert.Equal(1, Assert.Single(RuleTextParser.Parse(field).StaticAbilities,
            a => a.Kind == StaticAbilityKinds.BattlefieldTargetSpellSkillDamageBonus).Amount);
    }

    [Theory]
    [InlineData("NONE")]
    [InlineData("UNIT")]
    [InlineData("BOTH")]
    public async Task FirestormRequiresExactlyOneRealBattlefieldBeforeSpending(string selection)
    {
        var state = Position("OGS·002/024");
        string[] targets = selection switch { "NONE" => [], "UNIT" => ["UNIT"], _ => ["HILL", "OTHER"] };
        var result = await new CoreRuleEngine().ResolveAsync(state, new("invalid-field", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "OGS·002/024", targets), default);
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FirestormCanSelectAnotherOrEmptyBattlefieldWithoutLeakingDamage(bool empty)
    {
        var state = Position("OGS·002/024");
        if (empty) {
            var zones = state.PlayerZones.ToDictionary(p => p.Key, p => p.Value);
            zones["P2"] = zones["P2"] with { Battlefields = ["OTHER", "OUTSIDE"], Base = ["UNIT", "SECOND"] };
            var locations = state.ObjectLocations.ToDictionary(p => p.Key, p => p.Value);
            foreach (var id in new[] { "UNIT", "SECOND" }) locations[id] = new("P2", "BASE");
            var cards = state.CardObjects.ToDictionary(p => p.Key, p => p.Value);
            cards["HILL"] = cards["HILL"] with { ControllerId = null };
            state = state with { PlayerZones = zones, ObjectLocations = locations, CardObjects = cards };
        }
        var engine = new CoreRuleEngine();
        var result = await engine.ResolveAsync(state, new("other-field", "P1", CommandTypes.PlayCard),
            new PlayCardCommand("SPELL", "OGS·002/024", [empty ? "HILL" : "OTHER"]), default);
        Assert.True(result.Accepted, result.ErrorMessage);
        for (var i = 0; i < 2; i++) {
            result = await engine.ResolveAsync(result.State, new("other-field-pass-" + i, result.State.PriorityPlayerId!, CommandTypes.PassPriority), new PassPriorityCommand(), default);
            Assert.True(result.Accepted, result.ErrorMessage);
        }
        Assert.Equal(0, result.State.CardObjects["UNIT"].Damage);
        Assert.Equal(0, result.State.CardObjects["SECOND"].Damage);
        Assert.Equal(empty ? 0 : 3, result.State.CardObjects["OUTSIDE"].Damage);
        Assert.Equal(empty ? 0 : 1, result.Events.Count(e => e.Kind == "DAMAGE_APPLIED"));
        Assert.Empty(OfficialInsightAndSpellLockTests.Errors(result.State));
    }

    private static MatchState Position(string spell)
    {
        var state = BattlefieldLocalPermissionTests.Position("OGN·296/298", "P2", "HILL") with {
            ActivePlayerId = "P1", TurnPlayerId = "P1"
        };
        var cards = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["UNIT"] = state.CardObjects["UNIT"] with { Power = 8 },
            ["SECOND"] = new("SECOND", cardNo: "SFD·069/221", power: 8, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
            ["OUTSIDE"] = new("OUTSIDE", cardNo: "SFD·069/221", power: 8, ownerId: "P2", controllerId: "P2", tags: [CardObjectTags.UnitCard]),
            ["SPELL"] = new("SPELL", cardNo: spell, ownerId: "P1", controllerId: "P1", tags: [CardObjectTags.SpellCard])
        };
        var zones = new Dictionary<string, PlayerZones>(state.PlayerZones) {
            ["P1"] = state.PlayerZones["P1"] with { Hand = ["SPELL"] },
            ["P2"] = state.PlayerZones["P2"] with { Battlefields = ["OTHER", "UNIT", "SECOND", "OUTSIDE"] }
        };
        var locations = new Dictionary<string, ObjectLocationState>(state.ObjectLocations) {
            ["SPELL"] = new("P1", "HAND"),
            ["SECOND"] = new("P2", "BATTLEFIELD", "HILL"),
            ["OUTSIDE"] = new("P2", "BATTLEFIELD", "OTHER")
        };
        return PrintedCostFixture.Add(state with { CardObjects = cards, PlayerZones = zones, ObjectLocations = locations }, "P1", spell);
    }
}
