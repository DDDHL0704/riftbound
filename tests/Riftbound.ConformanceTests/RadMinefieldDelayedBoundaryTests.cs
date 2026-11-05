using System.Text.Json;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class RadMinefieldDelayedBoundaryTests
{
    private const string Minefield = "RAD·142/167";
    private static readonly string[] OriginalBombs = ["B1", "B2", "B3"];

    private static MatchState Position(bool cascade = false, bool echo = false)
    {
        var state = VenUnitEntryBasicTests.Position(Minefield);
        Assert.True(P6TokenFactoryCatalog.TryGetByCardNo(P6TokenFactoryCatalog.BombTokenCardNo, out var factory));
        foreach (var id in OriginalBombs)
        {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = factory.CreateObject(id, "P1", "P1") } };
            state = LocalDestructionRecallTests.At(state, id, "P1", "BATTLEFIELD",
                cascade || id != "B3" ? "BF" : "OTHER");
        }
        if (cascade)
        {
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["ENEMY"] = OfficialGarenConquestBattlefieldCountTests.Printed("ENEMY", "OGN·082/298", "P2"),
                ["BF"] = state.CardObjects["BF"] with { ControllerId = "P2" } } };
            state = LocalDestructionRecallTests.At(state, "ENEMY", "P2", "BATTLEFIELD", "BF");
        }
        if (echo) state = state with { UntilEndOfTurnEffects = [EchoCostRules.GrantPrefix + "P1"] };
        state = OfficialCopyIdentityTests.AddSpell(state, "SECOND", Minefield);
        return OfficialCopyIdentityTests.AddSpell(state, "COUNTER", "OGN·064/298");
    }

    private static CardObjectState[] Bombs(Driver game)
        => game.State.CardObjects.Values.Where(card => card.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo).ToArray();

    private static BombRebuildContext[] Scheduled(Driver game)
        => game.State.UntilEndOfTurnEffects.Where(effect => effect.StartsWith(CoreRuleEngine.BombRebuildPrefix, StringComparison.Ordinal))
            .Select(effect => Assert.IsType<BombRebuildContext>(CoreRuleEngine.ReadBombRebuild(effect))).ToArray();

    private static async Task ResolveBombSkills(Driver game)
    {
        for (var step = 0; game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0; step++)
        {
            Assert.InRange(step, 0, 11);
            var tick = game.State.Tick;
            if (game.State.PendingCardChoice is { } choice)
            {
                Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
                Assert.Equal(["ENEMY"], choice.LegalObjectIds);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["ENEMY"]));
            }
            else if (game.State.TriggerQueue.Count > 0)
                await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
                    game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
            else await game.Top();
            Assert.True(game.State.Tick > tick);
        }
        Assert.Null(game.State.PendingCardChoice);
    }

    private static async Task RebuildOne(Driver game, int count, string battlefield)
    {
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal(CoreRuleEngine.BombRebuildChoiceWindow, choice.ChoiceWindow);
        var item = Assert.Single(game.State.StackItems, item => item.StackItemId == choice.ResolvingStackItemId);
        Assert.Equal(count, item.BombRebuild!.BombCount);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [battlefield]));
        await ResolveReadyRebuild(game, count, battlefield);
    }

    private static async Task ResolveReadyRebuild(Driver game, int count, string battlefield)
    {
        var firstEvent = game.Events().Length;
        await game.Top();
        var created = game.Events().Skip(firstEvent).Where(entry => entry.Event.Kind == "EQUIPMENT_TOKEN_CREATED").ToArray();
        Assert.Equal(count, created.Length);
        var resolved = Assert.Single(game.Events().Skip(firstEvent), entry => entry.Event.Kind == "BOMB_REBUILD_RESOLVED");
        Assert.Equal(count, resolved.Event.Payload["bombCount"]);
        Assert.Equal(battlefield, resolved.Event.Payload["battlefieldObjectId"]);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ChainedBombDeathsDoNotIncreaseTheSpellsCapturedRebuildCount(int selected)
    {
        var game = new Driver(Position(cascade: true));
        await game.Act("P1", new PlayCardCommand("UNIT", Minefield, OriginalBombs.Take(selected).ToArray()));
        await game.Top();
        Assert.Equal(selected, Assert.Single(Scheduled(game)).BombCount);
        await ResolveBombSkills(game);
        Assert.Empty(Bombs(game));
        Assert.Equal(6, game.State.CardObjects["ENEMY"].Damage);
        Assert.Equal(3 - selected, game.Events().Count(entry => entry.Event.Kind == "EQUIPMENT_DESTROYED"
            && entry.Event.Payload.GetValueOrDefault("reason") as string == "BOMB_CHAIN"));
        Assert.Equal(selected, Assert.Single(Scheduled(game)).BombCount);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "BOMB_REBUILD_SCHEDULED");
        await game.Act("P1", new EndTurnCommand());
        await RebuildOne(game, selected, "OTHER");
        Assert.Equal(selected, Bombs(game).Length);
        Assert.All(Bombs(game), bomb => {
            Assert.DoesNotContain(bomb.ObjectId, OriginalBombs);
            Assert.Equal(new("P1", "BATTLEFIELD", "OTHER"), game.State.ObjectLocations[bomb.ObjectId]);
            Assert.False(bomb.IsExhausted);
        });
        Assert.Empty(Scheduled(game));
        await game.Replay();
    }

    [Fact]
    public async Task PaidEchoCannotDestroyTheSameDepartedBombOrScheduleItsRebuildTwice()
    {
        var game = new Driver(Position(echo: true));
        await game.Act("P1", new PlayCardCommand("UNIT", Minefield, ["B1"], OptionalCosts: ["ECHO"]));
        Assert.Equal(2, Assert.Single(game.State.StackItems).EffectRepeatCount);
        Assert.Equal(16, game.State.RunePools["P1"].Mana);
        Assert.Equal(18, game.State.RunePools["P1"].TotalPower);
        await game.Top();
        Assert.False(game.State.CardObjects.ContainsKey("B1"));
        Assert.Equal(2, Bombs(game).Length);
        Assert.Equal(1, Assert.Single(Scheduled(game)).BombCount);
        Assert.Single(game.Events(), entry => entry.Event.Kind == "BOMB_REBUILD_SCHEDULED");
        await game.Act("P1", new EndTurnCommand());
        await RebuildOne(game, 1, "OTHER");
        Assert.Equal(3, Bombs(game).Length);
        Assert.Single(Bombs(game), bomb => !OriginalBombs.Contains(bomb.ObjectId));
        Assert.Empty(Scheduled(game));
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SeparateSpellsKeepIndependentCountsAndDestinationsAndTheirDelayedSkillsAreNotSpells(bool tryCounter)
    {
        var game = new Driver(Position());
        await game.Act("P1", new PlayCardCommand("UNIT", Minefield, ["B1"])); await game.Top();
        await game.Act("P1", new PlayCardCommand("SECOND", Minefield, ["B2", "B3"])); await game.Top();
        // Simultaneous deaths still open trigger ordering before skills with no
        // legal enemy target are discarded, even though the chain is empty.
        await ResolveBombSkills(game);
        Assert.Equal([1, 2], Scheduled(game).Select(context => context.BombCount).Order());
        Assert.Equal(2, Scheduled(game).Select(context => context.InstanceId).Distinct().Count());
        Assert.Empty(Bombs(game));
        for (var passes = 0; game.State.SpellDuelState.IsActive; passes++)
        {
            Assert.InRange(passes, 0, 1);
            await game.Act(Assert.IsType<string>(game.State.FocusPlayerId), new PassFocusCommand());
        }
        Assert.Equal(TimingStates.NeutralOpen, game.State.TimingState);
        await game.Act("P1", new EndTurnCommand());
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds:
            game.State.TriggerQueue.Select(trigger => trigger.TriggerId).ToArray()));
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Empty(Scheduled(game));
        // Pending items all confirm in insertion order before either can resolve.
        for (var index = 0; index < 2; index++)
        {
            var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
            var item = game.State.StackItems.Single(item => item.StackItemId == choice.ResolvingStackItemId);
            var count = item.BombRebuild!.BombCount;
            await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow,
                [count == 1 ? "BF" : "OTHER"]));
            Assert.Empty(Bombs(game));
        }
        Assert.Null(game.State.PendingCardChoice);
        for (var remaining = 2; remaining > 0; remaining--)
        {
            var item = game.State.StackItems[^1];
            var count = item.BombRebuild!.BombCount;
            if (tryCounter)
            {
                Assert.Equal("P1", game.State.PriorityPlayerId);
                var command = new PlayCardCommand("COUNTER", "OGN·064/298", [item.StackItemId]);
                var hash = MatchStateHasher.Hash(game.State);
                var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
                Assert.False(new CoreRuleEngine().PreviewPlayCard(game.State, "P1", PlayCostPreviewTests.Request(game.State, command)).CanPay);
                var rejected = await new CoreRuleEngine().ResolveAsync(game.State,
                    new("counter-delayed-" + remaining, "P1", command.CmdType), command, default);
                Assert.False(rejected.Accepted); Assert.Empty(rejected.Events);
                Assert.Equal(hash, MatchStateHasher.Hash(rejected.State));
                Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(rejected.State)));
            }
            await ResolveReadyRebuild(game, count, count == 1 ? "BF" : "OTHER");
        }
        Assert.Empty(game.State.StackItems); Assert.Null(game.State.PendingCardChoice);
        Assert.Single(Bombs(game), bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "BF");
        Assert.Equal(2, Bombs(game).Count(bomb => game.State.ObjectLocations[bomb.ObjectId].BattlefieldObjectId == "OTHER"));
        Assert.Equal(2, game.Events().Count(entry => entry.Event.Kind == "BOMB_REBUILD_RESOLVED"));
        await game.Replay();
    }
}
