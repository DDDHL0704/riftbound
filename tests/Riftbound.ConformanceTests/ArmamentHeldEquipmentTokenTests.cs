using System.Text.Json;
using Riftbound.CardCatalog;
using Riftbound.Contracts;
using Riftbound.Engine;
using Xunit;
using Driver = Riftbound.ConformanceTests.VenBasicFlowSpellTests.Driver;

namespace Riftbound.ConformanceTests;

public sealed class ArmamentHeldEquipmentTokenTests
{
    private const string Book = "SFD·086/221";

    private static MatchState Board(bool attached = true, bool hostAtBase = false, int copies = 1, bool song = false)
    {
        var state = RadGearTestPosition.Put(RadGearTestPosition.Create(), "HOST", "OGN·001/298", "P1",
            hostAtBase ? "BASE" : "BATTLEFIELD", hostAtBase ? null : "BF");
        state = LocalDestructionRecallTests.At(state, "ALLY", "P1", "BATTLEFIELD", "BF");
        for (var i = 0; i < copies; i++)
        {
            var id = "BOOK" + i;
            state = RadGearTestPosition.Put(state, id, Book, "P1", hostAtBase ? "BASE" : "BATTLEFIELD", hostAtBase ? null : "BF");
            if (attached) state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                [id] = state.CardObjects[id] with { AttachedToObjectId = "HOST" } } };
        }
        if (song)
        {
            state = RadGearTestPosition.Put(state, "SONG", "SFD·059/221", "P1", "BATTLEFIELD", "BF");
            state = state with { CardObjects = new Dictionary<string, CardObjectState>(state.CardObjects) {
                ["SONG"] = state.CardObjects["SONG"] with { AttachedToObjectId = "HOST", AttachmentRevision = 1,
                    AttachedRuleText = new(new("HOST", 0), "OGN·001/298", 1, 0, 0) } } };
        }
        return CoreRuleEngine.RecomputeRuleDerivedKeywords(state with { TurnPlayerId = "P2", ActivePlayerId = "P2" });
    }

    private static IEnumerable<CardObjectState> Gold(MatchState state)
        => state.CardObjects.Values.Where(c => c.TokenFactoryCardNo == "UNL·T05");

    private static async Task Hold(Driver game)
    {
        await game.Act("P2", new EndTurnCommand());
        if (game.State.TriggerQueue.Count > 0)
            await game.Act("P1", new OrderTriggersCommand(OrderedTriggerIds: game.State.TriggerQueue.Select(t => t.TriggerId).ToArray()));
    }

    private static async Task CloseDuel(Driver game)
    {
        for (var passes = 0; game.State.SpellDuelState.IsActive; passes++)
        {
            Assert.InRange(passes, 0, 7);
            if (game.State.PriorityPlayerId is { } priority)
                await game.Act(priority, new PassPriorityCommand());
            else await game.Act(game.State.SpellDuelState.FocusPlayerId!, new PassFocusCommand());
        }
    }

    [Fact]
    public async Task ActualPlayAssemblyAndNaturalHoldGrantTwoDormantGoldOnlyAfterResponses()
    {
        var definition = OfficialArmamentCatalog.Find(Book)!;
        var parsed = Assert.Single(TriggerParser.Parse(definition.EffectText));
        Assert.Equal(TriggerKinds.UnitBattlefieldHeldCreateEquipment, parsed.Kind);
        Assert.Equal(2, parsed.CreatedTokenCount); Assert.True(parsed.CreatedTokenExhausted);
        Assert.Equal(TriggerTokenDestinations.OwnerBase, parsed.CreatedTokenDestination);
        var initial = RadGearTestPosition.Put(Board(attached: false) with { TurnPlayerId = "P1", ActivePlayerId = "P1" },
            "BOOK0", Book, "P1", "HAND");
        var game = new Driver(initial);
        await game.Act("P1", new PlayCardCommand("BOOK0", Book, [], Destination: "BASE"));
        await game.Act("P1", new AssembleEquipmentCommand("BOOK0", "HOST", ["ASSEMBLE_GREEN"])); await game.Top();
        await CloseDuel(game); await game.Act("P1", new EndTurnCommand()); await Hold(game);
        var item = Assert.Single(game.State.StackItems);
        Assert.Equal("HOLD", item.UnitEvent!.Timing); Assert.Equal(Book, item.CardNo);
        Assert.Equal("HOST", item.SourceObjectId); Assert.Equal("P1", item.ControllerId);
        Assert.True(item.UnitEvent.Origin!.ArmamentGranted);
        Assert.Empty(Gold(game.State)); Assert.Equal("CHANNEL", game.State.TurnStartStep);
        await game.Top();
        Assert.Equal(2, Gold(game.State).Count()); Assert.All(Gold(game.State), gold => {
            Assert.True(gold.IsExhausted); Assert.Equal("P1", gold.OwnerId);
            Assert.Contains(gold.ObjectId, game.State.PlayerZones["P1"].Base);
        });
        await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AnUnattachedBookOrAHostAtBaseDoesNotTrigger(bool hostAtBase)
    {
        var game = new Driver(Board(attached: hostAtBase, hostAtBase: hostAtBase)); await Hold(game);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.CardNo == Book);
        Assert.Empty(Gold(game.State)); await game.Replay();
    }

    [Fact]
    public async Task TwoBooksGrantTwoSkillsAndCopiedPrintedTextDoesNotCopyTheirLowerBoxes()
    {
        var game = new Driver(Board(copies: 2, song: true)); await Hold(game);
        Assert.Equal(2, game.State.StackItems.Count);
        Assert.Equal(2, game.State.StackItems.Select(i => i.UnitEvent!.Origin!.Equipment.ObjectId).Distinct().Count());
        await game.Top(); Assert.Equal(2, Gold(game.State).Count());
        await game.Top(); Assert.Equal(4, Gold(game.State).Count()); await game.Replay();
    }

    [Fact]
    public async Task NaturalConquestDoesNotEarnTheHoldSkillWithoutATimingAlias()
    {
        var game = new Driver(Board(hostAtBase: true) with { TurnPlayerId = "P1", ActivePlayerId = "P1" });
        await game.Act("P1", new MoveUnitCommand("HOST", "BASE", "BATTLEFIELD:OTHER", []));
        await game.Act("P1", new PassFocusCommand()); await game.Act("P2", new PassFocusCommand());
        Assert.Equal(1, game.State.PlayerScores["P1"]);
        Assert.DoesNotContain(game.State.StackItems, i => i.UnitEvent?.CardNo == Book);
        Assert.Empty(Gold(game.State)); await game.Replay();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReturningTheBookOrItsHostKeepsTheEarnedTokensAndHidesTheCurrentPrivateSource(bool host)
    {
        var no = host ? "OGN·104/298" : "SFD·135/221";
        var initial = RadGearTestPosition.Put(Board(), "RETURN", no, "P1", "HAND");
        if (!host) initial = RadGearTestPosition.Put(initial, "KAISA", "RAD·063/167", "P1", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Hold(game);
        await game.Act("P1", new PlayCardCommand("RETURN", no, [host ? "HOST" : "BOOK0"])); await game.Top();
        Assert.Contains(host ? "HOST" : "BOOK0", game.State.PlayerZones["P1"].Hand);
        Assert.DoesNotContain(host ? "\"HOST\"" : "\"BOOK0\"",
            JsonSerializer.Serialize(ResolutionResult.BuildSnapshots(game.State)["P2"].Stack));
        var before = game.Events().Length; await game.Top();
        Assert.Equal(2, Gold(game.State).Count());
        if (host) Assert.DoesNotContain(game.Events().Skip(before), e => Equals(e.Event.Payload.GetValueOrDefault("sourceObjectId"), "HOST"));
        await game.Replay();
    }

    [Fact]
    public async Task TheHoldingHostControlsTheSkillWhenItsBookHasADifferentOwnerAndController()
    {
        var initial = Board();
        initial = initial with { CardObjects = new Dictionary<string, CardObjectState>(initial.CardObjects) {
            ["BOOK0"] = initial.CardObjects["BOOK0"] with { OwnerId = "P2", ControllerId = "P2" } } };
        initial = LocalDestructionRecallTests.At(initial, "BOOK0", "P2", "BATTLEFIELD", "BF");
        var game = new Driver(initial); await Hold(game);
        Assert.Equal("P1", Assert.Single(game.State.StackItems).ControllerId);
        await game.Top(); Assert.Equal(2, Gold(game.State).Count());
        Assert.All(Gold(game.State), token => Assert.Equal("P1", token.OwnerId)); await game.Replay();
    }

    [Theory]
    [InlineData("equipment")] [InlineData("revision")] [InlineData("erase")] [InlineData("tick")]
    public async Task RecoveryBindsTheSkillToTheOriginalAcceptedHoldAndGrant(string field)
    {
        var game = new Driver(Board()); await Hold(game);
        var item = Assert.Single(game.State.StackItems); var context = item.UnitEvent!; var origin = context.Origin!;
        var changed = field switch {
            "equipment" => context with { Origin = origin with { Equipment = new("MISSING", 0) } },
            "revision" => context with { Origin = origin with { Revision = origin.Revision + 1 } },
            "erase" => context with { Origin = null },
            _ => context with { CapturedTick = context.CapturedTick + 1 }
        };
        var forged = game.State with { StackItems = [item with { UnitEvent = changed }] };
        Assert.NotEmpty(ArmamentGrantedCombatTriggerTests.Errors(game, forged));
        await game.Top(); await game.Replay();
    }
}
