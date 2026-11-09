using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed partial class AzirTokenAttackMovementTests
{
    [Theory, InlineData("SFD·177/221"), InlineData("SFD·177a/221")]
    public async Task AnActualAssembledSongEarnsASeparateChoiceAndIndependentAttackSkill(string no)
    {
        var initial = RadGearTestPosition.Put(Position(no), "SONG", "SFD·059/221", "P1", "BASE");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await game.Act("P1", new PlayCardCommand("HOST", no, [], OptionalCosts: [HasteOptionalCostNames.HasteReady]));
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        Assert.Equal(no, game.State.CardObjects["SONG"].AttachedRuleText!.CardNo);
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Equal(2, game.State.TriggerQueue.Count);
        await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
        var choices=0;
        while (game.State.PendingCardChoice is not null) await Select(game, tokens[choices++]);
        Assert.Equal(2, choices); Assert.Equal(2, game.State.StackItems.Count);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is not null);
        Assert.Single(game.State.StackItems, item => item.UnitEvent!.Origin is null);
        Assert.All(tokens, id => Assert.Equal("BASE", game.State.ObjectLocations[id].Zone));
        await game.Top(); await game.Top();
        Assert.All(tokens, id => Assert.Equal("OTHER", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task AnActualImageCopiesTheAttackTextAndItsCeasedSourceCannotSupplyALastKnownBattlefield(bool sourceLeaves)
    {
        var initial = RadGearTestPosition.Put(Position("SFD·177/221"), "TEMPLATE", "SFD·177/221", "P2", "BASE");
        initial = RadGearTestPosition.Put(initial, "COPY", "UNL-200/219", "P1", "HAND");
        initial = RadGearTestPosition.Put(initial, "RETURN", "OGN·104/298", "P1", "HAND");
        var game = new Driver(initial); var tokens = await CreateTokens(game);
        await game.Act("P1", new PlayCardCommand("COPY", "UNL-200/219", ["TEMPLATE"])); await game.Top(); await game.Top();
        var image = Assert.Single(game.State.CardObjects.Values, c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.ImageTokenCardNo);
        Assert.Equal("SFD·177/221", image.CardNo);
        await game.Act("P1", new MoveUnitCommand(image.ObjectId, "BASE", "BATTLEFIELD:OTHER", []));
        Assert.Contains(image.ObjectId, game.State.PendingCardChoice!.LegalObjectIds);
        await Select(game, tokens);
        var skill = Assert.Single(game.State.StackItems); Assert.Equal(image.ObjectId, skill.SourceObjectId);
        if (sourceLeaves)
        {
            await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", [image.ObjectId])); await game.Top();
            Assert.False(game.State.CardObjects.ContainsKey(image.ObjectId));
            Assert.Equal(skill.StackItemId, Assert.Single(game.State.StackItems).StackItemId);
            RejectCoherentAttackHistoryForgeries(game);
        }
        await game.Top();
        Assert.All(tokens, id => Assert.Equal(sourceLeaves ? "BASE" : "BATTLEFIELD", game.State.ObjectLocations[id].Zone));
        Assert.All(tokens, id => Assert.Equal(sourceLeaves ? null : "OTHER", game.State.ObjectLocations[id].BattlefieldObjectId));
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AConfirmedTokenCannotBeReplacedByAnotherStillLegalTokenUsingACoherentSnapshot()
    {
        var game = new Driver(Position("SFD·177/221")); var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221"); await Select(game, tokens[0]);
        Assert.Empty(ValidateAttackHistory(game, game.State));
        var item = Assert.Single(game.State.StackItems);
        var journal = System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() });
        foreach (var forged in new[] {
            item with { TargetObjectIds = [tokens[1]], TargetGenerations = new Dictionary<string, long> {
                [tokens[1]] = game.State.CardObjects[tokens[1]].ObjectGeneration } },
            item with { TargetObjectIds = tokens, TargetGenerations = tokens.ToDictionary(id => id,
                id => game.State.CardObjects[id].ObjectGeneration) },
            item with { TargetGenerations = new Dictionary<string, long> {
                [tokens[0]] = game.State.CardObjects[tokens[0]].ObjectGeneration + 1 } }
        })
            Assert.Contains(ValidateAttackHistory(game, game.State with { StackItems = [forged] }),
                error => error == "unit event targets differ from original accepted confirmation");
        Assert.Equal(journal, System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() }));
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations[tokens[0]].BattlefieldObjectId);
        Assert.Equal("BASE", game.State.ObjectLocations[tokens[1]].Zone);
        await game.ColdCheckpoint();
    }

    [Fact]
    public async Task AnAcceptedTokenTargetConfirmationCannotBeRewoundToItsEarlierUnconfirmedSnapshot()
    {
        var game = new Driver(Position("SFD·177/221")); var tokens = await CreateTokens(game);
        await EnterAndAttack(game, "SFD·177/221");
        var unconfirmed = game.State;
        await Select(game, tokens[0]);
        Assert.Empty(ValidateAttackHistory(game, game.State));
        var journal = System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() });
        // Preserve the original accepted journal, but restore every target/choice
        // field from the real prior unconfirmed state at the current tick.
        var rewound = unconfirmed with { Tick = game.State.Tick };
        Assert.True(CoreRuleEngine.ValidUnitEventTargets(Assert.Single(rewound.StackItems)));
        Assert.Empty(MatchRecoveryValidator.ValidateComponentStateAndViews(rewound));
        Assert.Contains(ValidateAttackHistory(game, rewound),
            error => error == "unit event targets differ from original accepted confirmation");
        Assert.Equal(journal, System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() }));
        await game.Top();
        Assert.Equal("OTHER", game.State.ObjectLocations[tokens[0]].BattlefieldObjectId);
        Assert.Equal("BASE", game.State.ObjectLocations[tokens[1]].Zone);
        await game.ColdCheckpoint();
    }

    private static void RejectCoherentAttackHistoryForgeries(Driver game)
    {
        Assert.Empty(ValidateAttackHistory(game, game.State));
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!;
        var journal = System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() });
        foreach (var forged in new[] {
            item with { UnitEvent = context with { SourceGeneration = context.SourceGeneration + 1 } },
            item with { UnitEvent = context with { CapturedTick = context.CapturedTick + 1 } },
            item with { CardNo = "SFD·177a/221", UnitEvent = context with { CardNo = "SFD·177a/221" } },
            item with { StackItemId = "ordered-FORGED-ATTACK-BIRTH" }
        })
            Assert.Contains(ValidateAttackHistory(game, game.State with { StackItems = [forged] }),
                error => error == "battlefield timing skill differs from its original accepted capture");
        Assert.Equal(journal, System.Text.Json.JsonSerializer.Serialize(new { commands = game.Commands(), events = game.Events() }));
    }

    private static IReadOnlyList<string> ValidateAttackHistory(Driver game, MatchState state)
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        state = System.Text.Json.JsonSerializer.Deserialize<MatchState>(System.Text.Json.JsonSerializer.Serialize(state, options), options)!;
        var commands = System.Text.Json.JsonSerializer.Deserialize<RecoveredCommand[]>(System.Text.Json.JsonSerializer.Serialize(game.Commands(), options), options)!;
        var events = System.Text.Json.JsonSerializer.Deserialize<RecoveredEvent[]>(System.Text.Json.JsonSerializer.Serialize(game.Events(), options), options)!;
        var sequence = events.Length == 0 ? 0 : events[^1].Sequence;
        var snapshots = ResolutionResult.BuildSnapshots(state); var prompts = ResolutionResult.BuildPrompts(state);
        var views = state.Seats.Keys.ToDictionary(player => player,
            player => new RecoveredPlayerView(player, state.Tick, sequence, snapshots[player], state.Tick, sequence, prompts[player]));
        var spectator = MatchReplayRedactor.BuildSpectatorFrame(state.RoomId, state.Tick, sequence, [], state);
        return MatchRecoveryValidator.Validate(state.RoomId, sequence, commands, events, views, state, state.Tick, spectator);
    }
}
