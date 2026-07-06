using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class EnemySpellSkillTargetProtectionTests
{
    private const string IncinerateObjectId = "P1-SPELL-INCINERATE";
    private const string ProtectedUnitObjectId = "P2-PROTECTED-UNIT";
    private const string OtherBattlefieldUnitObjectId = "P2-BATTLEFIELD-UNIT";

    [Fact]
    public async Task EnemySpellCannotTargetSourceUnitWithEnemySpellSkillProtection()
    {
        var state = BuildTargetProtectionState();

        var result = await PlayIncinerateAsync(state, "intent-incinerate-protected-baron");

        AssertTargetProtectionRejected(state, result);
    }

    [Fact]
    public async Task EnemySpellCannotTargetSourceUnitWithAlternativeProtectionWording()
    {
        var state = BuildTargetProtectionState(protectedCardNo: "SFD·105/221", protectedPower: 5);

        var result = await PlayIncinerateAsync(state, "intent-incinerate-protected-desert-plunderer");

        AssertTargetProtectionRejected(state, result);
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public async Task EnemySpellTargetProtectionHonorsRequiredExperience(int protectedControllerExperience, bool expectedAccepted)
    {
        var state = BuildTargetProtectionState(
            protectedCardNo: "UNL-059/219",
            protectedPower: 12,
            protectedControllerExperience: protectedControllerExperience);

        var result = await PlayIncinerateAsync(state, $"intent-incinerate-yi-level-{protectedControllerExperience}");

        Assert.Equal(expectedAccepted, result.Accepted);
        if (!expectedAccepted)
        {
            AssertTargetProtectionRejected(state, result);
        }
    }

    private static void AssertTargetProtectionRejected(MatchState state, ResolutionResult result)
    {
        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InvalidTarget, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(2, 0), result.State.RunePools["P1"]);
        Assert.Equal([IncinerateObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
        Assert.Equal(0, result.State.CardObjects[ProtectedUnitObjectId].Damage);
    }

    [Fact]
    public void PlayCardPromptOmitsEnemySourceUnitWithEnemySpellSkillProtection()
    {
        var state = BuildTargetProtectionState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P1");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(
            playCandidate.Sources ?? [],
            source => string.Equals(source.Id, IncinerateObjectId, StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var sourceRequirement = Assert.Single(
            sourceRequirements,
            requirement => string.Equals(requirement["sourceObjectId"] as string, IncinerateObjectId, StringComparison.Ordinal));
        var choicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            sourceRequirement["targetChoicesByIndex"]);
        var targetChoiceIds = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(choicesByIndex["0"])
            .Select(choice => choice.Id)
            .ToArray();

        Assert.Equal([OtherBattlefieldUnitObjectId], targetChoiceIds);
        Assert.DoesNotContain(ProtectedUnitObjectId, targetChoiceIds);
    }

    private static async Task<ResolutionResult> PlayIncinerateAsync(MatchState state, string intentId)
    {
        return await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(intentId, "P1", CommandTypes.PlayCard),
            new PlayCardCommand(
                IncinerateObjectId,
                "OGS·003/024",
                [ProtectedUnitObjectId]),
            CancellationToken.None);
    }

    private static MatchState BuildTargetProtectionState(
        string protectedCardNo = "UNL-147/219",
        int protectedPower = 12,
        int protectedControllerExperience = 0)
    {
        return new MatchState(
            roomId: "enemy-spell-skill-target-protection-test",
            tick: 0,
            turnNumber: 1,
            activePlayerId: "P1",
            seats: Seats(),
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralOpen,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = new(2, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [IncinerateObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [ProtectedUnitObjectId, OtherBattlefieldUnitObjectId]
                }
            },
            playerExperience: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["P1"] = 0,
                ["P2"] = protectedControllerExperience
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [IncinerateObjectId] = NonUnit(
                    IncinerateObjectId,
                    "OGS·003/024",
                    CardObjectTags.SpellCard,
                    "P1",
                    manaCost: 2),
                [ProtectedUnitObjectId] = Unit(
                    ProtectedUnitObjectId,
                    cardNo: protectedCardNo,
                    power: protectedPower,
                    ownerId: "P2",
                    controllerId: "P2"),
                [OtherBattlefieldUnitObjectId] = Unit(
                    OtherBattlefieldUnitObjectId,
                    cardNo: "SFD·125/221",
                    power: 2,
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }

    private static Dictionary<string, string> Seats()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["P1"] = "connection-1",
            ["P2"] = "connection-2"
        };
    }

    private static CardObjectState Unit(
        string objectId,
        string cardNo,
        int power,
        string ownerId,
        string controllerId)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            power: power,
            tags: [CardObjectTags.UnitCard],
            ownerId: ownerId,
            controllerId: controllerId);
    }

    private static CardObjectState NonUnit(
        string objectId,
        string cardNo,
        string tag,
        string playerId,
        int manaCost)
    {
        return new CardObjectState(
            objectId,
            cardNo: cardNo,
            manaCost: manaCost,
            tags: [tag],
            ownerId: playerId,
            controllerId: playerId);
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
