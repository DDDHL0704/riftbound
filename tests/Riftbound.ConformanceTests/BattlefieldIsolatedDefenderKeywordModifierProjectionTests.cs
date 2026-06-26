using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BattlefieldIsolatedDefenderKeywordModifierProjectionTests
{
    private const string BattlefieldObjectId = "P2-FORBIDDEN-WASTELAND";
    private const string AttackerObjectId = "P1-FORBIDDEN-WASTELAND-ATTACKER";
    private const string DefenderObjectId = "P2-FORBIDDEN-WASTELAND-DEFENDER";
    private const string SecondDefenderObjectId = "P2-FORBIDDEN-WASTELAND-SECOND-DEFENDER";

    [Fact]
    public void ForbiddenWastelandBattlefieldIsolatedDefenderProjectsRuleTextModifier()
    {
        var state = BuildBattleState();

        var ruleText = Assert.Single(
            state.ContinuousEffects,
            effect => string.Equals(
                effect.EffectId,
                $"RULE_TEXT:BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER:{BattlefieldObjectId}:{DefenderObjectId}:坚守",
                StringComparison.Ordinal));

        Assert.Equal("OBJECT", ruleText.Scope);
        Assert.Equal(ContinuousEffectLayers.RuleText, ruleText.Layer);
        Assert.Equal("WHILE_SOURCE_BATTLEFIELD_AND_PARTICIPANT_AT_BATTLEFIELD", ruleText.Duration);
        Assert.Equal(DefenderObjectId, ruleText.TargetObjectId);
        Assert.Equal(BattlefieldObjectId, ruleText.SourceObjectId);
    }

    [Fact]
    public void ForbiddenWastelandBattlefieldIsolatedDefenderDoesNotProjectWhenDefendersAreNotIsolated()
    {
        var state = BuildBattleState(includeSecondDefender: true);

        Assert.DoesNotContain(
            state.ContinuousEffects,
            effect => string.Equals(
                effect.EffectId,
                $"RULE_TEXT:BATTLEFIELD_ISOLATED_DEFENDER_KEYWORD_MODIFIER:{BattlefieldObjectId}:{DefenderObjectId}:坚守",
                StringComparison.Ordinal));
    }

    private static MatchState BuildBattleState(bool includeSecondDefender = false)
    {
        var cardObjects = new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
        {
            [BattlefieldObjectId] = new(
                BattlefieldObjectId,
                cardNo: "UNL-210/219",
                tags: [P6TokenFactoryCatalog.BattlefieldCardTag],
                ownerId: "P2",
                controllerId: "P2"),
            [AttackerObjectId] = Unit(AttackerObjectId, "P1", power: 7, isAttacking: true),
            [DefenderObjectId] = Unit(DefenderObjectId, "P2", power: 4, "UNL-090/219", isDefending: true)
        };

        if (includeSecondDefender)
        {
            cardObjects[SecondDefenderObjectId] = Unit(SecondDefenderObjectId, "P2", power: 2, isDefending: true);
        }

        var objectLocations = cardObjects.ToDictionary(
            entry => entry.Key,
            entry => new ObjectLocationState(
                string.Equals(entry.Value.ControllerId, "P2", StringComparison.Ordinal) ? "P2" : "P1",
                "BATTLEFIELD",
                BattlefieldObjectId),
            StringComparer.Ordinal);

        return new MatchState(
            "battlefield-isolated-defender-keyword-modifier-projection-room",
            tick: 1,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "P1",
                ["P2"] = "P2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [AttackerObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = cardObjects.Keys
                        .Where(objectId => !string.Equals(objectId, AttackerObjectId, StringComparison.Ordinal))
                        .OrderBy(objectId => objectId, StringComparer.Ordinal)
                        .ToArray()
                }
            },
            cardObjects: cardObjects,
            objectLocations: objectLocations);
    }

    private static CardObjectState Unit(
        string objectId,
        string playerId,
        int power,
        string cardNo = "SFD·125/221",
        bool isAttacking = false,
        bool isDefending = false)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            isAttacking: isAttacking,
            isDefending: isDefending,
            isExhausted: false,
            tags: [CardObjectTags.UnitCard],
            ownerId: playerId,
            controllerId: playerId);
    }
}
