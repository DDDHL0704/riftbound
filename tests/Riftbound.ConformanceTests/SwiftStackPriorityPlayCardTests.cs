using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class SwiftStackPriorityPlayCardTests
{
    private const string PunishmentCardNo = "UNL-007/219";
    private const string PunishmentObjectId = "P2-HAND-PUNISHMENT";
    private const string BattlefieldTargetObjectId = "P1-BATTLEFIELD-TARGET";

    [Fact]
    public async Task SwiftSpellPromptAndPlayCardAreLegalInSpellDuelStackPriorityWindow()
    {
        var state = BuildStackPriorityState();
        var session = new MatchSession(state, new CoreRuleEngine(), new RecordingMatchJournal());
        session.EnsurePlayer("P1");
        session.EnsurePlayer("P2");

        var prompt = session.PromptFor("P2");

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.StackPriority, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, PunishmentObjectId, StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], target => string.Equals(target.Id, BattlefieldTargetObjectId, StringComparison.Ordinal));

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-p2-swift-punishment-response", "P2", CommandTypes.PlayCard),
            new PlayCardCommand(
                PunishmentObjectId,
                PunishmentCardNo,
                [BattlefieldTargetObjectId]),
            CancellationToken.None);

        Assert.True(result.Accepted, result.ErrorMessage);
        Assert.Equal(6, result.State.Tick);
        Assert.Equal(TimingStates.NeutralClosed, result.State.TimingState);
        Assert.Equal("P2", result.State.ActivePlayerId);
        Assert.Equal("P2", result.State.PriorityPlayerId);
        Assert.Empty(result.State.PassedPriorityPlayerIds);
        Assert.Equal(new RunePool(0, 0), result.State.RunePools["P2"]);
        Assert.DoesNotContain(PunishmentObjectId, result.State.PlayerZones["P2"].Hand);
        Assert.Equal(2, result.State.StackItems.Count);

        var responseStackItem = result.State.StackItems[^1];
        Assert.Equal($"STACK-6-{PunishmentObjectId}", responseStackItem.StackItemId);
        Assert.Equal("P2", responseStackItem.ControllerId);
        Assert.Equal(PunishmentObjectId, responseStackItem.SourceObjectId);
        Assert.Equal(PunishmentCardNo, responseStackItem.CardNo);
        Assert.Equal("PUNISHMENT_DAMAGE_3", responseStackItem.EffectKind);
        Assert.Equal([BattlefieldTargetObjectId], responseStackItem.TargetObjectIds);
        Assert.Equal(TimingStates.SpellDuelOpen, responseStackItem.TimingContext);
        Assert.Contains(result.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["sourceObjectId"] as string, PunishmentObjectId, StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["effectKind"] as string, "PUNISHMENT_DAMAGE_3", StringComparison.Ordinal));
    }

    private static MatchState BuildStackPriorityState()
    {
        return new MatchState(
            roomId: "swift-stack-priority-play-card-test",
            tick: 5,
            turnNumber: 1,
            activePlayerId: "P2",
            seats: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["P1"] = "connection-1",
                ["P2"] = "connection-2"
            },
            status: MatchStatuses.InProgress,
            readyPlayerIds: ["P1", "P2"],
            turnPlayerId: "P1",
            phase: MatchPhases.Main,
            timingState: TimingStates.NeutralClosed,
            runePools: new Dictionary<string, RunePool>(StringComparer.Ordinal)
            {
                ["P1"] = RunePool.Empty,
                ["P2"] = new(2, 0)
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Battlefields = [BattlefieldTargetObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Hand = [PunishmentObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [PunishmentObjectId] = new(
                    PunishmentObjectId,
                    cardNo: PunishmentCardNo,
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P2",
                    controllerId: "P2"),
                [BattlefieldTargetObjectId] = new(
                    BattlefieldTargetObjectId,
                    cardNo: "SFD·125/221",
                    power: 3,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                ["P1-SPELL-OPEN"] = new(
                    "P1-SPELL-OPEN",
                    cardNo: "OGS·003/024",
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1")
            },
            priorityPlayerId: "P2",
            stackItems:
            [
                new StackItemState(
                    "STACK-OPEN",
                    "P1",
                    "P1-SPELL-OPEN",
                    "INCINERATE_DAMAGE_2",
                    "OGS·003/024",
                    [BattlefieldTargetObjectId],
                    damageAmount: 2,
                    timingContext: TimingStates.SpellDuelOpen)
            ]);
    }

    private sealed class RecordingMatchJournal : IMatchJournal
    {
        public ValueTask RecordAsync(MatchJournalEntry entry, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }
}
