using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class TheCurtainRisesPaymentCostTests
{
    private const string SourceObjectId = "P1-SPELL-THE-CURTAIN-RISES";
    private const string TargetObjectId = "P2-BATTLEFIELD-UNIT-001";
    private const string CardNo = "UNL-009/219";
    private const string EffectKind = "THE_CURTAIN_RISES_READY_UNIT";

    [Fact]
    public void PromptExposesEchoReadyPaymentCostTargetingStackRequirement()
    {
        var state = BuildState(mana: 4);
        var prompt = ResolutionResult.BuildPrompts(state)["P1"];

        Assert.True(prompt.Actionable);
        Assert.Equal(PromptTypes.MainAction, prompt.View?.Type);
        Assert.Contains(CommandTypes.PlayCard, prompt.Actions);
        var playCandidate = Assert.Single(
            prompt.Candidates ?? [],
            candidate => string.Equals(candidate.Action, CommandTypes.PlayCard, StringComparison.Ordinal));
        Assert.True(playCandidate.Enabled);
        Assert.Contains(playCandidate.Sources ?? [], source => string.Equals(source.Id, SourceObjectId, StringComparison.Ordinal));
        Assert.Contains(playCandidate.Targets ?? [], target => string.Equals(target.Id, TargetObjectId, StringComparison.Ordinal));
        Assert.Contains(playCandidate.OptionalCosts ?? [], optionalCost => string.Equals(optionalCost.Id, EchoOptionalCostNames.Echo, StringComparison.Ordinal));

        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(playCandidate.Metadata);
        var sourceRequirements = Assert.IsAssignableFrom<IEnumerable<IReadOnlyDictionary<string, object?>>>(
                metadata["sourceRequirements"])
            .ToArray();
        var requirement = Assert.Single(sourceRequirements, entry =>
            string.Equals(entry["sourceObjectId"] as string, SourceObjectId, StringComparison.Ordinal));
        Assert.Equal(CardNo, Assert.IsType<string>(requirement["cardNo"]));
        Assert.Equal(2, Assert.IsType<int>(requirement["manaCost"]));
        Assert.Equal(1, Assert.IsType<int>(requirement["minTargetCount"]));
        Assert.Equal(1, Assert.IsType<int>(requirement["maxTargetCount"]));
        Assert.Equal(CardTargetScopes.AnyUnit, Assert.IsType<string>(requirement["targetScope"]));
        Assert.Equal(4, Assert.IsType<int>(requirement["availableMana"]));

        var targetChoicesByIndex = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(
            requirement["targetChoicesByIndex"]);
        var firstTargetChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            targetChoicesByIndex["0"]);
        Assert.Contains(firstTargetChoices, target => string.Equals(target.Id, TargetObjectId, StringComparison.Ordinal));
        var optionalCostChoices = Assert.IsAssignableFrom<IEnumerable<ActionPromptChoiceDto>>(
            requirement["optionalCostChoices"]);
        Assert.Contains(optionalCostChoices, choice => string.Equals(choice.Id, EchoOptionalCostNames.Echo, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, 2, 1, 2)]
    [InlineData(true, 4, 2, 0)]
    public async Task StackResolutionReadiesTargetAndRecordsPaymentAudit(
        bool payEcho,
        int expectedManaPaid,
        int expectedRepeatCount,
        int expectedRemainingMana)
    {
        var state = BuildState(mana: 4);
        var optionalCosts = payEcho ? new[] { EchoOptionalCostNames.Echo } : [];
        var command = new PlayCardCommand(SourceObjectId, CardNo, [TargetObjectId], OptionalCosts: optionalCosts);

        var played = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent(payEcho ? "intent-curtain-rises-echo" : "intent-curtain-rises", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.True(played.Accepted, played.ErrorMessage);
        Assert.Equal(1, played.State.Tick);
        Assert.Equal(expectedRemainingMana, played.State.RunePools["P1"].Mana);
        Assert.Equal(["CARD_PLAYED", "COST_PAID", "STACK_ITEM_ADDED"], played.Events.Select(gameEvent => gameEvent.Kind).ToArray());
        var costPaid = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "COST_PAID", StringComparison.Ordinal));
        Assert.Equal(expectedManaPaid, Assert.IsType<int>(costPaid.Payload["mana"]));
        Assert.Equal(2, Assert.IsType<int>(costPaid.Payload["baseMana"]));
        Assert.Equal(optionalCosts, Assert.IsType<string[]>(costPaid.Payload["optionalCosts"]));
        Assert.Equal(EffectKind, Assert.IsType<string>(costPaid.Payload["reason"]));
        var stackAdded = Assert.Single(played.Events, gameEvent => string.Equals(gameEvent.Kind, "STACK_ITEM_ADDED", StringComparison.Ordinal));
        Assert.Equal(expectedRepeatCount, Assert.IsType<int>(stackAdded.Payload["effectRepeatCount"]));

        var stackItem = Assert.Single(played.State.StackItems);
        Assert.Equal(EffectKind, stackItem.EffectKind);
        Assert.Equal(expectedRepeatCount, stackItem.EffectRepeatCount);
        Assert.Equal(optionalCosts, stackItem.OptionalCosts);
        Assert.True(played.State.CardObjects[TargetObjectId].IsExhausted);

        var p1Pass = await new CoreRuleEngine().ResolveAsync(
            played.State,
            new PlayerIntent("intent-curtain-rises-p1-pass", "P1", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);
        Assert.True(p1Pass.Accepted, p1Pass.ErrorMessage);
        var p2Pass = await new CoreRuleEngine().ResolveAsync(
            p1Pass.State,
            new PlayerIntent("intent-curtain-rises-p2-pass", "P2", CommandTypes.PassPriority),
            new PassPriorityCommand(),
            CancellationToken.None);

        Assert.True(p2Pass.Accepted, p2Pass.ErrorMessage);
        Assert.Empty(p2Pass.State.StackItems);
        Assert.False(p2Pass.State.CardObjects[TargetObjectId].IsExhausted);
        Assert.Equal([SourceObjectId], p2Pass.State.PlayerZones["P1"].Graveyard);
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "STACK_ITEM_RESOLVED", StringComparison.Ordinal));
        Assert.Contains(p2Pass.Events, gameEvent =>
            string.Equals(gameEvent.Kind, "UNIT_READIED", StringComparison.Ordinal)
            && string.Equals(gameEvent.Payload["targetObjectId"] as string, TargetObjectId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EchoPaymentRejectsWithoutEnoughManaAndDoesNotMutate()
    {
        var state = BuildState(mana: 3);
        var command = new PlayCardCommand(
            SourceObjectId,
            CardNo,
            [TargetObjectId],
            OptionalCosts: [EchoOptionalCostNames.Echo]);

        var result = await new CoreRuleEngine().ResolveAsync(
            state,
            new PlayerIntent("intent-curtain-rises-echo-insufficient-mana", "P1", CommandTypes.PlayCard),
            command,
            CancellationToken.None);

        Assert.False(result.Accepted);
        Assert.Equal(ErrorCodes.InsufficientCost, result.ErrorCode);
        Assert.Empty(result.Events);
        Assert.Equal(MatchStateHasher.Hash(state), MatchStateHasher.Hash(result.State));
        Assert.Equal(new RunePool(3, 0), result.State.RunePools["P1"]);
        Assert.Equal([SourceObjectId], result.State.PlayerZones["P1"].Hand);
        Assert.Empty(result.State.StackItems);
        Assert.Null(result.State.PendingPayment);
    }

    private static MatchState BuildState(int mana)
    {
        return new MatchState(
            roomId: "the-curtain-rises-payment-cost-test",
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
                ["P1"] = new(mana, 0),
                ["P2"] = RunePool.Empty
            },
            playerZones: new Dictionary<string, PlayerZones>(StringComparer.Ordinal)
            {
                ["P1"] = PlayerZones.Empty with
                {
                    Hand = [SourceObjectId]
                },
                ["P2"] = PlayerZones.Empty with
                {
                    Battlefields = [TargetObjectId]
                }
            },
            cardObjects: new Dictionary<string, CardObjectState>(StringComparer.Ordinal)
            {
                [SourceObjectId] = new(
                    SourceObjectId,
                    cardNo: CardNo,
                    manaCost: 2,
                    tags: [CardObjectTags.SpellCard],
                    ownerId: "P1",
                    controllerId: "P1"),
                [TargetObjectId] = new(
                    TargetObjectId,
                    cardNo: "SFD·125/221",
                    power: 2,
                    isExhausted: true,
                    tags: [CardObjectTags.UnitCard],
                    ownerId: "P2",
                    controllerId: "P2")
            });
    }
}
