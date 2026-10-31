using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;

namespace Riftbound.ConformanceTests;

public sealed class BombRebuildContextTests
{
    [Fact]
    public void MarkerRoundTripsAndRejectsTampering()
    {
        var state = Position() with { Tick = 4 };
        var context = Context(state, 3);
        var marker = CoreRuleEngine.BombRebuildMarker(context);

        Assert.Equal(context, CoreRuleEngine.ReadBombRebuild(marker));
        Assert.Null(CoreRuleEngine.ReadBombRebuild(marker + "x"));
        var payload = Convert.FromBase64String(marker[CoreRuleEngine.BombRebuildPrefix.Length..]);
        payload[^1] ^= 1;
        Assert.Null(CoreRuleEngine.ReadBombRebuild(
            CoreRuleEngine.BombRebuildPrefix + Convert.ToBase64String(payload)));
    }

    [Fact]
    public void SchedulingCapturesTheSourceGenerationAndIsIdempotent()
    {
        var state = Position() with { Tick = 4 };
        var source = state.CardObjects["UNIT"];
        var item = new StackItemState(
            "bomb-source-stack", "P1", source.ObjectId, "TEST_BOMB_COST", source.CardNo);
        var effects = new List<string>();
        var events = new List<GameEvent>();

        Assert.True(CoreRuleEngine.TryScheduleBombRebuild(state, item, 2, effects, events));
        Assert.False(CoreRuleEngine.TryScheduleBombRebuild(state, item, 2, effects, events));
        var context = Assert.IsType<BombRebuildContext>(CoreRuleEngine.ReadBombRebuild(Assert.Single(effects)));
        Assert.Equal(new ObjectBinding(source.ObjectId, source.ObjectGeneration), context.Source);
        Assert.Equal(2, context.BombCount);
        Assert.True(CoreRuleEngine.ValidBombRebuild(state, context));
        Assert.Single(events);
    }

    [Fact]
    public async Task EndStepChoiceRebuildsAllBombsOnOnePublicBattlefield()
    {
        var state = BombStackState(3);
        var engine = new CoreRuleEngine();

        var firstPass = await Pass(engine, state, "P1");
        Assert.True(firstPass.Accepted, firstPass.ErrorMessage);
        var choice = Assert.IsType<PendingCardChoiceState>(firstPass.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.BombRebuildChoiceWindow, choice.ChoiceWindow);
        Assert.Equal(["BF", "OTHER"], choice.LegalObjectIds);
        Assert.DoesNotContain("UNIT", choice.LegalObjectIds);

        var invalid = await engine.ResolveAsync(
            firstPass.State,
            new("bomb-invalid-choice", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["UNIT"]),
            default);
        Assert.False(invalid.Accepted);
        Assert.Equal(MatchStateHasher.Hash(firstPass.State), MatchStateHasher.Hash(invalid.State));

        var selected = await engine.ResolveAsync(
            firstPass.State,
            new("bomb-select-choice", "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["BF"]),
            default);
        Assert.True(selected.Accepted, selected.ErrorMessage);
        Assert.Null(selected.State.PendingCardChoice);
        Assert.Single(selected.State.StackItems);

        var resolved = await Pass(engine, selected.State, "P1");
        resolved = await Pass(engine, resolved.State, "P2");
        Assert.True(resolved.Accepted, resolved.ErrorMessage);

        var bombs = resolved.State.CardObjects.Values
            .Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo)
            .OrderBy(card => card.ObjectId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, bombs.Length);
        Assert.All(bombs, bomb =>
        {
            Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.CardNo);
            Assert.Equal("P1", bomb.OwnerId);
            Assert.Equal("P1", bomb.ControllerId);
            Assert.False(bomb.IsExhausted);
            Assert.Contains(CardObjectTags.EquipmentCard, bomb.Tags);
            Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
            Assert.Equal(new ObjectLocationState("P1", "BATTLEFIELD", "BF"), resolved.State.ObjectLocations[bomb.ObjectId]);
        });
        Assert.Equal(3, resolved.Events.Count(eventRecord => eventRecord.Kind == "EQUIPMENT_TOKEN_CREATED"));
        Assert.Empty(resolved.State.StackItems);
    }

    [Fact]
    public void RecoveryRejectsInvalidCapturedBombRebuildState()
    {
        var state = BombStackState(0);
        var errors = MatchRecoveryValidator.ValidateComponentStateAndViews(state);

        Assert.Contains(errors, error => error.Contains("invalid captured bomb rebuild stack", StringComparison.Ordinal));
    }

    [Fact]
    public void PublicSnapshotsExposeOnlyBombCountAndChoiceWindow()
    {
        var state = BombStackState(2);
        var snapshots = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(state));

        Assert.Contains("bombRebuild", snapshots, StringComparison.Ordinal);
        Assert.Contains("bombCount", snapshots, StringComparison.Ordinal);
        Assert.DoesNotContain("creatingStackItemId", snapshots, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceGeneration", snapshots, StringComparison.Ordinal);
    }

    private static async Task<ResolutionResult> Pass(
        CoreRuleEngine engine,
        MatchState state,
        string playerId)
        => await engine.ResolveAsync(
            state,
            new("bomb-pass-" + state.Tick + "-" + playerId, playerId, CommandTypes.PassPriority),
            new PassPriorityCommand(),
            default);

    private static BombRebuildContext Context(MatchState state, int count)
    {
        var source = state.CardObjects["UNIT"];
        return new(
            "bomb-source-stack:BOMB-REBUILD:0",
            "bomb-source-stack",
            source.CardNo!,
            "P1",
            new(source.ObjectId, source.ObjectGeneration),
            count,
            state.TurnNumber,
            state.TurnPlayerId,
            0,
            state.Tick);
    }

    private static MatchState BombStackState(int count)
    {
        var state = Position() with
        {
            Tick = 4,
            TimingState = TimingStates.NeutralClosed,
            PriorityPlayerId = "P1",
            ActivePlayerId = "P1",
            PassedPriorityPlayerIds = []
        };
        var context = Context(state, count);
        var item = new StackItemState(
            "ordered-" + context.InstanceId + ":END",
            "P1",
            context.Source.ObjectId,
            CoreRuleEngine.BombRebuildEffect,
            context.CardNo)
        {
            BombRebuild = context
        };
        return state with { StackItems = [item] };
    }

    private static MatchState Position()
        => VenUnitEntryBasicTests.Position("VEN·026");
}
