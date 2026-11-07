using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

// Official RAD·094 CN text; core 383.4.d, 403.2, 403.4.b and 383.4.a.2.
public sealed class HeldEquipmentTokenTests
{
    private const string Monkey = "RAD·094/167";

    [Fact]
    public async Task OfficialHoldEquipmentTemplateRetainsCountDestinationAndTokenIdentity()
    {
        var card = Assert.Single((await OfficialCardCatalog.LoadRuntimeAsync()).Cards, c => c.CardNo == Monkey);
        var spec = Assert.Single(RuleTextParser.Parse(card).Triggers,
            t => t.Kind == TriggerKinds.UnitBattlefieldHeldCreateEquipment);
        Assert.Equal(TriggerTimings.BattlefieldHeld, spec.Timing);
        Assert.Equal(TriggerTargetScopes.SourceUnit, spec.TargetScope);
        Assert.Equal(3, spec.CreatedTokenCount); Assert.Equal("炸弹", spec.CreatedTokenName);
        Assert.Equal(TriggerTokenDestinations.Battlefield, spec.CreatedTokenDestination);
        Assert.False(spec.CreatedTokenExhausted);
    }

    [Theory]
    [InlineData("F")] [InlineData("OTHER")]
    public async Task HoldingCreatesThreeActiveDeployBombsAtTheSingleConfirmedBattlefieldAfterResponses(string destination)
    {
        var game = new Driver(Position());
        await game.Act("P2", new EndTurnCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        var choice = Assert.IsType<PendingCardChoiceState>(game.State.PendingCardChoice);
        Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
        Assert.Equal(1, choice.RequiredCount); Assert.Equal(1, choice.MaxCount);
        Assert.Equal(new[] { "F", "OTHER" }, choice.LegalObjectIds);
        Assert.Empty(Bombs(game.State)); Assert.Null(game.State.PriorityPlayerId);
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [destination]));
        Assert.Empty(Bombs(game.State)); Assert.NotNull(game.State.PriorityPlayerId);
        var skill = Assert.Single(game.State.StackItems);
        Assert.Equal("UNIT_CREATE_BATTLEFIELD_EQUIPMENT", skill.HeldContext!.Kind);
        Assert.Equal(destination, Assert.Single(skill.TargetObjectIds));
        await game.Top();
        var bombs = Bombs(game.State); Assert.Equal(3, bombs.Length);
        Assert.All(bombs, id => {
            var bomb = game.State.CardObjects[id];
            Assert.Equal(P6TokenFactoryCatalog.BombTokenCardNo, bomb.TokenFactoryCardNo);
            Assert.Equal("P1", bomb.OwnerId); Assert.Equal("P1", bomb.ControllerId);
            Assert.False(bomb.IsExhausted); Assert.Contains(CardEquipmentKeywordNames.Deploy, bomb.Tags);
            Assert.Equal(destination, game.State.ObjectLocations[id].BattlefieldObjectId);
        });
        Assert.Equal(3, game.Events().Count(e => e.Event.Kind == "EQUIPMENT_TOKEN_CREATED"));
        await game.Replay();
    }

    [Theory]
    [InlineData("empty")] [InlineData("two")] [InlineData("unit")]
    [InlineData("missing")] [InlineData("opponent")]
    public async Task HoldDestinationIsMandatorySingleBattlefieldAndWrongChoicesAreAtomic(string branch)
    {
        var game = new Driver(Position()); await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!;
        string[] ids = branch switch { "empty" => [], "two" => ["F", "OTHER"],
            "unit" => ["HOST"], "missing" => ["missing"], _ => ["OTHER"] };
        var before = MatchStateHasher.Hash(game.State);
        var views = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State));
        var result = await new CoreRuleEngine().ResolveAsync(game.State,
            new("bad-held-equipment", branch == "opponent" ? "P2" : "P1", CommandTypes.ChooseCards),
            new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ids), default);
        Assert.False(result.Accepted); Assert.Empty(result.Events);
        Assert.Equal(before, MatchStateHasher.Hash(result.State));
        Assert.Equal(views, JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(result.State)));
    }

    [Fact]
    public async Task SourceReturningToHandDuringResponsesKeepsTheSkillWithoutPublishingPrivateIdentity()
    {
        var state = AttackDamageDistributionTests.Spell(Position(), "RETURN", "OGN·104/298", "P1");
        state = RadUnitEffectTestFixture.Place(state with {
            CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["RESPONSE-RUNE"] = new("RESPONSE-RUNE", cardNo: "OGN·007/298", ownerId: "P1", controllerId: "P1",
                    tags: [CardObjectTags.RuneCard, "COLOR:red"]) }
        }, "RESPONSE-RUNE", "P1", "BASE");
        var game = new Driver(state); await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OTHER"]));
        await game.Act("P1", new TapRuneCommand("RESPONSE-RUNE"));
        await game.Act("P1", new PlayCardCommand("RETURN", "OGN·104/298", ["HOST"])); await game.Top();
        Assert.Equal("HAND", game.State.ObjectLocations["HOST"].Zone);
        var publicStack = JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack);
        Assert.DoesNotContain("HOST", publicStack); Assert.DoesNotContain("unitSource", publicStack);
        var offset = game.Events().Length;
        await game.Top(); Assert.Equal(3, Bombs(game.State).Length);
        Assert.All(Bombs(game.State), id => Assert.DoesNotContain("HOST", id));
        foreach (var entry in game.Events().Skip(offset))
            Assert.False(entry.Event.Payload.TryGetValue("sourceObjectId", out var source) && Equals(source, "HOST"));
        await game.Replay();
    }

    [Theory]
    [InlineData("amount")] [InlineData("kind")] [InlineData("generation")] [InlineData("destination")] [InlineData("non-battlefield")]
    public async Task RecoveryRejectsForgedHeldEquipmentContextOrTargetBindings(string mutation)
    {
        var game = new Driver(Position()); await game.Act("P2", new EndTurnCommand());
        var choice = game.State.PendingCardChoice!;
        await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, ["OTHER"]));
        var item = Assert.Single(game.State.StackItems); var context = item.HeldContext!;
        var changed = mutation switch {
            "amount" => context with { Amount = 4 }, "kind" => context with { Kind = "UNIT_CHANNEL" },
            "generation" => context with { SourceGeneration = context.SourceGeneration + 1 }, _ => context };
        var forged = game.State with { StackItems = [item with { HeldContext = changed,
            TargetObjectIds = mutation == "non-battlefield" ? ["HOST"] : item.TargetObjectIds,
            TargetGenerations = mutation == "destination" ? new Dictionary<string, long>()
                : mutation == "non-battlefield" ? new Dictionary<string, long> { ["HOST"] = game.State.CardObjects["HOST"].ObjectGeneration }
                : item.TargetGenerations }] };
        Assert.NotEmpty(OfficialInsightAndSpellLockTests.Errors(forged));
    }

    [Theory]
    [InlineData("base")] [InlineData("opponent-turn")]
    public async Task AUnitOutsideTheHeldBattlefieldOrOnTheOtherTurnDoesNotEarnEquipment(string branch)
    {
        var state = Position();
        if (branch == "base") state = RadUnitEffectTestFixture.Place(state, "HOST", "P1", "BASE");
        else state = state with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        var game = new Driver(state); await game.Act(branch == "base" ? "P2" : "P1", new EndTurnCommand());
        Assert.DoesNotContain(game.State.StackItems, i => i.HeldContext?.Kind == "UNIT_CREATE_BATTLEFIELD_EQUIPMENT");
        Assert.Null(game.State.PendingCardChoice); Assert.Empty(Bombs(game.State)); await game.Replay();
    }

    internal static MatchState Position()
    {
        var state = RadUnitHoldTests.Position(Monkey);
        state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
            ["OTHER"] = OfficialGarenConquestBattlefieldCountTests.Printed("OTHER", "OGN·294/298", "P2") } };
        return RadUnitEffectTestFixture.Place(state, "OTHER", "P2", "BATTLEFIELD", "OTHER");
    }

    [Fact]
    public async Task BorrowedHeldTextAndBlueSentinelEachEarnTheirOwnDestinationChoice()
    {
        var state = Position() with { TurnPlayerId = "P1", ActivePlayerId = "P1" };
        state = OfficialHoldSequenceTests.AddUnit(AttachedRuleTextTests.AddSong(state, "SONG"), "BLUE", "UNL-087/219", "F", "P1");
        var game = new Driver(state);
        await game.Act("P1", new AssembleEquipmentCommand("SONG", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        await game.Act("P1", new EndTurnCommand()); await game.Act("P2", new EndTurnCommand());
        var count = 0;
        for (var step = 0; step < 60 && (game.State.TurnStartStep is not null
            || game.State.StackItems.Count > 0 || game.State.TriggerQueue.Count > 0); step++) {
            if (game.State.PendingCardChoice is { } choice) {
                Assert.Equal("TRIGGER_CONFIRMATION", choice.ChoiceWindow);
                await game.Act("P1", new ChooseCardsCommand(choice.ChoiceId, choice.ChoiceWindow, [count++ % 2 == 0 ? "OTHER" : "F"]));
            } else if (ResolutionResult.BuildPrompts(game.State).Values.FirstOrDefault(p =>
                p.Candidates?.Any(c => c.Action == CommandTypes.OrderTriggers && c.Enabled) == true) is { } order) {
                await game.Act(order.PlayerId, new OrderTriggersCommand(OrderedTriggerIds:
                    game.State.TriggerQueue.Where(t => t.ControllerId == order.PlayerId).Select(t => t.TriggerId).ToArray()));
            } else await game.Act(game.State.PriorityPlayerId!, new PassPriorityCommand());
        }
        Assert.Equal(4, count); Assert.Null(game.State.TurnStartStep);
        Assert.Equal(12, Bombs(game.State).Length);
        Assert.Equal(6, Bombs(game.State).Count(id => game.State.ObjectLocations[id].BattlefieldObjectId == "OTHER"));
        Assert.Equal(6, Bombs(game.State).Count(id => game.State.ObjectLocations[id].BattlefieldObjectId == "F"));
        await game.Replay();
    }
    private static string[] Bombs(MatchState state) => state.CardObjects.Values
        .Where(c => c.TokenFactoryCardNo == P6TokenFactoryCatalog.BombTokenCardNo).Select(c => c.ObjectId).ToArray();
}
