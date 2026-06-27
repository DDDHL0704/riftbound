using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class ViDoublePowerAbilityTests
{
    private const string ViObjectId = "P1-VI";

    [Fact]
    public void ViDoublePowerSourceGroupIncludesAltArt()
    {
        Assert.True(P4ActivatedAbilityCatalog.TryGetByAbilityId(
            P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
            out var ability));

        Assert.Contains(P4ActivatedAbilityCatalog.ViCardNo, P4ActivatedAbilityCatalog.SourceCardNosForAbility(ability));
        Assert.Contains(P4ActivatedAbilityCatalog.ViAltACardNo, P4ActivatedAbilityCatalog.SourceCardNosForAbility(ability));
    }

    [Fact]
    public async Task ViAltDoublePowerAbilityAddsStackItemAndResolves()
    {
        var activated = await new CoreRuleEngine().ResolveAsync(
            BuildViState(P4ActivatedAbilityCatalog.ViAltACardNo),
            new PlayerIntent("intent-vi-alt-activate", "P1", CommandTypes.ActivateAbility),
            new ActivateAbilityCommand(
                ViObjectId,
                P4ActivatedAbilityCatalog.ViDoublePowerAbilityId,
                []),
            CancellationToken.None);

        Assert.True(activated.Accepted, activated.ErrorMessage);
        Assert.Equal(new RunePool(0, 0), activated.State.RunePools["P1"]);
        var stackItem = Assert.Single(activated.State.StackItems);
        Assert.Equal(ViObjectId, stackItem.SourceObjectId);
        Assert.Equal(P4ActivatedAbilityCatalog.ViAltACardNo, stackItem.CardNo);
        Assert.Equal(P4ActivatedAbilityCatalog.ViDoublePowerAbilityEffectKind, stackItem.EffectKind);

        var p1Passed = await new CoreRuleEngine().ResolveAsync(
            activated.State,
            new PlayerIntent("intent-vi-alt-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Passed.Accepted, p1Passed.ErrorMessage);

        var resolved = await new CoreRuleEngine().ResolveAsync(
            p1Passed.State,
            new PlayerIntent("intent-vi-alt-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(resolved.Accepted, resolved.ErrorMessage);
        Assert.Empty(resolved.State.StackItems);
        var viState = resolved.State.CardObjects[ViObjectId];
        Assert.Equal(P4ActivatedAbilityCatalog.ViAltACardNo, viState.CardNo);
        Assert.Equal(6, viState.Power);
        Assert.Equal(3, viState.UntilEndOfTurnPowerModifier);
    }

    private static MatchState BuildViState(string cardNo)
    {
        return new MatchState(
            roomId: "vi-double-power-ability-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 1),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Base = [ViObjectId]
                },
                ["P2"] = PlayerZones.Empty
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [ViObjectId] = new(
                    ViObjectId,
                    power: 3,
                    tags: [CardObjectTags.UnitCard, CardObjectTags.Spellshield],
                    cardNo: cardNo,
                    ownerId: "P1",
                    controllerId: "P1")
            },
            objectLocations: new Dictionary<string, ObjectLocationState>(StringComparer.Ordinal)
            {
                [ViObjectId] = new("P1", "BASE")
            });
    }
}
